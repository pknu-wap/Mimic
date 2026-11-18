using MimicCell.Gameplay;
using MimicCell.Player;
using UnityEngine;

namespace MimicCell.Creatures
{
    public readonly struct FoodChainTarget
    {
        public FoodChainTarget(Component owner, ActorHealth health, CreatureFoodTier foodTier)
        {
            Owner = owner;
            Health = health;
            FoodTier = foodTier;
        }

        public Component Owner { get; }

        public ActorHealth Health { get; }

        public CreatureFoodTier FoodTier { get; }

        public Transform Transform
        {
            get
            {
                return Owner != null
                    ? Owner.transform
                    : null;
            }
        }

        public bool IsValid
        {
            get
            {
                return Owner != null &&
                       Health != null &&
                       Health.IsAlive;
            }
        }
    }

    public static class FoodChainRules
    {
        public static bool CanAttack(CreatureFoodTier attackerTier, CreatureFoodTier targetTier)
        {
            return (int)attackerTier > (int)targetTier;
        }

        public static bool MustFleeFrom(CreatureFoodTier selfTier, CreatureFoodTier otherTier)
        {
            return CanAttack(otherTier, selfTier);
        }

        public static bool TryResolveTarget(Collider2D sourceCollider, out FoodChainTarget target)
        {
            target = default;

            if (sourceCollider == null)
            {
                return false;
            }

            CreatureController creature = sourceCollider.GetComponentInParent<CreatureController>();

            if (creature != null)
            {
                target = new FoodChainTarget(creature, creature.Health, creature.CurrentFoodTier);

                return target.IsValid;
            }

            PrototypePlayer player = sourceCollider.GetComponentInParent<PrototypePlayer>();

            if (player != null)
            {
                target = new FoodChainTarget(player, player.Health, player.CurrentFoodTier);

                return target.IsValid;
            }

            return false;
        }
    }
}