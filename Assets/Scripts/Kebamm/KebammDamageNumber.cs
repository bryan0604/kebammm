using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// World-space damage floater. Shows the positive damage the monster lost and lets DOTween rise + fade it.
    /// </summary>
    public class KebammDamageNumber : MonoBehaviour
    {
        const float RiseDistance = 0.9f;
        const float Duration = 0.7f;

        static TMP_FontAsset _font;
        static bool _fontTried;

        public static void Spawn(Bounds spriteBounds, float amount)
        {
            var go = new GameObject("DamageNumber");
            go.transform.position = new Vector3(spriteBounds.center.x, spriteBounds.max.y + 0.12f, 0f);
            go.AddComponent<KebammDamageNumber>().Play(amount);
        }

        void Play(float amount)
        {
            var tmp = gameObject.AddComponent<TextMeshPro>();
            tmp.text = amount.ToString("0");
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 36f;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 1.5f);

            TMP_FontAsset font = GetFont();
            if (font != null)
                tmp.font = font;

            var renderer = tmp.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sortingOrder = 50;

            // World TMP at font size 36 is several units tall; scale it down for the orthographic playfield.
            transform.localScale = Vector3.one * 0.08f;

            transform.DOMoveY(transform.position.y + RiseDistance, Duration).SetEase(Ease.OutQuad).SetLink(gameObject);

            Color color = tmp.color;
            DOTween.To(() => color.a, a =>
            {
                color.a = a;
                tmp.color = color;
            }, 0f, Duration).SetEase(Ease.InQuad).SetLink(gameObject).OnComplete(() =>
            {
                if (this != null)
                    Destroy(gameObject);
            });
        }

        internal static TMP_FontAsset GetFont()
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
                Debug.LogWarning("[Kebamm] No built-in font for damage numbers.");
                return null;
            }

            _font = TMP_FontAsset.CreateFontAsset(builtin);
            return _font;
        }
    }
}
