using System;
using MimicCell.Creatures;
using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerMovementController))]
    public sealed class PlayerCreatureFormController : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private GameObject baseVisualRoot;
        [SerializeField] private SpriteRenderer creatureFormRenderer;

        [Header("Colliders")]
        [SerializeField] private Collider2D baseCollider;
        [SerializeField] private BoxCollider2D creatureFormCollider;

        private Rigidbody2D attachedRigidbody;
        private PlayerMovementController movementController;
        private CreatureDefinition currentDefinition;

        public CreatureDefinition CurrentDefinition =>
            currentDefinition;

        public bool IsCreatureForm =>
            currentDefinition != null;

        public event Action<CreatureDefinition> FormChanged;

        private void Awake()
        {
            attachedRigidbody = GetComponent<Rigidbody2D>();

            movementController = GetComponent<PlayerMovementController>();

            if (baseVisualRoot == null ||
                creatureFormRenderer == null ||
                baseCollider == null ||
                creatureFormCollider == null)
            {
                Debug.LogError(
                    "Player creature form references are incomplete.",
                    this);

                enabled = false;
                return;
            }

            baseVisualRoot.SetActive(true);
            creatureFormRenderer.gameObject.SetActive(false);

            baseCollider.enabled = true;
            creatureFormCollider.enabled = false;
        }

        public bool ApplyCreatureForm(CreatureDefinition definition)
        {
            if (definition == null ||
                definition.PlayerForm == null ||
                !definition.PlayerForm.IsConfigured)
            {
                Debug.LogWarning(
                    "Creature definition has no player form settings.",
                    this);

                return false;
            }

            CreaturePlayerFormSettings form = definition.PlayerForm;

            baseVisualRoot.SetActive(false);

            creatureFormRenderer.sprite = form.Sprite;

            creatureFormRenderer.transform.localPosition = Vector3.zero;

            creatureFormRenderer.transform.localRotation = Quaternion.identity;

            creatureFormRenderer.transform.localScale =
                new Vector3(
                    form.VisualScale.x,
                    form.VisualScale.y,
                    1f);

            creatureFormRenderer.flipX = form.FlipXToFaceRight;

            creatureFormRenderer.gameObject.SetActive(true);

            baseCollider.enabled = false;

            creatureFormCollider.size = form.ColliderSize;

            creatureFormCollider.offset = form.ColliderOffset;

            creatureFormCollider.enabled = true;

            currentDefinition = definition;
            FormChanged?.Invoke(currentDefinition);

            return true;
        }

        public void RestoreBaseForm()
        {
            currentDefinition = null;

            creatureFormRenderer.gameObject.SetActive(false);
            creatureFormRenderer.sprite = null;

            creatureFormCollider.enabled = false;

            baseVisualRoot.SetActive(true);
            baseCollider.enabled = true;

            FormChanged?.Invoke(null);
        }

        private void LateUpdate()
        {
            if (!IsCreatureForm || attachedRigidbody == null)
            {
                return;
            }

            creatureFormRenderer.transform.localRotation = Quaternion.identity;

            float horizontalVelocity = attachedRigidbody.linearVelocity.x;

            if (Mathf.Abs(horizontalVelocity) <= 0.01f)
            {
                return;
            }

            bool movingRight = horizontalVelocity > 0f;

            bool flipToFaceRight = currentDefinition.PlayerForm.FlipXToFaceRight;

            creatureFormRenderer.flipX =
                movingRight
                    ? flipToFaceRight
                    : !flipToFaceRight;
        }
    }
}
