using System.Collections;
using ProjectoRTS.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectoRTS.Audio
{
    /// <summary>
    /// Plays the ambient track while a gameplay scene is loaded. The clip
    /// lives in Resources so the gameplay scene does not need an extra
    /// AudioSource wired by hand, and the title screen keeps its own music.
    /// </summary>
    public sealed class GameplayMusic : MonoBehaviour
    {
        private const string ResourcePath = "Musica_Ambiente_1";
        private const float TargetVolume = 0.35f;
        private const float FadeInDuration = 1f;

        private static GameplayMusic _instance;

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

            AudioClip clip = UnityEngine.Resources.Load<AudioClip>(ResourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"[GameplayMusic] No se encontro el clip '{ResourcePath}' en Resources.");
                return;
            }

            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;

            source.Play();
            StartCoroutine(FadeIn(source));
        }

        private void EnsureAudioListener()
        {
            if (Object.FindFirstObjectByType<AudioListener>() != null)
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

        private IEnumerator FadeIn(AudioSource source)
        {
            float elapsed = 0f;

            while (elapsed < FadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(0f, TargetVolume, elapsed / FadeInDuration);
                yield return null;
            }

            source.volume = TargetVolume;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
