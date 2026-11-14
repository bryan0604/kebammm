using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Kebamm
{
    /// <summary>
    /// Fine Tuning VFX: when a merge pays combo points, a few of our own glowing dots (soft bar-coloured glow with a
    /// white core, no imported asset) burst out from the merge, then curve into the combo bar. The bar fill and glow
    /// start when the first dot hits (KebammScoreHud). Pooled: PoolSize UI dots made once and reused, nothing is
    /// instantiated or destroyed during play. Values on the Game Manager asset (local tuning until approved).
    /// </summary>
    public class KebammComboDots : MonoBehaviour
    {
        const int PoolSize = 40;
        const int SpriteSize = 64;
        readonly List<RectTransform> _free = new List<RectTransform>();
        readonly List<RectTransform> _busy = new List<RectTransform>();
        RectTransform _root;
        static Sprite _glowSprite;

        public RectTransform Root => _root;

        public static KebammComboDots Create(Transform canvas)
        {
            var go = new GameObject("ComboDots");
            go.transform.SetParent(canvas, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var dots = go.AddComponent<KebammComboDots>();
            dots._root = rt;
            dots.BuildPool();
            return dots;
        }

        void BuildPool()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var dotGo = new GameObject("ComboDot");
                dotGo.transform.SetParent(_root, false);
                var dot = dotGo.AddComponent<RectTransform>();
                dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
                dot.pivot = new Vector2(0.5f, 0.5f);
                // Drop shadow first so it draws under the glow: dark soft blob offset down-right (elevation).
                var shadowGo = new GameObject("Shadow");
                shadowGo.transform.SetParent(dot, false);
                var shadow = shadowGo.AddComponent<RectTransform>();
                shadow.anchorMin = new Vector2(-0.05f, -0.05f);
                shadow.anchorMax = new Vector2(1.05f, 1.05f);
                shadow.offsetMin = shadow.offsetMax = Vector2.zero;
                var shadowImg = shadowGo.AddComponent<Image>();
                shadowImg.sprite = GlowSprite;
                shadowImg.raycastTarget = false;

                var glowGo = new GameObject("Glow");
                glowGo.transform.SetParent(dot, false);
                var glow = glowGo.AddComponent<RectTransform>();
                glow.anchorMin = Vector2.zero;
                glow.anchorMax = Vector2.one;
                glow.offsetMin = glow.offsetMax = Vector2.zero;
                var img = glowGo.AddComponent<Image>();
                img.sprite = GlowSprite;
                img.raycastTarget = false;

                var coreGo = new GameObject("Core");
                coreGo.transform.SetParent(dot, false);
                var core = coreGo.AddComponent<RectTransform>();
                core.anchorMin = new Vector2(0.3f, 0.3f);
                core.anchorMax = new Vector2(0.7f, 0.7f);
                core.offsetMin = core.offsetMax = Vector2.zero;
                var coreImg = coreGo.AddComponent<Image>();
                coreImg.sprite = GlowSprite;
                coreImg.color = Color.white;
                coreImg.raycastTarget = false;

                dotGo.SetActive(false);
                _free.Add(dot);
            }
        }

        /// <summary>
        /// Flies <paramref name="count"/> dots from <paramref name="from"/> to <paramref name="to"/> (both local to Root).
        /// <paramref name="onFirstArrive"/> runs once, when the first dot reaches the bar. False when nothing was spawned.
        /// </summary>
        public bool Fly(Vector2 from, Vector2 to, float targetSpreadX, int count, Color color, KebammGameManagerData gm, Action onFirstArrive)
        {
            if (count <= 0 || _free.Count == 0 || gm == null)
                return false;
            count = Mathf.Min(count, _free.Count);
            float size = gm.ComboDotSize;
            float burstRadius = gm.comboDotBurstRadius;
            float burstTime = gm.ComboDotBurstTime;
            float flightTime = gm.ComboDotFlightTime;
            float stagger = Mathf.Max(0f, gm.comboDotStagger);
            float curve = gm.comboDotCurve;
            bool arrived = false;
            float start = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            for (int i = 0; i < count; i++)
            {
                RectTransform dot = _free[_free.Count - 1];
                _free.RemoveAt(_free.Count - 1);
                _busy.Add(dot);
                dot.gameObject.SetActive(true);
                dot.SetAsLastSibling();
                dot.anchoredPosition = from;
                dot.sizeDelta = new Vector2(size, size);
                dot.localScale = Vector3.one * 0.4f;
                dot.Find("Glow").GetComponent<Image>().color = new Color(color.r, color.g, color.b, 1f);
                var sh = (RectTransform)dot.Find("Shadow");
                sh.GetComponent<Image>().color = new Color(0f, 0f, 0f, Mathf.Clamp01(gm.comboDotShadowAlpha));
                sh.anchoredPosition = new Vector2(gm.comboDotShadowOffset, -gm.comboDotShadowOffset);

                float ang = start + i * Mathf.PI * 2f / count + UnityEngine.Random.Range(-0.35f, 0.35f);
                Vector2 burst = from + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * burstRadius * UnityEngine.Random.Range(0.7f, 1.1f);
                Vector2 end = to + new Vector2(UnityEngine.Random.Range(-targetSpreadX, targetSpreadX), 0f);
                Vector2 dir = end - burst;
                Vector2 perp = dir.sqrMagnitude > 0.01f ? new Vector2(-dir.y, dir.x).normalized : Vector2.up;
                Vector2 ctrl = (burst + end) * 0.5f + perp * curve * ((i % 2 == 0) ? 1f : -1f) * UnityEngine.Random.Range(0.5f, 1f);

                RectTransform d = dot;
                DOTween.Sequence().SetTarget(d).SetLink(d.gameObject)
                    .Append(d.DOAnchorPos(burst, burstTime).SetEase(Ease.OutQuad))
                    .Join(d.DOScale(1f, burstTime).SetEase(Ease.OutBack))
                    .AppendInterval(i * stagger)
                    .Append(DOTween.To(() => 0f, t =>
                    {
                        float u = 1f - t;
                        d.anchoredPosition = u * u * burst + 2f * u * t * ctrl + t * t * end;
                    }, 1f, flightTime).SetEase(Ease.InQuad))
                    .Join(d.DOScale(0.6f, flightTime).SetEase(Ease.InQuad))
                    .OnComplete(() =>
                    {
                        Release(d);
                        if (!arrived)
                        {
                            arrived = true;
                            onFirstArrive?.Invoke();
                        }
                    });
            }
            return true;
        }

        /// <summary>Hides every flying dot (new attempt / reset). Their arrival callbacks do not run.</summary>
        public void Clear()
        {
            for (int i = _busy.Count - 1; i >= 0; i--)
            {
                RectTransform d = _busy[i];
                if (d == null)
                    continue;
                DOTween.Kill(d);
                d.DOKill();
                d.gameObject.SetActive(false);
                _free.Add(d);
            }
            _busy.Clear();
        }

        void Release(RectTransform d)
        {
            if (d == null)
                return;
            d.gameObject.SetActive(false);
            if (_busy.Remove(d))
                _free.Add(d);
        }

        /// <summary>Soft round glow: bright centre fading to transparent at the edge.</summary>
        static Sprite GlowSprite
        {
            get
            {
                if (_glowSprite != null)
                    return _glowSprite;
                var tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                float c = SpriteSize * 0.5f;
                for (int y = 0; y < SpriteSize; y++)
                    for (int x = 0; x < SpriteSize; x++)
                    {
                        float dx = (x + 0.5f - c) / c, dy = (y + 0.5f - c) / c;
                        float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                        float a = Mathf.Pow(1f - d, 2f);
                        if (d < 0.25f)
                            a = Mathf.Max(a, 1f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                    }
                tex.Apply();
                _glowSprite = Sprite.Create(tex, new Rect(0, 0, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f), 100f);
                return _glowSprite;
            }
        }
    }
}