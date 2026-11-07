using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Kebamm
{
    /// <summary>
    /// Game Manager data (Resources/Kebamm/GameManager; GDD "Game Manager Controller"). Core layout in world units.
    /// Applied live: edits in the Inspector during Play rebuild the container layout on the next frame.
    /// Monster sizes live in Monster Tier Data (KebammMonsterData.size).
    /// </summary>
    [CreateAssetMenu(fileName = "GameManager", menuName = "Kebamm/Game Manager Data")]
    public class KebammGameManagerData : ScriptableObject
    {
        public const float MinPillarHeight = 1f;
        public const float MinPillarGapWidth = 1f;
        public const float MinCapsLineHeight = 0.5f;

        [Tooltip("Pillar (container wall) height in world units: inner height from the container floor top. Wall collider = this + wall thickness; pillar art stretches to match.")]
        public float pillarHeight = 5.8f;
        [Tooltip("Width between the two pillars in world units: inner gap (inside edge to inside edge). Launcher clamp and cap line follow it.")]
        public float pillarGapWidth = 5.0f;
        [Tooltip("Max caps (red) line height in world units, measured up from the container floor top.")]
        public float capsLineHeight = 4.95f;
        [Tooltip("Distance in world units from the platform (ground) top up to the jar's inner floor top. The jar moves; the ground stays fixed. Applied live.")]
        public float jarToPlatformDistance = 3.525f;

        [Header("Launcher")]
        [Tooltip("How quickly the held ball eases toward the dragged position (1/s). Higher = snappier, 0 = no easing. Applied live.")]
        public float launcherFollowSharpness = 0f;

        [Header("Monster Recovery (stand back up)")]
        [Tooltip("A living monster on the ground tilted more than this many degrees from world up needs to recover.")]
        public float monsterRecoverTiltDegrees = 30f;
        [Tooltip("Seconds after the last ball hit before a tilted monster jumps back upright. Each new hit restarts it.")]
        public float monsterRecoverDelay = 5f;
        [Tooltip("Recovery jump height in world units.")]
        public float monsterRecoverJumpHeight = 0.8f;
        [Tooltip("Seconds the recovery jump takes to rotate the monster upright (around Z).")]
        public float monsterRecoverJumpDuration = 0.5f;
        [Tooltip("Max sideways distance (world units) the recovery hop moves back, opposite to the last knock-back.")]
        public float monsterRecoverHopDistance = 0.6f;
        [Header("Monster Return To Spawn X")]
        [Tooltip("Seconds after the last hit (and once sideways speed has settled) before a living monster slides back to its spawn X. Local only (not in Notion yet).")]
        public float monsterReturnDelay = 0.3f;
        [Tooltip("Seconds the slide back to spawn X takes (ease-out: fast at first, slowing at the end).")]
        public float monsterReturnDuration = 0.6f;
        [Tooltip("Sideways speed (units/s) below which the push counts as settled and the return may start.")]
        public float monsterReturnSettleSpeed = 0.5f;

        [Header("Main Menu Title")]
        [Tooltip("Seconds for one half (up or down) of the title letters' repeated breathing bob. Applied each time the main menu is shown.")]
        public float titleBreathDuration = 1.1f;
        [Tooltip("Title letter image height in reference pixels (1080x1920 canvas); the title still shrinks to fit 980 px wide. Applied when the menu is built (app launch).")]
        public float titleLetterHeight = 200f;
        [Tooltip("Title letters loop-grow to this scale multiplier and back to 1 (Notion title_pulse_scale).")]
        public float titlePulseScale = 1.1f;
        [Tooltip("Seconds for one grow or one shrink of the title pulse (Notion title_pulse_duration).")]
        public float titlePulseDuration = 0.35f;

        [Tooltip("Seconds 3+ fingers must be held in the top area to toggle the debug console (Notion debug_console_hold_seconds).")]
        public float debugConsoleHoldSeconds = 5f;

        [Tooltip("Seconds of rest between title pulses (Notion title_pulse_interval). 0 = continuous.")]
        public float titlePulseInterval = 5f;

        [Header("Game Result - Stars (GDD: killed / total monsters)")]
        [Tooltip("Fraction of the level's monsters killed for 1 star (Notion star_1_threshold). 0.20 = 20%.")]
        [Range(0f, 1f)] public float star1Threshold = 0.20f;
        [Tooltip("Fraction of the level's monsters killed for 2 stars (Notion star_2_threshold). Never below 1 star.")]
        [Range(0f, 1f)] public float star2Threshold = 0.60f;
        [Tooltip("Fraction of the level's monsters killed for 3 stars (Notion star_3_threshold). Never below 2 stars.")]
        [Range(0f, 1f)] public float star3Threshold = 0.85f;
        [Header("Magic Bomb (GDD Combo Gameplay Design)")]
        [Tooltip("Seconds from the bomb's first touch in the jar (wall, base or ball) to the explosion (Notion magic_bomb_touch_delay).")]
        [FormerlySerializedAs("bombTouchDelay")]
        public float magicBombTouchDelay = 1.2f;
        [Tooltip("Explosion radius in world units (Notion magic_bomb_explode_range).")]
        [FormerlySerializedAs("bombExplodeRange")]
        public float magicBombExplodeRange = 8f;
        [Tooltip("Damage to every ball in range: ball health - damage, 0 or less = destroyed (Notion magic_bomb_explode_damage).")]
        [FormerlySerializedAs("bombExplodeDamage")]
        public int magicBombExplodeDamage = 10;
        [Tooltip("Impulse pushing surviving balls away from the bomb (Notion magic_bomb_explode_strength).")]
        [FormerlySerializedAs("bombExplodeStrength")]
        public int magicBombExplodeStrength = 3;
        [Tooltip("Magic Bomb diameter in world units: visual and collider, held and dropped (Notion magic_bomb_size). <= 0 = combo_config bombDiameter (0.5).")]
        [FormerlySerializedAs("bombSize")]
        public float magicBombSize = 0.5f;
        [Header("Jar Panel (local only, not in Notion)")]
        [Tooltip("Colour + alpha of the see-through panel filling the jar play area, drawn above the level background and behind balls, bombs, pillars and base.")]
        public Color jarPanelColor = new Color(1f, 1f, 1f, 0.1f);
        [Tooltip("Extra size around the jar inner area (world units, each side). 0 = meets the pillars' and base's visible edges (tucked under them). Negative shrinks.")]
        public float jarPanelPadding = 0f;
        [Header("Drop Indication (local only, not in Notion)")]
        [Tooltip("White dotted aim guide opacity in percent (0 hides it). Local only; not exported to Notion or the Gist.")]
        [Range(0f, 100f)] public float dropIndicationOpacity = 30f;
        [Tooltip("Dotted aim guide thickness in world units. Local only; 0 hides it. Applied live.")]
        [Min(0f)] public float dropIndicationThickness = 0.025f;
        [Header("Remote Data")]
        [Tooltip("URL of kebamm_data.json (raw Gist link). Empty = no online fetch; cached or built-in data is used.")]
        public string remoteDataUrl = "";
        [Tooltip("Seconds before the online fetch gives up and falls back to cached / built-in data.")]
        public float remoteDataTimeoutSeconds = 5f;

        /// <summary>Raised when values change (Inspector edit) so the game can re-layout live.</summary>
        public static event Action<KebammGameManagerData> Changed;

        public float PillarHeight => Mathf.Max(MinPillarHeight, pillarHeight);
        public float PillarGapWidth => Mathf.Max(MinPillarGapWidth, pillarGapWidth);
        public float CapsLineHeight => Mathf.Max(MinCapsLineHeight, capsLineHeight);
        public float JarToPlatformDistance => Mathf.Max(0.5f, jarToPlatformDistance);
        public float LauncherFollowSharpness => Mathf.Max(0f, launcherFollowSharpness);
        public float MonsterRecoverTiltDegrees => Mathf.Clamp(monsterRecoverTiltDegrees, 1f, 179f);
        public float MonsterRecoverDelay => Mathf.Max(0f, monsterRecoverDelay);
        public float MonsterRecoverJumpHeight => Mathf.Max(0f, monsterRecoverJumpHeight);
        public float MonsterRecoverJumpDuration => Mathf.Max(0.05f, monsterRecoverJumpDuration);
        public float MonsterRecoverHopDistance => Mathf.Max(0f, monsterRecoverHopDistance);
        public float MonsterReturnDelay => Mathf.Max(0f, monsterReturnDelay);
        public float MonsterReturnDuration => Mathf.Max(0.05f, monsterReturnDuration);
        public float MonsterReturnSettleSpeed => Mathf.Max(0.01f, monsterReturnSettleSpeed);
        public float TitleBreathDuration => Mathf.Max(0.1f, titleBreathDuration);
        public float TitleLetterHeight => Mathf.Max(10f, titleLetterHeight);
        public float TitlePulseScale => Mathf.Max(0.1f, titlePulseScale);
        public float TitlePulseDuration => Mathf.Max(0.05f, titlePulseDuration);
        public float TitlePulseInterval => Mathf.Max(0f, titlePulseInterval);
        public float MagicBombTouchDelay => Mathf.Max(0f, magicBombTouchDelay);
        public float MagicBombExplodeRange => Mathf.Max(0f, magicBombExplodeRange);
        public int MagicBombExplodeDamage => Mathf.Max(0, magicBombExplodeDamage);
        public int MagicBombExplodeStrength => Mathf.Max(0, magicBombExplodeStrength);
        /// <summary>magicBombSize, or 0 when unset (caller falls back to combo_config bombDiameter).</summary>
        public float MagicBombSize => magicBombSize > 0f ? magicBombSize : 0f;
        public float Star1Threshold => Mathf.Clamp01(star1Threshold);
        public float Star2Threshold => Mathf.Max(Star1Threshold, Mathf.Clamp01(star2Threshold));
        public float Star3Threshold => Mathf.Max(Star2Threshold, Mathf.Clamp01(star3Threshold));

        /// <summary>
        /// GDD Game Result - Stars: killed / total >= threshold (no rounding). 0 monsters in the level = 0 stars
        /// (nothing to kill; logged as a warning by the caller).
        /// </summary>
        public int StarsFor(int killed, int total)
        {
            if (total <= 0)
                return 0;
            float ratio = (float)killed / total;
            if (ratio >= Star3Threshold) return 3;
            if (ratio >= Star2Threshold) return 2;
            if (ratio >= Star1Threshold) return 1;
            return 0;
        }

        public float DebugConsoleHoldSeconds => debugConsoleHoldSeconds > 0f ? debugConsoleHoldSeconds : 5f;

        void OnValidate()
        {
            pillarHeight = Mathf.Max(MinPillarHeight, pillarHeight);
            pillarGapWidth = Mathf.Max(MinPillarGapWidth, pillarGapWidth);
            capsLineHeight = Mathf.Max(MinCapsLineHeight, capsLineHeight);
            Changed?.Invoke(this);
        }
    }
}