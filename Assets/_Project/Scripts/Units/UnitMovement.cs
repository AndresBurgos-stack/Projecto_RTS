using UnityEngine;
using UnityEngine.AI;
namespace ProjectoRTS.Units
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class UnitMovement : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private GameObject selectionIndicator;

        [Header("Anti-Stuck")]
        [SerializeField, Min(0.2f)] private float stallTime = 1.2f;
        [SerializeField, Min(0.2f)] private float stallCheckInterval = 0.3f;
        [SerializeField, Min(0.5f)] private float escapeRadius = 1.5f;
        [SerializeField, Min(1)] private int maxRepathAttempts = 2;

        private Animator _animator;
        private float _turnSpeed = 720f;

        private Vector3 _lastDestination;
        private float _stallCheckTimer;
        private float _stallTimer;
        private int _repathAttempts;
        private int _escapeAttempts;
        private int _recoveryStage;

        public NavMeshAgent Agent => agent;
        public UnitRuntime Runtime { get; private set; }
        public bool IsSelected { get; private set; }
        public bool IsAlive => Runtime == null || Runtime.IsAlive;

        /// <summary>Raised when every recovery attempt failed and the unit is stuck.</summary>
        public event System.Action Stuck;

        private void Update()
        {
            UpdateAntiStuck();

            if (_animator == null || agent == null)
            {
                return;
            }

            float speed = agent.enabled && agent.isOnNavMesh
                ? agent.velocity.magnitude
                : 0f;
            float normalized = Mathf.Clamp01(speed / Mathf.Max(0.1f, agent.speed));
            _animator.SetFloat("Speed", normalized);

            UpdateAntiStuck();
        }

        /// <summary>
        /// Detects units that stopped making progress while a path exists and
        /// tries to recover: re-path, then escape sideways, then raise
        /// <see cref="Stuck"/> so controllers can re-decide.
        /// </summary>
        private void UpdateAntiStuck()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                _stallTimer = 0f;
                return;
            }

            _stallCheckTimer += Time.deltaTime;
            if (_stallCheckTimer < stallCheckInterval)
            {
                return;
            }

            _stallCheckTimer = 0f;

            bool tryingToMove = agent.hasPath &&
                !agent.pathPending &&
                agent.remainingDistance > agent.stoppingDistance + 0.15f;

            if (!tryingToMove)
            {
                _stallTimer = 0f;
                return;
            }

            if (agent.velocity.sqrMagnitude > 0.04f)
            {
                _stallTimer = 0f;
                _recoveryStage = 0;
                _repathAttempts = 0;
                _escapeAttempts = 0;
                return;
            }

            _stallTimer += stallCheckInterval;
            if (_stallTimer < stallTime)
            {
                return;
            }

            _stallTimer = 0f;

            if (_recoveryStage == 0)
            {
                _repathAttempts++;

                if (_repathAttempts <= maxRepathAttempts)
                {
                    agent.ResetPath();
                    agent.SetDestination(_lastDestination);
                    return;
                }

                _recoveryStage = 1;
            }

            TryEscapeOrStuck();
        }

        private void TryEscapeOrStuck()
        {
            _escapeAttempts++;

            if (_escapeAttempts > 2)
            {
                _recoveryStage = 0;
                _repathAttempts = 0;
                _escapeAttempts = 0;
                agent.ResetPath();
                Stuck?.Invoke();
                return;
            }

            for (int i = 0; i < 8; i++)
            {
                float angle = (i / 8f) * Mathf.PI * 2f + Random.value;
                Vector3 candidate = transform.position +
                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * escapeRadius;

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, escapeRadius, NavMesh.AllAreas))
                {
                    _lastDestination = hit.position;
                    _stallTimer = 0f;
                    agent.isStopped = false;
                    agent.ResetPath();
                    agent.SetDestination(hit.position);
                    return;
                }
            }

            Stuck?.Invoke();
        }

        public bool IsMoving
        {
            get
            {
                if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped)
                {
                    return false;
                }

                if (agent.pathPending)
                {
                    return true;
                }

                return agent.hasPath &&
                    agent.remainingDistance > agent.stoppingDistance + 0.05f;
            }
        }

        private void Awake()
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            Runtime = GetComponent<UnitRuntime>();
            _animator = GetComponentInChildren<Animator>(true);
            SetSelected(false);
        }

        private void OnEnable()
        {
            if (Runtime != null)
            {
                Runtime.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (Runtime != null)
            {
                Runtime.Died -= HandleDied;
            }
        }

        private void Start()
        {
            if (Runtime != null && Runtime.Definition != null)
            {
                Initialize(Runtime.Definition);
            }
        }

        public void Initialize(UnitDefinition definition)
        {
            if (Runtime == null)
            {
                Runtime = GetComponent<UnitRuntime>();
            }

            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            if (agent == null || definition == null)
            {
                return;
            }

            _turnSpeed = definition.TurnSpeed;
            agent.speed = definition.MoveSpeed;
            agent.angularSpeed = definition.TurnSpeed;
            agent.stoppingDistance = definition.StoppingDistance;
            agent.acceleration = Mathf.Max(8f, definition.MoveSpeed * 4f);
            agent.isStopped = false;

            if (Application.isPlaying &&
                !agent.isOnNavMesh &&
                NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        /// <summary>Sends the unit to a position on the NavMesh.</summary>
        public void MoveTo(Vector3 destination)
        {
            if (!IsAlive || agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            _lastDestination = destination;
            _stallTimer = 0f;
            _stallCheckTimer = 0f;
            _repathAttempts = 0;
            _escapeAttempts = 0;
            _recoveryStage = 0;

            agent.isStopped = false;
            agent.SetDestination(destination);
        }

        /// <summary>Stops the unit and clears its current path.</summary>
        public void Stop()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            if (agent.isStopped && !agent.hasPath)
            {
                return;
            }

            agent.isStopped = true;
            agent.ResetPath();
        }

        /// <summary>
        /// Horizontal bounds of a target's solid colliders (or its renderers
        /// when it has none). Buildings are much larger than their transform
        /// origin, so combat needs the real footprint to find an approach.
        /// </summary>
        public static bool TryGetTargetBounds(Transform target, out Bounds bounds)
        {
            bounds = default;

            if (target == null)
            {
                return false;
            }

            bool hasBounds = false;
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];

                if (collider == null || collider.isTrigger || !collider.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            if (hasBounds)
            {
                return true;
            }

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

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

            return hasBounds;
        }

        /// <summary>Closest point on the target's horizontal footprint.</summary>
        public static Vector3 ClosestPointOnFootprint(Bounds bounds, Vector3 from)
        {
            Vector3 probe = new Vector3(from.x, bounds.center.y, from.z);
            Vector3 closest = bounds.ClosestPoint(probe);

            // A point inside the footprint must be pushed to the nearest face,
            // otherwise the agent receives a destination it cannot stand on.
            if (Mathf.Abs(from.x - closest.x) < 0.001f &&
                Mathf.Abs(from.z - closest.z) < 0.001f)
            {
                float dx = bounds.extents.x - Mathf.Abs(from.x - bounds.center.x);
                float dz = bounds.extents.z - Mathf.Abs(from.z - bounds.center.z);

                if (dx < dz)
                {
                    closest.x = bounds.center.x +
                        Mathf.Sign(from.x - bounds.center.x) * bounds.extents.x;
                }
                else
                {
                    closest.z = bounds.center.z +
                        Mathf.Sign(from.z - bounds.center.z) * bounds.extents.z;
                }
            }

            closest.y = 0f;
            return closest;
        }

        /// <summary>
        /// Point just outside the target's footprint on the side facing
        /// <paramref name="from"/>, snapped to the NavMesh. Agents path to the
        /// near face instead of the unwalkable center of a large building.
        /// </summary>
        public static Vector3 GetApproachPoint(Transform target, Vector3 from, float margin)
        {
            if (target == null)
            {
                return from;
            }

            if (!TryGetTargetBounds(target, out Bounds bounds))
            {
                return target.position;
            }

            return GetApproachPoint(bounds, from, margin);
        }

        /// <summary>
        /// Same as <see cref="GetApproachPoint(Transform, Vector3, float)"/> but
        /// reuses bounds already cached by the caller.
        /// </summary>
        public static Vector3 GetApproachPoint(Bounds bounds, Vector3 from, float margin)
        {
            Vector3 center = new Vector3(bounds.center.x, 0f, bounds.center.z);
            Vector3 closest = ClosestPointOnFootprint(bounds, from);
            Vector3 outward = closest - center;
            outward.y = 0f;

            if (outward.sqrMagnitude < 0.0001f)
            {
                outward = from - center;
                outward.y = 0f;
            }

            if (outward.sqrMagnitude < 0.0001f)
            {
                outward = Vector3.forward;
            }

            outward.Normalize();
            float baseMargin = Mathf.Max(0.2f, margin);

            // The carved NavMesh hole can be a little larger than the collider,
            // so try progressively farther points until one is walkable.
            for (int i = 0; i < 4; i++)
            {
                Vector3 candidate = closest + outward * (baseMargin + i * 0.5f);

                if (!NavMesh.SamplePosition(
                        candidate,
                        out NavMeshHit hit,
                        1.5f,
                        NavMesh.AllAreas))
                {
                    continue;
                }

                Vector3 hitXZ = new Vector3(hit.position.x, 0f, hit.position.z);
                Vector3 hitClosest = ClosestPointOnFootprint(bounds, hitXZ);

                if ((hitXZ - hitClosest).sqrMagnitude >= 0.0025f)
                {
                    return hit.position;
                }
            }

            return closest + outward * baseMargin;
        }

        /// <summary>Turns the unit toward a world position without moving it.</summary>
        public void FaceTowards(Vector3 worldPosition)
        {
            Vector3 direction = worldPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                target,
                Mathf.Max(1f, _turnSpeed) * Time.deltaTime);
        }

        /// <summary>Shows or hides the selection ring under the unit.</summary>
        public void SetSelected(bool selected)
        {
            IsSelected = selected && IsAlive;

            if (selectionIndicator != null && selectionIndicator.activeSelf != IsSelected)
            {
                selectionIndicator.SetActive(IsSelected);
            }
        }

        private void HandleDied(UnitRuntime unit)
        {
            SetSelected(false);
            Stop();
        }
    }
}