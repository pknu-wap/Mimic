using UnityEngine;
using MimicCell.Creatures;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(5)]
    public sealed class ExtractionRigController : MonoBehaviour
    {
        [SerializeField] private Transform baseVisual;
        [SerializeField] private PrototypePlayer player;
        [SerializeField] private PlayerTentacleStrike strike;
        [SerializeField] private SpriteRenderer tentacleSprite;
        [SerializeField, Min(0.1f)] private float smallTentacleScale = 1.5f;
[SerializeField, Min(0.1f)] private float mediumTentacleScale = 2f;
[SerializeField, Min(0.1f)] private float largeTentacleScale = 3f;

        private void Update()
{
    if (baseVisual == null)
        return;

    transform.localPosition = baseVisual.localPosition;
    transform.localRotation = baseVisual.localRotation;

    float multiplier = 1f;

    if (player != null &&
        player.IsCreatureForm &&
        player.CurrentCreatureDefinition != null)
    {
        switch (player.CurrentCreatureDefinition.BodySize)
        {
            case CreatureBodySize.Small:
                multiplier = smallTentacleScale;
                break;

            case CreatureBodySize.Medium:
                multiplier = mediumTentacleScale;
                break;

            case CreatureBodySize.Large:
                multiplier = largeTentacleScale;
                break;
        }
    }

    transform.localScale = baseVisual.localScale * multiplier;
}

        private void LateUpdate()
        {
            if (player == null || strike == null || tentacleSprite == null)
                return;

            tentacleSprite.enabled =
                !player.IsCreatureForm || strike.IsPlaying;
        }
    }
}
