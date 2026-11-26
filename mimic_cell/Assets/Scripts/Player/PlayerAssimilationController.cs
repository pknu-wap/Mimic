using MimicCell.Creatures;
using MimicCell.Gameplay;
using MimicCell.StatusEffects;
using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCreatureFormController), typeof(ActorHealth))]
    public sealed class PlayerAssimilationController : MonoBehaviour
    {
        [SerializeField] private AssimilationDarkeningEffect darkeningEffect;
        [SerializeField, Range(0f, 100f)] private float assimilationPercent;

        [SerializeField, Range(0f, 1f)]
private float highAssimilationRateMultiplier = 0.25f;

        private PlayerCreatureFormController formController;
        private ActorHealth health;
        private string lastSpeciesId;
        [Header("Assimilation Effects")]
[SerializeField] private StatusEffectDefinition panic;
[SerializeField] private StatusEffectDefinition tunnelVision;
[SerializeField] private StatusEffectDefinition berserk;

private StatusEffectController statusController;
private StatusEffectDefinition activeAssimilationEffect;

private bool trainingLimitActive;

public float Percent => assimilationPercent;

private float AssimilationLimit => trainingLimitActive ? 80f : 100f;

public void SetTrainingLimit(bool active)
{
    trainingLimitActive = active;

    assimilationPercent = Mathf.Min(
        assimilationPercent, AssimilationLimit);

    if (formController != null && health != null)
    {
        RefreshAssimilationEffect(
            health.IsAlive ? formController.CurrentDefinition : null);
    }
}

public void AddTrainingAssimilation(float amount)
{
    if (!trainingLimitActive ||
        !isActiveAndEnabled ||
        health == null ||
        !health.IsAlive ||
        formController.CurrentDefinition == null)
    {
        return;
    }

    assimilationPercent = Mathf.Min(
        AssimilationLimit,
        assimilationPercent + Mathf.Max(0f, amount));

    RefreshAssimilationEffect(formController.CurrentDefinition);
}
        private void Awake()
        {
            formController = GetComponent<PlayerCreatureFormController>();
            health = GetComponent<ActorHealth>();
                statusController = GetComponent<StatusEffectController>();

        }

        private void OnEnable()
        {
            formController.FormChanged += HandleFormChanged;
            health.DamageTaken += HandleDamageTaken;
        }

        private void Start()
        {
            HandleFormChanged(formController.CurrentDefinition);
        }

        private void OnDisable()
        {
            formController.FormChanged -= HandleFormChanged;
                health.DamageTaken -= HandleDamageTaken;
                    RefreshAssimilationEffect(null);


        }

        private void HandleFormChanged(CreatureDefinition definition)
{
    if (definition != null && lastSpeciesId != definition.CreatureId)
    {
        lastSpeciesId = definition.CreatureId;
        assimilationPercent = 0f;

        Debug.Log(
            $"[동화율] {definition.DisplayName} 복제 · 0%로 초기화",
            this);
    }

    RefreshAssimilationEffect(health.IsAlive ? definition : null);
}

        private void Update()
{
    CreatureDefinition definition = formController.CurrentDefinition;

    if (health.IsAlive && definition != null)
    {
        float rateMultiplier = assimilationPercent >= 80f
            ? highAssimilationRateMultiplier
            : 1f;

        assimilationPercent = Mathf.Min(
    AssimilationLimit,
            assimilationPercent +
            GetIncreasePerSecond(definition.FoodTier) *
            rateMultiplier * Time.deltaTime);
    }

    RefreshAssimilationEffect(health.IsAlive ? definition : null);

    if (darkeningEffect != null)
        darkeningEffect.SetAssimilation(assimilationPercent / 100f);
}
private void HandleDamageTaken(ActorHealth actor)
{
    CreatureDefinition definition = formController.CurrentDefinition;
    if (definition == null || !actor.IsAlive)
        return;

    float increase;

    switch (definition.FoodTier)
    {
        case CreatureFoodTier.Tier1:
            increase = 8f;
            break;

        case CreatureFoodTier.Tier2:
            increase = 5f;
            break;

        case CreatureFoodTier.Tier3:
            increase = 3f;
            break;

        default:
            return;
    }

    bool isArmored =
        (definition.Traits & CreatureTraits.Armored) != CreatureTraits.None;

    if (isArmored)
        increase *= 0.5f;

assimilationPercent = Mathf.Min(
    AssimilationLimit, assimilationPercent + increase);
    Debug.Log(
        $"[동화율] 피격 +{increase}%p · 현재 {assimilationPercent:F1}%",
        this);
}

public void NotifyAttackHit(bool isReflectedDamage = false)
{
    if (!isActiveAndEnabled || health == null || !health.IsAlive)
        return;

    CreatureDefinition definition = formController.CurrentDefinition;
    if (definition == null)
        return;

    float increase;

    switch (definition.FoodTier)
    {
        case CreatureFoodTier.Tier2:
            increase = 3f;
            break;

        case CreatureFoodTier.Tier3:
            increase = 8f;
            break;

        default:
            return;
    }

    if (isReflectedDamage)
        increase *= 0.5f;

assimilationPercent = Mathf.Min(
    AssimilationLimit, assimilationPercent + increase);    RefreshAssimilationEffect(definition);

    Debug.Log(
        $"[동화율] 공격 적중 +{increase}%p · 현재 {assimilationPercent:F1}%",
        this);
}

[ContextMenu("Test Assimilation - Attack Hit")]
private void TestAttackHit()
{
    if (Application.isPlaying)
        NotifyAttackHit();
}

[ContextMenu("Test Assimilation - Reflected Damage")]
private void TestReflectedDamage()
{
    if (Application.isPlaying)
        NotifyAttackHit(true);
}
[ContextMenu("Test Assimilation - Take 1 Damage")]
private void TestDamage()
{
    if (!Application.isPlaying || health == null || !health.IsAlive)
        return;

    health.TakeDamage(1);
}
private void RefreshAssimilationEffect(CreatureDefinition definition)
{
    if (statusController == null)
        return;

    StatusEffectDefinition desiredEffect = null;

    if (definition != null && assimilationPercent >= 80f)
    {
        switch (definition.FoodTier)
        {
            case CreatureFoodTier.Tier1:
                desiredEffect = panic;
                break;

            case CreatureFoodTier.Tier2:
                desiredEffect = tunnelVision;
                break;

            case CreatureFoodTier.Tier3:
                desiredEffect = berserk;
                break;
        }
    }

    if (activeAssimilationEffect != desiredEffect)
    {
        if (activeAssimilationEffect != null)
            statusController.RemoveEffect(activeAssimilationEffect);

        activeAssimilationEffect = desiredEffect;
    }

    if (activeAssimilationEffect != null &&
        !statusController.HasEffect(activeAssimilationEffect))
    {
        statusController.ApplyEffect(activeAssimilationEffect);
    }
}
        private static float GetIncreasePerSecond(CreatureFoodTier tier)
        {
            switch (tier)
            {
                case CreatureFoodTier.Tier1:
                    return 1.43f;

                case CreatureFoodTier.Tier2:
                    return 2f;

                case CreatureFoodTier.Tier3:
                    return 3.33f;

                default:
                    return 0f;
            }
        }
    }
}