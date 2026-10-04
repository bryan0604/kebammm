using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    [Serializable]
    public class KebammTierEntry
    {
        public int tier = 1;
        public float size = 1f;
    }

    [CreateAssetMenu(fileName = "GameData", menuName = "Kebamm/Game Data")]
    public class KebammGameData : ScriptableObject
    {
        public List<KebammTierEntry> tiers = new List<KebammTierEntry>
        {
            new KebammTierEntry { tier = 1, size = 0.25f },
            new KebammTierEntry { tier = 2, size = 0.35f },
            new KebammTierEntry { tier = 3, size = 0.45f }
        };

        public float SizeForTier(int tier)
        {
            if (tiers != null)
            {
                for (int i = 0; i < tiers.Count; i++)
                {
                    KebammTierEntry entry = tiers[i];
                    if (entry != null && entry.tier == tier)
                        return entry.size;
                }
            }

            if (tier <= 1)
                return 0.25f;
            if (tier == 2)
                return 0.35f;
            return 0.45f;
        }
    }
}