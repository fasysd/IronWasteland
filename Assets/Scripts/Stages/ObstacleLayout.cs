using System.Collections.Generic;

namespace IronWasteland.Stages
{
    /// <summary>Một vị trí vật cản do <see cref="ObstacleLayout"/> sinh ra.</summary>
    public readonly struct ObstaclePlacement
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Radius;

        public ObstaclePlacement(float x, float y, float radius)
        {
            X = x;
            Y = y;
            Radius = radius;
        }
    }

    /// <summary>
    /// Sinh layout vật cản (đá/tường đổ) cho 1 màn — logic thuần C#, test headless được.
    ///
    /// Vì sao sinh bằng code thay vì đặt tay 9 scene:
    /// - Mỗi màn có layout riêng theo seed (mã màn) nên "đa dạng" mà không phải copy tay.
    /// - Thuật toán đảm bảo: chừa khoảng trống quanh điểm xuất phát, không chặn vùng spawn
    ///   quái ở rìa, không chồng lên nhau.
    /// <see cref="StageObstacleField"/> chỉ việc dựng GameObject từ danh sách này.
    /// </summary>
    public static class ObstacleLayout
    {
        /// <summary>Bán kính tối thiểu tính từ tâm màn (chừa chỗ cho tank xuất phát).</summary>
        public const float MinCenterDistance = 4f;

        /// <summary>Bán kính tối đa: giữ vật cản bên trong sân, tránh vùng spawn quái ở rìa.</summary>
        public const float MaxRingDistance = 10f;

        /// <summary>Khoảng cách tối thiểu giữa 2 vật cản (tâm tới tâm).</summary>
        public const float MinSeparation = 2.2f;

        public const float MinRadius = 0.6f;
        public const float MaxRadius = 1.3f;

        /// <summary>Chừa thêm khoảng đệm quanh vùng cấm (vùng spawn quái) để quái không sinh trúng đá.</summary>
        public const float KeepOutPadding = 0.6f;

        /// <summary>
        /// Sinh layout xác định (cùng seed + count ⇒ cùng kết quả).
        /// </summary>
        /// <param name="seed">Thường là hash của mã màn (1-1, 2-3...).</param>
        /// <param name="count">Số vật cản mong muốn.</param>
        /// <param name="attemptsPerObstacle">Số lần thử đặt mỗi vật cản trước khi bỏ qua.</param>
        /// <param name="keepOut">
        /// Vùng cấm (thường là các SpawnZone): vật cản không được chạm vào,
        /// nếu không quái sinh ra sẽ kẹt trong đá.
        /// </param>
        public static List<ObstaclePlacement> Build(
            int seed,
            int count,
            int attemptsPerObstacle = 32,
            IReadOnlyList<ObstaclePlacement> keepOut = null)
        {
            var result = new List<ObstaclePlacement>();
            if (count <= 0) return result;

            var rng = new DeterministicRandom(seed);
            int attempts = attemptsPerObstacle > 0 ? attemptsPerObstacle : 1;

            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < attempts; attempt++)
                {
                    float angle = rng.Next01() * 6.2831853f;
                    float radius = MinRadius + rng.Next01() * (MaxRadius - MinRadius);
                    float distance = MinCenterDistance + rng.Next01() * (MaxRingDistance - MinCenterDistance);

                    float x = (float)System.Math.Cos(angle) * distance;
                    float y = (float)System.Math.Sin(angle) * distance;

                    if (IsTooCloseToOthers(result, x, y, radius)) continue;
                    if (OverlapsKeepOut(keepOut, x, y, radius)) continue;

                    result.Add(new ObstaclePlacement(x, y, radius));
                    break;
                }
            }

            return result;
        }

        private static bool OverlapsKeepOut(IReadOnlyList<ObstaclePlacement> keepOut, float x, float y, float radius)
        {
            if (keepOut == null) return false;

            for (int i = 0; i < keepOut.Count; i++)
            {
                float dx = keepOut[i].X - x;
                float dy = keepOut[i].Y - y;
                float minGap = keepOut[i].Radius + radius + KeepOutPadding;

                if (dx * dx + dy * dy < minGap * minGap) return true;
            }
            return false;
        }

        private static bool IsTooCloseToOthers(List<ObstaclePlacement> placed, float x, float y, float radius)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                float dx = placed[i].X - x;
                float dy = placed[i].Y - y;
                float minGap = MinSeparation + (placed[i].Radius + radius) * 0.5f;

                if (dx * dx + dy * dy < minGap * minGap) return true;
            }
            return false;
        }

        /// <summary>
        /// Random nội bộ (LCG) để kết quả giống nhau trên mọi máy — System.Random
        /// đã đổi thuật toán giữa các phiên bản .NET nên không dùng được.
        /// </summary>
        private struct DeterministicRandom
        {
            private uint m_State;

            public DeterministicRandom(int seed)
            {
                unchecked
                {
                    m_State = (uint)seed * 2654435761u + 1013904223u;
                    if (m_State == 0u) m_State = 0x9E3779B9u;
                }
            }

            public float Next01()
            {
                unchecked
                {
                    m_State = m_State * 1664525u + 1013904223u;
                }
                return (m_State >> 8) / 16777216f;
            }
        }
    }
}
