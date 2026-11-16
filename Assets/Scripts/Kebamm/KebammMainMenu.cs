using System.Collections;
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
    /// Title and buttons sit on the canvas itself. The start button shows the current level.
    /// </summary>
    public class KebammMainMenu : MonoBehaviour
    {
        public static KebammMainMenu Instance { get; private set; }

        const string ArtPath = "Kebamm/UI/MainMenu/";
        // Background art size in pixels; the stage uses these coordinates.
        const float StageWidth = 900f;
        const float StageHeight = 2000f;
        const int BubbleCount = 7;
        const float StartButtonWidth = 700f;
        // Setting Button V3 art (1308x296, label baked in) is drawn at the Start art's scale.
        const float SettingsButtonWidth = 704f;
        const float StartButtonPadX = 100f;
        const float StartIconSize = 72f;
        const float StartIconTextSpacing = 18f;
        // Empty_Button art: green pill body is rows 0-247 of 288, drop shadow below.
        // Shift content up so it centres on the pill body, not the whole image.
        const float StartArtBodyCentreOffset = 20f / 288f;
        // Nudge the icon + level text group sideways (negative = left).
        const float StartContentOffsetX = -15f;
        const float StartLevelFontMin = 28f;
        const float StartLevelFontMax = 84f;
        const string LilitaFontPath = "Kebamm/Fonts/LilitaOne-Regular";
        const string StartIconPath = "icon_play";

        GameObject _root;
        GameObject _settingsRoot;
        RectTransform _rootRect;
        RectTransform _stage;
        RectTransform _safe;
        Rect _lastSafeArea;
        Vector2Int _lastScreen;
        TextMeshProUGUI _levelLabel;
        LayoutElement _levelLabelLayout;
        RectTransform _startButtonRect;
        RectTransform _startContent;
        [SerializeField] Sprite startButtonIcon;

        // Things that animate, with their rest positions so Show() can restart cleanly.
        RectTransform[] _letters;
        RectTransform[] _monsters;
        RectTransform[] _bubbles;
        RectTransform[] _bubbleShadows;
        Vector2[] _letterRest, _monsterRest, _bubbleRest;
        RectTransform _titleRect;
        RectTransform[] _letterShines; // light strip per letter, clipped to the letter shape (null for text fallbacks)
        float _titleHalfWidth;
        const float TitleShineTilt = -20f;

        public bool IsVisible => _root != null && _root.activeSelf;

        TextMeshProUGUI _dataLabel;
        GameObject _updateRoot;
        GameObject _deleteConfirmRoot;
        UnityEngine.UI.Button _soundButton;
        UnityEngine.UI.Button _musicButton;
        TextMeshProUGUI _playerName;
        TextMeshProUGUI _playerStars;
        TextMeshProUGUI _playerCoins;
        int _shownCoins = -1;
        string _shownName;
        int _shownStars = -1;
        TextMeshProUGUI _updateText;
        KebammGoalTracking _goal;
        UnityEngine.UI.Button _goalButton;
        // Goal Tracking auto-shows at most once per app run (the first main menu), and then only once per local day.
        static bool _goalSessionChecked;
        static TMP_FontAsset _font;
        static bool _fontTried;
        static TMP_FontAsset _lilitaFont;
        static bool _lilitaTried;

        static readonly Color BackgroundTop = new Color(0.78f, 0.24f, 0.71f);
        static readonly Color Pedestal = new Color(0.56f, 0.27f, 0.86f);
        static readonly Color StartGreen = new Color(0.36f, 0.75f, 0.18f);
        static readonly Color SettingsFill = new Color(0.96f, 0.94f, 1f);
        static readonly Color SettingsText = new Color(0.30f, 0.15f, 0.55f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _goalSessionChecked = false;
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            KebammAudio.Changed -= RefreshToggles;
            KillTweens();
            if (Instance == this)
                Instance = null;
        }

        void LateUpdate()
        {
            if (_stage == null || _rootRect == null || !IsVisible)
                return;

            ApplySafeArea();
            RefreshPlayerPanel(); // username arrives later from the online check

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
            // Start centre 365 (level text on the button). Settings centre 214, so about 20 px
            // between the visible Start pill (above its drop shadow) and the Settings art.
            var safeGo = new GameObject("SafeArea");
            safeGo.transform.SetParent(_root.transform, false);
            _safe = safeGo.AddComponent<RectTransform>();
            Stretch(_safe);
            ApplySafeArea();

            var startRect = BuildStartButton(_safe, new Vector2(0f, 365f), OnStart);
            var settingsRect = CreateButton(_safe, "SettingsButton", "Settings", 46f, SettingsText, SettingsFill,
                LoadArt("Setting_Button_V3"), new Vector2(0f, 214f), SettingsButtonWidth, new Vector2(520f, 110f), OnSettings);
            AnchorBottom(startRect);
            AnchorBottom(settingsRect);

            // Data version / time / source (online, cached, built-in), bottom-right corner.
            _dataLabel = CreateLabel(_safe, "DataVersionText", KebammRemoteData.StatusText, 26f,
                Vector2.zero, new Vector2(760f, 40f), new Color(1f, 1f, 1f, 0.8f));
            _dataLabel.alignment = TextAlignmentOptions.BottomRight;
            RectTransform dataRect = _dataLabel.rectTransform;
            dataRect.anchorMin = dataRect.anchorMax = dataRect.pivot = new Vector2(1f, 0f);
            dataRect.anchoredPosition = new Vector2(-20f, 16f);

            BuildPlayerPanel();
            BuildGoalButton();
            BuildGoalPopup(canvasGo.transform);
            BuildSettingsPlaceholder(canvasGo.transform);
            BuildUpdatePrompt(canvasGo.transform);
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
            // Six letter images (Notion User Interface > Main menu: "V2 - Title Alphabet" K, E, B, A, M orange, M green). v1 title_* files kept.
            string[] art = { "title_v2_k", "title_v2_e", "title_v2_b", "title_v2_a", "title_v2_m_orange", "title_v2_m_green" };
            string[] glyph = { "K", "E", "B", "A", "M", "M" };
            string[] fallbackHex = { "#3B82F6", "#EC4899", "#E11D48", "#F59E0B", "#F97316", "#84CC16" };

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_root.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            Place(titleRect, new Vector2(0f, 640f), new Vector2(1000f, 240f));
            _titleRect = titleRect;

            KebammGameManagerData gm = MenuGameManager();
            float letterHeight = gm != null ? gm.TitleLetterHeight : 200f; // Game Manager "title letter height"
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
            _letterShines = new RectTransform[art.Length];
            _titleHalfWidth = total * fit * 0.5f;
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
                    // Mask the letter so the title shine only lights the letter itself.
                    img.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                    var shine = CreateImage(rect, "Shine", TitleShineSprite, new Color(1f, 1f, 1f, 0f));
                    _letterShines[i] = shine.rectTransform;
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

            // Title: gentle pop in, then a looping grow pulse plus a small wave bob (Game Manager "title breath duration").
            if (_letters != null)
            {
                KebammGameManagerData gm = MenuGameManager();
                float breath = gm != null ? gm.TitleBreathDuration : 1.1f;
                float pulseScale = gm != null ? gm.TitlePulseScale : 1.1f;
                float pulseDur = gm != null ? gm.TitlePulseDuration : 0.35f;
                float pulseInterval = gm != null ? gm.TitlePulseInterval : 5f;
                for (int i = 0; i < _letters.Length; i++)
                {
                    var l = _letters[i];
                    l.localScale = Vector3.one * 0.6f;
                    // Pop in once, then (on complete) loop forever: 1 -> pulse scale -> 1, then wait the interval
                    // (Game Manager title pulse scale / duration / interval). Only these tweens touch the letter's scale.
                    l.DOScale(1f, 0.35f).SetDelay(0.06f * i).SetEase(Ease.OutBack).SetId(this).SetLink(l.gameObject)
                        .OnComplete(() => DOTween.Sequence()
                            .Append(l.DOScale(pulseScale, pulseDur).SetEase(Ease.InOutSine))
                            .Append(l.DOScale(1f, pulseDur).SetEase(Ease.InOutSine))
                            .AppendInterval(pulseInterval)
                            .SetLoops(-1, LoopType.Restart).SetId(this).SetLink(l.gameObject));
                    l.DOAnchorPosY(_letterRest[i].y + 8f, breath).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                        .SetDelay(0.4f + 0.12f * i).SetId(this).SetLink(l.gameObject);
                }
            }

            StartTitleShine();

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

        // ---------- Title shine ----------

        static Sprite _titleShineSprite;

        /// <summary>White strip, transparent at both sides and bright in the middle (soft falloff across its width).</summary>
        static Sprite TitleShineSprite
        {
            get
            {
                if (_titleShineSprite != null)
                    return _titleShineSprite;
                const int w = 32;
                var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int x = 0; x < w; x++)
                {
                    float a = Mathf.Sin(Mathf.PI * (x + 0.5f) / w);
                    tex.SetPixel(x, 0, new Color(1f, 1f, 1f, a * a));
                }
                tex.Apply();
                _titleShineSprite = Sprite.Create(tex, new Rect(0, 0, w, 1), new Vector2(0.5f, 0.5f), 100f);
                return _titleShineSprite;
            }
        }

        /// <summary>
        /// One tilted light strip sweeps left to right across the whole title, then waits and repeats (VFX table
        /// title_shine_*). Each letter carries its own strip, masked to the letter, placed at the same title-space x
        /// so it reads as one sweep while the letters bob and pulse.
        /// </summary>
        void StartTitleShine()
        {
            if (_letterShines == null || _titleRect == null)
                return;
            KebammGameManagerData gm = MenuGameManager();
            float duration = gm != null ? gm.TitleShineDuration : 0.8f;
            float interval = gm != null ? gm.TitleShineInterval : 3f;
            float alpha = gm != null ? gm.TitleShineAlpha : 0.7f;
            float thickness = gm != null ? gm.TitleShineThickness : 60f;
            for (int i = 0; i < _letterShines.Length; i++)
            {
                var s = _letterShines[i];
                if (s == null)
                    continue;
                s.sizeDelta = new Vector2(thickness, _letters[i].rect.height * 1.8f);
                s.GetComponent<Image>().color = new Color(1f, 1f, 1f, alpha);
            }
            float half = _titleHalfWidth + thickness * 2f;
            PlaceTitleShine(-half);
            if (alpha <= 0f)
                return;
            DOTween.Sequence()
                .AppendInterval(1f)
                .Append(DOVirtual.Float(-half, half, duration, PlaceTitleShine).SetEase(Ease.InOutSine))
                .AppendInterval(interval)
                .SetLoops(-1, LoopType.Restart).SetId(this).SetLink(_titleRect.gameObject);
        }

        void PlaceTitleShine(float titleX)
        {
            if (_letterShines == null || _titleRect == null)
                return;
            Vector3 world = _titleRect.TransformPoint(new Vector3(titleX, 0f, 0f));
            for (int i = 0; i < _letterShines.Length; i++)
            {
                var s = _letterShines[i];
                if (s == null)
                    continue;
                Vector3 local = _letters[i].InverseTransformPoint(world);
                s.localPosition = new Vector3(local.x, 0f, 0f);
                s.localRotation = Quaternion.Euler(0f, 0f, TitleShineTilt - _letters[i].localEulerAngles.z);
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

        static KebammGameManagerData MenuGameManager()
        {
            var boot = KebammPrototypeBootstrap.Instance;
            return boot != null ? boot.GameManagerData : null;
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
            _levelLabel.text = $"LEVEL {level}";
            FitStartButtonContent();
        }

        // PLACEHOLDER Player Information Panel (Notion: Main Menu > Player Information Panel), top-left, not clickable.
        // Picture = placeholder circle until art; stars = total best stars across all levels.
        // Player Information Panel as one horizontal bar along the top, so it stays clear of the game title
        // (Notion ticket: Game UI - Player Information Panel). Row: picture, then name, stars and coins spread
        // across the rest of the bar (the name gets a little more room than each resource).
        // The Goal button sits right below the profile picture.
        const float TopBarMargin = 30f;
        const float TopBarHeight = 110f;
        const float PictureSize = 84f;
        const float PictureCentreX = 62f;      // from the bar's left edge
        const float RowLeft = 120f;            // name / resources area starts right of the picture
        const float RowRight = 20f;
        const float NameShare = 1.3f;          // name : stars : coins = 1.3 : 1 : 1
        const float GoalButtonWidth = 170f;
        const float GoalButtonHeight = 64f;
        const float TopBarGap = 14f;

        void BuildPlayerPanel()
        {
            var panel = KebammPlaceholderUi.Box(_safe, "PlayerInfoPanel", new Color(0f, 0f, 0f, 0.45f), false);
            RectTransform pr = panel.rectTransform;
            pr.anchorMin = new Vector2(0f, 1f);
            pr.anchorMax = new Vector2(1f, 1f);
            pr.pivot = new Vector2(0f, 1f);
            pr.sizeDelta = new Vector2(0f, TopBarHeight);
            pr.anchoredPosition = new Vector2(0f, -TopBarMargin);
            pr.offsetMin = new Vector2(TopBarMargin, pr.offsetMin.y);
            pr.offsetMax = new Vector2(-TopBarMargin, pr.offsetMax.y);

            var picture = KebammPlaceholderUi.Box(panel.transform, "PlayerPicture", new Color(0.85f, 0.80f, 0.95f), false);
            picture.sprite = KebammVisualFactory.CircleSprite;
            picture.preserveAspect = true;
            RectTransform pic = picture.rectTransform;
            pic.anchorMin = pic.anchorMax = new Vector2(0f, 0.5f);
            pic.pivot = new Vector2(0.5f, 0.5f);
            pic.anchoredPosition = new Vector2(PictureCentreX, 0f);
            pic.sizeDelta = new Vector2(PictureSize, PictureSize);

            // Row area right of the picture, split into name / stars / coins sections.
            var row = KebammPlaceholderUi.Rect(panel.transform, "InfoRow");
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(1f, 1f);
            row.offsetMin = new Vector2(RowLeft, 0f);
            row.offsetMax = new Vector2(-RowRight, 0f);
            float total = NameShare + 2f;
            float nameEnd = NameShare / total;
            float starsEnd = (NameShare + 1f) / total;

            RectTransform nameSection = Section(row, "NameSection", 0f, nameEnd);
            _playerName = KebammPlaceholderUi.Label(nameSection, "Username", "", 38f, Vector2.zero, Vector2.zero, Color.white);
            _playerName.alignment = TextAlignmentOptions.Left;
            _playerName.fontStyle = FontStyles.Bold;
            _playerName.enableAutoSizing = true;
            _playerName.fontSizeMin = 22f;
            _playerName.fontSizeMax = 38f;
            FillSection(_playerName.rectTransform, 0f, 12f);

            Color resourceText = new Color(1f, 0.86f, 0.3f);
            _playerStars = BuildResource(Section(row, "StarsSection", nameEnd, starsEnd), "Star", "Kebamm/UI/star_icon", resourceText);
            _playerCoins = BuildResource(Section(row, "CoinsSection", starsEnd, 1f), "Coin", "Kebamm/UI/coin_icon", resourceText);
            RefreshPlayerPanel();
        }

        /// <summary>A full-height slice of the row between two horizontal fractions.</summary>
        static RectTransform Section(RectTransform row, string name, float from, float to)
        {
            RectTransform r = KebammPlaceholderUi.Rect(row, name);
            r.anchorMin = new Vector2(from, 0f);
            r.anchorMax = new Vector2(to, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return r;
        }

        static void FillSection(RectTransform rect, float left, float right)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, 0f);
            rect.offsetMax = new Vector2(-right, 0f);
        }

        /// <summary>Icon on the left of the section, value right next to it.</summary>
        static TextMeshProUGUI BuildResource(RectTransform section, string name, string iconPath, Color colour)
        {
            const float iconSize = 44f;
            var icon = KebammPlaceholderUi.Box(section, name + "Icon", Color.white, false);
            icon.sprite = Resources.Load<Sprite>(iconPath);
            icon.preserveAspect = true;
            RectTransform ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0f, 0.5f);
            ir.anchoredPosition = Vector2.zero;
            ir.sizeDelta = new Vector2(iconSize, iconSize);
            TextMeshProUGUI label = KebammPlaceholderUi.Label(section, name + "s", "", 34f, Vector2.zero, Vector2.zero, colour);
            label.alignment = TextAlignmentOptions.Left;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20f;
            label.fontSizeMax = 34f;
            FillSection(label.rectTransform, iconSize + 8f, 4f);
            return label;
        }

        // Goal button right below the profile picture (Notion: Game Goal - Your Performance vs All).
        // Hidden until level 1 is completed. Opens Goal Tracking and replays the transition.
        void BuildGoalButton()
        {
            _goalButton = KebammPlaceholderUi.MakeButton(_safe, "GoalButton", "Goal", new Color(0.95f, 0.60f, 0.10f),
                Vector2.zero, new Vector2(GoalButtonWidth, GoalButtonHeight), OnGoalPressed, 38f);
            var rect = (RectTransform)_goalButton.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(TopBarMargin + PictureCentreX, -(TopBarMargin + TopBarHeight + TopBarGap));
            TextMeshProUGUI label = _goalButton.GetComponentInChildren<TextMeshProUGUI>();
            TMP_FontAsset lilita = GetLilitaFont();
            if (label != null && lilita != null)
                label.font = lilita;
            _goalButton.gameObject.SetActive(false);
        }
        void BuildGoalPopup(Transform parent)
        {
            var go = new GameObject("GoalTracking", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            KebammPlaceholderUi.Stretch((RectTransform)go.transform); // full-screen so the popup centres and the dimmer covers everything
            _goal = go.AddComponent<KebammGoalTracking>();
            _goal.Build(go.transform);
        }

        void OnGoalPressed()
        {
            if (_goal != null)
                _goal.Open(false);
        }

        /// <summary>Goal button visibility + the once-per-day auto show. Called from Show().</summary>
        void RefreshGoal()
        {
            if (_goal == null)
                return;
            bool completed = KebammGoalTracking.HasCompletedLevel1();
            if (_goalButton != null)
                _goalButton.gameObject.SetActive(completed);
            if (_goal.IsOpen)
            {
                if (!completed)
                    _goal.CloseImmediate(); // save deleted / progress reset while it was open
                return;
            }
            if (_goalSessionChecked)
                return;
            _goalSessionChecked = true;
            if (completed && !KebammGoalTracking.ShownToday)
            {
                if (_goalAutoOpen != null)
                    StopCoroutine(_goalAutoOpen);
                _goalAutoOpen = StartCoroutine(AutoOpenGoalWhenLoaded());
            }
        }

        // Goal Tracking auto show waits for app loading to finish. While the app is still loading, frames are long
        // (e.g. 1-2 s). The popup runs on unscaled time, so the launch would finish inside those frames before the
        // player sees anything. Wait for a few smooth frames in a row, then a short beat, then open.
        const int GoalSmoothFramesNeeded = 5;
        const float GoalSmoothFrameMax = 1f / 20f;   // a frame slower than this counts as still loading
        const float GoalSettleDelay = 0.35f;         // beat after loading so the menu is on screen first
        const float GoalMaxWait = 6f;                // safety: open anyway after this long
        Coroutine _goalAutoOpen;

        IEnumerator AutoOpenGoalWhenLoaded()
        {
            float waited = 0f;
            int smooth = 0;
            while (smooth < GoalSmoothFramesNeeded && waited < GoalMaxWait)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime;
                waited += Mathf.Min(dt, 0.5f);
                smooth = dt <= GoalSmoothFrameMax ? smooth + 1 : 0;
            }
            yield return new WaitForSecondsRealtime(GoalSettleDelay);
            _goalAutoOpen = null;
            bool menuUp = _root != null && _root.activeInHierarchy;
            if (_goal != null && menuUp && !_goal.IsOpen && !KebammDoorTransition.IsPlaying && KebammGoalTracking.HasCompletedLevel1())
            {
                Debug.Log($"[Kebamm] Goal Tracking auto show after loading ({waited:0.00}s wait)");
                _goal.Open(true);
            }
        }

        void RefreshPlayerPanel()
        {
            if (_playerName == null)
                return;
            string name = KebammSaveSystem.Data.player.username;
            if (string.IsNullOrEmpty(name))
                name = "Player...";
            int stars = KebammSaveSystem.TotalStars();
            int coins = KebammSaveSystem.Coins;
            if (name == _shownName && stars == _shownStars && coins == _shownCoins)
                return;
            _shownName = name;
            _shownStars = stars;
            _shownCoins = coins;
            _playerName.text = name;
            _playerStars.text = stars.ToString();
            _playerCoins.text = coins.ToString();
        }

        // PLACEHOLDER Settings popup (Notion: Main Menu > Setting Screen Popup): Sound, Music, Delete Save,
        // Quit Game (unchanged), Terms and Conditions (does nothing yet), Close.
        void BuildSettingsPlaceholder(Transform parent)
        {
            _settingsRoot = KebammPlaceholderUi.Popup(parent, "SettingsPopup", new Vector2(820f, 1240f), "Settings", 520f);
            Transform t = _settingsRoot.transform;
            Vector2 size = new Vector2(560f, 110f);
            _soundButton = KebammPlaceholderUi.MakeButton(t, "SoundToggle", "Sound", KebammPlaceholderUi.Green, new Vector2(0f, 360f), size,
                () => KebammAudio.SetSound(!KebammAudio.SoundOn, "settings"));
            _musicButton = KebammPlaceholderUi.MakeButton(t, "MusicToggle", "Music", KebammPlaceholderUi.Green, new Vector2(0f, 220f), size,
                () => KebammAudio.SetMusic(!KebammAudio.MusicOn, "settings"));
            KebammPlaceholderUi.MakeButton(t, "DeleteSaveButton", "Delete Save", KebammPlaceholderUi.Red, new Vector2(0f, 60f), size, OnDeleteSave);
            KebammPlaceholderUi.MakeButton(t, "QuitButton", "Quit Game", KebammPlaceholderUi.Red, new Vector2(0f, -80f), size, OnQuit);

            // Terms and Conditions: link-style text, intentionally does nothing yet (to be researched before publish).
            var terms = KebammPlaceholderUi.MakeButton(t, "TermsLink", "<u>Terms and Conditions</u>", new Color(1f, 1f, 1f, 0f),
                new Vector2(0f, -220f), new Vector2(560f, 80f), OnTerms, 36f);
            var termsLabel = terms.GetComponentInChildren<TextMeshProUGUI>();
            termsLabel.color = new Color(0.20f, 0.35f, 0.85f);
            termsLabel.fontStyle = FontStyles.Normal;

            KebammPlaceholderUi.MakeButton(t, "CloseButton", "Close", KebammPlaceholderUi.Purple, new Vector2(0f, -360f), size, OnSettingsBack);

            // Delete Save confirm.
            _deleteConfirmRoot = KebammPlaceholderUi.Popup(parent, "DeleteSaveConfirm", new Vector2(820f, 660f), "Are you sure?", 220f);
            Transform c = _deleteConfirmRoot.transform;
            KebammPlaceholderUi.Label(c, "Note", "Delete your save? All progress and stars are lost.", 32f,
                new Vector2(0f, 120f), new Vector2(760f, 60f), KebammPlaceholderUi.PanelText);
            KebammPlaceholderUi.MakeButton(c, "YesButton", "Yes, delete", KebammPlaceholderUi.Red, new Vector2(0f, -30f), new Vector2(560f, 120f), OnDeleteSaveConfirmed);
            KebammPlaceholderUi.MakeButton(c, "NoButton", "No", KebammPlaceholderUi.Purple, new Vector2(0f, -190f), new Vector2(560f, 120f), OnDeleteSaveCancelled);

            KebammAudio.Changed += RefreshToggles;
            RefreshToggles();
        }

        void RefreshToggles()
        {
            KebammPlaceholderUi.SetToggle(_soundButton, "Sound", KebammAudio.SoundOn);
            KebammPlaceholderUi.SetToggle(_musicButton, "Music", KebammAudio.MusicOn);
        }

        void OnDeleteSave()
        {
            Debug.Log("[Kebamm] Settings Delete Save pressed - asking to confirm.");
            _deleteConfirmRoot.SetActive(true);
        }

        void OnDeleteSaveCancelled()
        {
            Debug.Log("[Kebamm] Settings Delete Save cancelled.");
            _deleteConfirmRoot.SetActive(false);
        }

        void OnDeleteSaveConfirmed()
        {
            Debug.Log("[Kebamm] Settings Delete Save confirmed - same reset as console 'deletesave confirm'.");
            _deleteConfirmRoot.SetActive(false);
            _settingsRoot.SetActive(false);
            if (KebammPrototypeBootstrap.Instance != null)
                KebammPrototypeBootstrap.Instance.DeleteSaveAndRestart();
            _shownStars = -1;
            RefreshPlayerPanel();
        }

        void OnTerms()
        {
            Debug.Log("[Kebamm] Terms and Conditions pressed - placeholder, no link yet.");
        }

        // "New game data available" Yes / No prompt (same placeholder style as Settings). Its dimmer blocks menu input.
        void BuildUpdatePrompt(Transform parent)
        {
            _updateRoot = new GameObject("UpdatePrompt");
            _updateRoot.transform.SetParent(parent, false);
            Stretch(_updateRoot.AddComponent<RectTransform>());

            var dim = CreateImage(_updateRoot.transform, "Dimmer", KebammVisualFactory.SquareSprite, new Color(0f, 0f, 0f, 0.7f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            var panel = CreateImage(_updateRoot.transform, "Panel", KebammVisualFactory.SquareSprite, SettingsFill);
            Place(panel.rectTransform, new Vector2(0f, 0f), new Vector2(820f, 620f));

            CreateLabel(_updateRoot.transform, "Title", "New game data available", 56f,
                new Vector2(0f, 200f), new Vector2(760f, 90f), SettingsText).fontStyle = FontStyles.Bold;
            _updateText = CreateLabel(_updateRoot.transform, "Version", "", 38f,
                new Vector2(0f, 100f), new Vector2(760f, 60f), SettingsText);

            CreateButton(_updateRoot.transform, "YesButton", "Yes", 46f, Color.white, StartGreen,
                null, new Vector2(0f, -40f), 0f, new Vector2(560f, 120f), OnUpdateYes);
            CreateButton(_updateRoot.transform, "NoButton", "No", 46f, Color.white, new Color(0.45f, 0.30f, 0.75f),
                null, new Vector2(0f, -190f), 0f, new Vector2(560f, 120f), OnUpdateNo);

            _updateRoot.SetActive(false);
        }

        /// <summary>Shows the update prompt if newer online data is waiting (main menu only).</summary>
        public void ShowUpdatePrompt()
        {
            if (_updateRoot == null || !IsVisible || !KebammRemoteData.HasOffer)
                return;
            _updateText.text = KebammRemoteData.OfferText;
            _updateRoot.SetActive(true);
        }

        void OnUpdateYes()
        {
            _updateRoot.SetActive(false);
            if (KebammPrototypeBootstrap.Instance != null)
                KebammPrototypeBootstrap.Instance.AcceptRemoteData();
            RefreshDataLabel();
        }

        void OnUpdateNo()
        {
            _updateRoot.SetActive(false);
            KebammRemoteData.DeclineOffer();
        }

        void RefreshDataLabel()
        {
            if (_dataLabel != null)
                _dataLabel.text = KebammRemoteData.StatusText;
        }

        public void Show()
        {
            RefreshDataLabel();
            if (_updateRoot != null)
                _updateRoot.SetActive(false);
            if (_root != null)
                _root.SetActive(true);
            if (_settingsRoot != null)
                _settingsRoot.SetActive(false);
            if (_deleteConfirmRoot != null)
                _deleteConfirmRoot.SetActive(false);
            _shownStars = -1;
            RefreshPlayerPanel();
            RefreshLevel();
            RefreshGoal();
            StartIdleTweens();
            ShowUpdatePrompt();
        }

        public void Hide()
        {
            KillTweens();
            if (_goalAutoOpen != null)
            {
                StopCoroutine(_goalAutoOpen);
                _goalAutoOpen = null;
            }
            if (_goal != null)
                _goal.CloseImmediate();
            if (_updateRoot != null)
                _updateRoot.SetActive(false);
            if (_settingsRoot != null)
                _settingsRoot.SetActive(false);
            if (_deleteConfirmRoot != null)
                _deleteConfirmRoot.SetActive(false);
            if (_root != null)
                _root.SetActive(false);
        }

        void OnStart()
        {
            // Door transition: close, swap menu -> level while covered, then open.
            // Play() returns false (and ignores the press) if one is already running.
            KebammDoorTransition.Play(StartLevelNow);
        }

        void StartLevelNow()
        {
            if (KebammPrototypeBootstrap.Instance != null)
                KebammPrototypeBootstrap.Instance.StartGameFromMenu();
            else
                Hide();
        }

        void OnSettings()
        {
            Debug.Log("[Kebamm] Settings pressed - Settings popup opened.");
            RefreshToggles();
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


        /// <summary>
        /// Start button: Empty Start Button V2 art, with a play icon and
        /// "LEVEL {current}" in Lilita One, centred as one group. The icon
        /// sprite can be swapped via the startButtonIcon field or by replacing
        /// Resources/Kebamm/UI/MainMenu/icon_play.png.
        /// </summary>
        RectTransform BuildStartButton(Transform parent, Vector2 anchored, UnityEngine.Events.UnityAction onClick)
        {
            Sprite art = LoadArt("Empty_Button");
            bool hasArt = art != null;
            Vector2 size = hasArt
                ? new Vector2(StartButtonWidth, StartButtonWidth * Aspect(art, 0.22f))
                : new Vector2(700f, 160f);

            var image = CreateImage(parent, "StartButton", hasArt ? art : KebammVisualFactory.SquareSprite, hasArt ? Color.white : StartGreen);
            Place(image.rectTransform, anchored, size);
            image.preserveAspect = hasArt;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colours = button.colors;
            colours.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            button.colors = colours;
            button.onClick.AddListener(onClick);
            _startButtonRect = image.rectTransform;

            var contentGo = new GameObject("StartButtonContent");
            contentGo.transform.SetParent(image.transform, false);
            _startContent = contentGo.AddComponent<RectTransform>();
            _startContent.anchorMin = _startContent.anchorMax = _startContent.pivot = new Vector2(0.5f, 0.5f);
            _startContent.anchoredPosition = new Vector2(StartContentOffsetX, hasArt ? size.y * StartArtBodyCentreOffset : 0f);
            _startContent.sizeDelta = Vector2.zero;

            var layout = contentGo.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = StartIconTextSpacing;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(0, 0, 0, 0);

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Sprite iconSprite = GetStartButtonIcon();
            var icon = CreateImage(contentGo.transform, "Icon", iconSprite != null ? iconSprite : KebammVisualFactory.SquareSprite, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var iconLayout = icon.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = StartIconSize;
            iconLayout.preferredHeight = StartIconSize;
            iconLayout.minWidth = StartIconSize * 0.6f;
            iconLayout.minHeight = StartIconSize * 0.6f;
            iconLayout.flexibleWidth = 0f;
            iconLayout.flexibleHeight = 0f;

            _levelLabel = CreateLabel(contentGo.transform, "LevelText", "LEVEL 1", StartLevelFontMax,
                Vector2.zero, new Vector2(400f, StartIconSize), Color.white);
            _levelLabel.fontStyle = FontStyles.Normal;
            _levelLabel.alignment = TextAlignmentOptions.Center;
            TMP_FontAsset lilita = GetLilitaFont();
            if (lilita != null)
                _levelLabel.font = lilita;
            _levelLabel.enableAutoSizing = true;
            _levelLabel.fontSizeMin = StartLevelFontMin;
            _levelLabel.fontSizeMax = StartLevelFontMax;
            _levelLabelLayout = _levelLabel.gameObject.AddComponent<LayoutElement>();
            _levelLabelLayout.preferredHeight = Mathf.Max(StartIconSize, StartLevelFontMax);
            _levelLabelLayout.minHeight = StartLevelFontMin;
            _levelLabelLayout.flexibleWidth = 0f;

            return image.rectTransform;
        }

        /// <summary>
        /// Pack the icon + level text as a centred group. Short labels stay at
        /// the max font size; longer ones (e.g. LEVEL 100) shrink to fit inside
        /// the button, including its rounded ends.
        /// </summary>
        void FitStartButtonContent()
        {
            if (_levelLabel == null || _startContent == null || _startButtonRect == null || _levelLabelLayout == null)
                return;

            _levelLabel.enableAutoSizing = false;
            _levelLabel.fontSize = StartLevelFontMax;
            _levelLabel.ForceMeshUpdate();

            float textWidth = Mathf.Ceil(_levelLabel.preferredWidth) + 2f;
            float maxGroup = Mathf.Max(80f, _startButtonRect.rect.width - StartButtonPadX * 2f);
            float maxText = Mathf.Max(40f, maxGroup - StartIconSize - StartIconTextSpacing);

            if (textWidth > maxText)
            {
                _levelLabelLayout.preferredWidth = maxText;
                _levelLabel.enableAutoSizing = true;
                _levelLabel.fontSizeMin = StartLevelFontMin;
                _levelLabel.fontSizeMax = StartLevelFontMax;
            }
            else
            {
                _levelLabelLayout.preferredWidth = textWidth;
                _levelLabel.enableAutoSizing = false;
                _levelLabel.fontSize = StartLevelFontMax;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_startContent);
            CentreLevelTextVertically();
        }

        /// <summary>
        /// All-caps text sits off-centre with the font's line metrics, so nudge
        /// the visible glyphs onto the vertical centre of the button.
        /// </summary>
        void CentreLevelTextVertically()
        {
            _levelLabel.margin = Vector4.zero;
            _levelLabel.ForceMeshUpdate();
            if (_levelLabel.textInfo == null || _levelLabel.textInfo.characterCount == 0)
                return;
            RectTransform rt = _levelLabel.rectTransform;
            float rectCentreY = (0.5f - rt.pivot.y) * rt.rect.height;
            float offset = _levelLabel.textBounds.center.y - rectCentreY;
            _levelLabel.margin = new Vector4(0f, offset, 0f, -offset);
            _levelLabel.ForceMeshUpdate();
        }

        Sprite GetStartButtonIcon()
        {
            if (startButtonIcon != null)
                return startButtonIcon;
            Sprite loaded = Resources.Load<Sprite>(ArtPath + StartIconPath);
            if (loaded == null)
                Debug.LogWarning($"[Kebamm] Start button icon missing: Resources/{ArtPath}{StartIconPath}.png - using placeholder.");
            return loaded;
        }

        internal static TMP_FontAsset GetLilitaFont()
        {
            if (_lilitaFont != null)
                return _lilitaFont;
            if (_lilitaTried)
                return GetFont();
            _lilitaTried = true;
            KebammFrameSpikeLog.FontLoads++;
            Font ttf = Resources.Load<Font>(LilitaFontPath);
            if (ttf == null)
            {
                Debug.LogWarning($"[Kebamm] Lilita One missing: Resources/{LilitaFontPath}.ttf - falling back to the default menu font.");
                return GetFont();
            }

            _lilitaFont = TMP_FontAsset.CreateFontAsset(ttf);
            return _lilitaFont != null ? _lilitaFont : GetFont();
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
            // Only try once: CreateFontAsset fails on this setup ("Unable to load font face for [LegacyRuntime]")
            // and returns null, so without this flag it was retried (and warned, with a stack trace) on every call.
            if (_font != null || _fontTried)
                return _font;
            _fontTried = true;
            KebammFrameSpikeLog.FontLoads++;

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
