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

            RestartFade(_battleSource, BattleVolume, _ambientSource, 0f);
        }

        private void HandleWaveCleared(WaveDefinition wave)
        {
            PlayAmbientMusic();
        }

        private void HandleGameOver()
        {
            PlayAmbientMusic();
        }

        /// <summary>Picks a random ambient track and crossfades into it.</summary>
        private void PlayAmbientMusic()
        {
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

            RestartFade(_ambientSource, AmbientVolume, _battleSource, 0f);
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
