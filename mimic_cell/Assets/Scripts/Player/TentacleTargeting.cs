using System;
using System.Collections.Generic;
using MimicCell.Creatures;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.Player
{
    internal class TentacleTargeting
    {
        private readonly List<Collider2D> clickHits = new List<Collider2D>(8);

        public Target Find(Vector2 point, Predicate<CreatureDefinition> accepts = null)
        {
            Physics2D.OverlapPoint(point, new ContactFilter2D { useTriggers = true }, clickHits);
            Target best = default;
            float nearest = float.PositiveInfinity;
            foreach (Collider2D collider in clickHits)
            {
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
