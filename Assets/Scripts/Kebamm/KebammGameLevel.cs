using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    [Serializable]
    public class KebammGameLevelEntry
    {
        public bool active = true;
        public KebammLevel level;
    }

    [CreateAssetMenu(fileName = "GameLevel", menuName = "Kebamm/Game Level")]
    public class KebammGameLevel : ScriptableObject
    {
        public List<KebammGameLevelEntry> levels = new List<KebammGameLevelEntry>();

        public List<KebammLevel> GetActiveLevels()
        {
            var result = new List<KebammLevel>();
            if (levels == null)
                return result;

            for (int i = 0; i < levels.Count; i++)
            {
                KebammGameLevelEntry entry = levels[i];
                if (entry != null && entry.active && entry.level != null)
                    result.Add(entry.level);
            }
            return result;
        }
    }
}