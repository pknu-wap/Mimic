using MimicCell.StatusEffects;
using MimicCell.Gameplay;
using UnityEngine;
using MimicCell.Creatures;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorHealth))]
    public sealed class PlayerStaminaController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float maxStamina = 100f;
        [SerializeField, Min(0f)] private float recoveryPerSecond = 45f;
        [SerializeField, Min(0f)] private float recoveryDelay = 0.3f;

        [SerializeField] private float currentStamina;

        private ActorHealth health;
        private PlayerCreatureFormController formController;
private float storedStaminaRatio = 1f;
        private float recoveryStartsAt;
        private bool sprintDepleted;
        [SerializeField] private StatusEffectDefinition exhaustionDefinition;
        [SerializeField] private StatusEffectDefinition dopamineDefinition;
private float lastSprintAt = float.NegativeInfinity;
        [SerializeField, Min(0f)] private float baseFormSwimSpeed = 4f;
        
private StatusEffectController statusController;

public bool IsExhausted =>
    statusController != null &&
    exhaustionDefinition != null &&
    statusController.HasEffect(exhaustionDefinition);


private bool HasDopamine =>
    statusController != null &&
    dopamineDefinition != null &&
    statusController.HasEffect(dopamineDefinition);

        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;

       private void Awake()
{
    health = GetComponent<ActorHealth>();
    formController = GetComponent<PlayerCreatureFormController>();
    statusController = GetComponent<StatusEffectController>();

    maxStamina = 0f;
    currentStamina = 0f;
}

private void OnEnable()
{
    if (formController == null)
        return;

    formController.FormChanged += HandleFormChanged;
    HandleFormChanged(formController.CurrentDefinition);
}

private void OnDisable()
{
    if (formController != null)
        formController.FormChanged -= HandleFormChanged;
}

private void HandleFormChanged(CreatureDefinition definition)
{
    if (maxStamina > 0f)
        storedStaminaRatio = Mathf.Clamp01(currentStamina / maxStamina);

    if (definition == null)
    {
        maxStamina = 0f;
        currentStamina = 0f;
        recoveryPerSecond = 0f;
        recoveryDelay = 0f;
        return;
    }

    switch (definition.BodySize)
    {
        case CreatureBodySize.Small:
            maxStamina = 100f;
            recoveryPerSecond = 15f;
            recoveryDelay = 1.5f;
            break;

        case CreatureBodySize.Medium:
            maxStamina = 120f;
            recoveryPerSecond = 12f;
            recoveryDelay = 3f;
            break;

        case CreatureBodySize.Large:
            maxStamina = 150f;
            recoveryPerSecond = 10f;
            recoveryDelay = 5f;
            break;

        default:
            return;
    }
bool isSprintingType =
    (definition.Traits & CreatureTraits.Sprinting) != CreatureTraits.None;

if (isSprintingType)
    recoveryDelay *= 2f;
    currentStamina = maxStamina * storedStaminaRatio;
    recoveryStartsAt = Time.time + recoveryDelay;

    Debug.Log(
        $"[스태미나] {definition.DisplayName} · " +
        $"{currentStamina:F1}/{maxStamina:F1}",
        this);
}

       private void LateUpdate()
{
    if (!health.IsAlive)
        return;

    // 질주 소모 직후에는 회복하지 않음.
    if (Time.time <= lastSprintAt + Time.fixedDeltaTime)
        return;

    if (!HasDopamine && Time.time < recoveryStartsAt)
        return;

    currentStamina = Mathf.MoveTowards(
        currentStamina,
        maxStamina,
        recoveryPerSecond * Time.deltaTime);
}
private void ApplyExhaustionIfDepleted()
{
    CreatureDefinition definition = formController.CurrentDefinition;

    if (currentStamina > 0f ||
        definition == null ||
        statusController == null ||
        exhaustionDefinition == null ||
        IsExhausted)
    {
        return;
    }

   float calculatedDuration =
    Mathf.Abs((definition.SwimSpeed - baseFormSwimSpeed) * 0.8f) + 1f;

float duration = Mathf.Max(3f, calculatedDuration * 2f);

    statusController.ApplyEffect(exhaustionDefinition, duration);

    Debug.Log(
        $"[탈진] 지속시간 {duration:F2}초 · " +
        $"생물 속도 {definition.SwimSpeed}, 기본 속도 {baseFormSwimSpeed}",
        this);
}
public void ReleaseSprint()
{
    sprintDepleted = false;
}

public bool TrySprint(float deltaTime)
{
    if (!isActiveAndEnabled || !health.IsAlive ||
    formController.CurrentDefinition == null ||
    IsExhausted ||
    sprintDepleted || deltaTime <= 0f)
{
    return false;
}

    if (currentStamina <= 0f)
    {
        sprintDepleted = true;
        return false;
    }

    CreatureDefinition definition = formController.CurrentDefinition;
    float drainPerSecond;

    switch (definition.BodySize)
    {
        case CreatureBodySize.Small:
            drainPerSecond = 25f;
            break;

        case CreatureBodySize.Medium:
            drainPerSecond = 40f;
            break;

        case CreatureBodySize.Large:
            drainPerSecond = 75f;
            break;

        default:
            return false;
    }
bool isSprintingType =
    (definition.Traits & CreatureTraits.Sprinting) != CreatureTraits.None;

if (isSprintingType)
    drainPerSecond *= 0.8f;

if (HasDopamine)
    drainPerSecond *= 0.6f;
    
    currentStamina = Mathf.Max(
        0f, currentStamina - drainPerSecond * deltaTime);

lastSprintAt = Time.time;
    recoveryStartsAt = Time.time + recoveryDelay;

    if (currentStamina <= 0f)
    sprintDepleted = true;

ApplyExhaustionIfDepleted();

return !IsExhausted;
}        public bool TrySpend(float amount)
        {
            if (!isActiveAndEnabled || health == null || !health.IsAlive ||
                amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            {
                return false;
            }

            if (currentStamina < amount)
                return false;

            currentStamina -= amount;
            recoveryStartsAt = Time.time + recoveryDelay;

            ApplyExhaustionIfDepleted();


            return true;
        }

        [ContextMenu("Test Stamina - Apply Dopamine")]
private void TestDopamine()
{
    if (!Application.isPlaying ||
        statusController == null || dopamineDefinition == null)
    {
        return;
    }

    statusController.ApplyEffect(dopamineDefinition);
}
        [ContextMenu("Test Stamina - Deplete All")]
private void TestDepleteAll()
{
    if (!Application.isPlaying)
        return;

    Debug.Log(
        $"[탈진 확인] 잔량 {currentStamina:F3} · " +
        $"복제 상태 {formController.CurrentDefinition != null} · " +
        $"컨트롤러 연결 {statusController != null} · " +
        $"탈진 에셋 연결 {exhaustionDefinition != null} · " +
        $"이미 탈진 중 {IsExhausted}",
        this);

    bool spent = TrySpend(currentStamina);
    Debug.Log($"[탈진 확인] 전체 소모 성공: {spent}", this);
}

        [ContextMenu("Test Stamina - Spend 25")]
        private void TestSpend()
        {
            if (!Application.isPlaying)
                return;

            bool spent = TrySpend(25f);

            Debug.Log(
                spent
                    ? $"[스태미나] 25 소모 · {currentStamina:F1}/{maxStamina:F1}"
                    : "[스태미나] 소모 실패: 잔량이나 플레이어 상태를 확인해 주세요.",
                this);
        }
    }
}