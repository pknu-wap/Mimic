namespace MimicCell.Gameplay
{
    public enum ActorDamageType
    {
        Unknown,
        Contact,
        Reflection,
        StatusEffect,
        Environment
    }

    public readonly struct ActorDamageContext
    {
        public ActorDamageContext(int amount, ActorHealth source, ActorDamageType damageType)
        {
            Amount = amount;
            Source = source;
            DamageType = damageType;
        }

        public int Amount { get; }

        public ActorHealth Source { get; }

        public ActorDamageType DamageType { get; }

        public bool CanReflect =>
            Source != null &&
            DamageType == ActorDamageType.Contact;
    }
}