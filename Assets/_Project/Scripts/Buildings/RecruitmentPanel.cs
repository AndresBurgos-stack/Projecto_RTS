using System.Collections.Generic;
using ProjectoRTS.Economy;
using ProjectoRTS.UI;
using ProjectoRTS.Units;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Simple uGUI panel that lists the units available in the active
    /// barracks. Buttons are generated at runtime from the barracks data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RecruitmentPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RectTransform buttonContainer;
        [SerializeField] private Button buttonTemplate;
        [SerializeField] private Text title;
        [SerializeField] private RecruitmentManager manager;

        private const int GridColumns = 2;
        private static readonly Vector2 GridCellSize = new Vector2(84f, 84f);
        private static readonly Vector2 GridSpacing = new Vector2(20f, 10f);

        private readonly List<GameObject> _buttons = new List<GameObject>();
        private readonly List<UnitDefinition> _buttonUnits = new List<UnitDefinition>();
        private bool _initialized;
        private string _activeBarracksText = "Reclutamiento";
        private float _nextRefresh;

        private void Awake()
        {
            EnsureInitialized();
            Hide();
        }

        private void OnEnable()
        {
            PlayerEconomy.Changed += RefreshAffordability;
        }

        private void OnDisable()
        {
            PlayerEconomy.Changed -= RefreshAffordability;
        }

        /// <summary>Shows the panel and builds one button per recruitable unit.</summary>
        public void Show(BarracksBuilding barracks, RecruitmentManager recruitmentManager)
        {
            EnsureInitialized();
            DisableListLayout();
            manager = recruitmentManager;
            ClearButtons();

            if (title != null)
            {
                _activeBarracksText = barracks != null
                    ? $"Reclutamiento - {barracks.name}"
                    : "Reclutamiento";
            }

            RefreshPopulationTitle();

            if (barracks != null && buttonContainer != null && buttonTemplate != null)
            {
                List<UnitDefinition> recruitable =
                    new List<UnitDefinition>(barracks.RecruitableUnits.Count);

                foreach (UnitDefinition unit in barracks.RecruitableUnits)
                {
                    if (unit != null && unit.Prefab != null)
                    {
                        recruitable.Add(unit);
                    }
                }

                for (int i = 0; i < recruitable.Count; i++)
                {
                    UnitDefinition unit = recruitable[i];
                    Button button = Instantiate(buttonTemplate, buttonContainer);
                    button.gameObject.SetActive(true);
                    PositionButton(
                        button.transform as RectTransform,
                        i,
                        recruitable.Count);

                    Text label = button.GetComponentInChildren<Text>(true);
                    Sprite sprite = UiIconCatalog.GetUnitIcon(unit);

                    if (sprite != null)
                    {
                        Image background = button.GetComponent<Image>();
                        if (background != null)
                        {
                            background.color = new Color(0f, 0f, 0f, 0f);
                        }

                        Image icon = UiIconFactory.CreateIcon(
                            button.transform,
                            "Icon",
                            sprite);
                        button.targetGraphic = icon;
                        button.colors = UiButtonStyle.CreateIconColors();

                        if (label != null)
                        {
                            label.gameObject.SetActive(false);
                        }
                    }
                    else if (label != null)
                    {
                        label.fontSize = 14;
                        label.text =
                            $"Reclutar {unit.DisplayName} ({unit.WoodCost}M {unit.BronzeCost}B)";
                    }

                    TooltipTrigger tooltip = button.GetComponent<TooltipTrigger>();
                    if (tooltip == null)
                    {
                        tooltip = button.gameObject.AddComponent<TooltipTrigger>();
                    }

                    tooltip.Configure(
                        unit.DisplayName,
                        UnitTooltipText.GetBody(unit));

                    UnitDefinition captured = unit;
                    button.onClick.AddListener(() => manager.Recruit(captured));
                    _buttons.Add(button.gameObject);
                    _buttonUnits.Add(unit);
                }
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            RefreshAffordability();
        }

        private void DisableListLayout()
        {
            if (buttonContainer == null)
            {
                return;
            }

            LayoutGroup[] groups = buttonContainer.GetComponents<LayoutGroup>();
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].enabled = false;
            }
        }

        private static void PositionButton(RectTransform rect, int index, int total)
        {
            if (rect == null || total <= 0)
            {
                return;
            }

            int columns = Mathf.Min(GridColumns, total);
            int rows = Mathf.CeilToInt(total / (float)columns);
            int column = index % columns;
            int row = index / columns;

            float x = (column - (columns - 1) * 0.5f) *
                (GridCellSize.x + GridSpacing.x);
            float y = ((rows - 1) * 0.5f - row) *
                (GridCellSize.y + GridSpacing.y);

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = GridCellSize;
            rect.anchoredPosition = new Vector2(x, y);
        }

        public void Hide()
        {
            EnsureInitialized();

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            ClearButtons();
        }

        private void Update()
        {
            if (panelRoot == null || !panelRoot.activeSelf)
            {
                return;
            }

            if (Time.time < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.time + 0.5f;
            RefreshPopulationTitle();
            RefreshAffordability();
        }

        private void RefreshPopulationTitle()
        {
            if (title != null)
            {
                title.text = _activeBarracksText +
                    $" - Población: {PopulationSystem.CurrentPopulation}/{PopulationSystem.MaxPopulation}";
            }
        }

        private void RefreshAffordability()
        {
            for (int i = 0; i < _buttons.Count && i < _buttonUnits.Count; i++)
            {
                if (_buttons[i] == null)
                {
                    continue;
                }

                Button button = _buttons[i].GetComponent<Button>();
                if (button == null)
                {
                    continue;
                }

                UnitDefinition unit = _buttonUnits[i];
                button.interactable = unit != null &&
                    PlayerEconomy.CanAfford(unit.WoodCost, unit.BronzeCost) &&
                    PopulationSystem.CanAdd(unit.PopulationCost);
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (manager == null)
            {
                manager = FindFirstObjectByType<RecruitmentManager>(FindObjectsInactive.Include);
            }

            if (buttonTemplate != null)
            {
                buttonTemplate.gameObject.SetActive(false);
            }

            _initialized = true;
        }

        private void ClearButtons()
        {
            foreach (GameObject button in _buttons)
            {
                if (button != null)
                {
                    button.SetActive(false);
                    Destroy(button);
                }
            }

            _buttons.Clear();
            _buttonUnits.Clear();
        }
    }
}
