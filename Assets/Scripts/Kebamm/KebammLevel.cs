using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    [CreateAssetMenu(fileName = "Level", menuName = "Kebamm/Level")]
    public class KebammLevel : ScriptableObject
    {
        public const int MinBallTier = 1;
        public const int MaxBallTier = 5;

        [Header("Level Information")]
        public int levelNumber = 1;
        public int monsterQty = 3;

        [Header("Prepopulate")]
        public bool levelPrepopulate = true;
        public int prepopulateMonsterNumber = 3;
        public List<KebammBallColour> prepopulateBallColours = new List<KebammBallColour>
        {
            KebammBallColour.Red,
            KebammBallColour.Blue
        };
        [Range(MinBallTier, MaxBallTier)] public int prepopulateBallTierMin = 1;
        [Range(MinBallTier, MaxBallTier)] public int prepopulateBallTierMax = 2;
        public int prepopulateObstacleQty;
        [Tooltip("x = min, y = max")] public Vector2 prepopulateMonsterPositionRangeX = new Vector2(-2f, 2f);
        [Tooltip("x = min, y = max")] public Vector2 prepopulateMonsterPositionRangeY = new Vector2(0f, 1f);
        [Tooltip("x = min, y = max")] public Vector2 prepopulateObstaclePositionRangeX = new Vector2(-2f, 2f);
        [Tooltip("x = min, y = max")] public Vector2 prepopulateObstaclePositionRangeY = new Vector2(0f, 1f);

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

            prepopulateBallTierMin = Mathf.Clamp(prepopulateBallTierMin, MinBallTier, MaxBallTier);
            prepopulateBallTierMax = Mathf.Clamp(prepopulateBallTierMax, MinBallTier, MaxBallTier);
            if (prepopulateBallTierMax < prepopulateBallTierMin)
                prepopulateBallTierMax = prepopulateBallTierMin;

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