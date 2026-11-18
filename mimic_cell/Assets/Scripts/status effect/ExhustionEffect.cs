using UnityEngine;

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StatusEffectController))]
    public sealed class ExhaustionEffect : MonoBehaviour
    {
        [SerializeField] private StatusEffectDefinition exhaustionDefinition;

        [ContextMenu("Test Exhaustion")]
        private void TestExhaustion()
        {
            if (!Application.isPlaying || exhaustionDefinition == null)
                return;

            GetComponent<StatusEffectController>().ApplyEffect(exhaustionDefinition);
        }
    }
}