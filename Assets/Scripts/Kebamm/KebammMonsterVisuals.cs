using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Optional art per monster colour. Loaded from Resources/Kebamm/monster_visuals.
    /// A colour with a sprite uses that art with <see cref="spriteTint"/> (white = no recolour);
    /// a colour without one keeps the tinted square placeholder.
    /// If the asset or a field is empty, Resources/Kebamm/Monsters/monster_&lt;colour&gt; (e.g. monster_red) is tried by name.
    /// Sprites should be imported so the visible body is 1 x 1 unit with the pivot at the body centre,
    /// matching the 1 x 1 placeholder square and its collider.
    /// </summary>
    [CreateAssetMenu(fileName = "monster_visuals", menuName = "Kebamm/Monster Visuals")]
    public class KebammMonsterVisuals : ScriptableObject
    {
        public const string ResourcePath = "Kebamm/monster_visuals";
        public const string SpriteFolder = "Kebamm/Monsters/monster_";

        [Header("Sprite per colour (empty = tinted square placeholder)")]
        public Sprite red;
        public Sprite blue;
        public Sprite green;
        public Sprite yellow;

        [Header("Look")]
        [Tooltip("Tint for monsters that use a sprite. White keeps the art's own colours.")]
        public Color spriteTint = Color.white;
        [Tooltip("Hit flash tint for sprite monsters (the placeholder flashes white, which is invisible on a white-tinted sprite).")]
        public Color spriteHitFlash = new Color(1f, 0.55f, 0.55f, 1f);

        static KebammMonsterVisuals _cached;
        static bool _loaded;

        public static KebammMonsterVisuals Load()
        {
            if (!_loaded)
            {
                _cached = Resources.Load<KebammMonsterVisuals>(ResourcePath);
                _loaded = true;
            }
            return _cached;
        }

        public Sprite FieldFor(KebammBallColour colour)
        {
            switch (colour)
            {
                case KebammBallColour.Red: return red;
                case KebammBallColour.Blue: return blue;
                case KebammBallColour.Green: return green;
                case KebammBallColour.Yellow: return yellow;
                default: return null;
            }
        }

        /// <summary>Sprite for a colour, or null to keep the placeholder square.</summary>
        public static Sprite SpriteFor(KebammBallColour colour)
        {
            KebammMonsterVisuals visuals = Load();
            Sprite sprite = visuals != null ? visuals.FieldFor(colour) : null;
            if (sprite == null)
                sprite = Resources.Load<Sprite>(SpriteFolder + colour.ToString().ToLowerInvariant());
            return sprite;
        }

        public static Color TintFor()
        {
            KebammMonsterVisuals visuals = Load();
            return visuals != null ? visuals.spriteTint : Color.white;
        }

        public static Color HitFlashFor()
        {
            KebammMonsterVisuals visuals = Load();
            return visuals != null ? visuals.spriteHitFlash : new Color(1f, 0.55f, 0.55f, 1f);
        }
    }
}
