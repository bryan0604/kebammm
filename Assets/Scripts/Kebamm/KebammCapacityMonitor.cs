using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// CAPACITY RULE (see also KebammPrototypeBootstrap header):
    /// If any live ball's collider stays overlapping this trigger for 1.0 continuous
    /// seconds while its Rigidbody2D.linearVelocity magnitude is below SettledSpeed,
    /// the container floor opens and balls fall onto the monster.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class KebammCapacityMonitor : MonoBehaviour
    {
        public float HoldSeconds = 1f;
        public float SettledSpeed = 0.15f;

        KebammContainer _container;
        readonly Dictionary<int, float> _overlapTimers = new Dictionary<int, float>();

        public void Init(KebammContainer container, float width, float thickness)
        {
            _container = container;
            var box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(width, thickness);
            var rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (_container == null || _container.IsOpen)
                return;

            var ball = other.GetComponent<KebammBall>();
            if (ball == null || ball.IsPreview || ball.IsMerging)
                return;

            var rb = other.attachedRigidbody;
            if (rb == null)
                return;

            int id = ball.GetInstanceID();
            bool settled = rb.linearVelocity.magnitude <= SettledSpeed;
            if (!settled)
            {
                _overlapTimers[id] = 0f;
                return;
            }

            if (!_overlapTimers.ContainsKey(id))
                _overlapTimers[id] = 0f;

            _overlapTimers[id] += Time.fixedDeltaTime;
            if (_overlapTimers[id] >= HoldSeconds)
            {
                Debug.Log($"[Kebamm] Capacity breached by {ball.name} (settled {HoldSeconds:0.0}s above line)");
                _overlapTimers.Clear();
                _container.OpenFloor();
            }
        }

        void OnTriggerExit2D(Collider2D other)
        {
            var ball = other.GetComponent<KebammBall>();
            if (ball == null)
                return;
            _overlapTimers.Remove(ball.GetInstanceID());
        }

        public void ResetTimers()
        {
            _overlapTimers.Clear();
        }
    }
}
