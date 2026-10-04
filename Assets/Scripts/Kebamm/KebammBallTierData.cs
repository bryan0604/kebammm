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

        static Dictionary<int, KebammBallTierData> _byTier;

        public static void Resolve(int playTier, out float damage, out float hp)
        {
            int dataTier = Mathf.Clamp(playTier + 1, 1, 5);
            Ensure();
            if (_byTier != null && _byTier.TryGetValue(dataTier, out KebammBallTierData row) && row != null)
            {
                damage = row.damage;
                hp = row.hp > 0f ? row.hp : 1f;
                return;
            }

            damage = dataTier * 10f;
            hp = 1f;
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