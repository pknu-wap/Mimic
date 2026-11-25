using UnityEngine;

namespace MimicCell.Gameplay
{
    public enum ExtinctionStatus
    {
        None,
        MudCoating,
        Anoxia,
        Freezing
    }

    [DisallowMultipleComponent]
    public sealed class ExtinctionStatusEffects : MonoBehaviour
    {
        private sealed class EffectState
        {
            public readonly ExtinctionStatus Status;
            public readonly int DamagePercent;
            public readonly float LingerDuration;

            public bool Inside;
            public float Remaining;
            public float DamageTimer;
            public long DamageRemainder;

            public bool Active => Inside || Remaining > 0f;

            public EffectState(
                ExtinctionStatus status, int damagePercent, float lingerDuration)
            {
                Status = status;
                DamagePercent = damagePercent;
                LingerDuration = lingerDuration;
            }

            public void Reset()
            {
                Inside = false;
                Remaining = 0f;
                DamageTimer = 0f;
                DamageRemainder = 0;
            }
        }

        private readonly EffectState[] effects =
        {
            new EffectState(ExtinctionStatus.MudCoating, 10, 0f),
            new EffectState(ExtinctionStatus.Anoxia, 10, 2f),
            new EffectState(ExtinctionStatus.Freezing, 8, 4f)
        };

        private ActorHealth health;

        public ExtinctionStatus ActiveStatus
        {
            get
            {
                foreach (EffectState effect in effects)
                {
                    if (effect.Active)
                        return effect.Status;
                }

                return ExtinctionStatus.None;
            }
        }

        public float SpeedMultiplier =>
            (IsActive(ExtinctionStatus.MudCoating) ? 0.75f : 1f) *
            (IsActive(ExtinctionStatus.Freezing) ? 0.6f : 1f);

        private void Awake()
        {
            health = GetComponent<ActorHealth>();
        }

        public bool IsActive(ExtinctionStatus status)
        {
            foreach (EffectState effect in effects)
            {
                if (effect.Status == status)
                    return effect.Active;
            }

            return false;
        }

        public void SetExposure(ExtinctionStatus currentHazard)
        {
            foreach (EffectState effect in effects)
            {
                bool inside = effect.Status == currentHazard;

                if (inside == effect.Inside)
                    continue;

                if (inside && !effect.Active)
                    effect.Reset();

                effect.Inside = inside;

                if (!inside)
                {
                    effect.Remaining = effect.LingerDuration;

                    Debug.Log(
                        $"[몬스터 수역 이탈] {name} · {effect.Status} · " +
                        $"{effect.LingerDuration}초 후 해제",
                        this);

                    if (!effect.Active)
                        effect.Reset();
                }
            }
        }

        // 기존 시간제 적용 방식도 사용할 수 있도록 유지
        public void Apply(ExtinctionStatus status, float duration)
        {
            if (duration <= 0f || float.IsNaN(duration) ||
                float.IsInfinity(duration))
                return;

            foreach (EffectState effect in effects)
            {
                if (effect.Status != status)
                    continue;

                if (!effect.Active)
                    effect.Reset();

                effect.Remaining = Mathf.Max(effect.Remaining, duration);
                return;
            }
        }

        private void Update()
        {
            if (health == null || !health.IsAlive)
            {
                ClearEffects();
                return;
            }

            foreach (EffectState effect in effects)
            {
                if (!effect.Active)
                    continue;

                float activeTime = effect.Inside
                    ? Time.deltaTime
                    : Mathf.Min(Time.deltaTime, effect.Remaining);

                if (!effect.Inside)
                    effect.Remaining =
                        Mathf.Max(0f, effect.Remaining - Time.deltaTime);

                effect.DamageTimer += activeTime;

                while (effect.DamageTimer >= 1f && health.IsAlive)
                {
                    effect.DamageTimer -= 1f;
                    effect.DamageRemainder +=
                        (long)health.MaxHealth * effect.DamagePercent;

                    int damage = (int)(effect.DamageRemainder / 100);
                    effect.DamageRemainder %= 100;

                    if (damage > 0 && health.TakeDamage(damage))
                    {
                        Debug.Log(
                            $"[몬스터 상태이상] {name} · {effect.Status} " +
                            $"피해 {damage} · " +
                            $"체력 {health.CurrentHealth}/{health.MaxHealth}",
                            this);
                    }
                }

                if (!effect.Active)
                {
                    Debug.Log(
                        $"[몬스터 상태이상 해제] {name} · {effect.Status}",
                        this);

                    effect.Reset();
                }
            }
        }

        private void ClearEffects()
        {
            foreach (EffectState effect in effects)
                effect.Reset();
        }

        private void OnDisable()
        {
            ClearEffects();
        }
    }
}