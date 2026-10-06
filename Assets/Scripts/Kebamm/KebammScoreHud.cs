using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kebamm
{
    /// <summary>
    /// Running combined score, and the end panel after a bombing run goes idle.
    /// Gameplay shows one Score (merge plus monster). The end panel stays split.
    /// </summary>
    public class KebammScoreHud : MonoBehaviour
    {
        public static KebammScoreHud Instance { get; private set; }

        TextMeshProUGUI _scoreValue;
        TextMeshProUGUI _endMergeValue;
        TextMeshProUGUI _endMonsterValue;
        TextMeshProUGUI _endFinalValue;
        GameObject _endRoot;
        CanvasGroup _endGroup;
        bool _ended;

        public bool IsEndVisible => _ended;

        static TMP_FontAsset _font;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (_endGroup != null)
                _endGroup.DOKill();
            if (Instance == this)
                Instance = null;
        }

        void OnEnable()
        {
            KebammScore.Changed += Refresh;
        }

        void OnDisable()
        {
            KebammScore.Changed -= Refresh;
        }

        public void Build()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("ScoreCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _scoreValue = CreateLabel(canvasGo.transform, "ScoreHud", "Score: 0", 40f,
                new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(960f, 64f));

            _endRoot = new GameObject("EndScreen");
            _endRoot.transform.SetParent(canvasGo.transform, false);
            var endRect = _endRoot.AddComponent<RectTransform>();
            Stretch(endRect);
            _endGroup = _endRoot.AddComponent<CanvasGroup>();
            _endGroup.alpha = 0f;
            _endGroup.blocksRaycasts = false;

            var dimGo = new GameObject("Dimmer");
            dimGo.transform.SetParent(_endRoot.transform, false);
            var dimRect = dimGo.AddComponent<RectTransform>();
            Stretch(dimRect);
            var dim = dimGo.AddComponent<Image>();
            dim.sprite = KebammVisualFactory.SquareSprite;
            dim.color = new Color(0f, 0f, 0f, 0.78f);
            dim.raycastTarget = true;

            CreateLabel(_endRoot.transform, "EndMergeLabel", "Merge Score", 36f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(900f, 56f));
            _endMergeValue = CreateLabel(_endRoot.transform, "EndMergeValue", "0", 48f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(900f, 72f));
            CreateLabel(_endRoot.transform, "EndMonsterLabel", "Monster Score", 36f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(900f, 56f));
            _endMonsterValue = CreateLabel(_endRoot.transform, "EndMonsterValue", "0", 48f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(900f, 72f));
            CreateLabel(_endRoot.transform, "EndFinalLabel", "Final Score", 36f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(900f, 56f));
            _endFinalValue = CreateLabel(_endRoot.transform, "EndFinalValue", "0", 96f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(900f, 130f));

            var buttonGo = new GameObject("NextLevel");
            buttonGo.transform.SetParent(_endRoot.transform, false);
            var buttonRect = buttonGo.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, -320f);
            buttonRect.sizeDelta = new Vector2(520f, 120f);
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.sprite = KebammVisualFactory.SquareSprite;
            buttonImage.color = new Color(0.22f, 0.48f, 0.82f, 1f);
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(OnNextLevel);

            var buttonLabel = CreateLabel(buttonGo.transform, "Label", "Next Level", 42f,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 120f));
            Stretch(buttonLabel.rectTransform);
            buttonLabel.raycastTarget = false;

            _endRoot.SetActive(false);
            Refresh();
        }

        public void Refresh()
        {
            if (_scoreValue != null)
                _scoreValue.text = "Score: " + KebammScore.FinalScore;
            if (_endMergeValue != null)
                _endMergeValue.text = KebammScore.MergeScore.ToString();
            if (_endMonsterValue != null)
                _endMonsterValue.text = KebammScore.MonsterScore.ToString();
            if (_endFinalValue != null)
                _endFinalValue.text = KebammScore.FinalScore.ToString();
        }

        public void ShowEnd()
        {
            if (_ended || _endRoot == null)
                return;

            _ended = true;
            KebammScore.Lock();
            Refresh();
            _endRoot.SetActive(true);
            if (_endGroup != null)
            {
                _endGroup.DOKill();
                // Visible even if DOTween never ticks. Do not start the fade from alpha 0.
                _endGroup.alpha = 1f;
                _endGroup.blocksRaycasts = true;
                _endGroup.DOFade(1f, 0.28f).SetEase(Ease.OutQuad).SetLink(_endRoot)
                    .OnKill(() =>
                    {
                        if (_ended && _endGroup != null)
                            _endGroup.alpha = 1f;
                    });
            }
            if (KebammPrototypeBootstrap.Instance != null)
                KebammPrototypeBootstrap.Instance.SetDroppingEnabled(false);
        }

        public void HideEnd()
        {
            _ended = false;
            if (_endGroup != null)
            {
                _endGroup.DOKill();
                _endGroup.alpha = 0f;
                _endGroup.blocksRaycasts = false;
            }

            if (_endRoot != null)
                _endRoot.SetActive(false);
        }

        void OnNextLevel()
        {
            if (KebammPrototypeBootstrap.Instance != null)
                KebammPrototypeBootstrap.Instance.ResetSession();
        }

        void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        TextMeshProUGUI CreateLabel(Transform parent, string labelName, string text, float fontSize, Vector2 anchor, Vector2 anchored, Vector2 size)
        {
            var go = new GameObject(labelName);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchored;
            rect.sizeDelta = size;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;

            TMP_FontAsset font = GetFont();
            if (font != null)
                tmp.font = font;
            return tmp;
        }

        static TMP_FontAsset GetFont()
        {
            if (_font != null)
                return _font;

            Font builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtin == null)
                builtin = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (builtin == null)
            {
                Debug.LogWarning("[Kebamm] No built-in font for the score HUD.");
                return null;
            }

            _font = TMP_FontAsset.CreateFontAsset(builtin);
            return _font;
        }
    }
}