using MimicCell.Gameplay;
using UnityEngine;

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorHealth))]
    public sealed class BurnEffect : MonoBehaviour
    {
        private const float BurnDuration = 4f;
        private const float DamageInterval = 1f;
        private const int DamagePercent = 7;


[SerializeField] private bool showScreenEffect;
private static readonly int ScreenStrengthId =
    Shader.PropertyToID("_MimicCellBurnScreenStrength");
    
        private ActorHealth health;
        private bool isBurning;
        private float burnEndsAt;
        private float nextDamageAt;
        private long damageHundredths;

        private void Awake()
        {
            health = GetComponent<ActorHealth>();
        }

        public void ApplyBurn()
        {
            if (!isActiveAndEnabled || health == null || !health.IsAlive)
                return;

            float now = Time.time;

            if (!isBurning)
            {
                isBurning = true;
                nextDamageAt = now + DamageInterval;
                damageHundredths = 0;
                Debug.Log("화상 시작", this);
            }

            burnEndsAt = now + BurnDuration;
        }

        private void Update()
        {
            if (!isBurning)
                return;

            float now = Time.time;
            while (health.IsAlive &&
                   nextDamageAt <= now &&
                   nextDamageAt <= burnEndsAt)
            {
                nextDamageAt += DamageInterval;

                damageHundredths += (long)health.MaxHealth * DamagePercent;
                int damage = (int)(damageHundredths / 100);
                damageHundredths %= 100;

                if (damage > 0 && health.TakeDamage(damage))
                {
                    Debug.Log(
                        $"화상 피해 {damage} · 체력 {health.CurrentHealth}/{health.MaxHealth}",
                        this);
                }
            }

            if (now >= burnEndsAt || !health.IsAlive)
            {
                isBurning = false;
                Debug.Log("화상 종료", this);
            }
        }

        private void LateUpdate()
{
    if (!showScreenEffect)
        return;

    float strength = isBurning
        ? Mathf.Clamp01((burnEndsAt - Time.time) / 0.4f)*0.9f
        : 0f;

    Shader.SetGlobalFloat(ScreenStrengthId, strength);
}

private void OnDisable()
{
    isBurning = false;

    if (showScreenEffect)
        Shader.SetGlobalFloat(ScreenStrengthId, 0f);
}

        [ContextMenu("Test Burn")]
        private void TestBurn()
        {
            if (Application.isPlaying)
                ApplyBurn();
        }
    }
}