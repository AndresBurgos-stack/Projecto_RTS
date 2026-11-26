using ProjectoRTS.Audio;
using ProjectoRTS.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectoRTS.UI
{
    /// <summary>
    /// Pause/options menu opened with Escape. Freezes the game (timeScale 0),
    /// exposes the audio options and lets the player resume, restart the map,
    /// return to the title screen or quit. Created at runtime like the other
    /// HUD panels so the scene needs no manual wiring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenu : MonoBehaviour
    {
        private static PauseMenu _instance;

        /// <summary>True while the game is paused through this menu.</summary>
        public static bool IsPaused => _instance != null && _instance._open;

        private GameObject _root;
        private Slider _musicSlider;
        private Slider _sfxSlider;
        private Text _musicPercent;
        private Text _sfxPercent;
        private bool _open;

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

            GameObject host = new GameObject("Pause Menu");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<PauseMenu>();
        }

        private void Awake()
        {
            _instance = this;
            BuildUi();
            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Toggle();
            }
        }

        private void Toggle()
        {
            if (_open)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void Open()
        {
            _open = true;
            Time.timeScale = 0f;
            _root.SetActive(true);

            if (_musicSlider != null)
            {
                _musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            }

            UpdatePercents();
        }

        private void Close()
        {
            _open = false;
            Time.timeScale = 1f;
            _root.SetActive(false);
        }

        private void Resume()
        {
            Close();
        }

        private void Restart()
        {
            Close();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void ToTitle()
        {
            Close();
            SceneManager.LoadScene("TitleScreen");
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void UpdatePercents()
        {
            if (_musicPercent != null)
            {
                _musicPercent.text = Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%";
            }

            if (_sfxPercent != null)
            {
                _sfxPercent.text = Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%";
            }
        }

        private void BuildUi()
        {
            GameObject canvasObject = new GameObject("Pause Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();

            _root = new GameObject("Panel");
            _root.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = _root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(840f, 360f);

            Image background = _root.AddComponent<Image>();
            background.color = new Color(0.06f, 0.08f, 0.11f, 0.96f);

            Font font = UiFonts.Default;

            Text title = CreateLabel(_root.transform, font, "Pausa", 30, new Vector2(0f, 154f), new Vector2(400f, 44f));
            title.alignment = TextAnchor.MiddleCenter;

            // Botones a la derecha, centrados
            CreateButton(_root.transform, font, "Continuar", new Vector2(210f, 94f), Resume);
            CreateButton(_root.transform, font, "Reiniciar partida", new Vector2(210f, 28f), Restart);
            CreateButton(_root.transform, font, "Menú principal", new Vector2(210f, -38f), ToTitle);
            CreateButton(_root.transform, font, "Salir", new Vector2(210f, -104f), QuitGame);

            // Opciones de audio a la izquierda, centradas con porcentaje
            Text musicLabel = CreateLabel(_root.transform, font, "Música", 20, new Vector2(-230f, 94f), new Vector2(240f, 24f));
            musicLabel.alignment = TextAnchor.MiddleCenter;

            _musicSlider = CreateSlider(_root.transform, new Vector2(-230f, 62f), value =>
            {
                GameSettings.MusicVolume = value;
                UpdatePercents();
            });

            _musicPercent = CreatePercentLabel(_root.transform, font, new Vector2(-230f, 32f));

            Text sfxLabel = CreateLabel(_root.transform, font, "Efectos de sonido", 20, new Vector2(-230f, -16f), new Vector2(280f, 24f));
            sfxLabel.alignment = TextAnchor.MiddleCenter;

            _sfxSlider = CreateSlider(_root.transform, new Vector2(-230f, -48f), value =>
            {
                GameSettings.SfxVolume = value;
                UpdatePercents();
            });

            _sfxPercent = CreatePercentLabel(_root.transform, font, new Vector2(-230f, -78f));
        }

        private Text CreateLabel(
            Transform parent,
            Font font,
            string content,
            int fontSize,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject go = new GameObject("Label");
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Text label = go.AddComponent<Text>();
            label.font = font;
            label.text = content;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.94f, 0.92f, 0.86f, 1f);
            label.raycastTarget = false;
            return label;
        }

        private Text CreatePercentLabel(Transform parent, Font font, Vector2 anchoredPosition)
        {
            Text percent = CreateLabel(parent, font, "80%", 18, anchoredPosition, new Vector2(80f, 22f));
            percent.alignment = TextAnchor.MiddleCenter;
            percent.fontStyle = FontStyle.Normal;
            percent.color = new Color(0.78f, 0.62f, 0.28f, 1f);
            return percent;
        }

        private void CreateButton(
            Transform parent,
            Font font,
            string content,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction action)
        {
            GameObject go = new GameObject("Button");
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(300f, 56f);

            Image image = go.AddComponent<Image>();
            image.color = new Color(0.16f, 0.24f, 0.36f, 1f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(go.transform, false);

            RectTransform labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text label = labelObject.AddComponent<Text>();
            label.font = font;
            label.text = content;
            label.fontSize = 22;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        private Slider CreateSlider(
            Transform parent,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction<float> onValueChanged)
        {
            GameObject go = new GameObject("Slider");
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(240f, 24f);

            Slider slider = go.AddComponent<Slider>();

            GameObject backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(go.transform, false);

            RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            Image backgroundImage = backgroundObject.AddComponent<Image>();
            backgroundImage.color = new Color(1f, 1f, 1f, 0.12f);

            GameObject fillAreaObject = new GameObject("Fill Area");
            fillAreaObject.transform.SetParent(go.transform, false);

            RectTransform fillAreaRect = fillAreaObject.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(5f, 0f);
            fillAreaRect.offsetMax = new Vector2(-15f, 0f);

            GameObject fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(fillAreaObject.transform, false);

            RectTransform fillRect = fillObject.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImage = fillObject.AddComponent<Image>();
            fillImage.color = new Color(0.78f, 0.62f, 0.28f, 1f);

            GameObject handleAreaObject = new GameObject("Handle Slide Area");
            handleAreaObject.transform.SetParent(go.transform, false);

            RectTransform handleAreaRect = handleAreaObject.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            GameObject handleObject = new GameObject("Handle");
            handleObject.transform.SetParent(handleAreaObject.transform, false);

            RectTransform handleRect = handleObject.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20f, 28f);

            Image handleImage = handleObject.AddComponent<Image>();
            handleImage.color = new Color(0.9f, 0.88f, 0.8f, 1f);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.8f;
            slider.onValueChanged.AddListener(onValueChanged);
            return slider;
        }
    }
}
