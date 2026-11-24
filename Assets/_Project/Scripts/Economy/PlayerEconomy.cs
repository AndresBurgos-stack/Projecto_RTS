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
        public const int StartingCopper = 100;
        public const int StartingTin = 50;

        public static int Wood { get; private set; }
        public static int Bronze { get; private set; }
        public static int Copper { get; private set; }
        public static int Tin { get; private set; }

        /// <summary>Raised whenever the resource amounts change.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Wood = StartingWood;
            Bronze = StartingBronze;
            Copper = StartingCopper;
            Tin = StartingTin;
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

        /// <summary>Current amount of a single resource type in the pool.</summary>
        public static int Amount(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood:
                    return Wood;
                case ResourceType.Copper:
                    return Copper;
                case ResourceType.Tin:
                    return Tin;
                default:
                    return Bronze;
            }
        }

        /// <summary>True when the pool holds at least the given amount of a single resource type.</summary>
        public static bool CanAfford(ResourceType type, int amount)
        {
            amount = Mathf.Max(0, amount);

            switch (type)
            {
                case ResourceType.Wood:
                    return Wood >= amount;
                case ResourceType.Copper:
                    return Copper >= amount;
                case ResourceType.Tin:
                    return Tin >= amount;
                default:
                    return Bronze >= amount;
            }
        }

        /// <summary>Spends a single resource type. Returns false when the pool cannot cover it.</summary>
        public static bool TrySpend(ResourceType type, int amount)
        {
            amount = Mathf.Max(0, amount);

            if (!CanAfford(type, amount))
            {
                return false;
            }

            switch (type)
            {
                case ResourceType.Wood:
                    Wood -= amount;
                    break;
                case ResourceType.Copper:
                    Copper -= amount;
                    break;
                case ResourceType.Tin:
                    Tin -= amount;
                    break;
                default:
                    Bronze -= amount;
                    break;
            }

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

            switch (type)
            {
                case ResourceType.Wood:
                    Wood += amount;
                    break;
                case ResourceType.Copper:
                    Copper += amount;
                    break;
                case ResourceType.Tin:
                    Tin += amount;
                    break;
                default:
                    Bronze += amount;
                    break;
            }

            Changed?.Invoke();
        }

        public static void Refund(int wood, int bronze)
        {
            Add(wood, bronze);
        }
    }
}
