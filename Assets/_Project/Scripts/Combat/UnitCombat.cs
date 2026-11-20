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
        [SerializeField, Min(0.5f)] private float forceRepathInterval = 1.5f;
        [SerializeField, Min(0f)] private float rangePadding = 0.3f;
        [SerializeField, Min(0f)] private float buildingReach = 2.5f;
        [SerializeField, Min(0f)] private float aggroRadius = 9f;
        [SerializeField] private bool autoAcquire = true;

        [Header("Pilum Visuals")]
        [SerializeField] private Transform pilumHandVisual;
        [SerializeField] private Transform[] pilumBackVisuals;

        [Header("Sword Visuals")]
        [SerializeField] private Transform gladiusVisual;
        [SerializeField] private Transform gladiusHandBone;
        [SerializeField] private Vector3 gladiusHandLocalPosition = new Vector3(0.02f, 0.081f, -0.003f);
        [SerializeField] private Vector3 gladiusHandLocalEuler = new Vector3(0f, 0f, -90f);

        private UnitRuntime _target;
        private PantheonObjective _objectiveTarget;
        private BuildingRuntime _buildingTarget;
        private UnitRuntime _pendingUnitTarget;
        private BuildingRuntime _pendingBuildingTarget;
        private PantheonObjective _pendingObjectiveTarget;
        private float _pendingHitDeadline;
        private bool _hitAppliedThisAttack;
        private float _lastAttackStartTime = float.MinValue;
        private bool _manualTarget;
        private bool _autoTarget;
        private bool _chasing;
        private float _chaseIssuedTime;
        private float _nextRetargetTime;
        private float _nextAttackTime;
        private float _nextChaseTime;
        private float _nextForceRepathTime;
        private Vector3 _lastChasePosition;
        private Animator _animator;
        private HopliteProceduralAnimator _proceduralAnimator;
        private float _attackRange;
        private float _attackDamage;
        private float _attackInterval;
        private float _rangeBonus;
        private GameObject _projectilePrefab;
        private int _remainingPilums;
        private bool _usesPilumVolley;
        private float _pilumDamage;
        private float _pilumRange;
        private float _pilumInterval;
        private bool _pendingAttackUsesProjectile;
        private bool _pendingAttackConsumesPilum;
        private float _pendingAttackDamage;
        private bool _drawSwordOnFirstMelee;
        private bool _swordDrawn;
        private bool _drawingSword;
        private float _drawSwordReadyTime;
        private float _drawSwordDeadline;

        public UnitRuntime Target => _target;
        public PantheonObjective ObjectiveTarget => _objectiveTarget;
        public BuildingRuntime BuildingTarget => _buildingTarget;

        /// <summary>
        /// True when this unit fights in melee. Infantry and cavalry units with
        /// pilums still count as ranged attackers until the volley is spent.
        /// Melee units cannot damage archers mounted on walls.
        /// </summary>
        public bool IsMeleeAttacker =>
            runtime != null &&
            (runtime.Role == UnitRole.Infantry || runtime.Role == UnitRole.Cavalry) &&
            _remainingPilums <= 0;

        /// <summary>
        /// Attack range used for chasing and acquiring targets. A unit with
        /// pilums keeps its throwing range until the last pilum is spent.
        /// </summary>
        public float CurrentAttackRange =>
            (_remainingPilums > 0 ? Mathf.Max(_attackRange, _pilumRange) : _attackRange) +
            _rangeBonus;

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

            _animator = GetComponentInChildren<Animator>(true);
            _proceduralAnimator = GetComponent<HopliteProceduralAnimator>();
            AttachAttackEventRelay();
            ApplyDefinition();
        }

        /// <summary>
        /// The Animator lives on the visual child, so the animation event needs
        /// a relay on that same GameObject to reach the combat component.
        /// </summary>
        private void AttachAttackEventRelay()
        {
            if (_animator == null)
            {
                return;
            }

            UnitAttackEventRelay relay = _animator.GetComponent<UnitAttackEventRelay>();

            if (relay == null)
            {
                relay = _animator.gameObject.AddComponent<UnitAttackEventRelay>();
            }

            relay.Configure(this);
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
            _projectilePrefab = definition.ProjectilePrefab;
            _pilumDamage = definition.PilumDamage > 0f
                ? definition.PilumDamage
                : definition.AttackDamage;
            _pilumRange = definition.PilumRange > 0f
                ? definition.PilumRange
                : definition.AttackRange;
            _pilumInterval = Mathf.Max(
                0.05f,
                definition.PilumInterval > 0f
                    ? definition.PilumInterval
                    : definition.AttackInterval);
            _remainingPilums = Mathf.Max(0, definition.PilumCount);
            _usesPilumVolley = _remainingPilums > 0;
            _drawSwordOnFirstMelee = definition.DrawSwordOnFirstMelee;
            _swordDrawn = false;
            _drawingSword = false;
            _drawSwordReadyTime = 0f;
            _drawSwordDeadline = 0f;
            _nextAttackTime = 0f;
            RefreshPilumVisuals();
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

        /// <summary>Adds a flat range bonus, used while standing on a wall.</summary>
        public void SetRangeBonus(float bonus)
        {
            _rangeBonus = Mathf.Max(0f, bonus);
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

            if (IsMeleeAttacker && target.IsMounted)
            {
                BuildingRuntime wall = target.MountedWallBuilding;

                if (wall != null && wall.IsAlive)
                {
                    CommandAttackBuilding(wall);
                }

                return;
            }

            bool sameTarget = _target == target;

            _target = target;
            _objectiveTarget = null;
            _buildingTarget = null;
            _manualTarget = true;
            _autoTarget = false;
            _chasing = false;
            _nextRetargetTime = 0f;

            if (!sameTarget)
            {
                _nextAttackTime = 0f;
            }

            _nextChaseTime = 0f;
            _lastChasePosition = target.transform.position;
        }

        private void OnEnable()
        {
            if (movement == null)
            {
                movement = GetComponent<UnitMovement>();
            }

            if (movement != null)
            {
                movement.Stuck += HandleStuck;
            }
        }

        private void OnDisable()
        {
            if (movement != null)
            {
                movement.Stuck -= HandleStuck;
            }
        }

        /// <summary>
        /// Called by <see cref="UnitMovement"/> when the unit cannot reach its
        /// destination. Drops the current target so the AI re-decides instead
        /// of standing still forever.
        /// </summary>
        private void HandleStuck()
        {
            ClearTarget();
            _nextRetargetTime = Time.time + retargetInterval * 4f;
        }

        /// <summary>Orders this unit to attack the central objective.</summary>
        public void CommandAttackObjective(PantheonObjective target)
        {
            if (target == null || !target.IsAlive || runtime == null || !runtime.IsAlive)
            {
                return;
            }

            bool sameTarget = _objectiveTarget == target;

            _target = null;
            _objectiveTarget = target;
            _buildingTarget = null;
            _manualTarget = true;
            _autoTarget = false;
            _chasing = false;
            _nextRetargetTime = 0f;

            if (!sameTarget)
            {
                _nextAttackTime = 0f;
            }

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

            bool sameTarget = _buildingTarget == target;

            _target = null;
            _objectiveTarget = null;
            _buildingTarget = target;
            _manualTarget = true;
            _autoTarget = false;
            _chasing = false;
            _nextRetargetTime = 0f;

            if (!sameTarget)
            {
                _nextAttackTime = 0f;
            }

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

            // Safety net: if the attack clip had no animation event (or the
            // relay was lost), the pending hit still lands instead of stalling.
            if (HasPendingTargets() && Time.time >= _pendingHitDeadline)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[{name}] Attack hit applied by the safety net — the Attack " +
                    "clip's OnAttackHit animation event did not fire.");
#endif
                ApplyAttackHit();
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
                    float radius = Mathf.Max(aggroRadius, CurrentAttackRange + rangePadding);
                    _target = UnitRegistry.FindNearestEnemy(
                        transform.position,
                        runtime.Faction,
                        radius,
                        IsMeleeAttacker);

                    if (_target != null)
                    {
                        _autoTarget = true;
                    }
                }
            }

            // The draw is a visible action that happens before committing to
            // melee: wait for the last throw to finish, stop, sheathe/unsheathe
            // and only then chase or attack.
            if (_drawingSword || ShouldDrawSwordBeforeAttacking())
            {
                UpdateDrawSword();
                return;
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

            if (_target.IsMounted && IsMeleeAttacker)
            {
                BuildingRuntime wall = _target.MountedWallBuilding;

                if (wall != null && wall.IsAlive)
                {
                    CommandAttackBuilding(wall);
                }
                else
                {
                    ClearTarget();
                }

                return;
            }

            Vector3 delta = _target.transform.position - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            float attackDistance = CurrentAttackRange + rangePadding;

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

            _chasing = false;

            if (Time.time >= _nextAttackTime)
            {
                BeginAttack();
            }
        }

        /// <summary>
        /// Starts one attack cycle. Targets are snapshotted so the hit lands on
        /// the target that was actually attacked when the animation event fires.
        /// </summary>
        private void BeginAttack()
        {
            _pendingUnitTarget = _target;
            _pendingBuildingTarget = _buildingTarget;
            _pendingObjectiveTarget = _objectiveTarget;

            // Only living units can be hit by a pilum. Buildings and the
            // objective are always attacked in melee, so the volley is kept.
            bool usesProjectile = !IsMeleeAttacker && _pendingUnitTarget != null;
            bool consumesPilum = usesProjectile && _usesPilumVolley;
            float interval = consumesPilum ? _pilumInterval : _attackInterval;

            _nextAttackTime = Time.time + interval;
            _pendingAttackUsesProjectile = usesProjectile;
            _pendingAttackConsumesPilum = consumesPilum;
            _pendingAttackDamage = consumesPilum ? _pilumDamage : _attackDamage;
            _pendingHitDeadline = Time.time + Mathf.Max(0.5f, interval * 1.2f);
            _hitAppliedThisAttack = false;

            if (_proceduralAnimator != null)
            {
                _proceduralAnimator.PlayAttack(ApplyAttackHit);
                return;
            }

            if (_animator == null)
            {
                // Units without an attack clip keep the previous instant-hit behaviour.
                ApplyAttackHit();
                return;
            }

            // The attack clips are about one second long: never re-trigger
            // while one is still playing, or the animation would restart
            // mid-play without a new shot.
            const float minAttackSpacing = 1.05f;

            if (Time.time - _lastAttackStartTime < minAttackSpacing)
            {
                _nextAttackTime = _lastAttackStartTime + minAttackSpacing;
                ClearPendingTargets();
                return;
            }

            if (usesProjectile && HasAnimatorParameter("Throw"))
            {
                _lastAttackStartTime = Time.time;
                _animator.SetTrigger("Throw");

                // The throw clip is longer than the melee swing: its hit event
                // fires around 1.15 s at normal speed.
                _pendingHitDeadline = Time.time + 1.6f;
                return;
            }

            if (!HasAnimatorParameter("Attack"))
            {
                ApplyAttackHit();
                return;
            }

            _lastAttackStartTime = Time.time;

            // Controllers with more than one attack animation (for example
            // a sword slash and a shield bash) expose an AttackVariant int.
            // Pick one at random so the unit does not repeat the same swing.
            if (HasAnimatorParameter("AttackVariant"))
            {
                _animator.SetInteger("AttackVariant", Random.Range(0, 2));
            }

            _animator.SetTrigger("Attack");

            // The attack clip lasts ~1.1s and its OnAttackHit event fires
            // mid-clip, so the fallback only needs to outlive the clip, not
            // the whole attack interval.
            _pendingHitDeadline = Time.time + 1.2f;
        }

        /// <summary>
        /// Damage frame callback invoked by the Attack animation event. Applies
        /// the hit once to the target snapshotted by <see cref="BeginAttack"/>.
        /// </summary>
        public void ApplyAttackHit()
        {
            // The attack clip can deliver this callback more than once (a
            // duplicated animation event) or race with the safety-net deadline;
            // guarantee exactly one hit per attack cycle.
            if (_hitAppliedThisAttack)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[{name}] Ignored a repeated attack hit in the same cycle " +
                    "(duplicate animation event or safety net racing the relay).");
