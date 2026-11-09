using System.Collections.Generic;
using ProjectoRTS.Core;
using ProjectoRTS.Economy;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Permanent construction menu on the bottom-left corner. It lists every
    /// building definition bound to the number keys in a grid of three
    /// columns and starts the normal placement flow, so buildings can be
    /// placed without selecting a worker first.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorkerBuildPanel : MonoBehaviour
    {
        private const int Columns = 3;

        private static WorkerBuildPanel _instance;

        private BuildingPlacementController _placement;
        private BuildingPlacementHotkeys _hotkeys;
        private RecruitmentManager _recruitment;
        private GameObject _root;
        private RectTransform _buttonRow;
        private readonly List<GameObject> _buttons = new List<GameObject>();
        private float _nextRefresh;

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

            GameObject host = new GameObject("Build Panel");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<WorkerBuildPanel>();
        }

        private void Awake()
        {
            _instance = this;
            BuildUi();
            RefreshButtons();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void LateUpdate()
        {
            if (_placement == null)
            {
                _placement = FindFirstObjectByType<BuildingPlacementController>();
            }

            if (_hotkeys == null)
            {
                _hotkeys = FindFirstObjectByType<BuildingPlacementHotkeys>();
            }

            if (_recruitment == null)
            {
                _recruitment = FindFirstObjectByType<RecruitmentManager>();
            }

            // While a barracks or the town hall is selected the recruitment
            // panel takes this corner, so the build menu steps aside.
            bool barracksSelected = _recruitment != null && _recruitment.ActiveBarracks != null;

            if (_root != null && _root.activeSelf == barracksSelected)
            {
                _root.SetActive(!barracksSelected);
            }

            if (_hotkeys == null || barracksSelected)
            {
                return;
            }

            if (Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + 0.5f;
                RefreshButtons();
            }
        }

        private void RefreshButtons()
        {
            if (_buttonRow == null)
            {
                return;
            }

            BuildingDefinition[] definitions = _hotkeys != null
                ? _hotkeys.Definitions
                : null;

            if (definitions == null)
            {
                return;
            }

            while (_buttons.Count < definitions.Length)
            {
                _buttons.Add(CreateButton(_buttons.Count));
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                bool used = i < definitions.Length && definitions[i] != null;
                _buttons[i].SetActive(used);

                if (!used)
                {
                    continue;
                }

                int index = i;
                Button button = _buttons[i].GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => BeginPlacement(definitions[index]));
                    button.interactable = PlayerEconomy.CanAfford(
                        definitions[i].WoodCost,
                        definitions[i].BronzeCost);
                }

                Text label = _buttons[i].GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.fontSize = 10;
                    label.verticalOverflow = VerticalWrapMode.Overflow;
                    label.horizontalOverflow = HorizontalWrapMode.Overflow;
                    label.text =
                        $"{definitions[i].DisplayName}\n" +
                        $"{definitions[i].WoodCost}M {definitions[i].BronzeCost}B";
                }
            }
        }

        private void BeginPlacement(BuildingDefinition definition)
        {
            if (definition == null || _placement == null)
            {
                return;
            }

            _placement.BeginPlacement(definition);
        }

        private void BuildUi()
        {
            GameObject canvasObject = new GameObject("Build Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();

            _root = new GameObject("Panel");
            _root.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = _root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(20f, 20f);
            rect.sizeDelta = new Vector2(500f, 180f);

            Image background = _root.AddComponent<Image>();
            background.color = new Color(0.08f, 0.09f, 0.12f, 0.9f);

            Font font = ResolveFont();
            Text title = CreateLabel(_root.transform, font, "Construir");
            title.alignment = TextAnchor.MiddleCenter;

            GameObject row = new GameObject("Buttons");
            row.transform.SetParent(_root.transform, false);

            _buttonRow = row.AddComponent<RectTransform>();
            _buttonRow.anchorMin = new Vector2(0f, 1f);
            _buttonRow.anchorMax = new Vector2(0f, 1f);
            _buttonRow.pivot = new Vector2(0f, 1f);
            _buttonRow.anchoredPosition = new Vector2(0f, -32f);
            _buttonRow.sizeDelta = new Vector2(480f, 140f);
        }

        private GameObject CreateButton(int index)
        {
            GameObject go = new GameObject("Build " + index);
            go.transform.SetParent(_buttonRow, false);

            int column = index % Columns;
            int rowIndex = index / Columns;

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(10f + column * 160f, -10f - rowIndex * 44f);
            rect.sizeDelta = new Vector2(150f, 38f);

            Image image = go.AddComponent<Image>();
            image.color = new Color(0.24f, 0.3f, 0.38f, 1f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;

            GameObject labelObject = new GameObject("Text");
            labelObject.transform.SetParent(go.transform, false);

            RectTransform labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text label = labelObject.AddComponent<Text>();
            label.font = ResolveFont();
            label.text = "Edificio";
            label.fontSize = 12;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            return go;
        }

        private static Font ResolveFont()
        {
            Font font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = UnityEngine.Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }

        private static Text CreateLabel(Transform parent, Font font, string content)
        {
            GameObject go = new GameObject("Label");
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -6f);
            rect.sizeDelta = new Vector2(470f, 22f);

            Text label = go.AddComponent<Text>();
            label.font = font;
            label.text = content;
            label.fontSize = 15;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0.94f, 0.92f, 0.86f, 1f);
            label.raycastTarget = false;
            return label;
        }
    }
}
