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

        // Placeholder white star, tinted grey (unearned) or yellow (earned) from score_screen_config. Replace at polish.
        const string StarSpritePath = "Kebamm/UI/star_placeholder";
        static Sprite _starSprite;
        GameObject _nextButton;
        Sequence _endSequence;
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
            KillEndSequence();
            KillStarTweens();
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
            _levelValue = CreateLabel(canvasGo.transform, "LevelHud", "Level 1", 34f,
                new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(960f, 56f));

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
            _endNextLevelValue = CreateLabel(_endRoot.transform, "EndNextLevelValue", "Next: Level 1", 34f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -236f), new Vector2(900f, 56f));

            var buttonGo = new GameObject("NextLevel");
            buttonGo.transform.SetParent(_endRoot.transform, false);
            var buttonRect = buttonGo.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, -350f);
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
        void PlayEndSequence()
        {
            KillEndSequence();
            KebammScoreScreenConfig config = KebammScoreScreenConfig.Load();
            int earned = config.StarsForKills(KebammScore.MonstersDestroyed);
            Debug.Log($"[Kebamm] Stars earned: {earned} (monsters destroyed {KebammScore.MonstersDestroyed})");

            SetScoreNumbersVisible(false);
            if (_nextButton != null)
                _nextButton.SetActive(false);
            PrepareStars(earned);

            _endSequence = DOTween.Sequence()
                .AppendInterval(config.scoreNumbersDelay)
                .AppendCallback(() => SetScoreNumbersVisible(true))
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
        }

        void SetScoreNumbersVisible(bool visible)
        {
            if (_endMergeValue != null)
                _endMergeValue.gameObject.SetActive(visible);
            if (_endMonsterValue != null)
                _endMonsterValue.gameObject.SetActive(visible);
        }

        // Unearned stars show grey straight away; earned stars turn yellow and wait at scale 0 for the pop.
        void PrepareStars(int earned)
        {
            KillStarTweens();
            KebammScoreScreenConfig config = KebammScoreScreenConfig.Load();
            for (int i = 0; i < _stars.Length; i++)
            {
                Image star = _stars[i];
                if (star == null)
                    continue;

                bool isEarned = i < earned;
                star.color = isEarned ? config.starEarnedColor : config.starUnearnedColor;
                star.transform.localScale = isEarned ? Vector3.zero : Vector3.one;
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
                _stars[i].transform.localScale = Vector3.one;
                _stars[i].color = KebammScoreScreenConfig.Load().starUnearnedColor;
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
            var image = go.AddComponent<Image>();
            image.sprite = StarSprite;
            image.preserveAspect = true;
            image.color = KebammScoreScreenConfig.Load().starUnearnedColor;
            image.raycastTarget = false;
            return image;
        }

        // Loads Resources/Kebamm/UI/star_placeholder as a Sprite. If it was imported as a plain texture,
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
                    Debug.LogWarning("[Kebamm] star_placeholder missing from Resources/Kebamm/UI; drawing a star at runtime.");
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
            ResetStars();
            SetScoreNumbersVisible(true);
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
                KebammPrototypeBootstrap.Instance.ResetSession();
                KebammPrototypeBootstrap.Instance.AdvanceLevel();
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