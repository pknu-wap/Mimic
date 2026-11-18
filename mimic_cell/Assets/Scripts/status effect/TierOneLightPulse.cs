using UnityEngine;

namespace MimicCell.StatusEffects
{
    public sealed class TierOneLightPulse : MonoBehaviour
    {
        private const float PulseInterval = 2.0f;
        private const float PulseDuration = 0.5f;

        private SpriteRenderer body;
        private SpriteRenderer steadyLight;
        private SpriteRenderer pulseLight;
        private Texture2D lightTexture;
        private Sprite lightSprite;
        private float startsAt;

        public void Initialize(SpriteRenderer source, float delay)
        {
            body = source;
            startsAt = Time.time + delay;

            const int size = 64;
            lightTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var point = new Vector2(
                        (x + 0.5f) / size * 2f - 1f,
                        (y + 0.5f) / size * 2f - 1f);

                    float alpha = 1f - Mathf.SmoothStep(0f, 1f, point.magnitude);
                    pixels[y * size + x] = new Color32(
                        255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            lightTexture.SetPixels32(pixels);
            lightTexture.Apply(false, true);
            lightSprite = Sprite.Create(
                lightTexture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);

            steadyLight = CreateLight("Steady Light");
            pulseLight = CreateLight("Expanding Light");
        }

        private SpriteRenderer CreateLight(string objectName)
        {
            var lightObject = new GameObject(objectName);
            lightObject.transform.SetParent(transform, false);

            var renderer = lightObject.AddComponent<SpriteRenderer>();
            renderer.sprite = lightSprite;
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = body.sortingOrder - 1;
            renderer.enabled = false;
            return renderer;
        }

        private void LateUpdate()
        {
            if (steadyLight == null || pulseLight == null)
                return;

            bool showLight = body != null && body.enabled && body.sprite != null;
            steadyLight.enabled = showLight;
            pulseLight.enabled = false;

            if (!showLight)
                return;

            Bounds bounds = body.bounds;
            transform.position = bounds.center;
            float bodySize = Mathf.Max(bounds.size.x, bounds.size.y);

            SetLight(steadyLight, bodySize * 1.3f, 0.14f);

            float elapsed = Time.time - startsAt;
            if (elapsed < 0f)
                return;

            float phase = Mathf.Repeat(elapsed, PulseInterval) / PulseDuration;
            if (phase >= 1f)
                return;

            pulseLight.enabled = true;
            float diameter = bodySize * Mathf.Lerp(1.1f, 2.5f, phase);
            float opacity = 0.45f * (1f - phase);
            SetLight(pulseLight, diameter, opacity);
        }

        private void SetLight(SpriteRenderer renderer, float diameter, float opacity)
        {
            Vector3 scale = transform.lossyScale;
            renderer.transform.localScale = new Vector3(
                diameter / Mathf.Max(Mathf.Abs(scale.x), 0.001f),
                diameter / Mathf.Max(Mathf.Abs(scale.y), 0.001f),
                1f);

            renderer.color = new Color(1f, 0.88f, 0.45f, opacity);
        }

        private void OnDestroy()
        {
            Destroy(lightSprite);
            Destroy(lightTexture);
        }
    }
}