using System.Collections.Generic;
using ProjectoRTS.Units;
using UnityEngine;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Marks a placed building as a barracks and exposes the units it can
    /// recruit plus a spawn position for newly trained units.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BarracksBuilding : MonoBehaviour
    {
        [SerializeField] private UnitDefinition[] recruitableUnits;
        [SerializeField] private Transform spawnPoint;
        [SerializeField, Min(0.1f)] private float spawnSpacing = 0.8f;

        public IReadOnlyList<UnitDefinition> RecruitableUnits => recruitableUnits;

        /// <summary>
        /// Spawn position for the next unit. The first unit uses the spawn
        /// point exactly; later units are placed around it to avoid stacking.
        /// </summary>
        public Vector3 GetSpawnPosition(int index)
        {
            Vector3 origin = spawnPoint != null
                ? spawnPoint.position
                : transform.position + transform.forward * 2.5f;

            if (index <= 0)
            {
                return origin;
            }

            float angle = index * 137.5f * Mathf.Deg2Rad;
            float radius = spawnSpacing * Mathf.Ceil(Mathf.Sqrt(index));
            return origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        }
    }
}
