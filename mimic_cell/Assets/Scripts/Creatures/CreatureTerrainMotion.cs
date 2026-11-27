using System.Collections.Generic;
using MimicCell.World;
using UnityEngine;

namespace MimicCell.Creatures
{
    internal class CreatureTerrainMotion
    {
        private const float Clearance = 0.06f;
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(8);
        private readonly ContactFilter2D solidFilter = new ContactFilter2D { useTriggers = false };

        public Vector2 ConstrainVelocity(Collider2D body, Vector2 velocity, float deltaTime)
        {
            Vector2 step = velocity * deltaTime;
            float distance = step.magnitude;
            if (body == null || !body.enabled || distance < 0.0001f)
                return velocity;

            Vector2 heading = step / distance;
            float forward = GetClearance(body, heading, distance, out Vector2 normal);
            if (forward >= distance)
                return velocity;

            Vector2 remaining = heading * (distance - forward);
            Vector2 slide = remaining - normal * Vector2.Dot(remaining, normal);
            if (slide.sqrMagnitude > 0.000001f)
                return LimitStep(body, heading * forward + slide) / deltaTime;

            if (forward > 0.001f)
                return heading * (forward / deltaTime);

            // Head-on motion has no tangential projection. Try swimming around the face.
            Vector2 tangent = Vector2.Perpendicular(normal) * (distance * 0.65f);
            Vector2 turn = LimitStep(body, tangent);
            if (turn.sqrMagnitude <= 0.000001f)
                turn = LimitStep(body, -tangent);
            return turn / deltaTime;
        }

        private Vector2 LimitStep(Collider2D body, Vector2 step)
        {
            float distance = step.magnitude;
            if (distance < 0.0001f)
                return Vector2.zero;
            Vector2 direction = step / distance;
            return direction * GetClearance(body, direction, distance, out _);
        }

        private float GetClearance(Collider2D body, Vector2 direction, float distance, out Vector2 normal)
        {
            body.Cast(direction, solidFilter, hits, distance + Clearance);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider is EdgeCollider2D &&
                    hit.collider.transform.parent != null &&
                    hit.collider.transform.parent.TryGetComponent<SeabedTerrainWorld>(out _) &&
                    Vector2.Dot(hit.normal, direction) < -0.001f)
                {
                    normal = hit.normal;
                    return Mathf.Clamp(hit.distance - Clearance, 0f, distance);
                }
            }

            normal = default;
            return distance;
        }
    }
}
