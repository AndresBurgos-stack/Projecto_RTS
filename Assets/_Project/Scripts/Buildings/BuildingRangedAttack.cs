using System.Collections;
using ProjectoRTS.Units;
using UnityEngine;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Lets a structure shoot at nearby enemies like archers on a tower or
    /// wall. Each shot has a short wind-up so the archer appears to aim
    /// before the arrow flies; damage is applied when the shot is released
    /// and a small arrow flies to the target for feedback. Towers and walls
    /// configure their own damage, range and cadence from the demo scene
    /// builder.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingRangedAttack : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float attackDamage = 10f;
        [SerializeField, Min(0.5f)] private float attackRange = 9f;
        [SerializeField, Min(0.05f)] private float attackInterval = 1.2f;
        [SerializeField, Min(0.5f)] private float muzzleHeight = 2.4f;
        [SerializeField, Min(0.05f)] private float flightTime = 0.22f;
        [SerializeField, Min(0.05f)] private float windUpTime = 0.5f;

        private float _nextFireTime;
        private Material _arrowMaterial;
        private BuildingRuntime _runtime;
        private ConstructionSite _site;

        private void Awake()
        {
            _runtime = GetComponent<BuildingRuntime>();
            _site = GetComponent<ConstructionSite>();
        }

        private void Update()
        {
            if (_runtime == null)
            {
                _runtime = GetComponent<BuildingRuntime>();
            }

            if (_site == null)
            {
                _site = GetComponent<ConstructionSite>();
            }

            if (_runtime != null && !_runtime.IsAlive)
            {
                return;
            }

            if (_site != null && !_site.IsComplete)
            {
                return;
            }

            if (Time.time < _nextFireTime)
            {
                return;
            }

            UnitRuntime target = UnitRegistry.FindNearestEnemy(
                transform.position,
                Faction.Greek,
                attackRange);

            if (target == null)
            {
                return;
            }

            _nextFireTime = Time.time + attackInterval;
            Fire(target);
        }

        private void Fire(UnitRuntime target)
        {
            StartCoroutine(ShootAfterAim(target));
        }

        /// <summary>
        /// Holds the shot briefly so the archer appears to aim before the
        /// arrow flies, mirroring the unit archers' draw-then-release timing.
        /// The target is validated again after the wind-up because it may die
        /// or leave while the archer is aiming.
        /// </summary>
        private IEnumerator ShootAfterAim(UnitRuntime target)
        {
            yield return new WaitForSeconds(windUpTime);

            if (target == null || !target.IsAlive)
            {
                yield break;
            }

            target.ApplyDamage(attackDamage);
            StartCoroutine(FlyArrow(target));
        }

        private IEnumerator FlyArrow(UnitRuntime target)
        {
            if (target == null)
            {
                yield break;
            }

            GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrow.name = "Arrow";

            Collider collider = arrow.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            MeshRenderer renderer = arrow.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = ResolveArrowMaterial();
            }

            Vector3 start = transform.position + Vector3.up * muzzleHeight;
            Vector3 end = target.transform.position + Vector3.up * 0.8f;

            arrow.transform.position = start;
            arrow.transform.localScale = new Vector3(0.06f, 0.06f, 0.5f);

            float elapsed = 0f;
            while (elapsed < flightTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, flightTime));

                Vector3 tip = Vector3.Lerp(start, end, t);
                arrow.transform.position = tip;

                Vector3 forward = end - start;
                forward.y = 0f;

                if (forward.sqrMagnitude > 0.0001f)
                {
                    arrow.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                }

                yield return null;
            }

            Destroy(arrow);
        }

        private Material ResolveArrowMaterial()
        {
            if (_arrowMaterial != null)
            {
                return _arrowMaterial;
            }

            Renderer own = GetComponentInChildren<Renderer>(true);
            if (own != null && own.sharedMaterial != null)
            {
                _arrowMaterial = own.sharedMaterial;
                return _arrowMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            _arrowMaterial = new Material(shader);
            _arrowMaterial.color = new Color(0.32f, 0.22f, 0.12f, 1f);
            return _arrowMaterial;
        }
    }
}
