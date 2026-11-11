using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Trebuchet rock (GDD [Level] Monster Group Design). Thrown by KebammTrebuchet onto the launcher line, then falls
    /// with ball gravity x rockGravityMultiplier and rockMass (heavy). It instantly destroys the first T unique balls it
    /// touches (Level Data rock_destroy_count), then stays in the jar as a heavy blocker. Never merges.
    /// Takes damage like a ball (Magic Bomb: health - damage, 0 or less = removed).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class KebammRock : MonoBehaviour
    {
        public const int SortingOrder = 25; // above balls (10..20), below Magic Bomb overlay (30)
        static readonly Color RockColour = new Color(0.45f, 0.42f, 0.4f);

        /// <summary>Rocks currently in play (thrown and not destroyed).</summary>
        public static readonly List<KebammRock> Live = new List<KebammRock>();

        public string Id { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public bool Landed { get; private set; }
        public int DestroyLeft => _destroyLeft;

        Rigidbody2D _rb;
        CircleCollider2D _col;
        SpriteRenderer _sr;
        KebammSiegeData _data;
        int _destroyLeft;
        readonly HashSet<KebammBall> _destroyed = new HashSet<KebammBall>();

        public Rigidbody2D Body => _rb;

        public void Init(KebammSiegeData data, int destroyCount, float size)
        {
            _data = data;
            Id = data.rockId;
            name = $"{data.rockName}_{data.rockId}";
            Health = MaxHealth = data.rockHp;
            _destroyLeft = Mathf.Max(0, destroyCount);
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<CircleCollider2D>();
            _sr = GetComponent<SpriteRenderer>();
            _sr.sprite = KebammVisualFactory.CircleSprite;
            _sr.color = RockColour;
            _sr.sortingOrder = SortingOrder;
            transform.localScale = Vector3.one * (size > 0f ? size : data.rockScale); // per-level rock_size (collider radius 0.5 scales with it)
            _col.radius = 0.5f;
            _col.enabled = false;
            _rb.bodyType = RigidbodyType2D.Kinematic; // flying: moved by the toss
            _rb.simulated = true;
            if (!Live.Contains(this))
                Live.Add(this);
        }

        /// <summary>Reached the launcher line: becomes a heavy dynamic body.</summary>
        public void Land()
        {
            if (Landed)
                return;
            Landed = true;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.mass = _data.rockMass;
            _rb.gravityScale = KebammBallTierData.NormalGravityScale * _data.rockGravityMultiplier;
            _rb.linearVelocity = Vector2.zero;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.sharedMaterial = KebammPrototypeBootstrap.SharedPhysicsMaterial;
            _col.sharedMaterial = KebammPrototypeBootstrap.SharedPhysicsMaterial;
            _col.enabled = true;
            KebammMonster.IgnoreDeadMonsters(_col);
            Debug.Log($"[Kebamm] {name} landed at x={transform.position.x:0.00}: destroys the first {_destroyLeft} ball(s) it touches, then blocks. Rocks in play: {Live.Count}");
        }

        void OnCollisionEnter2D(Collision2D collision) { TryDestroyBall(collision.collider); }
        void OnCollisionStay2D(Collision2D collision) { TryDestroyBall(collision.collider); }

        void TryDestroyBall(Collider2D other)
        {
            if (_destroyLeft <= 0 || other == null)
                return;
            var ball = other.GetComponent<KebammBall>();
            if (ball == null || ball.IsPreview || ball.IsBomb || ball.Spent || ball.IsMerging)
                return;
            if (!_destroyed.Add(ball))
                return;
            _destroyLeft--;
            Debug.Log($"[Kebamm] {name} destroyed {ball.name} ({_destroyLeft} left before it only blocks)");
            ball.MarkSpent();
            Destroy(ball.gameObject);
        }

        /// <summary>Ball damage rule. Returns true when destroyed (caller removes it).</summary>
        public bool TakeDamage(float amount)
        {
            Health = Mathf.Max(0f, Health - amount);
            return Health <= 0f;
        }

        void OnDestroy()
        {
            Live.Remove(this);
        }
    }
}