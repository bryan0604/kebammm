using UnityEngine;

namespace Kebamm
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "Kebamm/Level Data")]
    public class KebammLevelData : ScriptableObject
    {
        public int monsterQty = 5;
        public int prepopulateMonsterNumber = 5;
        public bool levelPrepopulate = true;

        public int SpawnCountAtStart()
        {
            int count = levelPrepopulate ? prepopulateMonsterNumber : monsterQty;
            if (count < 0)
                count = 0;
            return count;
        }
    }
}