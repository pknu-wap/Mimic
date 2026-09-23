using System;
using MimicCell.Gameplay;
using MimicCell.Player;
using UnityEngine;

namespace MimicCell.Creatures
{
    public enum CreatureDisposition
    {
        Passive,
        Timid,
        Aggressive
    }

    public enum CreatureState
    {
        Idle,
        Wander,
        Chase,
        Flee,
        Dead
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(ActorHealth))]
    public sealed class CreatureController : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string speciesName = "Unknown Creature";
        [SerializeField] private CreatureDisposition disposition = CreatureDisposition.Passive;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 1.5f;
        [SerializeField, Min(0f)] private float acceleration = 7f;
        [SerializeField, Min(0.1f)] private float wanderRadius = 4f;

        [Header("Perception")]
        [SerializeField, Min(0f)] private float perceptionRadius = 4f;
        [SerializeField, Min(0.02f)] private float decisionInterval = 0.15f;

        [Header("Contact")]
        [SerializeField, Min(0)] private int contactDamage = 1;
        [SerializeField, Min(0f)] private float contactCooldown = 0.8f;

        [Header("Prototype Bounds")]
        [SerializeField] private Vector2 movementBoundsMin = new Vector2(-21f, -8.5f);
        [SerializeField] private Vector2 movementBoundsMax = new Vector2(21f, 8.5f);

        [Header("References")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private CreatureState currentState;

        private Rigidbody2D attachedRigidbody;
        private BoxCollider2D attachedCollider;
        private ActorHealth actorHealth;
        private PrototypePlayer player;
        private Vector2 homePosition;
        private Vector2 wanderDirection = Vector2.right;
        private float stateTimeRemaining;
        private float decisionTimeRemaining;
        private float contactCooldownRemaining;

        public string SpeciesName
        {
            get { return speciesName; }
        }

        public CreatureState CurrentState
        {
            get { return currentState; }
        }

        public event Action<CreatureController, CreatureState> StateChanged;

        private void Awake()
        {
            ResolveComponents();
            homePosition = attachedRigidbody.position;

            attachedRigidbody.gravityScale = 0f;
            attachedRigidbody.freezeRotation = true;
            attachedRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;

            actorHealth.Died += HandleDied;
            ResolveVisualRoot();
            BeginWanderCycle();
        }

        private void OnDestroy()
        {
            if (actorHealth != null)
            {
                actorHealth.Died -= HandleDied;
            }
        }

        private void Update()
        {
            if (CurrentState == CreatureState.Dead)
            {
                return;
            }

            contactCooldownRemaining = Mathf.Max(0f, contactCooldownRemaining - Time.deltaTime);
            decisionTimeRemaining -= Time.deltaTime;

            if (decisionTimeRemaining <= 0f)
            {
                decisionTimeRemaining = decisionInterval;
                EvaluatePlayer();
            }

            if (CurrentState == CreatureState.Idle || CurrentState == CreatureState.Wander)
            {
                stateTimeRemaining -= Time.deltaTime;
                if (stateTimeRemaining <= 0f)
                {
                    BeginWanderCycle();
                }
            }
        }

        private void FixedUpdate()
        {
            if (CurrentState == CreatureState.Dead)
            {
                attachedRigidbody.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 movementDirection = GetMovementDirection();
            movementDirection = KeepDirectionInsideBounds(movementDirection);

            float speedMultiplier = GetStateSpeedMultiplier();
            Vector2 targetVelocity = movementDirection * moveSpeed * speedMultiplier;
            attachedRigidbody.linearVelocity = Vector2.MoveTowards(
                attachedRigidbody.linearVelocity,
                targetVelocity,
                acceleration * Time.fixedDeltaTime);

            ClampPositionToBounds();
            UpdateVisualFacing(movementDirection);
        }

        public void Configure(
            string newSpeciesName,
            CreatureDisposition newDisposition,
            PrototypePlayer targetPlayer,
            Transform newVisualRoot,
            int maxHealth,
            float newMoveSpeed,
            float newPerceptionRadius,
            float newWanderRadius,
            int newContactDamage)
        {
            ResolveComponents();
            speciesName = string.IsNullOrWhiteSpace(newSpeciesName) ? "Unknown Creature" : newSpeciesName;
            disposition = newDisposition;
            player = targetPlayer;
            visualRoot = newVisualRoot;
            moveSpeed = Mathf.Max(0f, newMoveSpeed);
            perceptionRadius = Mathf.Max(0f, newPerceptionRadius);
            wanderRadius = Mathf.Max(0.1f, newWanderRadius);
            contactDamage = Mathf.Max(0, newContactDamage);
            homePosition = attachedRigidbody.position;

            actorHealth.Configure(maxHealth);
            attachedCollider.enabled = true;
            RestoreVisualColor();
            BeginWanderCycle();
        }

        private void EvaluatePlayer()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<PrototypePlayer>();
            }

            bool canSeePlayer = player != null &&
                                player.Health != null &&
                                player.Health.IsAlive &&
                                ((Vector2)player.transform.position - attachedRigidbody.position).sqrMagnitude <=
                                perceptionRadius * perceptionRadius;

            if (canSeePlayer && disposition == CreatureDisposition.Aggressive)
            {
                SetState(CreatureState.Chase);
                return;
            }

            if (canSeePlayer && disposition == CreatureDisposition.Timid)
            {
                SetState(CreatureState.Flee);
                return;
            }

            if (CurrentState == CreatureState.Chase || CurrentState == CreatureState.Flee)
            {
                BeginWanderCycle();
            }
        }

        private Vector2 GetMovementDirection()
        {
            Vector2 currentPosition = attachedRigidbody.position;

            switch (CurrentState)
            {
                case CreatureState.Chase:
                    return DirectionToPlayer(currentPosition);

                case CreatureState.Flee:
                    return -DirectionToPlayer(currentPosition);

                case CreatureState.Wander:
                    if (Vector2.Distance(currentPosition, homePosition) > wanderRadius)
                    {
                        return (homePosition - currentPosition).normalized;
                    }

                    return wanderDirection;

                default:
                    return Vector2.zero;
            }
        }

        private Vector2 DirectionToPlayer(Vector2 currentPosition)
        {
            if (player == null)
            {
                return Vector2.zero;
            }

            Vector2 direction = (Vector2)player.transform.position - currentPosition;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.zero;
        }

        private float GetStateSpeedMultiplier()
        {
            switch (CurrentState)
            {
                case CreatureState.Chase:
                    return 1f;
                case CreatureState.Flee:
                    return 1.15f;
                case CreatureState.Wander:
                    return 0.45f;
                default:
                    return 0f;
            }
        }

        private void BeginWanderCycle()
        {
            if (UnityEngine.Random.value < 0.28f)
            {
                stateTimeRemaining = UnityEngine.Random.Range(0.5f, 1.4f);
                SetState(CreatureState.Idle);
                return;
            }

            wanderDirection = UnityEngine.Random.insideUnitCircle.normalized;
            if (wanderDirection.sqrMagnitude <= 0.001f)
            {
                wanderDirection = Vector2.right;
            }

            stateTimeRemaining = UnityEngine.Random.Range(1.2f, 3f);
            SetState(CreatureState.Wander);
        }

        private void SetState(CreatureState newState)
        {
            if (CurrentState == newState)
            {
                return;
            }

            currentState = newState;
            StateChanged?.Invoke(this, currentState);
        }

        private Vector2 KeepDirectionInsideBounds(Vector2 direction)
        {
            Vector2 position = attachedRigidbody.position;

            if (position.x <= movementBoundsMin.x + 0.2f && direction.x < 0f)
            {
                direction.x = Mathf.Abs(direction.x);
            }
            else if (position.x >= movementBoundsMax.x - 0.2f && direction.x > 0f)
            {
                direction.x = -Mathf.Abs(direction.x);
            }

            if (position.y <= movementBoundsMin.y + 0.2f && direction.y < 0f)
            {
                direction.y = Mathf.Abs(direction.y);
            }
            else if (position.y >= movementBoundsMax.y - 0.2f && direction.y > 0f)
            {
                direction.y = -Mathf.Abs(direction.y);
            }

            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }

        private void ClampPositionToBounds()
        {
            Vector2 position = attachedRigidbody.position;
            Vector2 clampedPosition = new Vector2(
                Mathf.Clamp(position.x, movementBoundsMin.x, movementBoundsMax.x),
                Mathf.Clamp(position.y, movementBoundsMin.y, movementBoundsMax.y));

            if (position != clampedPosition)
            {
                attachedRigidbody.position = clampedPosition;
            }
        }

        private void UpdateVisualFacing(Vector2 direction)
        {
            if (visualRoot == null || direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void ResolveVisualRoot()
        {
            if (visualRoot != null)
            {
                return;
            }

            Transform visualChild = transform.Find("Visual");
            visualRoot = visualChild != null ? visualChild : transform;
        }

        private void ResolveComponents()
        {
            if (attachedRigidbody == null)
            {
                attachedRigidbody = GetComponent<Rigidbody2D>();
            }

            if (attachedCollider == null)
            {
                attachedCollider = GetComponent<BoxCollider2D>();
            }

            if (actorHealth == null)
            {
                actorHealth = GetComponent<ActorHealth>();
            }

            ResolveVisualRoot();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (disposition != CreatureDisposition.Aggressive ||
                CurrentState == CreatureState.Dead ||
                contactCooldownRemaining > 0f ||
                contactDamage <= 0)
            {
                return;
            }

            PrototypePlayer hitPlayer = other.GetComponentInParent<PrototypePlayer>();
            if (hitPlayer == null || hitPlayer != player)
            {
                return;
            }

            if (hitPlayer.TakeDamage(contactDamage))
            {
                contactCooldownRemaining = contactCooldown;
            }
        }

        private void HandleDied(ActorHealth health)
        {
            attachedRigidbody.linearVelocity = Vector2.zero;
            attachedCollider.enabled = false;
            SetState(CreatureState.Dead);

            SpriteRenderer renderer = visualRoot != null ? visualRoot.GetComponent<SpriteRenderer>() : null;
            if (renderer != null)
            {
                renderer.color = new Color(0.35f, 0.35f, 0.35f, 0.4f);
            }
        }

        private void RestoreVisualColor()
        {
            SpriteRenderer renderer = visualRoot != null ? visualRoot.GetComponent<SpriteRenderer>() : null;
            if (renderer != null)
            {
                renderer.color = Color.white;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = disposition == CreatureDisposition.Aggressive ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, perceptionRadius);

            Gizmos.color = Color.cyan;
            Vector3 home = Application.isPlaying ? (Vector3)homePosition : transform.position;
            Gizmos.DrawWireSphere(home, wanderRadius);
        }
    }
}
