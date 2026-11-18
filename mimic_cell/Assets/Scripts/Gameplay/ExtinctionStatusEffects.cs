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

    // Temporary status rules for the extinction test. Durations and penalties can later come from game data.
    [DisallowMultipleComponent]
    public sealed class ExtinctionStatusEffects : MonoBehaviour
    {
        private ActorHealth health;
        private ExtinctionStatus status;
        private float remaining;
        private float damageTimer;

        public ExtinctionStatus ActiveStatus => remaining > 0f ? status : ExtinctionStatus.None;
        public float SpeedMultiplier => ActiveStatus == ExtinctionStatus.MudCoating ? 0.75f :
                                        ActiveStatus == ExtinctionStatus.Freezing ? 0.6f : 1f;

        private void Awake()
        {
            health = GetComponent<ActorHealth>();
        }

        private void Update()
        {
            if (remaining <= 0f)
            {
                status = ExtinctionStatus.None;
                damageTimer = 0f;
                return;
            }

            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            if (status == ExtinctionStatus.Anoxia && health != null && health.IsAlive)
            {
                damageTimer += Time.deltaTime;
                if (damageTimer >= 2f)
                {
                    damageTimer -= 2f;
                    health.TakeDamage(1);
                }
            }
        }

        public void Apply(ExtinctionStatus nextStatus, float duration)
        {
            if (nextStatus == ExtinctionStatus.None)
            {
                return;
            }

            if (status != nextStatus || remaining <= 0f)
            {
                damageTimer = 0f;
            }

            status = nextStatus;
            remaining = Mathf.Max(remaining, duration);
        }
    }
}
