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

        public void Initialize(
            WallMountPoint wall,
            int slot,
            UnitRuntime runtime,
            UnitMovement movement)
        {
            _wall = wall;
            _slot = slot;
            _runtime = runtime;
            _movement = movement;
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

            SnapToSlot();
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
            if (target == null || _wall == null || target == _wall || _runtime == null)
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
            SnapToSlot();
        }

        /// <summary>Leaves the wall and returns the unit to the ground below.</summary>
        public void Dismount()
        {
            if (_combat != null)
            {
                _combat.SetRangeBonus(0f);
                _combat.ClearTarget();
            }

            Vector3 basePoint = transform.position;
            basePoint.y = 0f;

            if (_wall != null)
            {
                Vector3 approach = _wall.transform.TransformPoint(new Vector3(0f, 0f, 1.4f));

                if (NavMesh.SamplePosition(approach, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    basePoint = hit.position;
                }
            }

            if (_movement != null && _movement.Agent != null)
            {
                _movement.Agent.enabled = true;
            }

            transform.position = basePoint;
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
            ReleaseWall();
        }
    }
}
