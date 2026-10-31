using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kebamm
{
    /// <summary>
    /// Main menu, built in code like KebammScoreHud. Art comes from Veritas's
    /// GDD > User Interface > Main menu > Assets, imported to
    /// Resources/Kebamm/UI/MainMenu. Every sprite has a placeholder fallback.
    ///
    /// Layout: the static portrait background is cover-fitted (aspect kept,
    /// edges cropped, bottom-aligned so the pedestal stays above the buttons).
    /// Monsters and bubbles live on a "Stage" that uses background pixel
    /// coordinates, so they stay on the pedestal on every screen shape.
    /// Title, level text and buttons sit on the canvas itself.
    /// </summary>
    public class KebammMainMenu : MonoBehaviour
    {
        public static KebammMainMenu Instance { get; private set; }

        const string ArtPath = "Kebamm/UI/MainMenu/";
        // Background art size in pixels; the stage uses these coordinates.
        const float StageWidth = 900f;
        const float StageHeight = 2000f;
        const int BubbleCount = 7;

        GameObject _root;
        GameObject _settingsRoot;
        RectTransform _rootRect;
        RectTransform _stage;
        RectTransform _safe;
        Rect _lastSafeArea;
        Vector2Int _lastScreen;
        TextMeshProUGUI _levelLabel;

        // Things that animate, with their rest positions so Show() can restart cleanly.
        RectTransform[] _letters;
        RectTransform[] _monsters;
        RectTransform[] _bubbles;
        RectTransform[] _bubbleShadows;
        Vector2[] _letterRest, _monsterRest, _bubbleRest;

        public bool IsVisible => _root != null && _root.activeSelf;

        static TMP_FontAsset _font;

        static readonly Color BackgroundTop = new Color(0.78f, 0.24f, 0.71f);
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
            KillTweens();
            if (Instance == this)
                Instance = null;
        }

        void LateUpdate()
        {
            if (_stage == null || _rootRect == null || !IsVisible)
                return;

            ApplySafeArea();

            // Cover fit: scale until both sides are filled, keep aspect, crop the rest.
            Rect r = _rootRect.rect;
            float scale = Mathf.Max(r.width / StageWidth, r.height / StageHeight);
            if (scale > 0f && !Mathf.Approximately(_stage.localScale.x, scale))
                _stage.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Keep the button stack inside Screen.safeArea (notches, home bar).</summary>
        void ApplySafeArea()
        {
            if (_safe == null || Screen.width <= 0 || Screen.height <= 0)
                return;
            Rect area = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (area == _lastSafeArea && screen == _lastScreen)
                return;
            _lastSafeArea = area;
            _lastScreen = screen;
            _safe.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            _safe.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            _safe.offsetMin = Vector2.zero;
            _safe.offsetMax = Vector2.zero;
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
            _rootRect = _root.AddComponent<RectTransform>();
            Stretch(_rootRect);
            // Clip the cover-fitted stage to the screen.
            _root.AddComponent<RectMask2D>();

            // Solid backing so nothing shows through, also blocks clicks to gameplay.
            var backing = CreateImage(_root.transform, "Backing", KebammVisualFactory.SquareSprite, BackgroundTop);
            Stretch(backing.rectTransform);
            backing.raycastTarget = true;

            BuildStage();
            BuildTitle();

            // Bottom stack inside the safe area, measured up from the safe bottom edge:
            // Settings centre 130 (about 60 clear below it), 50 gap, Start centre 365, Level text above Start.
            var safeGo = new GameObject("SafeArea");
            safeGo.transform.SetParent(_root.transform, false);
            _safe = safeGo.AddComponent<RectTransform>();
            Stretch(_safe);
            ApplySafeArea();

            var startRect = CreateButton(_safe, "StartButton", "START", 72f, Color.white, StartGreen,
                LoadArt("btn_start"), new Vector2(0f, 365f), 560f, new Vector2(700f, 160f), OnStart);
            var settingsRect = CreateButton(_safe, "SettingsButton", "Settings", 46f, SettingsText, SettingsFill,
                LoadArt("btn_settings"), new Vector2(0f, 130f), 400f, new Vector2(520f, 110f), OnSettings);
            AnchorBottom(startRect);
            AnchorBottom(settingsRect);

            _levelLabel = CreateLabel(_safe, "LevelText", "Level 1", 60f,
                Vector2.zero, new Vector2(800f, 80f), Color.white);
            _levelLabel.fontStyle = FontStyles.Bold;
            _levelLabel.outlineWidth = 0.25f;
            _levelLabel.outlineColor = new Color32(70, 25, 120, 255);
            AnchorBottom(_levelLabel.rectTransform);
            // Right above Start: Start top edge + 15 gap + half the label height.
            _levelLabel.rectTransform.anchoredPosition =
                new Vector2(0f, startRect.anchoredPosition.y + startRect.sizeDelta.y * 0.5f + 15f + 40f);

            BuildSettingsPlaceholder(canvasGo.transform);
            RefreshLevel();
            StartIdleTweens();
        }

        void BuildStage()
        {
            var stageGo = new GameObject("Stage");
            stageGo.transform.SetParent(_root.transform, false);
            _stage = stageGo.AddComponent<RectTransform>();
            // Bottom-aligned cover: crop comes off the top (sky rays) and the sides.
            _stage.anchorMin = _stage.anchorMax = new Vector2(0.5f, 0f);
            _stage.pivot = new Vector2(0.5f, 0f);
            _stage.anchoredPosition = Vector2.zero;
            _stage.sizeDelta = new Vector2(StageWidth, StageHeight);

            Sprite bgSprite = LoadArt("background");
            var bg = CreateImage(_stage, "Background", bgSprite != null ? bgSprite : KebammVisualFactory.SquareSprite,
                bgSprite != null ? Color.white : BackgroundTop);
            Stretch(bg.rectTransform);
            if (bgSprite == null)
            {
                var pedestal = CreateImage(_stage, "Pedestal", KebammVisualFactory.CircleSprite, Pedestal);
                Place(pedestal.rectTransform, new Vector2(0f, -425f), new Vector2(940f, 300f));
            }

            // Stage coordinates: centre origin, background pixels. The pedestal top
            // in the background art is around y = -420 (pixel row ~1420 of 2000).
            _bubbles = new RectTransform[BubbleCount];
            _bubbleShadows = new RectTransform[BubbleCount];
            _bubbleRest = new Vector2[BubbleCount];
            _monsters = new RectTransform[3];
            _monsterRest = new Vector2[3];
            // Concept: blue left and pink right on the pedestal, green hopping between and above.
            _monsters[0] = AddMonster("monster_green", "MonsterGreen", new Color(0.55f, 0.76f, 0.26f),
                new Vector2(10f, -60f), 300f);
            _monsters[1] = AddMonster("monster_blue", "MonsterBlue", new Color(0.25f, 0.45f, 0.85f),
                new Vector2(-190f, -315f), 340f);
            _monsters[2] = AddMonster("monster_pink", "MonsterPink", new Color(0.93f, 0.47f, 0.57f),
                new Vector2(190f, -320f), 300f);
            for (int i = 0; i < 3; i++)
                _monsterRest[i] = _monsters[i].anchoredPosition;

            // Tiny bubbles on the front edge of the pedestal, in front of the monsters.
            AddBubble(0, "bubble_green", new Color(0.55f, 0.85f, 0.2f), new Vector2(-330f, -480f), 60f);
            AddBubble(1, "bubble_blue", new Color(0.6f, 0.82f, 1f), new Vector2(40f, -500f), 52f);
            AddBubble(2, "bubble_orange", new Color(1f, 0.78f, 0.1f), new Vector2(350f, -470f), 56f);
            // Extra bubbles, mixed sizes, on the pedestal edges and front strip (clear of monsters).
            AddBubble(3, "bubble_blue", new Color(0.6f, 0.82f, 1f), new Vector2(-425f, -430f), 34f);
            AddBubble(4, "bubble_orange", new Color(1f, 0.78f, 0.1f), new Vector2(-170f, -530f), 40f);
            AddBubble(5, "bubble_green", new Color(0.55f, 0.85f, 0.2f), new Vector2(210f, -535f), 44f);
            AddBubble(6, "bubble_blue", new Color(0.6f, 0.82f, 1f), new Vector2(435f, -415f), 30f);
        }

        RectTransform AddMonster(string art, string goName, Color fallback, Vector2 pos, float width)
        {
            Sprite s = LoadArt(art);
            var img = CreateImage(_stage, goName, s != null ? s : KebammVisualFactory.CircleSprite, s != null ? Color.white : fallback);
            Place(img.rectTransform, pos, new Vector2(width, width * Aspect(s, 0.88f)));
            img.preserveAspect = true;
            return img.rectTransform;
        }

        void AddBubble(int index, string art, Color fallback, Vector2 groundPos, float size)
        {
            // Shadow on the pedestal surface, bubble floating a little above it.
            Sprite baseSprite = LoadArt("bubble_base");
            var shadow = CreateImage(_stage, "BubbleBase_" + art, baseSprite != null ? baseSprite : KebammVisualFactory.CircleSprite,
                // Bubble Base art is a light grey ellipse; tint it so it reads as a shadow on the purple pedestal.
                baseSprite != null ? new Color(0.22f, 0.08f, 0.38f, 0.55f) : new Color(0f, 0f, 0f, 0.25f));
            Place(shadow.rectTransform, groundPos, new Vector2(size * 1.1f, size * 1.1f * Aspect(baseSprite, 0.46f)));
            shadow.preserveAspect = true;

            Sprite s = LoadArt(art);
            var bubble = CreateImage(_stage, art, s != null ? s : KebammVisualFactory.CircleSprite, s != null ? Color.white : fallback);
            Place(bubble.rectTransform, groundPos + new Vector2(0f, size * 0.95f), new Vector2(size, size));
            bubble.preserveAspect = true;

            _bubbles[index] = bubble.rectTransform;
            _bubbleShadows[index] = shadow.rectTransform;
            _bubbleRest[index] = bubble.rectTransform.anchoredPosition;
        }

        void BuildTitle()
        {
            // Six letter images (Notion captions: K, E, B, A, M orange, M green).
            string[] art = { "title_k", "title_e", "title_b", "title_a", "title_m_orange", "title_m_green" };
            string[] glyph = { "K", "E", "B", "A", "M", "M" };
            string[] fallbackHex = { "#3B82F6", "#EC4899", "#E11D48", "#F59E0B", "#F97316", "#84CC16" };

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_root.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            Place(titleRect, new Vector2(0f, 640f), new Vector2(1000f, 240f));

            const float letterHeight = 200f;
            const float overlap = 0.86f; // letters tuck into each other slightly, like the mockups
            var sprites = new Sprite[art.Length];
            var widths = new float[art.Length];
            float total = 0f;
            for (int i = 0; i < art.Length; i++)
            {
                sprites[i] = LoadArt(art[i]);
                widths[i] = sprites[i] != null ? letterHeight / Aspect(sprites[i], 1.2f) : letterHeight * 0.75f;
                total += widths[i] * (i == art.Length - 1 ? 1f : overlap);
            }
            // Never wider than the reference screen.
            float fit = Mathf.Min(1f, 980f / total);

            _letters = new RectTransform[art.Length];
            _letterRest = new Vector2[art.Length];
            float x = -total * fit * 0.5f;
            for (int i = 0; i < art.Length; i++)
            {
                float w = widths[i] * fit;
                RectTransform rect;
                if (sprites[i] != null)
                {
                    var img = CreateImage(titleRect, "Letter_" + art[i], sprites[i], Color.white);
                    img.preserveAspect = true;
                    rect = img.rectTransform;
                }
                else
                {
                    var label = CreateLabel(titleRect, "Letter_" + art[i], $"<color={fallbackHex[i]}>{glyph[i]}</color>",
                        170f, Vector2.zero, Vector2.zero, Color.white);
                    label.fontStyle = FontStyles.Bold;
                    label.outlineWidth = 0.2f;
                    label.outlineColor = new Color32(45, 20, 70, 255);
                    rect = label.rectTransform;
                }
                Place(rect, new Vector2(x + w * 0.5f, 0f), new Vector2(w, letterHeight * fit));
                // Small alternating tilt for a playful candy look.
                rect.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 3f : -3f));
                _letters[i] = rect;
                _letterRest[i] = rect.anchoredPosition;
                x += w * overlap;
            }
        }

        // ---------- Idle animation ----------

        void StartIdleTweens()
        {
            KillTweens();
            ResetRestPoses();

            // Title: one-time gentle pop in, then a small wave bob.
            if (_letters != null)
            {
                for (int i = 0; i < _letters.Length; i++)
                {
                    var l = _letters[i];
                    l.localScale = Vector3.one * 0.6f;
                    l.DOScale(1f, 0.35f).SetDelay(0.06f * i).SetEase(Ease.OutBack).SetId(this).SetLink(l.gameObject);
                    l.DOAnchorPosY(_letterRest[i].y + 8f, 1.1f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                        .SetDelay(0.4f + 0.12f * i).SetId(this).SetLink(l.gameObject);
                }
            }

            // Monsters: gentle float, different speeds and phases.
            if (_monsters != null)
            {
                float[] amp = { 26f, 16f, 18f };
                float[] dur = { 1.5f, 1.9f, 1.7f };
                float[] phase = { 0f, 0.6f, 1.1f };
                for (int i = 0; i < _monsters.Length; i++)
                {
                    var m = _monsters[i];
                    var t = m.DOAnchorPosY(_monsterRest[i].y + amp[i], dur[i]).SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo).SetId(this).SetLink(m.gameObject);
                    t.Goto(phase[i], true);
                }
            }

            // Bubbles: smaller movement than the monsters; shadow shrinks as the bubble rises.
            if (_bubbles != null)
            {
                float[] dur = { 1.3f, 1.15f, 1.45f, 1.05f, 1.25f, 1.4f, 1.1f };
                float[] phase = { 0.3f, 0.9f, 0f, 0.5f, 1.1f, 0.15f, 0.75f };
                for (int i = 0; i < _bubbles.Length; i++)
                {
                    var b = _bubbles[i];
                    var s = _bubbleShadows[i];
                    if (b == null || s == null)
                        continue;
                    var up = b.DOAnchorPosY(_bubbleRest[i].y + 9f, dur[i]).SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo).SetId(this).SetLink(b.gameObject);
                    var shrink = s.DOScale(0.78f, dur[i]).SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo).SetId(this).SetLink(s.gameObject);
                    up.Goto(phase[i], true);
                    shrink.Goto(phase[i], true);
                }
            }
        }

        void ResetRestPoses()
        {
            if (_letters != null)
                for (int i = 0; i < _letters.Length; i++)
                {
                    _letters[i].anchoredPosition = _letterRest[i];
                    _letters[i].localScale = Vector3.one;
                }
            if (_monsters != null)
                for (int i = 0; i < _monsters.Length; i++)
                    _monsters[i].anchoredPosition = _monsterRest[i];
            if (_bubbles != null)
                for (int i = 0; i < _bubbles.Length; i++)
                {
                    if (_bubbles[i] != null) _bubbles[i].anchoredPosition = _bubbleRest[i];
                    if (_bubbleShadows[i] != null) _bubbleShadows[i].localScale = Vector3.one;
                }
        }

        void KillTweens()
        {
            DOTween.Kill(this);
        }

        void RefreshLevel()
        {
            if (_levelLabel == null)
                return;
            var bootstrap = KebammPrototypeBootstrap.Instance;
            int level = bootstrap != null ? bootstrap.CurrentLevelNumber : 1;
            _levelLabel.text = $"Level {level}";
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
                null, new Vector2(0f, 10f), 0f, new Vector2(560f, 120f), OnQuit);
            CreateButton(_settingsRoot.transform, "BackButton", "Back", 46f, Color.white, new Color(0.45f, 0.30f, 0.75f),
                null, new Vector2(0f, -170f), 0f, new Vector2(560f, 120f), OnSettingsBack);

            _settingsRoot.SetActive(false);
        }

        public void Show()
        {
            if (_root != null)
                _root.SetActive(true);
            if (_settingsRoot != null)
                _settingsRoot.SetActive(false);
            RefreshLevel();
            StartIdleTweens();
        }

        public void Hide()
        {
            KillTweens();
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

        /// <summary>
        /// Button. With art, the sprite is the whole button (Veritas's art already
        /// carries the icon and the "START" / "Settings" text), so the TMP label is
        /// kept but hidden. Without art it falls back to a flat box with the label.
        /// </summary>
        static void AnchorBottom(RectTransform rect)
        {
            Vector2 pos = rect.anchoredPosition;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = pos;
        }

        RectTransform CreateButton(Transform parent, string buttonName, string text, float fontSize, Color textColour, Color fill,
            Sprite art, Vector2 anchored, float artWidth, Vector2 fallbackSize, UnityEngine.Events.UnityAction onClick)
        {
            bool hasArt = art != null;
            Vector2 size = hasArt ? new Vector2(artWidth, artWidth * Aspect(art, 0.4f)) : fallbackSize;

            var image = CreateImage(parent, buttonName, hasArt ? art : KebammVisualFactory.SquareSprite, hasArt ? Color.white : fill);
            Place(image.rectTransform, anchored, size);
            image.preserveAspect = hasArt;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colours = button.colors;
            colours.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            button.colors = colours;
            button.onClick.AddListener(onClick);

            var label = CreateLabel(image.transform, "Label", text, fontSize, Vector2.zero, size, textColour);
            Stretch(label.rectTransform);
            label.fontStyle = FontStyles.Bold;
            label.enabled = !hasArt;
            return image.rectTransform;
        }

        static Sprite LoadArt(string file)
        {
            Sprite s = Resources.Load<Sprite>(ArtPath + file);
            if (s == null)
                Debug.LogWarning($"[Kebamm] Main menu art missing: Resources/{ArtPath}{file}.png - using placeholder.");
            return s;
        }

        /// <summary>Height / width of a sprite, or the fallback when there is none.</summary>
        static float Aspect(Sprite s, float fallback)
        {
            if (s == null || s.rect.width <= 0f)
                return fallback;
            return s.rect.height / s.rect.width;
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
