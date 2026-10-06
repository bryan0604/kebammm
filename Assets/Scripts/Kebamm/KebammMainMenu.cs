using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kebamm
{
    /// <summary>
    /// Main menu, built in code like KebammScoreHud. Layout follows White Tea's
    /// concept (GDD > User Interface > Main menu): title at the top, three
    /// monsters on a pedestal, a big START button and a smaller Settings button.
    /// Placeholder shapes and colours only; no concept art is imported.
    /// </summary>
    public class KebammMainMenu : MonoBehaviour
    {
        public static KebammMainMenu Instance { get; private set; }

        GameObject _root;
        GameObject _settingsRoot;

        public bool IsVisible => _root != null && _root.activeSelf;

        static TMP_FontAsset _font;

        static readonly Color BackgroundTop = new Color(0.78f, 0.24f, 0.71f);
        static readonly Color BackgroundBottom = new Color(0.36f, 0.13f, 0.65f);
        static readonly Color Pedestal = new Color(0.56f, 0.27f, 0.86f);
        static readonly Color StartGreen = new Color(0.36f, 0.75f, 0.18f);
        static readonly Color SettingsFill = new Color(0.96f, 0.94f, 1f);
        static readonly Color SettingsText = new Color(0.30f, 0.15f, 0.55f);

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Build()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("MainMenuCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _root = new GameObject("MainMenu");
            _root.transform.SetParent(canvasGo.transform, false);
            Stretch(_root.AddComponent<RectTransform>());

            // Background: magenta top, purple bottom band, pedestal ellipse.
            var bg = CreateImage(_root.transform, "Background", KebammVisualFactory.SquareSprite, BackgroundTop);
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;

            var band = CreateImage(_root.transform, "BottomBand", KebammVisualFactory.SquareSprite, BackgroundBottom);
            band.rectTransform.anchorMin = new Vector2(0f, 0f);
            band.rectTransform.anchorMax = new Vector2(1f, 0f);
            band.rectTransform.pivot = new Vector2(0.5f, 0f);
            band.rectTransform.anchoredPosition = Vector2.zero;
            band.rectTransform.sizeDelta = new Vector2(0f, 640f);

            var pedestal = CreateImage(_root.transform, "Pedestal", KebammVisualFactory.CircleSprite, Pedestal);
            Place(pedestal.rectTransform, new Vector2(0f, -330f), new Vector2(1180f, 230f));

            // Placeholder monsters (concept: blue left, pink right, green jumping between).
            var blue = CreateImage(_root.transform, "MonsterBlue", KebammVisualFactory.CircleSprite, new Color(0.25f, 0.45f, 0.85f));
            Place(blue.rectTransform, new Vector2(-255f, -170f), new Vector2(430f, 380f));
            var pink = CreateImage(_root.transform, "MonsterPink", KebammVisualFactory.CircleSprite, new Color(0.93f, 0.47f, 0.57f));
            Place(pink.rectTransform, new Vector2(215f, -175f), new Vector2(370f, 350f));
            var green = CreateImage(_root.transform, "MonsterGreen", KebammVisualFactory.CircleSprite, new Color(0.55f, 0.76f, 0.26f));
            Place(green.rectTransform, new Vector2(40f, 135f), new Vector2(360f, 340f));

            // Title: one colour per letter, like concept option A (gummy candy).
            var title = CreateLabel(_root.transform, "Title",
                "<color=#3B82F6>K</color><color=#EC4899>E</color><color=#22C55E>B</color><color=#F59E0B>A</color><color=#14B8A6>M</color><color=#EF4444>M</color>",
                170f, new Vector2(0f, 600f), new Vector2(1000f, 220f), Color.white);
            title.fontStyle = FontStyles.Bold;
            title.outlineWidth = 0.2f;
            title.outlineColor = new Color32(45, 20, 70, 255);

            CreateButton(_root.transform, "StartButton", "START", 84f, Color.white, StartGreen,
                new Vector2(0f, -570f), new Vector2(820f, 180f), OnStart);
            CreateButton(_root.transform, "SettingsButton", "Settings", 50f, SettingsText, SettingsFill,
                new Vector2(0f, -780f), new Vector2(600f, 120f), OnSettings);

            BuildSettingsPlaceholder(canvasGo.transform);
        }

        // PLACEHOLDER: there is no Settings screen yet. This panel only offers Quit and Back.
        void BuildSettingsPlaceholder(Transform parent)
        {
            _settingsRoot = new GameObject("SettingsPlaceholder");
            _settingsRoot.transform.SetParent(parent, false);
            Stretch(_settingsRoot.AddComponent<RectTransform>());

            var dim = CreateImage(_settingsRoot.transform, "Dimmer", KebammVisualFactory.SquareSprite, new Color(0f, 0f, 0f, 0.7f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            var panel = CreateImage(_settingsRoot.transform, "Panel", KebammVisualFactory.SquareSprite, SettingsFill);
            Place(panel.rectTransform, new Vector2(0f, 0f), new Vector2(820f, 760f));

            CreateLabel(_settingsRoot.transform, "Title", "Settings", 64f,
                new Vector2(0f, 270f), new Vector2(760f, 90f), SettingsText).fontStyle = FontStyles.Bold;
            CreateLabel(_settingsRoot.transform, "Note", "Coming soon (placeholder)", 38f,
                new Vector2(0f, 170f), new Vector2(760f, 60f), SettingsText);

            CreateButton(_settingsRoot.transform, "QuitButton", "Quit Game", 46f, Color.white, new Color(0.85f, 0.27f, 0.32f),
                new Vector2(0f, 10f), new Vector2(560f, 120f), OnQuit);
            CreateButton(_settingsRoot.transform, "BackButton", "Back", 46f, Color.white, new Color(0.45f, 0.30f, 0.75f),
                new Vector2(0f, -170f), new Vector2(560f, 120f), OnSettingsBack);

            _settingsRoot.SetActive(false);
        }

        public void Show()
        {
            if (_root != null)
                _root.SetActive(true);
            if (_settingsRoot != null)
                _settingsRoot.SetActive(false);
        }

        public void Hide()
        {
            if (_settingsRoot != null)
                _settingsRoot.SetActive(false);
            if (_root != null)
                _root.SetActive(false);
        }

        void OnStart()
        {
            if (KebammPrototypeBootstrap.Instance != null)
                KebammPrototypeBootstrap.Instance.StartGameFromMenu();
            else
                Hide();
        }

        void OnSettings()
        {
            Debug.Log("[Kebamm] Settings pressed - placeholder panel (no Settings screen yet).");
            if (_settingsRoot != null)
                _settingsRoot.SetActive(true);
        }

        void OnSettingsBack()
        {
            if (_settingsRoot != null)
                _settingsRoot.SetActive(false);
        }

        void OnQuit()
        {
            Debug.Log("[Kebamm] Quit pressed.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            Application.Quit();
        }

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        void CreateButton(Transform parent, string buttonName, string text, float fontSize, Color textColour, Color fill,
            Vector2 anchored, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var image = CreateImage(parent, buttonName, KebammVisualFactory.SquareSprite, fill);
            Place(image.rectTransform, anchored, size);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var label = CreateLabel(image.transform, "Label", text, fontSize, Vector2.zero, size, textColour);
            Stretch(label.rectTransform);
            label.fontStyle = FontStyles.Bold;
        }

        static Image CreateImage(Transform parent, string imageName, Sprite sprite, Color colour)
        {
            var go = new GameObject(imageName);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        static void Place(RectTransform rect, Vector2 anchored, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchored;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        TextMeshProUGUI CreateLabel(Transform parent, string labelName, string text, float fontSize, Vector2 anchored, Vector2 size, Color colour)
        {
            var go = new GameObject(labelName);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            Place(rect, anchored, size);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.richText = true;
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.color = colour;
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
                Debug.LogWarning("[Kebamm] No built-in font for the main menu.");
                return null;
            }

            _font = TMP_FontAsset.CreateFontAsset(builtin);
            return _font;
        }
    }
}