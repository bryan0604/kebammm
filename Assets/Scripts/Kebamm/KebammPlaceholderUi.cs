using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Kebamm
{
    /// <summary>
    /// PLACEHOLDER UI helpers for the Pause Menu, Settings popup, confirm popups and player panel
    /// (flat boxes + TMP labels). White Tea will replace the look later.
    /// </summary>
    public static class KebammPlaceholderUi
    {
        public static readonly Color PanelFill = new Color(0.96f, 0.94f, 1f);
        public static readonly Color PanelText = new Color(0.30f, 0.15f, 0.55f);
        public static readonly Color Purple = new Color(0.45f, 0.30f, 0.75f);
        public static readonly Color Red = new Color(0.85f, 0.27f, 0.32f);
        public static readonly Color Green = new Color(0.30f, 0.70f, 0.35f);
        public static readonly Color Grey = new Color(0.55f, 0.55f, 0.60f);

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        public static void Place(RectTransform rect, Vector2 anchored, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchored;
            rect.sizeDelta = size;
        }

        public static Image Box(Transform parent, string name, Color colour, bool raycast)
        {
            var rect = Rect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = KebammVisualFactory.SquareSprite;
            image.color = colour;
            image.raycastTarget = raycast;
            return image;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Vector2 anchored, Vector2 box, Color colour)
        {
            var rect = Rect(parent, name);
            Place(rect, anchored, box);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = colour;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>Full-screen dimmer (blocks clicks behind) with a centred panel; returns the root (inactive).</summary>
        public static GameObject Popup(Transform parent, string name, Vector2 panelSize, string title, float titleY)
        {
            var root = Rect(parent, name);
            Stretch(root);
            Stretch(Box(root, "Dimmer", new Color(0f, 0f, 0f, 0.7f), true).rectTransform);
            Place(Box(root, "Panel", PanelFill, true).rectTransform, Vector2.zero, panelSize);
            Label(root, "Title", title, 60f, new Vector2(0f, titleY), new Vector2(panelSize.x - 60f, 90f), PanelText).fontStyle = FontStyles.Bold;
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        public static Button MakeButton(Transform parent, string name, string text, Color fill, Vector2 anchored, Vector2 size, UnityAction onClick, float fontSize = 44f)
        {
            var image = Box(parent, name, fill, true);
            Place(image.rectTransform, anchored, size);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colours = button.colors;
            colours.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            button.colors = colours;
            if (onClick != null)
                button.onClick.AddListener(onClick);
            var label = Label(image.transform, "Label", text, fontSize, Vector2.zero, size, Color.white);
            Stretch(label.rectTransform);
            label.fontStyle = FontStyles.Bold;
            return button;
        }

        /// <summary>Shows "Sound: ON" (green) or "Sound: OFF" (grey) on a toggle button.</summary>
        public static void SetToggle(Button button, string name, bool on)
        {
            if (button == null)
                return;
            ((Image)button.targetGraphic).color = on ? Green : Grey;
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = $"{name}: {(on ? "ON" : "OFF")}";
        }
    }
}
