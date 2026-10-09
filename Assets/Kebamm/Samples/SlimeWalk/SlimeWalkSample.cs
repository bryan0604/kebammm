using UnityEngine;

namespace Kebamm.Samples
{
    /// <summary>
    /// Standalone sample (not part of the game flow): plays the slime walk cycle on a SpriteRenderer,
    /// patrols left and right between two x bounds and flips the sprite at each turn.
    /// Open Assets/Kebamm/Samples/SlimeWalk/SlimeWalkSample.unity and press Play,
    /// or add this component to an empty GameObject in any new scene.
    /// Frames come from the serialized array, or from Resources/SlimeWalk (slime_walk_00..05) when empty.
    /// </summary>
    [DisallowMultipleComponent]
    public class SlimeWalkSample : MonoBehaviour
    {
        public const string FramesResourcePath = "SlimeWalk";

        [Header("Frames")]
        [Tooltip("Walk frames in order. Leave empty to load Resources/SlimeWalk automatically.")]
        [SerializeField] Sprite[] frames;
        [Tooltip("Walk animation speed in frames per second.")]
        [SerializeField, Min(0.1f)] float framesPerSecond = 8f;

        [Header("Patrol")]
        [SerializeField] float minX = -3f;
        [SerializeField] float maxX = 3f;
        [Tooltip("World units per second.")]
        [SerializeField, Min(0f)] float moveSpeed = 1.2f;
        [Tooltip("The source art faces right. Untick if a future sheet faces left.")]
        [SerializeField] bool artFacesRight = true;

        [Header("Bob (optional, on top of the hop drawn in the frames)")]
        [SerializeField, Min(0f)] float bobAmplitude = 0f;
        [SerializeField, Min(0f)] float bobFrequency = 2f;

        [Header("Setup")]
        [Tooltip("Uniform scale. The frames are imported so the slime is about 1 unit tall at scale 1.")]
        [SerializeField, Min(0.01f)] float scale = 2f;
        [Tooltip("Creates an orthographic camera if the scene has none, so the sample works in an empty scene.")]
        [SerializeField] bool createCameraIfMissing = true;

        SpriteRenderer _renderer;
        float _animTime;
        float _bobTime;
        float _baseY;
        int _direction = 1;

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
                _renderer = gameObject.AddComponent<SpriteRenderer>();

            if (frames == null || frames.Length == 0)
            {
                frames = Resources.LoadAll<Sprite>(FramesResourcePath);
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }

            if (frames == null || frames.Length == 0)
            {
                Debug.LogWarning("[SlimeWalkSample] No frames assigned and none found in Resources/" + FramesResourcePath + ".");
                enabled = false;
                return;
            }

            transform.localScale = new Vector3(scale, scale, 1f);
            _baseY = transform.position.y;
            _renderer.sprite = frames[0];
            ApplyFacing();

            if (createCameraIfMissing && Camera.main == null && FindAnyObjectByType<Camera>() == null)
                CreateCamera();
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // Walk cycle.
            _animTime += dt;
            int index = (int)(_animTime * framesPerSecond) % frames.Length;
            _renderer.sprite = frames[index];

            // Patrol between minX and maxX, turning at each end.
            Vector3 pos = transform.position;
            pos.x += _direction * moveSpeed * dt;
            if (pos.x >= maxX)
            {
                pos.x = maxX;
                _direction = -1;
                ApplyFacing();
            }
            else if (pos.x <= minX)
            {
                pos.x = minX;
                _direction = 1;
                ApplyFacing();
            }

            // Small sine bob.
            _bobTime += dt;
            pos.y = _baseY + Mathf.Sin(_bobTime * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            transform.position = pos;
        }

        void ApplyFacing()
        {
            bool movingRight = _direction > 0;
            _renderer.flipX = artFacesRight ? !movingRight : movingRight;
        }

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera (SlimeWalkSample)");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 1f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.85f, 0.88f, 0.92f, 1f);
        }

        void OnValidate()
        {
            if (maxX < minX)
            {
                float t = minX;
                minX = maxX;
                maxX = t;
            }
        }

        void OnDrawGizmosSelected()
        {
            float y = Application.isPlaying ? _baseY : transform.position.y;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector3(minX, y, 0f), new Vector3(maxX, y, 0f));
            Gizmos.DrawWireSphere(new Vector3(minX, y, 0f), 0.1f);
            Gizmos.DrawWireSphere(new Vector3(maxX, y, 0f), 0.1f);
        }
    }
}
