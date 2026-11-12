using System.Collections.Generic;
using ProjectoRTS.Buildings;
using UnityEngine;

namespace ProjectoRTS.Resources
{
    /// <summary>
    /// A finite wood or bronze node that workers can harvest. Nodes register
    /// themselves so workers can find the closest one without scene lookups.
    /// Each node accepts a limited number of workers (claims) so gatherers
    /// spread across the map instead of piling on a single tree.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceNode : MonoBehaviour
    {
        private static readonly List<ResourceNode> Nodes = new List<ResourceNode>(64);

        [SerializeField] private ResourceType type = ResourceType.Wood;
        [SerializeField, Min(1)] private int amount = 250;
        [SerializeField] private bool hideWhenDepleted = true;
        [SerializeField, Min(1)] private int capacity = 2;

        [Header("Gather Focus")]
        [SerializeField] private Transform gatherPoint;
        [SerializeField, Min(0f)] private float gatherRadius = 0.3f;

        private readonly List<WorkerBuilder> _claimants = new List<WorkerBuilder>(2);
        private bool _depleted;
        private Vector3 _gatherPoint;
        private float _gatherRadius;
        private bool _gatherFocusReady;

        public ResourceType Type => type;
        public int Amount => amount;
        public bool IsDepleted => _depleted || amount <= 0;

        /// <summary>
        /// World position the workers should walk to and face when gathering.
        /// For the bronze nodes this is the centre of the visible rock, not
        /// the node pivot, because the rock prefab is offset from the pivot.
        /// </summary>
        public Vector3 GatherPoint
        {
            get
            {
                if (!_gatherFocusReady)
                {
                    RefreshGatherFocus();
                }

                return _gatherPoint;
            }
        }

        /// <summary>Horizontal radius of the node around <see cref="GatherPoint"/>.</summary>
        public float GatherRadius
        {
            get
            {
                if (!_gatherFocusReady)
                {
                    RefreshGatherFocus();
                }

                return _gatherRadius;
            }
        }

        /// <summary>Number of workers currently reserved on this node.</summary>
        public int ClaimedCount
        {
            get
            {
                PruneClaims();
                return _claimants.Count;
            }
        }

        /// <summary>True while another worker can still claim this node.</summary>
        public bool HasFreeSlot
        {
            get
            {
                PruneClaims();
                return _claimants.Count < capacity;
            }
        }

        /// <summary>All nodes currently registered in the scene.</summary>
        public static IReadOnlyList<ResourceNode> All => Nodes;

        private void Awake()
        {
            RefreshGatherFocus();
        }

        private void OnValidate()
        {
            _gatherFocusReady = false;
        }

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
            _gatherFocusReady = false;
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

        /// <summary>
        /// Reserves a gathering slot for the worker. Returns true when the
        /// worker already owns a slot or a new one was granted.
        /// </summary>
        public bool TryClaim(WorkerBuilder worker)
        {
            PruneClaims();

            if (worker == null)
            {
                return false;
            }

            if (_claimants.Contains(worker))
            {
                return true;
            }

            if (_claimants.Count >= capacity)
            {
                return false;
            }

            _claimants.Add(worker);
            return true;
        }

        /// <summary>Frees the worker's slot on this node.</summary>
        public void Release(WorkerBuilder worker)
        {
            if (worker != null)
            {
                _claimants.Remove(worker);
            }
        }

        /// <summary>True when the worker already reserved a slot here.</summary>
        public bool IsClaimedBy(WorkerBuilder worker)
        {
            return worker != null && _claimants.Contains(worker);
        }

        private void PruneClaims()
        {
            for (int i = _claimants.Count - 1; i >= 0; i--)
            {
                WorkerBuilder claimant = _claimants[i];

                if (claimant == null || !claimant.isActiveAndEnabled)
                {
                    _claimants.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Resolves the point workers should gather from. An explicit
        /// <see cref="gatherPoint"/> wins; otherwise the bronze rock child is
        /// used, and any other node falls back to its own pivot.
        /// </summary>
        private void RefreshGatherFocus()
        {
            _gatherFocusReady = true;

            if (gatherPoint != null)
            {
                _gatherPoint = gatherPoint.position;
                _gatherRadius = Mathf.Max(0f, gatherRadius);
                return;
            }

            Transform bronze = transform.Find("Bronze");

            if (bronze != null)
            {
                _gatherPoint = ComputeGatherFocus(bronze, out _gatherRadius);
                return;
            }

            _gatherPoint = transform.position;
            _gatherRadius = Mathf.Max(0f, gatherRadius);
        }

        /// <summary>
        /// Horizontal centre and radius of every renderer under the given
        /// root. Used for the bronze rock, whose prefab is offset from the
        /// node pivot.
        /// </summary>
        private Vector3 ComputeGatherFocus(Transform root, out float radius)
        {
            radius = Mathf.Max(0f, gatherRadius);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null ||
                    renderer is ParticleSystemRenderer ||
                    renderer is TrailRenderer ||
                    renderer is LineRenderer)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                return root.position;
            }

            Vector3 center = bounds.center;
            center.y = transform.position.y;
            radius = Mathf.Max(radius, Mathf.Max(bounds.extents.x, bounds.extents.z));
            return center;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 point = transform.position;
            float radius = Mathf.Max(0f, gatherRadius);

            if (gatherPoint != null)
            {
                point = gatherPoint.position;
            }
            else
            {
                Transform bronze = transform.Find("Bronze");

                if (bronze != null)
                {
                    point = ComputeGatherFocus(bronze, out radius);
                }
            }

            Gizmos.color = new Color(1f, 0.8f, 0.1f, 1f);
            Gizmos.DrawWireSphere(point, Mathf.Max(0.05f, radius));
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, point);
        }

        /// <summary>
        /// Closest non-depleted node of the given type with a free slot, or
        /// null. Nodes already claimed by the worker count as available for it.
        /// </summary>
        public static ResourceNode FindNearestAvailable(
            Vector3 position,
            ResourceType resourceType,
            float maxDistance,
            WorkerBuilder worker)
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

                if (!node.HasFreeSlot && !node.IsClaimedBy(worker))
                {
                    continue;
                }

                float sqr = (node.GatherPoint - position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = node;
                }
            }

            return best;
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

                float sqr = (node.GatherPoint - position).sqrMagnitude;
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
