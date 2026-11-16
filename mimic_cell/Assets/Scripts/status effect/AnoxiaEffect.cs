using MimicCell.Gameplay;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    public sealed class AnoxiaEffect : MonoBehaviour
    {
        [SerializeField] private bool enableTestKey;

       private const float LingerDuration = 2f;

private ActorHealth health;
private bool insideAnoxia;
private bool exposed;
private float releaseAt;
private float nextDamageAt;
private long damageRemainder;
        // 순서대로 W, A, S, D가 가리키는 방향.
        private readonly Vector2[] directions = new Vector2[4];

       public void SetExposure(bool inside)
{
    if (!isActiveAndEnabled || insideAnoxia == inside)
        return;

    insideAnoxia = inside;
    float now = Time.time;

    if (!insideAnoxia)
    {
        if (exposed)
            releaseAt = now + LingerDuration;

        return;
    }

    if (exposed && now < releaseAt)
        return;

    health = GetComponent<ActorHealth>();
    exposed = health != null && health.IsAlive;

    if (!exposed)
        return;

    nextDamageAt = now + 1f;
    damageRemainder = 0;
    ShuffleDirections();
}

private void Update()
{
    if (enableTestKey && WasTestKeyPressed())
        SetExposure(!insideAnoxia);

    if (!exposed)
        return;

    if (health == null || !health.IsAlive)
    {
        exposed = false;
        return;
    }

    float now = Time.time;
    float damageUntil = insideAnoxia ? now : Mathf.Min(now, releaseAt);

    while (health.IsAlive && nextDamageAt <= damageUntil)
    {
        nextDamageAt += 1f;

        long scaledDamage = (long)health.MaxHealth + damageRemainder;
        int damage = (int)(scaledDamage / 10);
        damageRemainder = scaledDamage % 10;

        if (damage > 0 && health.TakeDamage(damage))
        {
            Debug.Log(
                $"무산소화 피해 {damage} · 체력 " +
                $"{health.CurrentHealth}/{health.MaxHealth}", this);
        }
    }

    if (!health.IsAlive || (!insideAnoxia && now >= releaseAt))
        exposed = false;
}

        public bool TryReadMovement(out Vector2 move)
        {
            move = Vector2.zero;

            if (!isActiveAndEnabled || !exposed || health == null || !health.IsAlive)
                return false;

            bool w = false;
            bool a = false;
            bool s = false;
            bool d = false;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                w = keyboard.wKey.isPressed;
                a = keyboard.aKey.isPressed;
                s = keyboard.sKey.isPressed;
                d = keyboard.dKey.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            w |= Input.GetKey(KeyCode.W);
            a |= Input.GetKey(KeyCode.A);
            s |= Input.GetKey(KeyCode.S);
            d |= Input.GetKey(KeyCode.D);
#endif

            if (w) move += directions[0];
            if (a) move += directions[1];
            if (s) move += directions[2];
            if (d) move += directions[3];

            move = Vector2.ClampMagnitude(move, 1f);
            return true;
        }

        private void ShuffleDirections()
        {
            do
            {
                directions[0] = Vector2.up;
                directions[1] = Vector2.left;
                directions[2] = Vector2.down;
                directions[3] = Vector2.right;

                for (int i = directions.Length - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (directions[i], directions[j]) =
                        (directions[j], directions[i]);
                }
            }
            while (directions[0] == Vector2.up &&
                   directions[1] == Vector2.left &&
                   directions[2] == Vector2.down &&
                   directions[3] == Vector2.right);
        }

      private void OnDisable()
{
    insideAnoxia = false;
    exposed = false;
}

        private static bool WasTestKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null &&
                   Keyboard.current.oKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.O);
#else
            return false;
#endif
        }
    }
}