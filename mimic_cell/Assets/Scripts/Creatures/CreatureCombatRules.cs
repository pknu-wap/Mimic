using UnityEngine;

namespace MimicCell.Creatures
{
    public static class CreatureCombatRules
    {
        private const float ArmoredDamageMultiplier = 0.5f;

        public static int CalculateDamage(
            int baseDamage,
            CreatureFoodTier attackerTier,
            CreatureFoodTier defenderTier,
            CreatureTraits defenderTraits)
        {
            int finalDamage = Mathf.Max(0, baseDamage);

            bool isPredatorAttack = FoodChainRules.CanAttack(attackerTier, defenderTier);

            bool defenderIsArmored = defenderTraits.HasTrait(CreatureTraits.Armored);

            if (isPredatorAttack && defenderIsArmored)
            {
                finalDamage = Mathf.CeilToInt(finalDamage * ArmoredDamageMultiplier);
            }

            return finalDamage;
        }
    }
}