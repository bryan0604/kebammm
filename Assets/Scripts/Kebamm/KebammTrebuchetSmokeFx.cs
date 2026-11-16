using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Fine Tuning VFX: construction smoke on the trebuchet (prefabs assigned on the Game Manager asset).
    /// Construction start (stage 0): "CFXR2 Poison Cloud" (colour tweaked by Veritas) = trebuchetSmokePrefab.
    /// Every stage increase (incl. ready): "CFXR3 Hit Misc F Smoke" = trebuchetStageSmokePrefab (falls back to the
    /// start prefab when empty). Size = trebuchetScale x trebuchetSmokeScale. Visual only; replaces the old placeholder
    /// circle puff when assigned. One small pool (3) per prefab, reused round-robin.
    /// </summary>
    public static class KebammTrebuchetSmokeFx
    {
        const int SortingOrder = KebammTrebuchet.SortingOrder + 10; // in front of the trebuchet parts, behind its floating text (+20)
        const int PoolSize = 3;

        class PoolSet
        {
            public Transform Root;
            public readonly List<ParticleSystem> Items = new List<ParticleSystem>();
            public int Next;
        }

        static readonly Dictionary<GameObject, PoolSet> Pools = new Dictionary<GameObject, PoolSet>();

        /// <summary>Plays the smoke; false when no prefab / scale 0 (caller keeps its placeholder puff).</summary>
        public static bool Play(Vector3 position, float hostScale, bool stageUp)
        {
            var boot = KebammPrototypeBootstrap.Instance;
            var gm = boot != null ? boot.GameManagerData : null;
            if (gm == null || hostScale <= 0f || gm.TrebuchetSmokeScale <= 0f)
                return false;
            GameObject prefab = stageUp && gm.trebuchetStageSmokePrefab != null ? gm.trebuchetStageSmokePrefab : gm.trebuchetSmokePrefab;
            if (prefab == null)
                return false;
            PoolSet set = Ensure(prefab);
            if (set == null || set.Items.Count == 0)
                return false;
            ParticleSystem ps = set.Items[set.Next];
            set.Next = (set.Next + 1) % set.Items.Count;
            if (ps == null)
                return false;
            Transform t = ps.transform;
            t.position = new Vector3(position.x, position.y, 0f);
            float size = hostScale * gm.TrebuchetSmokeScale;
            t.localScale = new Vector3(size, size, size);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
            return true;
        }

        static PoolSet Ensure(GameObject prefab)
        {
            if (Pools.TryGetValue(prefab, out PoolSet existing) && existing.Root != null)
                return existing;
            var set = new PoolSet { Root = new GameObject("TrebuchetSmokePool_" + prefab.name).transform };
            for (int i = 0; i < PoolSize; i++)
            {
                GameObject fx = Object.Instantiate(prefab, new Vector3(0f, -1000f, 0f), Quaternion.identity, set.Root);
                fx.name = "TrebuchetSmoke";
                foreach (var l in fx.GetComponentsInChildren<Light>(true))
                    Object.Destroy(l);
                var effect = fx.GetComponent("CFXR_Effect");
                if (effect != null)
                    Object.Destroy(effect);
                foreach (var r in fx.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    r.sortingOrder = SortingOrder;
                var ps = fx.GetComponent<ParticleSystem>();
                if (ps == null)
                {
                    Object.Destroy(fx);
                    continue;
                }
                foreach (var p in fx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = p.main;
                    main.playOnAwake = false;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.stopAction = ParticleSystemStopAction.None;
                }
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                set.Items.Add(ps);
            }
            Pools[prefab] = set;
            return set;
        }
    }
}