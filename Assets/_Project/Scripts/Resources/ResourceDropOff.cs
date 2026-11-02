using System.Collections.Generic;
using ProjectoRTS.Economy;
using UnityEngine;

namespace ProjectoRTS.Resources
{
    /// <summary>
    /// A building point where workers can deposit harvested resources. The
    /// town hall and barracks both use one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceDropOff : MonoBehaviour
    {
        private static readonly List<ResourceDropOff> DropOffs = new List<ResourceDropOff>(8);

        public static IReadOnlyList<ResourceDropOff> All => DropOffs;

        private void OnEnable()
        {
            if (!DropOffs.Contains(this))
            {
                DropOffs.Add(this);
            }
        }

        private void OnDisable()
        {
            DropOffs.Remove(this);
        }

        /// <summary>Adds the carried resources to the shared player pool.</summary>
        public void Deposit(ResourceType type, int amount)
        {
            PlayerEconomy.Add(type, amount);
        }

        /// <summary>Closest active drop-off point, or null.</summary>
        public static ResourceDropOff FindNearest(Vector3 position, float maxDistance)
        {
            ResourceDropOff best = null;
            float bestSqr = maxDistance * maxDistance;

            for (int i = DropOffs.Count - 1; i >= 0; i--)
            {
                ResourceDropOff dropOff = DropOffs[i];

                if (dropOff == null)
                {
                    DropOffs.RemoveAt(i);
                    continue;
                }

                if (!dropOff.isActiveAndEnabled)
                {
                    continue;
                }

                float sqr = (dropOff.transform.position - position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = dropOff;
                }
            }

            return best;
        }
    }
}