#endif
                return;
            }

            _hitAppliedThisAttack = true;

            if (runtime == null || !runtime.IsAlive)
            {
                ClearPendingTargets();
                return;
            }

            if (_pendingUnitTarget != null && _pendingUnitTarget.IsAlive)
            {
                if (_pendingAttackUsesProjectile)
                {
                    ArrowProjectile.Spawn(
                        transform.position + Vector3.up * 1.2f,
                        _pendingUnitTarget,
                        _pendingAttackDamage,
                        runtime,
                        visualPrefab: _projectilePrefab);
                }
                else
                {
                    _pendingUnitTarget.ApplyDamage(_pendingAttackDamage, true, runtime);
                }
            }
            else if (_pendingBuildingTarget != null && _pendingBuildingTarget.IsAlive)
            {
                _pendingBuildingTarget.ApplyDamage(_pendingAttackDamage);
            }
            else if (_pendingObjectiveTarget != null && _pendingObjectiveTarget.IsAlive)
            {
                _pendingObjectiveTarget.ApplyDamage(_pendingAttackDamage);
            }

            if (_pendingAttackConsumesPilum)
            {
                _remainingPilums = Mathf.Max(0, _remainingPilums - 1);
                _usesPilumVolley = _remainingPilums > 0;
                RefreshPilumVisuals();

                if (_remainingPilums <= 0 && _drawSwordOnFirstMelee && !_swordDrawn)
                {
                    // Do not interrupt the throw clip: the draw starts once the
                    // last pilum animation has had time to finish.
                    _drawSwordReadyTime = Mathf.Max(_nextAttackTime, Time.time + 0.9f);
                }
            }

            ClearPendingTargets();
        }

        /// <summary>
        /// Shows the pilum held in hand while the volley lasts and hides the
        /// spare pilums on the back one by one as they are thrown.
        /// </summary>
        private void RefreshPilumVisuals()
        {
            if (pilumHandVisual != null)
            {
                pilumHandVisual.gameObject.SetActive(
                    _remainingPilums > 0 && !_swordDrawn);
            }

            if (pilumBackVisuals == null)
            {
                return;
            }

            int visibleBackPilums = Mathf.Max(0, _remainingPilums - 1);

            for (int i = 0; i < pilumBackVisuals.Length; i++)
            {
                Transform visual = pilumBackVisuals[i];

                if (visual != null)
                {
                    visual.gameObject.SetActive(i < visibleBackPilums);
                }
            }
        }

        /// <summary>
        /// True when the unit must stop and draw its gladius before chasing or
        /// attacking. Only melee-ready units with a live target qualify.
        /// </summary>
        private bool ShouldDrawSwordBeforeAttacking()
        {
            return _drawSwordOnFirstMelee &&
                !_swordDrawn &&
                IsMeleeAttacker &&
                HasTarget;
        }

        /// <summary>
        /// Drives the pre-attack draw: waits for the last pilum throw to end,
        /// plays the DrawSword clip and blocks movement and attacks until the
        /// animation event (or the deadline fallback) equips the gladius.
        /// </summary>
        private void UpdateDrawSword()
        {
            Transform targetTransform = GetDrawTargetTransform();

            if (_drawingSword)
            {
                StopAndFace(targetTransform);

                if (Time.time >= _drawSwordDeadline)
                {
                    ApplyDrawSword();
                }

                return;
            }

            if (Time.time < _drawSwordReadyTime)
            {
                StopAndFace(targetTransform);
                return;
            }

            BeginDrawSword(targetTransform);
        }

        private void BeginDrawSword(Transform targetTransform)
        {
            _drawingSword = true;
            _drawSwordDeadline = Time.time + 1f;
            StopAndFace(targetTransform);

            if (_animator != null && HasAnimatorParameter("DrawSword"))
            {
                _animator.ResetTrigger("DrawSword");
                _animator.SetTrigger("DrawSword");
                return;
            }

            // Controllers without a draw clip equip the sword instantly.
            ApplyDrawSword();
        }

        private void StopAndFace(Transform targetTransform)
        {
            if (movement == null)
            {
                return;
            }

            movement.Stop();

            if (targetTransform != null)
            {
                movement.FaceTowards(targetTransform.position);
            }
        }

        private Transform GetDrawTargetTransform()
        {
            if (_buildingTarget != null)
            {
                return _buildingTarget.transform;
            }

            if (_objectiveTarget != null)
            {
                return _objectiveTarget.transform;
            }

            if (_target != null)
            {
                return _target.transform;
            }

            return null;
        }

        /// <summary>
        /// Called by the DrawSword animation event (or by the fallback
        /// deadline): moves the gladius from the hip to the hand. The local
        /// offset matches the Legionario prefab so the grip lines up with the
        /// same hand bone.
        /// </summary>
        public void ApplyDrawSword()
        {
            _drawingSword = false;
            _drawSwordDeadline = 0f;

            if (_animator != null && HasAnimatorParameter("DrawSword"))
            {
                _animator.ResetTrigger("DrawSword");
            }

            if (_swordDrawn)
            {
                return;
            }

            _swordDrawn = true;
            _drawSwordReadyTime = 0f;

            if (pilumHandVisual != null)
            {
                pilumHandVisual.gameObject.SetActive(false);
            }

            if (gladiusVisual == null || gladiusHandBone == null)
            {
                return;
            }

            gladiusVisual.SetParent(gladiusHandBone, false);
            gladiusVisual.localPosition = gladiusHandLocalPosition;
            gladiusVisual.localRotation = Quaternion.Euler(gladiusHandLocalEuler);

            // Let the draw clip finish before the next attack can interrupt it.
            _nextAttackTime = Mathf.Max(_nextAttackTime, Time.time + 0.15f);
        }

        private void ClearPendingTargets()
        {
            _pendingUnitTarget = null;
            _pendingBuildingTarget = null;
            _pendingObjectiveTarget = null;
            _pendingAttackUsesProjectile = false;
            _pendingAttackConsumesPilum = false;
            _pendingAttackDamage = 0f;
            _pendingHitDeadline = float.MaxValue;
        }

        private bool HasPendingTargets()
        {
            return _pendingUnitTarget != null ||
                _pendingBuildingTarget != null ||
                _pendingObjectiveTarget != null;
        }

        /// <summary>
        /// True when the unit animator exposes the given trigger. Avoids the
        /// "Parameter does not exist" console spam when a controller only has
        /// the Speed parameter.
        /// </summary>
        private bool HasAnimatorParameter(string parameterName)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                return false;
            }

            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
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
                BeginAttack();
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
                BeginAttack();
            }
        }

        private void ChaseTarget()
        {
            Vector3 targetPosition = _target.transform.position;
            bool targetMoved = (targetPosition - _lastChasePosition).sqrMagnitude > 0.25f;

            if (!targetMoved && Time.time < _nextChaseTime && Time.time < _nextForceRepathTime)
            {
                return;
            }

            if (targetMoved)
            {
                _nextChaseTime = Time.time + chaseRefreshInterval;
            }

            _nextForceRepathTime = Time.time + forceRepathInterval;
            _lastChasePosition = targetPosition;
            _chasing = true;
            _chaseIssuedTime = Time.time;
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
