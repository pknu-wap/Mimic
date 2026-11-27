using UnityEngine;

namespace MimicCell.Prologue
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class DialoguePanelMotion : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float showDuration = 0.25f;
        [SerializeField, Min(0.01f)] private float hideDuration = 0.2f;
        [SerializeField, Min(0f)] private float slideDistance = 24f;

        private RectTransform panel;
        private CanvasGroup canvasGroup;
        private Vector2 shownPosition;

        private float progress;
        private bool wantsVisible;

        public bool IsAnimating =>
            !Mathf.Approximately(
                progress, wantsVisible ? 1f : 0f);

        private void Awake()
        {
            panel = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();

            shownPosition = panel.anchoredPosition;

            progress = 0f;
            wantsVisible = false;

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            ApplyAppearance();
        }

        public void Show()
        {
            wantsVisible = true;
        }

        public void Hide()
        {
            wantsVisible = false;
        }

        private void Update()
        {
            float target = wantsVisible ? 1f : 0f;
            float duration = wantsVisible ? showDuration : hideDuration;

            progress = Mathf.MoveTowards(
                progress,
                target,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));

            ApplyAppearance();
        }

        private void ApplyAppearance()
        {
            float eased = Mathf.SmoothStep(0f, 1f, progress);

            canvasGroup.alpha = eased;

            Vector2 hiddenPosition =
                shownPosition + Vector2.down * slideDistance;

            panel.anchoredPosition = Vector2.Lerp(
                hiddenPosition, shownPosition, eased);
        }
    }
}