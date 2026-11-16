using UnityEngine;
using UnityEngine.Rendering;

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StatusEffectController))]
    public sealed class DopamineVisualEffect : MonoBehaviour
    {
        [SerializeField] private StatusEffectDefinition dopamineDefinition;
        [SerializeField] private Volume dopamineVolume;

        private StatusEffectController statusController;

        private void Awake()
        {
            statusController = GetComponent<StatusEffectController>();
        }

        private void LateUpdate()
        {
            if (dopamineVolume == null)
                return;

            bool active = dopamineDefinition != null &&
                          statusController.HasEffect(dopamineDefinition);

            float targetWeight = active ? 1f : 0f;
            float transitionSpeed = active ? 10f : 5f;

            dopamineVolume.weight = Mathf.MoveTowards(
                dopamineVolume.weight,
                targetWeight,
                transitionSpeed * Time.deltaTime);
        }

        private void OnDisable()
        {
            if (dopamineVolume != null)
                dopamineVolume.weight = 0f;
        }
    }
}