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

        public static event Action Changed;

        public static void ResetScores()
        {
            _locked = false;
            MergeScore = 0;
            MonsterScore = 0;
            Changed?.Invoke();
        }

        public static void Lock()
        {
            _locked = true;
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

        public static void AddDestroy(int points)
        {
            if (_locked)
                return;

            MonsterScore += points;
            Debug.Log($"[Kebamm] Monster destroy score +{points} total={MonsterScore}");
            Changed?.Invoke();
        }
    }
}