using UnityEngine;

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    public sealed class AssimilationDarkeningEffect : MonoBehaviour
    {
        private static readonly int AssimilationId =
            Shader.PropertyToID("_MimicCellAssimilation");

        [SerializeField] private bool useTestValue;
        [SerializeField, Range(0f, 100f)] private float testPercent;

        private float assimilation;
        private float displayedAssimilation;

        public void SetAssimilation(float normalizedValue)
        {
            assimilation = Mathf.Clamp01(normalizedValue);
        }

        private void LateUpdate()
        {
            float target = useTestValue ? testPercent / 100f : assimilation;

            displayedAssimilation = Mathf.MoveTowards(
                displayedAssimilation, target, Time.deltaTime * 2f);

            Shader.SetGlobalFloat(AssimilationId, displayedAssimilation);
        }

        private void OnDisable()
        {
            displayedAssimilation = 0f;
            Shader.SetGlobalFloat(AssimilationId, 0f);
        }
    }
}