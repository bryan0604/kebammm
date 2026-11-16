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
        TextMeshProUGUI _levelValue;
        TextMeshProUGUI _endLevelValue;
        TextMeshProUGUI _endNextLevelValue;
        readonly Image[] _stars = new Image[3];

        // Art Document "Star Resource" icon for earned stars (shown untinted).
        const string StarSpritePath = "Kebamm/UI/star_icon";
        static Sprite _starSprite;
        // Art Document "Star Resource Empty": shown untinted for stars not obtained yet.
        const string EmptyStarSpritePath = "Kebamm/UI/star_icon_empty";
        static Sprite _emptyStarSprite;

        static Sprite EmptyStarSprite
        {
            get
            {
                if (_emptyStarSprite != null)
                    return _emptyStarSprite;
                _emptyStarSprite = Resources.Load<Sprite>(EmptyStarSpritePath);
                if (_emptyStarSprite == null)
                {
                    Texture2D tex = Resources.Load<Texture2D>(EmptyStarSpritePath);
                    if (tex != null)
                        _emptyStarSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
                if (_emptyStarSprite == null)
                {
                    Debug.LogWarning("[Kebamm] star_icon_empty missing from Resources/Kebamm/UI; using star_icon for unearned stars.");
                    _emptyStarSprite = StarSprite;
                }
                return _emptyStarSprite;
            }
        }
        GameObject _nextButton;
        Sequence _endSequence;
        TextMeshProUGUI _endMergeValue;
        TextMeshProUGUI _endMonsterValue;
        TextMeshProUGUI _endComboValue;
        TextMeshProUGUI _endFinalValue;
        TextMeshProUGUI _endCoinsLabel;
        TextMeshProUGUI _endCoinsValue;
        Image _endCoinsIcon;
        Tween _coinCountTween;
        // Score text count-up (Fine Tuning DOTween): HUD rolls from the shown value; end screen numbers roll from 0.
        Tween _hudCountTween;
        int _hudShown;
        readonly System.Collections.Generic.List<Tween> _endCountTweens = new System.Collections.Generic.List<Tween>();

        /// <summary>Coins earned by the last finished attempt (score screen, save and log all use this).</summary>
        public static int LastCoinsEarned { get; private set; }

        static readonly Color CoinTextColor = new Color(1f, 0.84f, 0.15f, 1f); // placeholder gold, same as earned star
        GameObject _endRoot;
        CanvasGroup _endGroup;
        bool _ended;

        public bool IsEndVisible => _ended;

        static TMP_FontAsset _font;
        static bool _fontTried;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (_endGroup != null)
                _endGroup.DOKill();
            KillEndSequence();
            KillStarTweens();
            if (Instance == this)
                Instance = null;
        }

        void OnEnable()
        {
            KebammScore.Changed += Refresh;
            KebammCombo.Changed += RefreshCombo;
            KebammCombo.BarFilled += PulseComboBar;
        }

        void OnDisable()
        {
            KebammScore.Changed -= Refresh;
            KebammCombo.Changed -= RefreshCombo;
            KebammCombo.BarFilled -= PulseComboBar;
        }

        // ---- Combo bar (placeholder, top left; GDD Combo Gameplay Design) -------------------------------
        RectTransform _comboBar;
        RectTransform _comboFill;
        TextMeshProUGUI _comboLabel;
        float _comboBarWidth = 360f; // from combo_config barWidth / barHeight when built
        RectTransform _comboGlow;
        Image _comboGlowImage;
        Image _comboFillImage;

        void BuildComboBar(Transform parent)
        {
            KebammComboConfig cfg = KebammCombo.Config;
            _comboBarWidth = cfg.BarWidth;
            float barHeight = cfg.BarHeight;
            // Centre pivot so the pulse and the cute bounce grow from the middle (top-left still at barPosition).
            Vector2 centre = cfg.barPosition + new Vector2(_comboBarWidth * 0.5f, -barHeight * 0.5f);

            // Combo glow sits behind the bar (created first so it draws underneath).
            var glowGo = new GameObject("ComboGlow");
            glowGo.transform.SetParent(parent, false);
            _comboGlow = glowGo.AddComponent<RectTransform>();
            _comboGlow.anchorMin = _comboGlow.anchorMax = new Vector2(0f, 1f);
            _comboGlow.pivot = new Vector2(0.5f, 0.5f);
            _comboGlow.anchoredPosition = centre;
            _comboGlowImage = glowGo.AddComponent<Image>();
            _comboGlowImage.sprite = ComboGlowSprite;
            _comboGlowImage.type = Image.Type.Sliced;
            _comboGlowImage.raycastTarget = false;
            _comboGlowImage.color = new Color(1f, 1f, 1f, 0f);

            var root = new GameObject("ComboBar");
            root.transform.SetParent(parent, false);
            _comboBar = root.AddComponent<RectTransform>();
            _comboBar.anchorMin = _comboBar.anchorMax = new Vector2(0f, 1f);
            _comboBar.pivot = new Vector2(0.5f, 0.5f);
            _comboBar.anchoredPosition = centre;
            _comboBar.sizeDelta = new Vector2(_comboBarWidth, barHeight);
            var bg = root.AddComponent<Image>();
            bg.sprite = KebammVisualFactory.SquareSprite;
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(root.transform, false);
            _comboFill = fillGo.AddComponent<RectTransform>();
            _comboFill.anchorMin = new Vector2(0f, 0f);
            _comboFill.anchorMax = new Vector2(0f, 1f);
            _comboFill.pivot = new Vector2(0f, 0.5f);
            _comboFill.offsetMin = new Vector2(3f, 3f);
            _comboFill.offsetMax = new Vector2(3f, -3f);
            var fill = fillGo.AddComponent<Image>();
            fill.sprite = KebammVisualFactory.SquareSprite;
            fill.color = new Color(1f, 0.78f, 0.15f, 1f);
            _comboFillImage = fill;
            fill.raycastTarget = false;

            _comboLabel = CreateLabel(root.transform, "ComboLabel", "Combo 0/100", cfg.barLabelFontSize,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(_comboBarWidth, barHeight));
            RefreshCombo();
        }

        // Animated fill state (left-to-right, left pivot). _fillT is the shown fraction 0..1.
        float _fillT;
        int _lastBarPoints;
        int _lastTotal;
        Sequence _fillSeq;

        void RefreshCombo()
        {
            if (_comboFill == null)
                return;
            KebammComboConfig cfg = KebammCombo.Config;
            int cap = cfg.BarCapacity;
            int bar = KebammCombo.BarPoints;
            int total = KebammCombo.TotalComboPoints;
            float target = Mathf.Clamp01((float)bar / cap);
            int gain = total - _lastTotal;
            bool overflow = gain > 0 && _lastBarPoints + gain >= cap;
            _lastBarPoints = bar;
            _lastTotal = total;

            if (gain <= 0 || !isActiveAndEnabled)
            {
                // Reset / new attempt: drop dots still flying (their fill would be stale) and snap.
                _comboGen++;
                _deferPulse = false;
                if (_comboDots != null)
                    _comboDots.Clear();
                if (_fillSeq != null && _fillSeq.IsActive())
                    _fillSeq.Kill();
                _fillSeq = null;
                SetFill(target, cap);
                return;
            }

            int combo = KebammCombo.ComboCount;
            int gen = _comboGen;
            if (SpawnComboDots(combo, () =>
                {
                    if (gen != _comboGen || _comboFill == null)
                        return;
                    PlayComboGain(combo, overflow, target, cap);
                    if (overflow)
                        PulseComboBarNow();
                }))
            {
                _deferPulse = overflow; // BarFilled fires right after this; the pulse waits for the dots
                return;
            }
            PlayComboGain(combo, overflow, target, cap);
        }

        // ---- Combo dots (VFX Fine Tuning; local tuning in GameManager.asset until the style is approved) ----
        KebammComboDots _comboDots;
        int _comboGen;
        bool _deferPulse;

        /// <summary>Flies glowing dots from the latest merge to the combo bar. False (no dots) = fill right away.</summary>
        bool SpawnComboDots(int combo, System.Action onFirstArrive)
        {
            if (_comboDots == null || _comboBar == null)
                return false;
            KebammGameManagerData gm = KebammPrototypeBootstrap.Instance != null ? KebammPrototypeBootstrap.Instance.GameManagerData : null;
            Camera cam = Camera.main;
            if (gm == null || cam == null)
                return false;
            int count = gm.ComboDotCountFor(combo);
            if (count <= 0)
                return false;
            Vector2 screenFrom = cam.WorldToScreenPoint(KebammCombo.LastMergePosition);
            Vector2 screenTo = RectTransformUtility.WorldToScreenPoint(null, _comboBar.position);
            RectTransform root = _comboDots.Root;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenFrom, null, out Vector2 from)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenTo, null, out Vector2 to))
                return false;
            Color c = _comboFillImage != null ? _comboFillImage.color : new Color(1f, 0.78f, 0.15f, 1f);
            return _comboDots.Fly(from, to, _comboBarWidth * 0.3f, count, c, gm, onFirstArrive);
        }

        /// <summary>The bar's reaction to a combo gain: glow + bounce and the animated fill (when the dots hit, or at once).</summary>
        void PlayComboGain(int combo, bool overflow, float target, int cap)
        {
            KebammComboConfig cfg = KebammCombo.Config;
            if (_fillSeq != null && _fillSeq.IsActive())
                _fillSeq.Kill(); // rapid merges: never stack tweens, continue from the shown value
            _fillSeq = null;

            GlowComboBar(combo, overflow);

            float dur = Mathf.Max(0f, cfg.barFillDuration);
            if (dur <= 0f || !isActiveAndEnabled)
            {
                SetFill(target, cap); // animation off: snap
                return;
            }

            _fillSeq = DOTween.Sequence().SetLink(_comboFill.gameObject);
            if (overflow)
            {
                // Bomb earned: animate to full, reset to 0, then animate to the carried-over amount.
                _fillSeq.Append(DOTween.To(() => _fillT, v => SetFill(v, cap), 1f, dur).SetEase(cfg.barFillEase));
                _fillSeq.AppendCallback(() => SetFill(0f, cap));
                if (target > 0f)
                    _fillSeq.Append(DOTween.To(() => _fillT, v => SetFill(v, cap), target, dur).SetEase(cfg.barFillEase));
            }
            else
            {
                _fillSeq.Append(DOTween.To(() => _fillT, v => SetFill(v, cap), target, dur).SetEase(cfg.barFillEase));
            }
            _fillSeq.OnComplete(() => _fillSeq = null);
        }

        void SetFill(float t, int cap)
        {
            _fillT = Mathf.Clamp01(t);
            if (_comboFill != null)
                _comboFill.sizeDelta = new Vector2((_comboBarWidth - 6f) * _fillT, _comboFill.sizeDelta.y);
            if (_comboLabel != null)
                _comboLabel.text = $"Combo {Mathf.RoundToInt(_fillT * cap)}/{cap}";
        }

        // ---- Combo glow + cute bounce (VFX Fine Tuning; local tuning in GameManager.asset until the style is approved) ----

        static Sprite _comboGlowSprite;
        const int GlowBorder = 24;

        /// <summary>Soft ring: transparent in the middle, bright at the bar's edge, fading outward. 9-sliced around the bar.</summary>
        static Sprite ComboGlowSprite
        {
            get
            {
                if (_comboGlowSprite != null)
                    return _comboGlowSprite;
                const int size = GlowBorder * 2 + 4;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                float lo = GlowBorder, hi = size - GlowBorder;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float dx = Mathf.Max(lo - px, 0f, px - hi);
                        float dy = Mathf.Max(lo - py, 0f, py - hi);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        bool inside = px > lo && px < hi && py > lo && py < hi;
                        float a = inside ? 0f : Mathf.Clamp01(1f - d / GlowBorder);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                tex.Apply();
                _comboGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(GlowBorder, GlowBorder, GlowBorder, GlowBorder));
                return _comboGlowSprite;
            }
        }

        Sequence _glowSeq;
        Sequence _bounceSeq;

        /// <summary>
        /// Each combo point gain: a bar-coloured glow flashes around the box and fades, stronger as the combo climbs,
        /// and the box does a squash-and-stretch bounce. When the bar fills, the existing bar pulse takes over the scale.
        /// </summary>
        void GlowComboBar(int combo, bool barFilled)
        {
            if (_comboBar == null || _comboGlow == null)
                return;
            KebammGameManagerData gm = KebammPrototypeBootstrap.Instance != null ? KebammPrototypeBootstrap.Instance.GameManagerData : null;
            float duration = gm != null ? gm.ComboGlowDuration : 0.45f;
            float size = gm != null ? gm.ComboGlowSize : 28f;
            int maxCombo = gm != null ? gm.ComboGlowMaxCombo : 6;
            float alphaMin = gm != null ? gm.comboGlowAlphaMin : 0.55f;
            float alphaMax = gm != null ? gm.comboGlowAlphaMax : 1f;
            float popMin = gm != null ? gm.comboGlowPopMin : 1.05f;
            float popMax = gm != null ? gm.comboGlowPopMax : 1.2f;
            float bounceMin = gm != null ? gm.comboBounceMin : 0.08f;
            float bounceMax = gm != null ? gm.comboBounceMax : 0.18f;

            // 0 at combo 1, 1 at max combo and beyond.
            float s = maxCombo > 1 ? Mathf.Clamp01((combo - 1f) / (maxCombo - 1f)) : 1f;
            float alpha = Mathf.Clamp01(Mathf.Lerp(alphaMin, alphaMax, s));
            float pop = Mathf.Lerp(popMin, popMax, s);
            float bounce = Mathf.Lerp(bounceMin, bounceMax, s);

            // Glow: bar colour, ring as thick as "size" px around the box.
            Color c = _comboFillImage != null ? _comboFillImage.color : new Color(1f, 0.78f, 0.15f, 1f);
            _comboGlowImage.pixelsPerUnitMultiplier = GlowBorder / Mathf.Max(1f, size);
            _comboGlow.sizeDelta = _comboBar.sizeDelta + Vector2.one * size * 2f;
            if (_glowSeq != null && _glowSeq.IsActive())
                _glowSeq.Kill();
            _comboGlow.localScale = Vector3.one * 0.92f;
            _comboGlowImage.color = new Color(c.r, c.g, c.b, alpha);
            _glowSeq = DOTween.Sequence().SetLink(_comboGlow.gameObject)
                .Append(_comboGlow.DOScale(pop, 0.12f).SetEase(Ease.OutBack))
                .Join(_comboGlowImage.DOFade(0f, Mathf.Max(0.05f, duration)).SetEase(Ease.InQuad))
                .Insert(0.12f, _comboGlow.DOScale(1f, Mathf.Max(0.05f, duration - 0.12f)).SetEase(Ease.OutQuad));

            if (barFilled || bounce <= 0f)
                return; // the bar-filled pulse handles the box scale
            if (_bounceSeq != null && _bounceSeq.IsActive())
                _bounceSeq.Kill();
            _comboBar.DOKill();
            _comboBar.localScale = Vector3.one;
            _bounceSeq = DOTween.Sequence().SetTarget(_comboBar).SetLink(_comboBar.gameObject)
                .Append(_comboBar.DOScale(new Vector3(1f + bounce, 1f - bounce, 1f), 0.07f).SetEase(Ease.OutQuad))
                .Append(_comboBar.DOScale(new Vector3(1f - bounce * 0.6f, 1f + bounce * 0.6f, 1f), 0.08f).SetEase(Ease.InOutQuad))
                .Append(_comboBar.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutElastic));
        }

        void PulseComboBar(int fills)
        {
            if (_deferPulse)
            {
                _deferPulse = false; // played when the combo dots hit the bar
                return;
            }
            PulseComboBarNow();
        }

        void PulseComboBarNow()
        {
            if (_comboBar == null)
                return;
            KebammComboConfig cfg = KebammCombo.Config;
            if (_bounceSeq != null && _bounceSeq.IsActive())
                _bounceSeq.Kill();
            _comboBar.DOKill();
            _comboBar.localScale = Vector3.one;
            _comboBar.DOScale(Mathf.Max(1f, cfg.barPulseScale), Mathf.Max(0.02f, cfg.barPulseDuration))
                .SetLoops(Mathf.Max(1, cfg.barPulseCount) * 2, LoopType.Yoyo)
                .SetEase(Ease.OutQuad)
                .SetLink(_comboBar.gameObject)
                .OnComplete(() => { if (_comboBar != null) _comboBar.localScale = Vector3.one; });
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
            _levelValue = CreateLabel(canvasGo.transform, "LevelHud", "Level 1", 34f,
                new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(960f, 56f));
            BuildComboBar(canvasGo.transform);
            _comboDots = KebammComboDots.Create(canvasGo.transform); // above the bar, below the end screen

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

            _endLevelValue = CreateLabel(_endRoot.transform, "EndLevelValue", "Level 1", 56f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 600f), new Vector2(900f, 80f));

            // Three stars under the level text, above Merge Score. Middle one is larger.
            _stars[0] = CreateStar(_endRoot.transform, "Star1", new Vector2(-160f, 470f), 110f);
            _stars[1] = CreateStar(_endRoot.transform, "Star2", new Vector2(0f, 485f), 130f);
            _stars[2] = CreateStar(_endRoot.transform, "Star3", new Vector2(160f, 470f), 110f);
            // Merge, Monster and Combo lines, then Final (tightened 16 Nov 2026 to fit the Combo Score line).
            CreateLabel(_endRoot.transform, "EndMergeLabel", "Merge Score", 36f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 380f), new Vector2(900f, 56f));
            _endMergeValue = CreateLabel(_endRoot.transform, "EndMergeValue", "0", 48f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 322f), new Vector2(900f, 72f));
            CreateLabel(_endRoot.transform, "EndMonsterLabel", "Monster Score", 36f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 265f), new Vector2(900f, 56f));
            _endMonsterValue = CreateLabel(_endRoot.transform, "EndMonsterValue", "0", 48f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 207f), new Vector2(900f, 72f));
            CreateLabel(_endRoot.transform, "EndComboLabel", "Combo Score", 36f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(900f, 56f));
            _endComboValue = CreateLabel(_endRoot.transform, "EndComboValue", "0", 48f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 92f), new Vector2(900f, 72f));
            CreateLabel(_endRoot.transform, "EndFinalLabel", "Final Score", 36f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 25f), new Vector2(900f, 56f));
            _endFinalValue = CreateLabel(_endRoot.transform, "EndFinalValue", "0", 96f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(900f, 130f));
            // Rewards Earn: Art Document coin icon and coins earned this level, counting up from 0.
            _endCoinsLabel = CreateLabel(_endRoot.transform, "EndCoinsLabel", "Coins Earned", 36f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -225f), new Vector2(900f, 56f));
            _endCoinsLabel.color = CoinTextColor;
            _endCoinsValue = CreateLabel(_endRoot.transform, "EndCoinsValue", "+0", 60f,
                new Vector2(0.5f, 0.5f), new Vector2(40f, -290f), new Vector2(220f, 80f));
            _endCoinsValue.color = CoinTextColor;
            _endCoinsIcon = KebammPlaceholderUi.Box(_endRoot.transform, "EndCoinsIcon", Color.white, false);
            _endCoinsIcon.sprite = Resources.Load<Sprite>("Kebamm/UI/coin_icon");
            _endCoinsIcon.preserveAspect = true;
            KebammPlaceholderUi.Place(_endCoinsIcon.rectTransform, new Vector2(-110f, -290f), new Vector2(56f, 56f));
            _endNextLevelValue = CreateLabel(_endRoot.transform, "EndNextLevelValue", "Next: Level 1", 34f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -370f), new Vector2(900f, 56f));

            var buttonGo = new GameObject("NextLevel");
            buttonGo.transform.SetParent(_endRoot.transform, false);
            var buttonRect = buttonGo.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, -480f);
            buttonRect.sizeDelta = new Vector2(520f, 120f);
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.sprite = KebammVisualFactory.SquareSprite;
            buttonImage.color = new Color(0.22f, 0.48f, 0.82f, 1f);
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(OnNextLevel);
            _nextButton = buttonGo;

            var buttonLabel = CreateLabel(buttonGo.transform, "Label", "Next Level", 42f,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 120f));
            Stretch(buttonLabel.rectTransform);
            buttonLabel.raycastTarget = false;

            _endRoot.SetActive(false);
            Refresh();
            RefreshLevel();
        }

        static KebammGameManagerData Gm => KebammPrototypeBootstrap.Instance != null ? KebammPrototypeBootstrap.Instance.GameManagerData : null;

        public void Refresh()
        {
            RefreshHudScore();
            if (_ended)
                return; // end screen numbers are shown by the end sequence count-up
            if (_endMergeValue != null)
                _endMergeValue.text = KebammScore.MergeScore.ToString();
            if (_endMonsterValue != null)
                _endMonsterValue.text = KebammScore.MonsterScore.ToString();
            if (_endComboValue != null)
                _endComboValue.text = KebammScore.ComboScore.ToString();
            if (_endFinalValue != null)
                _endFinalValue.text = KebammScore.FinalScore.ToString();
        }

        // HUD score: rolls up from the shown number to the new one with a scale bump. Drops / resets snap.
        void RefreshHudScore()
        {
            if (_scoreValue == null)
                return;
            int target = KebammScore.FinalScore;
            var gm = Gm;
            float duration = gm != null ? gm.ScoreHudCountDuration : 0.4f;
            float bump = gm != null ? gm.ScoreHudBump : 0.2f;
            Transform tr = _scoreValue.transform;
            if (target <= _hudShown || duration <= 0f || !isActiveAndEnabled)
            {
                if (_hudCountTween != null) { _hudCountTween.Kill(); _hudCountTween = null; }
                _hudShown = target;
                _scoreValue.text = "Score: " + target;
                if (target == 0) { tr.DOKill(); tr.localScale = Vector3.one; }
                else BumpText(tr, bump);
                return;
            }
            if (_hudCountTween != null)
                _hudCountTween.Kill();
            _hudCountTween = DOTween.To(() => _hudShown, v => { _hudShown = v; _scoreValue.text = "Score: " + v; }, target, duration)
                .SetEase(Ease.OutQuad)
                .SetLink(_scoreValue.gameObject)
                .OnComplete(() => _hudCountTween = null);
            BumpText(tr, bump);
        }

        static void BumpText(Transform tr, float bump)
        {
            tr.DOKill(true);
            tr.localScale = Vector3.one;
            if (bump <= 0f)
                return;
            tr.DOPunchScale(Vector3.one * bump, 0.25f, 6, 0.8f)
                .SetLink(tr.gameObject)
                .OnKill(() => { if (tr != null) tr.localScale = Vector3.one; });
        }

        // End screen: a number rolls up from 0 to its value, then bumps.
        void CountEndNumber(TextMeshProUGUI label, int value)
        {
            if (label == null)
                return;
            var gm = Gm;
            float duration = gm != null ? gm.ScoreEndCountDuration : 0.6f;
            float bump = gm != null ? gm.ScoreEndBump : 0.15f;
            Transform tr = label.transform;
            tr.DOKill();
            tr.localScale = Vector3.one;
            if (duration <= 0f || value <= 0)
            {
                label.text = value.ToString();
                BumpText(tr, bump);
                return;
            }
            int shown = 0;
            label.text = "0";
            Tween tw = DOTween.To(() => shown, v => { shown = v; label.text = v.ToString(); }, value, duration)
                .SetEase(Ease.OutQuad)
                .SetLink(label.gameObject)
                .OnComplete(() => { if (label != null) BumpText(label.transform, bump); })
                .OnKill(() => { if (label != null) label.text = value.ToString(); });
            _endCountTweens.Add(tw);
        }

        void KillEndCounts()
        {
            for (int i = 0; i < _endCountTweens.Count; i++)
                if (_endCountTweens[i] != null) _endCountTweens[i].Kill();
            _endCountTweens.Clear();
        }

        public void RefreshLevel()
        {
            int current = 1;
            int next = 1;
            bool hasNext = false;
            KebammPrototypeBootstrap bootstrap = KebammPrototypeBootstrap.Instance;
            if (bootstrap != null)
            {
                current = bootstrap.CurrentLevelNumber;
                next = bootstrap.NextLevelNumber;
                hasNext = bootstrap.HasNextLevel;
            }

            if (_levelValue != null)
                _levelValue.text = "Level " + current;
            if (_endLevelValue != null)
                _endLevelValue.text = "Level " + current;
            if (_endNextLevelValue != null)
                _endNextLevelValue.text = hasNext ? "Next: Level " + next : "Next: Level " + current + " (last level)";
        }

        public void ShowEnd()
        {
            if (_ended || _endRoot == null)
                return;

            _ended = true;
            KebammScore.Lock();
            Refresh();
            RefreshLevel();
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
            PlayEndSequence();
            if (KebammPrototypeBootstrap.Instance != null)
                KebammPrototypeBootstrap.Instance.SetDroppingEnabled(false);
        }

        // GDD cue: Score Screen > 1s > Scores Numbers (Merge Score, Monster Score) > 1s > 3 Stars Logics > 2s > Next Button.
        // Timings come from KebammScoreScreenConfig; each delay counts from the previous step.
        /// <summary>
        /// Game Result - Stars: 1 star per category (monsters, obstacles, trebuchet) at >= its threshold; a category not in the level is a free star.
        /// Used for the score screen, the save and the Level Results row (debug console win goes through here too).
        /// </summary>
        static int CalculateStars(KebammPrototypeBootstrap boot)
        {
            var gm = boot != null ? boot.GameManagerData : null;
            if (gm == null)
                gm = Resources.Load<KebammGameManagerData>("Kebamm/GameManager");
            if (gm == null)
                gm = ScriptableObject.CreateInstance<KebammGameManagerData>();
            int mKilled = KebammScore.MonstersDestroyed, mTotal = boot != null ? boot.MonstersInLevel : 0;
            int oKilled = boot != null ? boot.ObstaclesDestroyed : 0, oTotal = boot != null ? boot.ObstaclesInLevel : 0;
            int tKilled = boot != null ? boot.TrebuchetsDestroyed : 0, tTotal = boot != null ? boot.TrebuchetsInLevel : 0;
            var lv = boot != null ? boot.CurrentLevel : null;
            float mThr = lv != null && lv.starMonsterThreshold >= 0f ? Mathf.Clamp01(lv.starMonsterThreshold) : gm.StarMonsterThreshold;
            float oThr = lv != null && lv.starObstacleThreshold >= 0f ? Mathf.Clamp01(lv.starObstacleThreshold) : gm.StarObstacleThreshold;
            float tThr = lv != null && lv.starTrebuchetThreshold >= 0f ? Mathf.Clamp01(lv.starTrebuchetThreshold) : gm.StarTrebuchetThreshold;
            int m = KebammGameManagerData.CategoryStar(mKilled, mTotal, mThr);
            int o = KebammGameManagerData.CategoryStar(oKilled, oTotal, oThr);
            int tr = KebammGameManagerData.CategoryStar(tKilled, tTotal, tThr);
            int earned = m + o + tr;
            Debug.Log($"[Kebamm] Stars earned: {earned} (monsters {mKilled}/{mTotal} -> {m}{(mTotal <= 0 ? " free" : "")}, obstacles {oKilled}/{oTotal} -> {o}{(oTotal <= 0 ? " free" : "")}, trebuchet {tKilled}/{tTotal} -> {tr}{(tTotal <= 0 ? " free" : "")}; thresholds {mThr * 100f:0.#}% / {oThr * 100f:0.#}% / {tThr * 100f:0.#}%)");
            return earned;
        }

        /// <summary>Notion 'Game Reward - Tabulation': coins from kills, stars and merge score (Game Manager tunables).</summary>
        static int CalculateCoins(KebammPrototypeBootstrap boot, int stars)
        {
            var gm = boot != null ? boot.GameManagerData : null;
            if (gm == null)
                gm = Resources.Load<KebammGameManagerData>("Kebamm/GameManager");
            if (gm == null)
                gm = ScriptableObject.CreateInstance<KebammGameManagerData>();
            int kills = KebammScore.MonstersDestroyed;
            int merge = KebammScore.MergeScore;
            int coins = gm.CoinsFor(kills, stars, merge);
            Debug.Log($"[Kebamm] Coins earned: {coins} (kills {kills} x {gm.CoinPerKill} + stars {stars} x {gm.CoinPerStar} + merge {merge} / {gm.MergePointsPerCoin} = {coins}; cap {gm.CoinCap})");
            return coins;
        }

        void PlayEndSequence()
        {
            KillEndSequence();
            KebammScoreScreenConfig config = KebammScoreScreenConfig.Load();
            // Save at score-screen display time (stars and coins are final here; the animation only shows them).
            var boot = KebammPrototypeBootstrap.Instance;
            int earned = CalculateStars(boot);
            int coins = CalculateCoins(boot, earned);
            LastCoinsEarned = coins;
            if (boot != null)
            {
                bool debugWin = KebammSaveSystem.SkipNextResult;
                KebammSaveSystem.RecordResult(boot.CurrentLevelNumber, earned, KebammScore.FinalScore, boot.NextLevelNumber, coins);
                if (!debugWin)
                    KebammLevelResults.Record(boot.CurrentLevelNumber, earned);
            }

            SetScoreNumbersVisible(false);
            if (_nextButton != null)
                _nextButton.SetActive(false);
            SetFinalScoreVisible(false);
            SetCoinsVisible(false);
            PrepareStars(earned);

            // Merge + Monster > 1s > Final Score > 0.25s > Rewards Earn (count-up) > stars > Next button.
            _endSequence = DOTween.Sequence()
                .AppendInterval(config.scoreNumbersDelay)
                .AppendCallback(() =>
                {
                    SetScoreNumbersVisible(true);
                    CountEndNumber(_endMergeValue, KebammScore.MergeScore);
                    CountEndNumber(_endMonsterValue, KebammScore.MonsterScore);
                    CountEndNumber(_endComboValue, KebammScore.ComboScore);
                })
                .AppendInterval(config.finalScoreDelay)
                .AppendCallback(() =>
                {
                    SetFinalScoreVisible(true);
                    CountEndNumber(_endFinalValue, KebammScore.FinalScore);
                })
                .AppendInterval(config.rewardDelay)
                .AppendCallback(() => PlayCoinCount(coins, config.rewardCountDuration))
                .AppendInterval(config.starsDelay)
                .AppendCallback(() => PopStars(earned, config))
                .AppendInterval(config.nextButtonDelay)
                .AppendCallback(() =>
                {
                    if (_nextButton != null)
                        _nextButton.SetActive(true);
                })
                .SetLink(_endRoot);
        }

        void KillEndSequence()
        {
            if (_endSequence != null)
            {
                _endSequence.Kill();
                _endSequence = null;
            }
            KillCoinCount();
            KillEndCounts();
        }

        void KillCoinCount()
        {
            if (_coinCountTween != null)
            {
                _coinCountTween.Kill();
                _coinCountTween = null;
            }
            if (_endCoinsValue != null)
                _endCoinsValue.transform.DOKill();
        }

        void SetFinalScoreVisible(bool visible)
        {
            if (_endFinalValue != null)
                _endFinalValue.gameObject.SetActive(visible);
        }

        void SetCoinsVisible(bool visible)
        {
            if (_endCoinsIcon != null)
                _endCoinsIcon.gameObject.SetActive(visible);
            if (_endCoinsLabel != null)
                _endCoinsLabel.gameObject.SetActive(visible);
            if (_endCoinsValue != null)
                _endCoinsValue.gameObject.SetActive(visible);
        }

        // Rewards Earn: shows the coin number and ticks it up from 0, with a small punch when it lands.
        void PlayCoinCount(int coins, float duration)
        {
            KillCoinCount();
            SetCoinsVisible(true);
            if (_endCoinsValue == null)
                return;
            Transform tr = _endCoinsValue.transform;
            tr.localScale = Vector3.one;
            if (duration <= 0f || coins <= 0)
            {
                _endCoinsValue.text = "+" + coins;
                return;
            }
            int shown = 0;
            _endCoinsValue.text = "+0";
            _coinCountTween = DOTween.To(() => shown, v => { shown = v; _endCoinsValue.text = "+" + v; }, coins, duration)
                .SetEase(Ease.OutQuad)
                .SetLink(_endCoinsValue.gameObject)
                .OnComplete(() =>
                {
                    _coinCountTween = null;
                    if (_endCoinsValue == null)
                        return;
                    _endCoinsValue.text = "+" + coins;
                    _endCoinsValue.transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 6, 0.8f)
                        .SetLink(_endCoinsValue.gameObject)
                        .OnKill(() => { if (_endCoinsValue != null) _endCoinsValue.transform.localScale = Vector3.one; });
                })
                .OnKill(() => { if (_endCoinsValue != null) _endCoinsValue.text = "+" + coins; });
        }

        void SetScoreNumbersVisible(bool visible)
        {
            if (_endMergeValue != null)
                _endMergeValue.gameObject.SetActive(visible);
            if (_endMonsterValue != null)
                _endMonsterValue.gameObject.SetActive(visible);
            if (_endComboValue != null)
                _endComboValue.gameObject.SetActive(visible);
        }

        // _stars[] are the filled Star Resource overlays. The empty base under each always stays visible;
        // every overlay starts hidden at scale 0 and only earned ones pop in on top. No tint.
        void PrepareStars(int earned)
        {
            KillStarTweens();
            KebammScoreScreenConfig config = KebammScoreScreenConfig.Load();
            for (int i = 0; i < _stars.Length; i++)
            {
                Image star = _stars[i];
                if (star == null)
                    continue;

                star.sprite = StarSprite;
                star.color = Color.white;
                star.transform.localScale = Vector3.zero;
            }
        }

        void PopStars(int earned, KebammScoreScreenConfig config)
        {
            for (int i = 0; i < _stars.Length && i < earned; i++)
            {
                Image star = _stars[i];
                if (star == null)
                    continue;

                Transform tr = star.transform;
                tr.DOKill();
                tr.localScale = Vector3.zero;
                tr.DOScale(1f, config.starPopDuration)
                    .SetDelay(config.starPopStagger * i)
                    .SetEase(Ease.OutBack)
                    .SetLink(star.gameObject)
                    .OnKill(() =>
                    {
                        if (tr != null)
                            tr.localScale = Vector3.one;
                    });
            }
        }
        void KillStarTweens()
        {
            for (int i = 0; i < _stars.Length; i++)
            {
                if (_stars[i] != null)
                    _stars[i].transform.DOKill();
            }
        }

        void ResetStars()
        {
            KillStarTweens();
            for (int i = 0; i < _stars.Length; i++)
            {
                if (_stars[i] == null)
                    continue;
                // Hide the filled overlay; the empty base (parent) stays at full size.
                _stars[i].transform.localScale = Vector3.zero;
                _stars[i].sprite = StarSprite;
                _stars[i].color = Color.white;
            }
        }

        Image CreateStar(Transform parent, string starName, Vector2 anchored, float size)
        {
            var go = new GameObject(starName);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchored;
            rect.sizeDelta = new Vector2(size, size);
            // Base layer: Star Resource Empty, always visible at full size.
            var baseImage = go.AddComponent<Image>();
            baseImage.sprite = EmptyStarSprite;
            baseImage.preserveAspect = true;
            baseImage.color = Color.white;
            baseImage.raycastTarget = false;

            // Top layer: filled Star Resource as a child (draws above the base), same rect, hidden until popped.
            var fillGo = new GameObject(starName + "Filled");
            fillGo.transform.SetParent(go.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0.5f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.localScale = Vector3.zero;
            var image = fillGo.AddComponent<Image>();
            image.sprite = StarSprite;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        // Loads Resources/Kebamm/UI/star_icon as a Sprite. If it was imported as a plain texture,
        // wrap it in a sprite; if it is missing, draw a white star at runtime.
        static Sprite StarSprite
        {
            get
            {
                if (_starSprite != null)
                    return _starSprite;

                _starSprite = Resources.Load<Sprite>(StarSpritePath);
                if (_starSprite == null)
                {
                    Texture2D tex = Resources.Load<Texture2D>(StarSpritePath);
                    if (tex != null)
                        _starSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
                if (_starSprite == null)
                {
                    Debug.LogWarning("[Kebamm] star_icon missing from Resources/Kebamm/UI; drawing a star at runtime.");
                    _starSprite = BuildStarSprite(128);
                }
                return _starSprite;
            }
        }

        static Sprite BuildStarSprite(int size)
        {
            // Five-pointed star, point up, same proportions as star_placeholder.png. 4x4 supersampling for soft edges.
            var points = new Vector2[10];
            float outer = size * 0.47f;
            float inner = outer * 0.48f;
            var center = new Vector2(size * 0.5f, size * 0.5f - size * 0.025f);
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? outer : inner;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            const int samples = 4;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < samples; sy++)
                    for (int sx = 0; sx < samples; sx++)
                    {
                        var p = new Vector2(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples);
                        if (InsidePolygon(points, p))
                            hits++;
                    }
                    byte alpha = (byte)(255 * hits / (samples * samples));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static bool InsidePolygon(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        public void HideEnd()
        {
            _ended = false;
            KillEndSequence();
            Refresh();
            ResetStars();
            SetScoreNumbersVisible(true);
            SetFinalScoreVisible(true);
            SetCoinsVisible(true);
            if (_nextButton != null)
                _nextButton.SetActive(true);
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
            {
                KebammPrototypeBootstrap.Instance.AdvanceLevel();
                // Advance first so ResetSession respawns monsters for the new level.
                KebammPrototypeBootstrap.Instance.ResetSession();
            }
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
                Debug.LogWarning("[Kebamm] No built-in font for the score HUD.");
                return null;
            }

            _font = TMP_FontAsset.CreateFontAsset(builtin);
            return _font;
        }
    }
}