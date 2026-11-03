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

        static Dictionary<int, KebammBallTierData> _byTier;

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

            damage = dataTier * 10f;
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
            return dataTier * 20;
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
                    _byTier[row.tier] = row;
            }
        }
    }
}