using System.Collections.Generic;
using DG.Tweening;
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

        [Header("Combo Score (Notion Combo Points combo_score; index 0 = combo 1)")]
        [Tooltip("Score paid into the Combo Score line by the Nth merge of one drop. Merges past the last entry pay the last entry. Not changed by combo_progress_modifier.")]
        public List<int> comboScore = new List<int> { 200, 500, 2500, 8750, 15560 };

        [Header("Combo Text")]
        [Tooltip("Lowest combo count that shows the floating 'Combo N!' text (2 = no popup for combo 1). Points and bar are unaffected.")]
        public int minComboToShowText = 2;
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
        [Tooltip("Combo bar width in HUD canvas units (1080x1920 reference).")]
        public float barWidth = 360f;
        [Tooltip("Combo bar height in HUD canvas units.")]
        public float barHeight = 60f;
        [Tooltip("Bar top-left corner offset from the screen's top-left (x right, y down negative). Sits below the level label.")]
        public Vector2 barPosition = new Vector2(30f, -190f);
        [Tooltip("Font size of the 'Combo N/100' text on the bar.")]
        public float barLabelFontSize = 40f;
        [Tooltip("Combo points needed for one Magic Bomb.")]
        public int barCapacity = 100;
        [Tooltip("Scale the bar pulses to when it fills.")]
        public float barPulseScale = 1.15f;
        [Tooltip("Seconds for one grow (or shrink) of the fill pulse.")]
        public float barPulseDuration = 0.15f;
        [Tooltip("Grow + shrink cycles per fill.")]
        public int barPulseCount = 2;
        [Tooltip("Seconds for the bar fill to animate to a new value (each leg of an overflow). 0 = snap.")]
        public float barFillDuration = 0.5f;
        [Tooltip("Ease of the bar fill: OutQuart = fast at first, slowing near the target.")]
        public Ease barFillEase = Ease.OutQuart;

        [Header("Magic Bomb (placeholder look)")]
        [Tooltip("Bomb diameter in world units.")]
        public float bombDiameter = 0.5f;
        [Tooltip("Indicator icon diameter beside the launcher (world units).")]
        public float indicatorIconSize = 0.3f;

        [Header("Bomb Overlay")]
        [Tooltip("Placeholder explosion-area circle colour (alpha set below). Diameter = 2 x bomb_explode_range.")]
        public Color overlayColor = new Color(1f, 0.3f, 0.2f, 1f);
        [Tooltip("Overlay alpha during the countdown.")]
        [Range(0f, 1f)] public float overlayAlpha = 0.15f;
        [Tooltip("Overlay alpha at the moment of the explosion flash.")]
        [Range(0f, 1f)] public float overlayFlashAlpha = 0.5f;
        [Tooltip("Seconds for the flash to fade out.")]
        public float overlayFadeDuration = 0.4f;

        [Header("Ball Impact Explosion Overlay")]
        [Tooltip("Show a quick flash at the impact point sized to the ball tier's impact_aoe_range.")]
        public bool impactOverlayEnabled = true;
        [Tooltip("Impact explosion flash colour (alpha comes from Impact Overlay Alpha).")]
        public Color impactOverlayColor = new Color(1f, 0.55f, 0.1f, 1f);
        [Tooltip("Flash alpha at the moment of the impact explosion.")]
        [Range(0f, 1f)] public float impactOverlayAlpha = 0.45f;
        [Tooltip("Seconds for the impact flash to fade out.")]
        public float impactOverlayFadeDuration = 0.3f;

        public int BarCapacity => Mathf.Max(1, barCapacity);
        public float BarWidth => Mathf.Max(20f, barWidth);
        public float BarHeight => Mathf.Max(8f, barHeight);

        public int PointsFor(int comboCount)
        {
            if (comboPoints == null || comboPoints.Count == 0 || comboCount <= 0)
                return 0;
            return Mathf.Max(0, comboPoints[Mathf.Min(comboCount, comboPoints.Count) - 1]);
        }

        public int ScoreFor(int comboCount)
        {
            if (comboScore == null || comboScore.Count == 0 || comboCount <= 0)
                return 0;
            return Mathf.Max(0, comboScore[Mathf.Min(comboCount, comboScore.Count) - 1]);
        }

        public string TextFor(int comboCount)
        {
            string f = string.IsNullOrEmpty(comboTextFormat) ? "Combo {combo_count}!" : comboTextFormat;
            return f.Replace("{combo_count}", comboCount.ToString());
        }
    }
}
