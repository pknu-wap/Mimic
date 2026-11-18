using MimicCell.Creatures;
using MimicCell.Player;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StatusEffectController), typeof(WasdSwimMovement))]
    public sealed class BerserkEffect : MonoBehaviour
    {
        [SerializeField] private StatusEffectDefinition berserkDefinition;
        [SerializeField, Min(0.1f)] private float detectionRadius = 5f;
        [SerializeField, Min(0.1f)] private float dashSpeed = 7f;
        [SerializeField, Min(0.1f)] private float stoppingDistance = 1.2f;


        private const float DashDuration = 3f;
        private const float Cooldown = 3f;
        private const float ScanInterval = 0.2f;

        private StatusEffectController statusController;
        private WasdSwimMovement movement;
        private CreatureController chaseTarget;
        private bool isDashing;
        private float dashEndsAt;
        private float nextDashAt;
        private float nextScanAt;

        private void Awake()
        {
            statusController = GetComponent<StatusEffectController>();
            movement = GetComponent<WasdSwimMovement>();
        }

        private void Update()
        {
            if (berserkDefinition == null)
                return;

            if (WasTestKeyPressed())
            {
                if (statusController.HasEffect(berserkDefinition))
                    statusController.RemoveEffect(berserkDefinition);
                else
                    statusController.ApplyEffect(berserkDefinition);
            }

            if (!statusController.HasEffect(berserkDefinition) ||
                !movement.isActiveAndEnabled)
            {
                EndDash();
                return;
            }

            if (isDashing)
{
    if (Time.time >= dashEndsAt ||
        chaseTarget == null ||
        !chaseTarget.isActiveAndEnabled ||
        chaseTarget.CurrentState == CreatureState.Dead)
    {
        EndDash();
        return;
    }

    FollowPrey();
    return;
}

if (Time.time < nextDashAt || Time.time < nextScanAt)
    return;

nextScanAt = Time.time + ScanInterval;

chaseTarget = FindNearestPrey();
if (chaseTarget == null)
    return;

isDashing = true;
dashEndsAt = Time.time + DashDuration;
FollowPrey();

Debug.Log("폭주 추적 시작");
        }

        private CreatureController FindNearestPrey()
{
    CreatureController closest = null;
    Vector2 origin = transform.position;
    float closestDistanceSquared = detectionRadius * detectionRadius;

    var creatures = FindObjectsByType<CreatureController>(
        FindObjectsSortMode.None);

    foreach (CreatureController creature in creatures)
    {
        CreatureDefinition definition = creature.Definition;
        if (!creature.isActiveAndEnabled ||
            definition == null ||
            creature.CurrentState == CreatureState.Dead)
        {
            continue;
        }

        CreatureFoodTier tier = definition.FoodTier;
        if (tier != CreatureFoodTier.Tier1 &&
            tier != CreatureFoodTier.Tier2)
        {
            continue;
        }

        Vector2 offset = (Vector2)creature.transform.position - origin;
        float distanceSquared = offset.sqrMagnitude;
        if (distanceSquared > closestDistanceSquared)
        {
            continue;
        }

        closest = creature;
        closestDistanceSquared = distanceSquared;
    }

    return closest;
}
private void FollowPrey()
{
    Vector2 offset = (Vector2)chaseTarget.transform.position -
                     (Vector2)transform.position;

    float distance = offset.magnitude;
    float remainingDistance = Mathf.Max(0f, distance - stoppingDistance);

    float approachSpeed = Mathf.Min(dashSpeed, remainingDistance * 4f);
    float speed = Mathf.Min(
        approachSpeed, remainingDistance / Time.fixedDeltaTime);

    Vector2 direction = distance > 0.001f
        ? offset / distance
        : Vector2.right;

    movement.StartForcedMovement(direction, speed);
}

        private void EndDash()
        {
            if (!isDashing)
                return;

            movement.StopForcedMovement();
            isDashing = false;
            chaseTarget = null;
            nextDashAt = Time.time + Cooldown;
            Debug.Log("폭주 돌진 종료");
        }

        private void OnDisable()
        {
            EndDash();
        }

        private static bool WasTestKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null &&
                   Keyboard.current.iKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.I);
#else
            return false;
#endif
        }
    }
}