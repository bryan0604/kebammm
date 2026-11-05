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

        static Dictionary<int, KebammBallTierData> _byTier;
        // Built-in fallbacks when a tier row is missing (Notion Ball Tier Data v14).
        static readonly float[] DefaultDamage = { 50f, 100f, 150f, 200f, 250f };
        static readonly int[] DefaultMergePtsByTier = { 20, 40, 80, 190, 380 };
        // impact_aoe_range fallbacks T1..T5 (Notion v19). The merge-only top tier (T6, no row) uses the T5 value.
        static readonly float[] DefaultImpactAoeRange = { 0.5f, 0.75f, 1.0f, 1.4f, 1.8f };

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
            int dataTier = Mathf.Clamp(playTier + 1, 1, 5);
            Ensure();
            if (_byTier != null && _byTier.TryGetValue(dataTier, out KebammBallTierData row) && row != null)
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
            int dataTier = Mathf.Clamp(playTier + 1, 1, 5);
            Ensure();
            if (_byTier != null && _byTier.TryGetValue(dataTier, out KebammBallTierData row) && row != null && row.impactStrength > 0f)
                return row.impactStrength;
            return 2f + dataTier * 0.5f;
        }

        /// <summary>Impact explosion radius for a code tier (0-based). T6 (no row) is clamped to the T5 row, like damage and impact strength.</summary>
        public static float ImpactAoeRangeForPlayTier(int playTier)
        {
            int dataTier = Mathf.Clamp(playTier + 1, 1, 5);
            Ensure();
            if (_byTier != null && _byTier.TryGetValue(dataTier, out KebammBallTierData row) && row != null && row.impactAoeRange >= 0f)
                return row.impactAoeRange;
            return DefaultImpactAoeRange[dataTier - 1];
        }

        /// <summary>Diameter for a code tier (0-based). Uses the tier row's ballSize; the merge-only top tier (no row) and 0/missing values use the formula.</summary>
        public static float DiameterForPlayTier(int playTier)
        {
            int dataTier = playTier + 1;
            Ensure();
            if (_byTier != null && _byTier.TryGetValue(dataTier, out KebammBallTierData row) && row != null && row.ballSize > 0f)
                return row.ballSize;
            return DefaultDiameterForPlayTier(playTier);
        }

        public static float DefaultDiameterForPlayTier(int playTier)
        {
            return 2f * (0.28f + Mathf.Max(0, playTier) * 0.14f);
        }

        public static int DefaultMergePts(int dataTier)
        {
            dataTier = Mathf.Clamp(dataTier, 1, 5);
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