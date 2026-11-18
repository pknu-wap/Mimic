using MimicCell.CameraSystem;
using MimicCell.Gameplay;
using UnityEngine;

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    public sealed class PlayerDamageShakeEffect : MonoBehaviour
    {
        [SerializeField] private CameraFollow2D cameraFollow;

        private ActorHealth health;

        private void Start()
        {
            if (TryGetComponent(out health))
                health.DamageTaken += HandleDamageTaken;
        }

        private void OnDestroy()
        {
            if (health != null)
                health.DamageTaken -= HandleDamageTaken;
        }

        private void HandleDamageTaken(ActorHealth actor)
        {
            if (isActiveAndEnabled && cameraFollow != null)
                cameraFollow.PlayDamageShake();
        }
    }
}