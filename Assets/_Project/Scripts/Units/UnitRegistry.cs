using System.Collections.Generic;
using UnityEngine;

namespace ProjectoRTS.Units
{
    /// <summary>
    /// Runtime registry of unit instances. Selection, targeting and health
    /// bars use it instead of scene-wide searches, which keeps per-frame
    /// queries cheap as the army grows.
    /// </summary>
    public static class UnitRegistry
    {
        private static readonly List<UnitRuntime> Units = new List<UnitRuntime>(64);

        public static IReadOnlyList<UnitRuntime> All => Units;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Units.Clear();
        }

        /// <summary>Adds a unit to the registry. Called by <see cref="UnitRuntime"/>.</summary>
        public static void Register(UnitRuntime unit)
        {
            if (unit == null || Units.Contains(unit))
            {
                return;
            }

            Units.Add(unit);
        }

        /// <summary>Removes a unit from the registry. Called by <see cref="UnitRuntime"/>.</summary>
        public static void Unregister(UnitRuntime unit)
        {
            if (unit != null)
            {
                Units.Remove(unit);
            }
        }

        /// <summary>
        /// Returns the closest living unit of another faction within a
        /// horizontal radius, or null when there is no candidate. Melee
        /// attackers pass <paramref name="excludeMounted"/> to skip archers
        /// standing on walls, which they cannot reach.
        /// </summary>
        public static UnitRuntime FindNearestEnemy(
            Vector3 position,
            Faction faction,
            float maxDistance,
            bool excludeMounted = false)
        {
            UnitRuntime best = null;
            float bestSqrDistance = maxDistance * maxDistance;

            for (int i = Units.Count - 1; i >= 0; i--)
            {
                UnitRuntime unit = Units[i];
                if (unit == null)
                {
                    Units.RemoveAt(i);
                    continue;
                }

                if (!unit.IsAlive || unit.Faction == faction || unit.Faction == Faction.None)
                {
                    continue;
                }

                if (excludeMounted && unit.IsMounted)
                {
                    continue;
                }

                Vector3 delta = unit.transform.position - position;
                delta.y = 0f;
                float sqrDistance = delta.sqrMagnitude;
                if (sqrDistance <= bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    best = unit;
                }
            }

            return best;
        }
    }
}
