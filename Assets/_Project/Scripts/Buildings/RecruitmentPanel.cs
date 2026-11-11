using System.Collections.Generic;
using ProjectoRTS.Economy;
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
                foreach (UnitDefinition unit in barracks.RecruitableUnits)
                {
                    if (unit == null || unit.Prefab == null)
                    {
                        continue;
                    }

                    Button button = Instantiate(buttonTemplate, buttonContainer);
                    button.gameObject.SetActive(true);

                    Text label = button.GetComponentInChildren<Text>(true);
                    if (label != null)
                    {
                        label.fontSize = 14;
                        label.text =
                            $"Reclutar {unit.DisplayName} ({unit.WoodCost}M {unit.BronzeCost}B)";
                    }

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
                    Destroy(button);
                }
            }

            _buttons.Clear();
            _buttonUnits.Clear();
        }
    }
}
