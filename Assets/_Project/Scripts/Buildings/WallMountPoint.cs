using System.Collections;
using System.Collections.Generic;
using ProjectoRTS.Units;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Wall segment that ranged units can climb on. Mounted archers stand on
    /// top of the wall and shoot with bonus range until they are ordered
    /// elsewhere or the wall is destroyed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallMountPoint : MonoBehaviour
    {
        [SerializeField, Min(1)] private int capacity = 2;
        [SerializeField, Min(0.5f)] private float topHeight = 1.35f;
        [SerializeField] private string climbMarkerName = "Escalera";
        [SerializeField, Min(0.8f)] private float approachDistance = 1.4f;
        [SerializeField, Min(0.3f)] private float climbFaceDistance = 0.55f;

        private readonly List<UnitRuntime> _mounted = new List<UnitRuntime>(2);
        private readonly List<UnitRuntime> _pending = new List<UnitRuntime>(4);
        private bool _hasClimbMarker;
        private float _markerSide = 1f;
        private Transform _climbMarker;
        private bool _markerWarningLogged;

        private void Awake()
        {
            CacheClimbMarker();
        }

        /// <summary>
        /// Detects the ladder marker child (if any) and remembers which face
        /// of the wall it sits on. Units must always climb from that face.
        /// </summary>
        private void CacheClimbMarker()
        {
            _hasClimbMarker = false;
            _climbMarker = null;

            Transform marker = FindDescendant(transform, climbMarkerName);
            if (marker == null)
            {
#if UNITY_EDITOR
                // Log once per point: buildings like towers have no ladder by design.
                if (!_markerWarningLogged)
                {
                    _markerWarningLogged = true;
                    Debug.LogWarning(
                        $"[WallMountPoint:{name}] No child named '{climbMarkerName}' found — " +
                        "archers will climb the side the squad stands on instead of the ladder.",
                        this);
                }
#endif
                return;
            }

            // Relative to the wall root, so it holds for any wall rotation.
            Vector3 local = transform.InverseTransformPoint(marker.position);
            _markerSide = local.z >= 0f ? 1f : -1f;
            _climbMarker = marker;
            _hasClimbMarker = true;

#if UNITY_EDITOR
            Debug.Log(
                $"[WallMountPoint:{name}] Ladder '{marker.name}' detected: local z = " +
                $"{local.z:F3} -> climb side {_markerSide:+0;-0}.",
                this);
#endif
        }

        private static Transform FindDescendant(Transform root, string childName)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindDescendant(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        /// <summary>Distance from the wall center to the ground approach point.</summary>
        public float ApproachDistance => approachDistance;

        public bool HasSpace
        {
            get
            {
                Prune();
                return _mounted.Count < capacity;
            }
        }

        /// <summary>Slot position on top of the wall for a mounted archer.</summary>
        public Vector3 GetSlotPosition(int index)
        {
            return transform.TransformPoint(SlotLocal(index));
        }

        /// <summary>
        /// Local offset of a slot on the roof. A single archer stands
        /// centered; two stand paired along the wall axis; four or more
        /// spread in a cross so each one faces a different side.
        /// </summary>
        private Vector3 SlotLocal(int index)
        {
            switch (capacity)
            {
                case 1:
                    return new Vector3(0f, topHeight, 0f);
                case 2:
                    return new Vector3(index % 2 == 0 ? -0.3f : 0.3f, topHeight, 0f);
                default:
                    float spread = 0.55f;
                    switch (index % 4)
                    {
                        case 0: return new Vector3(spread, topHeight, 0f);
                        case 1: return new Vector3(-spread, topHeight, 0f);
                        case 2: return new Vector3(0f, topHeight, spread);
                        default: return new Vector3(0f, topHeight, -spread);
                    }
            }
        }

        /// <summary>
        /// Ground point at the base of the climb face, directly below the
        /// given slot. Used as the start of the climb so the unit rises along
        /// the wall face instead of gliding diagonally.
        /// </summary>
        public Vector3 GetClimbBasePosition(int slotIndex, float climbSide, float groundY)
        {
            float slotOffset = SlotLocal(slotIndex).x;

            float faceDistance = climbFaceDistance;
            if (_climbMarker != null)
            {
                Vector3 markerLocal = transform.InverseTransformPoint(_climbMarker.position);
                faceDistance = Mathf.Max(0.5f, Mathf.Abs(markerLocal.z));
            }

            Vector3 world = transform.TransformPoint(
                new Vector3(slotOffset, 0f, climbSide * faceDistance));
            world.y = groundY;
            return world;
        }

        /// <summary>
        /// The face units must climb from (ladder marker side), or 0 when the
        /// wall has no marker and the climb side follows the squad.
        /// </summary>
        public float ClimbSide
        {
            get
            {
                if (!_hasClimbMarker)
                {
                    // Self-heal: re-detect in case Awake ran too early.
                    CacheClimbMarker();
                }

                return _hasClimbMarker ? _markerSide : 0f;
            }
        }

        public void Release(UnitRuntime runtime)
        {
            _mounted.Remove(runtime);
            _pending.Remove(runtime);
        }

        /// <summary>Reserves a slot for an archer and returns its index, or -1.</summary>
        public int AcquireSlot(UnitRuntime runtime)
        {
            Prune();

            if (_mounted.Count >= capacity)
            {
                return -1;
            }

            _mounted.Add(runtime);
            return _mounted.Count - 1;
        }

        /// <summary>Orders a unit to walk to the wall and climb on top.</summary>
        public void RequestMount(UnitMovement movement)
        {
            if (movement == null)
            {
                return;
            }

            UnitRuntime runtime = movement.Runtime != null
                ? movement.Runtime
                : movement.GetComponent<UnitRuntime>();

            if (runtime == null || !runtime.IsAlive)
            {
                return;
            }

            if (_pending.Contains(runtime) || _mounted.Contains(runtime))
            {
                return;
            }

            _pending.Add(runtime);
            StartCoroutine(MountRoutine(runtime, movement));
        }

        private void Prune()
        {
            for (int i = _mounted.Count - 1; i >= 0; i--)
            {
                if (_mounted[i] == null || !_mounted[i].IsAlive)
                {
                    _mounted.RemoveAt(i);
                }
            }
        }

        private IEnumerator MountRoutine(UnitRuntime runtime, UnitMovement movement)
        {
            if (!_hasClimbMarker)
            {
                CacheClimbMarker();
            }

            // With a ladder marker the climb face is fixed to the marker's
            // side; otherwise the archer climbs the face its squad stands on.
            Vector3 toUnit = runtime.transform.position - transform.position;
            toUnit.y = 0f;
            float squadSide = Vector3.Dot(toUnit, transform.forward) >= 0f ? 1f : -1f;
            float climbSide = _hasClimbMarker ? _markerSide : squadSide;
            Vector3 approach = transform.TransformPoint(new Vector3(0f, 0f, climbSide * approachDistance));

#if UNITY_EDITOR
            Debug.Log(
                $"[WallMountPoint:{name}] mount order: marker = {_hasClimbMarker}, " +
                $"climb side {climbSide:+0;-0}, approach {approach}",
                this);
#endif

            if (NavMesh.SamplePosition(approach, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                // Reject a sample that landed on the opposite face of the
                // wall: the climb side is not negotiable.
                Vector3 sampledDelta = hit.position - transform.position;
                sampledDelta.y = 0f;
                if (Vector3.Dot(sampledDelta, transform.forward) * climbSide >= 0f)
                {
                    approach = hit.position;
                }
            }

            movement.MoveTo(approach);

            float elapsed = 0f;
            while (true)
            {
                if (runtime == null || !runtime.IsAlive || !isActiveAndEnabled)
                {
                    _pending.Remove(runtime);
                    yield break;
                }

                Vector3 delta = approach - runtime.transform.position;
                delta.y = 0f;

                if (delta.sqrMagnitude <= 1.6f)
                {
                    break;
                }

                elapsed += Time.deltaTime;
                if (elapsed > 15f)
                {
                    _pending.Remove(runtime);
                    yield break;
                }

                yield return null;
            }

            Prune();

            if (_mounted.Count >= capacity)
            {
                _pending.Remove(runtime);
                yield break;
            }

            _pending.Remove(runtime);
            _mounted.Add(runtime);

            WallArcher archer = runtime.gameObject.GetComponent<WallArcher>();
            if (archer == null)
            {
                archer = runtime.gameObject.AddComponent<WallArcher>();
            }

            archer.Initialize(
                this,
                _mounted.Count - 1,
                runtime,
                movement,
                runtime.transform.position,
                climbSide);
        }
        private void OnDrawGizmosSelected()
        {
            Transform marker = FindDescendant(transform, climbMarkerName);
            float side;
            if (marker != null)
            {
                Vector3 local = transform.InverseTransformPoint(marker.position);
                side = local.z >= 0f ? 1f : -1f;

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(marker.position, 0.08f);
            }
            else
            {
                side = 1f;
            }

            // Active climb face (cyan) vs the opposite face (gray).
            Gizmos.color = new Color(0f, 0.85f, 1f);
            Vector3 active = transform.TransformPoint(new Vector3(0f, 0.5f, side * ApproachDistance));
            Gizmos.DrawLine(transform.position, active);
            Gizmos.DrawSphere(active, 0.12f);

            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Vector3 inactive = transform.TransformPoint(new Vector3(0f, 0.5f, -side * ApproachDistance));
            Gizmos.DrawSphere(inactive, 0.12f);
        }
    }
}
