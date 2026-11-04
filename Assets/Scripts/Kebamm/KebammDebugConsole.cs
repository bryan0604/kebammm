#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Kebamm
{
    /// <summary>
    /// Developer cheat console (GDD: Debug Console). Backquote opens/closes on desktop/Editor,
    /// three-finger hold on mobile: 3+ fingers currently in the top 37.5% of the screen, held for more than 5 s.
    /// Compiled only in the Editor and development builds.
    /// Commands: jump N, next, prev, restart, win, save, deletesave [confirm], unlockall, setstars N S, help.
    /// </summary>
    public class KebammDebugConsole : MonoBehaviour
    {
        const float PanelHeight = 300f;
        const float SlideDuration = 0.22f;
        const int MaxLines = 5;
        const int HoldFingers = 3;
        const float HoldTopFraction = 0.375f; // top 1.5/4 of the whole screen
        const float DefaultHoldSeconds = 5f;  // fallback when Game Manager data is missing
        // Must be held for more than this (Game Manager debugConsoleHoldSeconds, remote debug_console_hold_seconds).
        static float HoldSeconds => KebammPrototypeBootstrap.Instance != null && KebammPrototypeBootstrap.Instance.GameManagerData != null
            ? KebammPrototypeBootstrap.Instance.GameManagerData.DebugConsoleHoldSeconds : DefaultHoldSeconds;

        RectTransform _panel;
        GameObject _blocker;
        InputField _input;
        Text _output;
        readonly List<string> _lines = new List<string>();
        bool _open;
        float _holdStart = -1f;
        bool _holdFired;

        public static bool IsOpen { get; private set; }

        // URP's Rendering Debugger opens on a 3-finger double tap in development builds and grabs the same gesture.
        // Turn its runtime UI off so the 3-finger hold always means the Kebamm console.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void DisableRenderingDebuggerUi()
        {
            UnityEngine.Rendering.DebugManager.instance.enableRuntimeUI = false;
        }

        public void Build()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("DebugConsoleCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1100; // above the door transition (1000)
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Invisible full-screen blocker so menu / score-screen buttons can't be clicked while open.
            _blocker = new GameObject("InputBlocker");
            _blocker.transform.SetParent(canvasGo.transform, false);
            var blockRect = _blocker.AddComponent<RectTransform>();
            blockRect.anchorMin = Vector2.zero;
            blockRect.anchorMax = Vector2.one;
            blockRect.offsetMin = Vector2.zero;
            blockRect.offsetMax = Vector2.zero;
            var blockImg = _blocker.AddComponent<Image>();
            blockImg.color = new Color(0f, 0f, 0f, 0f);
            blockImg.raycastTarget = true;
            _blocker.SetActive(false);

            var panelGo = new GameObject("ConsolePanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            _panel = panelGo.AddComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0f, 1f);
            _panel.anchorMax = new Vector2(1f, 1f);
            _panel.pivot = new Vector2(0.5f, 1f);
            _panel.sizeDelta = new Vector2(0f, PanelHeight);
            _panel.anchoredPosition = new Vector2(0f, PanelHeight);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.04f, 0.10f, 0.88f);

            var outGo = new GameObject("Output");
            outGo.transform.SetParent(_panel, false);
            var outRect = outGo.AddComponent<RectTransform>();
            outRect.anchorMin = Vector2.zero;
            outRect.anchorMax = Vector2.one;
            outRect.offsetMin = new Vector2(24f, 92f);
            outRect.offsetMax = new Vector2(-24f, -12f);
            _output = outGo.AddComponent<Text>();
            _output.font = font;
            _output.fontSize = 28;
            _output.color = new Color(0.75f, 1f, 0.78f);
            _output.alignment = TextAnchor.LowerLeft;
            _output.horizontalOverflow = HorizontalWrapMode.Wrap;
            _output.verticalOverflow = VerticalWrapMode.Truncate;
            _output.raycastTarget = false;

            var inGo = new GameObject("Input");
            inGo.transform.SetParent(_panel, false);
            var inRect = inGo.AddComponent<RectTransform>();
            inRect.anchorMin = new Vector2(0f, 0f);
            inRect.anchorMax = new Vector2(1f, 0f);
            inRect.pivot = new Vector2(0.5f, 0f);
            inRect.anchoredPosition = new Vector2(0f, 16f);
            inRect.sizeDelta = new Vector2(-40f, 64f);
            var inImg = inGo.AddComponent<Image>();
            inImg.color = new Color(1f, 1f, 1f, 0.12f);

            Text text = CreateInputText(inGo.transform, "Text", font, Color.white);
            text.supportRichText = false;
            Text placeholder = CreateInputText(inGo.transform, "Placeholder", font, new Color(1f, 1f, 1f, 0.35f));
            placeholder.text = "type help";
            placeholder.fontStyle = FontStyle.Italic;

            _input = inGo.AddComponent<InputField>();
            _input.textComponent = text;
            _input.placeholder = placeholder;
            _input.lineType = InputField.LineType.SingleLine;
            _input.targetGraphic = inImg;
            // The backquote that toggles the console never ends up in the text.
            _input.onValidateInput = (s, i, c) => c == '`' ? '\0' : c;
            _input.onEndEdit.AddListener(OnEndEdit);

            panelGo.SetActive(false);
        }

        static Text CreateInputText(Transform parent, string name, Font font, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(14f, 4f);
            rect.offsetMax = new Vector2(-14f, -4f);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = 30;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        void OnDestroy()
        {
            if (_open)
            {
                IsOpen = false;
                KebammPrototypeBootstrap.GameplayInputBlocked = false;
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.backquoteKey.wasPressedThisFrame)
                Toggle();
            else if (_open && kb != null && kb.escapeKey.wasPressedThisFrame)
                Close();

            UpdateThreeFingerHold();
        }

        // Three-finger hold: counts the touches that are down and currently inside the top band of the screen
        // (no start-point or drift rule, other touches are ignored). 3+ of them for more than HoldSeconds toggles
        // the console once; drop below 3 in the band to re-arm. Input System touch y is 0 at the bottom.
        void UpdateThreeFingerHold()
        {
            var ts = Touchscreen.current;
            if (ts == null)
                return;
            float bandBottom = Screen.height * (1f - HoldTopFraction);
            int inBand = 0;
            foreach (var touch in ts.touches)
            {
                if (!touch.press.isPressed)
                    continue;
                if (touch.position.ReadValue().y >= bandBottom)
                    inBand++;
            }

            if (inBand < HoldFingers)
            {
                _holdStart = -1f;
                _holdFired = false;
                return;
            }
            if (_holdFired)
                return;
            if (_holdStart < 0f)
            {
                _holdStart = Time.unscaledTime;
                Debug.Log($"[Kebamm] Console hold started ({inBand} fingers in the top band).");
            }
            else if (Time.unscaledTime - _holdStart > HoldSeconds)
            {
                Debug.Log($"[Kebamm] Debug console opened after {Time.unscaledTime - _holdStart:0.00}s hold (setting={HoldSeconds:0.##})");
                _holdFired = true;
                _holdStart = -1f;
                Toggle();
            }
        }

        void Toggle()
        {
            if (_open)
                Close();
            else
                Open();
        }

        void Open()
        {
            if (_panel == null)
                return;
            _open = true;
            IsOpen = true;
            KebammPrototypeBootstrap.GameplayInputBlocked = true;
            _blocker.SetActive(true);
            _panel.gameObject.SetActive(true);
            _panel.DOKill();
            _panel.DOAnchorPosY(0f, SlideDuration).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(_panel.gameObject);
            Debug.Log("[Kebamm] Debug console opened.");
            if (_lines.Count == 0)
                Print("Kebamm debug console. Type help.");
            _input.text = string.Empty;
            _input.ActivateInputField();
        }

        void Close()
        {
            if (_panel == null)
                return;
            _open = false;
            IsOpen = false;
            KebammPrototypeBootstrap.GameplayInputBlocked = false;
            _blocker.SetActive(false);
            _input.DeactivateInputField();
            _panel.DOKill();
            _panel.DOAnchorPosY(PanelHeight, SlideDuration).SetEase(Ease.InCubic).SetUpdate(true).SetLink(_panel.gameObject)
                .OnComplete(() => _panel.gameObject.SetActive(false));
        }

        void OnEndEdit(string value)
        {
            if (!_open)
                return;
            var kb = Keyboard.current;
            bool enter = kb != null && (kb.enterKey.isPressed || kb.numpadEnterKey.isPressed);
            bool touchDone = _input.touchScreenKeyboard != null && _input.touchScreenKeyboard.status == TouchScreenKeyboard.Status.Done;
            if (!enter && !touchDone)
                return;

            _input.text = string.Empty;
            Execute(value != null ? value.Trim() : string.Empty);
            if (_open)
                _input.ActivateInputField();
        }

        void Execute(string cmd)
        {
            if (cmd.Length == 0)
                return;
            Print("> " + cmd);

            var b = KebammPrototypeBootstrap.Instance;
            if (b == null)
            {
                Error("Game is not running.");
                return;
            }

            string[] parts = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string name = parts[0].ToLowerInvariant();
            switch (name)
            {
                case "help":
                    Print("jump N | next | prev | restart | win | help");
                    Print("save | deletesave | unlockall | setstars N S");
                    Print("Levels: " + b.LevelNumbersText);
                    break;

                case "jump":
                    if (parts.Length != 2 || !int.TryParse(parts[1], out int n))
                    {
                        Error("Usage: jump N");
                        return;
                    }
                    if (!b.TryGetLevelIndex(n, out int index))
                    {
                        Error($"No active level {n}. Levels: {b.LevelNumbersText}");
                        return;
                    }
                    GoTo(b, index, $"Jumping to level {n}");
                    break;

                case "next":
                    if (!b.HasNextLevel)
                    {
                        Error("Already at the last level.");
                        return;
                    }
                    GoTo(b, b.CurrentLevelIndex + 1, $"Next: level {b.NextLevelNumber}");
                    break;

                case "prev":
                    if (b.CurrentLevelIndex <= 0)
                    {
                        Error("Already at the first level.");
                        return;
                    }
                    GoTo(b, b.CurrentLevelIndex - 1, "Previous level");
                    break;

                case "restart":
                    GoTo(b, b.CurrentLevelIndex, $"Restarting level {b.CurrentLevelNumber}");
                    break;

                case "win":
                    var hud = KebammScoreHud.Instance;
                    if (b.InMainMenu)
                        Error("Not in a level. Use jump N first.");
                    else if (hud == null)
                        Error("Score screen not available.");
                    else if (hud.IsEndVisible)
                        Error("Score screen already showing.");
                    else
                    {
                        Print($"Win: score screen for level {b.CurrentLevelNumber} (counts as a real result)");
                        // Q4-A: a debug win records a real result (stars + unlock) like a normal score screen.
                        Debug.Log($"[KebammSave] Debug win on level {b.CurrentLevelNumber}: recording as a real result.");
                        hud.ShowEnd();
                        Close();
                    }
                    break;

                case "save":
                    Print(KebammSaveSystem.Summary());
                    var levelLines = KebammSaveSystem.LevelLines();
                    Print(levelLines.Count == 0 ? "No level results yet." : string.Join(" | ", levelLines));
                    Debug.Log("[KebammSave] Console save: " + KebammSaveSystem.Summary() + " | " + string.Join(" | ", levelLines));
                    break;

                case "deletesave":
                    if (parts.Length == 2 && parts[1].ToLowerInvariant() == "confirm")
                    {
                        if (KebammDoorTransition.IsPlaying)
                        {
                            Error("A transition is running. Try again.");
                            return;
                        }
                        b.DeleteSaveAndRestart();
                        Print("Save deleted. Fresh save started at level 1.");
                        Close();
                    }
                    else if (parts.Length == 1)
                    {
                        Print("<color=#FFD27A>This deletes all progress on this device.</color>");
                        Print("Type 'deletesave confirm' to delete.");
                    }
                    else
                        Error("Usage: deletesave confirm");
                    break;

                case "unlockall":
                    KebammSaveSystem.UnlockAll(b.LastLevelNumber);
                    Print($"Unlocked up to level {KebammSaveSystem.Data.progress.highestLevelUnlocked}.");
                    break;

                case "setstars":
                    if (parts.Length != 3 || !int.TryParse(parts[1], out int starLevel) || !int.TryParse(parts[2], out int stars))
                    {
                        Error("Usage: setstars N S (S = 0-3)");
                        return;
                    }
                    if (!b.TryGetLevelIndex(starLevel, out _))
                    {
                        Error($"No active level {starLevel}. Levels: {b.LevelNumbersText}");
                        return;
                    }
                    if (stars < 0 || stars > 3)
                    {
                        Error("Stars must be 0-3.");
                        return;
                    }
                    KebammSaveSystem.SetStars(starLevel, stars);
                    Print($"Level {starLevel} stars set to {stars}.");
                    break;

                default:
                    Error($"Unknown command '{name}'. Type help.");
                    break;
            }
        }

        void GoTo(KebammPrototypeBootstrap b, int index, string message)
        {
            if (KebammDoorTransition.IsPlaying || !KebammDoorTransition.Play(() => b.JumpToLevelIndex(index)))
            {
                Error("A transition is already running. Try again.");
                return;
            }
            Print(message);
            Close();
        }

        void Error(string message)
        {
            Print("<color=#FF7A7A>Error: " + message + "</color>");
        }

        void Print(string line)
        {
            _lines.Add(line);
            while (_lines.Count > MaxLines)
                _lines.RemoveAt(0);
            if (_output != null)
                _output.text = string.Join("\n", _lines);
        }
    }
}
#endif