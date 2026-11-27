using MimicCell.Creatures;
using UnityEngine;

namespace MimicCell.UI
{
    internal sealed class PortraitLayout
    {
        private RectTransform target;
        private Vector2 originalPosition;
        private Vector3 originalScale;

        public void Apply(RectTransform newTarget, CreaturePortraitSettings settings)
        {
            if (target != newTarget)
            {
                Reset();
                target = newTarget;

                if (target != null)
                {
                    originalPosition = target.anchoredPosition;
                    originalScale = target.localScale;
                }
            }

            if (target == null)
                return;

            float scale = settings != null ? settings.Scale : 1f;
            Vector2 offset = settings != null ? settings.Offset : Vector2.zero;
            Vector2 position = originalPosition + offset;
            Vector3 localScale = new Vector3(
                originalScale.x * scale,
                originalScale.y * scale,
                originalScale.z);

            if (target.anchoredPosition != position)
                target.anchoredPosition = position;

            if (target.localScale != localScale)
                target.localScale = localScale;
        }

        public void Reset()
        {
            if (target != null)
            {
                target.anchoredPosition = originalPosition;
                target.localScale = originalScale;
            }

            target = null;
        }
    }
}
