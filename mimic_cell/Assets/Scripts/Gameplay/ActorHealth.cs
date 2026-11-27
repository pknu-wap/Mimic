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
        public event Action<ActorHealth, int> Damaged;
        public event Action<ActorHealth> Died;
        public event Action<ActorHealth> DamageTaken;
        public event Action<ActorHealth, ActorDamageContext, int> DamageApplied;

private readonly System.Collections.Generic.HashSet<UnityEngine.Object>
    deathProtectionSources =
        new System.Collections.Generic.HashSet<UnityEngine.Object>();

public void SetDeathProtection(UnityEngine.Object source, bool active)
{
    if (source == null)
        return;

    if (active)
        deathProtectionSources.Add(source);
    else
        deathProtectionSources.Remove(source);
}

private readonly System.Collections.Generic.HashSet<UnityEngine.Object>
    damageImmunitySources =
        new System.Collections.Generic.HashSet<UnityEngine.Object>();

public void SetDamageImmunity(UnityEngine.Object source, bool active)
{
    if (source == null)
        return;

    if (active)
        damageImmunitySources.Add(source);
    else
        damageImmunitySources.Remove(source);
}

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
            ActorDamageContext context =
                new ActorDamageContext(amount, null, ActorDamageType.Unknown);

            return TakeDamage(context);
        }

        public bool TakeDamage(ActorDamageContext context)
        {
            int amount = context.Amount;

if (amount <= 0 || !IsAlive || damageImmunitySources.Count > 0)            {
                return false;
            }

            int previousHealth = currentHealth;

            int minimumHealth = deathProtectionSources.Count > 0 ? 1 : 0;

currentHealth = Mathf.Max(minimumHealth, currentHealth - amount);

int appliedDamage = previousHealth - currentHealth;

if (appliedDamage <= 0)
    return false;

            HealthChanged?.Invoke(this);
            DamageTaken?.Invoke(this);
            Damaged?.Invoke(this, appliedDamage);

            DamageApplied?.Invoke(this, context, appliedDamage);

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
