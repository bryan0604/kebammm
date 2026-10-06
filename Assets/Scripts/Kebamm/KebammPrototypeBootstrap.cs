using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    /*
     * Kebamm minimal Suika-style prototype
     * ------------------------------------
     * CAPACITY RELEASE RULE:
     *   Any ball whose collider remains overlapping the capacity trigger for 1.0
     *   continuous seconds WHILE settled (Rigidbody2D.linearVelocity magnitude
     *   <= 0.15) opens the container floor/gate. Balls then fall with gravity
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
        public float ContainerWidth = 3.2f;
        public float ContainerHeight = 5.2f;
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
            if (FindFirstObjectByType<KebammPrototypeBootstrap>() != null)
                return;

            var go = new GameObject("KebammPrototype");
            go.AddComponent<KebammPrototypeBootstrap>();
        }

        void Awake()
        {
            Instance = this;
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
            if (kb != null && kb.rKey.wasPressedThisFrame)
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
            containerGo.transform.position = new Vector3(0f, 0.4f, 0f);
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

            Debug.Log("[Kebamm] Prototype ready. Press START, then aim with mouse, drop with LMB/Space, reset with R.");
        }

        void SpawnMonsters()
        {
            _monsters.Clear();

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

            int spawnCount = levelData.SpawnCountAtStart();
            float groundTop = (MonsterY - 1f) + GroundThickness * 0.5f;
            float halfPlatform = GroundWidth * 0.5f;

            for (int i = 0; i < spawnCount; i++)
            {
                KebammMonsterData data = monsterDatas[Random.Range(0, monsterDatas.Length)];
                float size = gameData.SizeForTier(data.tier);
                if (size < 0.05f)
                    size = 0.05f;

                KebammBallColour colour = ColourPool[Random.Range(0, ColourPool.Length)];
                float reach = halfPlatform - size * 0.5f - SpawnInset;
                if (reach < 0f)
                    reach = 0f;
                float x = Random.Range(-reach, reach);
                // Square sprite pivot is center, so lift by half the uniform scale to rest on the ground.
                float y = groundTop + size * 0.5f;

                KebammMonster monster = SpawnMonster(data, size, colour, new Vector3(x, y, 0f));
                _monsters.Add(monster);
                Debug.Log($"[Kebamm] Spawned {monster.name} tier={data.tier} hp={data.hp} colour={colour} size={size} x={x:0.00}");
            }
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
            mRb.bodyType = RigidbodyType2D.Kinematic;
            mRb.simulated = true;
            var monster = monsterGo.AddComponent<KebammMonster>();
            float hp = data != null ? data.hp : 100f;
            int hitPts;
            int destroyedPts;
            if (data != null)
                data.ResolveScore(out hitPts, out destroyedPts);
            else
                KebammMonsterData.FallbackPoints(1, out hitPts, out destroyedPts);
            monster.Init(hp, colour, hitPts, destroyedPts);
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
            return ColourPool[Random.Range(0, ColourPool.Length)];
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
            for (int i = 0; i < _monsters.Count; i++)
            {
                if (_monsters[i] != null)
                    _monsters[i].ResetHealth();
            }

            _dropper?.ClearPreview();
            _dropper?.SetDroppingEnabled(true);
            _dropper?.SpawnPreview();
            Debug.Log("[Kebamm] Session reset - container rebuilt for another drop round");
        }
    }
}