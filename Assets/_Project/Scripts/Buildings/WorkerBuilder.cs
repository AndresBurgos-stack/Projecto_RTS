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
        [SerializeField, Min(0.5f)] private float gatherRange = 1.3f;
        [SerializeField, Min(1f)] private float gatherTurnSpeed = 720f;
        [SerializeField] private float gatherYawOffset = 0f;
        [SerializeField, Min(0.5f)] private float depositRange = 3f;
        [SerializeField, Min(0.1f)] private float resourceRepathInterval = 0.6f;
        [SerializeField, Min(1f)] private float stuckRepathTimeout = 5f;

        [Header("Flee")]
        [SerializeField, Min(0.5f)] private float fleeRadius = 9f;
        [SerializeField, Min(0.5f)] private float safeRadius = 12f;
        [SerializeField, Min(1f)] private float fleeDistance = 14f;
        [SerializeField, Min(1f)] private float fleeSpeedMultiplier = 1.6f;
        [SerializeField, Min(0.1f)] private float fleeScanInterval = 0.3f;
        [SerializeField, Min(0.5f)] private float fleeRepathInterval = 1f;

        [Header("Cargo Visuals")]
        [SerializeField] private Material woodLogMaterial;
        [SerializeField] private Material bronzeOreMaterial;
        [Tooltip("Log bundle length as a fraction of the worker's own size.")]
        [SerializeField, Min(0.05f)] private float logLengthFactor = 0.28f;
        [Tooltip("Ore cluster diameter as a fraction of the worker's own size.")]
        [SerializeField, Min(0.05f)] private float oreSizeFactor = 0.17f;

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
        private float _outOfRangeTimer;

        private Vector3 _siteFootprintCenter;
        private Vector2 _siteFootprintHalf;

        private Animator _animator;
        private bool _hasGatherParam;
        private bool _gatheringSet;
        private bool _hasCarryingParam;
        private bool _carryingSet;
        private bool _hasBuildingParam;
        private bool _buildingSet;

        private Transform _cargoAnchor;
        private GameObject _cargoRoot;
        private GameObject _hammerVisual;
        private GameObject _logsHolder;
        private GameObject _rockHolder;
        private bool _cargoVisualReady;
        private bool _cargoVisualMissing;
        private readonly List<Renderer> _hammerRenderers = new List<Renderer>(4);
        private bool _hammerVisible = true;

        private static Material _woodFallbackMaterial;
        private static Material _oreFallbackMaterial;
        private Transform _leftLegBone;
        private Transform _rightLegBone;
        private float _bodyYawError;
        private bool _bodyYawErrorValid;

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
                    ReleaseNodeClaim();
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
            _animator = GetComponentInChildren<Animator>(true);
            _hasGatherParam = HasAnimatorParameter(_animator, "Gathering");
            _hasCarryingParam = HasAnimatorParameter(_animator, "Carrying");
            _hasBuildingParam = HasAnimatorParameter(_animator, "Building");

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
            ReleaseNodeClaim();
        }

        private void OnDestroy()
        {
            ReleaseNodeClaim();
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
            CacheSiteFootprint();

            if (_site == null)
            {
                SetBuilding(false);
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

            // Synced every frame so the carry animation follows the cargo even
            // when the worker spawned, fled or got assigned while loaded.
            SetCarrying(_cargoAmount > 0);
            EnsureCargoVisuals();
            UpdateCargoVisual();

            if (UpdateFlee())
            {
                return;
            }

            if (_site != null && _site.IsComplete)
            {
                _site = null;
                _stoppedAtSite = false;
                SetBuilding(false);
                ResumeCurrentTask();
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
                    CacheSiteFootprint();
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
                SetBuilding(false);
                _repathTimer -= Time.deltaTime;
                _outOfRangeTimer += Time.deltaTime;

                bool pathMissing = _agent != null &&
                    _agent.isOnNavMesh && !_agent.pathPending && !_agent.hasPath;
                bool timedOut = _outOfRangeTimer >= stuckRepathTimeout;

                if ((_repathTimer <= 0f && pathMissing) || timedOut)
                {
                    _repathTimer = repathInterval;
                    _outOfRangeTimer = 0f;
                    WalkToSite();
                }

                return;
            }

            _outOfRangeTimer = 0f;

            if (!_stoppedAtSite)
            {
                StopAtSite();
            }

            SetBuilding(true);
            _site.AddProgress(Time.deltaTime * workMultiplier);
        }

        private void UpdateGathering()
        {
            if (_resourceNode == null || _resourceNode.IsDepleted)
            {
                ReleaseNodeClaim();
                _resourceNode = FindGatherNode();
                _atNode = false;
                _gatherTimer = 0f;
                _resourceRepathTimer = 0f;
                _outOfRangeTimer = 0f;

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
                _outOfRangeTimer += Time.deltaTime;

                bool pathMissing = _agent != null &&
                    _agent.isOnNavMesh && !_agent.pathPending && !_agent.hasPath;
                bool timedOut = _outOfRangeTimer >= stuckRepathTimeout;

                if ((_resourceRepathTimer <= 0f && pathMissing) || timedOut)
                {
                    _resourceRepathTimer = resourceRepathInterval;
                    _outOfRangeTimer = 0f;
                    WalkToResource();
                }

                return;
            }

            _outOfRangeTimer = 0f;

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
                _outOfRangeTimer += Time.deltaTime;

                bool pathMissing = _agent != null &&
                    _agent.isOnNavMesh && !_agent.pathPending && !_agent.hasPath;
                bool timedOut = _outOfRangeTimer >= stuckRepathTimeout;

                if ((_resourceRepathTimer <= 0f && pathMissing) || timedOut)
                {
                    _resourceRepathTimer = resourceRepathInterval;
                    _outOfRangeTimer = 0f;
                    WalkToDropOff();
                }

                return;
            }

            _outOfRangeTimer = 0f;

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

            SetGathering(false);

            ResourceType harvestedType = _resourceNode.Type;
            int harvested = _resourceNode.Harvest(carryCapacity);

            _gatherTimer = 0f;
            _atNode = false;
            _resourceRepathTimer = 0f;

            if (harvested <= 0)
            {
                ReleaseNodeClaim();
                _resourceNode = null;
                return;
            }

            _cargoType = harvestedType;
            _cargoAmount = harvested;
            ReleaseNodeClaim();
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

            ResourceNode node = ResourceNode.FindNearestAvailable(
                transform.position,
                _preferredResource,
                AutoAssignRange,
                this);

            if (node != null && node.TryClaim(this))
            {
                return node;
            }

            return null;
        }

        private void WalkToResource()
        {
            if (_resourceNode == null)
            {
                return;
            }

            SetGathering(false);
            float standDistance = _resourceNode.GatherRadius + gatherRange * 0.75f;
            TryWalkTo(GetApproachPoint(_resourceNode.GatherPoint, standDistance));
        }

        private void WalkToDropOff()
        {
            if (_dropOff == null)
            {
                return;
            }

            SetGathering(false);
            TryWalkTo(GetApproachPoint(_dropOff.transform.position, depositRange * 0.75f));
        }

        private bool IsInGatherRange()
        {
            if (_resourceNode == null)
            {
                return false;
            }

            Vector3 delta = _resourceNode.GatherPoint - transform.position;
            delta.y = 0f;
            float range = _resourceNode.GatherRadius + gatherRange;
            return delta.sqrMagnitude <= range * range;
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
            SetGathering(true);

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
                _agent.updateRotation = false;
            }

            FaceGatherPoint(true);

