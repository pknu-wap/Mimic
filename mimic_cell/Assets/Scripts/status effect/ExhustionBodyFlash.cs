using UnityEngine;

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    public sealed class ExhaustionBodyFlash : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private Material flashMaterial;

        private SpriteRenderer flashRenderer;

        private void Awake()
        {
            if (bodyRenderer == null || flashMaterial == null)
            {
                Debug.LogWarning(
                    "탈진 몸 연출의 Body Renderer와 Flash Material을 연결해 주세요.",
                    this);
                enabled = false;
                return;
            }

            GameObject overlay = new GameObject("Exhaustion Body Overlay");
            overlay.layer = bodyRenderer.gameObject.layer;
            overlay.transform.SetParent(bodyRenderer.transform, false);

            flashRenderer = overlay.AddComponent<SpriteRenderer>();
            flashRenderer.sharedMaterial = flashMaterial;
            flashRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (flashRenderer == null)
                return;

if (bodyRenderer == null ||
    !bodyRenderer.enabled ||
    !bodyRenderer.gameObject.activeInHierarchy)            {
                flashRenderer.enabled = false;
                return;
            }

            flashRenderer.sprite = bodyRenderer.sprite;
            flashRenderer.flipX = bodyRenderer.flipX;
            flashRenderer.flipY = bodyRenderer.flipY;
            flashRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            flashRenderer.sortingOrder = bodyRenderer.sortingOrder + 1;
            flashRenderer.maskInteraction = bodyRenderer.maskInteraction;
            flashRenderer.color = new Color(1f, 1f, 1f, bodyRenderer.color.a);
            flashRenderer.enabled = true;
        }

        private void OnDisable()
        {
            if (flashRenderer != null)
                flashRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (flashRenderer != null)
                Destroy(flashRenderer.gameObject);
        }
    }
}