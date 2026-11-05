using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class KebammMonster : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;
        [SerializeField] KebammBallColour colour = KebammBallColour.Red;

        float _health;
        int _hitPts;
        int _destroyedPts;
        SpriteRenderer _sr;
        Color _baseColor;
        Color _hitFlashColor = Color.white;
        bool _usesArt;
        Coroutine _flashRoutine;
        bool _defeated;

        // Ball impact bounce: launched up + away from the hit side, spins on Z, then settles upright where it lands.
        const float SideRatio = 0.6f;        // sideways speed = strength * SideRatio
        const float SpinPerStrength = 90f;   // deg/s of Z spin per unit of impact strength
        const float MaxSpin = 540f;          // deg/s cap so it can never tumble forever
        const float SettleDelay = 0.15f;     // seconds at rest before easing back upright
        const float SettleSpeed = 360f;      // deg/s when easing back upright
        Rigidbody2D _rb;
        float _minX = -1000f;
        float _maxX = 1000f;
        float _restTimer;

        // Recovery (GDD Monster Behavior Design): a living monster on the ground, tilted past the threshold,
        // jumps back upright once the delay has passed since its last ball hit. Settings: Game Manager data.
        /// <summary>The platform ground collider (set by the bootstrap). Only monsters touching it recover.</summary>
        public static Collider2D GroundCollider;
        float _lastHitTime = -1000f;
        bool _recovering;
        float _recoverStart;
        float _recoverSpinSpeed;
        float _knockStartX; // x where the last knock-back started; the recovery hop moves back toward it
        public bool IsRecovering => _recovering;

        // Monsters hold in place (no gravity, frozen) until the first ball hit, and never collide with each other.
        internal static readonly List<Collider2D> LiveColliders = new List<Collider2D>();
        // Defeated monsters: untouchable by balls/bombs (Physics2D.IgnoreCollision), still collide with the ground so they keep rolling.
        static readonly List<Collider2D> DeadColliders = new List<Collider2D>();
        Collider2D _col;
        float _activeGravity = 1.4f;
        bool _activated;

        static PhysicsMaterial2D _sharedMaterial;
        public static PhysicsMaterial2D SharedMaterial
        {
            get
            {
                if (_sharedMaterial == null)
                    _sharedMaterial = new PhysicsMaterial2D("KebammMonsterMat") { friction = 0.8f, bounciness = 0f };
                return _sharedMaterial;
            }
        }

        public float Health => _health;
        public float MaxHealth => maxHealth;
        public KebammBallColour Colour => colour;
        public bool IsDefeated => _defeated;
        public bool UsesArt => _usesArt;

        public void Init(float hp, KebammBallColour ballColour, int hitPts, int destroyedPts)
        {
            maxHealth = hp;
            _health = hp;
            _defeated = false;
            _hitPts = hitPts;
            _destroyedPts = destroyedPts;
            colour = ballColour;
            _sr = GetComponent<SpriteRenderer>();
            _rb = GetComponent<Rigidbody2D>();
            HoldUntilHit();
            IgnoreOtherMonsters();
            ApplyLook();
            name = "Monster";
        }

        // Colours with art in KebammMonsterVisuals (e.g. blue) use the sprite untinted; the rest keep the tinted square.
        // The collider is left as spawned (1 x 1 box); art sprites are imported so the visible body is about 1 x 1 unit.
        void ApplyLook()
        {
            Sprite art = KebammMonsterVisuals.SpriteFor(colour);
            _usesArt = art != null;
            if (_usesArt)
            {
                _sr.sprite = art;
                _baseColor = KebammMonsterVisuals.TintFor();
                _hitFlashColor = KebammMonsterVisuals.HitFlashFor();
            }
            else
            {
                _baseColor = KebammVisualFactory.ColourFor(colour);
                _hitFlashColor = Color.white;
            }
            _sr.color = _baseColor;
        }

        /// <summary>Platform x-range for this monster centre; it is never pushed past these edges.</summary>
        public void SetBounds(float minX, float maxX)
        {
            _minX = Mathf.Min(minX, maxX);
            _maxX = Mathf.Max(minX, maxX);
        }

        void HoldUntilHit()
        {
            _activated = false;
            if (_rb == null)
                return;
            if (_rb.gravityScale > 0f)
                _activeGravity = _rb.gravityScale;
            _rb.gravityScale = 0f;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        void Activate()
        {
            if (_activated || _rb == null)
                return;
            _activated = true;
            _rb.constraints = RigidbodyConstraints2D.None;
            _rb.gravityScale = _activeGravity;
        }

        void IgnoreOtherMonsters()
        {
            if (_col == null)
                _col = GetComponent<Collider2D>();
            LiveColliders.RemoveAll(c => c == null);
            foreach (var other in LiveColliders)
                if (other != _col)
                    Physics2D.IgnoreCollision(_col, other, true);
            // Monsters and buildings may overlap at spawn: no collision between them.
            KebammObstacle.LiveColliders.RemoveAll(c => c == null);
            foreach (var ob in KebammObstacle.LiveColliders)
                Physics2D.IgnoreCollision(_col, ob, true);
            if (!LiveColliders.Contains(_col))
                LiveColliders.Add(_col);
        }

        void OnDestroy()
        {
            if (_col != null)
            {
                LiveColliders.Remove(_col);
                DeadColliders.Remove(_col);
            }
        }

        void ApplyImpact(float strength, float hitX)
        {
            Activate();
            if (_rb == null || _rb.bodyType != RigidbodyType2D.Dynamic)
                return;

            float dx = transform.position.x - hitX;
            float side = Mathf.Abs(dx) < 0.02f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(dx);
            // Against an edge, always bounce back inward.
            if (transform.position.x <= _minX + 0.01f) side = 1f;
            else if (transform.position.x >= _maxX - 0.01f) side = -1f;

            _knockStartX = transform.position.x;
            float s = strength;
            _rb.linearVelocity = Vector2.zero;
            _rb.AddForce(new Vector2(side * s * SideRatio, s) * _rb.mass, ForceMode2D.Impulse);
            // Flying right spins clockwise (negative Z), flying left counter-clockwise.
            _rb.angularVelocity = Mathf.Clamp(-side * s * SpinPerStrength, -MaxSpin, MaxSpin);
            _restTimer = 0f;
        }

        void FixedUpdate()
        {
            if (_rb == null || _rb.bodyType != RigidbodyType2D.Dynamic || !_activated)
                return;

            // Keep inside the platform edges.
            Vector2 p = _rb.position;
            if (p.x < _minX || p.x > _maxX)
            {
                p.x = Mathf.Clamp(p.x, _minX, _maxX);
                _rb.position = p;
                Vector2 v = _rb.linearVelocity;
                if ((p.x <= _minX && v.x < 0f) || (p.x >= _maxX && v.x > 0f))
                    _rb.linearVelocity = new Vector2(-v.x * 0.3f, v.y);
            }

            if (Mathf.Abs(_rb.angularVelocity) > MaxSpin)
                _rb.angularVelocity = Mathf.Sign(_rb.angularVelocity) * MaxSpin;

            float tilt = Mathf.DeltaAngle(0f, _rb.rotation);
            if (_recovering)
            {
                TickRecovery(tilt);
                return;
            }

            var gm = KebammPrototypeBootstrap.Instance != null ? KebammPrototypeBootstrap.Instance.GameManagerData : null;
            float threshold = gm != null ? gm.MonsterRecoverTiltDegrees : 30f;
            if (!_defeated && Mathf.Abs(tilt) > threshold)
            {
                // Toppled: no small settle; jump back upright after the delay since the last hit (ground only, alive only).
                _restTimer = 0f;
                float delay = gm != null ? gm.MonsterRecoverDelay : 5f;
                if (Time.time - _lastHitTime >= delay && OnGround())
                    StartRecovery(tilt, gm);
                return;
            }

            // Small tilt: once it has landed and calmed down, wobble back upright (position stays where it landed).
            if (_rb.linearVelocity.sqrMagnitude < 0.04f && Mathf.Abs(_rb.angularVelocity) < 45f && Mathf.Abs(tilt) > 0.5f)
            {
                _restTimer += Time.fixedDeltaTime;
                if (_restTimer >= SettleDelay)
                {
                    _rb.angularVelocity = 0f;
                    _rb.MoveRotation(Mathf.MoveTowards(tilt, 0f, SettleSpeed * Time.fixedDeltaTime));
                }
            }
            else
            {
                _restTimer = 0f;
            }
        }

        bool OnGround() => GroundCollider != null && _col != null && _col.IsTouching(GroundCollider);

        void StartRecovery(float tilt, KebammGameManagerData gm)
        {
            float height = gm != null ? gm.MonsterRecoverJumpHeight : 0.8f;
            float duration = gm != null ? gm.MonsterRecoverJumpDuration : 0.5f;
            float maxHop = gm != null ? gm.MonsterRecoverHopDistance : 0.6f;
            float g = Mathf.Abs(Physics2D.gravity.y * _rb.gravityScale);
            _recovering = true;
            _recoverStart = Time.time;
            _recoverSpinSpeed = Mathf.Abs(tilt) / duration;
            _rb.angularVelocity = 0f;
            float vy = g > 0f ? Mathf.Sqrt(2f * g * height) : 0f;
            // Reverse of the knock-back: hop back toward where the last knock started, capped and kept on the platform.
            float x = transform.position.x;
            float back = Mathf.Clamp(_knockStartX - x, -maxHop, maxHop);
            back = Mathf.Clamp(x + back, _minX, _maxX) - x;
            float airTime = g > 0f ? 2f * vy / g : 0f;
            float vx = airTime > 0f ? back / airTime : 0f;
            _rb.linearVelocity = new Vector2(vx, vy);
            Debug.Log($"[Kebamm] Monster recovering (tilt {tilt:0} deg, jump {height}, {duration}s)");
        }

        void TickRecovery(float tilt)
        {
            // Rotate around Z until local Y faces world up; physics handles the jump arc.
            _rb.angularVelocity = 0f;
            _rb.MoveRotation(Mathf.MoveTowards(tilt, 0f, _recoverSpinSpeed * Time.fixedDeltaTime));
            bool upright = Mathf.Abs(tilt) <= 0.5f;
            bool falling = _rb.linearVelocity.y <= 0.05f;
            if ((upright && falling && OnGround()) || Time.time - _recoverStart > 3f)
            {
                _recovering = false;
                _restTimer = 0f;
            }
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            var ball = collision.collider.GetComponent<KebammBall>();
            if (ball == null || ball.IsPreview || ball.IsBomb || ball.Spent) // Magic Bomb: no impact damage
                return;

            if (_defeated)
                return; // dead monsters are untouchable (collision is ignored; guard only)
            Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)ball.transform.position;
            ball.TakeDamage(ball.Damage); // kept for the log; the ball is removed on contact whatever its HP
            ReceiveHit(ball.Damage, ball.ImpactStrength, point.x, "direct");
            // Ball On Impact: direct contact explodes (AOE on other monsters/obstacles) and removes the ball.
            KebammImpactExplosion.Explode(ball, point, this);
        }

        /// <summary>Damage + push from a direct hit or an impact explosion. Same score rules for both (no score during the recovery hop).</summary>
        public void ReceiveHit(float ballDamage, float strength, float hitX, string source)
        {
            if (_defeated)
                return;
            float monsterHealth = _health;
            float damage = ballDamage - monsterHealth;
            _health = Mathf.Max(0f, monsterHealth - ballDamage);
            Debug.Log($"[Kebamm] Impact ({source}) Damage={damage} (ball {ballDamage} - monster {monsterHealth}). Monster HP={_health}/{maxHealth}");
            KebammFrameSpikeLog.Impacts++;
            // Hits during the recovery jump still deal damage but score nothing.
            bool scores = !_recovering;
            if (scores)
                KebammScore.AddHit(_hitPts);
            _lastHitTime = Time.time;
            if (KebammPrototypeBootstrap.Instance != null)
                KebammPrototypeBootstrap.Instance.NotifyMonsterImpact();

            if (_sr == null)
                _sr = GetComponent<SpriteRenderer>();
            KebammDamageNumber.Spawn(_sr.bounds, ballDamage);

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(Flash());

            _recovering = false; // a hit interrupts the recovery hop; the delay restarts from this hit
            ApplyImpact(strength, hitX);

            if (_health <= 0f && !_defeated)
            {
                _defeated = true;
                SetUntouchable(true);
                if (scores)
                    KebammScore.AddDestroy(_destroyedPts);
                Debug.Log("[Kebamm] Monster defeated");
                _sr.color = new Color(0.25f, 0.25f, 0.25f);
            }
        }

        IEnumerator Flash()
        {
            _sr.color = _hitFlashColor;
            yield return new WaitForSeconds(0.08f);
            if (!_defeated)
                _sr.color = _baseColor;
            _flashRoutine = null;
        }

        /// <summary>Called by every ball/bomb when it spawns: ignore collisions with monsters that are already defeated.</summary>
        public static void IgnoreDeadMonsters(Collider2D ballCol)
        {
            if (ballCol == null)
                return;
            DeadColliders.RemoveAll(c => c == null);
            foreach (var c in DeadColliders)
                Physics2D.IgnoreCollision(ballCol, c, true);
        }

        void SetUntouchable(bool on)
        {
            if (_col == null)
                _col = GetComponent<Collider2D>();
            if (_col == null)
                return;
            if (on) { if (!DeadColliders.Contains(_col)) DeadColliders.Add(_col); }
            else DeadColliders.Remove(_col);
            int n = 0;
            foreach (var ball in FindObjectsByType<KebammBall>(FindObjectsSortMode.None))
                foreach (var bc in ball.GetComponents<Collider2D>())
                {
                    Physics2D.IgnoreCollision(_col, bc, on);
                    n++;
                }
            Debug.Log($"[Kebamm] Monster {(on ? "defeated: untouchable by" : "revived: collides again with")} balls/bombs ({n} ball colliders updated); ground collision kept");
        }

        public void ResetHealth()
        {
            _health = maxHealth;
            if (_defeated)
                SetUntouchable(false);
            _defeated = false;
            if (_sr != null)
                _sr.color = _baseColor;
            Debug.Log($"[Kebamm] Monster HP reset to {_health}");
        }
    }
}