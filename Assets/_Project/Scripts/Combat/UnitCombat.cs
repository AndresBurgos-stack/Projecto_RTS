using ProjectoRTS.Buildings;
using ProjectoRTS.Units;
using ProjectoRTS.Waves;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectoRTS.Combat
{
    /// <summary>
    /// Basic unit combat: acquires nearby enemies, attacks with the stats from
    /// the <see cref="UnitDefinition"/> and chases manual attack orders.
    /// Enemy units can also attack the central <see cref="PantheonObjective"/>
    /// and placed buildings.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitRuntime), typeof(UnitMovement))]
    public sealed class UnitCombat : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UnitRuntime runtime;
        [SerializeField] private UnitMovement movement;

        [Header("Targeting")]
        [SerializeField, Min(0.05f)] private float retargetInterval = 0.25f;
        [SerializeField, Min(0.05f)] private float chaseRefreshInterval = 0.2f;
        [SerializeField, Min(0f)] private float rangePadding = 0.3f;
        [SerializeField, Min(0f)] private float buildingReach = 2.5f;
        [SerializeField, Min(0f)] private float aggroRadius = 9f;
        [SerializeField] private bool autoAcquire = true;

        private UnitRuntime _target;
        private PantheonObjective _objectiveTarget;
        private BuildingRuntime _buildingTarget;
        private bool _manualTarget;
        private bool _autoTarget;
        private bool _chasing;
        private float _chaseIssuedTime;
        private float _nextRetargetTime;
        private float _nextAttackTime;
        private float _nextChaseTime;
        private Vector3 _lastChasePosition;
        private float _attackRange;
        private float _attackDamage;
        private float _attackInterval;

        public UnitRuntime Target => _target;
        public PantheonObjective ObjectiveTarget => _objectiveTarget;
        public BuildingRuntime BuildingTarget => _buildingTarget;

        public bool HasTarget =>
            (_target != null && _target.IsAlive) ||
            (_objectiveTarget != null && _objectiveTarget.IsAlive) ||
            (_buildingTarget != null && _buildingTarget.IsAlive);

        /// <summary>
        /// True when the unit is chasing a building or the objective and the
        /// navmesh has no usable path to it.
        /// </summary>
        public bool TargetUnreachable
        {
            get
            {
                if (!_chasing)
                {
                    return false;
                }

                if (movement == null || movement.Agent == null)
                {
                    return false;
                }

                if (Time.time < _chaseIssuedTime + 0.5f)
                {
                    return false;
                }

                NavMeshAgent agent = movement.Agent;

                if (!agent.isOnNavMesh)
                {
                    return true;
                }

                if (agent.pathPending)
                {
                    return false;
                }

                return agent.pathStatus == NavMeshPathStatus.PathInvalid || !agent.hasPath;
            }
        }

        private void Awake()
        {
            if (runtime == null)
            {
                runtime = GetComponent<UnitRuntime>();
            }

            if (movement == null)
            {
                movement = GetComponent<UnitMovement>();
            }

            ApplyDefinition();
        }

        private void Start()
        {
            if (runtime != null && runtime.Definition != null)
            {
                Initialize(runtime.Definition);
            }
        }

        /// <summary>Applies the combat stats from the unit definition.</summary>
        public void Initialize(UnitDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            _attackRange = definition.AttackRange;
            _attackDamage = definition.AttackDamage;
            _attackInterval = Mathf.Max(0.05f, definition.AttackInterval);
            _nextAttackTime = 0f;
        }

        /// <summary>Enables or disables automatic enemy acquisition.</summary>
        public void SetAutoAcquire(bool value)
        {
            autoAcquire = value;

            if (!value)
            {
                _autoTarget = false;
            }
        }

        /// <summary>Orders this unit to chase and attack a specific enemy.</summary>
        public void CommandAttack(UnitRuntime target)
        {
            if (target == null || !target.IsAlive || runtime == null || !runtime.IsAlive)
            {
                return;
            }

            if (target.Faction == runtime.Faction || target.Faction == Faction.None)
            {
                return;
            }

            _target = target;
            _objectiveTarget = null;
            _buildingTarget = null;
            _manualTarget = true;
            _autoTarget = false;
            _chasing = false;
            _nextAttackTime = 0f;
            _nextRetargetTime = 0f;
            _nextChaseTime = 0f;
            _lastChasePosition = target.transform.position;
        }

        /// <summary>Orders this unit to attack the central objective.</summary>
        public void CommandAttackObjective(PantheonObjective target)
        {
            if (target == null || !target.IsAlive || runtime == null || !runtime.IsAlive)
            {
                return;
            }

            _target = null;
            _objectiveTarget = target;
            _buildingTarget = null;
            _manualTarget = true;
            _autoTarget = false;
            _chasing = false;
            _nextAttackTime = 0f;
            _nextRetargetTime = 0f;
            _nextChaseTime = 0f;
            _lastChasePosition = target.transform.position;
        }

        /// <summary>Orders this unit to attack a placed building.</summary>
        public void CommandAttackBuilding(BuildingRuntime target)
        {
            if (target == null || !target.IsAlive || runtime == null || !runtime.IsAlive)
            {
                return;
            }

            _target = null;
            _objectiveTarget = null;
            _buildingTarget = target;
            _manualTarget = true;
            _chasing = false;
            _nextAttackTime = 0f;
            _nextRetargetTime = 0f;
            _nextChaseTime = 0f;
            _lastChasePosition = target.transform.position;
        }

        /// <summary>Forgets the current target and returns to passive behavior.</summary>
        public void ClearTarget()
        {
            _target = null;
            _objectiveTarget = null;
            _buildingTarget = null;
            _manualTarget = false;
            _autoTarget = false;
            _chasing = false;
            _nextRetargetTime = 0f;
            _nextChaseTime = 0f;
        }

        private void Update()
        {
            if (runtime == null || !runtime.IsAlive)
            {
                return;
            }

            if (_target != null && (!_target.IsAlive || !_target.gameObject.activeInHierarchy))
            {
                bool wasManual = _manualTarget;
                ClearTarget();

                if (wasManual && movement != null)
                {
                    movement.Stop();
                }
            }

            if (_objectiveTarget != null && !_objectiveTarget.IsAlive)
            {
                ClearTarget();
            }

            if (_buildingTarget != null && !_buildingTarget.IsAlive)
            {
                ClearTarget();
            }

            if (autoAcquire && _target == null && !_manualTarget && Time.time >= _nextRetargetTime)
            {
                _nextRetargetTime = Time.time + retargetInterval;

                if (movement == null || !movement.IsMoving)
                {
                    float radius = Mathf.Max(aggroRadius, _attackRange + rangePadding);
                    _target = UnitRegistry.FindNearestEnemy(
                        transform.position,
                        runtime.Faction,
                        radius);

                    if (_target != null)
                    {
                        _autoTarget = true;
                    }
                }
            }

            if (_buildingTarget != null)
            {
                UpdateBuildingAttack();
                return;
            }

            if (_objectiveTarget != null)
            {
                UpdateObjectiveAttack();
                return;
            }

            if (_target == null)
            {
                return;
            }

            Vector3 delta = _target.transform.position - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            float attackDistance = _attackRange + rangePadding;

            if (distance > attackDistance)
            {
                float chaseLimit = Mathf.Max(aggroRadius * 1.5f, attackDistance + 1f);
                bool shouldChase = _manualTarget || (_autoTarget && distance <= chaseLimit);

                if (movement != null && shouldChase)
                {
                    ChaseTarget();
                }
                else
                {
                    ClearTarget();
                }

                return;
            }

            if (movement != null)
            {
                movement.Stop();
                movement.FaceTowards(_target.transform.position);
            }

            if (Time.time >= _nextAttackTime)
            {
                _nextAttackTime = Time.time + _attackInterval;
                _target.ApplyDamage(_attackDamage);
            }
        }

        private void UpdateBuildingAttack()
        {
            if (_buildingTarget == null)
            {
                ClearTarget();
                return;
            }

            Vector3 center = _buildingTarget.transform.position;
            Vector3 delta = center - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            float attackDistance = _attackRange + rangePadding + buildingReach;

            if (distance > attackDistance)
            {
                if (movement != null)
                {
                    Vector3 approach = delta.sqrMagnitude > 0.0001f
                        ? center - delta.normalized * buildingReach
                        : center;

                    _lastChasePosition = center;
                    _chasing = true;
                    _chaseIssuedTime = Time.time;
                    movement.MoveTo(approach);
                }

                return;
            }

            _chasing = false;

            if (movement != null)
            {
                movement.Stop();
                movement.FaceTowards(center);
            }

            if (Time.time >= _nextAttackTime)
            {
                _nextAttackTime = Time.time + _attackInterval;
                _buildingTarget.ApplyDamage(_attackDamage);
            }
        }

        private void UpdateObjectiveAttack()
        {
            Vector3 delta = _objectiveTarget.transform.position - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            float attackDistance = _attackRange + rangePadding + 6f;

            if (distance > attackDistance)
            {
                if (movement != null)
                {
                    _lastChasePosition = _objectiveTarget.transform.position;
                    _chasing = true;
                    _chaseIssuedTime = Time.time;
                    movement.MoveTo(_objectiveTarget.transform.position);
                }

                return;
            }

            _chasing = false;

            if (movement != null)
            {
                movement.Stop();
                movement.FaceTowards(_objectiveTarget.transform.position);
            }

            if (Time.time >= _nextAttackTime)
            {
                _nextAttackTime = Time.time + _attackInterval;
                _objectiveTarget.ApplyDamage(_attackDamage);
            }
        }

        private void ChaseTarget()
        {
            Vector3 targetPosition = _target.transform.position;
            bool targetMoved = (targetPosition - _lastChasePosition).sqrMagnitude > 0.25f;

            if (!targetMoved && Time.time < _nextChaseTime)
            {
                return;
            }

            _nextChaseTime = Time.time + chaseRefreshInterval;
            _lastChasePosition = targetPosition;
            movement.MoveTo(targetPosition);
        }

        private void ApplyDefinition()
        {
            if (runtime != null && runtime.Definition != null)
            {
                Initialize(runtime.Definition);
            }
        }
    }
}
