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

        private readonly List<UnitRuntime> _mounted = new List<UnitRuntime>(2);
        private readonly List<UnitRuntime> _pending = new List<UnitRuntime>(4);

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
            float offset = index % 2 == 0 ? -0.3f : 0.3f;
            Vector3 local = new Vector3(offset, topHeight, 0f);
            return transform.TransformPoint(local);
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
            Vector3 approach = transform.TransformPoint(new Vector3(0f, 0f, 1.4f));

            if (NavMesh.SamplePosition(approach, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                approach = hit.position;
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

            archer.Initialize(this, _mounted.Count - 1, runtime, movement);
        }
    }
}
