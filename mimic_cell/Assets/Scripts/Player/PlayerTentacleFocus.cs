using System.Collections.Generic;
using MimicCell.Creatures;
using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypePlayer))]
    [DefaultExecutionOrder(20)]
    public class PlayerTentacleFocus : MonoBehaviour
    {
        private const float TargetOpacity = 0.75f;

        private readonly List<SpriteRenderer> sprites = new List<SpriteRenderer>();
        private readonly List<float> originalAlpha = new List<float>();
        private PrototypePlayer player;
        private PlayerMovementController movement;
        private PlayerTentacleAttack attack;
        private PlayerDnaExtraction extraction;
        private CreatureController currentTarget;

        private void Awake()
        {
            player = GetComponent<PrototypePlayer>();
            movement = GetComponent<PlayerMovementController>();
            attack = GetComponent<PlayerTentacleAttack>();
            extraction = GetComponent<PlayerDnaExtraction>();
        }

        private void LateUpdate()
        {
            SetTarget(FindHoveredCreature());
        }

        private CreatureController FindHoveredCreature()
        {
            if (!player.Health.IsAlive || !movement.ControlsEnabled || Time.timeScale <= 0f ||
                !TentacleTargeting.TryReadPointer(transform.position, out Vector2 point))
            {
                return null;
            }

            return attack?.FindAimCreature(point) ?? extraction?.FindAimCreature(point);
        }

        private void SetTarget(CreatureController creature)
        {
            if (ReferenceEquals(currentTarget, creature))
            {
                return;
            }

            for (int i = 0; i < sprites.Count; i++)
            {
                if (sprites[i] != null)
                {
                    SetAlpha(sprites[i], originalAlpha[i]);
                }
            }
            sprites.Clear();
            originalAlpha.Clear();
            currentTarget = creature;
            if (creature == null || creature.VisualRoot == null)
            {
                return;
            }

            creature.VisualRoot.GetComponentsInChildren<SpriteRenderer>(false, sprites);
            foreach (SpriteRenderer sprite in sprites)
            {
                float alpha = sprite.color.a;
                originalAlpha.Add(alpha);
                SetAlpha(sprite, alpha * TargetOpacity);
            }
        }

        private static void SetAlpha(SpriteRenderer sprite, float alpha)
        {
            Color color = sprite.color;
            color.a = alpha;
            sprite.color = color;
        }

        private void OnDisable() => SetTarget(null);
    }
}
