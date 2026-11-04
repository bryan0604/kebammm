using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Combo + Magic Bomb display settings (GDD Combo Gameplay Design) and the local Combo Points table.
    /// Asset: Resources/Kebamm/combo_config. Combo points are overridden by the Notion Combo Points database
    /// (Gist table comboPointsData); bomb gameplay values live in Game Manager (bomb_* keys).
    /// </summary>
    [CreateAssetMenu(fileName = "combo_config", menuName = "Kebamm/Combo Config")]
    public class KebammComboConfig : ScriptableObject
    {
        [Header("Combo Points (Notion Combo Points; index 0 = combo 1)")]
        [Tooltip("Points paid by the Nth merge of one drop. Merges past the last entry pay the last entry (combo 5 = max).")]
        public List<int> comboPoints = new List<int> { 20, 40, 60, 80, 100 };

        [Header("Combo Text")]
        [Tooltip("Float text. {combo_count} is replaced by the merge's combo count.")]
        public string comboTextFormat = "Combo {combo_count}!";
        public float comboTextFontSize = 36f;
        [Tooltip("World scale of the text after the pop.")]
        public float comboTextScale = 0.09f;
        [Tooltip("Pop overshoot multiplier on spawn (DOTween).")]
        public float comboTextPopScale = 1.5f;
        [Tooltip("Rise speed in world units per second.")]
        public float comboTextSpeed = 1.2f;
        [Tooltip("Seconds the text stays before it is gone.")]
        public float comboTextDuration = 0.9f;
        [Tooltip("Height above the merge point where the text spawns (world units).")]
        public float comboTextYOffset = 0.5f;
        public Color comboTextColor = new Color(1f, 0.85f, 0.2f, 1f);

        [Header("Combo Bar")]
        [Tooltip("Combo points needed for one Magic Bomb.")]
        public int barCapacity = 100;
        [Tooltip("Scale the bar pulses to when it fills.")]
        public float barPulseScale = 1.15f;
        [Tooltip("Seconds for one grow (or shrink) of the fill pulse.")]
        public float barPulseDuration = 0.15f;
        [Tooltip("Grow + shrink cycles per fill.")]
        public int barPulseCount = 2;

        [Header("Magic Bomb (placeholder look)")]
        [Tooltip("Bomb diameter in world units.")]
        public float bombDiameter = 0.5f;
        [Tooltip("Indicator icon diameter beside the launcher (world units).")]
        public float indicatorIconSize = 0.3f;

        public int BarCapacity => Mathf.Max(1, barCapacity);

        public int PointsFor(int comboCount)
        {
            if (comboPoints == null || comboPoints.Count == 0 || comboCount <= 0)
                return 0;
            return Mathf.Max(0, comboPoints[Mathf.Min(comboCount, comboPoints.Count) - 1]);
        }

        public string TextFor(int comboCount)
        {
            string f = string.IsNullOrEmpty(comboTextFormat) ? "Combo {combo_count}!" : comboTextFormat;
            return f.Replace("{combo_count}", comboCount.ToString());
        }
    }
}
