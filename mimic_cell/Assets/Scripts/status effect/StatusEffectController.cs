
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MimicCell.StatusEffects
{
    public class StatusEffectController : MonoBehaviour
    {
        [SerializeField]
private StatusEffectTarget ownerType = StatusEffectTarget.Player;
public void SetOwnerType(StatusEffectTarget type)
{
    ownerType = type;
}

        // 현재 적용 중인 상태이상과 남은 시간을 저장
        private class ActiveStatusEffect
        {
            public StatusEffectDefinition definition;
            public float remainingTime;

            public ActiveStatusEffect(StatusEffectDefinition effect)
            {
                definition = effect;
                remainingTime = effect.Duration;
            }
        }
        public float GetSwimSpeedMultiplier()
{
    float multiplier = 1f;

    foreach (ActiveStatusEffect active in activeEffects)
    {
        multiplier *= active.definition.SwimSpeedMultiplier;
    }

    return multiplier;
}

        private readonly List<ActiveStatusEffect> activeEffects =
            new List<ActiveStatusEffect>();

        // 다른 스크립트가 상태이상 변화를 감지할 수 있는 이벤트
        public event Action<StatusEffectDefinition> EffectApplied;
        public event Action<StatusEffectDefinition> EffectRemoved;

        // 현재 적용된 상태이상의 개수
        public int ActiveEffectCount => activeEffects.Count;

        private void Update()
        {
            // 뒤에서부터 검사하면 삭제할 때 안전함
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                ActiveStatusEffect active = activeEffects[i];

                // 고정 지속시간 상태이상만 자동으로 시간 감소
                if (active.definition.DurationType !=
                    StatusEffectDurationType.FixedDuration)
                {
                    continue;
                }

                active.remainingTime -= Time.deltaTime;

                // 지속시간이 끝나면 해제
                if (active.remainingTime <= 0f)
                {
                    RemoveEffect(active.definition);
                }
            }
        }

        // 상태이상 적용
        public void ApplyEffect(StatusEffectDefinition effect)
        {
            if (effect == null)
            {
                Debug.LogWarning("적용할 상태이상 데이터가 없습니다.");
                return;
            }
            // 상태이상의 적용 대상이 맞는지 확인
if (effect.TargetType != StatusEffectTarget.Both &&
    effect.TargetType != ownerType)
{
    Debug.LogWarning(
        effect.DisplayName + "은(는) 이 대상에게 적용할 수 없습니다."
    );
    return;
}

            // 이미 적용된 상태이상인지 확인
            ActiveStatusEffect existing = FindEffect(effect);

            if (existing != null)
            {
                // 같은 상태이상은 중복 생성하지 않고 지속시간 갱신
                existing.remainingTime = effect.Duration;
                return;
            }

            // 지속시간이 0인 고정 상태이상은 적용하지 않음
            if (effect.DurationType ==
                StatusEffectDurationType.FixedDuration &&
                effect.Duration <= 0f)
            {
                return;
            }

            // 새로운 상태이상 추가
            ActiveStatusEffect newEffect =
                new ActiveStatusEffect(effect);

            activeEffects.Add(newEffect);

            EffectApplied?.Invoke(effect);

            Debug.Log(effect.DisplayName + " 상태이상 적용!");
        }

        // 특정 상태이상 해제
        public void RemoveEffect(StatusEffectDefinition effect)
        {
            if (effect == null)
                return;

            ActiveStatusEffect active = FindEffect(effect);

            if (active == null)
                return;

            activeEffects.Remove(active);

            EffectRemoved?.Invoke(effect);

            Debug.Log(effect.DisplayName + " 상태이상 해제!");
        }

        // 모든 상태이상 해제
        public void RemoveAllEffects()
        {
            // 목록 복사 후 해제하면 이벤트 처리 중에도 안전함
            List<StatusEffectDefinition> effectsToRemove =
                new List<StatusEffectDefinition>();

            foreach (ActiveStatusEffect active in activeEffects)
            {
                effectsToRemove.Add(active.definition);
            }

            foreach (StatusEffectDefinition effect in effectsToRemove)
            {
                RemoveEffect(effect);
            }
        }

        // 특정 상태이상이 적용되어 있는지 확인
        public bool HasEffect(StatusEffectDefinition effect)
        {
            return FindEffect(effect) != null;
        }

        // 특정 상태이상의 남은 시간 확인
        public float GetRemainingTime(StatusEffectDefinition effect)
        {
            ActiveStatusEffect active = FindEffect(effect);

            if (active == null)
                return 0f;

            return active.remainingTime;
        }

        // 상태이상 검색
        private ActiveStatusEffect FindEffect(StatusEffectDefinition effect)
        {
            if (effect == null)
                return null;

            foreach (ActiveStatusEffect active in activeEffects)
            {
                if (active.definition == effect)
                    return active;
            }

            return null;
        }

        // 컴포넌트가 비활성화되거나 파괴될 때 상태 정리
        private void OnDestroy()
        {
            RemoveAllEffects();
        }
    }
}
