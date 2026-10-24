
using UnityEngine;

namespace IronWasteland
{
    /// <summary>
    /// Các tiện ích tính toán vùng giao nhau giữa hai Collider2D.
    /// </summary>
    public static class Collider2DUtility
    {
        /// <summary>
        /// Ước lượng tâm vùng giao nhau giữa hai Collider2D
        /// bằng cách lấy mẫu các điểm trong vùng Bounds giao nhau.
        /// </summary>
        /// <param name="colliderA">Collider thứ nhất.</param>
        /// <param name="colliderB">Collider thứ hai.</param>
        /// <param name="center">Tâm vùng giao nhau ước lượng.</param>
        /// <param name="samplesPerAxis">
        /// Số điểm chia trên mỗi trục. Tổng số điểm tối đa
        /// là samplesPerAxis * samplesPerAxis.
        /// </param>
        public static bool TryGetApproximateOverlapCenter(
            Collider2D colliderA,
            Collider2D colliderB,
            out Vector2 center,
            int samplesPerAxis = 15)
        {
            center = default;

            if (!IsValid(colliderA) || !IsValid(colliderB))
                return false;

            if (!TryGetIntersectionBounds(
                    colliderA, colliderB,
                    out Vector2 min, out Vector2 max))
            {
                return false;
            }

            int resolution = Mathf.Max(4, samplesPerAxis);

            float stepX = (max.x - min.x) / resolution;
            float stepY = (max.y - min.y) / resolution;

            Vector2 sum = Vector2.zero;
            int count = 0;

            for (int x = 0; x < resolution; x++)
            {
                for (int y = 0; y < resolution; y++)
                {
                    Vector2 point = new Vector2(
                        min.x + (x + 0.5f) * stepX,
                        min.y + (y + 0.5f) * stepY
                    );

                    if (!colliderA.OverlapPoint(point) ||
                        !colliderB.OverlapPoint(point))
                    {
                        continue;
                    }

                    sum += point;
                    count++;
                }
            }

            if (count > 0)
            {
                center = sum / count;
                return true;
            }

            // Không có điểm mẫu hợp lệ:
            // dùng tâm Bounds giao nhau làm phương án dự phòng.
            center = (min + max) * 0.5f;
            return true;
        }

        /// <summary>
        /// Lấy ngẫu nhiên một điểm nằm trong vùng giao nhau
        /// thực tế của hai Collider2D.
        /// </summary>
        /// <param name="colliderA">Collider thứ nhất.</param>
        /// <param name="colliderB">Collider thứ hai.</param>
        /// <param name="point">Điểm ngẫu nhiên tìm được.</param>
        /// <param name="maxAttempts">Số lần thử tối đa.</param>
        public static bool TryGetRandomOverlapPoint(
            Collider2D colliderA,
            Collider2D colliderB,
            out Vector2 point,
            int maxAttempts = 40)
        {
            point = default;

            if (!IsValid(colliderA) || !IsValid(colliderB))
                return false;

            if (!TryGetIntersectionBounds(
                    colliderA, colliderB,
                    out Vector2 min, out Vector2 max))
            {
                return false;
            }

            int attempts = Mathf.Max(1, maxAttempts);

            for (int i = 0; i < attempts; i++)
            {
                Vector2 candidate = new Vector2(
                    Random.Range(min.x, max.x),
                    Random.Range(min.y, max.y)
                );

                if (!colliderA.OverlapPoint(candidate) ||
                    !colliderB.OverlapPoint(candidate))
                {
                    continue;
                }

                point = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Kiểm tra Collider có hợp lệ để truy vấn hay không.
        /// </summary>
        private static bool IsValid(Collider2D collider)
        {
            return collider != null
                && collider.enabled
                && collider.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Tìm hình chữ nhật Bounds giao nhau của hai Collider.
        /// </summary>
        private static bool TryGetIntersectionBounds(
            Collider2D colliderA,
            Collider2D colliderB,
            out Vector2 min,
            out Vector2 max)
        {
            Bounds a = colliderA.bounds;
            Bounds b = colliderB.bounds;

            min = Vector2.Max(a.min, b.min);
            max = Vector2.Min(a.max, b.max);

            return min.x < max.x && min.y < max.y;
        }
    }
}