using UnityEngine;

namespace MimicCell.Creatures
{
    [DisallowMultipleComponent]
    public sealed class CreaturePerceptionTraitController : MonoBehaviour
    {
        private CreatureTraits traits;
        private CreaturePerceptionSettings settings;

        public void Configure(CreatureTraits newTraits, CreaturePerceptionSettings newSettings)
        {
            traits = newTraits;
            settings = newSettings;
        }

        public float AdjustPerceptionRadius(float baseRadius)
        {
            float radius = Mathf.Max(0f, baseRadius);

            if (settings == null || !traits.HasTrait(CreatureTraits.Sensing))
            {
                return radius;
            }

            return radius * Mathf.Max(1f, settings.SensingRadiusMultiplier);
        }

        public float GetBehaviorMemoryDuration(CreatureFoodTier foodTier, CreatureState state)
        {
            if (settings == null)
            {
                return 0f;
            }

            float duration = 0f;

            switch (foodTier)
            {
                case CreatureFoodTier.Tier1:
                    if (state == CreatureState.Flee)
                    {
                        duration = settings.Tier1FleeDuration;
                    }
                    break;

                case CreatureFoodTier.Tier2:
                    duration =
                        state == CreatureState.Chase
                            ? settings.Tier2ChaseDuration
                            : state == CreatureState.Flee
                                ? settings.Tier2FleeDuration
                                : 0f;
                    break;

                case CreatureFoodTier.Tier3:
                    duration =
                        state == CreatureState.Chase
                            ? settings.Tier3ChaseDuration
                            : state == CreatureState.Flee
                                ? settings.Tier3FleeDuration
                                : 0f;
                    break;
            }

            if (traits.HasTrait(CreatureTraits.Sensing))
            {
                duration *= Mathf.Max(1f, settings.SensingDurationMultiplier);
            }

            return Mathf.Max(0f, duration);
        }
    }
}