using System.Collections.Generic;
using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Fine Tuning VFX: the Magic Bomb (combo bomb) explosion. Plays the Cartoon FX Remaster "CFXR Explosion 1"
    /// prefab (assigned on the Game Manager asset) where the bomb exploded, scaled with the blast range
    /// (scale = magic_bomb_explode_range x magicBombFxScale) so it reads as the area hit. Visual only: damage, push
    /// and timing are untouched. A tiny pool (2) is made once and reused; the pack's point light and CFXR_Effect
    /// script are removed (2D is unlit, and that script would destroy the pooled object).
    /// </summary>
    public static class KebammMagicBombFx
    {
        const int SortingOrder = 30; // above balls (10..20) and rocks (25), below damage numbers / HUD
        const int PoolSize = 2;
        static readonly List<ParticleSystem> Pool = new List<ParticleSystem>();
        static Transform _root;
        static GameObject _prefab;
        static int _next;

        public static void Play(Vector3 position, float range)
        {
            var boot = KebammPrototypeBootstrap.Instance;
            var gm = boot != null ? boot.GameManagerData : null;
            if (gm == null || gm.magicBombFxPrefab == null || range <= 0f || gm.MagicBombFxScale <= 0f)
                return;
            if (!EnsurePool(gm))
                return;
            ParticleSystem ps = Pool[_next];
            _next = (_next + 1) % Pool.Count;
            if (ps == null)
                return;
            Transform t = ps.transform;
            t.position = new Vector3(position.x, position.y, 0f);
            float size = range * gm.MagicBombFxScale;
            t.localScale = new Vector3(size, size, size);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
        }

        static bool EnsurePool(KebammGameManagerData gm)
        {
            if (_root != null && _prefab == gm.magicBombFxPrefab && Pool.Count > 0)
                return true;
            if (_root != null)
                Object.Destroy(_root.gameObject);
            Pool.Clear();
            _next = 0;
            _prefab = gm.magicBombFxPrefab;
            _root = new GameObject("MagicBombFxPool").transform;
            for (int i = 0; i < PoolSize; i++)
            {
                GameObject fx = Object.Instantiate(_prefab, new Vector3(0f, -1000f, 0f), Quaternion.identity, _root);
                fx.name = "MagicBombFx";
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