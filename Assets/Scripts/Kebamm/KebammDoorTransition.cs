using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Kebamm
{
    /// <summary>
    /// "Closing door" transition: two panels slide in from the left and right,
    /// meet in the middle, hold briefly, then slide back out. Built at runtime.
    /// Placeholder look (soft purple panels with a darker seam); swap art by
    /// putting sprites at Resources/Kebamm/UI/Transition/door_left.png and
    /// door_right.png (optional). Runs on unscaled time and blocks input
    /// for the whole transition.
    /// </summary>
    public class KebammDoorTransition : MonoBehaviour
    {
        public const float CloseDuration = 0.6f;
        public const float HoldDuration = 0.2f;
        public const float OpenDuration = 0.6f;

        static KebammDoorTransition _instance;

        static readonly Color PanelLeft = new Color(0.62f, 0.36f, 0.88f);
        static readonly Color PanelRight = new Color(0.58f, 0.32f, 0.85f);
        static readonly Color Seam = new Color(0.30f, 0.12f, 0.50f, 0.85f);

        RectTransform _canvasRect;
        GameObject _root;
        RectTransform _left;
        RectTransform _right;
        Sequence _sequence;

        public static bool IsPlaying => _instance != null && _instance._sequence != null && _instance._sequence.IsActive();

        /// <summary>
        /// Close the doors, call <paramref name="whileClosed"/> once fully covered,
        /// then open. Returns false if a transition is already running.
        /// </summary>
        public static bool Play(Action whileClosed, Action onOpened = null)
        {
            if (IsPlaying)
                return false;
            if (_instance == null)
            {
                var go = new GameObject("KebammDoorTransition");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<KebammDoorTransition>();
                _instance.Build();
            }
            _instance.Run(whileClosed, onOpened);
            return true;
        }

        void OnDestroy()
        {
            _sequence?.Kill();
            if (_instance == this)
                _instance = null;
        }

        void Build()
        {
            var canvasGo = new GameObject("DoorCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // above the menu (200) and HUDs
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _canvasRect = (RectTransform)canvasGo.transform;

            _root = new GameObject("Doors");
            _root.transform.SetParent(canvasGo.transform, false);
            Stretch(_root.AddComponent<RectTransform>());

            // Invisible full-screen blocker: eats clicks/taps while the doors move.
            var blocker = _root.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);
            blocker.raycastTarget = true;

            _left = BuildPanel("DoorLeft", "door_left", PanelLeft, 0f, 0.5f, true);
            _right = BuildPanel("DoorRight", "door_right", PanelRight, 0.5f, 1f, false);

            _root.SetActive(false);
        }

        RectTransform BuildPanel(string panelName, string art, Color fallback, float xMin, float xMax, bool seamOnRight)
        {
            Sprite sprite = Resources.Load<Sprite>("Kebamm/UI/Transition/" + art);
            var go = new GameObject(panelName);
            go.transform.SetParent(_root.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(xMin, 0f);
            rect.anchorMax = new Vector2(xMax, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            // Overlap the middle by a few px so no gap shows when closed.
            rect.sizeDelta = new Vector2(6f, 0f);
            var img = go.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : KebammVisualFactory.SquareSprite;
            img.color = sprite != null ? Color.white : fallback;
            img.raycastTarget = false;

            if (sprite == null)
            {
                // Placeholder seam: darker strip plus a soft shadow on the inner edge.
                AddStrip(rect, "Seam", Seam, seamOnRight, 10f);
                AddStrip(rect, "Shadow", new Color(Seam.r, Seam.g, Seam.b, 0.25f), seamOnRight, 36f);
            }
            return rect;
        }

        static void AddStrip(RectTransform parent, string stripName, Color colour, bool onRight, float width)
        {
            var go = new GameObject(stripName);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            float x = onRight ? 1f : 0f;
            rect.anchorMin = new Vector2(x, 0f);
            rect.anchorMax = new Vector2(x, 1f);
            rect.pivot = new Vector2(onRight ? 1f : 0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, 0f);
            var img = go.AddComponent<Image>();
            img.sprite = KebammVisualFactory.SquareSprite;
            img.color = colour;
            img.raycastTarget = false;
        }

        void Run(Action whileClosed, Action onOpened)
        {
            float half = Mathf.Max(1f, _canvasRect.rect.width * 0.5f) + 10f;
            _left.anchoredPosition = new Vector2(-half, 0f);
            _right.anchoredPosition = new Vector2(half, 0f);
            _root.SetActive(true);

            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _sequence.Append(_left.DOAnchorPosX(0f, CloseDuration).SetEase(Ease.InOutSine));
            _sequence.Join(_right.DOAnchorPosX(0f, CloseDuration).SetEase(Ease.InOutSine));
            _sequence.AppendCallback(() =>
            {
                try { whileClosed?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            });
            _sequence.AppendInterval(HoldDuration);
            _sequence.Append(_left.DOAnchorPosX(-half, OpenDuration).SetEase(Ease.InOutSine));
            _sequence.Join(_right.DOAnchorPosX(half, OpenDuration).SetEase(Ease.InOutSine));
            _sequence.OnComplete(() =>
            {
                _root.SetActive(false);
                _sequence = null;
                onOpened?.Invoke();
            });
            _sequence.OnKill(() =>
            {
                // Never leave input blocked if the sequence is killed early.
                if (_root != null)
                    _root.SetActive(false);
            });
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
