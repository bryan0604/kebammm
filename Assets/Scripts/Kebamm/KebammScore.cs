using System;
using UnityEngine;

namespace Kebamm
{
    public static class KebammScore
    {
        static bool _locked;

        public static int MergeScore { get; private set; }
        public static int MonsterScore { get; private set; }
        public static int FinalScore => MergeScore + MonsterScore;
        public static int MonstersDestroyed { get; private set; }
        /// <summary>Seconds of play since the last reset (main menu, pause and score screen excluded). Analytics duration_sec.</summary>
        public static float PlaySeconds { get; private set; }

        /// <summary>GDD tiers 1-6 (code play tier 0-5; tier 6 only comes from merging two tier 5 balls).</summary>
        public const int BallTierCount = KebammBall.MaxTier + 1;
        static readonly int[] _ballsByTier = new int[BallTierCount];
        /// <summary>Balls created this attempt (launcher drops + merges, any colour) for GDD tier 1..BallTierCount.</summary>
        public static int BallsCreated(int gddTier) => gddTier >= 1 && gddTier <= BallTierCount ? _ballsByTier[gddTier - 1] : 0;

        /// <summary>Counts one ball created at this code play tier (0-based).</summary>
        public static void CountBall(int playTier)
        {
            if (_locked)
                return;
            _ballsByTier[Mathf.Clamp(playTier, 0, BallTierCount - 1)]++;
        }


        public static event Action Changed;

        public static void ResetScores()
        {
            _locked = false;
            MergeScore = 0;
            MonsterScore = 0;
            MonstersDestroyed = 0;
            PlaySeconds = 0f;
            Array.Clear(_ballsByTier, 0, _ballsByTier.Length);
            Changed?.Invoke();
        }

        public static void Lock()
        {
            _locked = true;
        }

        /// <summary>Called from the bootstrap Update only while a level is being played (not paused, not in the main menu).</summary>
        public static void Tick(float dt)
        {
            if (!_locked)
                PlaySeconds += dt;
        }

        public static void AddMerge(int points)
        {
            if (_locked)
                return;

            MergeScore += points;
            Debug.Log($"[Kebamm] Merge score +{points} total={MergeScore}");
            Changed?.Invoke();
        }

        public static void AddHit(int points)
        {
            if (_locked)
                return;

            MonsterScore += points;
            Debug.Log($"[Kebamm] Monster hit score +{points} total={MonsterScore}");
            Changed?.Invoke();
        }

        /// <summary>Building destroyed: points go into the combined HUD score; does not count as a monster destroyed (stars).</summary>
        public static void AddObstacleDestroy(int points)
        {
            if (_locked)
                return;

            MonsterScore += points;
            Debug.Log($"[Kebamm] Obstacle destroy score +{points} total={MonsterScore}");
            Changed?.Invoke();
        }

        public static void AddDestroy(int points)
        {
            if (_locked)
                return;

            MonsterScore += points;
            MonstersDestroyed++;
            Debug.Log($"[Kebamm] Monster destroy score +{points} total={MonsterScore} destroyed={MonstersDestroyed}");
            Changed?.Invoke();
        }
    }
}