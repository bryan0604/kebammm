using UnityEngine;

namespace Kebamm
{
    [CreateAssetMenu(fileName = "MonsterData", menuName = "Kebamm/Monster Data")]
    public class KebammMonsterData : ScriptableObject
    {
        public int id;
        public float hp = 100f;
        [Range(1, 3)] public int tier = 1;
    }
}