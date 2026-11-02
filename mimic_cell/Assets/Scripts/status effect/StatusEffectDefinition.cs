
using UnityEngine;

namespace MimicCell.StatusEffects
//코드를 분류하는 이름표. 상태이상 관련 코드를 MimicCell.StatusEffects로 묶어줌.
{
    public enum StatusEffectCategory
    //enum - 정해진 선택지 중 하나를 고르게 만드는 자료형
    {
        Buff,
        Debuff
    }

    public enum StatusEffectTarget
    //StatusEffectTaarget은 플레이어, npc 생물 둘 다 중 하나를 선택하게 함.


    {
        Player,
        Creature,
        Both
    }

    public enum StatusEffectDurationType
    //지속 방식을 선택
    {
        FixedDuration,
        WhileConditionActive,
        Permanent
    }

    [CreateAssetMenu(
        fileName = "NewStatusEffect",
        menuName = "MimicCell/Status Effect")]
    public sealed class StatusEffectDefinition : ScriptableObject
    //ScriptableObject : 설정 데이터를 저장하는 에셋의 설계도로 사용
    {
        [Header("Basic Information")]
        [SerializeField] private string effectId;
        [SerializeField] private string displayName;

        [Header("Classification")]
        [SerializeField] private StatusEffectCategory category;
        [SerializeField] private StatusEffectTarget targetType;

        [Header("Duration")]
        [SerializeField] private StatusEffectDurationType durationType;
        [SerializeField, Min(0f)] private float duration = 3f;

        [Header("Movement")]
        [SerializeField, Min(0f)]
        private float swimSpeedMultiplier = 1f;

        public string EffectId => effectId;
        public string DisplayName => displayName;
        public StatusEffectCategory Category => category;
        public StatusEffectTarget TargetType => targetType;
        public StatusEffectDurationType DurationType => durationType;
        public float Duration => duration;
        public float SwimSpeedMultiplier => swimSpeedMultiplier;

        private void OnValidate()
        //OnValidate - 음수값을 입력하지 않도록 제한
        {
            duration = Mathf.Max(0f, duration);
            swimSpeedMultiplier = Mathf.Max(0f, swimSpeedMultiplier);
        }
    }
}
