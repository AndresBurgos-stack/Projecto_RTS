using System.Collections.Generic;
using ProjectoRTS.Core;
using ProjectoRTS.Resources;
using ProjectoRTS.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Right-side panel that appears while workers are selected. Two stacked
    /// colored boxes set the resource priority for every selected worker:
    /// green means prioritized, red means not. Each box also shows how many
    /// living workers prioritize that resource.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorkerPriorityPanel : MonoBehaviour
    {
        private const string WorkerName = "Trabajador";

        private static WorkerPriorityPanel _instance;

        private UnitSelectionController _selection;
        private GameObject _root;
        private Image _woodImage;
        private Image _copperImage;
        private Image _tinImage;
        private Image _stoneImage;
        private Image _forgeImage;
        private Text _woodLabel;
        private Text _copperLabel;
        private Text _tinLabel;
        private Text _stoneLabel;
        private Text _forgeLabel;
        private Text _stateLabel;
        private float _nextRefresh;

        private static readonly Color OnColor = new Color(0.2f, 0.65f, 0.3f, 1f);
        private static readonly Color OffColor = new Color(0.72f, 0.2f, 0.16f, 1f);

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

            GameObject host = new GameObject("Worker Priority Panel");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<WorkerPriorityPanel>();
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

        private void LateUpdate()
        {
            if (_selection == null)
            {
                _selection = FindFirstObjectByType<UnitSelectionController>();
            }

            bool workers = IsWorkerSelected();

            if (_root != null && _root.activeSelf != workers)
            {
                _root.SetActive(workers);
            }

            if (workers && Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + 0.5f;
                RefreshState();
            }
        }

        private void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private bool IsWorkerSelected()
        {
            IReadOnlyList<UnitMovement> selected = _selection != null
                ? _selection.SelectedUnits
                : null;

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

        private void BuildUi()
        {
            GameObject canvasObject = new GameObject("Worker Priority Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();

            _root = new GameObject("Panel");
            _root.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = _root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-24f, 0f);
            rect.sizeDelta = new Vector2(220f, 400f);

            Image background = _root.AddComponent<Image>();
            background.color = new Color(0.08f, 0.09f, 0.12f, 0.92f);

            Font font = ResolveFont();

            Text title = CreateLabel(_root.transform, font, "Prioridad de recursos");
            title.alignment = TextAnchor.MiddleCenter;
            title.fontSize = 15;

            _stateLabel = CreateLabel(_root.transform, font, "Sin prioridad");
            _stateLabel.alignment = TextAnchor.MiddleCenter;
            _stateLabel.fontSize = 12;
            _stateLabel.fontStyle = FontStyle.Normal;
            _stateLabel.color = new Color(0.85f, 0.83f, 0.75f, 1f);
            RectTransform stateRect = _stateLabel.GetComponent<RectTransform>();
            stateRect.anchoredPosition = new Vector2(0f, -36f);
            stateRect.sizeDelta = new Vector2(200f, 20f);

            CreatePanelButton(
                _root.transform,
                font,
                90f,
                "Madera",
                () => SetPriority(ResourceType.Wood),
                out _woodImage,
                out _woodLabel);
            CreatePanelButton(
                _root.transform,
                font,
                25f,
                "Cobre",
                () => SetPriority(ResourceType.Copper),
                out _copperImage,
                out _copperLabel);
            CreatePanelButton(
                _root.transform,
                font,
                -40f,
                "Estaño",
                () => SetPriority(ResourceType.Tin),
                out _tinImage,
                out _tinLabel);
            CreatePanelButton(
                _root.transform,
                font,
                -105f,
                "Piedra",
                () => SetPriority(ResourceType.Stone),
                out _stoneImage,
                out _stoneLabel);
            CreatePanelButton(
                _root.transform,
                font,
                -170f,
                "Fundir",
                SetHaul,
                out _forgeImage,
                out _forgeLabel);
        }

        private void CreatePanelButton(
            Transform parent,
            Font font,
            float y,
            string title,
            UnityEngine.Events.UnityAction action,
            out Image image,
            out Text label)
        {
            GameObject go = new GameObject("Priority " + title);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(180f, 50f);

            image = go.AddComponent<Image>();
            image.color = OffColor;
            image.raycastTarget = true;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            GameObject labelObject = new GameObject("Text");
            labelObject.transform.SetParent(go.transform, false);

            RectTransform labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            label = labelObject.AddComponent<Text>();
            label.font = font;
            label.text = title;
            label.fontSize = 14;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        private void SetPriority(ResourceType type)
        {
            if (_selection == null)
            {
                _selection = FindFirstObjectByType<UnitSelectionController>();
            }

            IReadOnlyList<UnitMovement> selected = _selection != null
                ? _selection.SelectedUnits
                : null;

            if (selected == null)
            {
                return;
            }

            for (int i = 0; i < selected.Count; i++)
            {
                UnitMovement unit = selected[i];
                WorkerBuilder worker = unit != null
                    ? unit.GetComponent<WorkerBuilder>()
                    : null;

                if (worker != null)
                {
                    worker.HaulForForge = false;
                    worker.PreferredResource = type;
                }
            }

            RefreshState();
        }

        private void SetHaul()
        {
            if (_selection == null)
            {
                _selection = FindFirstObjectByType<UnitSelectionController>();
            }

            IReadOnlyList<UnitMovement> selected = _selection != null
                ? _selection.SelectedUnits
                : null;

            if (selected == null)
            {
                return;
            }

            for (int i = 0; i < selected.Count; i++)
            {
                UnitMovement unit = selected[i];
                WorkerBuilder worker = unit != null
                    ? unit.GetComponent<WorkerBuilder>()
                    : null;

                if (worker != null)
                {
                    // Solo se convierte en repartidor si hay una fundicion
                    // con plaza libre; si no, conserva su trabajo actual.
                    worker.TrySetHaulForForge();
                }
            }

            RefreshState();
        }

        private void RefreshState()
        {
            ResourceType state = ResourceType.Wood;
            bool found = false;
            bool hauling = false;

            IReadOnlyList<UnitMovement> selected = _selection != null
                ? _selection.SelectedUnits
                : null;

            if (selected != null)
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    UnitMovement unit = selected[i];
                    WorkerBuilder worker = unit != null
                        ? unit.GetComponent<WorkerBuilder>()
                        : null;

                    if (worker != null)
                    {
                        state = worker.PreferredResource;
                        hauling = worker.HaulForForge;
                        found = true;
                        break;
                    }
                }
            }

            bool woodActive = found && !hauling && state == ResourceType.Wood;
            bool copperActive = found && !hauling && state == ResourceType.Copper;
            bool tinActive = found && !hauling && state == ResourceType.Tin;
            bool stoneActive = found && !hauling && state == ResourceType.Stone;
            bool forgeActive = found && hauling;

            if (_woodImage != null)
            {
                _woodImage.color = woodActive ? OnColor : OffColor;
            }

            if (_copperImage != null)
            {
                _copperImage.color = copperActive ? OnColor : OffColor;
            }

            if (_tinImage != null)
            {
                _tinImage.color = tinActive ? OnColor : OffColor;
            }

            if (_stoneImage != null)
            {
                _stoneImage.color = stoneActive ? OnColor : OffColor;
            }

            if (_forgeImage != null)
            {
                _forgeImage.color = forgeActive ? OnColor : OffColor;
            }

            if (_woodLabel != null)
            {
                _woodLabel.text = "Madera (" + WorkerBuilder.CountPreferred(ResourceType.Wood) + ")" +
                    (woodActive ? " - Activa" : "");
            }

            if (_copperLabel != null)
            {
                _copperLabel.text = "Cobre (" + WorkerBuilder.CountPreferred(ResourceType.Copper) + ")" +
                    (copperActive ? " - Activa" : "");
            }

            if (_tinLabel != null)
            {
                _tinLabel.text = "Estaño (" + WorkerBuilder.CountPreferred(ResourceType.Tin) + ")" +
                    (tinActive ? " - Activa" : "");
            }

            if (_stoneLabel != null)
            {
                _stoneLabel.text = "Piedra (" + WorkerBuilder.CountPreferred(ResourceType.Stone) + ")" +
                    (stoneActive ? " - Activa" : "");
            }

            if (_forgeLabel != null)
            {
                _forgeLabel.text = "Fundir (" + WorkerBuilder.CountHauling() + ")" +
                    (forgeActive ? " - Activa" : "");
            }

            if (_stateLabel != null)
            {
                if (!found)
                {
                    _stateLabel.text = "Sin prioridad";
                }
                else if (hauling)
                {
                    _stateLabel.text = "Repartidor de fundición";
                }
                else if (state == ResourceType.None)
                {
                    _stateLabel.text = "Sin prioridad";
                }
                else if (state == ResourceType.Wood)
                {
                    _stateLabel.text = "Priorizan madera";
                }
                else if (state == ResourceType.Copper)
                {
                    _stateLabel.text = "Priorizan cobre";
                }
                else if (state == ResourceType.Tin)
                {
                    _stateLabel.text = "Priorizan estaño";
                }
                else if (state == ResourceType.Stone)
                {
                    _stateLabel.text = "Priorizan piedra";
                }
                else
                {
                    _stateLabel.text = "Sin prioridad";
                }
            }
        }

        private static Text CreateLabel(Transform parent, Font font, string content)
        {
            GameObject go = new GameObject("Label");
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -8f);
            rect.sizeDelta = new Vector2(200f, 28f);

            Text label = go.AddComponent<Text>();
            label.font = font;
            label.text = content;
            label.fontSize = 14;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0.94f, 0.92f, 0.86f, 1f);
            label.raycastTarget = false;
            return label;
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
    }
}
