using ProjectoRTS.Combat;
using ProjectoRTS.Units;
using UnityEngine;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Tracks the selected barracks, shows its recruitment panel and spawns
    /// the chosen unit next to it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RecruitmentManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RecruitmentPanel panel;
        [SerializeField] private Transform unitParent;

        [Header("Spawning")]
        [SerializeField] private string unitParentName = "Units";

        private BarracksBuilding _activeBarracks;
        private int _spawnIndex;

        public BarracksBuilding ActiveBarracks => _activeBarracks;

        private void Awake()
        {
            if (panel == null)
            {
                panel = FindFirstObjectByType<RecruitmentPanel>(FindObjectsInactive.Include);
            }

            if (unitParent == null)
            {
                GameObject root = GameObject.Find(unitParentName);
                unitParent = root != null ? root.transform : transform;
            }
        }

        private void Start()
        {
            if (panel != null)
            {
                panel.Hide();
            }
        }

        /// <summary>Opens the recruitment panel for the clicked barracks.</summary>
        public void SelectBarracks(BarracksBuilding barracks)
        {
            if (barracks == null)
            {
                ClearSelection();
                return;
            }

            if (_activeBarracks != barracks)
            {
                _spawnIndex = 0;
            }

            _activeBarracks = barracks;

            if (panel != null)
            {
                panel.Show(barracks, this);
            }
        }

        /// <summary>Closes the recruitment panel and forgets the selection.</summary>
        public void ClearSelection()
        {
            _activeBarracks = null;

            if (panel != null)
            {
                panel.Hide();
            }
        }

        /// <summary>Recruits one unit next to the active barracks.</summary>
        public void Recruit(UnitDefinition unit)
        {
            if (_activeBarracks == null || unit == null || unit.Prefab == null)
            {
                return;
            }

            Vector3 position = _activeBarracks.GetSpawnPosition(_spawnIndex);
            _spawnIndex++;

            GameObject instance = Instantiate(unit.Prefab, position, Quaternion.identity, unitParent);
            instance.name = unit.DisplayName;

            UnitRuntime runtime = instance.GetComponent<UnitRuntime>();
            if (runtime != null)
            {
                runtime.Initialize(unit);
            }

            UnitMovement movement = instance.GetComponent<UnitMovement>();
            if (movement != null)
            {
                movement.Initialize(unit);
            }

            UnitCombat combat = instance.GetComponent<UnitCombat>();
            if (combat != null)
            {
                combat.Initialize(unit);
            }
        }
    }
}
