using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Building obstacle. Frozen and ignores balls until the bombing run (container floor open).
    /// During the bombing run a ball hit deals damage (same rule as monsters; the ball takes damage too),
    /// turns gravity on and pushes it sideways by the ball's impact strength - never upward.
    /// At HP &lt;= 0 it is darkened, awards destroyedPts and stays in place as an inert body.
    /// </summary>
    public class KebammObstacle : MonoBehaviour
    {
        const float SideRatio = 0.6f;    // same sideways ratio as monster impacts
        const float ActiveGravity = 1.4f;
        static readonly Color BaseColour = new Color(0.62f, 0.52f, 0.42f);
        static readonly Color HitColour = Color.white;
        static readonly Color DestroyedColour = new Color(0.25f, 0.25f, 0.25f);

        static PhysicsMaterial2D _sharedMaterial;
        public static PhysicsMaterial2D SharedMaterial
        {
            get
            {
                if (_sharedMaterial == null)
                    _sharedMaterial = new PhysicsMaterial2D("KebammObstacleMat") { friction = 0.8f, bounciness = 0f };
                return _sharedMaterial;
            }
        }

        public KebammObstacleShape Shape { get; private set; }
        public float Width { get; private set; }
        public float Height { get; private set; }
        public float StackSpacing { get; private set; }
        public float Health => _health;
        public bool IsDestroyed => _destroyed;

        internal static readonly List<Collider2D> LiveColliders = new List<Collider2D>();
        Collider2D _col;

        KebammObstacleData _data;
        float _health;
        bool _destroyed;
        bool _activated;
        float _minX = -1000f, _maxX = 1000f;
        SpriteRenderer _sr;
        Rigidbody2D _rb;
        Coroutine _flash;

        public void Init(KebammObstacleData data, float minX, float maxX)
        {
            _data = data;
            Shape = data.shape;
            Width = data.width;
            Height = data.height;
            StackSpacing = data.stackSpacing;
            _health = data.hp;
            _destroyed = false;
            foreach (var c in GetComponents<Collider2D>())
                c.enabled = true;
            _minX = Mathf.Min(minX, maxX);
            _maxX = Mathf.Max(minX, maxX);
            _sr = GetComponent<SpriteRenderer>();
            _rb = GetComponent<Rigidbody2D>();
            _sr.color = BaseColour;
            _rb.mass = data.Mass;
            // Untouched until the bombing run: no gravity, fully frozen.
            _activated = false;
            _rb.gravityScale = 0f;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeAll;
            name = $"Obstacle_{Shape}";
            IgnoreMonsters();
        }

        // Monsters and buildings may overlap at spawn: no collision between them. Balls still hit both.
        void IgnoreMonsters()
        {
            if (_col == null)
                _col = GetComponent<Collider2D>();
            if (_col == null)
                return;
            KebammMonster.LiveColliders.RemoveAll(c => c == null);
            foreach (var m in KebammMonster.LiveColliders)
                Physics2D.IgnoreCollision(_col, m, true);
            if (!LiveColliders.Contains(_col))
                LiveColliders.Add(_col);
        }

        /// <summary>Top edge in world space (pivot is the centre).</summary>
        public float TopY => transform.position.y + Height * 0.5f;

        void Activate()
        {
            if (_activated)
                return;
            _activated = true;
            // Rotation stays frozen so a building always rests on its base (a triangle never tips onto its point).
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.gravityScale = ActiveGravity;
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            var boot = KebammPrototypeBootstrap.Instance;
            if (boot == null || !boot.IsBombingRun)
                return;

            var ball = collision.collider.GetComponent<KebammBall>();
            if (ball == null || ball.IsPreview || ball.IsBomb || ball.Spent) // Magic Bomb: no obstacle damage
                return;
            if (_destroyed)
                return; // rubble has no collision

            Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)ball.transform.position;
            ball.TakeDamage(ball.Damage); // kept for the log; the ball is removed on contact whatever its HP
            ReceiveHit(ball.Damage, ball.ImpactStrength, point.x, "direct");
            // Ball On Impact: hitting a building explodes too, then the ball is removed.
            KebammImpactExplosion.Explode(ball, point, this);
        }

        /// <summary>Damage + sideways push from a direct hit or an impact explosion (bombing run only).</summary>
        public void ReceiveHit(float ballDamage, float strength, float hitX, string source)
        {
            var boot = KebammPrototypeBootstrap.Instance;
            if (_destroyed || boot == null || !boot.IsBombingRun)
                return;

            // Same damage rule as KebammMonster: building loses ball damage.
            float before = _health;
            _health = Mathf.Max(0f, before - ballDamage);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Kebamm] Obstacle {Shape} hit ({source}) Damage={ballDamage - before} HP={_health}/{_data.hp}");
