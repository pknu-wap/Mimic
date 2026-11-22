using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)]
    public class PlayerTentacleStrike : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float windup = 0.28f;
        [SerializeField, Min(0.01f)] private float lash = 0.12f;
        [SerializeField, Min(0.01f)] private float retract = 0.45f;
        [SerializeField, Min(0f)] private float bodyLean = 0.22f;
        [SerializeField] private PlayerExtractionTentacleVisual extractionVisual;

        private const float ContactHold = 0.05f;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Quaternion lastAimRotation;
        private Vector2 target;
        private float startedAt;
        private bool playing;

        public float ImpactTime => windup + lash;
        public float RecoveryTime => ContactHold + retract;

        private void Awake()
        {
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
        }

        public bool Play(Vector2 point)
        {
            if (!isActiveAndEnabled || extractionVisual == null || !extractionVisual.TryGetAttachmentDirection(out _))
            {
                return false;
            }

            target = point;
            startedAt = Time.time;
            lastAimRotation = transform.localRotation;
            playing = true;
            return true;
        }

        public void Track(Vector2 point) => target = point;

        private void LateUpdate()
        {
            if (!playing)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            if (extractionVisual == null || elapsed >= ImpactTime + RecoveryTime)
            {
                Cancel();
                return;
            }

            float charge = Mathf.Clamp01(elapsed / windup);
            float cast = Mathf.Clamp01((elapsed - windup) / lash);
            float recovery = Mathf.Clamp01((elapsed - ImpactTime - ContactHold) / retract);
            float released = 1f - Mathf.SmoothStep(0f, 1f, recovery);
            float reach = cast * cast * released;
            float curl = Mathf.Lerp(-0.7f, 0.12f, cast) + recovery * 0.65f;

            Vector2 direction = target - (Vector2)transform.parent.TransformPoint(restPosition);
            float lean = (reach - charge * (1f - cast) * 0.4f) * bodyLean;
            transform.localPosition = restPosition + transform.parent.InverseTransformVector(direction.normalized * lean);
            if (recovery > 0f)
            {
                transform.localRotation = Quaternion.Slerp(lastAimRotation, restRotation, 1f - released);
            }
            else if (direction.sqrMagnitude > 0.0001f && extractionVisual.TryGetAttachmentDirection(out Vector2 attachment))
            {
                Quaternion currentRotation = transform.localRotation;
                float angle = Vector2.SignedAngle(transform.parent.InverseTransformVector(attachment),
                    transform.parent.InverseTransformVector(direction));
                Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward) * currentRotation;
                float turnTime = Mathf.Min(elapsed, Time.deltaTime);

                // Apply the change in windup progress to the turn that is still left.
                float previousCharge = Mathf.Clamp01((elapsed - turnTime) / windup);
                float previousProgress = Mathf.SmoothStep(0f, 1f, previousCharge);
                float progress = Mathf.SmoothStep(0f, 1f, charge);
                float aimStep = previousProgress < 1f
                    ? (progress - previousProgress) / (1f - previousProgress)
                    : 1f;
                float maxTurn = 180f * 1.5f * turnTime / windup;
                float turnStep = Mathf.Min(Mathf.Abs(angle) * aimStep, maxTurn);
                transform.localRotation = Quaternion.RotateTowards(currentRotation, targetRotation, turnStep);
                lastAimRotation = transform.localRotation;
            }

            extractionVisual.SetPose(target, reach, curl);
        }

        private void OnDisable() => Cancel();

        public void Cancel()
        {
            if (!playing)
            {
                return;
            }
            playing = false;
            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
            if (extractionVisual != null)
            {
                extractionVisual.SetPose(default, 0f, 0f);
            }
        }
    }
}
