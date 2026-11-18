using System;
using MimicCell.Creatures;
using MimicCell.Gameplay;
using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(ActorHealth))]
    [RequireComponent(typeof(PlayerMovementController))]
    [RequireComponent(typeof(PlayerCreatureFormController))]
    public sealed class PrototypePlayer : MonoBehaviour
    {
        private Rigidbody2D attachedRigidbody;
        private ActorHealth actorHealth;
        private PlayerMovementController movementController;
        private PlayerCreatureFormController formController;

        public ActorHealth Health
        {
            get { return actorHealth; }
        }

        public bool IsCreatureForm
        {
            get 
            { 
                return formController != null && 
                         formController.IsCreatureForm; 
            }
        }

        public CreatureDefinition CurrentCreatureDefinition
        {
            get 
            { 
                return formController != null
                    ? formController.CurrentDefinition
                    : null; 
            }
        }

        public CreatureFoodTier CurrentFoodTier
        {
            get
            {
                return CurrentCreatureDefinition != null
                    ? CurrentCreatureDefinition.FoodTier
                    : CreatureFoodTier.Tier1;
            }
        }

        public int CurrentAttackDamage
        {
            get
            {
                return formController != null
                    ? formController.CurrentAttackDamage
                    : 0;
            }
        }

        public CreatureTraits CurrentTraits
        {
            get
            {
                return formController != null
                    ? formController.CurrentTraits
                    : CreatureTraits.None;
            }
        }

        public event Action<PrototypePlayer> Defeated;

        private void Awake()
        {
            ResolveComponents();
            actorHealth.Died += HandleDied;
        }

        private void OnDestroy()
        {
            if (actorHealth != null)
            {
                actorHealth.Died -= HandleDied;
            }
        }

        public bool TakeDamage(int amount)
        {
            return actorHealth.TakeDamage(amount);
        }

        public bool TryApplyCreatureForm(CreatureDefinition definition)
        {
            ResolveComponents();

            if (definition == null || formController == null)
            {
                return false;
            }

            return formController.ApplyCreatureForm(definition);
        }

        public void SetControlsEnabled(bool controlsEnabled)
        {
            movementController.SetControlEnabled(controlsEnabled);
            if (!controlsEnabled)
            {
                attachedRigidbody.linearVelocity = Vector2.zero;
            }
        }

        public void ResetAt(Vector3 spawnPosition)
        {
            transform.position = spawnPosition;
            attachedRigidbody.position = spawnPosition;
            attachedRigidbody.linearVelocity = Vector2.zero;
            actorHealth.RestoreFullHealth();
            SetControlsEnabled(true);
        }

        private void HandleDied(ActorHealth health)
        {
            Defeated?.Invoke(this);
        }

        private void ResolveComponents()
        {
            if (attachedRigidbody == null)
            {
                attachedRigidbody = GetComponent<Rigidbody2D>();
            }

            if (actorHealth == null)
            {
                actorHealth = GetComponent<ActorHealth>();
            }

            if (movementController == null)
            {
                movementController = GetComponent<PlayerMovementController>();
            }

            if (formController == null)
            {
                formController = GetComponent<PlayerCreatureFormController>();
            }
        }
    }
}
