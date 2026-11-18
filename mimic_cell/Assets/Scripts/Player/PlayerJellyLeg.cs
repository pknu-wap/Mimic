using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    public class PlayerJellyLeg : MonoBehaviour
    {
        [SerializeField] private Transform[] joints;
        [SerializeField] private LayerMask terrainLayers = ~0;
        [SerializeField, Min(0.01f)] private float fullBendSpeed = 4f;
        [SerializeField, Range(0f, 90f)] private float maxWaterBend = 42f;
        [SerializeField, Range(0f, 20f)] private float swayDegrees = 9f;
        [SerializeField, Min(0.01f)] private float contactRadius = 0.09f;

        private readonly RaycastHit2D[] terrainHits = new RaycastHit2D[8];
        private Rigidbody2D playerBody;
        private JointPose[] poses;

        private struct JointPose
        {
            public Quaternion RestRotation;
            public Vector2 RestDirection;
            public float Bend;
            public float BendVelocity;
        }

        private void Awake()
        {
            playerBody = GetComponentInParent<Rigidbody2D>();
            if (playerBody == null || joints == null || joints.Length < 3)
            {
                enabled = false;
                return;
            }

            poses = new JointPose[joints.Length - 1];
            for (int i = 0; i < poses.Length; i++)
            {
                poses[i].RestRotation = joints[i].localRotation;
                poses[i].RestDirection = poses[i].RestRotation * joints[i + 1].localPosition;
            }
        }

        private void LateUpdate()
        {
            if (Time.deltaTime <= 0f)
            {
                return;
            }

            Vector2 waterFlow = -Vector2.ClampMagnitude(playerBody.linearVelocity / fullBendSpeed, 1f);
            float wavePhase = Time.time * 5.2f;
            float sway = Mathf.Lerp(swayDegrees, swayDegrees * 0.6f, waterFlow.magnitude);
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(terrainLayers);

            for (int i = 0; i < poses.Length; i++)
            {
                Transform joint = joints[i];
                ref JointPose pose = ref poses[i];
                float flexibility = i / (float)(poses.Length - 1);
                Vector2 localFlow = joint.parent.InverseTransformDirection(waterFlow);
                float sideFlow = Vector2.Dot(Vector2.Perpendicular(pose.RestDirection.normalized), localFlow);
                float wave = Mathf.Sin(wavePhase - flexibility * 3f) * sway;
                float targetBend = (sideFlow * maxWaterBend * flexibility + wave) * flexibility;
                float response = Mathf.Lerp(0.08f, 0.25f, flexibility);

                Vector2 proposedOffset = joint.parent.TransformVector(Quaternion.Euler(0f, 0f, targetBend) * pose.RestDirection);
                if (TryFindTerrain(joint.position, proposedOffset, filter, out Vector2 normal))
                {
                    Vector2 tangent = Vector2.Perpendicular(normal);
                    float alongSurface = Vector2.Dot(tangent, proposedOffset.normalized);
                    if (Mathf.Abs(alongSurface) < 0.12f)
                    {
                        Vector2 currentOffset = joints[i + 1].position - joint.position;
                        alongSurface = Vector2.Dot(tangent, currentOffset);
                    }
                    if (alongSurface < 0f)
                    {
                        tangent = -tangent;
                    }

                    Vector2 awayFromTerrain = joint.parent.InverseTransformVector(tangent + normal * 0.15f);
                    targetBend = Vector2.SignedAngle(pose.RestDirection, awayFromTerrain);
                    response = 0.04f;
                }

                pose.Bend = Mathf.SmoothDampAngle(pose.Bend, targetBend, ref pose.BendVelocity, response);
                joint.localRotation = Quaternion.Euler(0f, 0f, pose.Bend) * pose.RestRotation;
            }
        }

        private void OnDisable()
        {
            if (poses == null)
            {
                return;
            }

            for (int i = 0; i < poses.Length; i++)
            {
                poses[i].Bend = 0f;
                poses[i].BendVelocity = 0f;
                joints[i].localRotation = poses[i].RestRotation;
            }
        }

        private bool TryFindTerrain(Vector2 start, Vector2 offset, ContactFilter2D filter, out Vector2 normal)
        {
            int count = Physics2D.CircleCast(start, contactRadius, offset.normalized, filter, terrainHits, offset.magnitude);
            float nearest = float.PositiveInfinity;
            normal = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = terrainHits[i];
                Rigidbody2D obstacleBody = hit.rigidbody;
                if (hit.collider == null || obstacleBody == playerBody ||
                    (obstacleBody != null && obstacleBody.bodyType != RigidbodyType2D.Static) || hit.distance >= nearest)
                {
                    continue;
                }
                nearest = hit.distance;
                normal = hit.normal;
            }
            return nearest < float.PositiveInfinity;
        }
    }
}
