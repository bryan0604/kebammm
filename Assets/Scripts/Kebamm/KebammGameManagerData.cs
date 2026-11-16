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

        [Header("Game Result - Stars per category (local tuning, not in Notion yet)")]
        [Tooltip("Monster star: fraction of the level's monsters destroyed for 1 star. 0.5 = 50%. No monsters in the level = free star.")]
        [Range(0f, 1f)] public float starMonsterThreshold = 0.5f;
        [Tooltip("Obstacle star: fraction of the level's obstacles destroyed for 1 star. No obstacles in the level = free star.")]
        [Range(0f, 1f)] public float starObstacleThreshold = 0.5f;
        [Tooltip("Trebuchet star: fraction of trebuchets destroyed for 1 star. No trebuchet built this attempt = free star.")]
        [Range(0f, 1f)] public float starTrebuchetThreshold = 0.5f;

        [Header("Game Result - Stars OLD (killed / total monsters; no longer used, kept until Notion is updated)")]
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
        [Header("Max Cap Line Blink (Notion Game Manager)")]
        [Tooltip("World units below the max cap line. If any in-play ball's top edge (position + radius) is this close or closer, the line blinks white and red (Notion max_cap_line_close_distance). <= 0 = 1.")]
        [Min(0f)] public float maxCapLineCloseDistance = 1f;
        [Tooltip("Seconds for one white-to-red or red-to-white colour flip. 0.25 = 4 flips per second (Notion max_cap_line_blink_half_period). <= 0 = 0.25.")]
        [Min(0.05f)] public float maxCapLineBlinkHalfPeriod = 0.25f;
        [Tooltip("When an in-play ball hits the Max Cap Line, the line blinks this many times faster (1.5 = 50% faster) until the bombing run starts (Notion max_cap_hit_blink_speed_multiplier). <= 0 = 1.5.")]
        [Min(0.1f)] public float maxCapHitBlinkSpeedMultiplier = 1.5f;
        [Tooltip("Seconds between a ball hitting the Max Cap Line and the bombing run starting; drops are blocked meanwhile (Notion max_cap_hit_delay_seconds). <= 0 = 1.5.")]
        [Min(0.1f)] public float maxCapHitDelaySeconds = 1.5f;
        [Header("Explosion Bits (Notion Game Manager)")]
        [Tooltip("Square bit size in world units. A destroyed host bursts into (width / size) x (height / size) bits (Notion explosion_bit_size). <= 0 = 0.2.")]
        [Min(0.02f)] public float explosionBitSize = 0.2f;
        [Tooltip("Max bits along each side of a destroyed host, so a big host or tiny bit size can't flood the screen (Notion explosion_bit_max_per_side). <= 0 = 12.")]
        [Min(1)] public int explosionBitMaxPerSide = 12;
        [Tooltip("Lowest upward launch speed of a bit, world units/s (Notion explosion_bit_up_force_min).")]
        [Min(0f)] public float explosionBitUpForceMin = 4f;
        [Tooltip("Highest upward launch speed of a bit, world units/s (Notion explosion_bit_up_force_max).")]
        [Min(0f)] public float explosionBitUpForceMax = 8f;
        [Tooltip("Max sideways launch speed, world units/s; bits on the left of the host lean left, right lean right (Notion explosion_bit_side_spread).")]
        [Min(0f)] public float explosionBitSideSpread = 3f;
        [Tooltip("Lowest gravity scale of a bit (x world gravity), so bits fall at different rates (Notion explosion_bit_gravity_min).")]
        [Min(0f)] public float explosionBitGravityMin = 1f;
        [Tooltip("Highest gravity scale of a bit (Notion explosion_bit_gravity_max).")]
        [Min(0f)] public float explosionBitGravityMax = 2f;
        [Tooltip("Seconds a bit lives; it fades out over the last third (Notion explosion_bit_lifetime). <= 0 = 1.2.")]
        [Min(0.1f)] public float explosionBitLifetime = 1.2f;
        [Tooltip("Max bits on screen at once; the oldest are recycled first (Notion explosion_bit_max_on_screen). 0 = no bits.")]
        [Min(0)] public int explosionBitMaxOnScreen = 150;
        [Header("Destroyed Fire VFX")]
        [Tooltip("Looping fire played on a destroyed obstacle or trebuchet (local asset reference, not in Notion). Empty = no fire.")]
        public GameObject destroyedFirePrefab;
        [Tooltip("Fire size = this x the destroyed host's larger side (Notion destroyed_fire_scale). <= 0 = 0.35.")]
        [Min(0f)] public float destroyedFireScale = 0.35f;
        [Tooltip("Flame lifetime multiplier; lower = shorter flames (Notion destroyed_fire_lifetime_scale). <= 0 = 0.5.")]
        [Min(0f)] public float destroyedFireLifetimeScale = 0.5f;
        [Tooltip("Playback speed of the destroyed fire; 0.5 = half speed (Notion destroyed_fire_speed_scale). <= 0 = 0.5.")]
        [Min(0f)] public float destroyedFireSpeedScale = 0.5f;
        [Tooltip("Fewest fire points on a destroyed obstacle or trebuchet (Notion destroyed_fire_points_min). 0 = no fire.")]
        [Min(0)] public int destroyedFirePointsMin = 1;
        [Tooltip("Most fire points on a destroyed obstacle or trebuchet; a random count between min and max is picked (Notion destroyed_fire_points_max).")]
        [Min(0)] public int destroyedFirePointsMax = 3;
        [Header("Goal Tracking Shine (Notion Game Manager)")]
        [Tooltip("Seconds for one light sweep up the player's bar (Notion goal_shine_duration). <= 0 = 0.6.")]
        [Min(0f)] public float goalShineDuration = 0.6f;
        [Tooltip("Pause in seconds between sweeps (Notion goal_shine_interval). < 0 = 2.")]
        [Min(0f)] public float goalShineInterval = 2f;
        [Tooltip("Brightness of the light strip, 0-1 (Notion goal_shine_alpha). 0 = no shine.")]
        [Range(0f, 1f)] public float goalShineAlpha = 0.6f;
        [Tooltip("Thickness of the light strip in UI pixels (Notion goal_shine_thickness). <= 0 = 36.")]
        [Min(0f)] public float goalShineThickness = 36f;
        [Header("Main Menu Title Shine (Notion VFX)")]
        [Tooltip("Seconds for one light sweep across the title (Notion VFX title_shine_duration). <= 0 = 0.8.")]
        [Min(0f)] public float titleShineDuration = 0.8f;
        [Tooltip("Pause in seconds between title sweeps (Notion VFX title_shine_interval). < 0 = 3.")]
        [Min(0f)] public float titleShineInterval = 3f;
        [Tooltip("Brightness of the title light strip, 0-1 (Notion VFX title_shine_alpha). 0 = no shine.")]
        [Range(0f, 1f)] public float titleShineAlpha = 0.7f;
        [Tooltip("Width of the title light strip in UI pixels (Notion VFX title_shine_thickness). <= 0 = 60.")]
        [Min(0f)] public float titleShineThickness = 60f;
        [Header("Magic Bomb Explosion VFX (Notion VFX)")]
        [Tooltip("Cartoon FX prefab played where the Magic Bomb explodes (CFXR Explosion 1). Empty = no explosion VFX.")]
        public GameObject magicBombFxPrefab;
        [Tooltip("Explosion VFX size = magic_bomb_explode_range x this. 0 = off.")]
        [Min(0f)] public float magicBombFxScale = 0.25f;

        [Header("Ball Impact Explosion VFX (Notion VFX)")]
        [Tooltip("Cartoon FX prefab played where a ball explodes on impact (ground, monster, obstacle, trebuchet). CFXR3 Fire Explosion B. Empty = off.")]
        public GameObject impactFxPrefab;
        [Tooltip("Impact explosion VFX size = the ball tier's impact_aoe_range x this. 0 = off.")]
        [Min(0f)] public float impactFxScale = 1f;
        [Tooltip("Pooled impact explosion instances reused round-robin. <=0 = 12.")]
        [Min(0)] public int impactFxPoolSize = 12;

        [Header("Trebuchet Construction Smoke VFX (Notion VFX)")]
        [Tooltip("Cartoon FX prefab played on the trebuchet when construction starts (CFXR2 Poison Cloud). Empty = old placeholder puff.")]
        public GameObject trebuchetSmokePrefab;
        [Tooltip("Cartoon FX prefab played on every trebuchet stage increase (CFXR3 Hit Misc F Smoke). Empty = uses the construction start prefab.")]
        public GameObject trebuchetStageSmokePrefab;
        [Tooltip("Construction smoke size = trebuchet scale x this. 0 = off (placeholder puff).")]
        [Min(0f)] public float trebuchetSmokeScale = 1f;

        [Header("Score Text Count-up (Notion VFX)")]
        [Tooltip("Gameplay HUD: seconds the Score number takes to roll up to the new value. 0 = jump straight.")]
        [Min(0f)] public float scoreHudCountDuration = 0.4f;
        [Tooltip("Gameplay HUD: scale punch on the Score text when points are gained (0.2 = +20%). 0 = no bump.")]
        [Min(0f)] public float scoreHudBump = 0.2f;
        [Tooltip("End screen: seconds Merge, Monster and Final Score take to roll up from 0. 0 = show straight away.")]
        [Min(0f)] public float scoreEndCountDuration = 0.6f;
        [Tooltip("End screen: scale punch on each score number when its roll finishes. 0 = no bump.")]
        [Min(0f)] public float scoreEndBump = 0.15f;

        [Header("Drop Burst VFX (Notion VFX)")]
        [Tooltip("Burst played where a dropped ball first touches with its bottom side (local asset reference, not in Notion). Empty = no burst.")]
        public GameObject dropBurstPrefab;
        [Tooltip("Burst size = this x the ball's diameter. <= 0 = 1.")]
        [Min(0f)] public float dropBurstScale = 1f;
        [Tooltip("How many bursts are pre-made and reused (most that can play at once). <= 0 = 6.")]
        [Min(0)] public int dropBurstPoolSize = 6;

        [Header("Firefly VFX (Notion VFX)")]
        [Tooltip("Ambient glow prefab placed along the monster platform only (local asset reference). Empty = no fireflies.")]
        public GameObject fireflyPrefab;
        [Tooltip("How many whole prefabs sit side by side along the platform, each over its own slice (no stacking). 0 = off.")]
        [Min(0)] public int fireflyCount = 2;
        [Tooltip("Height in world units above the platform top where the fireflies drift. <= 0 = 1.5.")]
        [Min(0f)] public float fireflyBandHeight = 1.5f;
        [Tooltip("Dots spawned per second by each prefab (pack default 50). <= 0 = 3.")]
        [Min(0f)] public float fireflyRate = 3f;
        [Tooltip("Multiplier on the pack's dot size. <= 0 = 1.")]
        [Min(0f)] public float fireflySizeScale = 1f;
        [Header("Combo Dots VFX (Notion VFX)")]
        [Tooltip("Glowing dots flying from a merge to the combo bar at combo 1. Both 0 = off (bar fills instantly as before).")]
        [Min(0)] public int comboDotCountMin = 4;
        [Tooltip("Dots at combo_glow_max_combo and above.")]
        [Min(0)] public int comboDotCountMax = 8;
        [Tooltip("Dot size in UI pixels. <= 0 = 26.")]
        [Min(0f)] public float comboDotSize = 26f;
        [Tooltip("How far (UI px) the dots burst out from the merge before flying.")]
        [Min(0f)] public float comboDotBurstRadius = 90f;
        [Tooltip("Seconds of the burst-out. <= 0 = 0.18.")]
        [Min(0f)] public float comboDotBurstTime = 0.18f;
        [Tooltip("Seconds each dot takes to curve into the bar. <= 0 = 0.45.")]
        [Min(0f)] public float comboDotFlightTime = 0.45f;
        [Tooltip("Delay in seconds between each dot leaving (0 = all together).")]
        [Min(0f)] public float comboDotStagger = 0.04f;
        [Tooltip("How much the flight path bends sideways (UI px). 0 = straight line.")]
        [Min(0f)] public float comboDotCurve = 120f;
        [Tooltip("Drop shadow darkness under each dot, 0-1 (0 = no shadow).")]
        [Range(0f, 1f)] public float comboDotShadowAlpha = 0.45f;
        [Tooltip("Drop shadow offset down-right in UI px (how 'lifted' the dot looks).")]
        [Min(0f)] public float comboDotShadowOffset = 8f;
        [Header("Combo Box Glow (Notion VFX)")]
        [Tooltip("Seconds the combo glow takes to fade out after each combo point gain. <= 0 = 0.45.")]
        [Min(0f)] public float comboGlowDuration = 0.45f;
        [Tooltip("Glow ring thickness around the combo box in UI pixels. <= 0 = 28.")]
        [Min(0f)] public float comboGlowSize = 28f;
        [Tooltip("Combo count at which the glow and bounce reach full strength (combo 1 = weakest).")]
        [Min(1)] public int comboGlowMaxCombo = 6;
        [Tooltip("Glow brightness at combo 1 (0-1).")]
        [Range(0f, 1f)] public float comboGlowAlphaMin = 0.55f;
        [Tooltip("Glow brightness at max combo (0-1).")]
        [Range(0f, 1f)] public float comboGlowAlphaMax = 1f;
        [Tooltip("Glow pop-out scale at combo 1.")]
        [Min(1f)] public float comboGlowPopMin = 1.05f;
        [Tooltip("Glow pop-out scale at max combo.")]
        [Min(1f)] public float comboGlowPopMax = 1.2f;
        [Tooltip("Box squash-and-stretch amount at combo 1 (0.08 = 8%). 0 = no bounce.")]
        [Min(0f)] public float comboBounceMin = 0.08f;
        [Tooltip("Box squash-and-stretch amount at max combo.")]
        [Min(0f)] public float comboBounceMax = 0.18f;
        [Header("Coin Reward (local only, not in Notion)")]
        [Tooltip("Coins per monster killed this attempt. Local only; not exported to Notion or the Gist.")]
        public int coinPerKill = 8;
        [Tooltip("Coins per star earned this attempt (0-3 stars).")]
        public int coinPerStar = 30;
        [Tooltip("Merge score is divided by this (1 coin per N merge points). 0 / negative = 100.")]
        public int mergePointsPerCoin = 100;
        [Tooltip("Max coins per level result (299 keeps it under 300).")]
        public int coinCap = 299;
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
        public float MaxCapLineCloseDistance => maxCapLineCloseDistance > 0f ? maxCapLineCloseDistance : 1f;
        public float MaxCapLineBlinkHalfPeriod => maxCapLineBlinkHalfPeriod > 0f ? maxCapLineBlinkHalfPeriod : 0.25f;
        public float MaxCapHitBlinkSpeedMultiplier => maxCapHitBlinkSpeedMultiplier > 0f ? maxCapHitBlinkSpeedMultiplier : 1.5f;
        public float MaxCapHitDelaySeconds => maxCapHitDelaySeconds > 0f ? maxCapHitDelaySeconds : 1.5f;
        public float ExplosionBitSize => explosionBitSize > 0f ? Mathf.Max(0.02f, explosionBitSize) : 0.2f;
        public int ExplosionBitMaxPerSide => explosionBitMaxPerSide > 0 ? explosionBitMaxPerSide : 12;
        public float ExplosionBitUpForceMin => Mathf.Max(0f, Mathf.Min(explosionBitUpForceMin, explosionBitUpForceMax));
        public float ExplosionBitUpForceMax => Mathf.Max(0f, Mathf.Max(explosionBitUpForceMin, explosionBitUpForceMax));
        public float ExplosionBitSideSpread => Mathf.Max(0f, explosionBitSideSpread);
        public float ExplosionBitGravityMin => Mathf.Max(0f, Mathf.Min(explosionBitGravityMin, explosionBitGravityMax));
        public float ExplosionBitGravityMax => Mathf.Max(0f, Mathf.Max(explosionBitGravityMin, explosionBitGravityMax));
        public float ExplosionBitLifetime => explosionBitLifetime > 0f ? Mathf.Max(0.1f, explosionBitLifetime) : 1.2f;
        public int ExplosionBitMaxOnScreen => Mathf.Max(0, explosionBitMaxOnScreen);
        public float ComboGlowDuration => comboGlowDuration > 0f ? comboGlowDuration : 0.45f;
        public float ComboGlowSize => comboGlowSize > 0f ? comboGlowSize : 28f;
        public int ComboGlowMaxCombo => Mathf.Max(1, comboGlowMaxCombo);
        public float TitleShineDuration => titleShineDuration > 0f ? Mathf.Max(0.05f, titleShineDuration) : 0.8f;
        public float TitleShineInterval => titleShineInterval >= 0f ? titleShineInterval : 3f;
        public float TitleShineAlpha => Mathf.Clamp01(titleShineAlpha);
        public float TitleShineThickness => titleShineThickness > 0f ? titleShineThickness : 60f;
        public float GoalShineDuration => goalShineDuration > 0f ? Mathf.Max(0.05f, goalShineDuration) : 0.6f;
        public float GoalShineInterval => goalShineInterval >= 0f ? goalShineInterval : 2f;
        public float GoalShineAlpha => Mathf.Clamp01(goalShineAlpha);
        public float GoalShineThickness => goalShineThickness > 0f ? goalShineThickness : 36f;
        public float DestroyedFireSpeedScale => destroyedFireSpeedScale > 0f ? destroyedFireSpeedScale : 0.5f;
        public float DestroyedFireLifetimeScale => destroyedFireLifetimeScale > 0f ? destroyedFireLifetimeScale : 0.5f;
        public int DestroyedFirePointsMin => Mathf.Max(0, Mathf.Min(destroyedFirePointsMin, destroyedFirePointsMax));
        public int DestroyedFirePointsMax => Mathf.Max(0, Mathf.Max(destroyedFirePointsMin, destroyedFirePointsMax));
        public float MagicBombFxScale => Mathf.Max(0f, magicBombFxScale);
        public float ImpactFxScale => Mathf.Max(0f, impactFxScale);
        public int ImpactFxPoolSize => impactFxPoolSize > 0 ? impactFxPoolSize : 12;
        public float TrebuchetSmokeScale => Mathf.Max(0f, trebuchetSmokeScale);
        public float ScoreHudCountDuration => Mathf.Max(0f, scoreHudCountDuration);
        public float ScoreHudBump => Mathf.Max(0f, scoreHudBump);
        public float ScoreEndCountDuration => Mathf.Max(0f, scoreEndCountDuration);
        public float ScoreEndBump => Mathf.Max(0f, scoreEndBump);
        public float DropBurstScale => dropBurstScale > 0f ? dropBurstScale : 1f;
        public int DropBurstPoolSize => dropBurstPoolSize > 0 ? dropBurstPoolSize : 6;
        public int FireflyCount => Mathf.Max(0, fireflyCount);
        public float FireflyBandHeight => fireflyBandHeight > 0f ? fireflyBandHeight : 1.5f;
        public float FireflyRate => fireflyRate > 0f ? fireflyRate : 3f;
        public float FireflySizeScale => fireflySizeScale > 0f ? fireflySizeScale : 1f;
        public float ComboDotSize => comboDotSize > 0f ? comboDotSize : 26f;
        public float ComboDotBurstTime => comboDotBurstTime > 0f ? comboDotBurstTime : 0.18f;
        public float ComboDotFlightTime => comboDotFlightTime > 0f ? comboDotFlightTime : 0.45f;
        /// <summary>Dots for this combo count: min at combo 1 up to max at combo_glow_max_combo.</summary>
        public int ComboDotCountFor(int combo)
        {
            int max = ComboGlowMaxCombo;
            float s = max > 1 ? Mathf.Clamp01((combo - 1f) / (max - 1f)) : 1f;
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(comboDotCountMin, comboDotCountMax, s)));
        }
        public float DestroyedFireScale => destroyedFireScale > 0f ? destroyedFireScale : 0.35f;
        public float Star1Threshold => Mathf.Clamp01(star1Threshold);
        public float Star2Threshold => Mathf.Max(Star1Threshold, Mathf.Clamp01(star2Threshold));
        public float Star3Threshold => Mathf.Max(Star2Threshold, Mathf.Clamp01(star3Threshold));

        /// <summary>
        /// GDD Game Result - Stars: killed / total >= threshold (no rounding). 0 monsters in the level = 0 stars
        /// (nothing to kill; logged as a warning by the caller).
        /// </summary>
        public float StarMonsterThreshold => Mathf.Clamp01(starMonsterThreshold);
        public float StarObstacleThreshold => Mathf.Clamp01(starObstacleThreshold);
        public float StarTrebuchetThreshold => Mathf.Clamp01(starTrebuchetThreshold);

        /// <summary>One category star: 1 when destroyed / total >= threshold (no rounding); 1 for free when the level has none of it.</summary>
        public static int CategoryStar(int destroyed, int total, float threshold)
        {
            if (total <= 0)
                return 1;
            return (float)destroyed / total >= threshold ? 1 : 0;
        }

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

        public int CoinPerKill => Mathf.Max(0, coinPerKill);
        public int CoinPerStar => Mathf.Max(0, coinPerStar);
        public int MergePointsPerCoin => mergePointsPerCoin > 0 ? mergePointsPerCoin : 100;
        public int CoinCap => Mathf.Max(0, coinCap);

        /// <summary>
        /// GDD / Notion 'Game Reward - Tabulation':
        /// coins = min(cap, floor(kills x perKill + stars x perStar + merge / mergePointsPerCoin)), clamped to [0, cap].
        /// </summary>
        public int CoinsFor(int kills, int stars, int mergeScore)
        {
            double raw = (double)Mathf.Max(0, kills) * CoinPerKill + (double)Mathf.Clamp(stars, 0, 3) * CoinPerStar
                         + (double)Mathf.Max(0, mergeScore) / MergePointsPerCoin;
            return (int)System.Math.Min(CoinCap, System.Math.Max(0.0, System.Math.Floor(raw)));
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