#if UNITY_EDITOR
            Debug.Log(
                $"[WorkerBuilder] {name}: gather bodyYawError={_bodyYawError:F1}deg, " +
                $"rootYaw={transform.eulerAngles.y:F1}deg");
#endif
        }

        /// <summary>
        /// Turns the worker towards the visible gather point. Called once on
        /// arrival and from <see cref="LateUpdate"/> every frame afterwards,
        /// because the NavMeshAgent and collisions can otherwise leave the
        /// body facing the wrong way.
        /// </summary>
        private void FaceGatherPoint(bool snap)
        {
            if (_resourceNode == null)
            {
                return;
            }

            Vector3 look = _resourceNode.GatherPoint - transform.position;
            look.y = 0f;

            if (look.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            // The gather clip carries its own baked-in root rotation, so the
            // visible body can face sideways even when the transform is aimed
            // at the resource. Measure the real pose yaw from the leg bones
            // and cancel it out of the root rotation.
            float measured = GetBodyYawError();
            float deltaTime = snap ? 0f : Time.deltaTime;

            if (snap || !_bodyYawErrorValid)
            {
                _bodyYawError = measured;
                _bodyYawErrorValid = true;
            }
            else
            {
                _bodyYawError = Mathf.LerpAngle(
                    _bodyYawError,
                    measured,
                    1f - Mathf.Exp(-8f * deltaTime));
            }

            float yaw = -_bodyYawError;

            if (Mathf.Abs(gatherYawOffset) > 0.01f)
            {
                yaw += gatherYawOffset;
            }

            Quaternion target = Quaternion.LookRotation(look.normalized, Vector3.up);

            if (Mathf.Abs(yaw) > 0.01f)
            {
                target = Quaternion.Euler(0f, yaw, 0f) * target;
            }

            if (snap)
            {
                transform.rotation = target;
                return;
            }

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                target,
                Mathf.Max(1f, gatherTurnSpeed) * Time.deltaTime);
        }

        /// <summary>
        /// Visual yaw of the animated body relative to the worker root,
        /// measured from the humanoid upper leg bones. Returns 0 when the
        /// skeleton is not available.
        /// </summary>
        private float GetBodyYawError()
        {
            if (_animator == null)
            {
                return 0f;
            }

            if (_leftLegBone == null || _rightLegBone == null)
            {
                _leftLegBone = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                _rightLegBone = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            }

            if (_leftLegBone == null || _rightLegBone == null)
            {
                return 0f;
            }

            Vector3 side = _leftLegBone.position - _rightLegBone.position;
            side.y = 0f;

            if (side.sqrMagnitude <= 0.000001f)
            {
                return 0f;
            }

            Vector3 bodyForward = Vector3.Cross(Vector3.up, side.normalized);

            return Vector3.SignedAngle(transform.forward, bodyForward, Vector3.up);
        }

        /// <summary>
        /// Keeps the gather facing corrected after the animator has evaluated
        /// the clip pose for this frame.
        /// </summary>
        private void LateUpdate()
        {
            if (_atNode && _resourceNode != null && IsInGatherRange())
            {
                FaceGatherPoint(false);
            }
        }

        private void CancelGathering()
        {
            ReleaseNodeClaim();
            SetGathering(false);

            if (_agent != null)
            {
                _agent.updateRotation = true;
            }

            _resourceNode = null;
            _atNode = false;
            _gatherTimer = 0f;
            _resourceRepathTimer = 0f;
        }

        /// <summary>Frees the reserved slot on the current resource node.</summary>
        private void ReleaseNodeClaim()
        {
            if (_resourceNode != null)
            {
                _resourceNode.Release(this);
            }
        }

        /// <summary>Plays or stops the gather animation on the worker animator.</summary>
        private void SetGathering(bool value)
        {
            if (!_hasGatherParam || _animator == null)
            {
                return;
            }

            if (_gatheringSet == value)
            {
                return;
            }

            _gatheringSet = value;
            _animator.SetBool("Gathering", value);
        }

        /// <summary>Plays the carry animation while the worker has cargo.</summary>
        private void SetCarrying(bool value)
        {
            if (!_hasCarryingParam || _animator == null)
            {
                return;
            }

            if (_carryingSet == value)
            {
                return;
            }

            _carryingSet = value;
            _animator.SetBool("Carrying", value);
        }

        /// <summary>Plays the strike animation while the worker builds.</summary>
        private void SetBuilding(bool value)
        {
            if (!_hasBuildingParam || _animator == null)
            {
                return;
            }

            if (_buildingSet == value)
            {
                return;
            }

            _buildingSet = value;
            _animator.SetBool("Building", value);
        }

        /// <summary>
        /// Builds the carried-cargo props once: a log bundle for wood and the
        /// bronze ore vein. The props live in normal world-metre space under
        /// the unit root and are glued to the rig's right-hand bone every
        /// frame, so they appear exactly when the carry animation plays. The
        /// "Martillo" node is never used as a pose source: after the model
        /// swap its placement points far outside the character, which dragged
        /// the cargo under the terrain.
        /// </summary>
        private void EnsureCargoVisuals()
        {
            if (_cargoVisualReady || _cargoVisualMissing)
            {
                return;
            }

            Transform hammer = FindDeepChild(transform, "Martillo");
            if (hammer == null)
            {
                _cargoVisualMissing = true;
                return;
            }

            _hammerVisual = hammer.gameObject;
            CacheHammerRenderers(hammer);

            // Grip on the animated hand bone. The hammer node only serves as
            // a fallback anchor and for the hide-while-carrying renderer swap.
            Transform grip = FindHandBone(transform);
            if (grip == null)
            {
                grip = hammer;
                Debug.LogWarning(
                    "WorkerBuilder: no RightHand bone found; carrying the cargo on the Martillo node instead.");
            }
            else
            {
                float gripDistance = Vector3.Distance(grip.position, transform.position);
                if (gripDistance > 3f)
                {
                    Debug.LogWarning(
                        $"WorkerBuilder: RightHand bone is {gripDistance:0.00} m from the unit root; cargo may look misplaced.");
                }
            }

            _cargoAnchor = grip;

            // The cargo root mirrors the grip's world pose each frame. Its
            // scale is normalised to one so props can be sized in plain
            // metres, regardless of the unit's or the rig's own scaling.
            _cargoRoot = new GameObject("Cargo Anchor");
            Transform cargoTransform = _cargoRoot.transform;
            cargoTransform.SetParent(transform, false);
            Vector3 unitLossy = AbsScale(transform.lossyScale);
            cargoTransform.localScale = new Vector3(
                1f / Mathf.Max(unitLossy.x, 1e-5f),
                1f / Mathf.Max(unitLossy.y, 1e-5f),
                1f / Mathf.Max(unitLossy.z, 1e-5f));

            SyncCargoPose();

            // Sizes come from the visible character itself: the Martillo
            // node's placement is unreliable after the model swap, but the
            // skinned body meshes always describe the real unit size.
            float unitSize = GetUnitSize();
            float logLength = unitSize * logLengthFactor;
            float oreDiameter = unitSize * oreSizeFactor;

            try
            {
                // The cargo root faces the same way as the worker, so the
                // bundle lies across the chest where both hands grip it and
                // the ore cluster sits between the palms.
                _logsHolder = BuildLogBundle(Vector3.zero, logLength);
                _rockHolder = BuildOreVein(oreDiameter);
            }
            catch (System.Exception exception)
            {
                // A broken asset reference must not throw from Update every
                // frame; disable the cargo visuals instead.
                Debug.LogWarning($"WorkerBuilder: cargo visuals disabled ({exception.Message})");
                _cargoVisualMissing = true;
                return;
            }

            _cargoVisualReady = true;
        }

        /// <summary>
        /// Finds the rig's right-hand bone: an exact "RightHand" (with or
        /// without a rig prefix like "mixamorig:") first, then any name
        /// containing it, since finger bones still sit at the palm.
        /// </summary>
        private static Transform FindHandBone(Transform root)
        {
            Transform exact = null;
            Transform loose = null;
            CollectHandBone(root, ref exact, ref loose);
            return exact != null ? exact : loose;
        }

        private static void CollectHandBone(Transform node, ref Transform exact, ref Transform loose)
        {
            string name = node.name;
            if (exact == null &&
                (string.Equals(name, "RightHand", System.StringComparison.OrdinalIgnoreCase) ||
                 name.EndsWith(":RightHand", System.StringComparison.OrdinalIgnoreCase)))
            {
                exact = node;
            }

            if (loose == null &&
                name.IndexOf("RightHand", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                loose = node;
            }

            for (int i = 0; i < node.childCount; i++)
            {
                CollectHandBone(node.GetChild(i), ref exact, ref loose);
            }
        }

        /// <summary>
        /// Diagonal of the unit's skinned body bounds, used to size the cargo
        /// relative to the worker. Skinned meshes ignore the misplaced
        /// Martillo node, whose MeshRenderers would poison combined bounds.
        /// </summary>
        private float GetUnitSize()
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool found = false;

            foreach (SkinnedMeshRenderer renderer in transform.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found && bounds.size.magnitude > 0.01f ? bounds.size.magnitude : 2.2f;
        }

        /// <summary>
        /// Glues the cargo anchor to the grip's position and the body's
        /// facing: the hands tell it where to hold, the body tells it which
        /// way to lie, so the bundle always sits across the chest.
        /// </summary>
        private void SyncCargoPose()
        {
            if (_cargoRoot == null || _cargoAnchor == null)
            {
                return;
            }

            _cargoRoot.transform.position = _cargoAnchor.position;
            _cargoRoot.transform.rotation = transform.rotation;
        }

        /// <summary>Shows or hides the hammer and the carried cargo props.</summary>
        private void UpdateCargoVisual()
        {
            // Keep the cargo glued to the hammer's world pose before any
            // visibility work, so it never lags a frame behind the hand.
            SyncCargoPose();

            if (_cargoAnchor == null)
            {
                return;
            }

            bool carrying = _cargoAmount > 0;
            bool showLogs = carrying && _cargoType == ResourceType.Wood;
            bool showRock = carrying && _cargoType == ResourceType.Bronze;

            // The cargo is only synced to the hammer's pose; the hammer itself
            // stays active and just swaps its renderers out while carrying.
            SetHammerVisible(!carrying);

            if (_logsHolder != null && _logsHolder.activeSelf != showLogs)
            {
                _logsHolder.SetActive(showLogs);
            }

            if (_rockHolder != null && _rockHolder.activeSelf != showRock)
            {
                _rockHolder.SetActive(showRock);
            }
        }

        private void SetHammerVisible(bool visible)
        {
            if (_hammerVisible == visible)
            {
                return;
            }

            _hammerVisible = visible;
            for (int i = 0; i < _hammerRenderers.Count; i++)
            {
                if (_hammerRenderers[i] != null)
                {
                    _hammerRenderers[i].enabled = visible;
                }
            }
        }

        private void CacheHammerRenderers(Transform hammer)
        {
            _hammerRenderers.Clear();
            foreach (Renderer renderer in hammer.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                _hammerRenderers.Add(renderer);
            }
        }

        private GameObject BuildLogBundle(Vector3 centerLocalPosition, float logLength)
        {
            GameObject holder = new GameObject("Cargo Wood");
            Transform holderTransform = holder.transform;
            holderTransform.SetParent(_cargoRoot.transform, false);
            holderTransform.localPosition = centerLocalPosition;
            holderTransform.localRotation = Quaternion.identity;

            // The bundle lies across the body along the cargo root's X axis,
            // so both hands grip it. Three cylinders packed in a triangle:
            // two at the bottom, one resting on top.
            float logDiameter = logLength * 0.16f;
            float lengthUnits = logLength * 0.5f;

            for (int i = 0; i < 3; i++)
            {
                GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(log.GetComponent<Collider>());
                log.name = "Log";
                log.transform.SetParent(holderTransform, false);

                MeshRenderer logRenderer = log.GetComponent<MeshRenderer>();
                logRenderer.sharedMaterial = woodLogMaterial != null
                    ? woodLogMaterial
                    : GetWoodFallbackMaterial();

                // Roll each cylinder onto its side so its length runs along
                // the bundle axis (X).
                log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                log.transform.localScale = new Vector3(logDiameter, lengthUnits, logDiameter);

                switch (i)
                {
                    case 0: // bottom, far side
                        log.transform.localPosition = new Vector3(0f, 0f, -logDiameter * 0.5f);
                        break;
                    case 1: // bottom, near side
                        log.transform.localPosition = new Vector3(0f, 0f, logDiameter * 0.5f);
                        break;
                    default: // resting on top, in the groove of the pair
                        log.transform.localPosition = new Vector3(0f, logDiameter * 0.87f, 0f);
                        break;
                }
            }

            return holder;
        }

        /// <summary>
        /// Builds a small "ore vein" cluster out of squashed spheres with the
        /// bronze material, matching the round ore chunks shown on the
        /// resource nodes. Centred on the grip so the chunk sits in the palm.
        /// The ore copy is matte so it reads as mineral instead of a mirror.
        /// </summary>
        private GameObject BuildOreVein(float oreDiameter)
        {
            GameObject holder = new GameObject("Cargo Bronze");
            Transform holderTransform = holder.transform;
            holderTransform.SetParent(_cargoRoot.transform, false);
            holderTransform.localPosition = Vector3.zero;

            Material oreMaterial = GetOreMaterial(bronzeOreMaterial);
            float diameter = oreDiameter;
            float squash = 0.78f;

            CreateOreChunk(holderTransform, oreMaterial,
                new Vector3(0f, 0.06f, 0f) * diameter, diameter, squash);
            CreateOreChunk(holderTransform, oreMaterial,
                new Vector3(0.55f, -0.28f, 0.3f) * diameter, diameter * 0.55f, squash);
            CreateOreChunk(holderTransform, oreMaterial,
                new Vector3(-0.45f, -0.34f, -0.35f) * diameter, diameter * 0.45f, squash);

            return holder;
        }

        /// <summary>
        /// Clones the bronze material with a much lower metallic value: raw
        /// metal reflects the sky and reads as a plain white blob in-game.
        /// </summary>
        private static Material GetOreMaterial(Material source)
        {
            if (_oreFallbackMaterial == null)
            {
                if (source != null)
                {
                    _oreFallbackMaterial = new Material(source);
                }
                else
                {
                    _oreFallbackMaterial = new Material(
                        Shader.Find("Universal Render Pipeline/Lit"));
                    _oreFallbackMaterial.SetColor(
                        "_BaseColor", new Color(0.72f, 0.52f, 0.2f, 1f));
                }

                _oreFallbackMaterial.name = "Worker Ore";
                if (_oreFallbackMaterial.HasProperty("_Metallic"))
                {
                    _oreFallbackMaterial.SetFloat("_Metallic", 0.35f);
                }

                if (_oreFallbackMaterial.HasProperty("_Smoothness"))
                {
                    _oreFallbackMaterial.SetFloat("_Smoothness", 0.4f);
                }
            }

            return _oreFallbackMaterial;
        }

        /// <summary>Brown material used when the wood reference is missing.</summary>
        private static Material GetWoodFallbackMaterial()
        {
            if (_woodFallbackMaterial == null)
            {
                _woodFallbackMaterial = new Material(
                    Shader.Find("Universal Render Pipeline/Lit"));
                _woodFallbackMaterial.name = "Worker Wood";
                _woodFallbackMaterial.SetColor(
                    "_BaseColor", new Color(0.55f, 0.38f, 0.22f, 1f));
            }

            return _woodFallbackMaterial;
        }

        private static void CreateOreChunk(
            Transform parent,
            Material material,
            Vector3 offset,
            float diameter,
            float squash)
        {
            GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(chunk.GetComponent<Collider>());
            chunk.name = "Ore Vein";
            chunk.transform.SetParent(parent, false);
            chunk.transform.localPosition = offset;
            chunk.transform.localScale = new Vector3(diameter, diameter * squash, diameter);

            MeshRenderer chunkRenderer = chunk.GetComponent<MeshRenderer>();
            chunkRenderer.sharedMaterial = material;
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            if (root.name == childName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDeepChild(root.GetChild(i), childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>Diagonal of the combined world bounds of a prop's renderers.</summary>
        private static float GetPropWorldSize(Transform root)
        {
            return TryGetPropBounds(root, out Bounds bounds) ? bounds.size.magnitude : 0.3f;
        }

        /// <summary>Combined world bounds of every mesh renderer under a prop.</summary>
        private static bool TryGetPropBounds(Transform root, out Bounds bounds)
        {
            bool found = false;
            bounds = new Bounds(Vector3.zero, Vector3.zero);

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private static Vector3 AbsScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        }

        private static bool HasAnimatorParameter(Animator animator, string parameterName)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
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
                SetGathering(false);
                SetBuilding(false);
            }

            _fleeEnemy = enemy;
            _nextFleeRepath = Time.time + fleeRepathInterval;
            _fleePoint = FindFleePoint(enemy.transform.position);

            if (_agent != null)
            {
                _agent.speed = GetBaseSpeed() * Mathf.Max(1f, fleeSpeedMultiplier);
                _agent.isStopped = false;
                TryWalkTo(_fleePoint);
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
            if (_site == null)
            {
                return;
            }

            SetGathering(false);
            TryWalkTo(GetSiteApproachPoint());
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
                _agent.updateRotation = true;
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

        /// <summary>
        /// Moves the worker to a destination, warping it back onto the NavMesh
        /// first when it ended up off it (for example after a building carved
        /// the ground under it). Never silently fails like a raw SetDestination.
        /// </summary>
        private void TryWalkTo(Vector3 destination)
        {
            if (_agent == null)
            {
                _agent = GetComponent<NavMeshAgent>();

                if (_agent == null)
                {
                    return;
                }
            }

            if (!_agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    _agent.Warp(hit.position);
                }
                else
                {
                    return;
                }
            }

            _agent.isStopped = false;
            _agent.updateRotation = true;
            _agent.SetDestination(destination);
        }

        private bool IsInRange()
        {
            return DistanceToSiteFootprint() <= workRange;
        }

        /// <summary>
        /// Distance from the worker to the closest point of the site footprint
        /// (corners included), measured in XZ. Works for any building size, so
        /// workers at the corners of big structures like the 3x3 barracks are
        /// considered in range exactly like the ones at the middle of a wall.
        /// </summary>
        private float DistanceToSiteFootprint()
        {
            if (_site == null)
            {
                return float.MaxValue;
            }

            Vector3 local = _site.transform.InverseTransformPoint(transform.position);
            float dx = Mathf.Max(0f, Mathf.Abs(local.x - _siteFootprintCenter.x) - _siteFootprintHalf.x);
            float dz = Mathf.Max(0f, Mathf.Abs(local.z - _siteFootprintCenter.z) - _siteFootprintHalf.y);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Caches the XZ footprint of the current site from its renderer
        /// bounds, in site-local space. Called whenever the site changes.
        /// </summary>
        private void CacheSiteFootprint()
        {
            _siteFootprintCenter = Vector3.zero;
            _siteFootprintHalf = Vector2.zero;

            if (_site == null)
            {
                return;
            }

            Renderer[] renderers = _site.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds worldBounds = default;

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
                    worldBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    worldBounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                return;
            }

            Vector3 boundsCenter = worldBounds.center;
            Vector3 boundsExtents = worldBounds.extents;

            // World bounds are axis-aligned while the site may be rotated, so
            // converting just the min/max corners would yield wrong local
            // extents (negative half sizes at 90°/180°) and workers would
            // never consider themselves in range. Convert all eight corners
            // instead: exact for right-angle rotations, conservative otherwise.
            Vector3 min = new Vector3(
                float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(
                float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = boundsCenter + new Vector3(
                    (c & 1) == 0 ? -boundsExtents.x : boundsExtents.x,
                    (c & 2) == 0 ? -boundsExtents.y : boundsExtents.y,
                    (c & 4) == 0 ? -boundsExtents.z : boundsExtents.z);

                Vector3 local = _site.transform.InverseTransformPoint(corner);

                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }

            _siteFootprintCenter = new Vector3((min.x + max.x) * 0.5f, 0f, (min.z + max.z) * 0.5f);
            _siteFootprintHalf = new Vector2((max.x - min.x) * 0.5f, (max.z - min.z) * 0.5f);
        }

        /// <summary>
        /// Picks a navmesh point next to the site footprint edge: the point on
        /// the edge closest to the worker, pushed outward by the approach
        /// distance. When that spot is blocked (wall or building right next to
        /// the site), it tries 8 directions around the footprint so the worker
        /// always finds a valid place to build from.
        /// </summary>
        private Vector3 GetSiteApproachPoint()
        {
            if (_site == null)
            {
                return transform.position;
            }

            Vector3 local = _site.transform.InverseTransformPoint(transform.position);
            float edgeX = Mathf.Clamp(
                local.x,
                _siteFootprintCenter.x - _siteFootprintHalf.x,
                _siteFootprintCenter.x + _siteFootprintHalf.x);
            float edgeZ = Mathf.Clamp(
                local.z,
                _siteFootprintCenter.z - _siteFootprintHalf.y,
                _siteFootprintCenter.z + _siteFootprintHalf.y);

            Vector3 edge = _site.transform.TransformPoint(new Vector3(edgeX, 0f, edgeZ));
            Vector3 outward = edge - _site.transform.position;
            outward.y = 0f;

            if (outward.sqrMagnitude < 0.01f)
            {
                outward = Vector3.forward;
            }

            outward.Normalize();

            Vector3 candidate = edge + outward * approachDistance;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                return hit.position;
            }

            float radius = Mathf.Max(_siteFootprintHalf.x, _siteFootprintHalf.y) + approachDistance;

            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 fallback = _site.transform.position + direction * radius;

                if (NavMesh.SamplePosition(fallback, out hit, 3f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }

            return transform.position;
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
