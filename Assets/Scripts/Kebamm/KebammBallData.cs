using UnityEngine;

namespace Kebamm
{
    [CreateAssetMenu(fileName = "BallData", menuName = "Kebamm/Ball Data")]
    public class KebammBallData : ScriptableObject
    {
        public int id;
        public KebammBallTierData tierData;
    }
}