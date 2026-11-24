using MimicCell.Gameplay;
using UnityEngine;

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    public sealed class CreatureFreezingVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private Color frozenTint =
            new Color(0.55f, 0.8f, 1f, 1f);

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
                    "CreatureFreezingVisual의 Body에 몸의 SpriteRenderer를 연결해 주세요.",
                    this);

                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (body == null)
                return;

            // 상태이상 컴포넌트는 게임 도중 추가될 수도 있다.
            if (effects == null)
                effects = GetComponent<ExtinctionStatusEffects>();

            bool frozen =
                health != null &&
                health.IsAlive &&
                effects != null &&
                effects.isActiveAndEnabled &&
                effects.IsActive(ExtinctionStatus.Freezing);

            if (!frozen)
            {
                RestoreColor();
                return;
            }

            // 빙결을 시작할 때 원래 색을 한 번 저장한다.
            if (!tintApplied)
            {
                originalColor = body.color;
                tintApplied = true;
            }

            Color color = body.color;
            color.r = originalColor.r * frozenTint.r;
            color.g = originalColor.g * frozenTint.g;
            color.b = originalColor.b * frozenTint.b;
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