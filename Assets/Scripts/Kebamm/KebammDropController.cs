using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Kebamm
{
    /// <summary>
    /// Mouse X aims drop position; left-click or Space drops the queued ball.
    /// Touch (GDD Mobile - Touch Screen Control): touch and hold anywhere, the launcher jumps to the
    /// first finger's X; releasing that finger drops the ball. Touches that start on UI are ignored.
    /// Uses the new Input System (project activeInputHandler = Input System Only).
    /// </summary>
    public class KebammDropController : MonoBehaviour
    {
        public float DropCooldown = 0.35f;
        public float DropY = 3.6f;

        KebammContainer _container;
        KebammPrototypeBootstrap _bootstrap;
        KebammBall _preview;
        float _cooldownLeft;
        bool _canDrop = true;
        // Primary-finger state: holding = aiming with a touch that did not start on UI.
        bool _touchHolding;

        public void Init(KebammPrototypeBootstrap bootstrap, KebammContainer container, float dropY)
        {
            _bootstrap = bootstrap;
            _container = container;
            DropY = dropY;
            SpawnPreview();
        }

        public void SetDroppingEnabled(bool enabled)
        {
            _canDrop = enabled;
            if (_preview != null)
                _preview.gameObject.SetActive(enabled && (_container == null || !_container.IsOpen));
        }

        void Update()
        {
            if (_bootstrap == null || _container == null)
                return;

            if (_cooldownLeft > 0f)
                _cooldownLeft -= Time.deltaTime;

            bool allowDrop = _canDrop && !_container.IsOpen;
            if (_preview != null && _preview.gameObject.activeSelf != allowDrop)
                _preview.gameObject.SetActive(allowDrop);

            if (!allowDrop || KebammPrototypeBootstrap.GameplayInputBlocked)
            {
                _touchHolding = false; // cancel a hold; lifting later must not drop
                return;
            }

            bool dropPressed = false;
            bool touchActive = HandleTouch(ref dropPressed);

            // Mouse is skipped while a touch is active this frame, so simulated mouse-from-touch cannot double-drop.
            if (!touchActive)
            {
                // Follow the mouse only when it moved: on mobile an idle Mouse device reads (0,0) = far left.
                if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.01f)
                    UpdateAim(Mouse.current.position.ReadValue());
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !PointerOverUi(-1))
                    dropPressed = true;
            }
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                dropPressed = true;

            if (dropPressed && _cooldownLeft <= 0f)
                Drop();
        }

        /// <summary>Primary finger only (extra fingers are ignored until it lifts). Returns true if a touch was active this frame.</summary>
        bool HandleTouch(ref bool dropPressed)
        {
            var ts = Touchscreen.current;
            if (ts == null)
                return false;
            var touch = ts.primaryTouch;
            bool pressed = touch.press.wasPressedThisFrame;
            bool held = touch.press.isPressed;
            bool released = touch.press.wasReleasedThisFrame;
            if (!pressed && !held && !released)
                return false;

            int id = touch.touchId.ReadValue();
            if (pressed)
                _touchHolding = !PointerOverUi(id);
            // UI hover can lag a frame behind the press, so also cancel if the finger is over UI while held.
            if (_touchHolding && PointerOverUi(id))
                _touchHolding = false;
            if (_touchHolding)
                UpdateAim(touch.position.ReadValue());
            if (released)
            {
                if (_touchHolding)
                    dropPressed = true;
                _touchHolding = false;
            }
            return true;
        }

        static bool PointerOverUi(int pointerId)
        {
            var es = EventSystem.current;
            if (es == null)
                return false;
            return pointerId >= 0 ? es.IsPointerOverGameObject(pointerId) : es.IsPointerOverGameObject();
        }

        // Last aim position (mouse or finger), reused when a new held ball spawns after a drop.
        Vector2? _lastAimScreen;

        // Launcher X kept for the app session (static), so new levels and restarts start where the last ball
        // was aimed (GDD Controls). Not saved to disk: an app restart starts from the centre again.
        static float? s_sessionLauncherX;

        void UpdateAim()
        {
            if (_lastAimScreen.HasValue)
                UpdateAim(_lastAimScreen.Value);
            else if (s_sessionLauncherX.HasValue)
                PlaceAtWorldX(s_sessionLauncherX.Value);
            else if (_preview != null)
                UpdateAim(Camera.main != null ? (Vector2)Camera.main.WorldToScreenPoint(transform.position) : Vector2.zero);
        }

        void UpdateAim(Vector2 screen)
        {
            if (_preview == null)
                return;
            _lastAimScreen = screen;

            float x = transform.position.x;
            var cam = Camera.main;
            if (cam != null)
            {
                // ScreenToWorldPoint's z is distance from the camera, not world Z.
                // Camera sits at z=-10 looking at gameplay on z=0.
                float depth = Mathf.Abs(cam.transform.position.z);
                if (depth < 0.01f)
                    depth = 10f;
                Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
                x = world.x;
            }
            PlaceAtWorldX(x);
        }

        /// <summary>Clamps to the current container's inner width (it can differ per level) and moves the held ball.</summary>
        void PlaceAtWorldX(float x)
        {
            if (_preview == null || _container == null)
                return;
            float pad = _preview.Radius + 0.05f;
            float minX = _container.transform.position.x - _container.InnerHalfWidth + pad;
            float maxX = _container.transform.position.x + _container.InnerHalfWidth - pad;
            x = Mathf.Clamp(x, minX, maxX);
            _preview.transform.position = new Vector3(x, DropY, 0f);
            s_sessionLauncherX = x;
        }

        void Drop()
        {
            if (_preview == null)
                return;

            // Reuse the held ball as the dropped ball instead of destroying it and building a new
            // GameObject in the same frame (removes one create + one destroy per drop).
            var held = _preview;
            _preview = null;
            held.transform.position = new Vector3(held.transform.position.x, DropY, 0f);
            _bootstrap.ReleasePreview(held);
            _cooldownLeft = DropCooldown;
            SpawnPreview();
        }

        public void SpawnPreview()
        {
            if (_preview != null)
                Destroy(_preview.gameObject);

            // The held ball shows the next colour and tier ahead of the drop; it is re-rolled after each drop.
            var colour = _bootstrap.NextQueuedColour();
            int tier = _bootstrap.NextQueuedTier();
            _preview = _bootstrap.SpawnBall(new Vector3(0f, DropY, 0f), colour, tier, preview: true);
            if (_preview != null)
                UpdateAim();
            if (_preview != null)
                _preview.gameObject.SetActive(_canDrop && !_container.IsOpen);
        }

        public void ClearPreview()
        {
            if (_preview != null)
            {
                Destroy(_preview.gameObject);
                _preview = null;
            }
        }
    }
}
