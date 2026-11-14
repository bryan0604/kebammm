using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Fine Tuning VFX: gentle fireflies (Cartoon FX Remaster "CFXR3 Ambient Glows", assigned on the Game Manager asset)
    /// drifting just above the monster platform only. firefly_count whole prefabs (1 or 2) sit side by side along the
    /// platform, each covering its own slice so they never stack. Each prefab already spawns its own small dots; its big
    /// 30 x 10 box is resized to the slice (platform width / count) x firefly_band_height, and its emission is set to
    /// firefly_rate per second so only a few dots show. Made once with the ground and loops for the whole session;
    /// nothing is spawned during play. The pack's point light and CFXR_Effect script are removed (2D is unlit).
    /// </summary>
    public static class KebammFireflies
    {
        const int SortingOrder = -50; // above the background art (-100), behind obstacles (0) and monsters (1)

        public static void Build(Transform parent, KebammGameManagerData gm, float groundTop, float platformWidth)
        {
            if (gm == null || gm.fireflyPrefab == null || platformWidth <= 0f)
                return;
            int count = gm.FireflyCount;
            if (count <= 0)
                return;

            var root = new GameObject("Fireflies").transform;
            root.SetParent(parent, false);
            float slice = platformWidth / count;
            float band = gm.FireflyBandHeight;
            for (int i = 0; i < count; i++)
            {
                float x = -platformWidth * 0.5f + slice * (i + 0.5f);
                GameObject fx = Object.Instantiate(gm.fireflyPrefab, new Vector3(x, groundTop, 0f), Quaternion.identity, root);
                fx.name = "Firefly";
                foreach (var l in fx.GetComponentsInChildren<Light>(true))
                    Object.Destroy(l);
                var effect = fx.GetComponent("CFXR_Effect");
                if (effect != null)
                    Object.Destroy(effect);
                foreach (var r in fx.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    r.sortingOrder = SortingOrder;
                foreach (var p in fx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = p.main;
                    main.loop = true;
                    main.prewarm = true; // already drifting when the level shows
                    main.stopAction = ParticleSystemStopAction.None;
                    main.startSizeMultiplier *= gm.FireflySizeScale;
                    var shape = p.shape;
                    if (shape.enabled)
                    {
                        shape.position = new Vector3(0f, band * 0.5f, 0f);
                        shape.scale = new Vector3(slice, band, 0f);
                    }
                    var emission = p.emission;
                    emission.rateOverTimeMultiplier = gm.FireflyRate;
                    p.Play(false);
                }
            }
        }
    }
}