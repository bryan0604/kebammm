using System;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kebamm
{
    /// <summary>
    /// "Goal Tracking" popup (Notion: Game Goal - Your Performance vs All). Built in code under the main menu canvas.
    /// Shows 7 bars (one per 10-level block), each a stack of ball sprites at full height (no player data yet).
    /// The player's "profile picture" (a default ball) is launched like a cannon ball from the left, lands on the
    /// bar of the player's current block (bounce, glow, confetti), then the description and the Lets Go! button pop in.
    /// Auto-shows once per local day on the first main menu of an app run (see KebammMainMenu); the Goal button replays it.
    /// </summary>
    public class KebammGoalTracking : MonoBehaviour
    {
        // PlayerPrefs: local device date (yyyy-MM-dd) the popup last auto-showed.
        public const string LastShownKey = "kebamm_goal_last_shown_date";

        const string TitleText = "Goal Tracking";
        const string DescriptionFormat = "Your current level is <color=#FF8A00>{0}</color>, keep pushing!";
        const string ButtonText = "Lets Go!";

        // ---- Bars ----
        public const int BarsShown = 7;          // always 7 bars (fewer only if the game has fewer blocks)
        public const int LevelsPerBlock = 10;    // one bar = one 10-level block
        const int BallsPerFullBar = 8;           // balls in a 100% bar
        const float DefaultBarFill = 1f;         // 100% for every bar until there is player data (see BarFillFor)

        // ---- Layout (design units; the content is scaled to fit the 3/4-screen panel) ----
        const float PanelScreenFraction = 0.92f; // popup covers almost the full screen, centred
        const float BannerHeight = 160f;
        const float DesignWidth = 810f;
        const float DesignHeight = 1280f;
        const float BarWidth = 62f;
        const float BarPitch = 72f;
        const float BallStep = 58f;              // vertical step of the stacked balls (slight overlap)
        const float ChartCentreX = 0f;           // chart is centred in the popup
        const float ChartCentreY = 60f;
        const float ChartWidth = 560f;
        const float ChartHeight = 700f;
        const float BarBaseY = -200f;
        const float PlayerBallSize = 74f;
        static readonly Vector2 BallStart = new Vector2(
            ChartCentreX - ChartWidth * 0.5f - PlayerBallOuterRadius,            // just outside the chart's left edge
            ChartCentreY - ChartHeight * 0.5f + PlayerBallOuterRadius);          // standing on the chart's bottom edge
        const float PlayerBallOuterRadius = (PlayerBallSize + 18f) * 0.5f;       // ball + white outline
        const float DescriptionY = -420f;
        const float ButtonY = -560f;

        // ---- Timing (seconds, about 1.6 s in total) ----
        const float DimAlpha = 0.7f;
        const float OpenDuration = 0.25f;
        const float BarRiseStart = 0.15f;
        const float BarRiseStagger = 0.04f;
        const float BarRiseDuration = 0.3f;
        const float AnticipationStart = 0.2f;    // ball squashes down before the launch
        const float LaunchTime = 0.38f;
        const float FlightDuration = 0.5f;
        const float ArcHeight = 260f;
        const float CheckStartDelay = 0.07f;     // cleared-block checks pop in after the landing
        const float CheckStagger = 0.06f;
        const float TextDelayAfterLand = 0.17f;
        const float ButtonDelayAfterLand = 0.42f;
        const float CloseDuration = 0.15f;

        const int ConfettiCount = 14;

        static readonly string[] BallArt = { "red", "blue", "green", "yellow" };
        static readonly Color[] BallFallback =
        {
            new Color(0.90f, 0.30f, 0.30f), new Color(0.25f, 0.50f, 0.90f),
            new Color(0.35f, 0.75f, 0.30f), new Color(0.97f, 0.80f, 0.15f)
        };
        static readonly Color AheadTint = new Color(0.50f, 0.50f, 0.56f, 0.55f);
        static readonly Color GlowColour = new Color(1f, 0.86f, 0.30f, 1f);
        static readonly Color ChartBackground = new Color(0.88f, 0.84f, 0.97f);
        static readonly Color CheckGreen = new Color(0.30f, 0.70f, 0.35f);

        class Bar
        {
            public int block;
            public RectTransform root;
            public Image glow;
            public RectTransform shine;
            public RectTransform check;
            public float height;
            public float x;
        }

        RectTransform _root;
        Image _dimmer;
        bool _tapCloseReady;
        RectTransform _panel;
        RectTransform _contentArea;
        RectTransform _content;
        RectTransform _barsRoot;
        RectTransform _ball;
        CanvasGroup _youGroup;
        TextMeshProUGUI _desc;
        CanvasGroup _descGroup;
        RectTransform _descRect;
        RectTransform _buttonRect;
        RectTransform[] _confetti;
        Image[] _confettiImages;
        readonly System.Collections.Generic.List<Bar> _bars = new System.Collections.Generic.List<Bar>();
        int _playerBarIndex;
        bool _closing;
        static Sprite[] _ballSprites;
        static bool _ballSpritesTried;

        public bool IsOpen { get; private set; }

        // ---------- Rules (static, also used by the main menu) ----------

        /// <summary>Player has completed level 1: level 2 unlocked, or the level 1 score screen was reached.</summary>
        public static bool HasCompletedLevel1()
        {
            KebammSaveData d = KebammSaveSystem.Data;
            if (d.progress.highestLevelUnlocked > 1)
                return true;
            foreach (KebammSaveLevel l in d.levels)
                if (l.level == 1 && (l.bestStars > 0 || l.bestScore > 0 || l.attempts > 0))
                    return true;
            return false;
        }

        static string TodayKey()
        {
            return DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); // local device date
        }

        public static bool ShownToday => PlayerPrefs.GetString(LastShownKey, "") == TodayKey();

        public static void MarkShownToday()
        {
            PlayerPrefs.SetString(LastShownKey, TodayKey());
            PlayerPrefs.Save();
        }

        /// <summary>Forget the last-shown day (save delete / progress reset), so the popup can auto-show again.</summary>
        public static void ClearShownDate()
        {
            if (!PlayerPrefs.HasKey(LastShownKey))
                return;
            PlayerPrefs.DeleteKey(LastShownKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Which blocks the 7 bars show. Centre the player's block (3 behind, 3 ahead) and clamp at both ends so
        /// there are always 7 bars. Fewer than 7 blocks: show them all. Blocks are 0-based (block 0 = levels 1-10).
        /// </summary>
        public static void ComputeWindow(int currentLevel, int maxLevel, out int firstBlock, out int shown, out int playerBlock, out int totalBlocks)
        {
            totalBlocks = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, maxLevel) / (float)LevelsPerBlock));
            playerBlock = Mathf.Clamp((Mathf.Max(1, currentLevel) - 1) / LevelsPerBlock, 0, totalBlocks - 1);
            shown = Mathf.Min(BarsShown, totalBlocks);
            firstBlock = Mathf.Clamp(playerBlock - BarsShown / 2, 0, totalBlocks - shown);
        }

        /// <summary>Bar height, 0-1 (share of players in the block). 100% for now; real data would plug in here.</summary>
        static float BarFillFor(int block)
        {
            return DefaultBarFill;
        }

        // ---------- Build ----------

        public void Build(Transform parent)
        {
            _root = KebammPlaceholderUi.Rect(parent, "GoalTrackingPopup");
            KebammPlaceholderUi.Stretch(_root);
            // Own sorting layer so the popup draws (and takes clicks) above every other menu element.
            var popupCanvas = _root.gameObject.AddComponent<Canvas>();
            popupCanvas.overrideSorting = true;
            popupCanvas.sortingOrder = 500;
            _root.gameObject.AddComponent<GraphicRaycaster>();

            // Full-screen dimmer: blocks every click to the menu behind, and a tap anywhere closes the popup.
            _dimmer = KebammPlaceholderUi.Box(_root, "Dimmer", new Color(0f, 0f, 0f, DimAlpha), true);
            KebammPlaceholderUi.Stretch(_dimmer.rectTransform);
            var tapToClose = _dimmer.gameObject.AddComponent<Button>();
            tapToClose.transition = Selectable.Transition.None;
            tapToClose.targetGraphic = _dimmer;
            tapToClose.onClick.AddListener(OnTapAnywhere);

            _panel = KebammPlaceholderUi.Rect(_root, "Panel");
            float margin = (1f - PanelScreenFraction) * 0.5f;
            _panel.anchorMin = new Vector2(margin, margin);
            _panel.anchorMax = new Vector2(1f - margin, 1f - margin);
            _panel.offsetMin = Vector2.zero;
            _panel.offsetMax = Vector2.zero;
            _panel.pivot = new Vector2(0.5f, 0.5f);
            var panelBg = KebammPlaceholderUi.Box(_panel, "PanelBackground", KebammPlaceholderUi.PanelFill, false);
            KebammPlaceholderUi.Stretch(panelBg.rectTransform);

            // Title banner across the top.
            var banner = KebammPlaceholderUi.Box(_panel, "TitleBanner", KebammPlaceholderUi.Purple, false);
            RectTransform br = banner.rectTransform;
            br.anchorMin = new Vector2(0f, 1f);
            br.anchorMax = new Vector2(1f, 1f);
            br.pivot = new Vector2(0.5f, 1f);
            br.anchoredPosition = Vector2.zero;
            br.sizeDelta = new Vector2(0f, BannerHeight);
            TextMeshProUGUI title = MakeLabel(banner.transform, "Title", TitleText, 92f, Vector2.zero, new Vector2(700f, BannerHeight), Color.white);
            KebammPlaceholderUi.Stretch(title.rectTransform);

            // Content area under the banner; the design-size content is scaled to fit it.
            _contentArea = KebammPlaceholderUi.Rect(_panel, "ContentArea");
            KebammPlaceholderUi.Stretch(_contentArea);
            _contentArea.offsetMax = new Vector2(0f, -BannerHeight);
            _content = KebammPlaceholderUi.Rect(_contentArea, "Content");
            KebammPlaceholderUi.Place(_content, Vector2.zero, new Vector2(DesignWidth, DesignHeight));

            // Placeholder background behind the bars.
            var chart = KebammPlaceholderUi.Box(_content, "ChartBackground", ChartBackground, false);
            KebammPlaceholderUi.Place(chart.rectTransform, new Vector2(ChartCentreX, ChartCentreY), new Vector2(ChartWidth, ChartHeight));
            var baseline = KebammPlaceholderUi.Box(_content, "ChartBaseline", new Color(0.55f, 0.45f, 0.75f), false);
            KebammPlaceholderUi.Place(baseline.rectTransform, new Vector2(ChartCentreX, BarBaseY - 4f), new Vector2(ChartWidth - 40f, 8f));

            _barsRoot = KebammPlaceholderUi.Rect(_content, "Bars");
            KebammPlaceholderUi.Stretch(_barsRoot);

            // "You" marker above the waiting ball (bottom-left corner of the chart).
            TextMeshProUGUI you = MakeLabel(_content, "YouLabel", "YOU", 44f, new Vector2(BallStart.x, BallStart.y + 72f), new Vector2(200f, 60f), KebammPlaceholderUi.PanelText);
            _youGroup = you.gameObject.AddComponent<CanvasGroup>();

            // Player "profile picture": a default ball with a white outline.
            _ball = KebammPlaceholderUi.Rect(_content, "PlayerBall");
            KebammPlaceholderUi.Place(_ball, BallStart, new Vector2(PlayerBallSize, PlayerBallSize));
            var outline = KebammPlaceholderUi.Box(_ball, "Outline", Color.white, false);
            outline.sprite = KebammVisualFactory.CircleSprite;
            KebammPlaceholderUi.Place(outline.rectTransform, Vector2.zero, new Vector2(PlayerBallSize + 18f, PlayerBallSize + 18f));
            var ballImage = KebammPlaceholderUi.Box(_ball, "Ball", Color.white, false);
            SetBallLook(ballImage, 0);
            KebammPlaceholderUi.Place(ballImage.rectTransform, Vector2.zero, new Vector2(PlayerBallSize, PlayerBallSize));

            // Description (pops in after the landing).
            _desc = MakeLabel(_content, "Description", "", 56f, new Vector2(0f, DescriptionY), new Vector2(740f, 150f), KebammPlaceholderUi.PanelText);
            _desc.textWrappingMode = TextWrappingModes.Normal;
            _descRect = _desc.rectTransform;
            _descGroup = _desc.gameObject.AddComponent<CanvasGroup>();

            // Lets Go! (closes the popup).
            var button = KebammPlaceholderUi.MakeButton(_content, "LetsGoButton", ButtonText, KebammPlaceholderUi.Green,
                new Vector2(0f, ButtonY), new Vector2(460f, 120f), Close, 58f);
            _buttonRect = (RectTransform)button.transform;
            ApplyFont(button.GetComponentInChildren<TextMeshProUGUI>());

            // Confetti pool for the landing burst.
            _confetti = new RectTransform[ConfettiCount];
            _confettiImages = new Image[ConfettiCount];
            for (int i = 0; i < ConfettiCount; i++)
            {
                var c = KebammPlaceholderUi.Box(_content, "Confetti" + i, Color.white, false);
                if (i % 2 == 0)
                    c.sprite = KebammVisualFactory.CircleSprite;
                KebammPlaceholderUi.Place(c.rectTransform, Vector2.zero, new Vector2(16f, 16f));
                c.gameObject.SetActive(false);
                _confetti[i] = c.rectTransform;
                _confettiImages[i] = c;
            }

            _root.gameObject.SetActive(false);
        }

        static TextMeshProUGUI MakeLabel(Transform parent, string name, string text, float size, Vector2 anchored, Vector2 box, Color colour)
        {
            TextMeshProUGUI label = KebammPlaceholderUi.Label(parent, name, text, size, anchored, box, colour);
            label.richText = true;
            ApplyFont(label);
            return label;
        }

        static void ApplyFont(TextMeshProUGUI label)
        {
            if (label == null)
                return;
            TMP_FontAsset lilita = KebammMainMenu.GetLilitaFont();
            if (lilita != null)
                label.font = lilita;
        }

        static void LoadBallSprites()
        {
            if (_ballSpritesTried)
                return;
            _ballSpritesTried = true;
            _ballSprites = new Sprite[BallArt.Length];
            for (int i = 0; i < BallArt.Length; i++)
                _ballSprites[i] = Resources.Load<Sprite>("Kebamm/Balls/ball_" + BallArt[i]); // existing ball art, null = tinted circle
        }

        /// <summary>Existing ball sprite (colour index 0-3), or a tinted circle placeholder if the art is missing.</summary>
        static void SetBallLook(Image image, int colourIndex)
        {
            LoadBallSprites();
            int i = ((colourIndex % BallArt.Length) + BallArt.Length) % BallArt.Length;
            Sprite s = _ballSprites[i];
            image.sprite = s != null ? s : KebammVisualFactory.CircleSprite;
            image.color = s != null ? Color.white : BallFallback[i];
            image.preserveAspect = true;
        }

        // ---------- Open / close ----------

        /// <summary>Opens the popup and plays the transition (also restarts it if already open).</summary>
        public void Open(bool auto)
        {
            if (_root == null)
                return;
            KillAll();
            _closing = false;
            _tapCloseReady = false;
            IsOpen = true;
            if (auto)
                MarkShownToday();
            _root.gameObject.SetActive(true);

            var boot = KebammPrototypeBootstrap.Instance;
            int level = boot != null ? Mathf.Max(1, boot.CurrentLevelNumber) : 1;
            int maxLevel = boot != null ? Mathf.Max(1, boot.LastLevelNumber) : 1;
            BuildBars(level, maxLevel);
            _desc.text = string.Format(DescriptionFormat, level);

            Canvas.ForceUpdateCanvases();
            FitContent();
            ResetPose();
            Play();
        }

        /// <summary>Tap anywhere (dimmer or panel) closes the popup once the transition has finished.</summary>
        void OnTapAnywhere()
        {
            if (_tapCloseReady)
                Close();
        }

        public void Close()
        {
            if (!IsOpen || _closing)
                return;
            _closing = true;
            KillAll();
            DOTween.Sequence().SetUpdate(true).SetId(this)
                .Join(_panel.DOScale(0.85f, CloseDuration).SetEase(Ease.InBack))
                .Join(_dimmer.DOFade(0f, CloseDuration))
                .OnComplete(CloseImmediate);
        }

        public void CloseImmediate()
        {
            KillAll();
            _closing = false;
            IsOpen = false;
            if (_root != null)
                _root.gameObject.SetActive(false);
        }

        void KillAll()
        {
            DOTween.Kill(this);
        }

        void OnDestroy()
        {
            KillAll();
        }

        void LateUpdate()
        {
            if (IsOpen)
                FitContent();
        }

        void FitContent()
        {
            Rect r = _contentArea.rect;
            if (r.width <= 0f || r.height <= 0f)
                return;
            float s = Mathf.Min(r.width / DesignWidth, r.height / DesignHeight);
            _content.localScale = new Vector3(s, s, 1f);
        }

        // ---------- Bars ----------

        void BuildBars(int level, int maxLevel)
        {
            for (int i = _barsRoot.childCount - 1; i >= 0; i--)
                Destroy(_barsRoot.GetChild(i).gameObject);
            _bars.Clear();

            ComputeWindow(level, maxLevel, out int first, out int shown, out int playerBlock, out int totalBlocks);
            _playerBarIndex = playerBlock - first;
            Debug.Log($"[Kebamm] Goal Tracking: level {level}/{maxLevel}, {totalBlocks} blocks, showing blocks {first + 1}-{first + shown} (player block {playerBlock + 1}, {_playerBarIndex} behind, {shown - 1 - _playerBarIndex} ahead).");

            for (int i = 0; i < shown; i++)
            {
                int block = first + i;
                bool passed = block < playerBlock;
                bool current = block == playerBlock;
                float x = ChartCentreX + (i - (shown - 1) * 0.5f) * BarPitch;
                int count = Mathf.Max(1, Mathf.RoundToInt(BallsPerFullBar * Mathf.Clamp01(BarFillFor(block))));
                float height = (count - 1) * BallStep + BarWidth;
                var bar = new Bar { block = block, x = x, height = height };

                // Glow behind the player's bar (invisible until the landing).
                if (current)
                {
                    bar.glow = KebammPlaceholderUi.Box(_barsRoot, "Glow", GlowColour, false);
                    RectTransform gr = bar.glow.rectTransform;
                    gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f);
                    gr.pivot = new Vector2(0.5f, 0f);
                    gr.anchoredPosition = new Vector2(x, BarBaseY - 10f);
                    gr.sizeDelta = new Vector2(BarWidth + 24f, height + 20f);
                }

                RectTransform root = KebammPlaceholderUi.Rect(_barsRoot, "Bar_" + (block + 1));
                root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
                root.pivot = new Vector2(0.5f, 0f);
                root.anchoredPosition = new Vector2(x, BarBaseY);
                root.sizeDelta = new Vector2(BarWidth, height);
                bar.root = root;
                for (int k = 0; k < count; k++)
                {
                    var ball = KebammPlaceholderUi.Box(root, "Ball" + k, Color.white, false);
                    SetBallLook(ball, block);
                    RectTransform br = ball.rectTransform;
                    br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f);
                    br.pivot = new Vector2(0.5f, 0f);
                    br.anchoredPosition = new Vector2(0f, k * BallStep);
                    br.sizeDelta = new Vector2(BarWidth, BarWidth);
                    if (!passed && !current)
                        ball.color = ball.color * AheadTint; // blocks ahead are dimmer
                }

                // Shine: a soft diagonal light strip clipped to the player's bar, swept upward after the landing.
                if (current)
                {
                    root.gameObject.AddComponent<RectMask2D>();
                    var shine = KebammPlaceholderUi.Box(root, "Shine", Color.white, false);
                    shine.sprite = ShineSprite;
                    RectTransform sr = shine.rectTransform;
                    sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0f);
                    sr.pivot = new Vector2(0.5f, 0.5f);
                    sr.localRotation = Quaternion.Euler(0f, 0f, 25f);
                    shine.gameObject.SetActive(false);
                    bar.shine = sr;
                }

                int lastLevel = Mathf.Min((block + 1) * LevelsPerBlock, maxLevel);
                TextMeshProUGUI range = MakeLabel(_barsRoot, "Range_" + (block + 1), $"{block * LevelsPerBlock + 1}-{lastLevel}", 22f,
                    new Vector2(x, BarBaseY - 36f), new Vector2(BarPitch, 30f),
                    passed || current ? KebammPlaceholderUi.PanelText : new Color(0.30f, 0.15f, 0.55f, 0.45f));
                range.enableAutoSizing = true;
                range.fontSizeMin = 12f;
                range.fontSizeMax = 22f;

                if (passed)
                    bar.check = MakeCheck(_barsRoot, new Vector2(x, BarBaseY + height + 30f));

                _bars.Add(bar);
            }
        }

        /// <summary>Small green check badge for a cleared block (circle plus two rotated strokes).</summary>
        static RectTransform MakeCheck(Transform parent, Vector2 pos)
        {
            RectTransform badge = KebammPlaceholderUi.Rect(parent, "Check");
            KebammPlaceholderUi.Place(badge, pos, new Vector2(44f, 44f));
            var circle = KebammPlaceholderUi.Box(badge, "Circle", CheckGreen, false);
            circle.sprite = KebammVisualFactory.CircleSprite;
            KebammPlaceholderUi.Stretch(circle.rectTransform);
            var shortLeg = KebammPlaceholderUi.Box(badge, "Short", Color.white, false);
            KebammPlaceholderUi.Place(shortLeg.rectTransform, new Vector2(-7f, -3f), new Vector2(6f, 14f));
            shortLeg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var longLeg = KebammPlaceholderUi.Box(badge, "Long", Color.white, false);
            KebammPlaceholderUi.Place(longLeg.rectTransform, new Vector2(4f, 1f), new Vector2(6f, 26f));
            longLeg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            return badge;
        }

        // ---------- Bar shine ----------

        static Sprite _shineSprite;

        /// <summary>White strip, transparent at both long edges and bright in the middle (soft falloff).</summary>
        static Sprite ShineSprite
        {
            get
            {
                if (_shineSprite != null)
                    return _shineSprite;
                const int h = 32;
                var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int y = 0; y < h; y++)
                {
                    float a = Mathf.Sin(Mathf.PI * (y + 0.5f) / h);
                    tex.SetPixel(0, y, new Color(1f, 1f, 1f, a * a));
                }
                tex.Apply();
                _shineSprite = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
                return _shineSprite;
            }
        }

        /// <summary>Loops the light strip from below the player's bar to above it, with a pause between sweeps.</summary>
        void StartShine(Bar bar)
        {
            if (bar.shine == null)
                return;
            var boot = KebammPrototypeBootstrap.Instance;
            var gm = boot != null ? boot.GameManagerData : null;
            float duration = gm != null ? gm.GoalShineDuration : 0.6f;
            float interval = gm != null ? gm.GoalShineInterval : 2f;
            float alpha = gm != null ? gm.GoalShineAlpha : 0.6f;
            float thickness = gm != null ? gm.GoalShineThickness : 36f;
            if (alpha <= 0f)
                return;

            RectTransform sr = bar.shine;
            var img = sr.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, alpha);
            sr.sizeDelta = new Vector2(BarWidth * 2.5f, thickness);
            float startY = -thickness * 2f;
            float endY = bar.height + thickness * 2f;
            sr.anchoredPosition = new Vector2(0f, startY);
            sr.gameObject.SetActive(true);

            DOTween.Sequence().SetUpdate(true).SetId(this)
                .AppendInterval(0.35f)
                .Append(sr.DOAnchorPosY(endY, duration).From(new Vector2(0f, startY)).SetEase(Ease.InOutSine))
                .AppendInterval(interval)
                .SetLoops(-1, LoopType.Restart);
        }

        // ---------- Transition ----------

        void ResetPose()
        {
            Color d = _dimmer.color;
            _dimmer.color = new Color(d.r, d.g, d.b, 0f);
            _panel.localScale = Vector3.one * 0.85f;
            foreach (Bar b in _bars)
            {
                b.root.localScale = new Vector3(1f, 0f, 1f);
                if (b.glow != null)
                {
                    Color g = b.glow.color;
                    b.glow.color = new Color(g.r, g.g, g.b, 0f);
                }
                if (b.check != null)
                    b.check.localScale = Vector3.zero;
                if (b.shine != null)
                    b.shine.gameObject.SetActive(false);
            }
            _ball.anchoredPosition = BallStart;
            _ball.localScale = Vector3.one;
            _ball.localRotation = Quaternion.identity;
            _youGroup.alpha = 1f;
            _descGroup.alpha = 0f;
            _descRect.localScale = Vector3.one * 0.6f;
            _buttonRect.gameObject.SetActive(false);
            _buttonRect.localScale = Vector3.zero;
            foreach (RectTransform c in _confetti)
                c.gameObject.SetActive(false);
        }

        Vector2 LandingPoint()
        {
            Bar target = _bars[Mathf.Clamp(_playerBarIndex, 0, _bars.Count - 1)];
            // Ball rests on the top of the stack (centre = top + a bit less than its radius).
            return new Vector2(target.x, BarBaseY + target.height + PlayerBallSize * 0.5f - 6f);
        }

        void Play()
        {
            Vector2 start = BallStart;
            Vector2 end = LandingPoint();
            float land = LaunchTime + FlightDuration;

            Sequence seq = DOTween.Sequence().SetUpdate(true).SetId(this);
            seq.Insert(0f, _dimmer.DOFade(DimAlpha, 0.15f));
            seq.Insert(0f, _panel.DOScale(1f, OpenDuration).SetEase(Ease.OutBack));

            // Bars rise one after another (the stacks are drawn at full height; they grow from the baseline).
            for (int i = 0; i < _bars.Count; i++)
                seq.Insert(BarRiseStart + BarRiseStagger * i, _bars[i].root.DOScale(Vector3.one, BarRiseDuration).SetEase(Ease.OutBack));

            // Ball: squash down, stretch up, launch in an arc (spinning), back to round mid-flight.
            seq.Insert(AnticipationStart, _ball.DOScale(new Vector3(1.25f, 0.75f, 1f), 0.12f).SetEase(Ease.OutQuad));
            seq.Insert(AnticipationStart + 0.12f, _ball.DOScale(new Vector3(0.85f, 1.2f, 1f), LaunchTime - AnticipationStart - 0.12f).SetEase(Ease.OutQuad));
            seq.Insert(LaunchTime, _ball.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutQuad));
            seq.Insert(LaunchTime, _ball.DORotate(new Vector3(0f, 0f, -360f), FlightDuration, RotateMode.FastBeyond360).SetEase(Ease.Linear));
            seq.Insert(LaunchTime, _youGroup.DOFade(0f, 0.15f));
            seq.Insert(LaunchTime, DOVirtual.Float(0f, 1f, FlightDuration, v =>
            {
                Vector2 p = Vector2.Lerp(start, end, v);
                p.y += ArcHeight * 4f * v * (1f - v);
                _ball.anchoredPosition = p;
            }).SetEase(Ease.Linear));
            seq.InsertCallback(land, () => OnLanded(end));
        }

        void OnLanded(Vector2 end)
        {
            _ball.anchoredPosition = end;
            _ball.localRotation = Quaternion.identity;

            // Ball squashes on impact and wobbles back; it dips with the bar, then floats gently.
            DOTween.Sequence().SetUpdate(true).SetId(this)
                .Append(_ball.DOScale(new Vector3(1.35f, 0.65f, 1f), 0.07f).SetEase(Ease.OutQuad))
                .Append(_ball.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutElastic));
            DOTween.Sequence().SetUpdate(true).SetId(this)
                .Append(_ball.DOAnchorPosY(end.y - 12f, 0.07f).SetEase(Ease.OutQuad))
                .Append(_ball.DOAnchorPosY(end.y, 0.3f).SetEase(Ease.OutBounce));
            _ball.DOAnchorPosY(end.y + 6f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                .SetDelay(0.5f).SetUpdate(true).SetId(this);

            Bar bar = _bars[Mathf.Clamp(_playerBarIndex, 0, _bars.Count - 1)];
            // The bar stack bounces, and the block lights up.
            bar.root.DOPunchScale(new Vector3(0.14f, -0.1f, 0f), 0.4f, 8, 0.6f).SetUpdate(true).SetId(this);
            if (bar.glow != null)
            {
                Image glow = bar.glow;
                glow.DOFade(0.9f, 0.12f).SetUpdate(true).SetId(this)
                    .OnComplete(() => glow.DOFade(0.45f, 0.55f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetId(this));
            }

            StartShine(bar);
            Burst(end);

            // Cleared blocks get a check, one after another from the left.
            int k = 0;
            foreach (Bar b in _bars)
            {
                if (b.check == null)
                    continue;
                b.check.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetDelay(CheckStartDelay + CheckStagger * k).SetUpdate(true).SetId(this);
                k++;
            }

            // Description pops in, then the Lets Go! button.
            _descGroup.DOFade(1f, 0.15f).SetDelay(TextDelayAfterLand).SetUpdate(true).SetId(this);
            _descRect.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetDelay(TextDelayAfterLand).SetUpdate(true).SetId(this);
            DOVirtual.DelayedCall(ButtonDelayAfterLand, () =>
            {
                _buttonRect.gameObject.SetActive(true);
                _tapCloseReady = true;
                _buttonRect.localScale = Vector3.zero;
                _buttonRect.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true).SetId(this);
            }, true).SetId(this);
        }

        /// <summary>Small confetti burst from the landing point.</summary>
        void Burst(Vector2 origin)
        {
            Color[] palette =
            {
                new Color(1f, 0.86f, 0.30f), new Color(0.95f, 0.45f, 0.55f), new Color(0.40f, 0.75f, 1f), new Color(0.55f, 0.85f, 0.25f), Color.white
            };
            for (int i = 0; i < _confetti.Length; i++)
            {
                RectTransform c = _confetti[i];
                Image img = _confettiImages[i];
                c.gameObject.SetActive(true);
                c.anchoredPosition = origin;
                c.localScale = Vector3.one;
                c.localRotation = Quaternion.identity;
                Color col = palette[i % palette.Length];
                img.color = col;

                float angle = (360f / _confetti.Length * i + UnityEngine.Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
                float dist = UnityEngine.Random.Range(90f, 210f);
                Vector2 target = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist + Vector2.down * 30f;

                DOTween.Sequence().SetUpdate(true).SetId(this)
                    .Join(c.DOAnchorPos(target, 0.5f).SetEase(Ease.OutCubic))
                    .Join(c.DORotate(new Vector3(0f, 0f, UnityEngine.Random.Range(-240f, 240f)), 0.5f, RotateMode.FastBeyond360))
                    .Insert(0.25f, img.DOFade(0f, 0.25f))
                    .Insert(0.2f, c.DOScale(0.3f, 0.3f))
                    .OnComplete(() => c.gameObject.SetActive(false));
            }
        }
    }
}