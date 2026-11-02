using UnityEngine;

namespace Kebamm
{
    /// <summary>
    /// Dev-only frame spike logger: in the Editor and development builds, logs any frame longer than
    /// SpikeMs with what happened since the previous frame (drops, monster impacts, merges, font loads,
    /// live balls). Compiles to an empty call in release builds.
    /// </summary>
    public static class KebammFrameSpikeLog
    {
        public const float SpikeMs = 25f;
        public static int Drops, Impacts, Merges, FontLoads;

        public static void EndFrame()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float ms = Time.unscaledDeltaTime * 1000f;
            if (ms > SpikeMs && Time.frameCount > 2)
                Debug.Log($"[Kebamm] Frame spike {ms:0.0}ms: drops={Drops} impacts={Impacts} merges={Merges} fontLoads={FontLoads} liveBalls={KebammBall.Live.Count}");
#endif
            Drops = Impacts = Merges = FontLoads = 0;
        }
    }
}