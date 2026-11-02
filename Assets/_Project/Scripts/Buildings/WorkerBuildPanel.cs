using System.Collections.Generic;
using ProjectoRTS.Economy;
using ProjectoRTS.Units;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Build menu that appears when a worker is selected. It lists every
    /// building definition bound to the number keys and starts the normal
    /// placement flow, so the worker builds with the same preview, grid checks
    /// and drag painting the hotkeys already use.
    /// The panel builds its own UI at runtime and polls the selection, because
    /// UnitSelectionController exposes SelectedUnits without a change event.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorkerBuildPanel : MonoBehaviour
    {
        private const string WorkerName = "Trabajador";

        private static WorkerBuildPanel _instance;

        private UnitSelectionController _selection;
        private BuildingPlacementController _placement;
        private BuildingPlacementHotkeys _hotkeys;
        private GameObject _root;
        private RectTransform _buttonRow;
        private readonly List<GameObject> _buttons = new List<GameObject>();
        private bool _workerSelected;
        private float _nextRefresh;

        /// <summary>True while the build menu is visible.</summary>
        public bool IsOpen => _root != null && _root.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
            {
                return;
            }

            GameObject host = new GameObject("Worker Build Panel");
            host.AddComponent<WorkerBuildPanel>();
        }

        private void Awake()
        {
            _instance = this;
            BuildUi();
            Hide();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>Shows the build menu and refreshes its buttons.</summary>
        public void Show()
        {
            if (_root == null)
            {
                return;
            }

            RefreshButtons();
            _root.SetActive(true);
        }

        /// <summary>Hides the build menu.</summary>
        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (_selection == null)
            {
                _selection = FindFirstObjectByType<UnitSelectionController>();
            }

            if (_placement == null)
            {
                _placement = FindFirstObjectByType<BuildingPlacementController>();
            }

            if (_hotkeys == null)
            {
                _hotkeys = FindFirstObjectByType<BuildingPlacementHotkeys>();
            }

            if (_selection == null)
            {
                return;
            }

            bool worker = IsWorkerSelected();
            if (worker == _workerSelected)
            {
                if (worker && IsOpen && Time.time >= _nextRefresh)
                {
                    _nextRefresh = Time.time + 0.5f;
                    RefreshButtons();
                }

                return;
            }

            _workerSelected = worker;

            if (worker)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }

        private bool IsWorkerSelected()
        {
            IReadOnlyList<UnitMovement> selected = _selection.SelectedUnits;
            if (selected == null || selected.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < selected.Count; i++)
            {
                UnitMovement unit = selected[i];
                if (unit == null)
                {
                    continue;
                }

                if (!unit.gameObject.name.Contains(WorkerName))
                {
                    return false;
                }
            }

            return true;
        }

        private void RefreshButtons()
        {
            if (_hotkeys == null || _buttonRow == null)
            {
                return;
            }

            BuildingDefinition[] definitions = _hotkeys.Definitions;
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
            Hide();
        }

        private void BuildUi()
        {
            GameObject canvasObject = new GameObject("Worker Build Canvas");
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
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 24f);
            rect.sizeDelta = new Vector2(660f, 96f);

            Image background = _root.AddComponent<Image>();
            background.color = new Color(0.08f, 0.09f, 0.12f, 0.9f);

            Font font = ResolveFont();
            Text title = CreateLabel(_root.transform, font, "Construir");
            title.alignment = TextAnchor.MiddleCenter;

            GameObject row = new GameObject("Buttons");
            row.transform.SetParent(_root.transform, false);

            _buttonRow = row.AddComponent<RectTransform>();
            _buttonRow.anchorMin = new Vector2(0.5f, 0f);
            _buttonRow.anchorMax = new Vector2(0.5f, 0f);
            _buttonRow.pivot = new Vector2(0.5f, 0f);
            _buttonRow.anchoredPosition = new Vector2(0f, 10f);
            _buttonRow.sizeDelta = new Vector2(640f, 40f);
        }

        private GameObject CreateButton(int index)
        {
            GameObject go = new GameObject("Build " + index);
            go.transform.SetParent(_buttonRow, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(index * 104f, 0f);
            rect.sizeDelta = new Vector2(98f, 34f);

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
            rect.sizeDelta = new Vector2(640f, 22f);

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