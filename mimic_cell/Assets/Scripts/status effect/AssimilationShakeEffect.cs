using MimicCell.CameraSystem;
using UnityEngine;

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StatusEffectController))]
    public sealed class AssimilationShakeEffect : MonoBehaviour
    {
        [SerializeField] private CameraFollow2D cameraFollow;
        [SerializeField] private StatusEffectDefinition tunnelVision;
        [SerializeField] private StatusEffectDefinition berserk;
        [SerializeField] private StatusEffectDefinition panic;
        [SerializeField, Min(0.3f)] private float beatInterval = 0.85f;

        private StatusEffectController statusController;

        private float timeUntilNextBeat;

        private void Awake()
        {
            statusController = GetComponent<StatusEffectController>();
        }

       private void Update()
{
    bool hasAssimilationEffect =
        statusController.HasEffect(tunnelVision) ||
        statusController.HasEffect(berserk) ||
        statusController.HasEffect(panic);

    if (!hasAssimilationEffect || cameraFollow == null)
    {
        timeUntilNextBeat = 0f;
        return;
    }

    timeUntilNextBeat -= Time.deltaTime;
    if (timeUntilNextBeat > 0f)
        return;

    cameraFollow.PlayPressureShake();
    timeUntilNextBeat = beatInterval;
}
    }
}
