using System.Collections;
using ProjectoRTS.Combat;
using ProjectoRTS.Units;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Runtime state for a unit standing on top of a wall. The navmesh agent
    /// is disabled while mounted and the archer gains bonus range. It
    /// dismounts when ordered elsewhere, when the wall is destroyed or when
    /// it dies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallArcher : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float rangeBonus = 2f;

        private WallMountPoint _wall;
        private WallMountPoint _targetWall;
        private int _slot;
        private UnitRuntime _runtime;
        private UnitMovement _movement;
        private UnitCombat _combat;
        private bool _released;
        private bool _transferring;
        private float _climbSide = 1f;

        public void Initialize(
            WallMountPoint wall,
            int slot,
            UnitRuntime runtime,
            UnitMovement movement,
            Vector3 climbStart,
            float climbSide)
        {
            _wall = wall;
            _slot = slot;
            _runtime = runtime;
            _movement = movement;
            _climbSide = climbSide >= 0f ? 1f : -1f;
            _combat = runtime != null ? runtime.GetComponent<UnitCombat>() : null;

            if (_movement != null && _movement.Agent != null)
            {
                _movement.Agent.isStopped = true;
                _movement.Agent.ResetPath();
                _movement.Agent.enabled = false;
            }

            if (_combat != null)
            {
                _combat.SetRangeBonus(rangeBonus);
                _combat.ClearTarget();
            }

            if (_runtime != null)
            {
                _runtime.SetMounted(
                    true,
                    _wall != null ? _wall.GetComponentInParent<BuildingRuntime>() : null);
            }

            _transferring = true;
            StartCoroutine(ClimbRoutine(climbStart));
        }

        /// <summary>
        /// Raises the unit from the base of the wall up to its slot. The unit
        /// first settles at the wall face below its slot and then rises
        /// straight up along the face — no jump arc.
        /// </summary>
        private IEnumerator ClimbRoutine(Vector3 start)
        {
            if (_wall == null)
            {
                _transferring = false;
                yield break;
            }

            // The archer faces the wall while climbing up its face.
            Vector3 face = _wall.transform.position - start;
            face.y = 0f;
            if (face.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
            }

            Quaternion wallFacing = transform.rotation;
            Quaternion outwardFacing = FaceOutwardRotation();

            // Phase 1: close the remaining ground gap to the wall face.
            Vector3 slot = _wall.GetSlotPosition(_slot);
            Vector3 basePoint = _wall.GetClimbBasePosition(_slot, _climbSide, start.y);
            Vector3 settleDelta = basePoint - start;
            settleDelta.y = 0f;

            float settleDuration = Mathf.Clamp(settleDelta.magnitude * 0.45f, 0f, 0.45f);
            float elapsed = 0f;
            while (elapsed < settleDuration)
            {
                if (ClimbBlocked())
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / settleDuration);
                transform.position = Vector3.Lerp(start, basePoint, t);
                yield return null;
            }

            transform.position = basePoint;

            // Phase 2: steady eased rise along the wall face into the slot,
            // turning outward over the last stretch instead of snapping.
            float riseHeight = slot.y - basePoint.y;
            float riseDuration = Mathf.Clamp(riseHeight * 0.55f, 0.6f, 1.3f);
            elapsed = 0f;
            while (elapsed < riseDuration)
            {
                if (ClimbBlocked())
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / riseDuration);
                float eased = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(basePoint, slot, eased);

                float turn = Mathf.Clamp01((t - 0.65f) / 0.35f);
                if (turn > 0f)
                {
                    transform.rotation = Quaternion.Slerp(wallFacing, outwardFacing, turn);
                }

                yield return null;
            }

            _transferring = false;
            SnapToSlot();
            transform.rotation = outwardFacing;
        }

        /// <summary>
        /// True when the climb must stop. Unit death leaves the cleanup to
        /// the Update loop; a destroyed wall dismounts immediately.
        /// </summary>
        private bool ClimbBlocked()
        {
            if (_runtime == null || !_runtime.IsAlive)
            {
                return true;
            }

            if (_wall == null || !_wall.isActiveAndEnabled)
            {
                Dismount();
                return true;
            }

            return false;
        }

        /// <summary>Rotation looking over the wall, away from the face it climbed.</summary>
        private Quaternion FaceOutwardRotation()
        {
            if (_wall == null)
            {
                return transform.rotation;
            }

            Vector3 outward = _wall.transform.forward * -_climbSide;
            outward.y = 0f;
            return outward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(outward.normalized, Vector3.up)
                : transform.rotation;
        }

        /// <summary>Turns the archer to look over the wall, away from the face it climbed.</summary>
        private void FaceOutward()
        {
            transform.rotation = FaceOutwardRotation();
        }

        private void Update()
        {
            if (_runtime == null || !_runtime.IsAlive)
            {
                ReleaseWall();
                enabled = false;
                return;
            }

            if (_wall == null || !_wall.isActiveAndEnabled)
            {
                Dismount();
                return;
            }

            if (!_transferring)
            {
                SnapToSlot();
            }
        }

        /// <summary>
        /// Moves the archer to a slot on a nearby connected wall segment,
        /// sliding along the top of the wall. Only segments close to the
        /// current one are accepted.
        /// </summary>
        public void RequestMoveTo(WallMountPoint target)
        {
            if (target == null || _wall == null || target == _wall || _runtime == null || _transferring)
            {
                return;
            }

            Vector3 delta = target.transform.position - _wall.transform.position;
            delta.y = 0f;

            if (delta.sqrMagnitude > 6.25f)
            {
                return;
            }

            if (!target.HasSpace)
            {
                return;
            }

            int slot = target.AcquireSlot(_runtime);
            if (slot < 0)
            {
                return;
            }

            _wall.Release(_runtime);
            _targetWall = target;
            _released = false;
            _transferring = true;
            StartCoroutine(TransferRoutine(target, slot));
        }

        private IEnumerator TransferRoutine(WallMountPoint target, int slot)
        {
            Vector3 start = transform.position;
            Vector3 end = target.GetSlotPosition(slot);
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (_runtime == null || !_runtime.IsAlive || target == null || !target.isActiveAndEnabled)
                {
                    Dismount();
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 point = Vector3.Lerp(start, end, t);
                point.y += Mathf.Sin(t * Mathf.PI) * 0.4f;
                transform.position = point;

                Vector3 forward = end - start;
                forward.y = 0f;

                if (forward.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                }

                yield return null;
            }

            if (target == null || !target.isActiveAndEnabled)
            {
                Dismount();
                yield break;
            }

            _wall = target;
            _slot = slot;
            _targetWall = null;
            _released = false;
            _transferring = false;

            if (_runtime != null)
            {
                _runtime.SetMounted(
                    true,
                    target.GetComponentInParent<BuildingRuntime>());
            }

            SnapToSlot();
            FaceOutward();
        }

        /// <summary>Leaves the wall and returns the unit to the ground below.</summary>
        public void Dismount()
        {
            if (_runtime != null)
            {
                _runtime.SetMounted(false);
            }

            if (_combat != null)
            {
                _combat.SetRangeBonus(0f);
                _combat.ClearTarget();
            }

            Vector3 basePoint = transform.position;
            basePoint.y = 0f;

            if (_wall != null)
            {
                // Always drop on the ladder face when the wall marks one,
                // regardless of where the archer climbed from.
                float dropSide = Mathf.Approximately(_wall.ClimbSide, 0f)
                    ? _climbSide
                    : _wall.ClimbSide;

                Vector3 approach = _wall.transform.TransformPoint(new Vector3(0f, 0f, dropSide * _wall.ApproachDistance));

                // Default to the ladder face itself: even when the ground
                // there is blocked (buildings, map edge), the drop must stay
                // on this side instead of the wall center, where both faces
                // are equally near.
                basePoint = approach;

                // A close snap cannot cross the wall; the wider fallback is
                // validated so it never lands on the opposite face.
                if (NavMesh.SamplePosition(approach, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                {
                    basePoint = hit.position;
                }
                else if (NavMesh.SamplePosition(approach, out hit, 3f, NavMesh.AllAreas))
                {
                    Vector3 sampledDelta = hit.position - _wall.transform.position;
                    sampledDelta.y = 0f;

                    if (Vector3.Dot(sampledDelta, _wall.transform.forward) * dropSide >= 0f)
                    {
                        basePoint = hit.position;
                    }
                }
            }

            // Teleport while the agent is still disabled, then enable it.
            // Enabling first makes the agent clamp to the nearest walkable
            // point, and on top of a wall both faces are equidistant, so it
            // could anchor on the wrong side and fight the drop.
            transform.position = basePoint;

            if (_movement != null && _movement.Agent != null)
            {
                _movement.Agent.enabled = true;
            }

#if UNITY_EDITOR
            if (_wall != null)
            {
                Debug.Log(
                    $"[WallArcher] {name} dismount: wall ladder side {_wall.ClimbSide:+0;-0;-}, " +
                    $"archer climb side {_climbSide:+0;-0;-}, drop point {basePoint}",
                    this);
            }
#endif

            ReleaseWall();
            Destroy(this);
        }

        private void SnapToSlot()
        {
            if (_wall == null)
            {
                return;
            }

            Vector3 slot = _wall.GetSlotPosition(_slot);
            transform.position = slot;
        }

        private void ReleaseWall()
        {
            if (_released)
            {
                return;
            }

            _released = true;

            if (_wall != null && _runtime != null)
            {
                _wall.Release(_runtime);
            }

            if (_targetWall != null && _runtime != null)
            {
                _targetWall.Release(_runtime);
            }
        }

        private void OnDestroy()
        {
            if (_runtime != null)
            {
                _runtime.SetMounted(false);
            }

            ReleaseWall();
        }
    }
}
