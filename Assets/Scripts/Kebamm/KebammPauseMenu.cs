using UnityEngine;
using UnityEngine.UI;

namespace Kebamm
{
    /// <summary>
    /// PLACEHOLDER pause button (top right of the HUD) and Pause Menu (Notion: Game Pausing, Game Pause Menu).
    /// Pause = Time.timeScale 0 via KebammPrototypeBootstrap.PauseGame. The menu offers Resume,
    /// Sound / Music toggles (shared KebammAudio setting) and Quit Level (confirm popup, no result recorded).
    /// </summary>
    public class KebammPauseMenu : MonoBehaviour
    {
        public static KebammPauseMenu Instance { get; private set; }

        GameObject _pauseButton;
        GameObject _menuRoot;
        GameObject _confirmRoot;
        Button _soundButton;
        Button _musicButton;
        RectTransform _safe;
        Rect _lastSafeArea;

        void Awake() { Instance = this; }

        void OnDestroy()
        {
            KebammAudio.Changed -= RefreshToggles;
            if (Instance == this)
                Instance = null;
        }

        public void Build()
        {
            var canvasGo = new GameObject("PauseCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150; // above the score HUD (100), below the main menu (200)
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _safe = KebammPlaceholderUi.Rect(canvasGo.transform, "SafeArea");
            KebammPlaceholderUi.Stretch(_safe);
            ApplySafeArea();

            // Pause button, top right.
            var pause = KebammPlaceholderUi.MakeButton(_safe, "PauseButton", "II", KebammPlaceholderUi.Purple,
                Vector2.zero, new Vector2(120f, 120f), OnPausePressed, 56f);
            var pr = (RectTransform)pause.transform;
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(1f, 1f);
            pr.anchoredPosition = new Vector2(-30f, -30f);
            _pauseButton = pause.gameObject;
            _pauseButton.SetActive(false);

            // Pause Menu.
            _menuRoot = KebammPlaceholderUi.Popup(canvasGo.transform, "PauseMenu", new Vector2(820f, 960f), "Paused", 360f);
            var t = _menuRoot.transform;
            KebammPlaceholderUi.MakeButton(t, "ResumeButton", "Resume", KebammPlaceholderUi.Green, new Vector2(0f, 200f), new Vector2(560f, 120f), OnResume);
            _soundButton = KebammPlaceholderUi.MakeButton(t, "SoundToggle", "Sound", KebammPlaceholderUi.Green, new Vector2(0f, 50f), new Vector2(560f, 120f),
                () => KebammAudio.SetSound(!KebammAudio.SoundOn, "pause menu"));
            _musicButton = KebammPlaceholderUi.MakeButton(t, "MusicToggle", "Music", KebammPlaceholderUi.Green, new Vector2(0f, -100f), new Vector2(560f, 120f),
                () => KebammAudio.SetMusic(!KebammAudio.MusicOn, "pause menu"));
            KebammPlaceholderUi.MakeButton(t, "QuitLevelButton", "Quit Level", KebammPlaceholderUi.Red, new Vector2(0f, -290f), new Vector2(560f, 120f), OnQuitLevel);

            // Quit Level confirm.
            _confirmRoot = KebammPlaceholderUi.Popup(canvasGo.transform, "QuitLevelConfirm", new Vector2(820f, 620f), "Are you sure?", 200f);
            var c = _confirmRoot.transform;
            KebammPlaceholderUi.Label(c, "Note", "Quit this level? Progress in this level is lost.", 34f,
                new Vector2(0f, 100f), new Vector2(760f, 60f), KebammPlaceholderUi.PanelText);
            KebammPlaceholderUi.MakeButton(c, "YesButton", "Yes, quit", KebammPlaceholderUi.Red, new Vector2(0f, -40f), new Vector2(560f, 120f), OnQuitConfirmed);
            KebammPlaceholderUi.MakeButton(c, "NoButton", "No", KebammPlaceholderUi.Purple, new Vector2(0f, -190f), new Vector2(560f, 120f), OnQuitCancelled);

            KebammAudio.Changed += RefreshToggles;
            RefreshToggles();
        }

        void ApplySafeArea()
        {
            if (_safe == null || Screen.width <= 0 || Screen.height <= 0)
                return;
            Rect area = Screen.safeArea;
            if (area == _lastSafeArea)
                return;
            _lastSafeArea = area;
            _safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            _safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            _safe.offsetMin = _safe.offsetMax = Vector2.zero;
        }

        void Update()
        {
            ApplySafeArea();
            var b = KebammPrototypeBootstrap.Instance;
            bool canPause = b != null && b.CanPause;
            if (_pauseButton != null && _pauseButton.activeSelf != (canPause && !KebammPrototypeBootstrap.IsPaused))
                _pauseButton.SetActive(canPause && !KebammPrototypeBootstrap.IsPaused);
            // Safety: never stay frozen behind the main menu or the score screen (e.g. debug console jump / win).
            if (KebammPrototypeBootstrap.IsPaused && b != null && !canPause && !KebammDoorTransition.IsPlaying)
                b.ResumeGame("not in a level");
        }

        public void ShowMenu()
        {
            RefreshToggles();
            if (_confirmRoot != null) _confirmRoot.SetActive(false);
            if (_menuRoot != null) _menuRoot.SetActive(true);
        }

        public void HideAll()
        {
            if (_confirmRoot != null) _confirmRoot.SetActive(false);
            if (_menuRoot != null) _menuRoot.SetActive(false);
        }

        void RefreshToggles()
        {
            KebammPlaceholderUi.SetToggle(_soundButton, "Sound", KebammAudio.SoundOn);
            KebammPlaceholderUi.SetToggle(_musicButton, "Music", KebammAudio.MusicOn);
        }

        void OnPausePressed()
        {
            KebammPrototypeBootstrap.Instance?.PauseGame("pause button");
        }

        void OnResume()
        {
            KebammPrototypeBootstrap.Instance?.ResumeGame("resume button");
        }

        void OnQuitLevel()
        {
            Debug.Log("[Kebamm] Quit Level pressed - asking to confirm.");
            _menuRoot.SetActive(false);
            _confirmRoot.SetActive(true);
        }

        void OnQuitCancelled()
        {
            Debug.Log("[Kebamm] Quit Level cancelled - still paused.");
            _confirmRoot.SetActive(false);
            _menuRoot.SetActive(true);
        }

        void OnQuitConfirmed()
        {
            HideAll();
            var b = KebammPrototypeBootstrap.Instance;
            if (b == null)
                return;
            // Door transition runs on unscaled time; the level stays frozen while the doors close.
            if (!KebammDoorTransition.Play(b.QuitLevelToMenu))
                b.QuitLevelToMenu();
        }
    }
}
