using System.Collections.Generic;
using ProjectoRTS.Combat;
using ProjectoRTS.Economy;
using ProjectoRTS.Resources;
using ProjectoRTS.Units;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Lets a worker build construction sites and harvest resource nodes.
    /// Construction follows a one-to-one rule: each site gets at most one
    /// worker, so two sites mean two builders. Workers without a site gather
    /// their preferred resource. Workers run from enemies and return to their
    /// current job once the area is safe again.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class WorkerBuilder : MonoBehaviour
    {
        private const float AutoAssignRange = 1000f;

        private static readonly List<WorkerBuilder> Workers = new List<WorkerBuilder>(16);

        [Header("Build")]
        [SerializeField, Min(0.1f)] private float workRange = 2.2f;
        [SerializeField, Min(0.1f)] private float workMultiplier = 1f;
        [SerializeField, Min(0f)] private float approachDistance = 2f;
        [SerializeField, Min(0.1f)] private float repathInterval = 0.5f;

        [Header("Gather")]
        [SerializeField, Min(1)] private int carryCapacity = 10;
        [SerializeField, Min(0.1f)] private float gatherTime = 2f;
        [SerializeField, Min(0.5f)] private float gatherRange = 2.4f;
        [SerializeField, Min(0.5f)] private float depositRange = 3f;
        [SerializeField, Min(0.1f)] private float resourceRepathInterval = 0.6f;

        [Header("Flee")]
        [SerializeField, Min(0.5f)] private float fleeRadius = 9f;
        [SerializeField, Min(0.5f)] private float safeRadius = 12f;
        [SerializeField, Min(1f)] private float fleeDistance = 14f;
        [SerializeField, Min(1f)] private float fleeSpeedMultiplier = 1.6f;
        [SerializeField, Min(0.1f)] private float fleeScanInterval = 0.3f;
        [SerializeField, Min(0.5f)] private float fleeRepathInterval = 1f;

        private NavMeshAgent _agent;
        private UnitRuntime _runtime;
        private ConstructionSite _site;
        private bool _stoppedAtSite;
        private float _repathTimer;
        private float _baseSpeed;
        private bool _fleeing;
        private UnitRuntime _fleeEnemy;
        private Vector3 _fleePoint;
        private float _nextFleeScan;
        private float _nextFleeRepath;

        private ResourceNode _resourceNode;
        private ResourceDropOff _dropOff;
        private ResourceType _cargoType;
        private int _cargoAmount;
        private ResourceType _nextGatherType = ResourceType.Wood;
        private ResourceType _preferredResource = ResourceType.None;
        private bool _atNode;
        private float _gatherTimer;
        private float _resourceRepathTimer;

        /// <summary>Site this worker is currently assigned to, or null.</summary>
        public ConstructionSite Site => _site;

        /// <summary>True while the worker carries harvested resources.</summary>
        public bool HasCargo => _cargoAmount > 0;

        /// <summary>
        /// Resource the worker must collect, or None while the worker has no
        /// gathering job (it stays idle). Changing it cancels the current
        /// gathering job immediately (unless the worker is carrying cargo) so
        /// the worker switches resources right away.
        /// </summary>
        public ResourceType PreferredResource
        {
            get => _preferredResource;
            set
            {
                if (_preferredResource == value)
                {
                    return;
                }

                _preferredResource = value;
                _nextGatherType = value;

                if (_cargoAmount <= 0)
                {
                    _resourceNode = null;
                    _atNode = false;
                    _gatherTimer = 0f;
                    _resourceRepathTimer = 0f;
                }
            }
        }

        /// <summary>Number of living workers in the scene.</summary>
        public static int Count
        {
            get
            {
                int count = 0;

                for (int i = 0; i < Workers.Count; i++)
                {
                    WorkerBuilder worker = Workers[i];

                    if (worker != null && (worker._runtime == null || worker._runtime.IsAlive))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Number of living workers that prioritize a resource type.</summary>
        public static int CountPreferred(ResourceType type)
        {
            int count = 0;

            for (int i = 0; i < Workers.Count; i++)
            {
                WorkerBuilder worker = Workers[i];

                if (worker != null &&
                    (worker._runtime == null || worker._runtime.IsAlive) &&
                    worker._preferredResource == type)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>True when the worker is close enough to be building.</summary>
        public bool IsBuilding => _site != null && !_site.IsComplete && !_fleeing && IsInRange();

        /// <summary>True while the worker is running away from an enemy.</summary>
        public bool IsFleeing => _fleeing;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _runtime = GetComponent<UnitRuntime>();
            _baseSpeed = _agent != null ? _agent.speed : 0f;

            UnitCombat combat = GetComponent<UnitCombat>();
            if (combat != null)
            {
                combat.SetAutoAcquire(false);
            }
        }

        private void OnEnable()
        {
            if (!Workers.Contains(this))
            {
                Workers.Add(this);
            }
        }

        private void OnDisable()
        {
            Workers.Remove(this);
        }

        /// <summary>
        /// Assigns a construction site. Passing null clears the assignment.
        /// </summary>
        public void AssignSite(ConstructionSite site)
        {
            _site = site;
            _stoppedAtSite = false;
            _repathTimer = 0f;
            CancelGathering();

            if (_site == null)
            {
                ResumeCurrentTask();
                return;
            }

            if (_cargoAmount <= 0)
            {
                WalkToSite();
            }
        }

        private void Update()
        {
            if (_agent == null)
            {
                _agent = GetComponent<NavMeshAgent>();
                if (_agent != null)
                {
                    _baseSpeed = _agent.speed;
                }
            }

            if (_runtime == null)
            {
                _runtime = GetComponent<UnitRuntime>();
            }

            if (UpdateFlee())
            {
                return;
            }

            if (_site != null && _site.IsComplete)
            {
                _site = null;
                _stoppedAtSite = false;
            }

            if (_cargoAmount > 0)
            {
                UpdateDeposit();
                return;
            }

            if (_site == null)
            {
                _site = FindBestSite(transform.position, AutoAssignRange, null);

                if (_site != null)
                {
                    CancelGathering();
                    _stoppedAtSite = false;
                    _repathTimer = 0f;
                    WalkToSite();
                }
            }

            if (_site != null)
            {
                UpdateConstruction();
                return;
            }

            UpdateGathering();
        }

        private void UpdateConstruction()
        {
            if (_site == null)
            {
                return;
            }

            if (!IsInRange())
            {
                _repathTimer -= Time.deltaTime;

                if (_repathTimer <= 0f)
                {
                    _repathTimer = repathInterval;

                    if (_agent != null && _agent.isOnNavMesh && !_agent.pathPending && !_agent.hasPath)
                    {
                        WalkToSite();
                    }
                }

                return;
            }

            if (!_stoppedAtSite)
            {
                StopAtSite();
            }

            _site.AddProgress(Time.deltaTime * workMultiplier);
        }

        private void UpdateGathering()
        {
            if (_resourceNode == null || _resourceNode.IsDepleted)
            {
                _resourceNode = FindGatherNode();
                _atNode = false;
                _gatherTimer = 0f;
                _resourceRepathTimer = 0f;

                if (_resourceNode == null)
                {
                    Resume();
                    return;
                }

                WalkToResource();
            }

            if (!IsInGatherRange())
            {
                _resourceRepathTimer -= Time.deltaTime;

                if (_resourceRepathTimer <= 0f)
                {
                    _resourceRepathTimer = resourceRepathInterval;

                    if (_agent != null && _agent.isOnNavMesh && !_agent.pathPending && !_agent.hasPath)
                    {
                        WalkToResource();
                    }
                }

                return;
            }

            if (!_atNode)
            {
                StopAtResource();
            }

            _gatherTimer += Time.deltaTime;

            if (_gatherTimer >= gatherTime)
            {
                HarvestNode();

                if (_cargoAmount > 0)
                {
                    _dropOff = null;
                    _resourceRepathTimer = 0f;
                    UpdateDeposit();
                }
            }
        }

        private void UpdateDeposit()
        {
            if (_cargoAmount <= 0)
            {
                return;
            }

            if (_dropOff == null || !_dropOff.isActiveAndEnabled)
            {
                _dropOff = ResourceDropOff.FindNearest(transform.position, AutoAssignRange);
            }

            if (_dropOff == null)
            {
                PlayerEconomy.Add(_cargoType, _cargoAmount);
                CompleteDeposit();
                return;
            }

            if (!IsInDepositRange())
            {
                _resourceRepathTimer -= Time.deltaTime;

                if (_resourceRepathTimer <= 0f)
                {
                    _resourceRepathTimer = resourceRepathInterval;

                    if (_agent != null && _agent.isOnNavMesh && !_agent.pathPending && !_agent.hasPath)
                    {
                        WalkToDropOff();
                    }
                }

                return;
            }

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }

            _dropOff.Deposit(_cargoType, _cargoAmount);
            CompleteDeposit();
        }

        private void HarvestNode()
        {
            if (_resourceNode == null)
            {
                return;
            }

            ResourceType harvestedType = _resourceNode.Type;
            int harvested = _resourceNode.Harvest(carryCapacity);

            _gatherTimer = 0f;
            _atNode = false;
            _resourceRepathTimer = 0f;

            if (harvested <= 0)
            {
                _resourceNode = null;
                return;
            }

            _cargoType = harvestedType;
            _cargoAmount = harvested;
            _resourceNode = null;
            _dropOff = null;
            Resume();
        }

        private void CompleteDeposit()
        {
            _cargoAmount = 0;
            _dropOff = null;
            _resourceRepathTimer = 0f;
            _nextGatherType = _preferredResource;
            Resume();
        }

        private ResourceNode FindGatherNode()
        {
            if (_preferredResource == ResourceType.None)
            {
                return null;
            }

            return ResourceNode.FindNearest(
                transform.position,
                _preferredResource,
                AutoAssignRange);
        }

        private void WalkToResource()
        {
            if (_resourceNode == null || _agent == null || !_agent.isOnNavMesh)
            {
                return;
            }

            _agent.isStopped = false;
            _agent.SetDestination(GetApproachPoint(_resourceNode.transform.position, gatherRange * 0.75f));
        }

        private void WalkToDropOff()
        {
            if (_dropOff == null || _agent == null || !_agent.isOnNavMesh)
            {
                return;
            }

            _agent.isStopped = false;
            _agent.SetDestination(GetApproachPoint(_dropOff.transform.position, depositRange * 0.75f));
        }

        private bool IsInGatherRange()
        {
            if (_resourceNode == null)
            {
                return false;
            }

            Vector3 delta = _resourceNode.transform.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= gatherRange * gatherRange;
        }

        private bool IsInDepositRange()
        {
            if (_dropOff == null)
            {
                return false;
            }

            Vector3 delta = _dropOff.transform.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= depositRange * depositRange;
        }

        private void StopAtResource()
        {
            _atNode = true;

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }

            if (_resourceNode != null)
            {
                Vector3 look = _resourceNode.transform.position - transform.position;
                look.y = 0f;

                if (look.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(look.normalized);
                }
            }
        }

        private void CancelGathering()
        {
            _resourceNode = null;
            _atNode = false;
            _gatherTimer = 0f;
            _resourceRepathTimer = 0f;
        }

        /// <summary>
        /// Handles the flee state. Returns true while the worker must skip all
        /// construction and gathering logic.
        /// </summary>
        private bool UpdateFlee()
        {
            if (_runtime == null || !_runtime.IsAlive)
            {
                return false;
            }

            if (Time.time >= _nextFleeScan)
            {
                _nextFleeScan = Time.time + fleeScanInterval;
                float radius = _fleeing ? safeRadius : fleeRadius;
                UnitRuntime enemy = UnitRegistry.FindNearestEnemy(
                    transform.position,
                    _runtime.Faction,
                    radius);

                if (enemy == null)
                {
                    if (_fleeing)
                    {
                        StopFleeing();
                    }

                    return false;
                }

                if (!_fleeing)
                {
                    StartFleeing(enemy);
                }
                else
                {
                    _fleeEnemy = enemy;
                }
            }

            if (!_fleeing)
            {
                return false;
            }

            if (_fleeEnemy == null || !_fleeEnemy.IsAlive)
            {
                StopFleeing();
                return false;
            }

            if (HasReachedFleePoint() || Time.time >= _nextFleeRepath)
            {
                StartFleeing(_fleeEnemy);
            }

            return true;
        }

        private void StartFleeing(UnitRuntime enemy)
        {
            if (enemy == null)
            {
                return;
            }

            if (!_fleeing)
            {
                _fleeing = true;
                _stoppedAtSite = false;
                _atNode = false;
            }

            _fleeEnemy = enemy;
            _nextFleeRepath = Time.time + fleeRepathInterval;
            _fleePoint = FindFleePoint(enemy.transform.position);

            if (_agent != null)
            {
                _agent.speed = GetBaseSpeed() * Mathf.Max(1f, fleeSpeedMultiplier);
                _agent.isStopped = false;

                if (_agent.isOnNavMesh)
                {
                    _agent.SetDestination(_fleePoint);
                }
            }
        }

        private void StopFleeing()
        {
            _fleeing = false;
            _fleeEnemy = null;

            if (_agent != null)
            {
                _agent.speed = GetBaseSpeed();
            }

            ResumeCurrentTask();
        }

        private void ResumeCurrentTask()
        {
            if (_cargoAmount > 0)
            {
                _dropOff = null;
                _resourceRepathTimer = 0f;
                return;
            }

            if (_site != null && !_site.IsComplete)
            {
                _stoppedAtSite = false;
                _repathTimer = 0f;
                WalkToSite();
                return;
            }

            if (_resourceNode != null && !_resourceNode.IsDepleted)
            {
                _atNode = false;
                _resourceRepathTimer = 0f;
                WalkToResource();
                return;
            }

            _resourceNode = null;
            _atNode = false;
            _gatherTimer = 0f;
            Resume();
        }

        private float GetBaseSpeed()
        {
            if (_runtime != null && _runtime.Definition != null)
            {
                return _runtime.Definition.MoveSpeed;
            }

            return _baseSpeed;
        }

        private Vector3 FindFleePoint(Vector3 threat)
        {
            Vector3 away = transform.position - threat;
            away.y = 0f;

            if (away.sqrMagnitude < 0.0001f)
            {
                away = transform.forward;
            }

            away.Normalize();

            float[] angles = { 0f, 35f, -35f, 70f, -70f, 110f, -110f, 150f, -150f };

            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, angles[i], 0f) * away;
                Vector3 candidate = transform.position + direction * fleeDistance;

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }

            return transform.position;
        }

        private bool HasReachedFleePoint()
        {
            Vector3 delta = _fleePoint - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= 4f;
        }

        private void WalkToSite()
        {
            if (_site == null || _agent == null || !_agent.isOnNavMesh)
            {
                return;
            }

            _agent.isStopped = false;
            _agent.SetDestination(GetApproachPoint(_site.transform.position, approachDistance));
        }

        private void StopAtSite()
        {
            _stoppedAtSite = true;

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }

            if (_site != null)
            {
                Vector3 look = _site.transform.position - transform.position;
                look.y = 0f;

                if (look.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(look.normalized);
                }
            }
        }

        private void Resume()
        {
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
            }
        }

        private Vector3 GetApproachPoint(Vector3 center, float distance)
        {
            Vector3 fromWorker = transform.position - center;
            fromWorker.y = 0f;

            if (fromWorker.sqrMagnitude < 0.0001f)
            {
                fromWorker = Vector3.forward;
            }

            Vector3 point = center + fromWorker.normalized * distance;

            NavMeshHit hit;

            if (NavMesh.SamplePosition(point, out hit, 4f, NavMesh.AllAreas))
            {
                point = hit.position;
            }

            return point;
        }

        private bool IsInRange()
        {
            if (_site == null)
            {
                return false;
            }

            Vector3 delta = _site.transform.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= workRange * workRange;
        }

        /// <summary>
        /// Picks the nearest active site that has no worker assigned yet. With
        /// this one-to-one rule a site never piles up workers, so one site gets
        /// one builder and two sites get two.
        /// </summary>
        private static ConstructionSite FindBestSite(
            Vector3 position,
            float maxDistance,
            ConstructionSite avoid)
        {
            ConstructionSite best = null;
            float bestSqr = float.MaxValue;
            float maxSqr = maxDistance * maxDistance;
            IReadOnlyList<ConstructionSite> sites = ConstructionSite.Active;

            for (int i = 0; i < sites.Count; i++)
            {
                ConstructionSite site = sites[i];

                if (site == null || site.IsComplete || site == avoid)
                {
                    continue;
                }

                if (CountAssigned(site) > 0)
                {
                    continue;
                }

                float sqr = (site.transform.position - position).sqrMagnitude;

                if (sqr > maxSqr)
                {
                    continue;
                }

                if (sqr < bestSqr)
                {
                    best = site;
                    bestSqr = sqr;
                }
            }

            return best;
        }

        private static int CountAssigned(ConstructionSite site)
        {
            int count = 0;

            for (int i = 0; i < Workers.Count; i++)
            {
                WorkerBuilder worker = Workers[i];

                if (worker != null && worker._site == site)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
