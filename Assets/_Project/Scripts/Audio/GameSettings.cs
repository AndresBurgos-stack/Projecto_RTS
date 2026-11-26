using System;
using UnityEngine;

namespace ProjectoRTS.Audio
{
    /// <summary>
    /// Persistent audio options shared by the pause menu, the music player
    /// and the one-shot SFX pool. Values live in PlayerPrefs.
    /// </summary>
    public static class GameSettings
    {
        private const string MusicVolumeKey = "GameSettings.MusicVolume";
        private const string SfxVolumeKey = "GameSettings.SfxVolume";

        /// <summary>Raised whenever an option changes.</summary>
        public static event Action Changed;

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f);
            set
            {
                PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
                Changed?.Invoke();
            }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxVolumeKey, 0.8f);
            set
            {
                PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
                Changed?.Invoke();
            }
        }
    }
}
