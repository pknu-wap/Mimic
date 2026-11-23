using UnityEngine;

namespace MimicCell.Creatures
{
    [DisallowMultipleComponent]
    public sealed class CreatureDirectionalPerception : MonoBehaviour
    {
        [SerializeField, Range(0f, 360f)]
        private float frontViewAngle = 120f;

        [SerializeField, Range(0f, 1f)]
        private float surroundingRadiusRatio = 0.25f;

        public bool CanDetect(
            Vector2 offset, Transform visualRoot, float perceptionRadius)
        {
            float distanceSquared = offset.sqrMagnitude;
            float radius = Mathf.Max(0f, perceptionRadius);

            if (distanceSquared > radius * radius)
                return false;

            float surroundingRadius = radius * surroundingRadiusRatio;

            if (distanceSquared <= surroundingRadius * surroundingRadius)
                return true;

            Vector2 forward = visualRoot != null
                ? (Vector2)visualRoot.TransformVector(Vector3.right)
                : Vector2.right;

            if (forward.sqrMagnitude <= 0.0001f)
                return false;

            float minimumDot =
                Mathf.Cos(frontViewAngle * 0.5f * Mathf.Deg2Rad);

            return Vector2.Dot(forward.normalized, offset.normalized)
                >= minimumDot;
        }
        public void DrawDetectionGizmos(
    Vector3 origin, Transform visualRoot, float radius)
{
    radius = Mathf.Max(0f, radius);

    // 가까운 거리에서는 어느 방향이든 감지
    Gizmos.color = Color.yellow;
    Gizmos.DrawWireSphere(origin, radius * surroundingRadiusRatio);

    Vector2 forward = visualRoot != null
        ? (Vector2)visualRoot.TransformVector(Vector3.right)
        : Vector2.right;

    if (forward.sqrMagnitude <= 0.0001f)
        return;

    float heading = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
    float startAngle = heading - frontViewAngle * 0.5f;

    Gizmos.color = Color.green;

    const int segments = 48;
    Vector3 previous = origin;

    for (int i = 0; i <= segments; i++)
    {
        float angle =
            (startAngle + frontViewAngle * i / segments) * Mathf.Deg2Rad;

        Vector3 point = origin + new Vector3(
            Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

        if (i == 0)
            Gizmos.DrawLine(origin, point);
        else
            Gizmos.DrawLine(previous, point);

        previous = point;
    }

    Gizmos.DrawLine(previous, origin);
}
    }
}