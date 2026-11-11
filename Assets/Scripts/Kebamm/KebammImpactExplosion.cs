using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Ball On Impact (GDD): a ball that directly hits a monster or obstacle explodes and is removed, whatever its HP.
    /// Every other monster/obstacle within the tier's impact_aoe_range (radius) takes the ball's damage once and is
    /// pushed away from the impact point with the tier's impact strength. Balls are never affected.
    /// The directly hit target already took its damage from the contact, so it is skipped here.
    /// </summary>
    public static class KebammImpactExplosion
    {
        static readonly HashSet<Component> Hit = new HashSet<Component>();

        public static void Explode(KebammBall ball, Vector2 point, Component directTarget)
        {
            if (ball == null || ball.Spent)
                return;
            ball.MarkSpent();

            float range = KebammBallTierData.ImpactAoeRangeForPlayTier(ball.Tier);
            float damage = ball.Damage;
            float strength = ball.ImpactStrength;
            int monsters = 0, obstacles = 0;

            if (range > 0f)
            {
                Hit.Clear();
                Collider2D[] cols = Physics2D.OverlapCircleAll(point, range);
                for (int i = 0; i < cols.Length; i++)
                {
                    Collider2D c = cols[i];
                    if (c == null || c.isTrigger)
                        continue;
                    var m = c.GetComponentInParent<KebammMonster>();
                    if (m != null)
                    {
                        if (m.IsDefeated)
                            continue; // dead monsters are untouchable: no damage, no push
                        if (m != directTarget && Hit.Add(m))
                        {
                            m.ReceiveHit(damage, strength, point.x, "explosion", ball.ImpactUpwardMultiplier); // upward part scaled per tier
                            monsters++;
                        }
                        continue;
                    }
                    var tb = c.GetComponentInParent<KebammTrebuchet>();
                    if (tb != null)
                    {
                        if (tb != directTarget && Hit.Add(tb))
                        {
                            tb.ReceiveHit(damage, "explosion"); // obstacle rule, no push (stationary)
                            obstacles++;
                        }
                        continue;
                    }
                    var o = c.GetComponentInParent<KebammObstacle>();
                    if (o != null && o != directTarget && Hit.Add(o))
                    {
                        o.ReceiveHit(damage, strength, point.x, "explosion");
                        obstacles++;
                    }
                }
                Hit.Clear();
                ShowFlash(point, range);
            }

            string target = directTarget != null ? directTarget.GetType().Name.Replace("Kebamm", "") : "ground";
            Debug.Log($"[Kebamm] Ball T{ball.Tier + 1} impact explosion on {target} at ({point.x:0.##},{point.y:0.##}) radius {range:0.##}: damage {damage} to {monsters} other monster(s), {obstacles} other obstacle(s). Ball removed (HP was {ball.Health}).");
            Object.Destroy(ball.gameObject);
        }

        /// <summary>Quick orange flash sized to the radius, fading out (same circle overlay as the Magic Bomb range).</summary>
        static void ShowFlash(Vector2 point, float range)
        {
            KebammComboConfig cfg = KebammCombo.Config;
            if (cfg == null || !cfg.impactOverlayEnabled)
                return;
            var go = new GameObject("BallImpactExplosion");
            go.transform.position = new Vector3(point.x, point.y, 0f);
            go.transform.localScale = Vector3.one * (range * 2f); // CircleSprite is 1 world unit across
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = KebammVisualFactory.CircleSprite;
            Color c = cfg.impactOverlayColor;
            c.a = cfg.impactOverlayAlpha;
            sr.color = c;
            sr.sortingOrder = 30; // above balls (0..20), below HUD
            sr.DOFade(0f, Mathf.Max(0.01f, cfg.impactOverlayFadeDuration))
                .SetEase(Ease.OutQuad)
                .SetLink(go)
                .OnComplete(() => { if (go != null) Object.Destroy(go); });
        }
    }
}
