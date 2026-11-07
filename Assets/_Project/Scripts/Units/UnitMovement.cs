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

        private Animator _animator;
        private float _turnSpeed = 720f;

        public NavMeshAgent Agent => agent;
        public UnitRuntime Runtime { get; private set; }
        public bool IsSelected { get; private set; }
        public bool IsAlive => Runtime == null || Runtime.IsAlive;

        private void Update()
        {
            if (_animator == null || agent == null)
            {
                return;
            }

            float speed = agent.enabled && agent.isOnNavMesh
                ? agent.velocity.magnitude
                : 0f;
            float normalized = Mathf.Clamp01(speed / Mathf.Max(0.1f, agent.speed));
            _animator.SetFloat("Speed", normalized);
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