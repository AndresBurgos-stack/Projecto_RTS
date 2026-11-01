using ProjectoRTS.Units;
using UnityEngine;

namespace ProjectoRTS.Combat
{
    /// <summary>
    /// Basic unit combat: acquires nearby enemies, attacks with the stats from
    /// the <see cref="UnitDefinition"/> and chases manual attack orders.
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

        private UnitRuntime _target;
        private bool _manualTarget;
        private float _nextRetargetTime;
        private float _nextAttackTime;
        private float _nextChaseTime;
        private Vector3 _lastChasePosition;
        private float _attackRange;
        private float _attackDamage;
        private float _attackInterval;

        public UnitRuntime Target => _target;
        public bool HasTarget => _target != null && _target.IsAlive;

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
            _manualTarget = true;
            _nextAttackTime = 0f;
            _nextRetargetTime = 0f;
            _nextChaseTime = 0f;
            _lastChasePosition = target.transform.position;
        }

        /// <summary>Forgets the current target and returns to passive behavior.</summary>
        public void ClearTarget()
        {
            _target = null;
            _manualTarget = false;
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

            if (_target == null && Time.time >= _nextRetargetTime)
            {
                _nextRetargetTime = Time.time + retargetInterval;

                if (movement == null || !movement.IsMoving)
                {
                    _target = UnitRegistry.FindNearestEnemy(
                        transform.position,
                        runtime.Faction,
                        _attackRange + rangePadding);
                }
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
                if (_manualTarget && movement != null)
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
