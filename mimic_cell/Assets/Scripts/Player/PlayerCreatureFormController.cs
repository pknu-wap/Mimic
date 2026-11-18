using System;
using MimicCell.Creatures;
using UnityEngine;
using MimicCell.Gameplay;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(ActorHealth))]
    [RequireComponent(typeof(PlayerMovementController))]
    public sealed class PlayerCreatureFormController : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private GameObject baseVisualRoot;
        [SerializeField] private SpriteRenderer creatureFormRenderer;

        [Header("Colliders")]
        [SerializeField] private Collider2D baseCollider;
        [SerializeField] private BoxCollider2D creatureFormCollider;

        [Header("Base Combat")]
        [SerializeField, Min(0)] private int baseAttackDamage = 0;

        private ActorHealth actorHealth;
        private int baseMaxHealth;
        private int currentAttackDamage;
        private float currentVariation;
        private Rigidbody2D attachedRigidbody;
        private PlayerMovementController movementController;
        private CreatureDefinition currentDefinition;
        public CreatureDefinition CurrentDefinition => currentDefinition;
        public bool IsCreatureForm => currentDefinition != null;
        public int CurrentAttackDamage => currentAttackDamage;
        public float CurrentVariation => currentVariation;
        public CreatureTraits CurrentTraits =>
            currentDefinition != null
                ? currentDefinition.Traits
                : CreatureTraits.None;

        public event Action<CreatureDefinition> FormChanged;

        private void Awake()
        {
            attachedRigidbody = GetComponent<Rigidbody2D>();

            movementController = GetComponent<PlayerMovementController>();

            actorHealth = GetComponent<ActorHealth>();

            baseMaxHealth = actorHealth.MaxHealth;

            currentAttackDamage = baseAttackDamage;

            currentVariation = 0f;

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

            float variation = UnityEngine.Random.value;

            int newMaxHealth = definition.HealthRange.Evaluate(variation);

            int newAttackDamage = definition.AttackRange.Evaluate(variation);

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

            movementController.SetCreatureSpeed(definition.SwimSpeed);

            actorHealth.Configure(newMaxHealth);

            currentAttackDamage = newAttackDamage;

            currentVariation = variation;

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

            movementController.RestoreBaseSpeed();

            actorHealth.Configure(baseMaxHealth);

            currentAttackDamage = baseAttackDamage;

            currentVariation = 0f;

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
