using System;
using System.Collections;
using System.Collections.Generic;
using ProjectoRTS.Combat;
using ProjectoRTS.Units;
using UnityEngine;

namespace ProjectoRTS.Waves
{
    /// <summary>
    /// Spawns a Roman squad from a single spawn point and sends the whole
    /// group toward the Pantheon. A ground marker warns the player a few
    /// seconds before the squad appears.
    /// The squad advances as a group: every unit gets a MoveTo to the
    /// destination at spawn time and the spawner refreshes that order while
    /// the unit has no combat target. The AI stays enabled so the Romans
    /// attack anything in their path (houses, walls, towers, hoplites) and
    /// resume the march once the target dies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RomanSpawner : MonoBehaviour
    {
        [Serializable]
        public sealed class SquadEntry
        {
            public UnitDefinition unit;
            [Min(1)] public int count = 3;
            [Min(0f)] public float delay = 0f;
        }

        [Header("Squad")]
        [SerializeField] private List<SquadEntry> squad = new List<SquadEntry>();
        [SerializeField] private Transform unitParent;

        [Header("Spawn")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField, Min(0f)] private float warningTime = 4f;
        [SerializeField, Min(0f)] private float spawnStagger = 0.15f;
        [SerializeField, Min(0.5f)] private float formationSpacing = 1.5f;

        [Header("Advance")]
        [SerializeField] private Transform advanceTarget;
        [SerializeField, Min(0.1f)] private float repathInterval = 0.5f;

        [Header("Marker")]
        [SerializeField] private Color markerColor = new Color(0.85f, 0.2f, 0.15f, 0.6f);
        [SerializeField, Min(0.5f)] private float markerRadius = 2.5f;

        [Header("Flow")]
        [SerializeField] private bool autoStart = false;

        /// <summary>Raised when the squad has finished its march (all marchers gone).</summary>
        public event Action SquadArrived;

        private readonly List<UnitRuntime> _alive = new List<UnitRuntime>(16);
        private readonly List<AdvancingUnit> _advancing = new List<AdvancingUnit>(16);
        private Coroutine _sequence;
        private bool _running;
        private bool _arrivedRaised;
        private float _nextRepathTime;
        private Vector3 _destination;

        public int AliveCount => _alive.Count;
        public Transform SpawnPoint => spawnPoint;
        public bool IsRunning => _running;

        private sealed class AdvancingUnit
        {
            public UnitRuntime runtime;
            public UnitMovement movement;
            public UnitCombat combat;
            public EnemyAiController ai;
        }

        private void Start()
        {
            if (autoStart)
            {
                StartSpawning();
            }
        }

        private void OnDisable()
        {
            if (_sequence != null)
            {
                StopCoroutine(_sequence);
                _sequence = null;
            }
        }

        /// <summary>
        /// Fills the squad from the wave definition and starts the sequence.
        /// </summary>
        public void BeginWave(WaveDefinition wave)
        {
            if (wave == null || wave.Groups == null)
            {
                return;
            }

            squad.Clear();
            for (int i = 0; i < wave.Groups.Count; i++)
            {
                WaveGroup group = wave.Groups[i];
                if (group == null || group.unit == null || group.count <= 0)
                {
                    continue;
                }

                squad.Add(new SquadEntry
                {
                    unit = group.unit,
                    count = group.count,
                    delay = 0f
                });
            }

            StartSpawning();
        }

        public void StartSpawning()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            _arrivedRaised = false;
            _sequence = StartCoroutine(RunSequence());
        }

        public void StopSpawning()
        {
            _running = false;
            if (_sequence != null)
            {
                StopCoroutine(_sequence);
                _sequence = null;
            }
        }

        private void Update()
        {
            PruneDead();

            if (_advancing.Count == 0)
            {
                if (_running || _arrivedRaised)
                {
                    return;
                }

                if (AliveCount == 0 && _sequence == null)
                {
                    RaiseArrived();
                }

                return;
            }

            if (Time.time < _nextRepathTime)
            {
                return;
            }

            _nextRepathTime = Time.time + Mathf.Max(0.1f, repathInterval);

            for (int i = 0; i < _advancing.Count; i++)
            {
                AdvancingUnit entry = _advancing[i];
                if (entry == null || entry.movement == null)
                {
                    continue;
                }

                bool inCombat = entry.combat != null && entry.combat.HasTarget;
                if (!inCombat)
                {
                    if (entry.ai != null && entry.runtime != null)
                    {
                        entry.ai.SetHome(entry.runtime.transform.position, 5f);
                    }

                    entry.movement.MoveTo(_destination);
                }
            }
        }

