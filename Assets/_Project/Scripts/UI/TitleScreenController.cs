using System.Collections;
using ProjectoRTS.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectoRTS.UI
{
    /// <summary>
    /// Handles the single Play button of the title screen. It loads the
    /// gameplay scene asynchronously, shows a simple loading overlay and
    /// fades the menu music out while the next scene is prepared.
    /// </summary>
    public sealed class TitleScreenController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button playButton;
        [SerializeField] private CanvasGroup loadingOverlay;
        [SerializeField] private RectTransform loadingFill;
        [SerializeField] private Text loadingLabel;

        [Header("Scene")]
        [SerializeField] private string gameplaySceneName = SceneFlow.GameplaySceneName;
        [SerializeField] private float minimumLoadingTime = 0.6f;

        [Header("Music")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private float musicFadeDuration = 0.4f;

        private bool _loading;
        private float _musicStartVolume;

        private void Awake()
        {
            _musicStartVolume = musicSource != null ? musicSource.volume : 0f;

            if (playButton != null)
            {
                playButton.onClick.AddListener(Play);
            }

            HideOverlay();
        }

        private void OnDestroy()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(Play);
            }
        }

        public void Play()
        {
            if (_loading)
            {
                return;
            }

            _loading = true;

            if (playButton != null)
            {
                playButton.interactable = false;
            }

            ShowOverlay();
            StartCoroutine(LoadGameplay());
        }

        private IEnumerator LoadGameplay()
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                Fail("No se ha configurado la escena de juego.");
                yield break;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Fail($"No se pudo cargar la escena '{gameplaySceneName}'.");
                yield break;
            }

            operation.allowSceneActivation = false;

            float elapsed = 0f;
            float fadeElapsed = 0f;

            while (operation.progress < 0.9f || elapsed < minimumLoadingTime)
            {
                elapsed += Time.unscaledDeltaTime;

                float progress = Mathf.Clamp01(operation.progress / 0.9f);
                SetProgress(progress);

                if (musicSource != null && musicFadeDuration > 0f)
                {
                    fadeElapsed = Mathf.Min(fadeElapsed + Time.unscaledDeltaTime, musicFadeDuration);
                    musicSource.volume = Mathf.Lerp(_musicStartVolume, 0f, fadeElapsed / musicFadeDuration);
                }

                yield return null;
            }

            if (musicSource != null)
            {
                musicSource.volume = 0f;
            }

            SetProgress(1f);
            operation.allowSceneActivation = true;

            while (!operation.isDone)
            {
                yield return null;
            }
        }

        private void SetProgress(float progress)
        {
            if (loadingFill != null)
            {
                Vector2 anchorMax = loadingFill.anchorMax;
                anchorMax.x = Mathf.Clamp01(progress);
                loadingFill.anchorMax = anchorMax;
            }

            if (loadingLabel != null)
            {
                loadingLabel.text = $"Cargando... {Mathf.RoundToInt(progress * 100f)}%";
            }
        }

        private void ShowOverlay()
        {
            if (loadingOverlay == null)
            {
                return;
            }

            loadingOverlay.alpha = 1f;
            loadingOverlay.interactable = true;
            loadingOverlay.blocksRaycasts = true;
        }

        private void HideOverlay()
        {
            if (loadingOverlay == null)
            {
                return;
            }

            loadingOverlay.alpha = 0f;
            loadingOverlay.interactable = false;
            loadingOverlay.blocksRaycasts = false;
        }

        private void Fail(string message)
        {
            Debug.LogError($"[TitleScreen] {message}");

            _loading = false;

            if (playButton != null)
            {
                playButton.interactable = true;
            }

            HideOverlay();
        }
    }
}
