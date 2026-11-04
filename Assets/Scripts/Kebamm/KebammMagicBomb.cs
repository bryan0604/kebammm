using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Magic Bomb behaviour on a live bomb ball (KebammBall.IsBomb). The timer starts on the first touch with
    /// anything in the jar (wall, base/floor or a ball); after bomb_touch_delay it explodes: every ball within
    /// bomb_explode_range takes bomb_explode_damage (ball health - damage; 0 or less = destroyed) and survivors are
    /// pushed away with bomb_explode_strength (impulse). Balls only: no monsters, obstacles, score or merges.
    /// </summary>
    public class KebammMagicBomb : MonoBehaviour
    {
        const float UnarmedLifetime = 10f; // safety: a bomb that never touches the jar is removed

        bool _armed;
        bool _exploded;
        float _timer;
        float _life;

        static readonly HashSet<KebammBall> Hits = new HashSet<KebammBall>();

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (_armed || _exploded || collision.collider.isTrigger)
                return;
            if (!IsJarContact(collision.collider))
                return;
            _armed = true;
            _timer = 0f;
            Debug.Log($"[Kebamm] Magic Bomb touched {collision.collider.name}; exploding in {Delay():0.##}s");
        }

        static bool IsJarContact(Collider2D col)
        {
            var ball = col.GetComponent<KebammBall>();
            if (ball != null)
                return !ball.IsPreview;
            var boot = KebammPrototypeBootstrap.Instance;
            var container = boot != null ? boot.Container : null;
            return container != null && col.transform.IsChildOf(container.transform);
        }

        static KebammGameManagerData Gm => KebammPrototypeBootstrap.Instance != null ? KebammPrototypeBootstrap.Instance.GameManagerData : null;
        static float Delay() => Gm != null ? Gm.BombTouchDelay : 1.2f;

        void Update()
        {
            if (_exploded)
                return;
            if (!_armed)
            {
                _life += Time.deltaTime;
                if (_life >= UnarmedLifetime)
                {
                    Debug.LogWarning("[Kebamm] Magic Bomb never touched the jar; removed without exploding.");
                    _exploded = true;
                    Destroy(gameObject);
                }
                return;
            }
            _timer += Time.deltaTime;
            if (_timer >= Delay())
                Explode();
        }

        void Explode()
        {
            _exploded = true;
            var gm = Gm;
            float range = gm != null ? gm.BombExplodeRange : 8f;
            int damage = gm != null ? gm.BombExplodeDamage : 10;
            int strength = gm != null ? gm.BombExplodeStrength : 3;
            Vector2 centre = transform.position;

            Hits.Clear();
            foreach (Collider2D col in Physics2D.OverlapCircleAll(centre, range))
            {
                var ball = col != null ? col.GetComponent<KebammBall>() : null;
                if (ball != null && !ball.IsBomb && !ball.IsPreview && !ball.IsMerging)
                    Hits.Add(ball);
            }

            int destroyed = 0, pushed = 0;
            foreach (KebammBall ball in Hits)
            {
                if (ball.TakeDamage(damage))
                {
                    destroyed++;
                    Destroy(ball.gameObject);
                    continue;
                }
                var rb = ball.Body;
                if (rb == null || strength == 0)
                    continue;
                Vector2 dir = (Vector2)ball.transform.position - centre;
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.up;
                rb.AddForce(dir * strength, ForceMode2D.Impulse);
                pushed++;
            }
            Debug.Log($"[Kebamm] Magic Bomb exploded at ({centre.x:0.00}, {centre.y:0.00}) range={range} damage={damage} strength={strength}: balls hit={Hits.Count} destroyed={destroyed} pushed={pushed}");
            Hits.Clear();
            Destroy(gameObject);
        }
    }
}
