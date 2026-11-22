using UnityEngine;

namespace MimicCell.Creatures
{
    [DisallowMultipleComponent]
    public sealed class CreatureMovementTraitController : MonoBehaviour
    {
        private CreatureTraits traits;
        private CreatureHabitatMask allowedHabitats;
        private ICreatureEnvironment environment;

        public void Configure(CreatureTraits newTraits, CreatureHabitatMask newAllowedHabitats, ICreatureEnvironment newEnvironment)
        {
            traits = newTraits;
            allowedHabitats = newAllowedHabitats;
            environment = newEnvironment;
        }

        public Vector2 AdjustDirection(Vector2 direction, Vector2 position, float bodyHalfHeight)
        {
            if (environment == null || !traits.HasTrait(CreatureTraits.Benthic))
            {
                return direction;
            }

            if (!environment.TryGetHabitatBottomY(allowedHabitats, position.x, out float habitatBottomY))
            {
                return direction;
            }

            float targetY = habitatBottomY + Mathf.Max(0.1f, bodyHalfHeight) + CreatureTraitSettings.BenthicClearance;

            float heightError = targetY - position.y;

            float verticalDirection = Mathf.Abs(heightError) <= CreatureTraitSettings.BenthicHeightTolerance
                    ? 0f
                    : Mathf.Clamp(heightError * CreatureTraitSettings.BenthicCorrectionStrength,
                        -1f,
                        1f);

            Vector2 adjustedDirection = new Vector2(direction.x, verticalDirection);

            return adjustedDirection.sqrMagnitude > 1f
                ? adjustedDirection.normalized
                : adjustedDirection;
        }
    }
}