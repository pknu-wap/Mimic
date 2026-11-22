using MimicCell.Gameplay;
using UnityEngine;

namespace MimicCell.Creatures
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorHealth))]
    public sealed class CreatureCombatTraitController : MonoBehaviour
    {
        private ActorHealth actorHealth;
        private CreatureTraits traits;
        private int reflectionDamage;

        private void Awake()
        {
            actorHealth = GetComponent<ActorHealth>();

            actorHealth.DamageApplied += HandleDamageApplied;
        }

        private void OnDestroy()
        {
            if (actorHealth != null)
            {
                actorHealth.DamageApplied -= HandleDamageApplied;
            }
        }

        public void Configure(CreatureDefinition definition, float normalizedVariation)
        {
            if (definition == null)
            {
                Clear();
                return;
            }

            traits = definition.Traits;

            reflectionDamage = definition.ReflectionDamageRange.Evaluate(normalizedVariation);
        }

        public void Clear()
        {
            traits = CreatureTraits.None;
            reflectionDamage = 0;
        }

        private void HandleDamageApplied(
            ActorHealth damagedHealth,
            ActorDamageContext context,
            int appliedDamage)
        {
            if (damagedHealth != actorHealth ||
                appliedDamage <= 0 ||
                reflectionDamage <= 0 ||
                !traits.HasTrait(
                    CreatureTraits.Spiked) ||
                !context.CanReflect ||
                context.Source == null ||
                context.Source == actorHealth ||
                !context.Source.IsAlive)
            {
                return;
            }

            ActorDamageContext reflectionContext =
                new ActorDamageContext(
                    reflectionDamage,
                    actorHealth,
                    ActorDamageType.Reflection);

            if (context.Source.TakeDamage(reflectionContext))
            {
                Debug.Log(
                    "가시형 반사 피해 발동" +
                    " / 반사 피해: " +
                    reflectionDamage,
                    this);
            }
        }
    }
}