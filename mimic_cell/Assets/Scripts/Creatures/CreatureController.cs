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
    [RequireComponent(typeof(ActorHealth))]
    public sealed class CreatureController : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string speciesName = "Unknown Creature";
        [SerializeField] private CreatureDisposition disposition = CreatureDisposition.Passive;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 1.5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 2.5f;
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
        [SerializeField] private Collider2D attachedCollider;
        private ActorHealth actorHealth;
        private PrototypePlayer player;

        private CreatureMovementTraitController movementTraitController;

        private CreaturePerceptionTraitController perceptionTraitController;
        private CreatureCombatTraitController combatTraitController;

        private CreatureDefinition definition;
        private EcosystemDefinition ecosystem;
        private ICreatureEnvironment environment;

        private float instanceVariation;
        private int spawnRegionId;
        private int spawnStableSeed;
        private bool initializedFromDefinition;

        private Vector2 homePosition;
        private Vector2 wanderDirection = Vector2.right;

        private Vector2 groupMovementDirection;
        private bool hasGroupMovementDirection;

        private float stateTimeRemaining;
        private float decisionTimeRemaining;
        private float behaviorMemoryTimeRemaining;
        private float contactCooldownRemaining;
        private float sprintingSpeedBoostEndTime;
        private bool extinctionFrenzy;
        private bool extinctionCold;
        private Vector2 extinctionEscapeDirection;
        private Vector2 extinctionWanderDirection;
        private float extinctionWanderTime;
        private float coldPursuitMemory;
        private CreatureController coldPrey;
        private FoodChainTarget currentFoodChainTarget;

        public string SpeciesName
        {
            get { return speciesName; }
        }

        public CreatureDefinition Definition
        {
            get { return definition; }
        }

        public ActorHealth Health
        {
            get { return actorHealth; }
        }

        public PolygonCollider2D BodyCollider
        {
            get
            {
                return attachedCollider as PolygonCollider2D;
            }
        }

        public CreatureFoodTier CurrentFoodTier
        {
            get
            {
                return definition != null
                    ? definition.FoodTier
                    : CreatureFoodTier.Tier1;
            }
        }

        public CreatureTraits CurrentTraits
        {
            get
            {
                return definition != null
                    ? definition.Traits
                    : CreatureTraits.None;
            }
        }

        public float InstanceVariation
        {
            get { return instanceVariation; }
        }

        public int SpawnRegionId
        {
            get { return spawnRegionId; }
        }

        public int SpawnStableSeed
        {
            get { return spawnStableSeed; }
        }

        public bool IsInitializedFromDefinition
        {
            get { return initializedFromDefinition; }
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
            actorHealth.Damaged += HandleDamaged;
            ResolveVisualRoot();
            BeginWanderCycle();
        }

        private void OnDestroy()
        {
            if (actorHealth != null)
            {
                actorHealth.Died -= HandleDied;
                actorHealth.Damaged -= HandleDamaged;
            }
        }

        private void HandleDamaged(ActorHealth health, int damageAmount)
        {
            if (damageAmount <= 0 || !CurrentTraits.HasTrait(CreatureTraits.Sprinting))
            {
                return;
            }

            sprintingSpeedBoostEndTime = Time.time + CreatureTraitSettings.SprintingDuration;

            Debug.Log(
                "질주형 피격 가속 발동: " +
                speciesName +
                " / 속도 1.3배 / 3초",
                this);
        }

        private void Update()
        {
            if (CurrentState == CreatureState.Dead)
            {
                return;
            }

            contactCooldownRemaining = Mathf.Max(0f, contactCooldownRemaining - Time.deltaTime);

            behaviorMemoryTimeRemaining = Mathf.Max(0f, behaviorMemoryTimeRemaining - Time.deltaTime);

            if (extinctionFrenzy)
            {
                extinctionWanderTime -= Time.deltaTime;
                if (extinctionWanderTime <= 0f)
                {
                    PickExtinctionDirection();
                }
                return;
            }

            coldPursuitMemory = Mathf.Max(0f, coldPursuitMemory - Time.deltaTime);
            decisionTimeRemaining -= Time.deltaTime;

            if (decisionTimeRemaining <= 0f)
            {
                decisionTimeRemaining = decisionInterval;
                EvaluateFoodChain();
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

            if (movementTraitController != null)
            {
                movementDirection =
                    movementTraitController.AdjustDirection(
                        movementDirection,
                        attachedRigidbody.position,
                        attachedCollider.bounds.extents.y);
            }

            float currentSpeed = GetCurrentSpeed();

            bool constrainRoamingHabitat =
                ShouldConstrainRoamingToAllowedHabitats();

            if (constrainRoamingHabitat)
            {
                movementDirection =
                    ConstrainRoamingDirectionToAllowedHabitats(
                        movementDirection,
                        currentSpeed);
            }

            movementDirection = KeepDirectionInsideBounds(movementDirection);

            Vector2 targetVelocity = movementDirection * currentSpeed;
            Vector2 nextVelocity = Vector2.MoveTowards(
                attachedRigidbody.linearVelocity,
                targetVelocity,
                acceleration * Time.fixedDeltaTime);

            if (constrainRoamingHabitat)
            {
                nextVelocity =
                    ConstrainRoamingVelocityToAllowedHabitats(
                        nextVelocity);
            }

            attachedRigidbody.linearVelocity = nextVelocity;

            ClampPositionToBounds();
            UpdateVisualFacing(movementDirection);
        }

        public bool Initialize(
            CreatureDefinition newDefinition,
            EcosystemDefinition newEcosystem,
            CreatureSpawnContext spawnContext,
            ICreatureEnvironment newEnvironment,
            PrototypePlayer targetPlayer)
        {
            if (newDefinition == null)
            {
                Debug.LogError(
                    "CreatureDefinition is required.",
                    this);

                return false;
            }

            if (newEcosystem == null)
            {
                Debug.LogError(
                    "EcosystemDefinition is required.",
                    this);

                return false;
            }

            if (newEnvironment == null)
            {
                Debug.LogError(
                    "Creature environment is required.",
                    this);

                return false;
            }

            ResolveComponents();

            if (attachedCollider == null)
            {
                Debug.LogError(
                    "A Collider2D is required on the creature prefab.",
                    this);

                return false;
            }

            definition = newDefinition;
            ecosystem = newEcosystem;
            environment = newEnvironment;
            player = targetPlayer;

            if (movementTraitController != null)
            {
                movementTraitController.Configure(definition.Traits, definition.AllowedHabitats, environment);
            }

            if (perceptionTraitController != null)
            {
                perceptionTraitController.Configure(definition.Traits, ecosystem.PerceptionSettings);
            }

            instanceVariation = spawnContext.Variation;

            if (combatTraitController != null)
            {
                combatTraitController.Configure(definition, instanceVariation);
            }

            spawnRegionId = spawnContext.RegionId;
            spawnStableSeed = spawnContext.StableSeed;

            attachedRigidbody.position = spawnContext.Position;
            attachedRigidbody.linearVelocity = Vector2.zero;

            ApplyDefinitionStats();
            ApplyDefinitionScale();

            homePosition = attachedRigidbody.position;
            attachedCollider.enabled = true;

            RestoreVisualColor();
            BeginWanderCycle();

            initializedFromDefinition = true;
            return true;
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

        public void SetGroupMovementDirection(Vector2 direction)
        {
            if (CurrentState == CreatureState.Dead)
            {
                return;
            }

            if (direction.sqrMagnitude <= 0.001f)
            {
                ClearGroupMovementDirection();
                return;
            }

            groupMovementDirection = direction.normalized;
            hasGroupMovementDirection = true;
        }

        public void ClearGroupMovementDirection()
        {
            groupMovementDirection = Vector2.zero;
            hasGroupMovementDirection = false;
        }

        public void SetExtinctionBehavior(bool frenzy, Vector2 escapeDirection, bool cold)
        {
            if (CurrentState == CreatureState.Dead)
            {
                extinctionFrenzy = false;
                extinctionCold = false;
                return;
            }

            if (frenzy && !extinctionFrenzy)
            {
                PickExtinctionDirection();
            }
            else if (!frenzy && extinctionFrenzy)
            {
                BeginWanderCycle();
            }

            extinctionFrenzy = frenzy;
            extinctionEscapeDirection = escapeDirection;
            extinctionCold = cold;
            if (!cold)
            {
                coldPrey = null;
                coldPursuitMemory = 0f;
            }
        }

        private void PickExtinctionDirection()
        {
            extinctionWanderDirection = UnityEngine.Random.insideUnitCircle.normalized;
            if (extinctionWanderDirection.sqrMagnitude < 0.001f)
            {
                extinctionWanderDirection = Vector2.right;
            }
            extinctionWanderTime = UnityEngine.Random.Range(0.45f, 1.35f);
        }

        private void ApplyDefinitionStats()
        {
            speciesName =
                string.IsNullOrWhiteSpace(definition.DisplayName)
                    ? definition.CreatureId
                    : definition.DisplayName;

            disposition = definition.Disposition;

            moveSpeed = Mathf.Max(
                0f,
                definition.SwimSpeed);

            sprintSpeed = Mathf.Max(
                moveSpeed,
                definition.SprintSpeed);

            wanderRadius = Mathf.Max(
                0.1f,
                definition.WanderRadius);

            float basePerceptionRadius =
                definition.GetPerceptionRadius(
                    ecosystem.PerceptionSettings
                        .CommonPerceptionRadius);

            perceptionRadius =
                perceptionTraitController != null
                    ? perceptionTraitController
                        .AdjustPerceptionRadius(
                            basePerceptionRadius)
                    : basePerceptionRadius;

            decisionInterval = Mathf.Max(
                0.02f,
                ecosystem.PerceptionSettings
                    .DecisionInterval);

            int maxHealth =
                definition.HealthRange.Evaluate(
                    instanceVariation);

            contactDamage =
                definition.AttackRange.Evaluate(
                    instanceVariation);

            actorHealth.Configure(maxHealth);
        }

        private void ApplyDefinitionScale()
        {
            Vector2 minimum =
                definition.DesignSizeRange.Min;

            Vector2 maximum =
                definition.DesignSizeRange.Max;

            Vector2 selectedSize =
                definition.DesignSizeRange.Evaluate(
                    instanceVariation);

            float referenceWidth =
                (minimum.x + maximum.x) * 0.5f;

            float referenceHeight =
                (minimum.y + maximum.y) * 0.5f;

            float scaleSum = 0f;
            int scaleAxisCount = 0;

            if (referenceWidth > 0.001f)
            {
                scaleSum +=
                    selectedSize.x / referenceWidth;

                scaleAxisCount++;
            }

            if (referenceHeight > 0.001f)
            {
                scaleSum +=
                    selectedSize.y / referenceHeight;

                scaleAxisCount++;
            }

            float uniformScale =
                scaleAxisCount > 0
                    ? scaleSum / scaleAxisCount
                    : 1f;

            transform.localScale =
                Vector3.one *
                Mathf.Max(0.01f, uniformScale);
        }

        private void EvaluateFoodChain()
        {
            float radius = perceptionRadius * (extinctionCold ? 1.3f : 1f);

            FoodChainTarget closestPrey = default;
            FoodChainTarget closestThreat = default;

            float closestPreyDistance = float.PositiveInfinity;

            float closestThreatDistance = float.PositiveInfinity;

            Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(attachedRigidbody.position, radius);

            foreach (Collider2D candidateCollider in nearbyColliders)
            {
                if (!FoodChainRules.TryResolveTarget(candidateCollider, out FoodChainTarget candidate))
                {
                    continue;
                }

                if (candidate.Owner == this || candidate.Health == actorHealth)
                {
                    continue;
                }

                Vector2 offset = (Vector2)candidate.Transform.position - attachedRigidbody.position;

                float distanceSquared = offset.sqrMagnitude;

                if (FoodChainRules.MustFleeFrom(CurrentFoodTier, candidate.FoodTier))
                {
                    if (distanceSquared < closestThreatDistance)
                    {
                        closestThreat = candidate;
                        closestThreatDistance = distanceSquared;
                    }

                    continue;
                }

                if (disposition ==
                        CreatureDisposition.Aggressive &&
                    FoodChainRules.CanAttack(CurrentFoodTier, candidate.FoodTier))
                {
                    if (distanceSquared < closestPreyDistance)
                    {
                        closestPrey = candidate;
                        closestPreyDistance = distanceSquared;
                    }
                }
            }

            if (closestThreat.IsValid)
            {
                coldPrey = null;

                currentFoodChainTarget = closestThreat;

                if (extinctionCold)
                {
                    coldPursuitMemory = 1.2f;
                }

                RefreshBehaviorMemory(CreatureState.Flee);
                SetState(CreatureState.Flee);
                return;
            }

            if (closestPrey.IsValid)
            {
                coldPrey = null;

                currentFoodChainTarget = closestPrey;

                if (extinctionCold)
                {
                    coldPursuitMemory = 1.2f;
                }

                RefreshBehaviorMemory(CreatureState.Chase);
                SetState(CreatureState.Chase);
                return;
            }

            if (extinctionCold &&
                disposition ==
                    CreatureDisposition.Aggressive &&
                definition != null &&
                definition.FoodTier >=
                    CreatureFoodTier.Tier2)
            {
                CreatureController candidate = FindSmallerSameTierPrey(radius);

                if (candidate != null)
                {
                    currentFoodChainTarget = default;
                    coldPrey = candidate;
                    coldPursuitMemory = 1.2f;

                    SetState(CreatureState.Chase);
                    return;
                }
            }

            if (extinctionCold &&
                coldPursuitMemory > 0f &&
                (CurrentState == CreatureState.Chase ||
                 CurrentState == CreatureState.Flee))
            {
                return;
            }

            if ((CurrentState == CreatureState.Chase ||
                 CurrentState == CreatureState.Flee) &&
                behaviorMemoryTimeRemaining > 0f &&
                currentFoodChainTarget.IsValid)
            {
                return;
            }

            behaviorMemoryTimeRemaining = 0f;
            coldPrey = null;
            currentFoodChainTarget = default;

            if (CurrentState == CreatureState.Chase ||
                CurrentState == CreatureState.Flee)
            {
                BeginWanderCycle();
            }
        }

        private CreatureController FindSmallerSameTierPrey(float radius)
        {
            Collider2D[] nearby = Physics2D.OverlapCircleAll(attachedRigidbody.position, radius);
            CreatureController closest = null;
            float closestDistance = radius * radius;
            foreach (Collider2D candidateCollider in nearby)
            {
                CreatureController candidate = candidateCollider.GetComponentInParent<CreatureController>();
                if (candidate == null || candidate == this || candidate.definition == null ||
                    candidate.CurrentState == CreatureState.Dead ||
                    candidate.definition.FoodTier != definition.FoodTier ||
                    candidate.definition.BodySize >= definition.BodySize)
                {
                    continue;
                }

                float distance = ((Vector2)candidate.transform.position - attachedRigidbody.position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closest = candidate;
                    closestDistance = distance;
                }
            }
            return closest;
        }

        private Vector2 GetMovementDirection()
        {
            if (extinctionFrenzy)
            {
                // The hazard influence is deliberately weaker than the random heading.
                Vector2 direction = extinctionWanderDirection + extinctionEscapeDirection * 0.38f;
                return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.zero;
            }

            Vector2 currentPosition = attachedRigidbody.position;

            switch (CurrentState)
            {
                case CreatureState.Chase:
                    return DirectionToFoodChainTarget(currentPosition);

                case CreatureState.Flee:
                    return -DirectionToFoodChainTarget(currentPosition);

                case CreatureState.Idle:
                    return hasGroupMovementDirection
                        ? groupMovementDirection
                        : Vector2.zero;

                case CreatureState.Wander:
                    if (hasGroupMovementDirection)
                    {
                        return groupMovementDirection;
                    }

                    if (Vector2.Distance(currentPosition, homePosition) > wanderRadius)
                    {
                        return (homePosition - currentPosition).normalized;
                    }

                    return wanderDirection;

                default:
                    return Vector2.zero;
            }
        }

        private Vector2 DirectionToFoodChainTarget(Vector2 currentPosition)
        {
            if (coldPrey != null &&
                coldPrey.CurrentState !=
                    CreatureState.Dead)
            {
                Vector2 toColdPrey = (Vector2)coldPrey.transform.position - currentPosition;

                return toColdPrey.sqrMagnitude > 0.001f
                    ? toColdPrey.normalized
                    : Vector2.zero;
            }

            if (!currentFoodChainTarget.IsValid)
            {
                return Vector2.zero;
            }

            Vector2 direction = (Vector2)currentFoodChainTarget.Transform.position - currentPosition;

            return direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector2.zero;
        }

        private float GetCurrentSpeed()
        {
            ExtinctionStatusEffects status = GetComponent<ExtinctionStatusEffects>();

            float statusSpeedMultiplier = status != null
                    ? status.SpeedMultiplier
                    : 1f;

            float stateSpeed;

            if (extinctionFrenzy)
            {
                stateSpeed = sprintSpeed;
            }
            else
            {
                switch (CurrentState)
                {
                    case CreatureState.Chase:
                    case CreatureState.Flee:
                        stateSpeed = sprintSpeed;
                        break;

                    case CreatureState.Idle: 
                        stateSpeed = hasGroupMovementDirection 
                            ? moveSpeed
                            : 0f;
                        break;

                    case CreatureState.Wander:
                        stateSpeed = moveSpeed;
                        break;

                    default:
                        stateSpeed = 0f;
                        break;
                }
            }

            bool sprintingBoostActive =
                Time.time <
                    sprintingSpeedBoostEndTime && 
                    CurrentTraits.HasTrait(CreatureTraits.Sprinting);

            float traitSpeedMultiplier = sprintingBoostActive
                    ? CreatureTraitSettings.SprintingSpeedMultiplier
                    : 1f;

            return stateSpeed * statusSpeedMultiplier * traitSpeedMultiplier;
        }

        private void RefreshBehaviorMemory(CreatureState behaviorState)
        {
            behaviorMemoryTimeRemaining = perceptionTraitController != null
                    ? perceptionTraitController.GetBehaviorMemoryDuration(CurrentFoodTier, behaviorState)
                    : 0f;
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

            if (environment != null)
            {
                Vector2 normalizedDirection =
                    direction.sqrMagnitude > 1f
                        ? direction.normalized
                        : direction;

                Vector2 probePosition =
                    position + normalizedDirection * 0.5f;

                if (environment.IsInsidePlayableArea(probePosition))
                {
                    return normalizedDirection;
                }

                Vector2 returnDirection =
                    homePosition - position;

                return returnDirection.sqrMagnitude > 0.001f
                    ? returnDirection.normalized
                    : Vector2.zero;
            }

            if (position.x <= movementBoundsMin.x + 0.2f &&
                direction.x < 0f)
            {
                direction.x = Mathf.Abs(direction.x);
            }
            else if (
                position.x >= movementBoundsMax.x - 0.2f &&
                direction.x > 0f)
            {
                direction.x = -Mathf.Abs(direction.x);
            }

            if (position.y <= movementBoundsMin.y + 0.2f &&
                direction.y < 0f)
            {
                direction.y = Mathf.Abs(direction.y);
            }
            else if (
                position.y >= movementBoundsMax.y - 0.2f &&
                direction.y > 0f)
            {
                direction.y = -Mathf.Abs(direction.y);
            }

            return direction.sqrMagnitude > 1f
                ? direction.normalized
                : direction;
        }

        private bool ShouldConstrainRoamingToAllowedHabitats()
        {
            if (definition == null ||
                environment == null ||
                attachedCollider == null ||
                extinctionFrenzy ||
                definition.AllowedHabitats ==
                    CreatureHabitatMask.None)
            {
                return false;
            }

            if (CurrentState == CreatureState.Wander)
            {
                return true;
            }

            return CurrentState == CreatureState.Idle &&
                hasGroupMovementDirection;
        }

        private Vector2 ConstrainRoamingDirectionToAllowedHabitats(
            Vector2 direction,
            float currentSpeed)
        {
            Vector2 position = attachedRigidbody.position;

            if (!IsBodyInsideAllowedHabitats(position))
            {
                return GetAllowedHabitatRecoveryDirection(
                    position,
                    direction);
            }

            if (direction.sqrMagnitude <= 0.001f)
            {
                return Vector2.zero;
            }

            float probeDistance = Mathf.Max(
                0.25f,
                currentSpeed * Time.fixedDeltaTime);

            Vector2 normalizedDirection = direction.normalized;
            Vector2 probePosition =
                position +
                normalizedDirection * probeDistance;

            if (IsBodyInsideAllowedHabitats(probePosition))
            {
                return direction;
            }

            Vector2 horizontalDirection =
                new Vector2(direction.x, 0f);

            if (horizontalDirection.sqrMagnitude > 0.001f)
            {
                horizontalDirection.Normalize();

                Vector2 horizontalProbe =
                    position +
                    horizontalDirection * probeDistance;

                if (IsBodyInsideAllowedHabitats(
                        horizontalProbe))
                {
                    return horizontalDirection;
                }
            }

            return Vector2.zero;
        }

        private Vector2 ConstrainRoamingVelocityToAllowedHabitats(
            Vector2 velocity)
        {
            if (velocity.sqrMagnitude <= 0.001f)
            {
                return Vector2.zero;
            }

            Vector2 position = attachedRigidbody.position;

            if (!IsBodyInsideAllowedHabitats(position))
            {
                Vector2 recoveryDirection =
                    GetAllowedHabitatRecoveryDirection(
                        position,
                        velocity);

                float recoverySpeed = Vector2.Dot(
                    velocity,
                    recoveryDirection);

                return recoverySpeed > 0f
                    ? recoveryDirection * recoverySpeed
                    : Vector2.zero;
            }

            Vector2 predictedPosition =
                position +
                velocity * Time.fixedDeltaTime;

            if (IsBodyInsideAllowedHabitats(predictedPosition))
            {
                return velocity;
            }

            Vector2 horizontalVelocity =
                new Vector2(velocity.x, 0f);

            Vector2 horizontalPosition =
                position +
                horizontalVelocity * Time.fixedDeltaTime;

            return IsBodyInsideAllowedHabitats(horizontalPosition)
                ? horizontalVelocity
                : Vector2.zero;
        }

        private bool IsBodyInsideAllowedHabitats(
            Vector2 bodyPosition)
        {
            Bounds bounds = attachedCollider.bounds;

            Vector2 colliderCenterOffset =
                (Vector2)bounds.center -
                attachedRigidbody.position;

            Vector2 center =
                bodyPosition + colliderCenterOffset;

            float halfWidth = Mathf.Max(
                0.01f,
                bounds.extents.x - 0.02f);

            float halfHeight = Mathf.Max(
                0.01f,
                bounds.extents.y - 0.02f);

            return
                IsAllowedHabitatAt(center) &&
                IsAllowedHabitatAt(
                    center + new Vector2(0f, halfHeight)) &&
                IsAllowedHabitatAt(
                    center - new Vector2(0f, halfHeight)) &&
                IsAllowedHabitatAt(
                    center + new Vector2(halfWidth, halfHeight)) &&
                IsAllowedHabitatAt(
                    center + new Vector2(halfWidth, -halfHeight)) &&
                IsAllowedHabitatAt(
                    center + new Vector2(-halfWidth, halfHeight)) &&
                IsAllowedHabitatAt(
                    center - new Vector2(halfWidth, halfHeight));
        }

        private bool IsAllowedHabitatAt(Vector2 position)
        {
            CreatureHabitatMask habitat =
                environment.GetHabitatAt(position);

            return habitat != CreatureHabitatMask.None &&
                (definition.AllowedHabitats & habitat) != 0;
        }

        private Vector2 GetAllowedHabitatRecoveryDirection(
            Vector2 position,
            Vector2 originalDirection)
        {
            Bounds bounds = attachedCollider.bounds;

            Vector2 colliderCenterOffset =
                (Vector2)bounds.center -
                attachedRigidbody.position;

            Vector2 colliderCenter =
                position + colliderCenterOffset;

            float halfHeight = Mathf.Max(
                0.01f,
                bounds.extents.y - 0.02f);

            bool topAllowed = IsAllowedHabitatAt(
                colliderCenter + Vector2.up * halfHeight);

            bool bottomAllowed = IsAllowedHabitatAt(
                colliderCenter + Vector2.down * halfHeight);

            if (topAllowed != bottomAllowed)
            {
                float verticalDirection =
                    topAllowed ? 1f : -1f;

                Vector2 edgeRecoveryDirection =
                    new Vector2(
                        originalDirection.x,
                        verticalDirection);

                return edgeRecoveryDirection.sqrMagnitude > 0.001f
                    ? edgeRecoveryDirection.normalized
                    : Vector2.up * verticalDirection;
            }

            CreatureHabitatMask currentHabitat =
                environment.GetHabitatAt(colliderCenter);

            int currentDepthIndex =
                GetHabitatDepthIndex(currentHabitat);

            int nearestAllowedDepthIndex =
                GetNearestAllowedHabitatDepthIndex(
                    currentDepthIndex,
                    definition.AllowedHabitats);

            if (currentDepthIndex >= 0 &&
                nearestAllowedDepthIndex >= 0 &&
                currentDepthIndex != nearestAllowedDepthIndex)
            {
                float verticalDirection =
                    nearestAllowedDepthIndex < currentDepthIndex
                        ? 1f
                        : -1f;

                Vector2 recoveryDirection =
                    new Vector2(
                        originalDirection.x,
                        verticalDirection);

                return recoveryDirection.sqrMagnitude > 0.001f
                    ? recoveryDirection.normalized
                    : Vector2.up * verticalDirection;
            }

            Vector2 directionToHome =
                homePosition - position;

            return directionToHome.sqrMagnitude > 0.001f
                ? directionToHome.normalized
                : Vector2.zero;
        }

        private static int GetNearestAllowedHabitatDepthIndex(
            int currentDepthIndex,
            CreatureHabitatMask allowedHabitats)
        {
            int nearestIndex = -1;
            int nearestDistance = int.MaxValue;

            for (int index = 0; index < 5; index++)
            {
                CreatureHabitatMask habitat =
                    GetHabitatForDepthIndex(index);

                if ((allowedHabitats & habitat) == 0)
                {
                    continue;
                }

                int distance = currentDepthIndex >= 0
                    ? Mathf.Abs(index - currentDepthIndex)
                    : index;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = index;
                }
            }

            return nearestIndex;
        }

        private static int GetHabitatDepthIndex(
            CreatureHabitatMask habitat)
        {
            switch (habitat)
            {
                case CreatureHabitatMask.OceanAA:
                    return 0;

                case CreatureHabitatMask.OceanAB:
                    return 1;

                case CreatureHabitatMask.OceanBB:
                    return 2;

                case CreatureHabitatMask.OceanBC:
                    return 3;

                case CreatureHabitatMask.OceanCC:
                    return 4;

                default:
                    return -1;
            }
        }

        private static CreatureHabitatMask GetHabitatForDepthIndex(
            int depthIndex)
        {
            switch (depthIndex)
            {
                case 0:
                    return CreatureHabitatMask.OceanAA;

                case 1:
                    return CreatureHabitatMask.OceanAB;

                case 2:
                    return CreatureHabitatMask.OceanBB;

                case 3:
                    return CreatureHabitatMask.OceanBC;

                case 4:
                    return CreatureHabitatMask.OceanCC;

                default:
                    return CreatureHabitatMask.None;
            }
        }

        private void ClampPositionToBounds()
        {
            if (environment != null)
            {
                return;
            }

            Vector2 position = attachedRigidbody.position;

            Vector2 clampedPosition = new Vector2(
                Mathf.Clamp(
                    position.x,
                    movementBoundsMin.x,
                    movementBoundsMax.x),
                Mathf.Clamp(
                    position.y,
                    movementBoundsMin.y,
                    movementBoundsMax.y));

            if (position != clampedPosition)
            {
                attachedRigidbody.position = clampedPosition;
            }
        }

        private void UpdateVisualFacing(Vector2 direction)
        {
            if (visualRoot == null ||
                direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            visualRoot.localRotation =
                Quaternion.identity;

            if (Mathf.Abs(direction.x) <= 0.01f)
            {
                return;
            }

            Vector3 visualScale =
                visualRoot.localScale;

            visualScale.x =
                Mathf.Abs(visualScale.x) *
                (direction.x >= 0f ? 1f : -1f);

            visualRoot.localScale =
                visualScale;
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
                attachedCollider = GetComponentInChildren<Collider2D>(true);
            }

            if (actorHealth == null)
            {
                actorHealth = GetComponent<ActorHealth>();
            }

            if (movementTraitController == null)
            {
                movementTraitController = GetComponent<CreatureMovementTraitController>();
            }

            if (perceptionTraitController == null)
            {
                perceptionTraitController = GetComponent<CreaturePerceptionTraitController>();
            }

            if (combatTraitController == null)
            {
                combatTraitController = GetComponent<CreatureCombatTraitController>();
            }

            ResolveVisualRoot();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (CurrentState == CreatureState.Dead ||
                contactCooldownRemaining > 0f ||
                contactDamage <= 0)
            {
                return;
            }

            if (!CreatureColliderRole.IsBodyCollider(other))
            {
                return;
            }

            if (extinctionCold &&
                coldPrey != null &&
                other.GetComponentInParent<CreatureController>()
                    == coldPrey)
            {
                ActorHealth preyHealth = coldPrey.Health;

                int coldDamage = 
                    CreatureCombatRules.CalculateDamage(
                        contactDamage,
                        CurrentFoodTier,
                        coldPrey.CurrentFoodTier,
                        coldPrey.CurrentTraits);

                ActorDamageContext coldDamageContext =
                    new ActorDamageContext(
                        coldDamage,
                        actorHealth,
                        ActorDamageType.Contact);

                if (preyHealth != null && preyHealth.TakeDamage(coldDamageContext))
                {
                    contactCooldownRemaining = contactCooldown;
                }

                return;
            }

            if (disposition !=
                    CreatureDisposition.Aggressive ||
                CurrentState != CreatureState.Chase)
            {
                return;
            }

            if (!FoodChainRules.TryResolveTarget(other, out FoodChainTarget hitTarget))
            {
                return;
            }

            if (hitTarget.Health == actorHealth)
            {
                return;
            }

            if (!FoodChainRules.CanAttack(CurrentFoodTier, hitTarget.FoodTier))
            {
                return;
            }

            int finalDamage = 
                CreatureCombatRules.CalculateDamage(
                    contactDamage,
                    CurrentFoodTier,
                    hitTarget.FoodTier,
                    hitTarget.Traits);

            ActorDamageContext damageContext =
                new ActorDamageContext(
                    finalDamage,
                    actorHealth,
                    ActorDamageType.Contact);

            if (hitTarget.Health.TakeDamage(damageContext))
            {
                if (hitTarget.Owner is PrototypePlayer)
                {
                    Debug.Log(
                        "복제 플레이어 피격: " +
                        speciesName +
                        " / 기본 피해량: " +
                        contactDamage +
                        " / 실제 피해량: " +
                        finalDamage +
                        " / 남은 체력: " +
                        hitTarget.Health.CurrentHealth +
                        "/" +
                        hitTarget.Health.MaxHealth,
                        this);
                }

                contactCooldownRemaining = contactCooldown;
            }
        }

        private void HandleDied(ActorHealth health)
        {
            attachedRigidbody.linearVelocity = Vector2.zero;

            attachedCollider.enabled = false;

            SetState(CreatureState.Dead);

            Destroy(gameObject);
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
