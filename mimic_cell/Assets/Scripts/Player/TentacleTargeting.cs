using System;
using System.Collections.Generic;
using MimicCell.Creatures;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    public class TentacleTargeting : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float interactionRange = 6f;

        private readonly List<Collider2D> clickHits = new List<Collider2D>(8);

        internal bool IsInRange(Vector2 point) =>
            (point - (Vector2)transform.position).sqrMagnitude <= interactionRange * interactionRange;

        internal Vector2 ClampToRange(Vector2 point)
        {
            Vector2 origin = transform.position;
            return origin + Vector2.ClampMagnitude(point - origin, interactionRange);
        }

        internal Target Find(Vector2 point, Predicate<CreatureDefinition> accepts = null,
            float? rangeOverride = null)
        {
            float range = rangeOverride ?? interactionRange;
            if ((point - (Vector2)transform.position).sqrMagnitude > range * range)
            {
                return default;
            }

            Physics2D.OverlapPoint(point, new ContactFilter2D { useTriggers = true }, clickHits);
            Target best = default;
            float nearest = float.PositiveInfinity;
            foreach (Collider2D collider in clickHits)
            {
                if (!CreatureColliderRole.IsBodyCollider(collider))
                {
                    continue;
                }
                var creature = collider.GetComponentInParent<CreatureController>();
                if (creature == null || creature.Definition == null ||
                    (accepts != null && !accepts(creature.Definition)))
                {
                    continue;
                }

                var candidate = new Target(collider, creature, point);
                float distance = (point - (Vector2)creature.transform.position).sqrMagnitude;
                if (distance < nearest && candidate.IsAlive)
                {
                    nearest = distance;
                    best = candidate;
                }
            }
            return best;
        }

        public static bool TryReadClick(int button, Vector3 origin, out Vector2 point)
        {
            point = default;
            if (!TryReadScreenClick(button, out Vector2 screenPosition))
            {
                return false;
            }

            return TryProjectPointer(screenPosition, origin, out point);
        }

        public static bool TryReadPointer(Vector3 origin, out Vector2 point)
        {
            point = default;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return TryProjectPointer(Mouse.current.position.ReadValue(), origin, out point);
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return TryProjectPointer(Input.mousePosition, origin, out point);
#else
            return false;
#endif
        }

        private static bool TryProjectPointer(Vector2 screenPosition, Vector3 origin, out Vector2 point)
        {
            point = default;
            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            if (!new Plane(Vector3.forward, origin).Raycast(ray, out float distance))
            {
                return false;
            }

            point = ray.GetPoint(distance);
            return true;
        }

        private static bool TryReadScreenClick(int button, out Vector2 point)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                point = Mouse.current.position.ReadValue();
                return (button == 0 ? Mouse.current.leftButton : Mouse.current.rightButton).wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            point = Input.mousePosition;
            return Input.GetMouseButtonDown(button);
#else
            point = default;
            return false;
#endif
        }

        public readonly struct Target
        {
            private readonly Collider2D collider;
            private readonly Vector2 localPoint;

            public Target(Collider2D collider, CreatureController creature, Vector2 point)
            {
                this.collider = collider;
                Creature = creature;
                localPoint = collider.transform.InverseTransformPoint(point);
            }

            public CreatureController Creature { get; }
            public bool IsAlive => collider != null && collider.enabled && collider.gameObject.activeInHierarchy &&
                Creature != null && Creature.Health != null && Creature.Health.IsAlive;
            public Vector2 Point => collider.transform.TransformPoint(localPoint);
        }
    }
}
