using MimicCell.Gameplay;
using UnityEngine;

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    public sealed class CreatureAnoxiaVisual : MonoBehaviour
    {
        [SerializeField] private GameObject bubbleEffect;

        private ExtinctionStatusEffects effects;
        private ActorHealth health;

        private void Awake()
{
    health = GetComponent<ActorHealth>();

    Transform bubbleChild = transform.Find("PoisonVFX");

    if (bubbleChild != null)
        bubbleEffect = bubbleChild.gameObject;

    if (bubbleEffect == null ||
        !bubbleEffect.transform.IsChildOf(transform))
    {
        Debug.LogWarning(
            "CreatureBase 아래에 자식 PoisonVFX를 넣어 주세요.",
            this);

        enabled = false;
        return;
    }

    SetVisible(false);
}

        private void LateUpdate()
        {
            if (effects == null)
                effects = GetComponent<ExtinctionStatusEffects>();

            bool visible =
                health != null &&
                health.IsAlive &&
                effects != null &&
                effects.isActiveAndEnabled &&
                effects.IsActive(ExtinctionStatus.Anoxia);

            SetVisible(visible);
        }

        private void SetVisible(bool visible)
        {
            if (bubbleEffect != null && bubbleEffect.activeSelf != visible)
                bubbleEffect.SetActive(visible);
        }

        private void OnDisable()
        {
            SetVisible(false);
        }
    }
}