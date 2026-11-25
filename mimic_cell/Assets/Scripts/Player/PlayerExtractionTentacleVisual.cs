using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(LineRenderer))]
    [DefaultExecutionOrder(20)]
    public class PlayerExtractionTentacleVisual : MonoBehaviour
    {
        [SerializeField] private PlayerJellyLeg rope;

        private readonly Vector3[] points = new Vector3[25];
        private SpriteRenderer restingSprite;
        private LineRenderer line;
        private float restingAlpha;
        private Color lineStartTint;
        private Color lineEndTint;
        private Vector2 worldTip;
        private float reach;
        private float curl;

        private void Awake()
        {
            restingSprite = GetComponent<SpriteRenderer>();
            line = GetComponent<LineRenderer>();
            restingAlpha = restingSprite.color.a;
            lineStartTint = line.startColor;
            lineEndTint = line.endColor;
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            ResetPose();
        }

        public bool TryGetAttachmentDirection(out Vector2 direction)
        {
            direction = default;
            if (!isActiveAndEnabled || rope == null || !rope.isActiveAndEnabled)
            {
                return false;
            }

            direction = rope.GetJointPosition(0) - (Vector2)transform.parent.position;
            return direction.sqrMagnitude > 0.0001f;
        }

        public void SetPose(Vector2 worldTip, float reach, float curl)
        {
            this.worldTip = worldTip;
            this.reach = reach;
            this.curl = curl;
            if (reach <= 0f)
            {
                ResetPose();
            }
        }

        private void LateUpdate()
        {
            if (reach <= 0f || rope == null)
            {
                return;
            }

            Vector2 root = transform.InverseTransformPoint(rope.GetJointPosition(0));
            Vector2 span = (Vector2)transform.InverseTransformPoint(worldTip) - root;
            Vector2 bow = Vector2.Perpendicular(span) * curl;
            int lastSample = rope.JointCount - 1;
            Vector2 firstJoint = root;
            Vector2 nextJoint = transform.InverseTransformPoint(rope.GetJointPosition(1));
            int currentSegment = 0;
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / (float)(points.Length - 1);
                float sample = t * lastSample;
                int first = Mathf.Min((int)sample, lastSample - 1);
                if (first != currentSegment)
                {
                    currentSegment = first;
                    firstJoint = transform.InverseTransformPoint(rope.GetJointPosition(first));
                    nextJoint = transform.InverseTransformPoint(rope.GetJointPosition(first + 1));
                }
                Vector2 restingPoint = Vector2.LerpUnclamped(firstJoint, nextJoint, sample - first);
                Vector2 stretchedPoint = root + span * t + bow * (4f * t * (1f - t));
                points[i] = Vector2.LerpUnclamped(restingPoint, stretchedPoint, reach);
            }

            line.SetPositions(points);
            float opacity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(reach / 0.15f));
            Color tint = restingSprite.color;
            tint.a = restingAlpha;
            line.startColor = WithOpacity(lineStartTint * tint, opacity);
            line.endColor = WithOpacity(lineEndTint * tint, opacity);
            SetRestingOpacity(1f - opacity);
            line.enabled = true;
        }

        private void OnDisable() => ResetPose();

        private void ResetPose()
        {
            reach = 0f;
            if (line != null)
            {
                line.enabled = false;
                SetRestingOpacity(1f);
            }
        }

        private void SetRestingOpacity(float opacity)
        {
            Color color = restingSprite.color;
            color.a = restingAlpha * opacity;
            restingSprite.color = color;
        }

        private static Color WithOpacity(Color color, float opacity)
        {
            color.a *= opacity;
            return color;
        }
    }
}
