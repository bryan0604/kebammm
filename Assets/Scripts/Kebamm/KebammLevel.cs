using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Kebamm
{
    [CreateAssetMenu(fileName = "Level", menuName = "Kebamm/Level")]
    public class KebammLevel : ScriptableObject
    {
        public const int MinMonsterTier = 1;
        public const int MaxMonsterTier = 5;

        [Header("Level Information")]
        public int levelNumber = 1;
        public int monsterQty = 3;

        [Header("Prepopulate")]
        public bool levelPrepopulate = true;
        // Unused: monster count comes from monsterQty only. Kept so existing assets still load.
        [Tooltip("Unused - monster count comes from Monster Qty.")] public int prepopulateMonsterNumber = 3;
        public List<KebammBallColour> prepopulateBallColours = new List<KebammBallColour>
        {
            KebammBallColour.Red,
            KebammBallColour.Blue
        };
        [Tooltip("Lowest monster data tier spawned in this level.")][FormerlySerializedAs("prepopulateBallTierMin")][Range(MinMonsterTier, MaxMonsterTier)] public int prepopulateMonsterTierMin = 1;
        [Tooltip("Highest monster data tier spawned in this level.")][FormerlySerializedAs("prepopulateBallTierMax")][Range(MinMonsterTier, MaxMonsterTier)] public int prepopulateMonsterTierMax = 2;
        public int prepopulateObstacleQty;
        [Tooltip("x = min, y = max")] public Vector2 prepopulateMonsterPositionRangeX = new Vector2(-2f, 2f);
        [Tooltip("x = min, y = max")] public Vector2 prepopulateMonsterPositionRangeY = new Vector2(0f, 1f);
        [Tooltip("x = min, y = max")] public Vector2 prepopulateObstaclePositionRangeX = new Vector2(-2f, 2f);
        [Tooltip("x = min, y = max")] public Vector2 prepopulateObstaclePositionRangeY = new Vector2(0f, 1f);
        [Tooltip("Chance (0-1) each prepopulated monster after the first stacks on a random existing stack.")][Range(0f, 1f)] public float monsterStackChance;
        [Tooltip("Multiplier on every monster's tier HP in this level (Notion Levels column monster_hp_modifier). 500 hp x 1.5 = 750 hp. Rounded, min 1 hp.")][Min(0f)] public float monsterHpModifier = 1f;
        /// <summary>monsterHpModifier, or 1 when unset / not positive.</summary>
        public float MonsterHpModifier => monsterHpModifier > 0f ? monsterHpModifier : 1f;
        /// <summary>Final monster HP for this level: base tier HP x modifier, rounded to the nearest int, minimum 1.</summary>
        public float ModifiedMonsterHp(float baseHp) => Mathf.Max(1f, Mathf.Round(baseHp * MonsterHpModifier));

        [Header("Level Type & Rules")]
        [Tooltip("Label only (Notion Levels column Type Level, exported as type_level): Normal / Hard / Fun / King / Bomb. No gameplay effect.")] public string typeLevel = "";
        [Tooltip("Bomb level rule (Notion Levels column bomb_every_moves): every N normal drops queue 1 Magic Bomb. 0 = off. Magic Bomb drops never count.")][Min(0)] public int bombEveryMoves;
        /// <summary>bombEveryMoves, or 0 (off) when not positive.</summary>
        public int BombEveryMoves => bombEveryMoves > 0 ? bombEveryMoves : 0;
        [Tooltip("Multiplier on combo points that fill the bar (Notion Levels column combo_progress_modifier). e.g. base 20 x 1.5 = 30. Does not change score or Combo N! text. Blank/0 = 1.")]
        [Min(0f)] public float comboProgressModifier = 1f;
        /// <summary>comboProgressModifier, or 1 when unset / not positive (same pattern as MonsterHpModifier).</summary>
        public float ComboProgressModifier => comboProgressModifier > 0f ? comboProgressModifier : 1f;

        [Header("Stars - per category goal (Notion Levels star_*_threshold)")]
        [Tooltip("Monster star: fraction of this level's monsters destroyed for 1 star (0.5 = 50%). Negative / blank = Game Manager default.")]
        public float starMonsterThreshold = -1f;
        [Tooltip("Obstacle star: fraction of this level's obstacles destroyed for 1 star. Negative / blank = Game Manager default.")]
        public float starObstacleThreshold = -1f;
        [Tooltip("Trebuchet star: fraction of trebuchets destroyed for 1 star. Negative / blank = Game Manager default.")]
        public float starTrebuchetThreshold = -1f;

        // Trebuchet (GDD [Level] Monster Group Design). Design letters: Y, X, Z, W, T.
        public const int DefaultTrebuchetStartDrops = 15;  // Y
        public const int DefaultTrebuchetStageDrops = 5;   // X
        public const int DefaultTrebuchetStageGain = 1;    // Z
        public const int DefaultTrebuchetLaunchDrops = 5;  // W
        public const int DefaultRockDestroyCount = 2;      // T
        public const float DefaultRockSize = 0.6f;         // rock diameter (world units); matches siege_data rockScale
        public const int DefaultTrebuchetMinMonsters = 5;  // min monsters to start construction
        [Header("Trebuchet")]
        [Tooltip("Notion Levels column trebuchet_enabled (On / Off). Off = no trebuchet in this level at all (nothing built or shown, no messages, no rocks, no toss lock). Blank = On.")]
        public bool trebuchetEnabled = true;
        [Tooltip("Y (Notion Levels column trebuchet_start_drops): player ball drops before monsters start building (also needs trebuchet_min_monsters, default 5). 0 = start immediately. Blank/negative = 15.")]
        public int trebuchetStartDrops = DefaultTrebuchetStartDrops;
        [Tooltip("X (trebuchet_stage_drops): every X more drops adds Z build stages (5 stages = complete). 0 or less = 5.")]
        public int trebuchetStageDrops = DefaultTrebuchetStageDrops;
        [Tooltip("Z (trebuchet_stage_gain): build stages added every X drops. 0 or less = 1.")]
        public int trebuchetStageGain = DefaultTrebuchetStageGain;
        [Tooltip("W (trebuchet_launch_drops): once complete, a rock is launched every W drops (max 3 rocks in play). 0 or less = 5.")]
        public int trebuchetLaunchDrops = DefaultTrebuchetLaunchDrops;
        [Tooltip("T (rock_destroy_count): a landed rock destroys the first T balls it touches, then only blocks. 0 = destroys none. Blank/negative = 2.")]
        public int rockDestroyCount = DefaultRockDestroyCount;
        [Tooltip("Rock diameter in world units (Notion Levels column rock_size). Blank / 0 / negative = siege_data rock scale (0.6).")]
        public float rockSize = DefaultRockSize;
        /// <summary>rock_size for this level, or the siege_data rock scale when not positive.</summary>
        public float RockSizeOr(float fallback) => rockSize > 0f ? rockSize : fallback;
        [Tooltip("Minimum monsters required in the level before construction can start after Y drops (Notion Levels column trebuchet_min_monsters). Blank / 0 / negative = 5 (existing MinMonstersToBuild fallback).")]
        public int trebuchetMinMonsters = DefaultTrebuchetMinMonsters;
        /// <summary>trebuchet_min_monsters for this level, or the fallback (KebammTrebuchet.MinMonstersToBuild, 5) when not positive.</summary>
        public int TrebuchetMinMonstersOr(int fallback) => trebuchetMinMonsters > 0 ? trebuchetMinMonsters : fallback;
        public int TrebuchetStartDrops => trebuchetStartDrops >= 0 ? trebuchetStartDrops : DefaultTrebuchetStartDrops;
        public int TrebuchetStageDrops => trebuchetStageDrops > 0 ? trebuchetStageDrops : DefaultTrebuchetStageDrops;
        public int TrebuchetStageGain => trebuchetStageGain > 0 ? trebuchetStageGain : DefaultTrebuchetStageGain;
        public int TrebuchetLaunchDrops => trebuchetLaunchDrops > 0 ? trebuchetLaunchDrops : DefaultTrebuchetLaunchDrops;
        public int RockDestroyCount => rockDestroyCount >= 0 ? rockDestroyCount : DefaultRockDestroyCount;

        [Header("Obstacles")]
        [Tooltip("Populate building obstacles at level start. Uses Prepopulate Obstacle Qty and Obstacle Position Range X.")] public bool obstaclePopulate;
        [Tooltip("Chance (0-1) each building after the first stacks on a random existing building stack (max 3 per stack).")][Range(0f, 1f)] public float obstacleStackChance;
        [Tooltip("Shapes this level may spawn. Empty = all shapes.")] public List<KebammObstacleShape> allowedObstacleShapes = new List<KebammObstacleShape>();

        // Dropped ball tier (GDD / Ball Tier Data tiers 1-5; code play tier = GDD tier - 1). 0 = unset.
        // If min or max is unset, only the smallest ball (tier 1) is dropped.
        public const int MinBallTier = 1;
        public const int MaxBallTier = 5;
        [Header("Ball Tier Spawning")]
        [Tooltip("Lowest ball tier (1-5) the launcher can load. 0 = unset.")][Range(0, MaxBallTier)] public int ballTierMin;
        [Tooltip("Highest ball tier (1-5) the launcher can load. 0 = unset.")][Range(0, MaxBallTier)] public int ballTierMax;
        [Tooltip("Relative chance for tier 1. 0 = excluded. Only tiers inside min-max are rolled.")][Min(0f)] public float ballTier1Weight;
        [Tooltip("Relative chance for tier 2. 0 = excluded. Only tiers inside min-max are rolled.")][Min(0f)] public float ballTier2Weight;
        [Tooltip("Relative chance for tier 3. 0 = excluded. Only tiers inside min-max are rolled.")][Min(0f)] public float ballTier3Weight;
        [Tooltip("Relative chance for tier 4. 0 = excluded. Only tiers inside min-max are rolled.")][Min(0f)] public float ballTier4Weight;
        [Tooltip("Relative chance for tier 5. 0 = excluded. Only tiers inside min-max are rolled.")][Min(0f)] public float ballTier5Weight;

        public float BallTierWeight(int gddTier)
        {
            switch (gddTier)
            {
                case 1: return ballTier1Weight;
                case 2: return ballTier2Weight;
                case 3: return ballTier3Weight;
                case 4: return ballTier4Weight;
                case 5: return ballTier5Weight;
                default: return 0f;
            }
        }

        /// <summary>
        /// Weighted roll of the next dropped ball, returned as a code play tier (GDD tier - 1).
        /// Unset min/max -> tier 1 only. Tiers outside min-max or with weight 0 are skipped;
        /// if every weight in range is 0, tiers in range are equally likely.
        /// </summary>
        public int RollDropPlayTier()
        {
            if (ballTierMin <= 0 || ballTierMax <= 0)
                return 0;
            int lo = Mathf.Clamp(Mathf.Min(ballTierMin, ballTierMax), MinBallTier, MaxBallTier);
            int hi = Mathf.Clamp(Mathf.Max(ballTierMin, ballTierMax), MinBallTier, MaxBallTier);
            float total = 0f;
            for (int t = lo; t <= hi; t++)
                total += Mathf.Max(0f, BallTierWeight(t));
            if (total <= 0f)
                return Random.Range(lo, hi + 1) - 1;
            float r = Random.value * total;
            for (int t = lo; t <= hi; t++)
            {
                float w = Mathf.Max(0f, BallTierWeight(t));
                if (w <= 0f)
                    continue;
                if (r < w)
                    return t - 1;
                r -= w;
            }
            for (int t = hi; t >= lo; t--)
                if (BallTierWeight(t) > 0f)
                    return t - 1;
            return lo - 1;
        }

        void OnValidate()
        {
            if (levelNumber < 1)
                levelNumber = 1;
            if (monsterQty < 0)
                monsterQty = 0;
            if (prepopulateMonsterNumber < 0)
                prepopulateMonsterNumber = 0;
            if (prepopulateObstacleQty < 0)
                prepopulateObstacleQty = 0;

            prepopulateMonsterTierMin = Mathf.Clamp(prepopulateMonsterTierMin, MinMonsterTier, MaxMonsterTier);
            prepopulateMonsterTierMax = Mathf.Clamp(prepopulateMonsterTierMax, MinMonsterTier, MaxMonsterTier);
            if (prepopulateMonsterTierMax < prepopulateMonsterTierMin)
                prepopulateMonsterTierMax = prepopulateMonsterTierMin;

            ClampRange(ref prepopulateMonsterPositionRangeX);
            ClampRange(ref prepopulateMonsterPositionRangeY);
            ClampRange(ref prepopulateObstaclePositionRangeX);
            ClampRange(ref prepopulateObstaclePositionRangeY);
        }

        static void ClampRange(ref Vector2 range)
        {
            if (range.y < range.x)
                range.y = range.x;
        }
    }
}