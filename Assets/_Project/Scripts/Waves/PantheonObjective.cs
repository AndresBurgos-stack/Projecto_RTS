using System;
using UnityEngine;

namespace ProjectoRTS.Waves
{
    /// <summary>
    /// Central objective the player must protect. When its health reaches
    /// zero the run is lost.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PantheonObjective : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 2000f;
        [SerializeField] private float currentHealth;

        public event Action<PantheonObjective> HealthChanged;
        public event Action<PantheonObjective> Destroyed;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float HealthNormalized => maxHealth > 0f ? currentHealth / maxHealth : 0f;
        public bool IsAlive => currentHealth > 0f;

        private bool _destroyed;

        private void Awake()
        {
            if (currentHealth <= 0f)
            {
                currentHealth = maxHealth;
            }
        }

        /// <summary>Applies damage and raises Destroyed on the first lethal hit.</summary>
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