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