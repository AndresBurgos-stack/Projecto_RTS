using System;
using ProjectoRTS.Resources;
using UnityEngine;

namespace ProjectoRTS.Economy
{
    /// <summary>
    /// Minimal shared resource pool for the Greek player: wood and bronze.
    /// Buildings and recruitment spend from it; the HUD listens to Changed.
    /// </summary>
    public static class PlayerEconomy
    {
        public const int StartingWood = 500;
        public const int StartingBronze = 300;

        public static int Wood { get; private set; }
        public static int Bronze { get; private set; }

        /// <summary>Raised whenever the resource amounts change.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Wood = StartingWood;
            Bronze = StartingBronze;
            Changed = null;
        }

        public static bool CanAfford(int wood, int bronze)
        {
            return Wood >= Mathf.Max(0, wood) && Bronze >= Mathf.Max(0, bronze);
        }

        public static bool TrySpend(int wood, int bronze)
        {
            wood = Mathf.Max(0, wood);
            bronze = Mathf.Max(0, bronze);

            if (!CanAfford(wood, bronze))
            {
                return false;
            }

            Wood -= wood;
            Bronze -= bronze;
            Changed?.Invoke();
            return true;
        }

        public static void Add(int wood, int bronze)
        {
            Wood += Mathf.Max(0, wood);
            Bronze += Mathf.Max(0, bronze);
            Changed?.Invoke();
        }

        /// <summary>Adds a single resource type, used by worker deposits.</summary>
        public static void Add(ResourceType type, int amount)
        {
            amount = Mathf.Max(0, amount);

            if (amount <= 0)
            {
                return;
            }

            if (type == ResourceType.Wood)
            {
                Wood += amount;
            }
            else
            {
                Bronze += amount;
            }

            Changed?.Invoke();
        }

        public static void Refund(int wood, int bronze)
        {
            Add(wood, bronze);
        }
    }
}
