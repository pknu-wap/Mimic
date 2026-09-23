using System;
using MimicCell.Gameplay;
using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(ActorHealth))]
    [RequireComponent(typeof(PlayerMovementController))]
    public sealed class PrototypePlayer : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 20;

        private Rigidbody2D attachedRigidbody;
        private ActorHealth actorHealth;
        private PlayerMovementController movementController;

        public ActorHealth Health
        {
            get { return actorHealth; }
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

        public void Configure(int newMaxHealth)
        {
            ResolveComponents();
            maxHealth = Mathf.Max(1, newMaxHealth);
            actorHealth.Configure(maxHealth);
        }

        public bool TakeDamage(int amount)
        {
            return actorHealth.TakeDamage(amount);
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
        }
    }
}
