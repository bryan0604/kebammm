using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Kebamm
{
    /// <summary>
    /// Snap control (GDD Mobile - Touch Screen Control): touch/click and hold anywhere; the launcher snaps instantly to
    /// the finger's/mouse's X (clamped pillar-to-pillar) and follows it instantly while held.
    /// Releasing drops the ball; holding never drops. Space also drops. Touches that start on UI are ignored;
    /// a touch during which 3+ fingers were down (debug console hold) never drops.
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
            _hasDisplaced = false; // a ball displaced by the debug 'bomb' command does not carry into a new level
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
            UpdateBombIndicator();

            if (!allowDrop || KebammPrototypeBootstrap.GameplayInputBlocked || KebammPrototypeBootstrap.IsPaused)
            {
                _touchHolding = false; // cancel a hold; lifting later must not drop
                _mouseHolding = false;
                return;
            }

            bool dropPressed = false;
            bool touchActive = HandleTouch(ref dropPressed);

            // Mouse is skipped while a touch is active this frame, so simulated mouse-from-touch cannot double-drop.
            // Same relative drag as touch: press holds, horizontal movement steers, release drops.
            var mouse = Mouse.current;
            if (!touchActive && mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    _mouseHolding = !PointerOverUi(-1);
                    if (_mouseHolding)
                        BeginDrag(mouse.position.ReadValue().x);
                }
                if (_mouseHolding)
                    Drag(mouse.position.ReadValue().x);
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    if (_mouseHolding)
                        dropPressed = true;
                    _mouseHolding = false;
                }
            }
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                dropPressed = true;

            FollowTarget();
            UpdateBombIndicator();

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
            {
                _touchHolding = !PointerOverUi(id);
                if (_touchHolding)
                    BeginDrag(touch.position.ReadValue().x);
            }
            // UI hover can lag a frame behind the press, so also cancel if the finger is over UI while held.
            if (_touchHolding && PointerOverUi(id))
                _touchHolding = false;
            // 3+ fingers during this touch (debug console hold): cancel, lifting must not drop.
            if (_touchHolding && CountTouchesDown(ts) >= 3)
                _touchHolding = false;
            if (_touchHolding)
                Drag(touch.position.ReadValue().x);
            if (released)
            {
                if (_touchHolding)
                    dropPressed = true;
                _touchHolding = false;
            }
            return true;
        }

        static int CountTouchesDown(Touchscreen ts)
        {
            int n = 0;
            foreach (var t in ts.touches)
                if (t.press.isPressed)
                    n++;
            return n;
        }

        static bool PointerOverUi(int pointerId)
        {
            var es = EventSystem.current;
            if (es == null)
                return false;
            return pointerId >= 0 ? es.IsPointerOverGameObject(pointerId) : es.IsPointerOverGameObject();
        }

        // Launcher X kept for the app session (static), so new levels and restarts start where the last ball
        // was aimed (GDD Controls). Not saved to disk: an app restart starts from the centre again.
        static float? s_sessionLauncherX;

        // Snap control: the target is the finger's/mouse's world X (absolute), set on press and every held frame.
        bool _mouseHolding;
        float _dragStartScreenX;
        float _dragStartTargetX;
        float _targetX;
        bool _hasTarget;

        /// <summary>Fallback easing sharpness (1/s) if Game Manager data is missing. Higher = snappier; 0 = no easing.</summary>
        public float FollowSharpness = 0f;

        void BeginDrag(float screenX)
        {
            EnsureTarget();
            _dragStartScreenX = screenX;
            _dragStartTargetX = _targetX;
            Drag(screenX); // snap on touch-down
        }

        void Drag(float screenX)
        {
            if (_preview == null || _container == null || Screen.width <= 0)
                return;
            var cam = Camera.main;
            if (cam == null)
                return;
            // ScreenToWorldPoint's z is distance from the camera (camera at z=-10, gameplay on z=0).
            float depth = Mathf.Abs(cam.transform.position.z);
            if (depth < 0.01f)
                depth = 10f;
            _targetX = Clamped(cam.ScreenToWorldPoint(new Vector3(screenX, 0f, depth)).x);
            _hasTarget = true;
            s_sessionLauncherX = _targetX;
        }

        void EnsureTarget()
        {
            if (_hasTarget)
                return;
            _targetX = s_sessionLauncherX ?? (_container != null ? _container.transform.position.x : 0f);
            _hasTarget = true;
        }

        void FollowTarget()
        {
            if (_preview == null)
                return;
            EnsureTarget();
            _targetX = Clamped(_targetX); // held ball size or container width may have changed
            var gm = _bootstrap != null ? _bootstrap.GameManagerData : null;
            float sharpness = gm != null ? gm.LauncherFollowSharpness : FollowSharpness;
            float x = _preview.transform.position.x;
            x = sharpness <= 0f ? _targetX : Mathf.Lerp(x, _targetX, 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime));
            PlaceAtWorldX(x);
        }

        /// <summary>Places a newly spawned held ball at the current target (no easing).</summary>
        void UpdateAim()
        {
            EnsureTarget();
            _targetX = Clamped(_targetX);
            PlaceAtWorldX(_targetX);
        }

        float Clamped(float x)
        {
            if (_preview == null || _container == null)
                return x;
            float pad = _preview.Radius + 0.05f;
            float cx = _container.transform.position.x;
            float minX = cx - _container.InnerHalfWidth + pad;
            float maxX = cx + _container.InnerHalfWidth - pad;
            return minX > maxX ? cx : Mathf.Clamp(x, minX, maxX);
        }

        /// <summary>Clamps to the current container's inner width (it can differ per level) and moves the held ball.</summary>
        void PlaceAtWorldX(float x)
        {
            if (_preview == null || _container == null)
                return;
            _preview.transform.position = new Vector3(Clamped(x), DropY, 0f);
        }

        void Drop()
        {
            if (_preview == null)
                return;

            // Reuse the held ball as the dropped ball instead of destroying it and building a new
            // GameObject in the same frame (removes one create + one destroy per drop).
            var held = _preview;
            _preview = null;
            if (!held.IsBomb)
            {
                KebammCombo.OnBallDropped(); // new combo window; a Magic Bomb drop is not a drop for combo
                KebammCombo.OnNormalDropForBombRule(_bootstrap != null ? _bootstrap.CurrentBombEveryMoves : 0); // bomb level rule
            }
            held.transform.position = new Vector3(held.transform.position.x, DropY, 0f);
            _bootstrap.ReleasePreview(held);
            _cooldownLeft = DropCooldown;
            SpawnPreview();
        }

        // Ball displaced from the launcher by the debug 'bomb' command; it comes back right after the bomb(s).
        bool _hasDisplaced;
        KebammBallColour _displacedColour;
        int _displacedTier;

        /// <summary>
        /// Debug console 'bomb N': the very next ball to leave the launcher is a Magic Bomb (the held slot becomes the bomb
        /// now), plus N-1 more queued behind it. The displaced held ball returns after the bombs. No combo, score or save change.
        /// </summary>
        public void DebugLoadBombs(int count)
        {
            if (count <= 0 || _bootstrap == null)
                return;
            if (_preview != null && _preview.IsBomb)
            {
                KebammCombo.DebugQueueBombs(count); // a bomb is already next up; add these behind it
                UpdateBombIndicator();
                return;
            }
            if (_preview != null && !_hasDisplaced)
            {
                _hasDisplaced = true;
                _displacedColour = _preview.Colour;
                _displacedTier = _preview.Tier;
            }
            KebammCombo.DebugQueueBombs(count);
            SpawnPreview(); // takes one queued bomb into the held slot
            UpdateBombIndicator();
        }

        public void SpawnPreview()
        {
            if (_preview != null)
                Destroy(_preview.gameObject);

            // The held ball shows the next colour and tier ahead of the drop; it is re-rolled after each drop.
            // A queued Magic Bomb comes right after the ball that was already held (GDD Combo Gameplay Design).
            if (KebammCombo.TryTakeBomb())
            {
                _preview = _bootstrap.SpawnBomb(new Vector3(0f, DropY, 0f), preview: true);
                Debug.Log($"[Kebamm] Magic Bomb loaded in the launcher ({KebammCombo.QueuedBombs} more queued)");
            }
            else if (_hasDisplaced)
            {
                _hasDisplaced = false;
                _preview = _bootstrap.SpawnBall(new Vector3(0f, DropY, 0f), _displacedColour, _displacedTier, preview: true);
            }
            else
            {
                var colour = _bootstrap.NextQueuedColour();
                int tier = _bootstrap.NextQueuedTier();
                _preview = _bootstrap.SpawnBall(new Vector3(0f, DropY, 0f), colour, tier, preview: true);
            }
            if (_preview != null)
                UpdateAim();
            if (_preview != null)
                _preview.gameObject.SetActive(_canDrop && !_container.IsOpen);
        }

        // ---- Magic Bomb count indicator (placeholder: black circle + "xN" beside the launcher) ----------------
        GameObject _bombIndicator;
        TextMeshPro _bombCountText;
        int _shownBombCount = -1;

        void EnsureBombIndicator()
        {
            if (_bombIndicator != null)
                return;
            float icon = Mathf.Max(0.05f, KebammCombo.Config.indicatorIconSize);
            _bombIndicator = new GameObject("MagicBombIndicator");
            _bombIndicator.transform.SetParent(transform, false);
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(_bombIndicator.transform, false);
            iconGo.transform.localScale = Vector3.one * icon;
            var sr = iconGo.AddComponent<SpriteRenderer>();
            sr.sprite = KebammVisualFactory.CircleSprite;
            sr.color = Color.black;
            sr.sortingOrder = 40;
            var textGo = new GameObject("Count");
            textGo.transform.SetParent(_bombIndicator.transform, false);
            textGo.transform.localPosition = new Vector3(icon * 0.5f + 0.05f, 0f, 0f);
            _bombCountText = textGo.AddComponent<TextMeshPro>();
            _bombCountText.alignment = TextAlignmentOptions.Left;
            _bombCountText.fontSize = 36f;
            _bombCountText.color = Color.white;
            _bombCountText.textWrappingMode = TextWrappingModes.NoWrap;
            _bombCountText.overflowMode = TextOverflowModes.Overflow;
            _bombCountText.rectTransform.pivot = new Vector2(0f, 0.5f);
            _bombCountText.rectTransform.sizeDelta = new Vector2(6f, 1.5f);
            TMP_FontAsset font = KebammDamageNumber.GetFont();
            if (font != null)
                _bombCountText.font = font;
            var mr = _bombCountText.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sortingOrder = 41;
            textGo.transform.localScale = Vector3.one * 0.07f;
            _bombIndicator.SetActive(false);
        }

        /// <summary>Shows queued Magic Bombs beside the held ball; hidden when none are queued or the launcher is hidden.</summary>
        void UpdateBombIndicator()
        {
            int count = KebammCombo.QueuedBombs;
            bool show = count > 0 && _preview != null && _preview.gameObject.activeSelf;
            if (!show)
            {
                if (_bombIndicator != null && _bombIndicator.activeSelf)
                    _bombIndicator.SetActive(false);
                return;
            }
            EnsureBombIndicator();
            if (!_bombIndicator.activeSelf)
                _bombIndicator.SetActive(true);
            if (count != _shownBombCount)
            {
                _bombCountText.text = "x" + count;
                _shownBombCount = count;
            }
            // Right of the held ball; flips to the left near the right pillar.
            float icon = Mathf.Max(0.05f, KebammCombo.Config.indicatorIconSize);
            float gap = _preview.Radius + icon * 0.5f + 0.12f;
            float width = icon + 0.6f;
            Vector3 p = _preview.transform.position;
            float right = _container.transform.position.x + _container.InnerHalfWidth;
            float x = p.x + gap + width <= right + 0.4f ? p.x + gap : p.x - gap - width + icon;
            _bombIndicator.transform.position = new Vector3(x, p.y, 0f);
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
