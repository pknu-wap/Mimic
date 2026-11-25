using MimicCell.Creatures;
using UnityEngine;
using MimicCell.Gameplay;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypePlayer), typeof(PlayerAttackEffects))]
    public sealed class PlayerCreatureContactAttack : MonoBehaviour
    {
        [Header("Creature Form Contact Attack")]
        [SerializeField, Min(0f)]
        private float contactCooldown = 0.8f;

        private PrototypePlayer player;
        private PlayerAttackEffects attackEffects;
        private float cooldownRemaining;

        private void Awake()
        {
            player = GetComponent<PrototypePlayer>();
            attackEffects = GetComponent<PlayerAttackEffects>();
        }

        private void Update()
        {
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (cooldownRemaining > 0f ||
                player == null ||
                player.Health == null ||
                !player.Health.IsAlive)
            {
                return;
            }

            if (!CreatureColliderRole.IsBodyCollider(other))
            {
                return;
            }

            if (!player.IsCreatureForm)
            {
                return;
            }

            int attackDamage = player.CurrentAttackDamage;

            if (attackDamage <= 0)
            {
                return;
            }

            CreatureController target = other.GetComponentInParent<CreatureController>();

            if (target == null ||
                target.CurrentState ==
                    CreatureState.Dead ||
                target.Health == null ||
                !target.Health.IsAlive)
            {
                return;
            }

            int finalDamage = CreatureCombatRules.CalculateDamage(
                attackDamage,
                player.CurrentFoodTier,
                target.CurrentFoodTier,
                target.CurrentTraits);

            if (finalDamage <= 0 || !attackEffects.TryBeginAttack())
            {
                return;
            }

            ActorDamageContext damageContext =
                new ActorDamageContext(
                    finalDamage,
                    player.Health,
                    ActorDamageType.Contact);

            if (target.Health.TakeDamage(damageContext))
            {
                cooldownRemaining = contactCooldown;
                attackEffects.NotifyHit();
                Debug.Log(
                    "복제 몬스터 공격 성공: " +
                    target.SpeciesName +
                    " / 기본 피해량: " +
                    attackDamage +
                    " / 실제 피해량: " +
                    finalDamage +
                    " / 남은 체력: " +
                    target.Health.CurrentHealth +
                    "/" +
                    target.Health.MaxHealth,
                    this);
            }
        }
    }
}
