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
     *   onto the monster below and apply damage = (tier + 1) * 5.
     *
     * CONTROLS:
     *   Mouse X     — aim drop position (clamped inside walls)
     *   LMB / Space — drop next queued ball (tier 0, random of 4 colours)
     *   R           — reset container (close floor, clear balls, restore monster HP)
     *
     * HOW TO RUN:
     *   Open SampleScene and press Play. Auto-bootstrap spawns this component
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
        public float MonsterHealth = 100f;

        KebammContainer _container;
        KebammCapacityMonitor _capacity;
        KebammDropController _dropper;
        KebammMonster _monster;
        Transform _ballsRoot;
        Transform _worldRoot;

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
            BuildWorld();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
                ResetSession();
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

            var monsterGo = new GameObject("Monster");
            monsterGo.transform.SetParent(_worldRoot, false);
            monsterGo.transform.position = new Vector3(0f, MonsterY, 0f);
            monsterGo.transform.localScale = new Vector3(2.4f, 1.1f, 1f);
            var mSr = monsterGo.AddComponent<SpriteRenderer>();
            mSr.sprite = KebammVisualFactory.SquareSprite;
            mSr.sortingOrder = 1;
            monsterGo.AddComponent<BoxCollider2D>();
            var mRb = monsterGo.AddComponent<Rigidbody2D>();
            mRb.bodyType = RigidbodyType2D.Kinematic;
            mRb.simulated = true;
            _monster = monsterGo.AddComponent<KebammMonster>();
            _monster.Init(MonsterHealth);

            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(_worldRoot, false);
            groundGo.transform.position = new Vector3(0f, MonsterY - 1.0f, 0f);
            var gSr = groundGo.AddComponent<SpriteRenderer>();
            gSr.sprite = KebammVisualFactory.SquareSprite;
            gSr.color = new Color(0.22f, 0.28f, 0.22f);
            gSr.sortingOrder = 0;
            groundGo.transform.localScale = new Vector3(8f, 0.35f, 1f);
            groundGo.AddComponent<BoxCollider2D>();

            var dropGo = new GameObject("DropController");
            dropGo.transform.SetParent(_worldRoot, false);
            _dropper = dropGo.AddComponent<KebammDropController>();
            float dropY = containerGo.transform.position.y + ContainerHeight * 0.5f + 0.35f;
            _dropper.Init(this, _container, dropY);

            Debug.Log("[Kebamm] Prototype ready. Aim with mouse, drop with LMB/Space, reset with R.");
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
            return ball;
        }

        public void SpawnMergedBall(Vector3 position, KebammBallColour colour, int tier)
        {
            var ball = SpawnBall(position, colour, tier, preview: false);
            var rb = ball.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            Debug.Log($"[Kebamm] Merged -> {colour} T{tier} at {position}");
        }

        public void ResetSession()
        {
            if (_ballsRoot != null)
            {
                for (int i = _ballsRoot.childCount - 1; i >= 0; i--)
                    Destroy(_ballsRoot.GetChild(i).gameObject);
            }

            _capacity?.ResetTimers();
            _container?.CloseFloor();
            _monster?.ResetHealth();
            _dropper?.ClearPreview();
            _dropper?.SetDroppingEnabled(true);
            _dropper?.SpawnPreview();
            Debug.Log("[Kebamm] Session reset — container rebuilt for another drop round");
        }
    }
}
