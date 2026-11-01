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

        // Monsters hold in place (no gravity, frozen) until the first ball hit, and never collide with each other.
        static readonly List<Collider2D> LiveColliders = new List<Collider2D>();
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
            if (!LiveColliders.Contains(_col))
                LiveColliders.Add(_col);
        }

        void OnDestroy()
        {
            if (_col != null)
                LiveColliders.Remove(_col);
        }

        void ApplyImpact(KebammBall ball, Collision2D collision)
        {
            Activate();            if (_rb == null || _rb.bodyType != RigidbodyType2D.Dynamic)
                return;

            float hitX = collision.contactCount > 0 ? collision.GetContact(0).point.x : ball.transform.position.x;
            float dx = transform.position.x - hitX;
            float side = Mathf.Abs(dx) < 0.02f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(dx);
            // Against an edge, always bounce back inward.
            if (transform.position.x <= _minX + 0.01f) side = 1f;
            else if (transform.position.x >= _maxX - 0.01f) side = -1f;

            float s = ball.ImpactStrength;
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

            // Once it has landed and calmed down, wobble back upright (position stays where it landed).
            float tilt = Mathf.DeltaAngle(0f, _rb.rotation);
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

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (_defeated)
                return;

            var ball = collision.collider.GetComponent<KebammBall>();
            if (ball == null || ball.IsPreview)
                return;

            float ballDamage = ball.Damage;
            float monsterHealth = _health;
            float damage = ballDamage - monsterHealth;
            _health = Mathf.Max(0f, monsterHealth - ballDamage);
            bool ballBroken = ball.TakeDamage(ballDamage);
            Debug.Log($"[Kebamm] Impact Damage={damage} (ball {ballDamage} - monster {monsterHealth}). Monster HP={_health}/{maxHealth}. Ball HP={ball.Health}");
            KebammScore.AddHit(_hitPts);
            if (KebammPrototypeBootstrap.Instance != null)
                KebammPrototypeBootstrap.Instance.NotifyMonsterImpact();

            if (_sr == null)
                _sr = GetComponent<SpriteRenderer>();
            KebammDamageNumber.Spawn(_sr.bounds, ballDamage);

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(Flash());

            ApplyImpact(ball, collision);

            if (ballBroken)
                Destroy(ball.gameObject);

            if (_health <= 0f && !_defeated)
            {
                _defeated = true;
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

        public void ResetHealth()
        {
            _health = maxHealth;
            _defeated = false;
            if (_sr != null)
                _sr.color = _baseColor;
            Debug.Log($"[Kebamm] Monster HP reset to {_health}");
        }
    }
}