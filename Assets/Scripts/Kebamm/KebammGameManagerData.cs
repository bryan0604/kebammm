using System;
using UnityEngine;

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

        [Header("Main Menu Title")]
        [Tooltip("Seconds for one half (up or down) of the title letters' repeated breathing bob. Applied each time the main menu is shown.")]
        public float titleBreathDuration = 1.1f;
        [Tooltip("Title letter image height in reference pixels (1080x1920 canvas); the title still shrinks to fit 980 px wide. Applied when the menu is built (app launch).")]
        public float titleLetterHeight = 200f;

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
        public float TitleBreathDuration => Mathf.Max(0.1f, titleBreathDuration);
        public float TitleLetterHeight => Mathf.Max(10f, titleLetterHeight);

        void OnValidate()
        {
            pillarHeight = Mathf.Max(MinPillarHeight, pillarHeight);
            pillarGapWidth = Mathf.Max(MinPillarGapWidth, pillarGapWidth);
            capsLineHeight = Mathf.Max(MinCapsLineHeight, capsLineHeight);
            Changed?.Invoke(this);
        }
    }
}