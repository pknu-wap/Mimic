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
        [SerializeField] private Animator creatureFormAnimator;

        [Header("Colliders")]
        [SerializeField] private Collider2D baseCollider;
        [SerializeField] private PolygonCollider2D creatureFormCollider;

        [Header("Base Combat")]
        [SerializeField, Min(0)] private int baseAttackDamage = 0;

        private ActorHealth actorHealth;
        private int baseMaxHealth;
        private int currentAttackDamage;
        private float currentVariation;
        private Rigidbody2D attachedRigidbody;
        private PlayerMovementController movementController;
        private PlayerCreatureTraitController traitController;
        private CreatureCombatTraitController combatTraitController;
        private CreatureDefinition currentDefinition;
        public CreatureDefinition CurrentDefinition => currentDefinition;
        public PolygonCollider2D CreatureFormCollider => creatureFormCollider;
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

            traitController = GetComponent<PlayerCreatureTraitController>();

            combatTraitController = GetComponent<CreatureCombatTraitController>();

            actorHealth = GetComponent<ActorHealth>();

            baseMaxHealth = actorHealth.MaxHealth;

            currentAttackDamage = baseAttackDamage;

            currentVariation = 0f;

            if (baseVisualRoot == null ||
                creatureFormRenderer == null ||
                creatureFormAnimator == null ||
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

            creatureFormAnimator.enabled = false;
            creatureFormAnimator.runtimeAnimatorController = null;
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

            if (!ApplyCreatureCollider(definition))
            {
                return false;
            }

            float variation = UnityEngine.Random.value;

            int newMaxHealth = definition.HealthRange.Evaluate(variation);

            int newAttackDamage = definition.AttackRange.Evaluate(variation);

            baseVisualRoot.SetActive(false);

            creatureFormAnimator.enabled = false;
            creatureFormAnimator.runtimeAnimatorController = null;

            creatureFormRenderer.sprite = form.Sprite;

            creatureFormRenderer.transform.localPosition = Vector3.zero;

            creatureFormRenderer.transform.localRotation = Quaternion.identity;

            creatureFormRenderer.transform.localScale =
                new Vector3(
                    form.VisualScale.x,
                    form.VisualScale.y,
                    1f);

            creatureFormRenderer.flipX = false;
            SetCreatureFacing(true, form);

            creatureFormAnimator.runtimeAnimatorController = form.MovementController;
            creatureFormAnimator.enabled = form.MovementController != null;

            creatureFormRenderer.gameObject.SetActive(true);

            if (creatureFormAnimator.enabled)
            {
                creatureFormAnimator.Rebind();
                creatureFormAnimator.Update(0f);
            }

            baseCollider.enabled = false;

            creatureFormCollider.enabled = true;

            movementController.SetCreatureSpeed(definition.SwimSpeed);

            actorHealth.Configure(newMaxHealth);

            currentAttackDamage = newAttackDamage;

            currentVariation = variation;

            currentDefinition = definition;

            if (combatTraitController != null)
            {
                combatTraitController.Configure(currentDefinition, currentVariation);
            }

            if (traitController != null)
            {
                traitController.ApplyTraits(currentDefinition);
            }

            FormChanged?.Invoke(currentDefinition);

            return true;
        }

        private bool ApplyCreatureCollider(CreatureDefinition definition)
        {
            CreatureController sourceCreature = definition.Prefab != null
                    ? definition.Prefab.GetComponent<CreatureController>()
                    : null;

            PolygonCollider2D sourceCollider = sourceCreature != null
                    ? sourceCreature.BodyCollider
                    : null;

            if (sourceCollider == null)
            {
                Debug.LogWarning(
                    "Creature body PolygonCollider2D is missing: " +
                    definition.DisplayName,
                    this);

                return false;
            }

            creatureFormCollider.pathCount = sourceCollider.pathCount;

            Vector2 colliderOffset = sourceCollider.offset;

            colliderOffset.x *= -1f;

            creatureFormCollider.offset = colliderOffset;

            creatureFormCollider.useDelaunayMesh = sourceCollider.useDelaunayMesh;

            for (int pathIndex = 0;
                 pathIndex < sourceCollider.pathCount;
                 pathIndex++)
            {
                Vector2[] path = sourceCollider.GetPath(pathIndex);

                for (int pointIndex = 0;
                     pointIndex < path.Length;
                     pointIndex++)
                {
                    path[pointIndex].x *= -1f;
                }

                creatureFormCollider.SetPath(pathIndex, path);
            }

            return true;
        }

        public void RestoreBaseForm()
        {
            if (traitController != null)
            {
                traitController.ClearTraits();
            }

            if (combatTraitController != null)
            {
                combatTraitController.Clear();
            }

            currentDefinition = null;

            creatureFormAnimator.enabled = false;
            creatureFormAnimator.runtimeAnimatorController = null;

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

        private void SetCreatureFacing(bool facingRight, CreaturePlayerFormSettings form)
        {
            bool shouldFlip = facingRight
                    ? form.FlipXToFaceRight
                    : !form.FlipXToFaceRight;

            Vector3 visualScale = creatureFormRenderer.transform.localScale;

            visualScale.x = Mathf.Abs(visualScale.x) * (shouldFlip ? -1f : 1f);

            creatureFormRenderer.transform.localScale = visualScale;

            creatureFormRenderer.flipX = false;
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

            SetCreatureFacing(movingRight, currentDefinition.PlayerForm);
        }
    }
}