        private void PruneDead()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                UnitRuntime runtime = _alive[i];
                if (runtime == null || !runtime.IsAlive)
                {
                    _alive.RemoveAt(i);
                }
            }

            for (int i = _advancing.Count - 1; i >= 0; i--)
            {
                AdvancingUnit entry = _advancing[i];
                if (entry == null || entry.movement == null || !entry.movement.IsAlive)
                {
                    _advancing.RemoveAt(i);
                }
            }
        }

        private void RaiseArrived()
        {
            _arrivedRaised = true;
            SquadArrived?.Invoke();
        }

        private IEnumerator RunSequence()
        {
            Transform point = ResolveSpawnPoint();
            if (point == null)
            {
                _running = false;
                _sequence = null;
                yield break;
            }

            Vector3 origin = SamplePosition(point.position);
            GameObject marker = CreateMarker(origin);

            float elapsed = 0f;
            while (elapsed < warningTime)
            {
                elapsed += Time.deltaTime;
                PulseMarker(marker, elapsed, warningTime);
                yield return null;
            }

            if (marker != null)
            {
                Destroy(marker);
            }

            Vector3 destination = ResolveDestination();
            _destination = destination;
            Vector3 forward = destination - origin;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = point.forward;
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            int index = 0;
            int total = CountUnits();
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(total)));

            if (squad != null)
            {
                for (int i = 0; i < squad.Count; i++)
                {
                    SquadEntry entry = squad[i];
                    if (entry == null || entry.unit == null || entry.count <= 0)
                    {
                        continue;
                    }

                    if (entry.delay > 0f)
                    {
                        yield return new WaitForSeconds(entry.delay);
                    }

                    for (int n = 0; n < entry.count; n++)
                    {
                        int row = index / columns;
                        int column = index % columns;
                        float offsetX = (column - (columns - 1) * 0.5f) * formationSpacing;
                        float offsetZ = -row * formationSpacing;
                        Vector3 slot = origin + right * offsetX + forward * offsetZ;

                        SpawnUnit(entry.unit, SamplePosition(slot), point.rotation, destination);
                        index++;

                        if (spawnStagger > 0f && index < total)
                        {
                            yield return new WaitForSeconds(spawnStagger);
                        }
                    }
                }
            }

            _nextRepathTime = Time.time;
            _sequence = null;
            _running = false;

            if (_advancing.Count == 0 && AliveCount == 0)
            {
                RaiseArrived();
            }
        }

        private void SpawnUnit(UnitDefinition unit, Vector3 position, Quaternion rotation, Vector3 destination)
        {
            if (unit == null || unit.Prefab == null)
            {
                return;
            }

            GameObject instance = Instantiate(unit.Prefab, position, rotation, unitParent);
            instance.name = unit.DisplayName;

            UnitRuntime runtime = instance.GetComponent<UnitRuntime>();
            if (runtime != null)
            {
                runtime.Initialize(unit);
                _alive.Add(runtime);
            }

            UnitMovement movement = instance.GetComponent<UnitMovement>();
            if (movement != null)
            {
                movement.Initialize(unit);
            }

            UnitCombat combat = instance.GetComponent<UnitCombat>();
            if (combat != null)
            {
                combat.Initialize(unit);
            }

            EnemyAiController ai = instance.GetComponent<EnemyAiController>();
            if (ai == null)
            {
                ai = instance.AddComponent<EnemyAiController>();
            }

            ai.SetHome(position, 5f);
            ai.SetPatrolEnabled(false);
            ai.enabled = true;

            if (movement != null)
            {
                movement.MoveTo(destination);
                _advancing.Add(new AdvancingUnit
                {
                    runtime = runtime,
                    movement = movement,
                    combat = combat,
                    ai = ai
                });
            }
        }

        private Transform ResolveSpawnPoint()
        {
            if (spawnPoint != null)
            {
                return spawnPoint;
            }

            return transform;
        }

        private Vector3 ResolveDestination()
        {
            if (advanceTarget != null)
            {
                return advanceTarget.position;
            }

            PantheonObjective objective = FindFirstObjectByType<PantheonObjective>();
            if (objective != null)
            {
                return objective.transform.position;
            }

            return Vector3.zero;
        }

        private int CountUnits()
        {
            int total = 0;
            if (squad == null)
            {
                return total;
            }

            for (int i = 0; i < squad.Count; i++)
            {
                if (squad[i] != null && squad[i].unit != null && squad[i].count > 0)
                {
                    total += squad[i].count;
                }
            }

            return total;
        }

        private static Vector3 SamplePosition(Vector3 position)
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(
                    position,
                    out UnityEngine.AI.NavMeshHit hit,
                    3f,
                    UnityEngine.AI.NavMesh.AllAreas))
            {
                return hit.position;
            }

            return position;
        }

        private GameObject CreateMarker(Vector3 position)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Spawn Warning";
            marker.transform.position = position + Vector3.up * 0.05f;
            marker.transform.localScale = new Vector3(
                markerRadius * 2f,
                0.05f,
                markerRadius * 2f);

            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateMarkerMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return marker;
        }

        private void PulseMarker(GameObject marker, float elapsed, float duration)
        {
            if (marker == null)
            {
                return;
            }

            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 8f);
            marker.transform.localScale = new Vector3(
                markerRadius * 2f * (0.85f + 0.15f * pulse),
                0.05f,
                markerRadius * 2f * (0.85f + 0.15f * pulse));

            MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Color color = markerColor;
                color.a = Mathf.Lerp(0.25f, markerColor.a, pulse) * (1f - t * 0.35f);
                renderer.material.color = color;
            }
        }

        private Material CreateMarkerMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new Material(shader != null ? shader : Shader.Find("Standard"));
            material.name = "Spawn Warning Marker";
            material.color = markerColor;
            return material;
        }
    }
}