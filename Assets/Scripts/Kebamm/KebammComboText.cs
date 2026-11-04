using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Kebamm
{
    /// <summary>World-space "Combo N!" float text: DOTween pop, rise and fade (settings in KebammComboConfig).</summary>
    public class KebammComboText : MonoBehaviour
    {
        public static void Spawn(Vector3 position, string text, KebammComboConfig cfg)
        {
            var go = new GameObject("ComboText");
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.AddComponent<KebammComboText>().Play(text, cfg);
        }

        void Play(string text, KebammComboConfig cfg)
        {
            var tmp = gameObject.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = cfg.comboTextFontSize;
            tmp.color = cfg.comboTextColor;
            tmp.fontStyle = FontStyles.Bold;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.rectTransform.sizeDelta = new Vector2(8f, 1.5f);
            TMP_FontAsset font = KebammDamageNumber.GetFont();
            if (font != null)
                tmp.font = font;
            var renderer = tmp.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sortingOrder = 60;

            float scale = Mathf.Max(0.001f, cfg.comboTextScale);
            float duration = Mathf.Max(0.1f, cfg.comboTextDuration);
            float popTime = Mathf.Min(0.18f, duration * 0.3f);
            transform.localScale = Vector3.zero;
            DOTween.Sequence()
                .Append(transform.DOScale(scale * Mathf.Max(1f, cfg.comboTextPopScale), popTime).SetEase(Ease.OutBack))
                .Append(transform.DOScale(scale, popTime).SetEase(Ease.OutQuad))
                .SetLink(gameObject);
            transform.DOMoveY(transform.position.y + cfg.comboTextSpeed * duration, duration).SetEase(Ease.OutQuad).SetLink(gameObject);

            Color color = tmp.color;
            DOTween.To(() => color.a, a =>
            {
                color.a = a;
                tmp.color = color;
            }, 0f, duration * 0.5f).SetDelay(duration * 0.5f).SetEase(Ease.InQuad).SetLink(gameObject).OnComplete(() =>
            {
                if (this != null)
                    Destroy(gameObject);
            });
        }
    }
}
