using UnityEngine;

namespace MimicCell.Prototype
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlaceholderSprite : MonoBehaviour
    {
        [SerializeField] private Vector2Int pixelSize = new Vector2Int(16, 16);
        [SerializeField, Min(1f)] private float pixelsPerUnit = 16f;
        [SerializeField] private Color fillColor = new Color(0.25f, 0.9f, 1f, 0.85f);
        [SerializeField] private Color outlineColor = new Color(0.92f, 1f, 1f, 1f);
        [SerializeField, Min(0)] private int outlinePixels = 1;

        private Texture2D generatedTexture;
        private Sprite generatedSprite;
        private SpriteRenderer cachedRenderer;

        private void OnEnable()
        {
            Refresh();
        }

        private void OnValidate()
        {
            pixelSize = new Vector2Int(Mathf.Max(1, pixelSize.x), Mathf.Max(1, pixelSize.y));
            pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
            outlinePixels = Mathf.Max(0, outlinePixels);
        }

        private void OnDisable()
        {
            DestroyGeneratedObjects();
        }

        public void Apply(Vector2Int newPixelSize, Color newFillColor, Color newOutlineColor, float newPixelsPerUnit)
        {
            pixelSize = new Vector2Int(Mathf.Max(1, newPixelSize.x), Mathf.Max(1, newPixelSize.y));
            fillColor = newFillColor;
            outlineColor = newOutlineColor;
            pixelsPerUnit = Mathf.Max(1f, newPixelsPerUnit);
            Refresh();
        }

        public void Refresh()
        {
            cachedRenderer = GetComponent<SpriteRenderer>();
            DestroyGeneratedObjects();

            generatedTexture = new Texture2D(pixelSize.x, pixelSize.y, TextureFormat.RGBA32, false);
            generatedTexture.name = name + "_PlaceholderTexture";
            generatedTexture.filterMode = FilterMode.Point;
            generatedTexture.wrapMode = TextureWrapMode.Clamp;
            generatedTexture.hideFlags = HideFlags.HideAndDontSave;

            for (int y = 0; y < pixelSize.y; y++)
            {
                for (int x = 0; x < pixelSize.x; x++)
                {
                    bool isOutline = outlinePixels > 0 &&
                                     (x < outlinePixels ||
                                      y < outlinePixels ||
                                      x >= pixelSize.x - outlinePixels ||
                                      y >= pixelSize.y - outlinePixels);
                    generatedTexture.SetPixel(x, y, isOutline ? outlineColor : fillColor);
                }
            }

            generatedTexture.Apply(false, false);

            generatedSprite = Sprite.Create(
                generatedTexture,
                new Rect(0f, 0f, pixelSize.x, pixelSize.y),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
            generatedSprite.name = name + "_PlaceholderSprite";
            generatedSprite.hideFlags = HideFlags.HideAndDontSave;

            cachedRenderer.sprite = generatedSprite;
        }

        private void DestroyGeneratedObjects()
        {
            if (cachedRenderer != null && cachedRenderer.sprite == generatedSprite)
            {
                cachedRenderer.sprite = null;
            }

            DestroyGeneratedObject(generatedSprite);
            DestroyGeneratedObject(generatedTexture);
            generatedSprite = null;
            generatedTexture = null;
        }

        private static void DestroyGeneratedObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
