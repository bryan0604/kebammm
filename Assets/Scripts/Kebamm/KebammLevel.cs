using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Kebamm
{
    [CreateAssetMenu(fileName = "Level", menuName = "Kebamm/Level")]
    public class KebammLevel : ScriptableObject
    {
        public const int MinMonsterTier = 1;
        public const int MaxMonsterTier = 5;

        [Header("Level Information")]
        public int levelNumber = 1;
        public int monsterQty = 3;

        [Header("Prepopulate")]
        public bool levelPrepopulate = true;
        // Unused: monster count comes from monsterQty only. Kept so existing assets still load.
        [Tooltip("Unused - monster count comes from Monster Qty.")] public int prepopulateMonsterNumber = 3;
        public List<KebammBallColour> prepopulateBallColours = new List<KebammBallColour>
        {
            KebammBallColour.Red,
            KebammBallColour.Blue
        };
        [Tooltip("Lowest monster data tier spawned in this level.")][FormerlySerializedAs("prepopulateBallTierMin")][Range(MinMonsterTier, MaxMonsterTier)] public int prepopulateMonsterTierMin = 1;
        [Tooltip("Highest monster data tier spawned in this level.")][FormerlySerializedAs("prepopulateBallTierMax")][Range(MinMonsterTier, MaxMonsterTier)] public int prepopulateMonsterTierMax = 2;
        public int prepopulateObstacleQty;
        [Tooltip("x = min, y = max")] public Vector2 prepopulateMonsterPositionRangeX = new Vector2(-2f, 2f);
        [Tooltip("x = min, y = max")] public Vector2 prepopulateMonsterPositionRangeY = new Vector2(0f, 1f);
        [Tooltip("x = min, y = max")] public Vector2 prepopulateObstaclePositionRangeX = new Vector2(-2f, 2f);
        [Tooltip("x = min, y = max")] public Vector2 prepopulateObstaclePositionRangeY = new Vector2(0f, 1f);
        [Tooltip("Chance (0-1) each prepopulated monster after the first stacks on a random existing stack.")][Range(0f, 1f)] public float monsterStackChance;

        [Header("Obstacles")]
        [Tooltip("Populate building obstacles at level start. Uses Prepopulate Obstacle Qty and Obstacle Position Range X.")] public bool obstaclePopulate;
        [Tooltip("Chance (0-1) each building after the first stacks on a random existing building stack (max 3 per stack).")][Range(0f, 1f)] public float obstacleStackChance;
        [Tooltip("Shapes this level may spawn. Empty = all shapes.")] public List<KebammObstacleShape> allowedObstacleShapes = new List<KebammObstacleShape>();

        // Dropped ball tier (GDD / Ball Tier Data tiers 1-5; code play tier = GDD tier - 1). 0 = unset.
        // If min or max is unset, only the smallest ball (tier 1) is dropped.
        public const int MinBallTier = 1;
        public const int MaxBallTier = 5;
        [Header("Ball Tier Spawning")]
        [Tooltip("Lowest ball tier (1-5) the launcher can load. 0 = unset.")][Range(0, MaxBallTier)] public int ballTierMin;
        [Tooltip("Highest ball tier (1-5) the launcher can load. 0 = unset.")][Range(0, MaxBallTier)] public int ballTierMax;
        [Tooltip("Relative chance for tier 1. 0 = excluded. Only tiers inside min-max are rolled.")][Min(0f)] public float ballTier1Weight;
        [Tooltip("Relative chance for tier 2. 0 = excluded. Only tiers inside min-max are rolled.")][Min(0f)] public float ballTier2Weight;
        [Tooltip("Relative chance for tier 3. 0 = excluded. Only tiers inside min-max are rolled.")][Min(0f)] public float ballTier3Weight;
        [Tooltip("Relative chance for tier 4. 0 = excluded. Only tiers inside min-max are rolled.")][Min(0f)] public float ballTier4Weight;
        [Tooltip("Relative chance for tier 5. 0 = excluded. Only tiers inside min-max are rolled.")][Min(0f)] public float ballTier5Weight;

        public float BallTierWeight(int gddTier)
        {
            switch (gddTier)
            {
                case 1: return ballTier1Weight;
                case 2: return ballTier2Weight;
                case 3: return ballTier3Weight;
                case 4: return ballTier4Weight;
                case 5: return ballTier5Weight;
                default: return 0f;
            }
        }

        /// <summary>
        /// Weighted roll of the next dropped ball, returned as a code play tier (GDD tier - 1).
        /// Unset min/max -> tier 1 only. Tiers outside min-max or with weight 0 are skipped;
        /// if every weight in range is 0, tiers in range are equally likely.
        /// </summary>
        public int RollDropPlayTier()
        {
            if (ballTierMin <= 0 || ballTierMax <= 0)
                return 0;
            int lo = Mathf.Clamp(Mathf.Min(ballTierMin, ballTierMax), MinBallTier, MaxBallTier);
            int hi = Mathf.Clamp(Mathf.Max(ballTierMin, ballTierMax), MinBallTier, MaxBallTier);
            float total = 0f;
            for (int t = lo; t <= hi; t++)
                total += Mathf.Max(0f, BallTierWeight(t));
            if (total <= 0f)
                return Random.Range(lo, hi + 1) - 1;
            float r = Random.value * total;
            for (int t = lo; t <= hi; t++)
            {
                float w = Mathf.Max(0f, BallTierWeight(t));
                if (w <= 0f)
                    continue;
                if (r < w)
                    return t - 1;
                r -= w;
            }
            for (int t = hi; t >= lo; t--)
                if (BallTierWeight(t) > 0f)
                    return t - 1;
            return lo - 1;
        }

        void OnValidate()
        {
            if (levelNumber < 1)
                levelNumber = 1;
            if (monsterQty < 0)
                monsterQty = 0;
            if (prepopulateMonsterNumber < 0)
                prepopulateMonsterNumber = 0;
            if (prepopulateObstacleQty < 0)
                prepopulateObstacleQty = 0;

            prepopulateMonsterTierMin = Mathf.Clamp(prepopulateMonsterTierMin, MinMonsterTier, MaxMonsterTier);
            prepopulateMonsterTierMax = Mathf.Clamp(prepopulateMonsterTierMax, MinMonsterTier, MaxMonsterTier);
            if (prepopulateMonsterTierMax < prepopulateMonsterTierMin)
                prepopulateMonsterTierMax = prepopulateMonsterTierMin;

            ClampRange(ref prepopulateMonsterPositionRangeX);
            ClampRange(ref prepopulateMonsterPositionRangeY);
            ClampRange(ref prepopulateObstaclePositionRangeX);
            ClampRange(ref prepopulateObstaclePositionRangeY);
        }

        static void ClampRange(ref Vector2 range)
        {
            if (range.y < range.x)
                range.y = range.x;
        }
    }
}