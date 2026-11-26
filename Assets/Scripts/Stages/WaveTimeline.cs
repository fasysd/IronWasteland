using System;
using System.Collections.Generic;

namespace IronWasteland.Stages
{
    /// <summary>
    /// Lịch chạy đợt quái (wave) — logic thuần C#, KHÔNG phụ thuộc UnityEngine.
    ///
    /// Vì sao tách riêng khỏi <see cref="EnemySpawner"/>:
    /// - Test được headless (Tools/WaveTimelineTests) mà không cần mở Unity.
    /// - Spawner chỉ lo Instantiate/Clone stats; "đợt nào bắn lúc nào" nằm ở đây.
    ///
    /// Quy tắc:
    /// - Wave bắn theo StartTime tăng dần, KHÔNG theo thứ tự khai báo trong list.
    ///   (Bản cũ trong StageDirector duyệt list nên nếu designer nhập lộn thứ tự thì
    ///   wave 90s có thể bắn trước wave 30s.)
    /// - Mỗi wave bắn đúng 1 lần; <see cref="Reset"/> để chạy lại màn.
    /// - Thời gian chỉ tiến, deltaTime âm bị bỏ qua.
    /// </summary>
    public sealed class WaveTimeline
    {
        private readonly float[] m_StartTimes;
        private readonly int[] m_Order;
        private int m_Next;
        private float m_Elapsed;

        /// <param name="startTimes">StartTime của từng wave, theo đúng index trong list của spawner.</param>
        public WaveTimeline(IReadOnlyList<float> startTimes)
        {
            int count = startTimes != null ? startTimes.Count : 0;
            m_StartTimes = new float[count];
            m_Order = new int[count];

            for (int i = 0; i < count; i++)
            {
                m_StartTimes[i] = startTimes[i];
                m_Order[i] = i;
            }

            SortByStartTimeStable();
        }

        /// <summary>Số wave trong lịch.</summary>
        public int Count => m_StartTimes.Length;

        /// <summary>Tổng thời gian đã trôi kể từ lần Reset gần nhất.</summary>
        public float Elapsed => m_Elapsed;

        /// <summary>Index (trong list gốc) của wave kế tiếp, -1 nếu đã bắn hết.</summary>
        public int NextWaveIndex => m_Next < m_Order.Length ? m_Order[m_Next] : -1;

        /// <summary>Số wave đã bắn (0..Count) — dùng cho HUD "Đợt X / Y".</summary>
        public int FiredCount => m_Next;

        /// <summary>Đã bắn hết mọi wave chưa.</summary>
        public bool IsFinished => m_Next >= m_Order.Length;

        /// <summary>Về lại mốc 0 và cho phép bắn lại từ đầu.</summary>
        public void Reset()
        {
            m_Next = 0;
            m_Elapsed = 0f;
        }

        /// <summary>Đẩy thời gian. deltaTime &lt;= 0 bị bỏ qua (không tua ngược).</summary>
        public void Advance(float deltaTime)
        {
            if (deltaTime > 0f) m_Elapsed += deltaTime;
        }

        /// <summary>Lấy wave kế tiếp nếu đã tới StartTime.</summary>
        public bool TryTakeDue(out int waveIndex)
        {
            if (m_Next < m_Order.Length)
            {
                int candidate = m_Order[m_Next];
                if (m_StartTimes[candidate] <= m_Elapsed)
                {
                    m_Next++;
                    waveIndex = candidate;
                    return true;
                }
            }

            waveIndex = -1;
            return false;
        }

        /// <summary>Lấy TẤT CẢ wave đã tới hạn trong 1 lần gọi, theo thứ tự thời gian.</summary>
        /// <returns>Số wave lấy được.</returns>
        public int TakeDue(List<int> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            int taken = 0;
            while (TryTakeDue(out int index))
            {
                results.Add(index);
                taken++;
            }
            return taken;
        }

        /// <summary>
        /// Sắp xếp index theo StartTime tăng dần. Dùng insertion sort để GIỮ ổn định
        /// thứ tự khai báo khi 2 wave cùng StartTime (wave khai báo trước bắn trước).
        /// </summary>
        private void SortByStartTimeStable()
        {
            for (int i = 1; i < m_Order.Length; i++)
            {
                int current = m_Order[i];
                float key = m_StartTimes[current];
                int j = i - 1;

                while (j >= 0 && m_StartTimes[m_Order[j]] > key)
                {
                    m_Order[j + 1] = m_Order[j];
                    j--;
                }
                m_Order[j + 1] = current;
            }
        }
    }
}
