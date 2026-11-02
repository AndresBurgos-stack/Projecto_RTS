using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectoRTS.Buildings
{
    /// <summary>
    /// Health for a placed building. The maximum is configured from the
    /// BuildingDefinition when the structure is spawned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingRuntime : MonoBehaviour
    {
        private static readonly List<BuildingRuntime> Buildings = new List<BuildingRuntime>(64);

        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;

        public event Action<BuildingRuntime> HealthChanged;
        public event Action<BuildingRuntime> Destroyed;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float HealthNormalized => maxHealth > 0f ? currentHealth / maxHealth : 0f;
        public bool IsAlive => currentHealth > 0f;

        /// <summary>All living and dead buildings registered in the scene.</summary>
        public static IReadOnlyList<BuildingRuntime> All => Buildings;

        private bool _destroyed;

        private void Awake()
        {
            if (currentHealth <= 0f)
            {
                currentHealth = maxHealth;
            }
        }

        private void OnEnable()
        {
            if (!Buildings.Contains(this))
            {
                Buildings.Add(this);
            }
        }

        private void OnDisable()
        {
            Buildings.Remove(this);
        }

        /// <summary>Sets the maximum health and refills the building.</summary>
        public void Configure(float health)
        {
            maxHealth = Mathf.Max(1f, health);
            currentHealth = maxHealth;
            _destroyed = false;
            HealthChanged?.Invoke(this);
        }

        /// <summary>Applies damage and destroys the building on the lethal hit.</summary>
        public void ApplyDamage(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            HealthChanged?.Invoke(this);

            if (currentHealth <= 0f && !_destroyed)
            {
                _destroyed = true;
                Destroyed?.Invoke(this);
                Destroy(gameObject);
            }
        }

        public void Repair()
        {
            currentHealth = maxHealth;
            _destroyed = false;
            HealthChanged?.Invoke(this);
        }
    }
}
