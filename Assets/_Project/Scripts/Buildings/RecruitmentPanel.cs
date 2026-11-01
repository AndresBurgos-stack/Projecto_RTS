using System.Collections.Generic;
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
        private bool _initialized;

        private void Awake()
        {
            EnsureInitialized();
            Hide();
        }

        /// <summary>Shows the panel and builds one button per recruitable unit.</summary>
        public void Show(BarracksBuilding barracks, RecruitmentManager recruitmentManager)
        {
            EnsureInitialized();
            manager = recruitmentManager;
            ClearButtons();

            if (title != null)
            {
                title.text = barracks != null
                    ? $"Reclutamiento - {barracks.name}"
                    : "Reclutamiento";
            }

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
                        label.text = $"Reclutar {unit.DisplayName}";
                    }

                    UnitDefinition captured = unit;
                    button.onClick.AddListener(() => manager.Recruit(captured));
                    _buttons.Add(button.gameObject);
                }
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }
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
        }
    }
}
