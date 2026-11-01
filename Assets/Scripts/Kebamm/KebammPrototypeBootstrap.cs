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
     * CONTROLS:
     *   Mouse X     - aim drop position (clamped inside walls)
     *   LMB / Space - drop next queued ball (tier 0, random of 4 colours)
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
        public float ContainerWidth = 5.0f;
        public float ContainerHeight = 5.8f;
        // Inner floor top (world y). The container grows upward from here as height changes.
        public float ContainerFloorY = -2.5f;
        public float WallThickness = 0.25f;
        public float CapacityFromTop = 0.85f;
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
        float _bombingIdleTimer;
        const float BombingIdleSeconds = 5f;
        KebammGameLevel _gameLevel;
        readonly List<KebammLevel> _activeLevels = new List<KebammLevel>();
        int _levelIndex;
        KebammMainMenu _mainMenu;

        public bool InMainMenu => _mainMenu != null && _mainMenu.IsVisible;

        /// <summary>True while the dev console is open; ball drops and the R reset ignore input.</summary>
        public static bool GameplayInputBlocked { get; set; }
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
            Instance = this;
            GameplayInputBlocked = false;
            EnsurePhysicsMaterial();
            LoadLevels();
            BuildWorld();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (InMainMenu)
                return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (!GameplayInputBlocked && kb != null && kb.rKey.wasPressedThisFrame)
                ResetSession();

            TickBombingRunIdle();
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
            containerGo.transform.position = new Vector3(0f, ContainerFloorY + ContainerHeight * 0.5f, 0f);
            _container = containerGo.AddComponent<KebammContainer>();
            _container.Build(ContainerWidth, ContainerHeight, WallThickness, CapacityFromTop);

            var capGo = new GameObject("CapacityTrigger");
            capGo.transform.SetParent(containerGo.transform, false);
            capGo.transform.position = new Vector3(0f, _container.CapacityY, 0f);
            capGo.AddComponent<BoxCollider2D>();
            _capacity = capGo.AddComponent<KebammCapacityMonitor>();
            _capacity.Init(_container, ContainerWidth * 0.95f, 0.35f);

            var ballsGo = new GameObject("Balls");
            ballsGo.transform.SetParent(_worldRoot, false);
            _ballsRoot = ballsGo.transform;

            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(_worldRoot, false);
            groundGo.transform.position = new Vector3(0f, MonsterY - 1f, 0f);
            var gSr = groundGo.AddComponent<SpriteRenderer>();
            gSr.sprite = KebammVisualFactory.SquareSprite;
            gSr.color = new Color(0.22f, 0.28f, 0.22f);
            gSr.sortingOrder = 0;
            groundGo.transform.localScale = new Vector3(GroundWidth, GroundThickness, 1f);
            groundGo.AddComponent<BoxCollider2D>();
            // Background art draws its own grass; keep the collider, hide the flat ground strip.
            if (BuildBackground(cam, (MonsterY - 1f) + GroundThickness * 0.5f))
                gSr.enabled = false;

            SpawnMonsters();

            var dropGo = new GameObject("DropController");
            dropGo.transform.SetParent(_worldRoot, false);
            _dropper = dropGo.AddComponent<KebammDropController>();
            float dropY = containerGo.transform.position.y + ContainerHeight * 0.5f + 0.35f;
            _dropper.Init(this, _container, dropY);

            var hudGo = new GameObject("ScoreHud");
            hudGo.transform.SetParent(transform, false);
            hudGo.AddComponent<KebammScoreHud>().Build();
            KebammScore.ResetScores();

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

            KebammGameData gameData = Resources.Load<KebammGameData>("Kebamm/GameData");
            if (gameData == null)
            {
                gameData = ScriptableObject.CreateInstance<KebammGameData>();
                Debug.LogWarning("[Kebamm] GameData missing from Resources; using in-memory defaults.");
            }

            KebammLevelData levelData = Resources.Load<KebammLevelData>("Kebamm/LevelData");
            if (levelData == null)
            {
                levelData = ScriptableObject.CreateInstance<KebammLevelData>();
                Debug.LogWarning("[Kebamm] LevelData missing from Resources; using in-memory defaults.");
            }

            KebammMonsterData[] monsterDatas = Resources.LoadAll<KebammMonsterData>("Kebamm");
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

            for (int i = 0; i < spawnCount; i++)
            {
                KebammMonsterData data = monsterDatas[Random.Range(0, monsterDatas.Length)];
                float size = gameData.SizeForTier(data.tier);
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
                        // Must also sit inside this monster's own edge bounds, or the edge clamp would shift its X.
                        float stackX = MonsterCenterX(candidate[0]);
                        if (candidateY + size * 0.5f <= stackCeiling && Mathf.Abs(stackX) <= halfPlatform - size * 0.5f)
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
            float[] hps = { 100f, 200f, 300f };
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

        public void SpawnMergedBall(Vector3 position, KebammBallColour colour, int tier)
        {
            var ball = SpawnBall(position, colour, tier, preview: false);
            var rb = ball.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            _capacity?.NotifyMerge(ball);
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

            _activeLevels.AddRange(_gameLevel.GetActiveLevels());
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
                _levelIndex++;
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
            _bombingRun = false;
            _bombingIdleTimer = 0f;
            _dropper?.SetDroppingEnabled(false);
            _dropper?.ClearPreview();
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