using System;
using System.Collections;
using ProjectoRTS.Buildings;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectoRTS.Units
{
    /// <summary>
    /// Runtime state for a unit. Stats come from its
    /// <see cref="UnitDefinition"/>; health changes are broadcast so health
    /// bars, damage flashes and the selection controller can react.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitRuntime : MonoBehaviour
    {
        [SerializeField] private UnitDefinition definition;
        [SerializeField] private float currentHealth;
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField, Min(0f)] private float corpseDuration = 1.1f;

        private bool _registered;
        private bool _died;

        public event Action<UnitRuntime> HealthChanged;
        public event Action<UnitRuntime, float> Damaged;
        public event Action<UnitRuntime> Died;

        /// <summary>
        /// Raised when another unit damages this one, with the attacker.
        /// Lets AI controllers retaliate instead of standing still.
        /// </summary>
        public event Action<UnitRuntime, UnitRuntime> HitByEnemy;

        public UnitDefinition Definition => definition;
        public Faction Faction => definition != null ? definition.Faction : Faction.None;
        public UnitRole Role => definition != null ? definition.Role : UnitRole.Infantry;
        public UnitMovement Movement { get; private set; }
        public float CurrentHealth => currentHealth;
        public float MaxHealth => definition != null ? definition.MaxHealth : 0f;
        public float HealthNormalized => MaxHealth > 0f ? currentHealth / MaxHealth : 0f;
        public bool IsAlive => currentHealth > 0f;

        /// <summary>True while the unit stands on top of a wall segment.</summary>
        public bool IsMounted { get; private set; }

        /// <summary>
        /// Wall building that hosts this unit while mounted, or null.
        /// Used by melee attackers to retarget the wall instead of the archer.
        /// </summary>
        public BuildingRuntime MountedWallBuilding { get; private set; }

        private void Awake()
        {
            Movement = GetComponent<UnitMovement>();

            if (definition != null && currentHealth <= 0f)
            {
                currentHealth = definition.MaxHealth;
            }

            Register();
        }

        private void OnEnable()
        {
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnDestroy()
        {
            Unregister();
        }

        /// <summary>Assigns the unit data and refills its health.</summary>
        public void Initialize(UnitDefinition unitDefinition)
        {
            definition = unitDefinition;
            currentHealth = unitDefinition != null ? unitDefinition.MaxHealth : 0f;
            _died = false;
            HealthChanged?.Invoke(this);
        }

        /// <summary>Applies damage after the unit's damage reduction.</summary>
        public void ApplyDamage(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            float reduction = definition != null ? definition.DamageReduction : 0f;
            float applied = amount * (1f - reduction);
            currentHealth = Mathf.Max(0f, currentHealth - applied);

            Damaged?.Invoke(this, applied);
            HealthChanged?.Invoke(this);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// Applies damage from a melee or ranged attacker. Melee attacks are
        /// ignored while the unit is mounted on a wall: only ranged attackers
        /// can hurt it until it dismounts.
        /// </summary>
        public void ApplyDamage(float amount, bool isMelee, UnitRuntime attacker = null)
        {
            if (IsMounted && isMelee)
            {
                return;
            }

            ApplyDamage(amount);

            if (attacker != null && IsAlive)
            {
                HitByEnemy?.Invoke(this, attacker);
            }
        }

        /// <summary>Sets or clears the wall-mounted state for melee immunity.</summary>
        public void SetMounted(bool mounted, BuildingRuntime wallBuilding = null)
        {
            IsMounted = mounted;
            MountedWallBuilding = mounted ? wallBuilding : null;
        }

        private void Register()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;
            UnitRegistry.Register(this);
        }

        private void Unregister()
        {
            if (!_registered)
            {
                return;
            }

            _registered = false;
            UnitRegistry.Unregister(this);
        }

        private void Die()
        {
            if (_died)
            {
                return;
            }

            _died = true;
            Died?.Invoke(this);

            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null && HasAnimatorParameter(animator, "Dead"))
            {
                animator.SetTrigger("Dead");
            }

            if (destroyOnDeath)
            {
                StartCoroutine(DeathRoutine());
            }
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

        private IEnumerator DeathRoutine()
        {
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }

                agent.enabled = false;
            }

            foreach (Collider collider in GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }

            Vector3 startScale = transform.localScale;
            Vector3 startPosition = transform.position;
            float elapsed = 0f;

            while (elapsed < corpseDuration)
            {
                elapsed += Time.deltaTime;
                float t = corpseDuration > 0f ? Mathf.Clamp01(elapsed / corpseDuration) : 1f;
                transform.localScale = Vector3.Lerp(startScale, startScale * 0.35f, t);
                transform.position = startPosition + Vector3.down * (t * 0.6f);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
