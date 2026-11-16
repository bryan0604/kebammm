using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Fine Tuning VFX: the normal bomb (bombing run ball impact) explosion. Plays the Cartoon FX Remaster
    /// "CFXR3 Fire Explosion B" prefab (assigned on the Game Manager asset) wherever a ball explodes on impact: the
    /// village ground, a monster, an obstacle or the trebuchet. Scales with the ball tier's impact_aoe_range
    /// (scale = impact_aoe_range x impactFxScale), so bigger tiers make bigger booms. Visual only. A pool of
    /// impactFxPoolSize instances is made once and reused (oldest restarted when all are busy).
    /// </summary>
    public static class KebammImpactFx
    {
        const int SortingOrder = 30; // above balls (10..20) and rocks (25), below damage numbers / HUD

        static readonly List<ParticleSystem> Pool = new List<ParticleSystem>();
        static Transform _root;
        static GameObject _prefab;
        static int _next;
        static int _poolSize;

        public static void Play(Vector3 position, float range)
        {
            var boot = KebammPrototypeBootstrap.Instance;
            var gm = boot != null ? boot.GameManagerData : null;
            if (gm == null || gm.impactFxPrefab == null || range <= 0f || gm.ImpactFxScale <= 0f)
                return;
            if (!EnsurePool(gm))
                return;
            ParticleSystem ps = Pool[_next];
            _next = (_next + 1) % Pool.Count;
            if (ps == null)
                return;
            Transform t = ps.transform;
            t.position = new Vector3(position.x, position.y, 0f);
            float size = range * gm.ImpactFxScale;
            t.localScale = new Vector3(size, size, size);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
        }

        static bool EnsurePool(KebammGameManagerData gm)
        {
            if (_root != null && _prefab == gm.impactFxPrefab && _poolSize == gm.ImpactFxPoolSize)
                return true;
            if (_root != null)
                Object.Destroy(_root.gameObject);
            Pool.Clear();
            _next = 0;
            _prefab = gm.impactFxPrefab;
            _poolSize = gm.ImpactFxPoolSize;
            _root = new GameObject("ImpactFxPool").transform;
            for (int i = 0; i < _poolSize; i++)
            {
                GameObject fx = Object.Instantiate(_prefab, new Vector3(0f, -1000f, 0f), Quaternion.identity, _root);
                fx.name = "ImpactFx";
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