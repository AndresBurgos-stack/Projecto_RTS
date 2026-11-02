using System.Collections.Generic;
using ProjectoRTS.Resources;
using ProjectoRTS.Units;
using UnityEngine;
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
        private Image _bronzeImage;
        private Text _woodLabel;
        private Text _bronzeLabel;
        private Text _stateLabel;
        private float _nextRefresh;

        private static readonly Color OnColor = new Color(0.2f, 0.65f, 0.3f, 1f);
        private static readonly Color OffColor = new Color(0.72f, 0.2f, 0.16f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
            {
                return;
            }

            GameObject host = new GameObject("Worker Priority Panel");
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
            rect.sizeDelta = new Vector2(220f, 200f);

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

            CreatePriorityButton(
                _root.transform,
                font,
                10f,
                "Madera",
                ResourceType.Wood,
                out _woodImage,
                out _woodLabel);
            CreatePriorityButton(
                _root.transform,
                font,
                -55f,
                "Bronce",
                ResourceType.Bronze,
                out _bronzeImage,
                out _bronzeLabel);
        }

        private void CreatePriorityButton(
            Transform parent,
            Font font,
            float y,
            string title,
            ResourceType type,
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
            button.onClick.AddListener(() => SetPriority(type));

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
                    worker.PreferredResource = type;
                }
            }

            RefreshState();
        }

        private void RefreshState()
        {
            ResourceType state = ResourceType.Wood;
            bool found = false;

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
                        found = true;
                        break;
                    }
                }
            }

            bool woodActive = found && state == ResourceType.Wood;
            bool bronzeActive = found && state == ResourceType.Bronze;

            if (_woodImage != null)
            {
                _woodImage.color = woodActive ? OnColor : OffColor;
            }

            if (_bronzeImage != null)
            {
                _bronzeImage.color = bronzeActive ? OnColor : OffColor;
            }

            if (_woodLabel != null)
            {
                _woodLabel.text = "Madera (" + WorkerBuilder.CountPreferred(ResourceType.Wood) + ")" +
                    (woodActive ? " - Activa" : "");
            }

            if (_bronzeLabel != null)
            {
                _bronzeLabel.text = "Bronce (" + WorkerBuilder.CountPreferred(ResourceType.Bronze) + ")" +
                    (bronzeActive ? " - Activa" : "");
            }

            if (_stateLabel != null)
            {
                if (!found || state == ResourceType.None)
                {
                    _stateLabel.text = "Sin prioridad";
                }
                else if (state == ResourceType.Wood)
                {
                    _stateLabel.text = "Priorizan madera";
                }
                else
                {
                    _stateLabel.text = "Priorizan bronce";
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
