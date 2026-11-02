using UnityEngine;

namespace Kebamm
{
    public enum KebammObstacleShape
    {
        Rectangle = 0,
        Square = 1,
        Triangle = 2
    }

    /// <summary>
    /// One building obstacle shape (Notion: Data Structure / Obstacle Data).
    /// A shape is only spawned once hp, width and height are all above 0.
    /// </summary>
    [CreateAssetMenu(fileName = "ObstacleData", menuName = "Kebamm/Obstacle Data")]
    public class KebammObstacleData : ScriptableObject
    {
        public KebammObstacleShape shape;
        [Tooltip("Building health. Damage = ball damage - building HP (same rule as monsters). 0 = shape not spawned.")][Min(0f)] public float hp;
        [Tooltip("Width in world units. 0 = shape not spawned.")][Min(0f)] public float width;
        [Tooltip("Height in world units. 0 = shape not spawned.")][Min(0f)] public float height;
        [Tooltip("Score points when the building reaches HP <= 0.")][Min(0)] public int destroyedPts;
        [Tooltip("Minimum side gap (world units) to the next building stack. 0 = touching allowed.")][Min(0f)] public float stackSpacing;
        [Tooltip("Physics mass once active in the bombing run. 0 = 2 (same as monsters).")][Min(0f)] public float mass;
        [Tooltip("Multiplies ball impact strength for the sideways push. 0 = 1.")][Min(0f)] public float pushMultiplier;

        public bool IsUsable => hp > 0f && width > 0f && height > 0f;
        public float Mass => mass > 0f ? mass : 2f;
        public float PushMultiplier => pushMultiplier > 0f ? pushMultiplier : 1f;
    }
}