using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.UI
{
    /// <summary>
    /// Sinh sprite bo goc luc chay (9-slice) de UI dung chung mot ngon ngu hinh khoi
    /// ma khong can file anh nao trong project.
    /// </summary>
    public static class UISprites
    {
        private static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();

        public static Sprite Rounded(int radius)
        {
            Sprite cached;
            if (Cache.TryGetValue(radius, out cached) && cached != null) return cached;

            int size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float r = radius;
            float max = size - 1;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float cx = Mathf.Clamp(px, r, max - r);
                    float cy = Mathf.Clamp(py, r, max - r);
                    float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
                }
            }

            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));

            sprite.name = "rounded_" + radius;
            Cache[radius] = sprite;
            return sprite;
        }

        private static readonly Dictionary<int, Sprite> FrameCache = new Dictionary<int, Sprite>();

        /// <summary>
        /// Sprite rong (chi co vien) bo goc, 9-slice — dung cho khung "vung camera dang nhin"
        /// tren ban do: keo gian khung ma vien khong bi meo.
        /// </summary>
        public static Sprite Frame(int radius, int thickness)
        {
            int key = radius * 100 + thickness;
            Sprite cached;
            if (FrameCache.TryGetValue(key, out cached) && cached != null) return cached;

            int size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float r = radius;
            float t = Mathf.Max(1, thickness);
            float max = size - 1;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float cx = Mathf.Clamp(px, r, max - r);
                    float cy = Mathf.Clamp(py, r, max - r);
                    float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));

                    float outer = Mathf.Clamp01(r - d + 0.5f);
                    float inner = Mathf.Clamp01((r - t) - d + 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(outer - inner)));
                }
            }

            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));

            sprite.name = "frame_" + radius + "_" + thickness;
            FrameCache[key] = sprite;
            return sprite;
        }
    }
}
