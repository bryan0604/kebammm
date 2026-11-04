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
            Changed?.Invoke();
        }

        /// <summary>A normal ball left the launcher: new combo window. Magic Bomb drops do not call this.</summary>
        public static void OnBallDropped()
        {
            ComboCount = 0;
        }

        public static void OnMerge(Vector3 position)
        {
            if (KebammScoreHud.Instance != null && KebammScoreHud.Instance.IsEndVisible)
                return;
            KebammComboConfig cfg = Config;
            ComboCount++;
            int pts = cfg.PointsFor(ComboCount);
            TotalComboPoints += pts;
            BarPoints += pts;
            int fills = 0;
            int cap = cfg.BarCapacity;
            while (BarPoints >= cap)
            {
                BarPoints -= cap;
                fills++;
            }
            QueuedBombs += fills;
            Debug.Log($"[Kebamm] Combo {ComboCount}: +{pts} combo points, bar {BarPoints}/{cap}, attempt total {TotalComboPoints}");
            KebammComboText.Spawn(position + Vector3.up * cfg.comboTextYOffset, cfg.TextFor(ComboCount), cfg);
            Changed?.Invoke();
            if (fills > 0)
            {
                Debug.Log($"[Kebamm] Combo bar filled x{fills}: Magic Bomb queued (queue {QueuedBombs})");
                BarFilled?.Invoke(fills);
            }
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
