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

        private const float TerrainClearance = 0.008f;
        private readonly RaycastHit2D[] terrainHits = new RaycastHit2D[8];
        private readonly Collider2D[] nearbyColliders = new Collider2D[12];
        private Rigidbody2D playerBody;
        private JointPose[] poses;
        private float chainLength;
        private Vector2 strikeTip;
        private float strikeReach;
        private float strikeCurl;
        private Vector2[] strikePoints;
        private bool strikePoseApplied;

        private struct JointPose
        {
            public Quaternion RestRotation;
            public Vector2 RestDirection;
            public Vector3 RestOffset;
            public Quaternion SwimRotation;
            public Vector2 SegmentAxis;
            public float SegmentLength;
            public float FollowAngle;
            public float WorldAngle;
            public float AngularVelocity;
        }

        internal int JointCount => joints.Length;
        internal Vector2 GetJointPosition(int index) => joints[index].position;

        internal float GetStrikeAlignment(Vector2 target)
        {
            Vector2 root = joints[0].position;
            Vector2 direction = (Vector2)joints[joints.Length - 1].position - root;
            return Vector2.Dot(direction.normalized, (target - root).normalized);
        }

        internal void SetStrikePose(Vector2 tip, float reach, float curl)
        {
            strikeTip = tip;
            strikeReach = reach;
            strikeCurl = curl;
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
                Vector2 localSegment = joints[i + 1].localPosition;
                poses[i].RestOffset = joints[i + 1].localPosition;
                poses[i].RestRotation = joints[i].localRotation;
                poses[i].SegmentAxis = localSegment.normalized;
                poses[i].SegmentLength = localSegment.magnitude;
                poses[i].RestDirection = transform.InverseTransformVector(joints[i + 1].position - joints[i].position);
                chainLength += poses[i].SegmentLength;
            }
        }

        private void OnEnable()
        {
            if (poses == null)
            {
                return;
            }

            float heading = transform.eulerAngles.z;
            for (int i = 0; i < poses.Length; i++)
            {
                poses[i].FollowAngle = heading;
                poses[i].WorldAngle = GetAngle(joints[i + 1].position - joints[i].position);
            }
        }

        private void LateUpdate()
        {
            RestoreSwimPose();
            if (Time.deltaTime <= 0f)
            {
                return;
            }

            Vector2 waterFlow = -Vector2.ClampMagnitude(playerBody.linearVelocity / fullBendSpeed, 1f);
            float wavePhase = Time.time * 5.2f + transform.localPosition.x * 2f;
            float sway = Mathf.Lerp(swayDegrees, swayDegrees * 0.6f, waterFlow.magnitude);
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(terrainLayers);
            bool nearTerrain = HasNearbyTerrain(filter);
            // Keep the total delay consistent when the same leg uses more bones.
            float turnScale = 3f / (poses.Length - 1);

            float heading = transform.eulerAngles.z;
            float precedingFollowAngle = heading;
            float precedingWorldAngle = 0f;
            float precedingPreviousAngle = 0f;
            float precedingRestAngle = 0f;

            for (int i = 0; i < poses.Length; i++)
            {
                Transform joint = joints[i];
                Transform parent = joint.parent;
                ref JointPose pose = ref poses[i];
                float flexibility = i / (float)(poses.Length - 1);
                Vector2 restDirection = transform.TransformVector(pose.RestDirection);
                float restAngle = GetAngle(restDirection);
                float followTime = Mathf.Lerp(0.025f, 0.07f, flexibility) * turnScale;
                // Propagate the body's turn along the chain in world space.
                pose.FollowAngle = i == 0 ? heading : Mathf.LerpAngle(pose.FollowAngle, precedingFollowAngle,
                    1f - Mathf.Exp(-Time.deltaTime / followTime));
                float turnLag = Mathf.DeltaAngle(heading, pose.FollowAngle);
                Vector2 dragAxis = GetDirection(restAngle + turnLag);
                float sideFlow = Vector2.Dot(Vector2.Perpendicular(dragAxis), waterFlow);
                float wave = Mathf.Sin(wavePhase - flexibility * 3f) * sway;
                float targetAngle = restAngle + turnLag + (sideFlow * maxWaterBend * flexibility + wave) * flexibility;
                float response = Mathf.Lerp(0.055f, 0.15f, flexibility);
                float previousAngle = pose.WorldAngle;
                float bendLimit = 0f;
                if (i > 0)
                {
                    float authoredBend = Mathf.Abs(Mathf.DeltaAngle(precedingRestAngle, restAngle));
                    float previousBend = Mathf.Abs(Mathf.DeltaAngle(precedingPreviousAngle, previousAngle));
                    // A joint leaving terrain contact can straighten from its current bend.
                    float freeBend = Mathf.Lerp(22f, 39f, flexibility) * Mathf.Min(turnScale, 1f);
                    bendLimit = Mathf.Max(freeBend, Mathf.Max(authoredBend, previousBend));
                    float targetBend = Mathf.DeltaAngle(precedingWorldAngle, targetAngle);
                    targetAngle = precedingWorldAngle + Mathf.Clamp(targetBend, -bendLimit, bendLimit);
                }

                float currentAngle = i == 0 ? GetAngle(joints[i + 1].position - joint.position) : previousAngle;
                bool touchingTerrain = false;
                if (nearTerrain)
                {
                    Vector2 localTarget = ((Vector2)parent.InverseTransformVector(GetDirection(targetAngle))).normalized;
                    Vector2 proposedOffset = parent.TransformVector(localTarget * pose.SegmentLength);
                    touchingTerrain = TryFindTerrain(joint.position, proposedOffset, filter, out RaycastHit2D hit);
                    if (touchingTerrain)
                    {
                        Vector2 corrected = ConstrainToTerrain(joint.position, parent, localTarget, pose.SegmentLength, currentAngle, hit);
                        targetAngle = GetAngle(parent.TransformVector(corrected));
                        response = 0.055f;
                    }
                }

                pose.WorldAngle = Mathf.SmoothDampAngle(currentAngle, targetAngle, ref pose.AngularVelocity, response);
                if (i > 0 && !touchingTerrain)
                {
                    float bend = Mathf.DeltaAngle(precedingWorldAngle, pose.WorldAngle);
                    if (Mathf.Abs(bend) > bendLimit)
                    {
                        pose.WorldAngle = precedingWorldAngle + Mathf.Clamp(bend, -bendLimit, bendLimit);
                        if (pose.AngularVelocity * bend > 0f)
                        {
                            pose.AngularVelocity = 0f;
                        }
                    }
                }

                Vector2 localDirection = parent.InverseTransformVector(GetDirection(pose.WorldAngle));
                joint.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(pose.SegmentAxis, localDirection));
                precedingFollowAngle = pose.FollowAngle;
                precedingWorldAngle = pose.WorldAngle;
                precedingPreviousAngle = previousAngle;
                precedingRestAngle = restAngle;
            }

            if (strikeReach > 0f)
            {
                ApplyStrikePose();
            }
        }

        private void OnDisable()
        {
            if (poses == null)
            {
                return;
            }

            RestoreSwimPose();
            strikeReach = 0f;
            for (int i = 0; i < poses.Length; i++)
            {
                poses[i].AngularVelocity = 0f;
                joints[i].localRotation = poses[i].RestRotation;
            }
        }

        private void ApplyStrikePose()
        {
            strikePoints ??= new Vector2[joints.Length];
            Vector2 root = joints[0].position;
            Vector2 span = strikeTip - root;
            Vector2 bow = Vector2.Perpendicular(span) * strikeCurl;
            for (int i = 0; i < joints.Length; i++)
            {
                float t = i / (float)(joints.Length - 1);
                Vector2 stretched = root + span * t + bow * (4f * t * (1f - t));
                strikePoints[i] = Vector2.Lerp(joints[i].position, stretched, strikeReach);
            }

            for (int i = 0; i < poses.Length; i++)
            {
                Transform joint = joints[i];
                Transform next = joints[i + 1];
                poses[i].SwimRotation = joint.localRotation;
                Vector2 direction = joint.parent.InverseTransformVector(strikePoints[i + 1] - strikePoints[i]);
                if (direction.sqrMagnitude > 0.000001f)
                {
                    joint.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(poses[i].SegmentAxis, direction));
                }
                next.position = strikePoints[i + 1];
            }
            strikePoseApplied = true;
        }

        private void RestoreSwimPose()
        {
            if (!strikePoseApplied)
            {
                return;
            }

            for (int i = 0; i < poses.Length; i++)
            {
                joints[i].localRotation = poses[i].SwimRotation;
                joints[i + 1].localPosition = poses[i].RestOffset;
            }
            strikePoseApplied = false;
        }

        private static float GetAngle(Vector2 direction) => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        private static Vector2 GetDirection(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private Vector2 ConstrainToTerrain(
            Vector2 start, Transform parent, Vector2 target, float segmentLength, float currentAngle, RaycastHit2D hit)
        {
            Vector2 normal = hit.normal;
            float minimumTravel = Vector2.Dot(hit.centroid - start, normal) + TerrainClearance;
            if (hit.distance <= 0f)
            {
                Vector2 separation = start - hit.collider.ClosestPoint(start);
                float distance = separation.magnitude;
                if (distance > 0.001f)
                {
                    normal = separation / distance;
                    minimumTravel = contactRadius - distance + TerrainClearance;
                }
            }

            // Express the surface constraint in joint space so squash and stretch preserve clearance.
            Vector2 localNormal = new Vector2(
                Vector2.Dot(parent.TransformVector(Vector3.right), normal),
                Vector2.Dot(parent.TransformVector(Vector3.up), normal));
            float travelPerSegment = localNormal.magnitude * segmentLength;
            if (travelPerSegment <= 0.0001f)
            {
                return target;
            }

            Vector2 outward = localNormal.normalized;
            float required = Mathf.Clamp(minimumTravel / travelPerSegment, -1f, 1f);
            if (Vector2.Dot(target, outward) >= required)
            {
                return target;
            }

            Vector2 tangent = Vector2.Perpendicular(outward);
            float side = Vector2.Dot(tangent, target);
            if (Mathf.Abs(side) < 0.12f)
            {
                side = Vector2.Dot(tangent, parent.InverseTransformVector(GetDirection(currentAngle)));
            }
            float along = Mathf.Sqrt(Mathf.Max(0f, 1f - required * required));
            return outward * required + tangent * (Mathf.Sign(side) * along);
        }

        private bool HasNearbyTerrain(ContactFilter2D filter)
        {
            float scaleBound = Mathf.Sqrt(transform.TransformVector(Vector3.right).sqrMagnitude +
                transform.TransformVector(Vector3.up).sqrMagnitude);
            int count = Physics2D.OverlapCircle(joints[0].position, chainLength * scaleBound + contactRadius,
                filter, nearbyColliders);
            for (int i = 0; i < count; i++)
            {
                if (IsTerrain(nearbyColliders[i]))
                {
                    return true;
                }
            }
            return count == nearbyColliders.Length;
        }

        private bool TryFindTerrain(Vector2 start, Vector2 offset, ContactFilter2D filter, out RaycastHit2D nearestHit)
        {
            int count = Physics2D.CircleCast(start, contactRadius, offset.normalized, filter, terrainHits, offset.magnitude);
            float nearest = float.PositiveInfinity;
            nearestHit = default;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = terrainHits[i];
                if (hit.distance >= nearest || !IsTerrain(hit.collider))
                {
                    continue;
                }
                nearest = hit.distance;
                nearestHit = hit;
            }
            return nearest < float.PositiveInfinity;
        }

        private bool IsTerrain(Collider2D collider)
        {
            if (collider == null)
            {
                return false;
            }
            Rigidbody2D obstacleBody = collider.attachedRigidbody;
            return obstacleBody == null || (obstacleBody != playerBody && obstacleBody.bodyType == RigidbodyType2D.Static);
        }
    }
}
