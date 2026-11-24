using MimicCell.Gameplay;
using UnityEngine;

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    public sealed class CreatureStatusTintVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;

        [SerializeField] private Color frozenTint =
            new Color(0.55f, 0.8f, 1f, 1f);

        [SerializeField] private Color mudTint =
            new Color(0.7f, 0.45f, 0.25f, 1f);

        private ActorHealth health;
        private ExtinctionStatusEffects effects;
        private Color originalColor;
        private bool tintApplied;

        private void Awake()
        {
            health = GetComponent<ActorHealth>();

            if (body == null)
            {
                Transform visual = transform.Find("Visual");

                if (visual != null)
                    body = visual.GetComponent<SpriteRenderer>();
            }

            if (body == null)
            {
                Debug.LogWarning(
                    "CreatureStatusTintVisual의 Body에 몸의 SpriteRenderer를 연결해 주세요.",
                    this);

                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (body == null)
                return;

            if (effects == null)
                effects = GetComponent<ExtinctionStatusEffects>();

            bool canShow =
                health != null &&
                health.IsAlive &&
                effects != null &&
                effects.isActiveAndEnabled;

            bool frozen = canShow &&
                effects.IsActive(ExtinctionStatus.Freezing);

            bool muddy = canShow &&
                effects.IsActive(ExtinctionStatus.MudCoating);

            if (!frozen && !muddy)
            {
                RestoreColor();
                return;
            }

            if (!tintApplied)
            {
                originalColor = body.color;
                tintApplied = true;
            }

            Color tint = Color.white;

            if (frozen)
                tint *= frozenTint;

            if (muddy)
                tint *= mudTint;

            Color color = body.color;
            color.r = originalColor.r * tint.r;
            color.g = originalColor.g * tint.g;
            color.b = originalColor.b * tint.b;
            body.color = color;
        }

        private void RestoreColor()
        {
            if (!tintApplied)
                return;

            if (body != null)
            {
                Color color = originalColor;
                color.a = body.color.a;
                body.color = color;
            }

            tintApplied = false;
        }

        private void OnDisable()
        {
            RestoreColor();
        }
    }
}