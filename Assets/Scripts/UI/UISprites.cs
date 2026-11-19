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

        private static Sprite _square;

        /// <summary>O vuong 1 mau (dung cho thanh mau dang Filled).</summary>
        public static Sprite Square()
        {
            if (_square != null) return _square;

            const int size = 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, Color.white);
            texture.Apply();

            _square = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            _square.name = "square";
            return _square;
        }

        private static readonly Dictionary<string, Sprite> ShapeCache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Panel theo bo cuc SVG: nen gradient doc, vien mo, bo goc nho va CAT CHEO goc phai
        /// (goc cat giong file thiet ke). cutCorner: 0 = khong cat, 1 = tren-phai,
        /// 2 = duoi-phai, 3 = CA HAI goc phai (dang "mui nhon" nhu nut ESC).
        /// </summary>
        public static Sprite PanelShape(int width, int height, int cut, int radius,
            Color top, Color bottom, Color border, float borderWidth, int cutCorner = 0)
        {
            width = Mathf.Clamp(width, 8, 1024);
            height = Mathf.Clamp(height, 8, 1024);
            string key = string.Format("{0}x{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}",
                width, height, cut, radius,
                ColorUtility.ToHtmlStringRGBA(top), ColorUtility.ToHtmlStringRGBA(bottom),
                ColorUtility.ToHtmlStringRGBA(border), borderWidth.ToString("0.0"), cutCorner);

            Sprite cached;
            if (ShapeCache.TryGetValue(key, out cached) && cached != null) return cached;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float w = width;
            float h = height;
            float r = Mathf.Max(0f, radius);
            float bw = Mathf.Max(0f, borderWidth);
            float c = Mathf.Max(0f, cut);
            float invSqrt2 = 0.70710678f;

            for (int y = 0; y < height; y++)
            {
                // Texture toa do y tu duoi len; panel tinh tu tren xuong.
                float py = height - 1 - y + 0.5f;
                float v = (py - 0.5f) / Mathf.Max(1f, h - 1f);
                Color fill = Color.Lerp(top, bottom, Mathf.Clamp01(v));

                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;

                    // Khoang cach co dau vao trong hinh chu nhat bo goc (SDF chuan).
                    // > 0 la nam trong, = 0 la dung tren vien.
                    float qx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - r);
                    float qy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - r);
                    float qxPos = Mathf.Max(qx, 0f);
                    float qyPos = Mathf.Max(qy, 0f);
                    float dist = Mathf.Sqrt(qxPos * qxPos + qyPos * qyPos) + Mathf.Min(Mathf.Max(qx, qy), 0f);
                    float inset = -dist;

                    // goc bi cat cheo. Do cao tinh tu DINH panel xuong cho khop voi SVG
                    // (Figma cat goc theo huong tu tren xuong, con texture tinh tu duoi len).
                    if (c > 0.5f)
                    {
                        float vTop = h - py;                                   // 0 = dinh panel
                        float cutSize = Mathf.Min(c, Mathf.Max(0.5f, (h - 1f) * 0.5f));

                        // Cat goc tren-phai: duong thang tu (w-cut, 0) den (w, cut) => u - v = w - cut.
                        if (cutCorner == 1 || cutCorner == 3)
                        {
                            float dTop = ((w - cutSize) - (px - vTop)) * invSqrt2;
                            if (dTop < inset) inset = dTop;
                        }

                        // Cat goc duoi-phai: duong thang tu (w-cut, h) den (w, h-cut) => u + v = w + h - cut.
                        if (cutCorner == 2 || cutCorner == 3)
                        {
                            float dBottom = ((w + h - cutSize) - (px + vTop)) * invSqrt2;
                            if (dBottom < inset) inset = dBottom;
                        }
                    }

                    Color pixel;
                    if (inset <= 0f) pixel = new Color(0f, 0f, 0f, 0f);
                    else if (bw > 0f && inset < bw)
                    {
                        // vien mem 1 pixel o ria
                        float a = Mathf.Clamp01(inset + 0.5f);
                        pixel = new Color(border.r, border.g, border.b, border.a * a);
                    }
                    else
                    {
                        float a = Mathf.Clamp01(inset + 0.5f) * fill.a;
                        pixel = new Color(fill.r, fill.g, fill.b, a);
                    }

                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "panel_shape";
            ShapeCache[key] = sprite;
            return sprite;
        }
    }
}
