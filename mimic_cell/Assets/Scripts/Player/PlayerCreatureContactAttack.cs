using MimicCell.Creatures;
using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypePlayer))]
    public sealed class PlayerCreatureContactAttack : MonoBehaviour
    {
        [Header("Creature Form Contact Attack")]
        [SerializeField, Min(0f)]
        private float contactCooldown = 0.8f;

        private PrototypePlayer player;
        private float cooldownRemaining;

        private void Awake()
        {
            player = GetComponent<PrototypePlayer>();
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

            if (target.Health.TakeDamage(attackDamage))
            {
                Debug.Log(
                    "복제 몬스터 공격 성공: " +
                    target.SpeciesName +
                    " / 피해량: " +
                    attackDamage +
                    " / 남은 체력: " +
                    target.Health.CurrentHealth +
                    "/" +
                    target.Health.MaxHealth,
                    this);

                cooldownRemaining =
                    contactCooldown;
            }
        }
    }
}