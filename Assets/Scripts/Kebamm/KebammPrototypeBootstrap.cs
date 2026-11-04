using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kebamm
{
    /*
     * Kebamm minimal Suika-style prototype
     * ------------------------------------
     * CAPACITY RELEASE RULE:
     *   Polled every FixedUpdate with ONE shared timer: while at least one settled
     *   ball (speed <= 0.15 or sleeping, not preview/merging) is touching or above
     *   the cap band for 1.0 continuous seconds, the container floor/gate opens.
     *   Any moving or merging ball over the line, or none settled there, resets it. Balls then fall with gravity
     *   onto the monsters below and apply damage = (tier + 1) * 5.
     *   That floor open is the bombing run. It is not a separate trigger.
     *
     * SCORE SCREEN:
     *   After the bombing run starts (container floor opens), show the end
     *   panel once 5 continuous seconds pass with no gameplay event. Events
     *   that reset the timer: a monster impact, a real drop (SpawnBall with
     *   preview false), and a merge (SpawnMergedBall). Preview respawns do
     *   not reset it. Ball velocity does not gate the timer. Idle before the
     *   bombing run does not end the round. There is no countdown.
     *
     * LEVEL TEXT:
     *   Active entries come from Resources/Kebamm/Levels/game_level
     *   (KebammGameLevel). The HUD and end panel show the current entry's
     *   levelNumber ("Level 1" if the asset is missing). Next Level resets the
     *   session, then advances to the next active entry and stays on the last.
     *   Spawning does not use the level values yet.
     *
     * MAIN MENU:
     *   The game opens on KebammMainMenu with gameplay idle (no dropping, no
     *   preview ball, no idle timer, R ignored). START hides the menu and runs
     *   ResetSession at the current level. Settings is a placeholder panel
     *   with Quit and Back.
     *
     * PAUSE:
     *   Pause button top right of the HUD (KebammPauseMenu) or the app going to background
     *   sets Time.timeScale 0 and shows the Pause Menu (Resume, Sound, Music, Quit Level).
     *   Quit Level asks to confirm, then returns to the main menu without recording a result.
     *
     * CONTROLS:
     *   Mouse X     - aim drop position (clamped inside walls)
     *   LMB / Space - drop next queued ball (tier rolled from level ball tier range; tier 0 if unset)
     *   R           - reset container (close floor, clear balls, restore every monster HP)
     *
     * HOW TO RUN:
     *   Open SampleScene and press Play, then press START on the main menu. Auto-bootstrap spawns this component
     *   if missing. Or use menu: Kebamm > Add Prototype Bootstrap To Open Scene.
     */
    public class KebammPrototypeBootstrap : MonoBehaviour
    {
        public static KebammPrototypeBootstrap Instance { get; private set; }
        public static PhysicsMaterial2D SharedPhysicsMaterial { get; private set; }

        [Header("Layout")]
        // Container width/height and cap line come from Game Manager data (Resources/Kebamm/GameManager); defaults below if missing.
        public float ContainerWidth = 5.0f;
        public float ContainerHeight = 5.8f;
        public float CapsLineHeight = 4.95f;
        // Inner floor top (world y). The container grows upward from here as height changes.
        public float ContainerFloorY = -2.5f;
        public float WallThickness = 0.25f;
        public float MonsterY = -5.2f;
        public float GroundWidth = 8f;
        public float GroundThickness = 0.35f;
        public float SpawnInset = 0.35f;

        KebammContainer _container;
        KebammCapacityMonitor _capacity;
        KebammDropController _dropper;
        readonly List<KebammMonster> _monsters = new List<KebammMonster>();
        Transform _ballsRoot;
        Transform _worldRoot;
        bool _bombingRun;
        /// <summary>True once the container floor has opened (bombing run).</summary>
        public bool IsBombingRun => _bombingRun || (_container != null && _container.IsOpen);
        readonly List<KebammObstacle> _obstacles = new List<KebammObstacle>();
        // Building stacks as (centre X, half width incl. widest building) so monsters can keep clear of them.
        float _bombingIdleTimer;
        const float BombingIdleSeconds = 5f;
        KebammGameLevel _gameLevel;
        readonly List<KebammLevel> _activeLevels = new List<KebammLevel>();
        int _levelIndex;
        KebammMainMenu _mainMenu;

        public bool InMainMenu => _mainMenu != null && _mainMenu.IsVisible;

        /// <summary>True while the dev console is open; ball drops and the R reset ignore input.</summary>
        public static bool GameplayInputBlocked { get; set; }

        /// <summary>True while the level is paused (Time.timeScale 0): physics, drops, monsters, bombing run and timers freeze.</summary>
        public static bool IsPaused { get; private set; }
        public int CurrentLevelIndex => _levelIndex;

        public int CurrentLevelNumber => LevelNumberAt(_levelIndex);
        public bool HasNextLevel => _levelIndex + 1 < _activeLevels.Count;
        public int NextLevelNumber => HasNextLevel ? LevelNumberAt(_levelIndex + 1) : CurrentLevelNumber;

        static readonly KebammBallColour[] ColourPool =
        {
            KebammBallColour.Red,
            KebammBallColour.Blue,
            KebammBallColour.Green,
            KebammBallColour.Yellow
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            if (!IsGameScene(SceneManager.GetActiveScene()))
                return;

            if (FindFirstObjectByType<KebammPrototypeBootstrap>() != null)
                return;

            var go = new GameObject("KebammPrototype");
            go.AddComponent<KebammPrototypeBootstrap>();
        }

        // Auto-start only in game scenes: scenes listed in Build Settings (buildIndex >= 0, currently
        // Assets/Scenes/SampleScene.unity), excluding anything under a Samples folder.
        // Sample scenes such as Assets/Kebamm/Samples/SlimeWalk/SlimeWalkSample.unity and unsaved scenes stay untouched.
        static bool IsGameScene(Scene scene)
        {
            if (!scene.IsValid() || scene.buildIndex < 0)
                return false;
            return !scene.path.Replace('\\', '/').Contains("/Samples/");
        }

        void Awake()
        {
            // Android defaults to 30 fps; run at 60 on device.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Instance = this;
            GameplayInputBlocked = false;
            IsPaused = false;
            Time.timeScale = 1f;
            EnsurePhysicsMaterial();
            // Remote data: apply the cached copy (already accepted) at launch; the Gist is checked on the main menu.
            KebammRemoteData.Begin(this);
            KebammRemoteData.CommitPending();
            LoadLevels();
            KebammSaveSystem.Load();
            StartAtSavedLevel();
            BuildWorld();
        }

        // Start at the saved level (last played, else highest unlocked) if it is still an active level.
        void StartAtSavedLevel()
        {
            var progress = KebammSaveSystem.Data.progress;
            if (TryGetLevelIndex(progress.lastLevelPlayed, out int index) ||
                TryGetLevelIndex(progress.highestLevelUnlocked, out index))
                _levelIndex = index;
            Debug.Log($"[KebammSave] Starting at level {CurrentLevelNumber}");
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                // App going to background mid-level: pause; it stays paused (menu shown) when the app returns.
                if (CanPause && !IsPaused)
                    PauseGame("app background");
                KebammSaveSystem.Save();
            }
            else if (IsPaused)
            {
                Debug.Log("[Kebamm] App back from background - still paused (pause menu shown).");
            }
        }

        /// <summary>A level is being played: not on the main menu, score screen or door transition.</summary>
        public bool CanPause => !InMainMenu && !KebammDoorTransition.IsPlaying &&
                                (KebammScoreHud.Instance == null || !KebammScoreHud.Instance.IsEndVisible);

        public void PauseGame(string reason)
        {
            if (IsPaused || !CanPause)
                return;
            IsPaused = true;
            Time.timeScale = 0f;
            Debug.Log($"[Kebamm] Paused ({reason}) on level {CurrentLevelNumber}.");
            if (KebammPauseMenu.Instance != null)
                KebammPauseMenu.Instance.ShowMenu();
        }

        public void ResumeGame(string reason)
        {
            if (KebammPauseMenu.Instance != null)
                KebammPauseMenu.Instance.HideAll();
            if (!IsPaused)
                return;
            IsPaused = false;
            Time.timeScale = 1f;
            Debug.Log($"[Kebamm] Resumed ({reason}).");
        }

        /// <summary>Pause Menu Quit Level (confirmed): back to the main menu. No result is recorded and the save is not changed.</summary>
        public void QuitLevelToMenu()
        {
            Debug.Log($"[Kebamm] Quit Level confirmed on level {CurrentLevelNumber} - back to main menu (no result recorded, save unchanged).");
            ResumeGame("quit level");
            ResetSession();
            ShowMainMenu();
        }

        void OnApplicationQuit()
        {
            KebammSaveSystem.Save();
        }

        /// <summary>Debug console deletesave confirm: fresh save, level 1, back to the main menu, new username.</summary>
        public void DeleteSaveAndRestart()
        {
            KebammSaveSystem.DeleteAndReset();
            KebammAudio.Refresh(); // fresh save: Sound / Music back to ON
            _levelIndex = 0;
            if (KebammScoreHud.Instance != null)
                KebammScoreHud.Instance.RefreshLevel();
            ResetSession();
            ShowMainMenu(); // the online check creates the new username
            if (!KebammSaveSystem.HasUsername && KebammRemoteData.IsChecking)
                Debug.Log("[KebammSave] Username will be created when the online check returns.");
        }

        public int LastLevelNumber => LevelNumberAt(Mathf.Max(0, _activeLevels.Count - 1));

        void OnDestroy()
        {
            if (IsPaused)
            {
                IsPaused = false;
                Time.timeScale = 1f;
            }
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (InMainMenu || IsPaused)
                return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (!GameplayInputBlocked && kb != null && kb.rKey.wasPressedThisFrame)
                ResetSession();

            TickBombingRunIdle();
            KebammFrameSpikeLog.EndFrame();
        }

        static void EnsurePhysicsMaterial()
        {
            if (SharedPhysicsMaterial != null)
                return;
            SharedPhysicsMaterial = new PhysicsMaterial2D("KebammBallMat")
            {
                friction = 0.35f,
                bounciness = 0.05f
            };
        }

        const string BackgroundPath = "Kebamm/Environment/bg_zone1";
        // Pixel row (from the top of the 1125x2000 image) where the Zone 1 grass surface begins.
        const float BackgroundGroundRow = 1846f;

        // Cover-scales the background behind everything, anchoring its painted ground to groundTop.
        // Returns false (camera background colour stays) when the sprite is missing.
        bool BuildBackground(Camera cam, float groundTop)
        {
            if (cam == null) return false;
            var sprite = Resources.Load<Sprite>(BackgroundPath);
            if (sprite == null) return false;

            float viewH = cam.orthographicSize * 2f;
            float viewW = viewH * cam.aspect;
            Vector2 size = sprite.bounds.size;
            float groundFromTop = BackgroundGroundRow / sprite.pixelsPerUnit;
            float below = size.y - groundFromTop;
            float camY = cam.transform.position.y;
            float viewTop = camY + viewH * 0.5f;
            float viewBottom = camY - viewH * 0.5f;

            float s = Mathf.Max(viewW / size.x, viewH / size.y);
            s = Mathf.Max(s, (viewTop - groundTop) / groundFromTop);
            if (below > 0f) s = Mathf.Max(s, (groundTop - viewBottom) / below);

            var go = new GameObject("Background");
            go.transform.SetParent(_worldRoot, false);
            float centerY = groundTop + (groundFromTop - size.y * 0.5f) * s;
            go.transform.position = new Vector3(cam.transform.position.x, centerY, 0f);
            go.transform.localScale = new Vector3(s, s, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -100;
            return true;
        }

        KebammGameManagerData _gameManager;
        /// <summary>Active Game Manager data (remote override copy or the asset); null if missing.</summary>
        public KebammGameManagerData GameManagerData => _gameManager;
        bool _layoutDirty;

        void LoadGameManager()
        {
            _gameManager = KebammRemoteData.ForGameManager(Resources.Load<KebammGameManagerData>("Kebamm/GameManager"));
            if (_gameManager == null)
            {
                Debug.LogWarning("[Kebamm] GameManager missing from Resources; using bootstrap layout defaults.");
                return;
            }
            ContainerWidth = _gameManager.PillarGapWidth;
            ContainerHeight = _gameManager.PillarHeight;
            CapsLineHeight = _gameManager.CapsLineHeight;
            // Jar floor top sits a fixed distance above the platform (ground) top; the ground never moves.
            ContainerFloorY = (MonsterY - 1f) + GroundThickness * 0.5f + _gameManager.JarToPlatformDistance;
        }

        void OnEnable() { KebammGameManagerData.Changed += OnGameManagerChanged; KebammRemoteData.OfferReady += OnRemoteDataOffer; }
        void OnDisable() { KebammGameManagerData.Changed -= OnGameManagerChanged; KebammRemoteData.OfferReady -= OnRemoteDataOffer; }

        void OnGameManagerChanged(KebammGameManagerData data)
        {
            if (data == _gameManager)
                _layoutDirty = true; // applied in LateUpdate (OnValidate is not a safe place to move colliders)
        }

        void LateUpdate()
        {
            if (!_layoutDirty || _gameManager == null || _container == null)
                return;
            _layoutDirty = false;
            LoadGameManager();
            ApplyLayout();
        }

        /// <summary>Live re-layout from Game Manager data: walls, floor, cap line/band and launcher height + clamp.</summary>
        void ApplyLayout()
        {
            _container.SetFloorTop(ContainerFloorY);
            _container.Layout(ContainerWidth, ContainerHeight, WallThickness, CapsLineHeight);
            if (_capacity != null)
                _capacity.SetBand(_container.CapacityY, ContainerWidth * 0.95f);
            if (_dropper != null)
                _dropper.DropY = _container.TopY + 0.35f;
            Debug.Log($"[Kebamm] Game Manager layout: pillar height {ContainerHeight}, gap {ContainerWidth}, caps line {CapsLineHeight}, jar floor y {ContainerFloorY}");
        }

        void BuildWorld()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 6.2f;
                cam.transform.position = new Vector3(0f, -0.6f, -10f);
                cam.backgroundColor = new Color(0.12f, 0.13f, 0.18f);
            }

            _worldRoot = new GameObject("KebammWorld").transform;
            _worldRoot.SetParent(transform, false);

            var containerGo = new GameObject("Container");
            containerGo.transform.SetParent(_worldRoot, false);
            LoadGameManager();
            containerGo.transform.position = new Vector3(0f, ContainerFloorY + ContainerHeight * 0.5f, 0f);
            _container = containerGo.AddComponent<KebammContainer>();
            _container.Build(ContainerFloorY, ContainerWidth, ContainerHeight, WallThickness, CapsLineHeight);

            var capGo = new GameObject("CapacityTrigger");
            capGo.transform.SetParent(containerGo.transform, false);
            capGo.transform.position = new Vector3(0f, _container.CapacityY, 0f);
            capGo.AddComponent<BoxCollider2D>();
            _capacity = capGo.AddComponent<KebammCapacityMonitor>();
            _capacity.Init(_container, ContainerWidth * 0.95f, 0.35f);

            var ballsGo = new GameObject("Balls");
            ballsGo.transform.SetParent(_worldRoot, false);
            _ballsRoot = ballsGo.transform;

            // Warm caches used on every drop/merge so placing a ball never loads assets mid-play.
            KebammBall.PrewarmArt();
            KebammBallTierData.ImpactStrengthForPlayTier(0);

            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(_worldRoot, false);
            groundGo.transform.position = new Vector3(0f, MonsterY - 1f, 0f);
            var gSr = groundGo.AddComponent<SpriteRenderer>();
            gSr.sprite = KebammVisualFactory.SquareSprite;
            gSr.color = new Color(0.22f, 0.28f, 0.22f);
            gSr.sortingOrder = -1; // below building obstacles (0), which sit 1 layer behind monsters (1)
            groundGo.transform.localScale = new Vector3(GroundWidth, GroundThickness, 1f);
            KebammMonster.GroundCollider = groundGo.AddComponent<BoxCollider2D>();
            // Background art draws its own grass; keep the collider, hide the flat ground strip.
            if (BuildBackground(cam, (MonsterY - 1f) + GroundThickness * 0.5f))
                gSr.enabled = false;

            SpawnMonsters();

            var dropGo = new GameObject("DropController");
            dropGo.transform.SetParent(_worldRoot, false);
            _dropper = dropGo.AddComponent<KebammDropController>();
            float dropY = _container.TopY + 0.35f;
            _dropper.Init(this, _container, dropY);

            var hudGo = new GameObject("ScoreHud");
            hudGo.transform.SetParent(transform, false);
            hudGo.AddComponent<KebammScoreHud>().Build();
            KebammScore.ResetScores();

            var pauseGo = new GameObject("PauseMenu");
            pauseGo.transform.SetParent(transform, false);
            pauseGo.AddComponent<KebammPauseMenu>().Build();

            var menuGo = new GameObject("MainMenu");
            menuGo.transform.SetParent(transform, false);
            _mainMenu = menuGo.AddComponent<KebammMainMenu>();
            _mainMenu.Build();
            ShowMainMenu();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var consoleGo = new GameObject("DebugConsole");
            consoleGo.transform.SetParent(transform, false);
            consoleGo.AddComponent<KebammDebugConsole>().Build();
#endif

            Debug.Log("[Kebamm] Prototype ready. Press START, then aim with mouse, drop with LMB/Space, reset with R.");
        }

        void SpawnMonsters()
        {
            for (int i = 0; i < _monsters.Count; i++)
            {
                if (_monsters[i] != null)
                    Destroy(_monsters[i].gameObject);
            }
            _monsters.Clear();
            KebammLevel level = CurrentLevel;

            KebammLevelData levelData = Resources.Load<KebammLevelData>("Kebamm/LevelData");
            if (levelData == null)
            {
                levelData = ScriptableObject.CreateInstance<KebammLevelData>();
                Debug.LogWarning("[Kebamm] LevelData missing from Resources; using in-memory defaults.");
            }

            KebammMonsterData[] monsterDatas = Resources.LoadAll<KebammMonsterData>("Kebamm");
            if (monsterDatas != null)
                for (int i = 0; i < monsterDatas.Length; i++)
                    monsterDatas[i] = KebammRemoteData.ForMonsterTier(monsterDatas[i]); // remote override copy, or the asset
            if (monsterDatas == null || monsterDatas.Length == 0)
            {
                monsterDatas = CreateFallbackMonsters();
                Debug.LogWarning("[Kebamm] MonsterData missing from Resources; using in-memory defaults.");
            }

            System.Array.Sort(monsterDatas, (a, b) => a.id.CompareTo(b.id));

            // Monster tier range: the level's prepopulateMonsterTierMin/Max filters
            // monster data tiers (monster_01_basic_tier_01..03). Falls back to all monsters.
            if (level != null)
            {
                var inRange = System.Array.FindAll(monsterDatas, m => m != null &&
                    m.tier >= level.prepopulateMonsterTierMin && m.tier <= level.prepopulateMonsterTierMax);
                if (inRange.Length > 0)
                    monsterDatas = inRange;
                else
                    Debug.LogWarning($"[Kebamm] Level {level.levelNumber}: no monster data with tier {level.prepopulateMonsterTierMin}-{level.prepopulateMonsterTierMax}; using all monsters.");
            }

            // Count from the current level; global LevelData.asset only when there is no active level.
            int spawnCount = level != null
                ? Mathf.Max(0, level.monsterQty)
                : levelData.SpawnCountAtStart();
            Debug.Log($"[Kebamm] Level {CurrentLevelNumber}: spawning {spawnCount} monsters, colours [{string.Join(", ", CurrentColourPool())}]");
            float groundTop = (MonsterY - 1f) + GroundThickness * 0.5f;
            float halfPlatform = GroundWidth * 0.5f;
            float stackChance = level != null ? level.monsterStackChance : 0f;
            // Stacks may not reach the base underside (container floor minus its thickness).
            float stackCeiling = ContainerFloorY - WallThickness - 0.05f;
            var stacks = new List<List<KebammMonster>>();
            var openStacks = new List<List<KebammMonster>>();
            // Buildings first. Monsters may overlap them (drawn 1 layer in front, no collision between them).
            SpawnObstacles(level, groundTop, halfPlatform, stackCeiling);

            for (int i = 0; i < spawnCount; i++)
            {
                KebammMonsterData data = monsterDatas[Random.Range(0, monsterDatas.Length)];
                float size = data.ResolveSize(); // Monster Tier Data size (fallback 0.25/0.35/0.45)
                if (size < 0.05f)
                    size = 0.05f;

                KebammBallColour colour = RandomLevelColour();
                float reach = halfPlatform - size * 0.5f - SpawnInset;
                if (reach < 0f)
                    reach = 0f;
                // Ground spawns avoid existing stacks' footprints so physics never pushes a stack sideways at spawn.
                float x = Random.Range(-reach, reach);
                for (int attempt = 0; attempt < 12 && OverlapsStack(stacks, x, size); attempt++)
                    x = Random.Range(-reach, reach);
                // Square sprite pivot is center, so lift by half the uniform scale to rest on the ground.
                float y = groundTop + size * 0.5f;

                // Stacking: first monster never rolls. Pick random non-full stacks until one fits.
                List<KebammMonster> target = null;
                float bottomX = 0f;
                if (stacks.Count > 0 && Random.value < stackChance)
                {
                    openStacks.Clear();
                    for (int s = 0; s < stacks.Count; s++)
                        if (stacks[s].Count < MaxMonsterStack)
                            openStacks.Add(stacks[s]);
                    while (openStacks.Count > 0 && target == null)
                    {
                        int pick = Random.Range(0, openStacks.Count);
                        var candidate = openStacks[pick];
                        KebammMonster top = candidate[candidate.Count - 1];
                        float topY = MonsterTopY(top);
                        float candidateY = topY + StackGap + size * 0.5f;
                        // Whole body must stay inside the platform with the SpawnInset margin (never at the edge).
                        float stackX = MonsterCenterX(candidate[0]);
                        if (candidateY + size * 0.5f <= stackCeiling && Mathf.Abs(stackX) <= halfPlatform - size * 0.5f - SpawnInset)
                        {
                            target = candidate;
                            // Same X as the bottom monster's collider centre (new monster's collider offset is 0); only Y rises.
                            bottomX = stackX;
                            x = bottomX;
                            y = candidateY;
                        }
                        openStacks.RemoveAt(pick);
                    }
                }

                KebammMonster monster = SpawnMonster(data, size, colour, new Vector3(x, y, 0f));
                _monsters.Add(monster);
                if (target != null)
                {
                    target.Add(monster);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[Kebamm] Stack x check: bottom={bottomX:0.000} new={MonsterCenterX(monster):0.000}");
#endif
                }
                else
                    stacks.Add(new List<KebammMonster> { monster });
                Debug.Log($"[Kebamm] Spawned {monster.name} tier={data.tier} hp={data.hp} colour={colour} size={size} x={x:0.00} y={y:0.00} stack={(target != null ? target.Count : 1)}");
            }
        }

        const int MaxMonsterStack = 5;
        const int MaxObstacleStack = 3;

        class ObstacleStack
        {
            public readonly List<KebammObstacle> Items = new List<KebammObstacle>();
            public float X, HalfWidth, Spacing;
        }

        static bool OverlapsObstacleStacks(List<ObstacleStack> stacks, ObstacleStack self, float x, float halfWidth, float spacing)
        {
            for (int s = 0; s < stacks.Count; s++)
            {
                ObstacleStack o = stacks[s];
                if (o == self)
                    continue;
                if (Mathf.Abs(x - o.X) < halfWidth + o.HalfWidth + Mathf.Max(0.02f, Mathf.Max(spacing, o.Spacing)))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Building obstacles: only when the level has obstaclePopulate on and prepopulateObstacleQty &gt; 0, using
        /// Obstacle Data shapes that have hp, width and height filled. Stacks only on buildings (max 3; nothing on a
        /// triangle's tip), never overlap each other, X from prepopulateObstaclePositionRangeX within the platform.
        /// </summary>
        void SpawnObstacles(KebammLevel level, float groundTop, float halfPlatform, float stackCeiling)
        {
            for (int i = 0; i < _obstacles.Count; i++)
                if (_obstacles[i] != null)
                    Destroy(_obstacles[i].gameObject);
            _obstacles.Clear();

            if (level == null || !level.obstaclePopulate || level.prepopulateObstacleQty <= 0)
                return;

            var shapes = new List<KebammObstacleData>();
            foreach (KebammObstacleData d in Resources.LoadAll<KebammObstacleData>("Kebamm"))
            {
                if (d == null || !d.IsUsable)
                    continue;
                if (level.allowedObstacleShapes != null && level.allowedObstacleShapes.Count > 0 && !level.allowedObstacleShapes.Contains(d.shape))
                    continue;
                shapes.Add(d);
            }
            if (shapes.Count == 0)
            {
                Debug.LogWarning($"[Kebamm] Level {level.levelNumber}: obstacles on but no Obstacle Data shape has hp, width and height filled; none spawned.");
                return;
            }

            // Safe area: whole building stays inside the platform with the SpawnInset margin (never at the edge).
            float safeHalf = Mathf.Max(0f, halfPlatform - SpawnInset);
            float rangeMin = Mathf.Max(-safeHalf, level.prepopulateObstaclePositionRangeX.x);
            float rangeMax = Mathf.Min(safeHalf, level.prepopulateObstaclePositionRangeX.y);
            var stacks = new List<ObstacleStack>();
            var open = new List<ObstacleStack>();

            for (int i = 0; i < level.prepopulateObstacleQty; i++)
            {
                KebammObstacleData data = shapes[Random.Range(0, shapes.Count)];
                float w = data.width, h = data.height, half = w * 0.5f;
                ObstacleStack target = null;
                float x = 0f, y = 0f;

                if (stacks.Count > 0 && Random.value < level.obstacleStackChance)
                {
                    open.Clear();
                    for (int s = 0; s < stacks.Count; s++)
                    {
                        var st = stacks[s];
                        if (st.Items.Count < MaxObstacleStack && st.Items[st.Items.Count - 1].Shape != KebammObstacleShape.Triangle)
                            open.Add(st);
                    }
                    while (open.Count > 0 && target == null)
                    {
                        int pick = Random.Range(0, open.Count);
                        var st = open[pick];
                        float cy = st.Items[st.Items.Count - 1].TopY + StackGap + h * 0.5f;
                        float newHalf = Mathf.Max(st.HalfWidth, half);
                        if (cy + h * 0.5f <= stackCeiling && Mathf.Abs(st.X) <= safeHalf - half
                            && !OverlapsObstacleStacks(stacks, st, st.X, newHalf, Mathf.Max(st.Spacing, data.stackSpacing)))
                        {
                            target = st;
                            x = st.X;
                            y = cy;
                        }
                        open.RemoveAt(pick);
                    }
                }

                if (target == null)
                {
                    float lo = rangeMin + half, hi = rangeMax - half;
                    if (hi < lo)
                    {
                        lo = -safeHalf + half;
                        hi = safeHalf - half;
                    }
                    if (hi < lo)
                        continue; // wider than the platform
                    bool found = false;
                    for (int attempt = 0; attempt < 12 && !found; attempt++)
                    {
                        x = Random.Range(lo, hi);
                        found = !OverlapsObstacleStacks(stacks, null, x, half, data.stackSpacing);
                    }
                    if (!found)
                    {
                        Debug.LogWarning($"[Kebamm] Obstacle {i + 1}/{level.prepopulateObstacleQty} ({data.shape}) skipped: no free spot without overlapping.");
                        continue;
                    }
                    y = groundTop + h * 0.5f;
                }

                KebammObstacle ob = SpawnObstacle(data, new Vector3(x, y, 0f), halfPlatform);
                _obstacles.Add(ob);
                if (target == null)
                {
                    target = new ObstacleStack { X = x };
                    stacks.Add(target);
                }
                target.Items.Add(ob);
                target.HalfWidth = Mathf.Max(target.HalfWidth, half);
                target.Spacing = Mathf.Max(target.Spacing, data.stackSpacing);
            }

            Debug.Log($"[Kebamm] Level {level.levelNumber}: spawned {_obstacles.Count} obstacles in {stacks.Count} stacks");
        }

        KebammObstacle SpawnObstacle(KebammObstacleData data, Vector3 position, float halfPlatform)
        {
            var go = new GameObject("Obstacle");
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(data.width, data.height, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 0; // 1 layer behind monsters (1)
            Collider2D col;
            if (data.shape == KebammObstacleShape.Triangle)
            {
                sr.sprite = KebammVisualFactory.TriangleSprite;
                var poly = go.AddComponent<PolygonCollider2D>();
                poly.points = new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0f, 0.5f) };
                col = poly;
            }
            else
            {
                sr.sprite = KebammVisualFactory.SquareSprite;
                col = go.AddComponent<BoxCollider2D>();
            }
            col.sharedMaterial = KebammObstacle.SharedMaterial;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
            rb.linearDamping = 0.2f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.sharedMaterial = KebammObstacle.SharedMaterial;
            var ob = go.AddComponent<KebammObstacle>();
            float edge = Mathf.Max(0f, halfPlatform - data.width * 0.5f);
            ob.Init(data, -edge, edge);
            return ob;
        }
        const float StackGap = 0.01f;

        static float MonsterCenterX(KebammMonster m)
        {
            var col = m.GetComponent<BoxCollider2D>();
            return col != null ? m.transform.TransformPoint(col.offset).x : m.transform.position.x;
        }

        static float MonsterWidth(KebammMonster m)
        {
            var col = m.GetComponent<BoxCollider2D>();
            return (col != null ? col.size.x : 1f) * Mathf.Abs(m.transform.lossyScale.x);
        }

        static bool OverlapsStack(List<List<KebammMonster>> stacks, float x, float size)
        {
            for (int s = 0; s < stacks.Count; s++)
            {
                float w = 0f;
                for (int k = 0; k < stacks[s].Count; k++)
                    w = Mathf.Max(w, MonsterWidth(stacks[s][k]));
                if (Mathf.Abs(x - MonsterCenterX(stacks[s][0])) < (w + size) * 0.5f + 0.02f)
                    return true;
            }
            return false;
        }

        // Top edge from the BoxCollider2D (size/offset in local space), valid before physics syncs.
        static float MonsterTopY(KebammMonster m)
        {
            Transform t = m.transform;
            var col = m.GetComponent<BoxCollider2D>();
            if (col == null)
                return t.position.y + t.lossyScale.y * 0.5f;
            return t.position.y + (col.offset.y + col.size.y * 0.5f) * t.lossyScale.y;
        }

        KebammMonster SpawnMonster(KebammMonsterData data, float size, KebammBallColour colour, Vector3 position)
        {
            var monsterGo = new GameObject("Monster");
            monsterGo.transform.SetParent(_worldRoot, false);
            monsterGo.transform.position = position;
            monsterGo.transform.localScale = new Vector3(size, size, 1f);
            var mSr = monsterGo.AddComponent<SpriteRenderer>();
            mSr.sprite = KebammVisualFactory.SquareSprite;
            mSr.sortingOrder = 1;
            monsterGo.AddComponent<BoxCollider2D>();
            var mRb = monsterGo.AddComponent<Rigidbody2D>();
            // Dynamic so ball impacts can knock monsters around (see KebammMonster.ApplyImpact).
            mRb.bodyType = RigidbodyType2D.Dynamic;
            mRb.simulated = true;
            mRb.gravityScale = 1.4f;
            mRb.mass = 2f;
            mRb.linearDamping = 0.2f;
            mRb.angularDamping = 3f;
            mRb.interpolation = RigidbodyInterpolation2D.Interpolate;
            mRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            mRb.sharedMaterial = KebammMonster.SharedMaterial;
            monsterGo.GetComponent<BoxCollider2D>().sharedMaterial = KebammMonster.SharedMaterial;
            var monster = monsterGo.AddComponent<KebammMonster>();
            float hp = data != null ? data.hp : 100f;
            int hitPts;
            int destroyedPts;
            if (data != null)
                data.ResolveScore(out hitPts, out destroyedPts);
            else
                KebammMonsterData.FallbackPoints(1, out hitPts, out destroyedPts);
            monster.Init(hp, colour, hitPts, destroyedPts);
            float edge = Mathf.Max(0f, GroundWidth * 0.5f - size * 0.5f);
            monster.SetBounds(-edge, edge);
            if (data != null && !string.IsNullOrEmpty(data.name))
                monsterGo.name = data.name;
            return monster;
        }

        static KebammMonsterData[] CreateFallbackMonsters()
        {
            int[] ids = { 0, 1, 2 };
            float[] hps = { 100f, 200f, 500f }; // Notion Monster Tier Data hp
            int[] tiers = { 1, 2, 3 };
            int[] hitPts = { 13, 26, 52 };
            int[] destroyedPts = { 250, 500, 1000 };
            string[] names =
            {
                "monster_01_basic_tier_01",
                "monster_01_basic_tier_02",
                "monster_01_basic_tier_03"
            };

            var result = new KebammMonsterData[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                KebammMonsterData data = ScriptableObject.CreateInstance<KebammMonsterData>();
                data.id = ids[i];
                data.hp = hps[i];
                data.tier = tiers[i];
                data.hitPts = hitPts[i];
                data.destroyedPts = destroyedPts[i];
                data.name = names[i];
                result[i] = data;
            }

            return result;
        }

        public KebammBallColour NextQueuedColour()
        {
            return RandomLevelColour();
        }

        /// <summary>Play tier (GDD tier - 1) of the next ball, rolled from the level's ball tier range and weights.</summary>
        public int NextQueuedTier()
        {
            KebammLevel level = CurrentLevel;
            return level != null ? level.RollDropPlayTier() : 0;
        }

        KebammLevel CurrentLevel =>
            _levelIndex >= 0 && _levelIndex < _activeLevels.Count ? _activeLevels[_levelIndex] : null;

        /// <summary>The level's prepopulateBallColours, or the full ColourPool if none are set.</summary>
        KebammBallColour[] CurrentColourPool()
        {
            KebammLevel level = CurrentLevel;
            if (level != null && level.prepopulateBallColours != null && level.prepopulateBallColours.Count > 0)
                return level.prepopulateBallColours.ToArray();
            return ColourPool;
        }

        KebammBallColour RandomLevelColour()
        {
            KebammBallColour[] pool = CurrentColourPool();
            return pool[Random.Range(0, pool.Length)];
        }

        public KebammBall SpawnBall(Vector3 position, KebammBallColour colour, int tier, bool preview)
        {
            var go = new GameObject(preview ? "PreviewBall" : "Ball");
            go.transform.SetParent(preview ? _dropper.transform : _ballsRoot, true);
            go.transform.position = position;
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<CircleCollider2D>();
            go.AddComponent<Rigidbody2D>();
            var ball = go.AddComponent<KebammBall>();
            ball.Configure(colour, tier, preview);
            if (!preview)
                ResetBombingIdleTimer();
            return ball;
        }

        /// <summary>
        /// Turns the held preview ball into a live ball in place (same object, no destroy/re-create),
        /// exactly as SpawnBall(position, colour, tier, preview: false) would configure it.
        /// </summary>
        public KebammBall ReleasePreview(KebammBall preview)
        {
            if (preview == null)
                return null;
            preview.transform.SetParent(_ballsRoot, true);
            preview.Configure(preview.Colour, preview.Tier, preview: false);
            ResetBombingIdleTimer();
            KebammFrameSpikeLog.Drops++;
            return preview;
        }

        public void SpawnMergedBall(Vector3 position, KebammBallColour colour, int tier, float startDiameter = -1f, bool inPlay = true)
        {
            var ball = SpawnBall(position, colour, tier, preview: false);
            if (ball != null && startDiameter > 0f)
                ball.BeginGrow(startDiameter);
            var rb = ball.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            _capacity?.NotifyMerge(ball, inPlay);
            KebammFrameSpikeLog.Merges++;
            Debug.Log($"[Kebamm] Merged -> {colour} T{tier} at {position}");
            KebammScore.AddMerge(KebammBallTierData.MergePointsForPlayTier(tier));
        }

        public void SetDroppingEnabled(bool enabled)
        {
            _dropper?.SetDroppingEnabled(enabled);
        }

        public void NotifyMonsterImpact()
        {
            ResetBombingIdleTimer();
        }

        void ResetBombingIdleTimer()
        {
            if (!_bombingRun)
                return;

            _bombingIdleTimer = 0f;
        }

        void TickBombingRunIdle()
        {
            if (!_bombingRun && _container != null && _container.IsOpen)
            {
                _bombingRun = true;
                _bombingIdleTimer = 0f;
                Debug.Log("[Kebamm] Bombing run started from capacity release. Score screen after 5s with no gameplay event.");
            }

            if (!_bombingRun)
                return;

            if (KebammScoreHud.Instance != null && KebammScoreHud.Instance.IsEndVisible)
                return;

            _bombingIdleTimer += Time.deltaTime;
            if (_bombingIdleTimer < BombingIdleSeconds)
                return;

            if (KebammScoreHud.Instance != null)
                KebammScoreHud.Instance.ShowEnd();
        }

        void LoadLevels()
        {
            _activeLevels.Clear();
            _levelIndex = 0;
            _gameLevel = Resources.Load<KebammGameLevel>("Kebamm/Levels/game_level");
            if (_gameLevel == null)
            {
                Debug.LogWarning("[Kebamm] game_level missing from Resources/Kebamm/Levels; showing Level 1.");
                return;
            }

            foreach (KebammLevel level in _gameLevel.GetActiveLevels())
                _activeLevels.Add(KebammRemoteData.ForLevel(level));
            if (_activeLevels.Count == 0)
                Debug.LogWarning("[Kebamm] game_level has no active levels; showing Level 1.");
        }

        int LevelNumberAt(int index)
        {
            if (index < 0 || index >= _activeLevels.Count || _activeLevels[index] == null)
                return 1;
            return _activeLevels[index].levelNumber;
        }

        public void AdvanceLevel()
        {
            if (HasNextLevel)
                _levelIndex++; // the save was already written when the score screen was shown
            Debug.Log($"[Kebamm] Current level: {CurrentLevelNumber} (entry {_levelIndex + 1}/{Mathf.Max(1, _activeLevels.Count)})");
            if (KebammScoreHud.Instance != null)
                KebammScoreHud.Instance.RefreshLevel();
        }

        /// <summary>Active level numbers, e.g. "1, 2, 3".</summary>
        public string LevelNumbersText
        {
            get
            {
                if (_activeLevels.Count == 0)
                    return "1";
                var numbers = new List<string>();
                for (int i = 0; i < _activeLevels.Count; i++)
                    numbers.Add(LevelNumberAt(i).ToString());
                return string.Join(", ", numbers);
            }
        }

        /// <summary>Index in the active level list for a level number.</summary>
        public bool TryGetLevelIndex(int levelNumber, out int index)
        {
            for (int i = 0; i < _activeLevels.Count; i++)
            {
                if (_activeLevels[i] != null && _activeLevels[i].levelNumber == levelNumber)
                {
                    index = i;
                    return true;
                }
            }
            index = 0;
            return _activeLevels.Count == 0 && levelNumber == 1;
        }

        /// <summary>Starts the level at index right away (from the menu or mid-game): fresh monsters, score and state.</summary>
        public void JumpToLevelIndex(int index)
        {
            if (_activeLevels.Count > 0)
                _levelIndex = Mathf.Clamp(index, 0, _activeLevels.Count - 1);
            Debug.Log($"[Kebamm] Jump to level {CurrentLevelNumber} (entry {_levelIndex + 1}/{Mathf.Max(1, _activeLevels.Count)})");
            if (KebammScoreHud.Instance != null)
                KebammScoreHud.Instance.RefreshLevel();
            if (InMainMenu)
                StartGameFromMenu();
            else
                ResetSession();
        }

        public void ShowMainMenu()
        {
            ResumeGame("main menu");
            _bombingRun = false;
            _bombingIdleTimer = 0f;
            _dropper?.SetDroppingEnabled(false);
            _dropper?.ClearPreview();
            if (_mainMenu != null)
                _mainMenu.Show();
            ApplyRemoteDataIfPending();
            KebammRemoteData.CheckOnline(this); // launch + every return to the menu; newer data is offered, not applied
        }

        void OnRemoteDataOffer()
        {
            if (InMainMenu)
                _mainMenu.ShowUpdatePrompt();
        }

        /// <summary>Player said Yes on the update prompt: cache and apply the new data (main menu only).</summary>
        public void AcceptRemoteData()
        {
            KebammRemoteData.AcceptOffer();
            ApplyRemoteDataIfPending();
        }

        /// <summary>Applies downloaded remote data. Only at launch or while the main menu is up, never mid-level.</summary>
        public void ApplyRemoteDataIfPending()
        {
            if (!KebammRemoteData.HasPending || !InMainMenu)
                return;
            if (!KebammRemoteData.CommitPending())
                return;
            int index = _levelIndex;
            int number = CurrentLevelNumber;
            LoadLevels();
            // Keep the same level number if it still exists (saves are keyed by level number).
            if (TryGetLevelIndex(number, out int sameLevel))
                _levelIndex = sameLevel;
            else
                _levelIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _activeLevels.Count - 1));
            LoadGameManager(); // the old remote copy was destroyed by CommitPending; the console reads this instance
            if (_container != null)
                ApplyLayout();
            if (_mainMenu != null)
                _mainMenu.Show();
        }

        public void StartGameFromMenu()
        {
            if (_mainMenu != null)
                _mainMenu.Hide();
            ResetSession();
            Debug.Log($"[Kebamm] START pressed - playing Level {CurrentLevelNumber}");
        }

        public void ResetSession()
        {
            ResumeGame("session reset");
            _bombingRun = false;
            _bombingIdleTimer = 0f;
            if (KebammScoreHud.Instance != null)
                KebammScoreHud.Instance.HideEnd();
            KebammScore.ResetScores();

            if (_ballsRoot != null)
            {
                for (int i = _ballsRoot.childCount - 1; i >= 0; i--)
                    Destroy(_ballsRoot.GetChild(i).gameObject);
            }

            _capacity?.ResetTimers();
            _container?.CloseFloor();
            // Respawn so count, colours and tiers follow the current level.
            SpawnMonsters();

            _dropper?.ClearPreview();
            _dropper?.SetDroppingEnabled(true);
            _dropper?.SpawnPreview();
            Debug.Log("[Kebamm] Session reset - container rebuilt for another drop round");
        }
    }
}