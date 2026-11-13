using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Fine Tuning VFX: a small burst (Cartoon FX Remaster "CFXR3 Hit Misc A", assigned on the Game Manager asset)
    /// where a dropped ball first touches with its bottom side. Only the ball the player dropped; merged and resting
    /// balls never puff. Optimised with a small pool: drop_burst_pool_size instances are made once and reused (the
    /// oldest is restarted when all are busy), so nothing is instantiated or destroyed during play. The pack's point
    /// light and CFXR_Effect script are removed (2D is unlit, and that script would destroy the pooled object).
    /// </summary>
    public static class KebammDropBurst
    {
        const int SortingOrder = 30; // above balls (10..20) and rocks (25)
        static readonly List<ParticleSystem> Pool = new List<ParticleSystem>();
        static Transform _root;
        static GameObject _prefab;
        static int _next;

        public static void Play(Vector3 position, float ballDiameter)
        {
            var boot = KebammPrototypeBootstrap.Instance;
            var gm = boot != null ? boot.GameManagerData : null;
            if (gm == null || gm.dropBurstPrefab == null || ballDiameter <= 0f)
                return;
            if (!EnsurePool(gm))
                return;

            ParticleSystem ps = Pool[_next];
            _next = (_next + 1) % Pool.Count;
            if (ps == null)
                return;
            Transform t = ps.transform;
            t.position = position;
            float size = ballDiameter * gm.DropBurstScale;
            t.localScale = new Vector3(size, size, size);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
        }

        static bool EnsurePool(KebammGameManagerData gm)
        {
            int size = gm.DropBurstPoolSize;
            if (_root != null && _prefab == gm.dropBurstPrefab && Pool.Count == size)
                return true;
            if (_root != null)
                Object.Destroy(_root.gameObject);
            Pool.Clear();
            _next = 0;
            _prefab = gm.dropBurstPrefab;
            _root = new GameObject("DropBurstPool").transform;
            for (int i = 0; i < size; i++)
            {
                // Rotate so the pack's +Z effect faces the 2D camera like the other CFXR effects.
                GameObject fx = Object.Instantiate(_prefab, new Vector3(0f, -1000f, 0f), Quaternion.identity, _root);
                fx.name = "DropBurst";
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
                Pool.Add(ps);
            }
            return Pool.Count > 0;
        }
    }
}