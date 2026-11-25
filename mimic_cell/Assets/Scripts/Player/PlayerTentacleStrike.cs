using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerTentacleBodyAim))]
    [DefaultExecutionOrder(-10)]
    public class PlayerTentacleStrike : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float windup = 0.28f;
        [SerializeField, Min(0.01f)] private float lash = 0.12f;
        [SerializeField, Min(0.01f)] private float retract = 0.45f;
        [SerializeField] private PlayerExtractionTentacleVisual extractionVisual;

        private const float ContactHold = 0.05f;
        private PlayerTentacleBodyAim bodyAim;
        private Vector2 target;
        private float startedAt;
        private bool playing;

        public float ImpactTime => windup + lash;
        public float RecoveryTime => ContactHold + retract;

        private void Awake() => bodyAim = GetComponent<PlayerTentacleBodyAim>();

        public bool Play(Vector2 point)
        {
            if (!isActiveAndEnabled || extractionVisual == null || !extractionVisual.TryGetAttachmentDirection(out _))
            {
                return false;
            }

            target = point;
            startedAt = Time.time;
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

            float cast = Mathf.Clamp01((elapsed - windup) / lash);
            float recovery = Mathf.Clamp01((elapsed - ImpactTime - ContactHold) / retract);
            float released = 1f - Mathf.SmoothStep(0f, 1f, recovery);
            float reach = cast * cast * released;
            float curl = Mathf.Lerp(-0.7f, 0.12f, cast) + recovery * 0.65f;

            bodyAim.ApplyPose(target, elapsed, windup, cast, reach, recovery);

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
            bodyAim.Restore();
            if (extractionVisual != null)
            {
                extractionVisual.SetPose(default, 0f, 0f);
            }
        }
    }
}
