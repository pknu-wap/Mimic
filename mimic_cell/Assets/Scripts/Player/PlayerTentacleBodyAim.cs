using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    public class PlayerTentacleBodyAim : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float bodyLean = 0.22f;
        [SerializeField] private Transform facingReference;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private Quaternion lastAimRotation;
        private Vector2 localFacingDirection;

        private void Awake()
        {
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            lastAimRotation = restRotation;
            // The body's forward direction stays the same whichever tentacle strikes.
            localFacingDirection = facingReference != null
                ? (Vector2)transform.InverseTransformPoint(facingReference.position)
                : Vector2.down;
        }

        internal void ApplyPose(Vector2 target, float elapsed, float windup,
            float cast, float reach, float recovery)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            Transform parent = transform.parent;
            Vector2 direction = target - (Vector2)parent.TransformPoint(restPosition);
            float charge = Mathf.Clamp01(elapsed / windup);
            float lean = (reach - charge * (1f - cast) * 0.4f) * bodyLean;
            transform.localPosition = restPosition + parent.InverseTransformVector(direction.normalized * lean);

            if (recovery > 0f)
            {
                transform.localRotation = Quaternion.Slerp(lastAimRotation, restRotation,
                    Mathf.SmoothStep(0f, 1f, recovery));
                return;
            }

            if (direction.sqrMagnitude <= 0.0001f || localFacingDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion currentRotation = transform.localRotation;
            Vector3 facing = transform.TransformVector(localFacingDirection);
            float angle = Vector2.SignedAngle(parent.InverseTransformVector(facing),
                parent.InverseTransformVector(direction));
            Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward) * currentRotation;
            float turnTime = Mathf.Min(elapsed, Time.deltaTime);

            // Apply the change in windup progress to the turn that is still left.
            float previousCharge = Mathf.Clamp01((elapsed - turnTime) / windup);
            float previousProgress = Mathf.SmoothStep(0f, 1f, previousCharge);
            float progress = Mathf.SmoothStep(0f, 1f, charge);
            float aimStep = previousProgress < 1f ? (progress - previousProgress) / (1f - previousProgress) : 1f;
            float maxTurn = 180f * 1.5f * turnTime / windup;
            float turnStep = Mathf.Min(Mathf.Abs(angle) * aimStep, maxTurn);
            transform.localRotation = Quaternion.RotateTowards(currentRotation, targetRotation, turnStep);
            lastAimRotation = transform.localRotation;
        }

        internal void Restore()
        {
            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
            lastAimRotation = restRotation;
        }

        private void OnDisable() => Restore();
    }
}