#endif
            KebammFrameSpikeLog.Impacts++;
            boot.NotifyMonsterImpact(); // counts as bombing-run activity
            // Damage text, same as monster hits.
            KebammDamageNumber.Spawn(_sr.bounds, ballDamage);

            Activate();
            Push(strength, hitX);

            if (_flash != null)
                StopCoroutine(_flash);
            if (_health > 0f)
                _flash = StartCoroutine(Flash());

            if (_health <= 0f)
            {
                _destroyed = true;
                _sr.color = DestroyedColour;
                KebammScore.AddObstacleDestroy(_data.destroyedPts);
                BecomeRubble();
            }
        }

        // HP <= 0: frozen in place with no collision, so balls, monsters and buildings pass through.
        // Buildings stacked above lose their support, so they are activated and fall under gravity.
        void BecomeRubble()
        {
            float top = TopY;
            float halfW = Width * 0.5f;
            float x = transform.position.x;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.gravityScale = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeAll;
            foreach (var c in GetComponents<Collider2D>())
                c.enabled = false;
            LiveColliders.RemoveAll(c => c == null);
            foreach (var c in LiveColliders)
            {
                var other = c.GetComponent<KebammObstacle>();
                if (other == null || other == this || other._destroyed)
                    continue;
                float ox = other.transform.position.x, oBottom = other.transform.position.y - other.Height * 0.5f;
                if (oBottom >= top - 0.05f && Mathf.Abs(ox - x) < halfW + other.Width * 0.5f)
                    other.Activate();
            }
        }

        void Push(float strength, float hitX)
        {
            float dx = transform.position.x - hitX;
            float side = Mathf.Abs(dx) < 0.02f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(dx);
            if (transform.position.x <= _minX + 0.01f) side = 1f;
            else if (transform.position.x >= _maxX - 0.01f) side = -1f;
            float s = strength * _data.PushMultiplier;
            Vector2 v = _rb.linearVelocity;
            _rb.linearVelocity = new Vector2(0f, Mathf.Min(0f, v.y));
            _rb.AddForce(new Vector2(side * s * SideRatio * _rb.mass, 0f), ForceMode2D.Impulse);
        }

        System.Collections.IEnumerator Flash()
        {
            _sr.color = HitColour;
            yield return new WaitForSeconds(0.08f);
            if (!_destroyed)
                _sr.color = BaseColour;
            _flash = null;
        }

        void FixedUpdate()
        {
            if (!_activated || _rb == null)
                return;
            Vector2 v = _rb.linearVelocity;
            // No upward bounce: only gravity and sideways push move a building.
            if (v.y > 0f)
                _rb.linearVelocity = v = new Vector2(v.x, 0f);
            Vector2 p = _rb.position;
            if (p.x < _minX || p.x > _maxX)
            {
                p.x = Mathf.Clamp(p.x, _minX, _maxX);
                _rb.position = p;
                _rb.linearVelocity = new Vector2(-v.x * 0.3f, v.y);
            }
        }

        void OnDestroy()
        {
            if (_col != null)
                LiveColliders.Remove(_col);
        }
    }
}