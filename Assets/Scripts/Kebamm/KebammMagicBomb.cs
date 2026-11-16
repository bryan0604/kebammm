using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Magic Bomb behaviour on a live bomb ball (KebammBall.IsBomb). The timer starts on the first touch with
    /// anything in the jar (wall, base/floor or a ball); after bomb_touch_delay it explodes: every ball within
    /// bomb_explode_range takes bomb_explode_damage (ball health - damage; 0 or less = destroyed) and survivors are
    /// pushed away with bomb_explode_strength (impulse). Balls and rocks only: no monsters, obstacles or merges.
    /// Each explosion adds the flat magic_bomb_explode_score bonus to the Monster score line.
    /// </summary>
    public class KebammMagicBomb : MonoBehaviour
    {
        const float UnarmedLifetime = 10f; // safety: a bomb that never touches the jar is removed

        bool _armed;
        bool _exploded;
        float _timer;
        float _life;

        SpriteRenderer _overlay; // placeholder explosion-area circle (no collider), own root so bomb scale doesn't apply

        static readonly HashSet<KebammBall> Hits = new HashSet<KebammBall>();
        static readonly HashSet<KebammRock> RockHits = new HashSet<KebammRock>();

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (_armed || _exploded || collision.collider.isTrigger)
                return;
            if (!IsJarContact(collision.collider))
                return;
            _armed = true;
            _timer = 0f;
            ShowOverlay();
            Debug.Log($"[Kebamm] Magic Bomb touched {collision.collider.name}; exploding in {Delay():0.##}s");
        }

        static bool IsJarContact(Collider2D col)
        {
            var ball = col.GetComponent<KebammBall>();
            if (ball != null)
                return !ball.IsPreview;
            if (col.GetComponent<KebammRock>() != null)
                return true;
            var boot = KebammPrototypeBootstrap.Instance;
            var container = boot != null ? boot.Container : null;
            return container != null && col.transform.IsChildOf(container.transform);
        }

        static KebammGameManagerData Gm => KebammPrototypeBootstrap.Instance != null ? KebammPrototypeBootstrap.Instance.GameManagerData : null;
        static float Delay() => Gm != null ? Gm.MagicBombTouchDelay : 1.2f;

        static float Range() => Gm != null ? Gm.MagicBombExplodeRange : 8f;

        /// <summary>Circle of diameter 2 x bomb_explode_range (OverlapCircleAll uses range as a radius).</summary>
        void ShowOverlay()
        {
            if (_overlay != null)
                return;
            KebammComboConfig cfg = KebammCombo.Config;
            var go = new GameObject("MagicBombRange");
            go.transform.position = transform.position;
            _overlay = go.AddComponent<SpriteRenderer>();
            _overlay.sprite = KebammVisualFactory.CircleSprite; // 1 world unit across at scale 1
            Color c = cfg.overlayColor;
            c.a = cfg.overlayAlpha;
            _overlay.color = c;
            _overlay.sortingOrder = 30; // above balls (0..20), below HUD
            UpdateOverlay();
            Debug.Log($"[Kebamm] Magic Bomb range overlay: radius {Range():0.##}, diameter {Range() * 2f:0.##} world units");
        }

        void UpdateOverlay()
        {
            if (_overlay == null)
                return;
            _overlay.transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
            _overlay.transform.localScale = Vector3.one * (Range() * 2f); // live range from data
        }

        void LateUpdate()
        {
            if (!_exploded)
                UpdateOverlay();
        }

        void FlashOverlayAndRelease()
        {
            if (_overlay == null)
                return;
            UpdateOverlay();
            KebammComboConfig cfg = KebammCombo.Config;
            SpriteRenderer sr = _overlay;
            _overlay = null;
            Color c = sr.color;
            c.a = cfg.overlayFlashAlpha;
            sr.color = c;
            sr.DOFade(0f, Mathf.Max(0.01f, cfg.overlayFadeDuration))
                .SetEase(Ease.OutQuad)
                .SetLink(sr.gameObject)
                .OnComplete(() => { if (sr != null) Destroy(sr.gameObject); });
        }

        void OnDestroy()
        {
            if (_overlay != null)
                Destroy(_overlay.gameObject); // bomb removed before exploding (reset, unarmed timeout)
        }

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
            float range = Range();
            int damage = gm != null ? gm.MagicBombExplodeDamage : 10;
            int strength = gm != null ? gm.MagicBombExplodeStrength : 3;
            Vector2 centre = transform.position;

            Hits.Clear();
            RockHits.Clear();
            foreach (Collider2D col in Physics2D.OverlapCircleAll(centre, range))
            {
                var ball = col != null ? col.GetComponent<KebammBall>() : null;
                if (ball != null && !ball.IsBomb && !ball.IsPreview && !ball.IsMerging)
                    Hits.Add(ball);
                var rock = col != null ? col.GetComponent<KebammRock>() : null;
                if (rock != null && rock.Landed)
                    RockHits.Add(rock);
            }
            // Trebuchet rocks take damage like balls.
            foreach (KebammRock rock in RockHits)
            {
                if (rock.TakeDamage(damage))
                {
                    Debug.Log($"[Kebamm] Magic Bomb destroyed {rock.name}");
                    Destroy(rock.gameObject);
                    continue;
                }
                var rrb = rock.Body;
                if (rrb == null || strength == 0)
                    continue;
                Vector2 rdir = (Vector2)rock.transform.position - centre;
                rdir = rdir.sqrMagnitude > 1e-6f ? rdir.normalized : Vector2.up;
                rrb.AddForce(rdir * strength, ForceMode2D.Impulse);
            }
            RockHits.Clear();

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
            KebammScore.AddMagicBomb(gm != null ? gm.MagicBombExplodeScore : 500);
            KebammMagicBombFx.Play(centre, range);
            FlashOverlayAndRelease();
            Destroy(gameObject);
        }
    }
}
