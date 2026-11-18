using System;
using UnityEngine;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Icon đơn sắc (trắng, nền trong suốt) vẽ bằng code từ các hình cơ bản (khoảng cách có dấu / SDF),
    /// nên không cần file ảnh. Dùng làm Image.sprite rồi tô màu bằng Image.color.
    /// Tọa độ vẽ: khung 64x64, gốc dưới-trái, y hướng lên.
    /// </summary>
    public static class UIIcons
    {
        private const int Size = 64;

        private static Sprite s_Play;
        private static Sprite s_Tank;
        private static Sprite s_Gear;
        private static Sprite s_Exit;

        /// <summary>Tam giác phát (Chọn màn chơi).</summary>
        public static Sprite Play => s_Play != null ? s_Play : (s_Play = Make(PlayShape));

        /// <summary>Xe tăng nhìn ngang (Nhà xe).</summary>
        public static Sprite Tank => s_Tank != null ? s_Tank : (s_Tank = Make(TankShape));

        /// <summary>Bánh răng (Cài đặt).</summary>
        public static Sprite Gear => s_Gear != null ? s_Gear : (s_Gear = Make(GearShape));

        /// <summary>Cánh cửa có mũi tên đi ra (Thoát game).</summary>
        public static Sprite Exit => s_Exit != null ? s_Exit : (s_Exit = Make(ExitShape));

        // ============================================================
        // HÌNH DẠNG (trả về khoảng cách có dấu: < 0 là bên trong)
        // ============================================================

        private static float PlayShape(float x, float y) => Tri(x, y, 22, 14, 54, 32, 22, 50);

        private static float TankShape(float x, float y)
        {
            float track = RoundBox(x, y, 32, 22, 22, 7, 6);
            float hull = Box(x, y, 32, 32, 17, 5);
            float turret = RoundBox(x, y, 30, 41, 10, 6, 3);
            float barrel = Box(x, y, 47, 42, 11, 2);
            return Mathf.Min(Mathf.Min(track, hull), Mathf.Min(turret, barrel));
        }

        private static float GearShape(float x, float y)
        {
            float d = Circle(x, y, 32, 32, 18);
            for (int k = 0; k < 4; k++)
                d = Mathf.Min(d, RotBox(x, y, 32, 32, 5.5f, 25f, k * 45f)); // 4 thanh = 8 răng
            return Mathf.Max(d, -Circle(x, y, 32, 32, 8f));                  // khoét lỗ giữa
        }

        private static float ExitShape(float x, float y)
        {
            float bracket = Mathf.Max(Box(x, y, 24, 32, 14, 22), -Box(x, y, 27, 32, 11, 18)); // khung cửa hở bên phải
            float shaft = Box(x, y, 40, 32, 12, 3);
            float head = Tri(x, y, 46, 22, 58, 32, 46, 42);
            return Mathf.Min(bracket, Mathf.Min(shaft, head));
        }

        // ============================================================
        // DỰNG SPRITE
        // ============================================================

        private static Sprite Make(Func<float, float, float> shape)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float d = shape(x + 0.5f, y + 0.5f);
                    float a = Mathf.Clamp01(0.5f - d); // khử răng cưa 1 pixel
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // ============================================================
        // HÌNH CƠ BẢN (SDF)
        // ============================================================

        private static float Box(float x, float y, float cx, float cy, float hx, float hy)
        {
            float dx = Mathf.Abs(x - cx) - hx;
            float dy = Mathf.Abs(y - cy) - hy;
            float ox = Mathf.Max(dx, 0f);
            float oy = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f);
        }

        private static float RoundBox(float x, float y, float cx, float cy, float hx, float hy, float r)
            => Box(x, y, cx, cy, hx - r, hy - r) - r;

        private static float Circle(float x, float y, float cx, float cy, float r)
            => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        private static float RotBox(float x, float y, float cx, float cy, float hx, float hy, float degrees)
        {
            float r = -degrees * Mathf.Deg2Rad;
            float dx = x - cx;
            float dy = y - cy;
            float rx = dx * Mathf.Cos(r) - dy * Mathf.Sin(r);
            float ry = dx * Mathf.Sin(r) + dy * Mathf.Cos(r);
            return Box(rx, ry, 0f, 0f, hx, hy);
        }

        /// <summary>Tam giác với ba đỉnh NGƯỢC chiều kim đồng hồ.</summary>
        private static float Tri(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            return Mathf.Max(Edge(x, y, ax, ay, bx, by),
                   Mathf.Max(Edge(x, y, bx, by, cx, cy), Edge(x, y, cx, cy, ax, ay)));
        }

        private static float Edge(float x, float y, float ax, float ay, float bx, float by)
        {
            float ex = bx - ax;
            float ey = by - ay;
            float len = Mathf.Sqrt(ex * ex + ey * ey);
            return ((x - ax) * ey - (y - ay) * ex) / len; // khoảng cách tới cạnh, dương = phía ngoài
        }
    }
}
