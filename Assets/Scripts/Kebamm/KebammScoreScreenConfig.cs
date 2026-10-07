using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Score screen timing and star rule. Loaded from Resources/Kebamm/score_screen_config.
    /// GDD cue: Score Screen > 1s > Scores Numbers (Merge Score, Monster Score) > 1s > 3 Stars Logics > 2s > Next Button.
    /// Each delay counts from the previous step.
    /// </summary>
    [CreateAssetMenu(fileName = "score_screen_config", menuName = "Kebamm/Score Screen Config")]
    public class KebammScoreScreenConfig : ScriptableObject
    {
        public const string ResourcePath = "Kebamm/score_screen_config";

        [Header("Sequence (seconds, each after the previous step)")]
        [Tooltip("After the score screen opens, wait this long before showing the Merge and Monster score numbers.")]
        public float scoreNumbersDelay = 1f;
        [Tooltip("After the score numbers appear, wait this long before the stars pop.")]
        public float starsDelay = 1f;
        [Tooltip("After the stars start popping, wait this long before showing the Next button.")]
        public float nextButtonDelay = 2f;

        [Header("Star pop")]
        public float starPopDuration = 0.35f;
        public float starPopStagger = 0.18f;

        // GDD (Scores): 1 star for 1 monster killed, 2 stars for 3, 3 stars for 5. Applies to every level.
        [Header("Stars (monsters killed this session)")]
        [Tooltip("Monsters killed needed for 1 star.")] public int star1Kills = 1;
        [Tooltip("Monsters killed needed for 2 stars.")] public int star2Kills = 3;
        [Tooltip("Monsters killed needed for 3 stars.")] public int star3Kills = 5;

        static KebammScoreScreenConfig _cached;

        public static KebammScoreScreenConfig Load()
        {
            if (_cached != null)
                return _cached;

            _cached = Resources.Load<KebammScoreScreenConfig>(ResourcePath);
            if (_cached == null)
            {
                Debug.LogWarning("[Kebamm] score_screen_config missing from Resources/Kebamm; using defaults.");
                _cached = CreateInstance<KebammScoreScreenConfig>();
            }
            return _cached;
        }

        public int StarsForKills(int kills)
        {
            if (kills >= star3Kills)
                return 3;
            if (kills >= star2Kills)
                return 2;
            if (kills >= star1Kills)
                return 1;
            return 0;
        }

        void OnValidate()
        {
            if (scoreNumbersDelay < 0f)
                scoreNumbersDelay = 0f;
            if (starsDelay < 0f)
                starsDelay = 0f;
            if (nextButtonDelay < 0f)
                nextButtonDelay = 0f;
            if (starPopDuration < 0.01f)
                starPopDuration = 0.01f;
            if (starPopStagger < 0f)
                starPopStagger = 0f;
            if (star1Kills < 1)
                star1Kills = 1;
            if (star2Kills < star1Kills)
                star2Kills = star1Kills;
            if (star3Kills < star2Kills)
                star3Kills = star2Kills;
        }
    }
}