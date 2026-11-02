using System.Collections.Generic;
using UnityEngine;

namespace ProjectoRTS.Resources
{
    /// <summary>
    /// A finite wood or bronze node that workers can harvest. Nodes register
    /// themselves so workers can find the closest one without scene lookups.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceNode : MonoBehaviour
    {
        private static readonly List<ResourceNode> Nodes = new List<ResourceNode>(64);

        [SerializeField] private ResourceType type = ResourceType.Wood;
        [SerializeField, Min(1)] private int amount = 250;
        [SerializeField] private bool hideWhenDepleted = true;

        private bool _depleted;

        public ResourceType Type => type;
        public int Amount => amount;
        public bool IsDepleted => _depleted || amount <= 0;

        /// <summary>All nodes currently registered in the scene.</summary>
        public static IReadOnlyList<ResourceNode> All => Nodes;

        private void OnEnable()
        {
            if (!Nodes.Contains(this))
            {
                Nodes.Add(this);
            }
        }

        private void OnDisable()
        {
            Nodes.Remove(this);
        }

        /// <summary>Sets up a node created from code, such as the demo scene.</summary>
        public void Configure(ResourceType resourceType, int resourceAmount)
        {
            type = resourceType;
            amount = Mathf.Max(1, resourceAmount);
            _depleted = false;
        }

        /// <summary>
        /// Takes up to the requested amount from the node and returns what was
        /// actually harvested. Depleted nodes are hidden by default.
        /// </summary>
        public int Harvest(int requested)
        {
            if (IsDepleted || requested <= 0)
            {
                return 0;
            }

            int harvested = Mathf.Min(requested, amount);
            amount -= harvested;

            if (amount <= 0)
            {
                amount = 0;
                _depleted = true;

                if (hideWhenDepleted)
                {
                    gameObject.SetActive(false);
                }
            }

            return harvested;
        }

        /// <summary>Closest non-depleted node of the given type, or null.</summary>
        public static ResourceNode FindNearest(
            Vector3 position,
            ResourceType resourceType,
            float maxDistance)
        {
            ResourceNode best = null;
            float bestSqr = maxDistance * maxDistance;

            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                ResourceNode node = Nodes[i];

                if (node == null)
                {
                    Nodes.RemoveAt(i);
                    continue;
                }

                if (node.IsDepleted || node.type != resourceType)
                {
                    continue;
                }

                float sqr = (node.transform.position - position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = node;
                }
            }

            return best;
        }
    }
}
