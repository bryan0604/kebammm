using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    [CreateAssetMenu(fileName = "BallTierData", menuName = "Kebamm/Ball Tier Data")]
    public class KebammBallTierData : ScriptableObject
    {
        public int tier = 1;
        public float damage;
        public float hp = 1f;
        public int mergePts;
        [Tooltip("Launch speed (units/s) given to a monster hit by this tier: up + away from the impact side.")]
        public float impactStrength = 3f;
        [Tooltip("Ball diameter in world units for this tier (spawn, held ball, merge grow, collider and art all scale to it). 0 = fallback formula 2*(0.28+0.14*(tier-1)).")]
        public float ballSize;
        [Tooltip("Ball On Impact explosion radius (world units) when this ball directly hits a monster or obstacle. Notion impact_aoe_range. < 0 = built-in fallback.")]
        public float impactAoeRange = -1f;
        [Tooltip("Rigidbody2D gravity scale for balls of this tier while the bombing run is active (container floor open). Notion bombing_run_gravity_scale. Normal play uses 1.4. Lower = falls slower. <= 0 = normal gravity.")]
        public float bombingRunGravityScale = 1.4f;
        [Tooltip("Multiplies only the UPWARD (+Y) part of the push this tier gives a monster it hits (direct hit and impact explosion). Sideways and downward are unchanged. Notion impact_upward_multiplier. 1 = unchanged, 0 = no upward push, negative = 0.")]
        public float impactUpwardMultiplier = 1f;

        static Dictionary<int, KebammBallTierData> _byTier;
        // Built-in fallbacks when a tier row is missing (Notion Ball Tier Data v14).
        // Index 5 = T6 (merge-only top tier). Only used if the ball_tier_06 row is missing too; it repeats T5 (old behaviour).
        static readonly float[] DefaultDamage = { 50f, 100f, 150f, 200f, 250f, 250f };
        static readonly int[] DefaultMergePtsByTier = { 20, 40, 80, 190, 380, 380 };
        // impact_aoe_range fallbacks T1..T5 (Notion v19); T6 repeats T5.
        static readonly float[] DefaultImpactAoeRange = { 0.5f, 0.75f, 1.0f, 1.4f, 1.8f, 1.8f };

        /// <summary>Highest data tier with a row (ball_tier_06 = merge-only top tier, code play tier 5).</summary>
        public const int MaxDataTier = 6;

        /// <summary>Row for a code tier (0-based). T6 uses ball_tier_06; if that row is missing it falls back to the T5 row.</summary>
        static KebammBallTierData RowFor(int playTier, out int dataTier)
        {
            dataTier = Mathf.Clamp(playTier + 1, 1, MaxDataTier);
            Ensure();
            if (_byTier == null)
                return null;
            if (_byTier.TryGetValue(dataTier, out KebammBallTierData row) && row != null)
                return row;
            if (dataTier == MaxDataTier && _byTier.TryGetValue(MaxDataTier - 1, out row) && row != null)
                return row;
            return null;
        }

        public static void Resolve(int playTier, out float damage, out float hp)
        {
            Resolve(playTier, out damage, out hp, out _);
        }

        public static int MergePointsForPlayTier(int playTier)
        {
            Resolve(playTier, out _, out _, out int mergePts);
            return mergePts;
        }

        public static void Resolve(int playTier, out float damage, out float hp, out int mergePts)
        {
            KebammBallTierData row = RowFor(playTier, out int dataTier);
            if (row != null)
            {
                damage = row.damage;
                hp = row.hp > 0f ? row.hp : 1f;
                mergePts = row.mergePts > 0 ? row.mergePts : DefaultMergePts(dataTier);
                return;
            }

            damage = DefaultDamage[dataTier - 1];
            hp = 1f;
            mergePts = DefaultMergePts(dataTier);
        }

        public static float ImpactStrengthForPlayTier(int playTier)
        {
            KebammBallTierData row = RowFor(playTier, out int dataTier);
            if (row != null && row.impactStrength > 0f)
                return row.impactStrength;
            return 2f + dataTier * 0.5f;
        }

        /// <summary>Upward push multiplier for a code tier (0-based): row value clamped to 0 or more; missing row = 1.</summary>
        public static float ImpactUpwardMultiplierForPlayTier(int playTier)
        {
            KebammBallTierData row = RowFor(playTier, out _);
            return row != null ? Mathf.Max(0f, row.impactUpwardMultiplier) : 1f;
        }

        /// <summary>Impact explosion radius for a code tier (0-based). T6 uses ball_tier_06 (T5 row if missing).</summary>
        public static float ImpactAoeRangeForPlayTier(int playTier)
        {
            KebammBallTierData row = RowFor(playTier, out int dataTier);
            if (row != null && row.impactAoeRange >= 0f)
                return row.impactAoeRange;
            return DefaultImpactAoeRange[dataTier - 1];
        }

        /// <summary>Normal (non bombing run) ball gravity scale.</summary>
        public const float NormalGravityScale = 1.4f;

        /// <summary>Bombing run gravity scale for a code tier (0-based). T6 uses ball_tier_06 (T5 row if missing). Missing / <= 0 = normal 1.4.</summary>
        public static float BombingRunGravityForPlayTier(int playTier)
        {
            KebammBallTierData row = RowFor(playTier, out int dataTier);
            if (row != null && row.bombingRunGravityScale > 0f)
                return row.bombingRunGravityScale;
            return NormalGravityScale;
        }

        /// <summary>Diameter for a code tier (0-based). Uses the tier row's ballSize (T6 = ball_tier_06); a missing row or 0 uses the formula.</summary>
        public static float DiameterForPlayTier(int playTier)
        {
            Ensure();
            if (_byTier != null && _byTier.TryGetValue(playTier + 1, out KebammBallTierData row) && row != null && row.ballSize > 0f)
                return row.ballSize;
            return DefaultDiameterForPlayTier(playTier);
        }

        public static float DefaultDiameterForPlayTier(int playTier)
        {
            return 2f * (0.28f + Mathf.Max(0, playTier) * 0.14f);
        }

        public static int DefaultMergePts(int dataTier)
        {
            dataTier = Mathf.Clamp(dataTier, 1, MaxDataTier);
            return DefaultMergePtsByTier[dataTier - 1];
        }

        /// <summary>Drops the tier lookup so the next call re-reads (used when remote data is applied).</summary>
        public static void ClearCache()
        {
            _byTier = null;
        }

        static void Ensure()
        {
            if (_byTier != null)
                return;

            _byTier = new Dictionary<int, KebammBallTierData>();
            KebammBallTierData[] rows = Resources.LoadAll<KebammBallTierData>("Kebamm");
            for (int i = 0; i < rows.Length; i++)
            {
                KebammBallTierData row = rows[i];
                if (row != null)
                {
                    KebammBallTierData r = KebammRemoteData.ForBallTier(row); // remote override copy, or the asset
                    _byTier[r.tier] = r;
                }
            }
        }
    }
}