using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// GDD Gameplay Explosion Bits: when a monster, obstacle or trebuchet is destroyed it bursts into square bits
    /// in its own colour. Bits count = (width / bit size) x (height / bit size), laid out as a grid over the host.
    /// Each bit gets its own upward force, sideways spread and gravity, bounces off monsters only, deals no damage,
    /// and fades out at the end of its lifetime. Pooled; at most explosion_bit_max_on_screen at once (oldest recycled).
    /// Settings: Game Manager data (explosion_bit_*).
    /// </summary>
    public class KebammExplosionBits : MonoBehaviour
    {
        class Bit
        {
            public Transform t;
            public SpriteRenderer sr;
            public Vector2 v;
            public float gravity;
            public float age;
            public float life;
            public float spin;
            public Color colour;
            public Collider2D ignore;
        }

        const int SortingOrder = 22;        // above balls (10..20), below rocks (25) and HUD
        const float FadeShare = 0.35f;      // the last 35% of the lifetime fades out
        const float BounceDamping = 0.55f;  // speed kept after bouncing off a monster
        const float MaxSpin = 540f;         // deg/s

        static KebammExplosionBits _instance;
        readonly List<Bit> _active = new List<Bit>();
        readonly Stack<Bit> _pool = new Stack<Bit>();

        static KebammExplosionBits Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new GameObject("KebammExplosionBits").AddComponent<KebammExplosionBits>();
                return _instance;
            }
        }

        /// <summary>Burst a destroyed host (its world bounds) into bits of its colour. ignore = the host's own collider.</summary>
        public static void Burst(Bounds area, Color colour, Collider2D ignore = null)
        {
            var boot = KebammPrototypeBootstrap.Instance;
            var gm = boot != null ? boot.GameManagerData : null;
            float size = gm != null ? gm.ExplosionBitSize : 0.2f;
            int maxPerSide = gm != null ? gm.ExplosionBitMaxPerSide : 12;
            int cols = Mathf.Clamp(Mathf.RoundToInt(area.size.x / size), 1, maxPerSide);
            int rows = Mathf.Clamp(Mathf.RoundToInt(area.size.y / size), 1, maxPerSide);
            Instance.Spawn(area, cols, rows, size, colour, ignore, gm);
        }

        /// <summary>Removes every bit (level end / restart).</summary>
        public static void ClearAll()
        {
            if (_instance == null)
                return;
            for (int i = _instance._active.Count - 1; i >= 0; i--)
                _instance.Recycle(i);
        }

        void Spawn(Bounds area, int cols, int rows, float size, Color colour, Collider2D ignore, KebammGameManagerData gm)
        {
            float upMin = gm != null ? gm.ExplosionBitUpForceMin : 4f;
            float upMax = gm != null ? gm.ExplosionBitUpForceMax : 8f;
            float spread = gm != null ? gm.ExplosionBitSideSpread : 3f;
            float gMin = gm != null ? gm.ExplosionBitGravityMin : 1f;
            float gMax = gm != null ? gm.ExplosionBitGravityMax : 2f;
            float life = gm != null ? gm.ExplosionBitLifetime : 1.2f;
            int cap = gm != null ? gm.ExplosionBitMaxOnScreen : 150;
            float g = Mathf.Abs(Physics2D.gravity.y);
            colour.a = 1f;

            float cellW = area.size.x / cols;
            float cellH = area.size.y / rows;
            Vector2 min = area.min;
            float halfW = Mathf.Max(0.01f, area.extents.x);
            int spawned = 0;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    if (cap <= 0)
                        return;
                    while (_active.Count >= cap)
                        Recycle(0); // oldest first, so the newest burst always shows
                    Bit b = Take();
                    Vector2 p = new Vector2(min.x + cellW * (c + 0.5f), min.y + cellH * (r + 0.5f));
                    float outward = (p.x - area.center.x) / halfW; // -1..1, bits on the left fly left
                    b.t.position = new Vector3(p.x, p.y, 0f);
                    b.t.localScale = new Vector3(size, size, 1f);
                    b.t.rotation = Quaternion.identity;
                    b.v = new Vector2(outward * spread * 0.5f + Random.Range(-spread, spread) * 0.5f, Random.Range(upMin, upMax));
                    b.gravity = Random.Range(gMin, gMax) * g;
                    b.spin = Random.Range(-MaxSpin, MaxSpin);
                    b.age = 0f;
                    b.life = Mathf.Max(0.1f, life * Random.Range(0.85f, 1.15f));
                    b.colour = colour;
                    b.sr.color = colour;
                    b.ignore = ignore;
                    b.t.gameObject.SetActive(true);
                    _active.Add(b);
                    spawned++;
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Kebamm] Explosion bits: {spawned} ({cols}x{rows}, size {size}) - {_active.Count} on screen");
#endif
        }

        Bit Take()
        {
            if (_pool.Count > 0)
                return _pool.Pop();
            var go = new GameObject("Bit");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = KebammVisualFactory.SquareSprite;
            sr.sortingOrder = SortingOrder;
            return new Bit { t = go.transform, sr = sr };
        }

        void Recycle(int index)
        {
            Bit b = _active[index];
            _active.RemoveAt(index);
            if (b.t != null)
            {
                b.t.gameObject.SetActive(false);
                _pool.Push(b);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _active.Count == 0)
                return; // paused
            KebammMonster.LiveColliders.RemoveAll(c => c == null);
            var monsters = KebammMonster.LiveColliders;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Bit b = _active[i];
                if (b.t == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }
                b.age += dt;
                if (b.age >= b.life)
                {
                    Recycle(i);
                    continue;
                }
                b.v.y -= b.gravity * dt;
                Vector2 p = (Vector2)b.t.position + b.v * dt;

                // Bits collide with monsters only (bounce off, no damage).
                for (int m = 0; m < monsters.Count; m++)
                {
                    Collider2D col = monsters[m];
                    if (col == b.ignore || !col.enabled || !col.gameObject.activeInHierarchy)
                        continue;
                    if (!col.bounds.Contains(new Vector3(p.x, p.y, col.bounds.center.z)) || !col.OverlapPoint(p))
                        continue;
                    Vector2 n = p - (Vector2)col.bounds.center;
                    n = n.sqrMagnitude > 0.0001f ? n.normalized : Vector2.up;
                    if (Vector2.Dot(b.v, n) < 0f)
                        b.v = Vector2.Reflect(b.v, n) * BounceDamping;
                    p = (Vector2)b.t.position; // stay outside this frame
                    break;
                }

                b.t.position = new Vector3(p.x, p.y, 0f);
                b.t.Rotate(0f, 0f, b.spin * dt);
                float fadeStart = b.life * (1f - FadeShare);
                if (b.age > fadeStart)
                {
                    Color c = b.colour;
                    c.a = 1f - (b.age - fadeStart) / (b.life - fadeStart);
                    b.sr.color = c;
                }
            }
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}