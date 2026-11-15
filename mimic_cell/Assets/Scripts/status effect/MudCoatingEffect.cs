using MimicCell.Gameplay;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    public sealed class MudCoatingEffect : MonoBehaviour
    {
        [SerializeField] private StatusEffectDefinition mudCoatingDefinition;
        [SerializeField] private bool enableTestKey;
        [SerializeField] private bool showScreenEffect;
        [SerializeField, Range(0, 100)] private int damagePercentPerSecond = 10;

        private ActorHealth health;
        private StatusEffectController statusController;
        private bool covered;
        private float damageTimer;
        private long damageRemainder;
        private Texture2D vignette;
        private float vignetteAspect;

        public void SetCovered(bool value)
        {
            if (covered == value)
                return;

            if (value)
            {
                health = GetComponent<ActorHealth>();
                statusController = GetComponent<StatusEffectController>();

                if (health == null || !health.IsAlive ||
                    statusController == null || mudCoatingDefinition == null)
                {
                    Debug.LogWarning(
                        "진흙 피복을 적용하려면 살아 있는 플레이어와 상태이상 에셋이 필요해.",
                        this);
                    return;
                }

                statusController.ApplyEffect(mudCoatingDefinition);
                if (!statusController.HasEffect(mudCoatingDefinition))
                    return;
            }

            covered = value;
            damageTimer = 0f;
            damageRemainder = 0;

            if (!covered && statusController != null)
                statusController.RemoveEffect(mudCoatingDefinition);
        }

        private void Update()
        {
            if (enableTestKey && WasTestKeyPressed())
                SetCovered(!covered);

            if (!covered)
                return;

            if (health == null || !health.IsAlive ||
                statusController == null ||
                !statusController.HasEffect(mudCoatingDefinition))
            {
                SetCovered(false);
                return;
            }

            damageTimer += Time.deltaTime;

            while (covered && health.IsAlive && damageTimer >= 1f)
            {
                damageTimer -= 1f;

                // 정수 체력에서 잘린 소수 피해를 다음 피해에 반영한다.
                long scaledDamage = (long)health.MaxHealth *
                                    damagePercentPerSecond + damageRemainder;
                int damage = (int)(scaledDamage / 100);
                damageRemainder = scaledDamage % 100;

                if (damage > 0 && health.TakeDamage(damage))
                {
                    Debug.Log(
                        $"진흙 피복 피해 {damage} · 체력 " +
                        $"{health.CurrentHealth}/{health.MaxHealth}", this);
                }
            }

            if (!health.IsAlive)
                SetCovered(false);
        }

        private void OnGUI()
        {
            if (!covered || !showScreenEffect ||
                Event.current.type != EventType.Repaint ||
                Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            float aspect = (float)Screen.width / Screen.height;
            if (vignette == null || Mathf.Abs(vignetteAspect - aspect) > 0.01f)
                CreateVignette(aspect);

            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                vignette, ScaleMode.StretchToFill, true);
        }

        private void CreateVignette(float aspect)
        {
            if (vignette != null)
                Destroy(vignette);

            const int size = 256;
            const float clearRadius = 0.22f;
            const float darkRadius = 0.49f;

            vignetteAspect = aspect;
            vignette = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            float scaleX = Mathf.Max(1f, aspect);
            float scaleY = Mathf.Max(1f, 1f / aspect);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = ((x + 0.5f) / size - 0.5f) * scaleX;
                    float dy = ((y + 0.5f) / size - 0.5f) * scaleY;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float fade = Mathf.InverseLerp(clearRadius, darkRadius, distance);
                    float alpha = Mathf.SmoothStep(0f, 0.90f, fade);

                    pixels[y * size + x] = new Color32(
                        47, 33, 22, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            vignette.SetPixels32(pixels);
            vignette.Apply(false, true);
        }

        private void OnDisable()
        {
            SetCovered(false);
        }

        private void OnDestroy()
        {
            if (vignette != null)
                Destroy(vignette);
        }

        private static bool WasTestKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null &&
                   Keyboard.current.pKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.P);
#else
            return false;
#endif
        }
    }
}