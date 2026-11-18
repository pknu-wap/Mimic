namespace MimicCell.Creatures
{
    public static class CreatureTraitExtensions
    {
        public static bool HasTrait(this CreatureTraits currentTraits, CreatureTraits requiredTrait)
        {
            if (requiredTrait == CreatureTraits.None)
            {
                return false;
            }

            return (currentTraits & requiredTrait) == requiredTrait;
        }
    }
}