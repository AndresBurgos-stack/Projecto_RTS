using System.Collections.Generic;
using ProjectoRTS.Buildings;
using ProjectoRTS.Combat;
using ProjectoRTS.Units;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectoRTS.Waves
{
    /// <summary>
    /// Simple enemy brain for Roman units. Target priority is: placed
    /// buildings first, then Greek soldiers, then the Pantheon. If the current
    /// target cannot be reached (no navmesh path) the unit gives up on it and
    /// tries the next priority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAiController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UnitRuntime runtime;
        [SerializeField] private UnitMovement movement;
        [SerializeField] private UnitCombat combat;

        [Header("Patrol")]
        [SerializeField, Min(0.5f)] private float patrolRadius = 5f;
        [SerializeField, Min(0.5f)] private float patrolWaitMin = 1.5f;
        [SerializeField, Min(0.5f)] private float patrolWaitMax = 4f;
        [SerializeField, Min(0.1f)] private float repathInterval = 0.35f;
        [SerializeField] private bool patrolEnabled = true;

        [Header("Detection")]
        [SerializeField, Min(0.5f)] private float detectionRadius = 10f;
        [SerializeField, Min(0.5f)] private float leashRadius = 14f;
        [SerializeField, Min(0.5f)] private float objectiveDetectionRadius = 30f;
        [SerializeField] private bool attackObjective = true;

        [Header("Priority")]
        [SerializeField, Min(0.5f)] private float buildingDetectionRadius = 30f;
        [SerializeField, Min(0.05f)] private float buildingScanInterval = 0.5f;
        [SerializeField, Min(0.5f)] private float blockedDuration = 4f;
        [SerializeField, Min(0f)] private float targetSwitchCooldown = 2f;

        private Vector3 _home;
        private float _homePatrolRadius;
        private float _nextPatrolTime;
        private float _nextRepathTime;
        private float _nextBuildingScanTime;
        private bool _returningHome;

        private BuildingRuntime _blockedBuilding;
        private float _blockedUntil;
        private float _objectiveBlockedUntil;
        private UnitRuntime _blockedUnit;
        private float _unitBlockedUntil;
        private float _lockedUntil;
        private PantheonObjective _objective;

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

            if (combat == null)
            {
                combat = GetComponent<UnitCombat>();
            }

            if (combat != null)
            {
                combat.SetAutoAcquire(false);
            }

            _home = transform.position;
            _homePatrolRadius = patrolRadius;
        }

        private void OnEnable()
        {
            if (runtime != null)
            {
                runtime.Died += HandleDied;
                runtime.HitByEnemy += HandleHitByEnemy;
            }
        }

        private void OnDisable()
        {
            if (runtime != null)
            {
                runtime.Died -= HandleDied;
                runtime.HitByEnemy -= HandleHitByEnemy;
            }
        }

        /// <summary>
        /// Retaliates when another unit damages this one: the attacker becomes
        /// the combat target so the unit marches toward it instead of standing
        /// still while being shot from beyond the detection radius.
        /// </summary>
        private void HandleHitByEnemy(UnitRuntime victim, UnitRuntime attacker)
        {
            if (attacker == null || !attacker.IsAlive || combat == null || !enabled)
            {
                return;
            }

            combat.CommandAttack(attacker);
            _lockedUntil = Time.time + targetSwitchCooldown;
            _returningHome = false;
            _nextRepathTime = 0f;
        }

        /// <summary>Sets the point the unit patrols around and its radius.</summary>
        public void SetHome(Vector3 home, float radius)
        {
            _home = home;
            _homePatrolRadius = Mathf.Max(0.5f, radius);
            patrolRadius = _homePatrolRadius;
            _nextPatrolTime = Time.time;
            _returningHome = false;
        }

        private void Update()
        {
            if (runtime == null || !runtime.IsAlive || movement == null)
            {
                return;
            }

            if (combat != null && combat.HasTarget)
            {
                if (combat.TargetUnreachable)
                {
                    BlockCurrentTarget();
                    combat.ClearTarget();
                    _returningHome = false;
                    _nextRepathTime = 0f;
                }
                else
                {
                    if (TrySwitchToSoldier())
                    {
                        return;
                    }

                    if (TrySwitchToBuilding())
                    {
                        return;
                    }

                    HandleCombat();
                    return;
                }
            }

            if (!TryDetectEnemy())
            {
                if (!TryDetectBuilding())
                {
                    TryDetectObjective();
                }
            }

            HandlePatrol();
        }

        private void HandleCombat()
        {
            if (combat == null || combat.Target == null)
            {
                return;
            }

            Vector3 targetPosition = combat.Target.transform.position;
            bool targetOutOfLeash =
                (targetPosition - _home).sqrMagnitude > leashRadius * leashRadius;
            bool selfOutOfLeash =
                (transform.position - _home).sqrMagnitude > leashRadius * leashRadius;

            if (targetOutOfLeash || selfOutOfLeash)
            {
                combat.ClearTarget();
                _returningHome = true;
                _nextRepathTime = 0f;
            }
        }

        private bool TryDetectBuilding()
        {
            if (combat == null)
            {
                return false;
            }

            if (Time.time < _nextBuildingScanTime)
            {
                return false;
            }

            _nextBuildingScanTime = Time.time + buildingScanInterval;

            IReadOnlyList<BuildingRuntime> buildings = BuildingRuntime.All;
            BuildingRuntime best = null;
            float bestSqr = buildingDetectionRadius * buildingDetectionRadius;

            for (int i = buildings.Count - 1; i >= 0; i--)
            {
                BuildingRuntime candidate = buildings[i];

                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                if (candidate == _blockedBuilding && Time.time < _blockedUntil)
                {
                    continue;
                }

                ConstructionSite site = candidate.GetComponent<ConstructionSite>();

                if (site != null && !site.IsComplete)
                {
                    continue;
                }

                float sqr = (candidate.transform.position - transform.position).sqrMagnitude;

                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                }
            }

            if (best == null)
            {
                return false;
            }

            combat.CommandAttackBuilding(best);
            _lockedUntil = Time.time + targetSwitchCooldown;
            _returningHome = false;
            return true;
        }

        private bool TryDetectEnemy()
        {
            if (combat == null || Time.time < _nextRepathTime)
            {
                return false;
            }

            _nextRepathTime = Time.time + repathInterval;

            UnitRuntime enemy = UnitRegistry.FindNearestEnemy(
                transform.position,
                runtime.Faction,
                detectionRadius,
                combat != null && combat.IsMeleeAttacker);

            if (enemy == null)
            {
                return false;
            }

            if (enemy == _blockedUnit && Time.time < _unitBlockedUntil)
            {
                return false;
            }

            combat.CommandAttack(enemy);
            _lockedUntil = Time.time + targetSwitchCooldown;
            _returningHome = false;
            return true;
        }

        private bool TrySwitchToSoldier()
        {
            if (combat == null || combat.Target != null || Time.time < _lockedUntil)
            {
                return false;
            }

            return TryDetectEnemy();
        }

        private bool TrySwitchToBuilding()
        {
            if (combat == null || combat.BuildingTarget != null || combat.Target != null || Time.time < _lockedUntil)
            {
                return false;
            }

            return TryDetectBuilding();
        }

        private void TryDetectObjective()
        {
            if (!attackObjective || combat == null || combat.HasTarget)
            {
                return;
            }

            if (Time.time < _objectiveBlockedUntil)
            {
                return;
            }

            PantheonObjective objective = _objective;
            if (objective == null)
            {
                _objective = FindFirstObjectByType<PantheonObjective>();
                objective = _objective;
            }
            if (objective == null || !objective.IsAlive)
            {
                return;
            }

            Vector3 delta = objective.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > objectiveDetectionRadius * objectiveDetectionRadius)
            {
                return;
            }

            combat.CommandAttackObjective(objective);
            _lockedUntil = Time.time + targetSwitchCooldown;
            _returningHome = false;
        }

        private void BlockCurrentTarget()
        {
            if (combat == null)
            {
                return;
            }

            if (combat.BuildingTarget != null)
            {
                _blockedBuilding = combat.BuildingTarget;
                _blockedUntil = Time.time + blockedDuration;
            }

            if (combat.ObjectiveTarget != null)
            {
                _objectiveBlockedUntil = Time.time + blockedDuration;
            }

            if (combat.Target != null)
            {
                _blockedUnit = combat.Target;
                _unitBlockedUntil = Time.time + blockedDuration;
            }
        }

        /// <summary>Enables or disables the patrol behaviour at runtime.</summary>
        public void SetPatrolEnabled(bool value)
        {
            patrolEnabled = value;
        }

        private void HandlePatrol()
        {
            if (!patrolEnabled)
            {
                return;
            }

            if (_returningHome)
            {
                if (movement.IsMoving)
                {
                    return;
                }

                _returningHome = false;
                _nextPatrolTime = Time.time + Random.Range(patrolWaitMin, patrolWaitMax);
                return;
            }

            if (movement.IsMoving || Time.time < _nextPatrolTime)
            {
                return;
            }

            _nextPatrolTime = Time.time + Random.Range(patrolWaitMin, patrolWaitMax);
            Vector3 point = RandomPointAround(_home, _homePatrolRadius);
            movement.MoveTo(point);
        }

        private static Vector3 RandomPointAround(Vector3 center, float radius)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(
                    candidate,
                    out NavMeshHit hit,
                    radius,
                    NavMesh.AllAreas))
            {
                return hit.position;
            }

            return center;
        }

        private void HandleDied(UnitRuntime unit)
        {
            if (combat != null)
            {
                combat.ClearTarget();
            }

            if (movement != null)
            {
                movement.Stop();
            }

            enabled = false;
        }
    }
}
