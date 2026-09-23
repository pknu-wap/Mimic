using System;
using UnityEngine;

namespace MimicCell.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ActorHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 10;
        [SerializeField, Min(0)] private int currentHealth = 10;

        public int MaxHealth
        {
            get { return maxHealth; }
        }

        public int CurrentHealth
        {
            get { return currentHealth; }
        }

        public bool IsAlive
        {
            get { return currentHealth > 0; }
        }

        public event Action<ActorHealth> HealthChanged;
        public event Action<ActorHealth> Died;

        private void Awake()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 1, maxHealth);
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }

        public void Configure(int newMaxHealth, bool restoreToFull = true)
        {
            maxHealth = Mathf.Max(1, newMaxHealth);
            currentHealth = restoreToFull
                ? maxHealth
                : Mathf.Clamp(currentHealth, 0, maxHealth);
            HealthChanged?.Invoke(this);
        }

        public bool TakeDamage(int amount)
        {
            if (amount <= 0 || !IsAlive)
            {
                return false;
            }

            int previousHealth = currentHealth;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            HealthChanged?.Invoke(this);

            if (previousHealth > 0 && currentHealth == 0)
            {
                Died?.Invoke(this);
            }

            return true;
        }

        public bool Heal(int amount)
        {
            if (amount <= 0 || !IsAlive || currentHealth >= maxHealth)
            {
                return false;
            }

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            HealthChanged?.Invoke(this);
            return true;
        }

        public void RestoreFullHealth()
        {
            currentHealth = maxHealth;
            HealthChanged?.Invoke(this);
        }
    }
}
