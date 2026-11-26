using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Number-key shortcuts for testing placement without a UI. Assign up to
    /// nine definitions in the Inspector; definitions stored under
    /// Resources/Buildings are appended automatically so new tiers can be
    /// added without editing the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingPlacementHotkeys : MonoBehaviour
    {
        private const string ExtraDefinitionsFolder = "Buildings";

        [SerializeField] private BuildingPlacementController placement;
        [SerializeField] private BuildingDefinition[] definitions;

        private BuildingDefinition[] _runtimeDefinitions;

        /// <summary>
        /// Building definitions bound to the number keys, grouped by tier so
        /// the build menu can draw one labelled row per tier.
        /// </summary>
        public BuildingDefinition[] Definitions
        {
            get
            {
                if (_runtimeDefinitions == null)
                {
                    BuildRuntimeDefinitions();
                }

                return _runtimeDefinitions;
            }
        }

        private void Awake()
        {
            if (placement == null)
            {
                placement = FindFirstObjectByType<BuildingPlacementController>();
            }

            BuildRuntimeDefinitions();
        }

        private void Update()
        {
            BuildingDefinition[] runtimeDefinitions = Definitions;
            if (placement == null || runtimeDefinitions == null || Keyboard.current == null)
            {
                return;
            }

            int count = Mathf.Min(runtimeDefinitions.Length, 9);
            for (int i = 0; i < count; i++)
            {
                if (runtimeDefinitions[i] == null)
                {
                    continue;
                }

                if (Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
                {
                    placement.BeginPlacement(runtimeDefinitions[i]);
                    return;
                }
            }
        }

        private void BuildRuntimeDefinitions()
        {
            List<BuildingDefinition> merged = new List<BuildingDefinition>(9);

            if (definitions != null)
            {
                for (int i = 0; i < definitions.Length; i++)
                {
                    AddUnique(merged, definitions[i]);
                }
            }

            BuildingDefinition[] extras =
                UnityEngine.Resources.LoadAll<BuildingDefinition>(ExtraDefinitionsFolder);
            for (int i = 0; i < extras.Length; i++)
            {
                AddUnique(merged, extras[i]);
            }

            // Stable insertion sort: wood tier first, stone tier below it,
            // keeping the authored order inside each tier.
            for (int i = 1; i < merged.Count; i++)
            {
                BuildingDefinition current = merged[i];
                int j = i - 1;
                while (j >= 0 && (int)merged[j].Tier > (int)current.Tier)
                {
                    merged[j + 1] = merged[j];
                    j--;
                }

                merged[j + 1] = current;
            }

            _runtimeDefinitions = merged.ToArray();
        }

        private static void AddUnique(
            List<BuildingDefinition> list,
            BuildingDefinition definition)
        {
            if (definition != null && !list.Contains(definition))
            {
                list.Add(definition);
            }
        }
    }
}
