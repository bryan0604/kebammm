using UnityEngine;

namespace Kebamm
{
    [CreateAssetMenu(fileName = "MonsterData", menuName = "Kebamm/Monster Data")]
    public class KebammMonsterData : ScriptableObject
    {
        public int id;
        public float hp = 100f;
        [Range(1, 3)] public int tier = 1;
        public int hitPts;
        public int destroyedPts;

        public void ResolveScore(out int hit, out int destroyed)
        {
            FallbackPoints(tier, out int fallbackHit, out int fallbackDestroyed);
            hit = hitPts > 0 ? hitPts : fallbackHit;
            destroyed = destroyedPts > 0 ? destroyedPts : fallbackDestroyed;
        }

        public static void FallbackPoints(int tier, out int hitPts, out int destroyedPts)
        {
            switch (Mathf.Clamp(tier, 1, 3))
            {
                case 1:
                    hitPts = 13;
                    destroyedPts = 250;
                    break;
                case 2:
                    hitPts = 26;
                    destroyedPts = 500;
                    break;
                default:
                    hitPts = 52;
                    destroyedPts = 1000;
                    break;
            }
        }
    }
}