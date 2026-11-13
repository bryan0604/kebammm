using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Fine Tuning VFX: a destroyed obstacle or trebuchet keeps burning with looping fires (Cartoon FX Remaster
    /// "CFXR Fire Breath", assigned on the Game Manager asset). A random number of fire points
    /// (destroyed_fire_points_min..max) is spread across the host, one per slot so they don't bunch up.
    /// Each fire points up, is sized to the host (destroyed_fire_scale x the host's larger side, +/-15%),
    /// and its flame lifetime is scaled by destroyed_fire_lifetime_scale so the flames stay short.
    /// Fires live on the host until the level ends.
    /// </summary>
    public static class KebammDestroyedFire
    {
        const int SortingOrderOffset = 5; // above obstacles (0) and monsters (1), below balls (10..20)
        const float SizeJitter = 0.15f;
        const string FireName = "DestroyedFire";

        public static void Spawn(Transform host, Bounds area)
        {
            var boot = KebammPrototypeBootstrap.Instance;
            var gm = boot != null ? boot.GameManagerData : null;
            if (gm == null || gm.destroyedFirePrefab == null || host == null)
                return;
            float baseSize = Mathf.Max(area.size.x, area.size.y) * gm.DestroyedFireScale;
            int count = Random.Range(gm.DestroyedFirePointsMin, gm.DestroyedFirePointsMax + 1);
            if (baseSize <= 0f || count <= 0)
                return;

            float slotW = area.size.x / count;
            for (int i = 0; i < count; i++)
            {
                // One fire per horizontal slot, jittered inside it; height anywhere on the lower 3/4 of the host.
                float x = area.min.x + slotW * (i + Random.Range(0.2f, 0.8f));
                float y = area.min.y + area.size.y * Random.Range(0.1f, 0.75f);
                float size = baseSize * Random.Range(1f - SizeJitter, 1f + SizeJitter);
                SpawnOne(gm, host, new Vector3(x, y, 0f), size);
            }
        }

        /// <summary>Removes every fire spawned on this host (used when a reused host, like the trebuchet, resets for a new level).</summary>
        public static void Clear(Transform host)
        {
            if (host == null)
                return;
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                Transform c = host.GetChild(i);
                if (c.name == FireName)
                    Object.Destroy(c.gameObject);
            }
        }

        static void SpawnOne(KebammGameManagerData gm, Transform host, Vector3 pos, float size)
        {
            // The prefab fires along +Z; rotate so it points up in 2D.
            GameObject fire = Object.Instantiate(gm.destroyedFirePrefab, pos, Quaternion.Euler(-90f, 0f, 0f), host);
            fire.name = FireName;
            Vector3 ps = host.lossyScale;
            fire.transform.localScale = new Vector3(
                size / Mathf.Max(0.0001f, Mathf.Abs(ps.x)),
                size / Mathf.Max(0.0001f, Mathf.Abs(ps.y)),
                size / Mathf.Max(0.0001f, Mathf.Abs(ps.z)));

            // 2D sprites are unlit, so the pack's point light only costs performance.
            foreach (var l in fire.GetComponentsInChildren<Light>(true))
                Object.Destroy(l);
            foreach (var r in fire.GetComponentsInChildren<ParticleSystemRenderer>(true))
                r.sortingOrder += SortingOrderOffset;
            float life = gm.DestroyedFireLifetimeScale;
            float speed = gm.DestroyedFireSpeedScale;
            float offset = Random.Range(0f, 1f);
            foreach (var p in fire.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = p.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.startLifetimeMultiplier *= life;
                main.simulationSpeed *= speed; // playback speed of the whole effect (0.5 = half speed)
            }
            // Start each fire at a different point in its loop so several fires don't pulse in sync.
            var rootPs = fire.GetComponent<ParticleSystem>();
            if (rootPs != null)
            {
                rootPs.Simulate(offset, true, true);
                rootPs.Play(true);
            }
        }
    }
}