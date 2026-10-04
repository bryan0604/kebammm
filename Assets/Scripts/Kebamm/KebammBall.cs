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

        Rigidbody2D _rb;
        CircleCollider2D _col;
        SpriteRenderer _sr;

        public float Radius => 0.28f + Tier * 0.14f;

        public void Configure(KebammBallColour colour, int tier, bool preview)
        {
            Colour = colour;
            Tier = Mathf.Clamp(tier, 0, MaxTier);
            IsPreview = preview;
            IsMerging = false;
            KebammBallTierData.Resolve(Tier, out float damage, out float hp);
            Damage = damage;
            Health = hp;

            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<CircleCollider2D>();
            _sr = GetComponent<SpriteRenderer>();

            float radius = Radius;
            transform.localScale = Vector3.one * (radius * 2f);
            _col.radius = 0.5f;
            _sr.sprite = KebammVisualFactory.CircleSprite;
            _sr.color = KebammVisualFactory.ColourFor(colour);
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
                _rb.gravityScale = 1.4f;
                _rb.mass = 1f + Tier * 0.75f;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                _rb.sharedMaterial = KebammPrototypeBootstrap.SharedPhysicsMaterial;
                _col.enabled = true;
                _col.sharedMaterial = KebammPrototypeBootstrap.SharedPhysicsMaterial;
            }

            name = $"Ball_{colour}_T{Tier}";
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

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsPreview || IsMerging)
                return;

            var other = collision.collider.GetComponent<KebammBall>();
            if (other == null || other.IsPreview || other.IsMerging)
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
            int nextTier = Tier + 1;
            var colour = Colour;

            Destroy(other.gameObject);
            Destroy(gameObject);

            if (KebammPrototypeBootstrap.Instance != null)
                KebammPrototypeBootstrap.Instance.SpawnMergedBall(mid, colour, nextTier);
        }
    }
}
