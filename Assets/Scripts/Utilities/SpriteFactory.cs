using UnityEngine;

namespace IronWasteland.Utilities
{
    /// <summary>
    /// Sinh sprite don gian bang code cho cac hieu ung chua co art (vong tron, vet lua...).
    /// Moi sprite duoc cache theo kich thuoc de khong tao texture moi lien tuc.
    /// </summary>
    public static class SpriteFactory
    {
        private const int DefaultSize = 64;

        private static Sprite s_Circle;
        private static Sprite s_SoftCircle;

        /// <summary>Hinh tron dac, vien mem (dung cho vung sat thuong).</summary>
        public static Sprite Circle => s_Circle != null ? s_Circle : (s_Circle = BuildCircle(DefaultSize, 0.75f));

        /// <summary>Hinh tron mo dan ra vien (dung cho vet lua / aura).</summary>
        public static Sprite SoftCircle => s_SoftCircle != null ? s_SoftCircle : (s_SoftCircle = BuildCircle(DefaultSize, 0f));

        private static Sprite BuildCircle(int size, float solidRadius)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "GeneratedCircle_" + size,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            float center = (size - 1) * 0.5f;
            float maxRadius = size * 0.5f;
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) / maxRadius;

                    float alpha;
                    if (distance >= 1f)
                    {
                        alpha = 0f;
                    }
                    else if (solidRadius > 0f && distance <= solidRadius)
                    {
                        alpha = 1f;
                    }
                    else
                    {
                        float span = Mathf.Max(0.0001f, 1f - solidRadius);
                        alpha = Mathf.Clamp01(1f - (distance - solidRadius) / span);
                        alpha *= alpha;
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size,
                0,
                SpriteMeshType.FullRect);

            sprite.name = "GeneratedCircle_" + size;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
