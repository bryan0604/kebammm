using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// CAPACITY RULE (GDD Game Transition): polled every FixedUpdate.
    /// A ball is in play from its first contact with the floor, a wall or a ball that is already in play (KebammBall.InPlay),
    /// and passes it on to balls resting on it; a merged ball is in play only if one of its two source balls was. The held/aimed ball and a fresh drop before its first contact never count.
    /// The moment any in-play ball touches the cap band (collider top at or above the drawn red line's bottom edge), the
    /// container floor opens (bombing run) immediately. Fires once; only during normal play.
    /// Near-cap blink (GDD Visual Near Max Caps Line): if any in-play jar ball's top edge is within closeDistance
    /// below the line, the drawn cap line lerps white/red until none are close, the bombing run starts, or the level resets.
    /// Held/preview balls, magic bombs, spent/removed balls and rocks are not counted.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class KebammCapacityMonitor : MonoBehaviour
    {
        KebammContainer _container;
        BoxCollider2D _box;

        /// <summary>Kept for callers; there is no hold timer any more.</summary>
        public float Timer => 0f;

        public void Init(KebammContainer container, float width, float thickness)
        {
            _container = container;
            _box = GetComponent<BoxCollider2D>();
            _box.isTrigger = true;
            _box.size = new Vector2(width, thickness);
            var rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
        }

        /// <summary>Live layout: move the cap band to y and resize it to width.</summary>
        public void SetBand(float y, float width)
        {
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
            if (_box != null)
                _box.size = new Vector2(width, _box.size.y);
        }

        // Trip at the drawn red line's bottom edge (not the thicker trigger band's bottom).
        float BandBottom => _container != null ? _container.CapLineBottomY : transform.position.y;

        void Update()
        {
            if (_container == null)
                return;
            var boot = KebammPrototypeBootstrap.Instance;
            if (_container.IsOpen || (boot != null && (boot.IsBombingRun || boot.InMainMenu)))
            {
                _container.RestoreCapLineColor();
                return;
            }

            var gm = boot != null ? boot.GameManagerData : null;
            float close = gm != null ? gm.MaxCapLineCloseDistance : 1f;
            float half = gm != null ? gm.MaxCapLineBlinkHalfPeriod : 0.25f;
            _container.TickCapLineBlink(AnyBallCloseToCapLine(close), half);
        }

        /// <summary>
        /// True if any in-play (non-preview, non-spent) ball's top edge (position + radius) is within closeDistance
        /// world units below the drawn cap line's bottom edge. Rocks and magic bombs are not in KebammBall.Live.
        /// </summary>
        bool AnyBallCloseToCapLine(float closeDistance)
        {
            float lineY = BandBottom;
            var live = KebammBall.Live;
            for (int i = 0; i < live.Count; i++)
            {
                KebammBall ball = live[i];
                if (ball == null || ball.IsPreview || ball.IsMerging || ball.Spent || !ball.InPlay || !ball.gameObject.activeInHierarchy)
                    continue;
                float below = lineY - (ball.transform.position.y + ball.Radius);
                if (below >= 0f && below <= closeDistance)
                    return true;
            }
            return false;
        }

        void FixedUpdate()
        {
            if (_container == null || _container.IsOpen)
                return;
            var boot = KebammPrototypeBootstrap.Instance;
            if (boot != null && boot.IsBombingRun)
                return;

            float bandBottom = BandBottom;
            var live = KebammBall.Live;
            for (int i = 0; i < live.Count; i++)
            {
                KebammBall ball = live[i];
                if (ball == null || ball.IsPreview || ball.IsMerging || !ball.InPlay || !ball.gameObject.activeInHierarchy)
                    continue;
                Collider2D col = ball.Col;
                if (col == null || !col.enabled || col.bounds.max.y < bandBottom)
                    continue;

                Debug.Log($"[Kebamm] Capacity breached ({ball.name} in play touched the line)");
                _container.OpenFloor();
                return;
            }
        }

        /// <summary>Called by the bootstrap when a merge spawns a ball: in play only if one of its source balls was.</summary>
        public void NotifyMerge(KebammBall merged, bool inPlay = true)
        {
            if (merged != null)
                merged.InPlay = inPlay;
        }

        public void ResetTimers()
        {
            _container?.RestoreCapLineColor();
        }
    }
}