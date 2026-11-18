using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)]
    public sealed class PlayerTentacleAim : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float aimHoldSeconds = 0.25f;
        [SerializeField, Min(1f)] private float returnSpeed = 720f;

        private PlayerJellyVisual jellyVisual;
        private Vector2 aimDirection;
        private float aimUntil = float.NegativeInfinity;

        private void Awake()
        {
            jellyVisual = GetComponentInParent<PlayerJellyVisual>();
            if (jellyVisual == null)
            {
                enabled = false;
            }
        }

        private void OnDisable()
        {
            ClearAim();
        }

        public void AimAt(Vector2 worldPosition)
        {
            Vector2 direction = worldPosition - (Vector2)transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            aimDirection = direction;
            aimUntil = Time.time + aimHoldSeconds;
        }

        public void ClearAim()
        {
            aimUntil = float.NegativeInfinity;
            transform.localRotation = Quaternion.identity;
        }

        private void LateUpdate()
        {
            if (Time.time <= aimUntil && jellyVisual.TryGetExtractionAttachmentDirection(out Vector2 attachmentDirection))
            {
                // Turn the visible rig without changing the parent that sets swim heading.
                float angle = Vector2.SignedAngle(attachmentDirection, aimDirection);
                transform.Rotate(0f, 0f, angle, Space.World);
            }
            else if (transform.localRotation != Quaternion.identity)
            {
                transform.localRotation = Quaternion.RotateTowards(
                    transform.localRotation, Quaternion.identity, returnSpeed * Time.deltaTime);
            }
        }
    }
}
