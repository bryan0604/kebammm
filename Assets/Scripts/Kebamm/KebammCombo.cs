using System;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Combo state for the current level attempt (GDD Combo Gameplay Design).
    /// A drop of a normal ball starts a new combo window (count back to 0); every merge until the next drop
    /// counts, including merges still caused by earlier balls. Each merge pays its own step (combo 1 = 20,
    /// combo 2 = 40 ... combo 5+ = combo 5 value). Bar overflow carries over; each fill queues one Magic Bomb.
    /// Reset on every new level attempt (KebammPrototypeBootstrap.ResetSession).
    /// </summary>
    public static class KebammCombo
    {
        public static int ComboCount { get; private set; }
        public static int BarPoints { get; private set; }
        public static int QueuedBombs { get; private set; }
        public static int TotalComboPoints { get; private set; }
        /// <summary>Bomb level rule: normal drops since the last rule bomb (level bombEveryMoves). Combo and debug bombs do not touch it.</summary>
        public static int MovesSinceRuleBomb { get; private set; }

        public static event Action Changed;
        /// <summary>Raised with the number of bombs earned when the bar fills (once per merge, even for several fills).</summary>
        public static event Action<int> BarFilled;

        static KebammComboConfig _config;

        public static KebammComboConfig Config
        {
            get
            {
                if (_config == null)
                {
                    var asset = Resources.Load<KebammComboConfig>("Kebamm/combo_config");
                    if (asset == null)
                    {
                        Debug.LogWarning("[Kebamm] combo_config missing from Resources/Kebamm; using built-in combo defaults.");
                        asset = ScriptableObject.CreateInstance<KebammComboConfig>();
                    }
                    _config = KebammRemoteData.ForComboConfig(asset);
                }
                return _config;
            }
        }

        /// <summary>Drop the cached config (remote data was applied).</summary>
        public static void Reload()
        {
            _config = null;
        }

        public static void ResetAttempt()
        {
            ComboCount = 0;
            BarPoints = 0;
            QueuedBombs = 0;
            TotalComboPoints = 0;
            MovesSinceRuleBomb = 0;
            Changed?.Invoke();
        }

        /// <summary>A normal ball left the launcher: new combo window. Magic Bomb drops do not call this.</summary>
        public static void OnBallDropped()
        {
            ComboCount = 0;
        }

        /// <param name="progressModifier">Level Data combo_progress_modifier; multiplies points paid into the bar only (not score / Combo N! text). Non-positive = 1.</param>
        public static void OnMerge(Vector3 position, float progressModifier = 1f)
        {
            if (KebammScoreHud.Instance != null && KebammScoreHud.Instance.IsEndVisible)
                return;
            KebammComboConfig cfg = Config;
            ComboCount++;
            int basePts = cfg.PointsFor(ComboCount);
            float mod = progressModifier > 0f ? progressModifier : 1f;
            int paid = Mathf.Max(0, Mathf.RoundToInt(basePts * mod)); // e.g. 20 x 1.5 = 30 into the bar
            TotalComboPoints += paid;
            BarPoints += paid;
            int fills = 0;
            int cap = cfg.BarCapacity;
            while (BarPoints >= cap)
            {
                BarPoints -= cap;
                fills++;
            }
            QueuedBombs += fills;
            Debug.Log($"[Kebamm] Combo {ComboCount}: +{paid} combo points (base {basePts} x {mod:0.###}), bar {BarPoints}/{cap}, attempt total {TotalComboPoints}");
            if (ComboCount >= cfg.minComboToShowText) // points and bar still count below this; only the popup is skipped
                KebammComboText.Spawn(position + Vector3.up * cfg.comboTextYOffset, cfg.TextFor(ComboCount), cfg);
            Changed?.Invoke();
            if (fills > 0)
            {
                Debug.Log($"[Kebamm] Combo bar filled x{fills}: Magic Bomb queued (queue {QueuedBombs})");
                BarFilled?.Invoke(fills);
            }
        }

        /// <summary>
        /// Bomb level rule (Level Data bomb_every_moves): call once per NORMAL drop (Magic Bomb drops never count).
        /// Every <paramref name="everyMoves"/> drops queues 1 Magic Bomb in the shared queue (no cap) and resets the counter. 0 = off.
        /// </summary>
        public static void OnNormalDropForBombRule(int everyMoves)
        {
            if (everyMoves <= 0)
                return;
            MovesSinceRuleBomb++;
            if (MovesSinceRuleBomb < everyMoves)
                return;
            MovesSinceRuleBomb = 0;
            QueuedBombs++;
            Debug.Log($"[Kebamm] Bomb level rule: {everyMoves} drops, Magic Bomb queued (queue {QueuedBombs})");
            Changed?.Invoke();
        }

        /// <summary>Debug console 'bomb': adds Magic Bombs to the launcher queue. Does not touch the combo bar, score or save.</summary>
        public static void DebugQueueBombs(int count)
        {
            if (count <= 0)
                return;
            QueuedBombs += count;
            Changed?.Invoke();
        }

        /// <summary>Takes one queued bomb for the launcher. False if none are queued.</summary>
        public static bool TryTakeBomb()
        {
            if (QueuedBombs <= 0)
                return false;
            QueuedBombs--;
            Changed?.Invoke();
            return true;
        }
    }
}
