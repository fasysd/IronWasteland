using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>Hàm dùng chung cho bộ kỹ năng Water B (tránh lặp code lấy chuột / quét xe tăng).</summary>
    internal static class WaterB_Utils
    {
        public static Vector3 GetMouseWorldPosition()
        {
            Vector2 screenPos = Vector2.zero;
            if (Pointer.current != null) screenPos = Pointer.current.position.ReadValue();

            Vector3 world = Vector3.zero;
            if (Camera.main != null)
            {
                Vector3 p = new Vector3(screenPos.x, screenPos.y, Mathf.Abs(Camera.main.transform.position.z));
                world = Camera.main.ScreenToWorldPoint(p);
            }
            world.z = 0f;
            return world;
        }

        /// <summary>Lấy mọi TankBrain còn sống trong bán kính (mỗi xe chỉ xuất hiện 1 lần).</summary>
        public static void GetTanksInRadius(Vector3 center, float radius, List<TankBrain> results)
        {
            results.Clear();
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                TankBrain tank = hits[i].GetComponentInParent<TankBrain>();
                if (tank != null && tank.IsAlive && !results.Contains(tank))
                {
                    results.Add(tank);
                }
            }
        }

        /// <summary>Tìm xe tăng gần điểm chỉ định nhất (thường là vị trí con trỏ chuột).</summary>
        public static TankBrain FindTankNearPoint(Vector3 point, float radius, TankBrain exclude)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(point, radius);
            TankBrain best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                TankBrain tank = hits[i].GetComponentInParent<TankBrain>();
                if (tank == null || tank == exclude || !tank.IsAlive) continue;

                float d = Vector2.Distance(point, tank.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = tank;
                }
            }
            return best;
        }

        private static Sprite s_DiscSprite;

        /// <summary>Sprite hình tròn trắng đường kính 1 đơn vị (tạo bằng code, dùng chung cho các vòng/hào quang).</summary>
        public static Sprite GetDiscSprite()
        {
            if (s_DiscSprite != null) return s_DiscSprite;

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01((1f - d) * c * 0.5f); // mép mềm vài pixel
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            s_DiscSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return s_DiscSprite;
        }

        /// <summary>Co/giãn root để sprite phủ đúng đường kính = 2 * radius (đồng bộ hình ảnh với vùng ảnh hưởng thật).</summary>
        public static void FitVisualToRadius(Transform root, SpriteRenderer sr, float radius)
        {
            if (root == null || sr == null || sr.sprite == null || radius <= 0f) return;

            float currentWidth = sr.bounds.size.x;
            if (currentWidth <= 0.0001f) return;

            root.localScale *= (radius * 2f) / currentWidth;
        }
    }
}
