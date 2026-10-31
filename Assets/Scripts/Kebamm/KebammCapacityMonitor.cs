using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// CAPACITY RULE (see also KebammPrototypeBootstrap header):
    /// Polled every FixedUpdate (so sleeping balls count). One shared timer.
    /// A ball is "over the line" when its collider top is at or above the cap band bottom
    /// (touching the band or resting anywhere above it). Preview balls never count.
    /// - Balls that have not settled since spawning (still falling in) are skipped entirely.
    /// - Any previously settled over-the-line ball moving faster than SettledSpeed (jiggle), or merging,
    ///   or a merge producing a ball over the line, resets the timer to 0.
    /// - No settled, non-merging ball over the line also resets it to 0.
    /// - Otherwise the timer runs; at HoldSeconds the container floor opens (bombing run).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class KebammCapacityMonitor : MonoBehaviour
    {
        public float HoldSeconds = 1f;
        public float SettledSpeed = 0.15f;

        KebammContainer _container;
        BoxCollider2D _box;
        float _timer;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const float LogInterval = 0.25f;
        float _nextLogTime;
#endif

        public float Timer => _timer;

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

        float BandBottom => _box != null ? _box.bounds.min.y : transform.position.y;

        void FixedUpdate()
        {
            if (_container == null || _container.IsOpen)
            {
                _timer = 0f;
                return;
            }

            float bandBottom = BandBottom;
            bool anyQualifying = false;
            bool reset = false;

            var live = KebammBall.Live;
            for (int i = 0; i < live.Count; i++)
            {
                KebammBall ball = live[i];
                if (ball == null || ball.IsPreview || !ball.gameObject.activeInHierarchy)
                    continue;
                Collider2D col = ball.Col;
                if (col == null || !col.enabled || col.bounds.max.y < bandBottom)
                    continue;

                if (ball.IsMerging)
                {
                    reset = true;
                    break;
                }

                Rigidbody2D rb = ball.Body;
                bool settled = rb == null || !rb.IsAwake() || rb.linearVelocity.sqrMagnitude <= SettledSpeed * SettledSpeed;
                if (!ball.HasSettled)
                {
                    // Still falling in since drop/spawn: skipped entirely (no reset, doesn't count).
                    if (!settled)
                        continue;
                    ball.HasSettled = true;
                }
                if (!settled)
                {
                    // Settled earlier, now moving again (jiggle).
                    reset = true;
                    break;
                }
                anyQualifying = true;
            }

            if (reset || !anyQualifying)
            {
                SetTimer(0f, reset ? "reset (moving/merging ball over line)" : null);
                return;
            }

            SetTimer(_timer + Time.fixedDeltaTime, null);
            if (_timer >= HoldSeconds)
            {
                Debug.Log($"[Kebamm] Capacity breached (settled ball(s) over line for {HoldSeconds:0.0}s)");
                _timer = 0f;
                _container.OpenFloor();
            }
        }

        /// <summary>Called by the bootstrap when a merge spawns a ball; resets if it is over the line.</summary>
        public void NotifyMerge(KebammBall merged)
        {
            if (merged == null)
                return;
            Collider2D col = merged.Col;
            if (col != null && col.bounds.max.y >= BandBottom)
                SetTimer(0f, "reset (merge over line)");
        }

        void SetTimer(float value, string reason)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool wasRunning = _timer > 0f;
#endif
            _timer = value;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_timer > 0f && Time.unscaledTime >= _nextLogTime)
            {
                _nextLogTime = Time.unscaledTime + LogInterval;
                Debug.Log($"[Kebamm] Capacity hold {_timer:0.00}/{HoldSeconds:0.0}s");
            }
            else if (wasRunning && _timer <= 0f)
            {
                Debug.Log($"[Kebamm] Capacity hold {reason ?? "reset (no settled ball over line)"}");
            }
#endif
        }

        public void ResetTimers()
        {
            _timer = 0f;
        }
    }
}