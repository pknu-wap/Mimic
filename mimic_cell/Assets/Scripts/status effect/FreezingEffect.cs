using System.Collections.Generic;
using MimicCell.Gameplay;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    public sealed class FreezingEffect : MonoBehaviour
    {
        private const int DurationSeconds = 4;
        private const int DamagePercent = 8;

        [SerializeField] private StatusEffectDefinition freezingDefinition;
        [SerializeField] private bool enableTestKey;
        [SerializeField] private Color frozenTint =
            new Color(0.65f, 0.85f, 1f, 1f);

        private readonly Dictionary<SpriteRenderer, Color> originalSpriteColors =
            new Dictionary<SpriteRenderer, Color>();

        private ActorHealth health;
        private StatusEffectController statusController;
        private bool touchingIce;
        private bool frozen;
       private float releaseAt;
private float nextDamageAt;
        private long damageRemainder;

        // 대멸종에서 빙하 수역의 안팎을 알려줄 때 사용한다.
        public void SetIceContact(bool touching)
{
    if (touchingIce == touching)
        return;

    touchingIce = touching;

    if (touchingIce)
    {
        StartFreezing();
    }
    else if (frozen)
    {
        releaseAt = Time.time + DurationSeconds;
    }
}

        private void StartFreezing()
        {
            if (frozen || !isActiveAndEnabled || freezingDefinition == null)
                return;

            health = GetComponent<ActorHealth>();
            statusController = GetComponent<StatusEffectController>();

            if (health == null || !health.IsAlive || statusController == null)
                return;

            statusController.ApplyEffect(freezingDefinition);
            if (!statusController.HasEffect(freezingDefinition))
                return;

            frozen = true;
            nextDamageAt = Time.time + 1f;
            releaseAt = Time.time + DurationSeconds;
            damageRemainder = 0;
            ApplySpriteTint();
        }

        private void Update()
        {
            if (enableTestKey && WasTestKeyPressed())
                StartFreezing();

            if (!frozen)
                return;

            if (health == null || !health.IsAlive || statusController == null ||
                !statusController.HasEffect(freezingDefinition))
            {
                EndFreezing();
                return;
            }

              float now = Time.time;
    float damageUntil = touchingIce ? now : Mathf.Min(now, releaseAt);

    while (frozen && health.IsAlive && nextDamageAt <= damageUntil)
    {
        nextDamageAt += 1f;

        long scaledDamage =
            (long)health.MaxHealth * DamagePercent + damageRemainder;
        int damage = (int)(scaledDamage / 100);
        damageRemainder = scaledDamage % 100;

        if (damage > 0 && health.TakeDamage(damage))
        {
            Debug.Log(
                $"빙결 피해 {damage} · 체력 " +
                $"{health.CurrentHealth}/{health.MaxHealth}", this);
        }
    }

    if (!health.IsAlive || (!touchingIce && now >= releaseAt))
        EndFreezing();
}

        private void ApplySpriteTint()
        {
            foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(true))
            {
                originalSpriteColors.Add(sprite, sprite.color);
                sprite.color = ApplyTint(sprite.color);
            }
        }

        private Color ApplyTint(Color original)
        {
            return new Color(
                original.r * frozenTint.r,
                original.g * frozenTint.g,
                original.b * frozenTint.b,
                original.a);
        }

        private void EndFreezing()
        {
            if (!frozen)
                return;

            frozen = false;

            foreach (var entry in originalSpriteColors)
            {
                if (entry.Key != null)
                {
                    // The extraction animation can change the sprite's opacity while it is frozen.
                    Color original = entry.Value;
                    original.a = entry.Key.color.a;
                    entry.Key.color = original;
                }
            }

            originalSpriteColors.Clear();

            if (statusController != null)
                statusController.RemoveEffect(freezingDefinition);
        }

        private void OnDisable()
        {
            touchingIce = false;
            EndFreezing();
        }

        private static bool WasTestKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null &&
                   Keyboard.current.fKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F);
#else
            return false;
#endif
        }
    }
}
