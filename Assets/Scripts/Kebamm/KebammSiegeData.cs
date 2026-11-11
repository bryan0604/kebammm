using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Trebuchet and Rock stats (GDD [Level] Monster Group Design). Trebuchet is obstacle-like, Rock is ball-like.
    /// Asset: Resources/Kebamm/Siege/siege_data. Values are TEMPORARY placeholders until filled in manually.
    /// </summary>
    [CreateAssetMenu(fileName = "SiegeData", menuName = "Kebamm/Siege Data (Trebuchet + Rock)")]
    public class KebammSiegeData : ScriptableObject
    {
        [Header("Trebuchet (takes damage like obstacles)")]
        public string trebuchetId = "trebuchet_01";
        public string trebuchetName = "Trebuchet";
        [Tooltip("Placeholder. Loses ball damage per hit (obstacle rule). <= 0 = destroyed (darkened, stays in place).")][Min(1f)] public float trebuchetHp = 100f;
        [Tooltip("Placeholder uniform scale of the trebuchet visual + collider.")][Min(0.1f)] public float trebuchetScale = 1f;
        [Tooltip("Random X placement: gap kept between the trebuchet's edge (half-width 0.85 x scale) and the platform / screen edge.")][Min(0f)] public float trebuchetEdgeMargin = 0.35f;

        [Header("Rock (takes damage like balls)")]
        public string rockId = "rock_01";
        public string rockName = "Rock";
        [Tooltip("Placeholder. Ball rule: health - damage (e.g. Magic Bomb); <= 0 = removed.")][Min(1f)] public float rockHp = 10f;
        [Tooltip("Placeholder rock diameter in world units.")][Min(0.1f)] public float rockScale = 0.6f;
        [Tooltip("Placeholder physics mass (balls are 1 + tier x 0.75).")][Min(0.1f)] public float rockMass = 6f;
        [Tooltip("Placeholder multiplier on ball gravity (1.4) so the rock falls heavier.")][Min(0.1f)] public float rockGravityMultiplier = 1.5f;

        const string ResourcePath = "Kebamm/Siege/siege_data";
        static KebammSiegeData _cached;

        public static KebammSiegeData Get()
        {
            if (_cached == null)
                _cached = Resources.Load<KebammSiegeData>(ResourcePath);
            if (_cached == null)
                _cached = CreateInstance<KebammSiegeData>(); // code defaults
            return _cached;
        }
    }
}