using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Optional number-key shortcuts for testing placement without a UI.
    /// Assign up to nine building definitions in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingPlacementHotkeys : MonoBehaviour
    {
        [SerializeField] private BuildingPlacementController placement;
        [SerializeField] private BuildingDefinition[] definitions;

        /// <summary>Building definitions bound to the number keys, in order.</summary>
        public BuildingDefinition[] Definitions => definitions;

        private void Awake()
        {
            if (placement == null)
            {
                placement = FindFirstObjectByType<BuildingPlacementController>();
            }
        }

        private void Update()
        {
            if (placement == null || definitions == null || Keyboard.current == null)
            {
                return;
            }

            int count = Mathf.Min(definitions.Length, 9);
            for (int i = 0; i < count; i++)
            {
                if (definitions[i] == null)
                {
                    continue;
                }

                if (Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
                {
                    placement.BeginPlacement(definitions[i]);
                    return;
                }
            }
        }
    }
}
