using UnityEngine;

namespace ProjectoRTS
{
    /// <summary>
    /// Uncaps the frame rate in play mode so the game uses all the GPU the
    /// machine can give instead of waiting for vsync.
    /// </summary>
    public static class PerformanceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Apply()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }
    }
}
