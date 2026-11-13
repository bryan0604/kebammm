using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class KebammBall : MonoBehaviour
    {
        public const int MaxTier = 5;

        public KebammBallColour Colour { get; private set; }
        public int Tier { get; private set; }
        public bool IsMerging { get; private set; }
        public bool IsPreview { get; private set; }
        public float Damage { get; private set; }
        public float Health { get; private set; }
        public float ImpactStrength { get; private set; }
        /// <summary>Tier impact_upward_multiplier: scales only the upward part of the push on monsters.</summary>
        public float ImpactUpwardMultiplier { get; private set; } = 1f;
        /// <summary>Set once the ball has exploded / touched the ground; it is being removed and ignores further contacts.</summary>
        public bool Spent { get; private set; }
        /// <summary>Magic Bomb (combo power-up): never merges, deals no impact damage, explodes via KebammMagicBomb.</summary>
        public bool IsBomb { get; private set; }

        Rigidbody2D _rb;
        CircleCollider2D _col;
        SpriteRenderer _sr;

        /// <summary>Live (non-preview) balls, kept for allocation-free polling (KebammCapacityMonitor).</summary>
        public static readonly List<KebammBall> Live = new List<KebammBall>();

        /// <summary>Normal gravity (1.4), or this tier's Ball Tier Data bombing_run_gravity_scale during the bombing run. Magic Bombs keep normal gravity.</summary>
        public void ApplyBombingRunGravity(bool bombingRun)
        {
            if (_rb == null || IsPreview)
                return;
            _rb.gravityScale = bombingRun && !IsBomb
                ? KebammBallTierData.BombingRunGravityForPlayTier(Tier)
                : KebammBallTierData.NormalGravityScale;
        }

        /// <summary>Switches every live ball (already falling, resting or in the container) to bombing run / normal gravity.</summary>
        public static void SetBombingRunGravityAll(bool bombingRun)
        {
            for (int i = 0; i < Live.Count; i++)
                if (Live[i] != null)
                    Live[i].ApplyBombingRunGravity(bombingRun);
        }
        bool _registered;

        public Rigidbody2D Body => _rb;
        public Collider2D Col => _col;
        /// <summary>Play state: true from this ball's first contact with another ball, the floor or a wall (merged balls inherit it).</summary>
        public bool InPlay { get; set; }

        // Drop burst VFX: set when the player drops this ball; cleared on its first bottom-side touch.
        bool _awaitingDropTouch;
        public void MarkDropped() { _awaitingDropTouch = true; }

        void OnDestroy()
        {
            if (_registered)
                Live.Remove(this);
            _registered = false;
        }

        public float Radius => IsBomb ? Mathf.Max(0.05f, BombDiameter()) * 0.5f : KebammBallTierData.DiameterForPlayTier(Tier) * 0.5f;

        /// <summary>Magic Bomb diameter: Game Manager bomb_size (remote / asset), else combo_config bombDiameter (0.5).</summary>
        static float BombDiameter()
        {
            var boot = KebammPrototypeBootstrap.Instance;
            var gm = boot != null ? boot.GameManagerData : null;
            return gm != null && gm.MagicBombSize > 0f ? gm.MagicBombSize : KebammCombo.Config.bombDiameter;
        }

        // Merge: the merged ball appears at the midpoint of the two balls and grows from the pre-merge size
        // to the new tier size, so physics pushes neighbours aside gradually.
        public const float MergeGrowDuration = 0.25f;  // seconds to grow to full size
        public const float MergePushMaxSpeed = 3f;     // neighbour speed cap while a merged ball grows (units/s)

        float _growFrom, _growTo, _growStart;
        bool _growing;
        static readonly Collider2D[] GrowHits = new Collider2D[16];

        public void Configure(KebammBallColour colour, int tier, bool preview)
        {
            Colour = colour;
            Tier = Mathf.Clamp(tier, 0, MaxTier);
            IsPreview = preview;
            IsMerging = false;
            KebammBallTierData.Resolve(Tier, out float damage, out float hp);
            Damage = damage;
            Health = hp;
            ImpactStrength = KebammBallTierData.ImpactStrengthForPlayTier(Tier);
            ImpactUpwardMultiplier = KebammBallTierData.ImpactUpwardMultiplierForPlayTier(Tier);

            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<CircleCollider2D>();
            _sr = GetComponent<SpriteRenderer>();
            if (!preview && !_registered)
            {
                Live.Add(this);
                _registered = true;
            }

            float radius = Radius;
            transform.localScale = Vector3.one * (radius * 2f);
            _col.radius = 0.5f;
            // Ball art (untinted) when present; otherwise the tinted placeholder circle.
            Sprite art = ArtFor(colour, Tier);
            _sr.sprite = art != null ? art : KebammVisualFactory.CircleSprite;
            _sr.color = art != null ? Color.white : KebammVisualFactory.ColourFor(colour);
            _sr.sortingOrder = 10 + Tier;

            if (preview)
            {
                _rb.bodyType = RigidbodyType2D.Kinematic;
                _rb.simulated = false;
                _col.enabled = false;
            }
            else
            {
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.simulated = true;
                var bootG = KebammPrototypeBootstrap.Instance;
                ApplyBombingRunGravity(bootG != null && bootG.IsBombingRun);
                _rb.mass = 1f + Tier * 0.75f;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                _rb.sharedMaterial = KebammPrototypeBootstrap.SharedPhysicsMaterial;
                _col.enabled = true;
                _col.sharedMaterial = KebammPrototypeBootstrap.SharedPhysicsMaterial;
            }

            name = $"Ball_{colour}_T{Tier}";
        }

        /// <summary>Configures this ball as a Magic Bomb (black placeholder circle, bomb diameter). Live bombs get KebammMagicBomb.</summary>
        public void ConfigureBomb(bool preview)
        {
            IsBomb = true;
            Configure(KebammBallColour.Red, 0, preview);
            if (_registered)
            {
                Live.Remove(this); // not a jar ball for the capacity line
                _registered = false;
            }
            _sr.sprite = KebammVisualFactory.CircleSprite;
            _sr.color = Color.black;
            _sr.sortingOrder = 20;
            name = preview ? "MagicBomb_Held" : "MagicBomb";
            if (!preview && GetComponent<KebammMagicBomb>() == null)
                gameObject.AddComponent<KebammMagicBomb>();
        }


        // Resources/Kebamm/Balls/ball_<colour>_t<tier> overrides per tier; ball_<colour> is used for every tier.
        // Art is a square, edge-to-edge circle imported at PPU = pixel width, so it is 1 unit wide like the placeholder.
        const string ArtFolder = "Kebamm/Balls/ball_";
        static readonly Dictionary<string, Sprite> ArtCache = new Dictionary<string, Sprite>();

        static Sprite ArtFor(KebammBallColour colour, int tier)
        {
            string c = colour.ToString().ToLowerInvariant();
            return LoadArt($"{c}_t{tier}") ?? LoadArt(c);
        }

        /// <summary>
        /// Loads (and caches) every colour/tier art lookup up front, so the first drop or merge of a new
        /// colour/tier does not hit Resources.Load and a texture upload mid-play (a frame hitch on placing a ball).
        /// </summary>
        public static void PrewarmArt()
        {
            foreach (KebammBallColour colour in System.Enum.GetValues(typeof(KebammBallColour)))
            {
                for (int tier = 0; tier <= MaxTier; tier++)
                    ArtFor(colour, tier);
            }
        }

        static Sprite LoadArt(string key)
        {
            if (!ArtCache.TryGetValue(key, out Sprite s))
            {
                s = Resources.Load<Sprite>(ArtFolder + key);
                ArtCache[key] = s;
            }
            return s != null ? s : null;
        }

        /// <summary>Start at startDiameter and grow to this tier's size over MergeGrowDuration.</summary>
        public void BeginGrow(float startDiameter)
        {
            _growTo = Radius * 2f;
            _growFrom = Mathf.Clamp(startDiameter, 0.01f, _growTo);
            _growStart = Time.time;
            _growing = true;
            transform.localScale = Vector3.one * _growFrom;
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }
        }

        // Scale is driven every rendered frame (not only at 50 Hz physics steps) with an ease-out cubic,
        // so the grow looks smooth; the collider follows the transform on the next physics step.
        void Update()
        {
            if (!_growing || IsPreview)
                return;
            float t = Mathf.Clamp01((Time.time - _growStart) / MergeGrowDuration);
            float e = 1f - (1f - t) * (1f - t) * (1f - t); // ease-out cubic
            transform.localScale = Vector3.one * Mathf.Lerp(_growFrom, _growTo, e);
            if (t >= 1f)
                _growing = false;
        }

        void FixedUpdate()
        {
            if (!_growing || IsPreview)
                return;

            // Keep the push gentle: cap this ball's and nearby balls' speed while growing.
            CapSpeed(_rb);
            int n = Physics2D.OverlapCircleNonAlloc(transform.position, _growTo * 0.5f + 0.1f, GrowHits);
            for (int i = 0; i < n; i++)
            {
                var other = GrowHits[i] != null ? GrowHits[i].GetComponent<KebammBall>() : null;
                if (other != null && other != this)
                    CapSpeed(other._rb);
            }
        }

        static void CapSpeed(Rigidbody2D rb)
        {
            if (rb != null && rb.linearVelocity.sqrMagnitude > MergePushMaxSpeed * MergePushMaxSpeed)
                rb.linearVelocity = rb.linearVelocity.normalized * MergePushMaxSpeed;
        }

        /// <summary>Marks the ball as used up and turns its colliders off so no second contact is processed this frame.</summary>
        public void MarkSpent()
        {
            Spent = true;
            foreach (var c in GetComponents<Collider2D>())
                c.enabled = false;
        }

        void Start()
        {
            // Defeated monsters are untouchable: this ball/bomb passes through them.
            foreach (var c in GetComponents<Collider2D>())
                KebammMonster.IgnoreDeadMonsters(c);
        }

        public bool TakeDamage(float amount)
        {
            Health = Mathf.Max(0f, Health - amount);
            return Health <= 0f;
        }
        public void BeginMerge()
        {
            IsMerging = true;
        }

        // Pass in-play along a resting stack: a not-yet-in-play ball touching an in-play ball becomes in play.
        void OnCollisionStay2D(Collision2D collision)
        {
            if (InPlay || IsPreview || collision.collider.isTrigger)
                return;
            var other = collision.collider.GetComponent<KebammBall>();
            if (other == null || (other.InPlay && !other.IsPreview))
                InPlay = true;
        }

        // Drop burst: plays once, only where the dropped ball touches with its bottom side (a contact below its
        // centre), so side bumps against walls or neighbours don't puff.
        void TryDropBurst(Collision2D collision)
        {
            float centreY = transform.position.y;
            float r = _col != null ? _col.bounds.extents.y : transform.lossyScale.y * 0.5f;
            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector2 p = collision.GetContact(i).point;
                if (p.y < centreY - r * 0.5f)
                {
                    _awaitingDropTouch = false;
                    KebammDropBurst.Play(new Vector3(p.x, p.y, 0f), r * 2f);
                    return;
                }
            }
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsPreview)
                return;
            // Ball On Impact: touching the village ground (platform outside the jar, not the jar floor) explodes like a
            // monster/obstacle hit (no direct target, so everything in range takes one hit) and removes the ball.
            if (!IsBomb && !Spent && KebammMonster.GroundCollider != null && collision.collider == KebammMonster.GroundCollider)
            {
                Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
                Debug.Log($"[Kebamm] Ball T{Tier + 1} touched the ground: exploding. HP={Health}");
                KebammImpactExplosion.Explode(this, point, null);
                return;
            }
            if (Spent)
                return;
            if (_awaitingDropTouch && !collision.collider.isTrigger && !IsMerging)
                TryDropBurst(collision);
            var other = collision.collider.GetComponent<KebammBall>();
            // In play only from a real contact: floor, wall or any non-ball solid, or a ball that is already in play.
            // Touching another still-falling ball does not count (spam-drop bombing bug).
            if (!collision.collider.isTrigger && (other == null || (other.InPlay && !other.IsPreview)))
                InPlay = true;
            if (IsMerging || IsBomb)
                return;

            if (other == null || other.IsPreview || other.IsMerging || other.IsBomb)
                return;

            if (Colour != other.Colour || Tier != other.Tier)
                return;

            if (Tier >= MaxTier)
                return;

            // Only the lower instance ID performs the merge to avoid double-spawn.
            if (GetInstanceID() > other.GetInstanceID())
                return;

            BeginMerge();
            other.BeginMerge();

            Vector3 mid = (transform.position + other.transform.position) * 0.5f;
            float startDiameter = Mathf.Max(transform.localScale.x, other.transform.localScale.x);
            int nextTier = Tier + 1;
            bool mergedInPlay = InPlay || other.InPlay;
            var colour = Colour;

            Destroy(other.gameObject);
            Destroy(gameObject);

            if (KebammPrototypeBootstrap.Instance != null)
                KebammPrototypeBootstrap.Instance.SpawnMergedBall(mid, colour, nextTier, startDiameter, mergedInPlay);
        }
    }
}
