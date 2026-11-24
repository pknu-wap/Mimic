using MimicCell.Creatures;
using MimicCell.Gameplay;
using UnityEngine;

namespace MimicCell.StatusEffects
{
    public enum CreatureInjuryStage
    {
        None,
        Minor,
        Severe,
        Critical
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CreatureController), typeof(ActorHealth))]
    public sealed class CreatureInjuryEffect : MonoBehaviour
    {
        [Header("Injured Movement")]
[SerializeField, Min(1f)] private float referenceTurnSpeed = 360f;
[SerializeField, Range(0f, 30f)] private float wobbleAngle = 10f;
        private ActorHealth health;

[SerializeField] private CreatureInjuryStage stage;

public CreatureInjuryStage Stage => stage;
        public float TurnSpeedMultiplier =>
            Stage >= CreatureInjuryStage.Minor ? 0.4f : 1f;

        public float ViewAngleMultiplier =>
            Stage >= CreatureInjuryStage.Severe ? 0.2f : 1f;
            public float PerceptionRadiusMultiplier =>
    Stage >= CreatureInjuryStage.Severe ? 0.5f : 1f;

        public bool HasUnstableMovement =>
            Stage >= CreatureInjuryStage.Minor;

        public bool CanSwim =>
            health != null && health.IsAlive &&
            Stage != CreatureInjuryStage.Critical;

        private void Awake()
        {
            health = GetComponent<ActorHealth>();
        }

        private void OnEnable()
        {
            health.HealthChanged += RefreshStage;
            RefreshStage(health);
        }

        private void OnDisable()
        {
            health.HealthChanged -= RefreshStage;
        }
public Vector2 AdjustMovement(
    Vector2 desiredDirection, Vector2 currentVelocity, float deltaTime)
{
    if (!HasUnstableMovement || desiredDirection.sqrMagnitude <= 0.0001f)
        return desiredDirection;

    float magnitude = Mathf.Min(1f, desiredDirection.magnitude);

    float targetAngle =
        Mathf.Atan2(desiredDirection.y, desiredDirection.x) * Mathf.Rad2Deg;

    float phase = (GetInstanceID() % 1024) * 0.1f;
    targetAngle += Mathf.Sin(Time.time * 2f + phase) * wobbleAngle;

    float currentAngle = currentVelocity.sqrMagnitude > 0.0001f
        ? Mathf.Atan2(currentVelocity.y, currentVelocity.x) * Mathf.Rad2Deg
        : targetAngle;

    float angle = Mathf.MoveTowardsAngle(
        currentAngle,
        targetAngle,
        referenceTurnSpeed * TurnSpeedMultiplier * deltaTime);

    float radians = angle * Mathf.Deg2Rad;

    return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * magnitude;
}
        private void RefreshStage(ActorHealth actor)
        {
            if (!actor.IsAlive)
                return;

            long lostHealth = (long)actor.MaxHealth - actor.CurrentHealth;
            long damagePercent = lostHealth * 100;

            CreatureInjuryStage nextStage = CreatureInjuryStage.None;

            if (damagePercent >= (long)actor.MaxHealth * 70)
                nextStage = CreatureInjuryStage.Critical;
            else if (damagePercent >= (long)actor.MaxHealth * 50)
                nextStage = CreatureInjuryStage.Severe;
            else if (damagePercent >= (long)actor.MaxHealth * 30)
                nextStage = CreatureInjuryStage.Minor;

            // 永続的な負傷として、回復しても段階は戻さない。
            if (nextStage <= Stage)
                return;

stage = nextStage;
            Debug.Log(
                $"[부상] {name}: {Stage} · 체력 {actor.CurrentHealth}/{actor.MaxHealth}",
                this);
        }

        [ContextMenu("Test Injury - Damage 10 Percent")]
        private void TestDamage()
        {
            if (!Application.isPlaying || health == null || !health.IsAlive)
                return;

            int damage = Mathf.Max(1, Mathf.CeilToInt(health.MaxHealth * 0.1f));
            health.TakeDamage(damage);
        }
    }
}
// 영구 부상이므로 체력을 회복해도 부상 단계는 내려가지 않음.