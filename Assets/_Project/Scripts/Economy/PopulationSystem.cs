using System.Collections.Generic;
using ProjectoRTS.Buildings;
using ProjectoRTS.Units;
using UnityEngine;

namespace ProjectoRTS.Economy
{
    /// <summary>
    /// Population cap with a free base (<see cref="BasePopulation"/>) plus
    /// the bonus of completed houses: every building whose definition has a
    /// <c>populationBonus</c> raises the cap. The current population counts
    /// living Greek units weighted by their <c>populationCost</c>. Queries
    /// are cheap and computed on demand, so the UI polls them.
    /// </summary>
    public static class PopulationSystem
    {
        /// <summary>Population available without any house.</summary>
        public const int BasePopulation = 10;

        /// <summary>Raised when the population numbers may have changed.</summary>
        public static event System.Action Changed;

        /// <summary>Total population granted by completed houses.</summary>
        public static int MaxPopulation
        {
            get
            {
                int total = BasePopulation;
                IReadOnlyList<BuildingRuntime> buildings = BuildingRuntime.All;

                for (int i = 0; i < buildings.Count; i++)
                {
                    BuildingRuntime building = buildings[i];

                    if (building == null || !building.IsAlive)
                    {
                        continue;
                    }

                    if (building.Definition == null || building.Definition.PopulationBonus <= 0)
                    {
                        continue;
                    }

                    ConstructionSite site = building.GetComponent<ConstructionSite>();

                    if (site != null && !site.IsComplete)
                    {
                        continue;
                    }

                    total += building.Definition.PopulationBonus;
                }

                return total;
            }
        }

        /// <summary>Population used by living Greek units.</summary>
        public static int CurrentPopulation
        {
            get
            {
                int total = 0;
                IReadOnlyList<UnitRuntime> units = UnitRegistry.All;

                for (int i = 0; i < units.Count; i++)
                {
                    UnitRuntime unit = units[i];

                    if (unit == null || !unit.IsAlive || unit.Faction != Faction.Greek)
                    {
                        continue;
                    }

                    if (unit.Definition == null || unit.Definition.PopulationCost <= 0)
                    {
                        continue;
                    }

                    total += unit.Definition.PopulationCost;
                }

                return total;
            }
        }

        /// <summary>True when another <paramref name="amount"/> of population fits.</summary>
        public static bool CanAdd(int amount)
        {
            return CurrentPopulation + Mathf.Max(0, amount) <= MaxPopulation;
        }

        /// <summary>Notifies listeners that the numbers may have changed.</summary>
        public static void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
