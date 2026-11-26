using System.Collections;
using ProjectoRTS.Core;
using ProjectoRTS.Waves;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectoRTS.Audio
{
    /// <summary>
    /// Plays the ambient track while a gameplay scene is loaded and switches
    /// to a battle track while a wave is on the field. The clips live in
    /// Resources so no AudioSource needs to be wired by hand, and the title
    /// screen keeps its own music.
    /// </summary>
    public sealed class GameplayMusic : MonoBehaviour
    {
        private static readonly string[] AmbientPaths =
        {
            "Musica_Ambiente_1",
            "Musica_Ambiente_2"
        };

        private const float AmbientVolume = 0.35f;
        private const float BattleVolume = 0.5f;
        private const float CrossfadeDuration = 1.2f;

        private static GameplayMusic _instance;

        private AudioSource _ambientSource;
        private AudioSource _battleSource;
        private Coroutine _fadeRoutine;
        private WaveManager _waves;
        private bool _battleActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!SceneFlow.IsGameplaySceneName(scene.name) || _instance != null)
            {
                return;
            }

            GameObject host = new GameObject("Gameplay Music");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<GameplayMusic>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            EnsureAudioListener();

            _ambientSource = CreateMusicSource(null);
            _battleSource = CreateMusicSource(null);

            GameSettings.Changed += HandleSettingsChanged;
            PlayAmbientMusic();
        }

        private void Start()
        {
            _waves = FindFirstObjectByType<WaveManager>();
            if (_waves == null)
            {
                return;
            }

            _waves.WaveStarted += HandleWaveStarted;
            _waves.WaveCleared += HandleWaveCleared;
            _waves.GameOver += HandleGameOver;
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= HandleSettingsChanged;

            if (_waves != null)
            {
                _waves.WaveStarted -= HandleWaveStarted;
                _waves.WaveCleared -= HandleWaveCleared;
                _waves.GameOver -= HandleGameOver;
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private AudioSource CreateMusicSource(AudioClip clip)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        private void HandleWaveStarted(WaveDefinition wave)
        {
            AudioClip clip = ResolveBattleClip(wave);
            if (clip == null)
            {
                return;
            }

            _battleSource.clip = clip;
            _battleSource.loop = true;

            if (!_battleSource.isPlaying)
            {
                _battleSource.Play();
            }

            _battleActive = true;
            ApplyTargets();
        }

        private void HandleWaveCleared(WaveDefinition wave)
        {
            PlayAmbientMusic();
        }

        private void HandleGameOver()
        {
            PlayAmbientMusic();
        }

        private void HandleSettingsChanged()
        {
            // Apply the new music volume instantly instead of crossfading
            // again: restarting the fade on every slider tick made the music
            // cut out while adjusting it.
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            if (_ambientSource == null || _battleSource == null)
            {
                return;
            }

            if (_battleActive)
            {
                _battleSource.volume = BattleVolume * GameSettings.MusicVolume;
                _ambientSource.volume = 0f;
            }
            else
            {
                _ambientSource.volume = AmbientVolume * GameSettings.MusicVolume;
                _battleSource.volume = 0f;
            }
        }

        /// <summary>Fades the active track in and the inactive one out, honouring the music volume option.</summary>
        private void ApplyTargets()
        {
            if (_ambientSource == null || _battleSource == null)
            {
                return;
            }

            if (_battleActive)
            {
                RestartFade(_battleSource, BattleVolume * GameSettings.MusicVolume, _ambientSource, 0f);
            }
            else
            {
                RestartFade(_ambientSource, AmbientVolume * GameSettings.MusicVolume, _battleSource, 0f);
            }
        }

        /// <summary>Picks a random ambient track and crossfades into it.</summary>
        private void PlayAmbientMusic()
        {
            _battleActive = false;

            AudioClip clip = UnityEngine.Resources.Load<AudioClip>(
                AmbientPaths[Random.Range(0, AmbientPaths.Length)]);
            if (clip == null)
            {
                Debug.LogWarning("[GameplayMusic] No se encontro un clip de ambiente en Resources.");
                return;
            }

            _ambientSource.clip = clip;

            if (!_ambientSource.isPlaying)
            {
                _ambientSource.Play();
            }

            ApplyTargets();
        }

        /// <summary>
        /// Picks the battle track for a wave: thematic tracks match the wave
        /// name, and generic Roman waves alternate the two Roman tracks.
        /// </summary>
        private static AudioClip ResolveBattleClip(WaveDefinition wave)
        {
            string displayName = wave != null ? wave.DisplayName : string.Empty;

            if (displayName.Contains("Centurion"))
            {
                return UnityEngine.Resources.Load<AudioClip>("OleadaCenturion");
            }

            if (displayName.Contains("Barbara") || displayName.Contains("Barbaro"))
            {
                return displayName.Contains("Gigante")
                    ? UnityEngine.Resources.Load<AudioClip>("OleadaBarbarosGigante")
                    : UnityEngine.Resources.Load<AudioClip>("OleadaBarbara");
            }

            if (displayName.Contains("Nomuerto") || displayName.Contains("Muerto"))
            {
                return UnityEngine.Resources.Load<AudioClip>("OleadaNomuertos");
            }

            return UnityEngine.Resources.Load<AudioClip>(
                Random.Range(0, 2) == 0 ? "OleadaRomana1" : "OleadaRomana2");
        }

        private void RestartFade(
            AudioSource fadeIn,
            float fadeInVolume,
            AudioSource fadeOut,
            float fadeOutVolume)
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
            }

            _fadeRoutine = StartCoroutine(
                FadeRoutine(fadeIn, fadeInVolume, fadeOut, fadeOutVolume));
        }

        private IEnumerator FadeRoutine(
            AudioSource fadeIn,
            float fadeInVolume,
            AudioSource fadeOut,
            float fadeOutVolume)
        {
            float elapsed = 0f;

            while (elapsed < CrossfadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / CrossfadeDuration);

                if (fadeIn != null)
                {
                    fadeIn.volume = Mathf.Lerp(0f, fadeInVolume, t);
                }

                if (fadeOut != null)
                {
                    fadeOut.volume = Mathf.Lerp(0f, fadeOutVolume, t);
                }

                yield return null;
            }

            if (fadeIn != null)
            {
                fadeIn.volume = fadeInVolume;
            }

            if (fadeOut != null)
            {
                fadeOut.volume = fadeOutVolume;
                if (fadeOutVolume <= 0.001f)
                {
                    fadeOut.Stop();
                }
            }

            _fadeRoutine = null;
        }

        private void EnsureAudioListener()
        {
            if (FindFirstObjectByType<AudioListener>() != null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.gameObject.AddComponent<AudioListener>();
                return;
            }

            gameObject.AddComponent<AudioListener>();
        }
    }
}
