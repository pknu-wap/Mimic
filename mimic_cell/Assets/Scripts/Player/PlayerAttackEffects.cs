using MimicCell.StatusEffects;
using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypePlayer), typeof(StatusEffectController), typeof(PlayerStaminaController))]
    public class PlayerAttackEffects : MonoBehaviour
    {
        [SerializeField] private StatusEffectDefinition dopamineRelease;

        private PrototypePlayer player;
        private PlayerStaminaController stamina;
        private PlayerAssimilationController assimilation;
        private StatusEffectController statusEffects;

        private void Awake()
        {
            player = GetComponent<PrototypePlayer>();
            stamina = GetComponent<PlayerStaminaController>();
            assimilation = GetComponent<PlayerAssimilationController>();
            statusEffects = GetComponent<StatusEffectController>();
        }

        public bool TryBeginAttack() =>
            stamina != null && stamina.TrySpendForAttack();

        public void NotifyHit()
        {
            if (!player.Health.IsAlive)
            {
                return;
            }

            if (assimilation != null)
            {
                assimilation.NotifyAttackHit();
            }
            if (dopamineRelease != null)
            {
                statusEffects.ApplyEffect(dopamineRelease);
            }
        }
    }
}
