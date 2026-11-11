using ProjectoRTS.Units;
using UnityEngine;

namespace ProjectoRTS.Combat
{
    /// <summary>
    /// Visible arrow that flies from a shooter to a unit target and applies
    /// damage on impact. The arrow always despawns: on hit, when the target
    /// dies mid-flight, or after a maximum lifetime, so arrows never
    /// accumulate in the scene.
    /// </summary>
    public sealed class ArrowProjectile : MonoBehaviour
    {
        private const float HitDistance = 0.35f;
        private const float MaxLifetime = 3f;
        private const float TargetHeight = 0.8f;
        private const float DefaultSpeed = 15f;

        private static Material _arrowMaterial;

        private UnitRuntime _target;
        private float _damage;
        private float _speed = DefaultSpeed;
        private float _spawnTime;
        private Vector3 _lastTargetPoint;

        /// <summary>Spawns an arrow that damages the target on impact.</summary>
        public static void Spawn(Vector3 origin, UnitRuntime target, float damage, float speed = DefaultSpeed)
        {
            if (target == null || !target.IsAlive || damage <= 0f)
            {
                return;
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

            arrow.transform.position = origin;
            arrow.transform.localScale = new Vector3(0.05f, 0.05f, 0.6f);

            Vector3 toTarget = TargetPoint(target) - origin;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                arrow.transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            }

            ArrowProjectile projectile = arrow.AddComponent<ArrowProjectile>();
            projectile._target = target;
            projectile._damage = damage;
            projectile._speed = Mathf.Max(1f, speed);
            projectile._spawnTime = Time.time;
            projectile._lastTargetPoint = TargetPoint(target);
        }

        private void Update()
        {
            Vector3 targetPoint = _target != null && _target.IsAlive
                ? TargetPoint(_target)
                : _lastTargetPoint;

            Vector3 toTarget = targetPoint - transform.position;
            float step = _speed * Time.deltaTime;

            if (toTarget.sqrMagnitude <= step * step ||
                toTarget.sqrMagnitude <= HitDistance * HitDistance)
            {
                Arrive();
                return;
            }

            _lastTargetPoint = targetPoint;

            if (toTarget.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            }

            transform.position += toTarget.normalized * step;

            if (Time.time - _spawnTime > MaxLifetime)
            {
                Destroy(gameObject);
            }
        }

        private void Arrive()
        {
            if (_target != null && _target.IsAlive)
            {
                _target.ApplyDamage(_damage, false);
            }

            Destroy(gameObject);
        }

        private static Vector3 TargetPoint(UnitRuntime target)
        {
            return target.transform.position + Vector3.up * TargetHeight;
        }

        private static Material ResolveArrowMaterial()
        {
            if (_arrowMaterial != null)
            {
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
