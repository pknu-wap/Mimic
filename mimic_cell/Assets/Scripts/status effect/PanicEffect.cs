using MimicCell.Creatures;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using MimicCell.Player;
using MimicCell.StatusEffects;

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WasdSwimMovement))]
    public sealed class PanicEffect : MonoBehaviour
    {
        [Header("Panic Settings")]
        [SerializeField] private StatusEffectDefinition panicDefinition;
        [SerializeField, Min(0f)] private float dashSpeed = 7f;
        [SerializeField, Min(0f)] private float dashDuration = 2f;
        [SerializeField, Min(0f)] private float cooldown = 3f;
        [SerializeField, Range(0f, 90f)] private float randomAngle = 30f;

        private WasdSwimMovement movement;
        private StatusEffectController statusController;
        private bool isDashing;
        private float nextDashTime;
        private Coroutine dashCoroutine;

        private void Awake()
        {
            movement = GetComponent<WasdSwimMovement>();
            statusController = GetComponent<StatusEffectController>();
        }

private void Update()
{
    bool testPressed = false;

#if ENABLE_INPUT_SYSTEM
    if (Keyboard.current != null)
    {
        testPressed = Keyboard.current.yKey.wasPressedThisFrame;
    }
#elif ENABLE_LEGACY_INPUT_MANAGER
    testPressed = Input.GetKeyDown(KeyCode.Y);
#endif
//y키로 테스트용
    if (testPressed)
    {
        if (statusController == null)
        {
            statusController = GetComponent<StatusEffectController>();
        }

        if (statusController != null && panicDefinition != null)
        {
            statusController.ApplyEffect(panicDefinition);
            TryEscape((Vector2)transform.position + Vector2.right * 3f);
        }
    }
    if (statusController == null)
{
    statusController = GetComponent<StatusEffectController>();
}

if (panicDefinition != null &&
    statusController != null &&
    statusController.HasEffect(panicDefinition))
{
    DetectPredators();
}
}
private void DetectPredators()
{
    Camera gameCamera = Camera.main;

    if (gameCamera == null)
    {
        return;
    }

    CreatureController[] creatures =
        FindObjectsByType<CreatureController>(FindObjectsSortMode.None);

    foreach (CreatureController creature in creatures)
    {
        if (creature == null ||
            creature.Definition == null ||
            creature.CurrentState == CreatureState.Dead)
        {
            continue;
        }

        CreatureFoodTier tier = creature.Definition.FoodTier;

        // Tier2 또는 Tier3만 포식자로 인식
        if (tier != CreatureFoodTier.Tier2 &&
            tier != CreatureFoodTier.Tier3)
        {
            continue;
        }

        Vector3 viewportPosition =
            gameCamera.WorldToViewportPoint(creature.transform.position);

        // 카메라 화면 안에 있는지 확인
        bool isVisible =
            viewportPosition.z > 0f &&
            viewportPosition.x >= 0f &&
            viewportPosition.x <= 1f &&
            viewportPosition.y >= 0f &&
            viewportPosition.y <= 1f;

        if (!isVisible)
        {
            continue;
        }

        // 포식자 위치를 전달하여 도주
        TryEscape(creature.transform.position);
        return;
    }
}
        // 포식자의 위치를 전달받으면 도주 시도
        public void TryEscape(Vector2 predatorPosition)
        {
            if (statusController == null)
            {
                statusController = GetComponent<StatusEffectController>();
            }

            if (panicDefinition == null ||
                statusController == null ||
                !statusController.HasEffect(panicDefinition) ||
                isDashing ||
                Time.time < nextDashTime)
            {
                return;
            }

            Vector2 escapeDirection =
                (Vector2)transform.position - predatorPosition;

            if (escapeDirection.sqrMagnitude <= 0.001f)
            {
                escapeDirection = Random.insideUnitCircle.normalized;
            }

            float angle = Random.Range(-randomAngle, randomAngle);
            escapeDirection =
                Quaternion.Euler(0f, 0f, angle) * escapeDirection.normalized;

            dashCoroutine = StartCoroutine(EscapeRoutine(escapeDirection));
        }

        private IEnumerator EscapeRoutine(Vector2 direction)
        {
            isDashing = true;
            movement.StartForcedMovement(direction, dashSpeed);

            yield return new WaitForSeconds(dashDuration);

            movement.StopForcedMovement();
            isDashing = false;
            nextDashTime = Time.time + cooldown;
            dashCoroutine = null;
        }

        private void OnDisable()
        {
            if (dashCoroutine != null)
            {
                StopCoroutine(dashCoroutine);
                dashCoroutine = null;
            }

            if (movement != null && isDashing)
            {
                movement.StopForcedMovement();
            }

            isDashing = false;
        }
    }
}
