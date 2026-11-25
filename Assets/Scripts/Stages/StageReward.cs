using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Stages
{
    /// <summary>Ket qua thuong cua 1 man.</summary>
    [Serializable]
    public struct StageRewardResult
    {
        public int Gems;
        public int AccessoryCount;
        public int[] DroppedAccessoryIndices;

        public StageRewardResult(int gems, int accessoryCount, int[] droppedAccessoryIndices = null)
        {
            Gems = gems;
            AccessoryCount = accessoryCount;
            DroppedAccessoryIndices = droppedAccessoryIndices ?? Array.Empty<int>();
        }

        public override string ToString() => $"Ngoc {Gems}, phu kien {AccessoryCount}";
    }

    /// <summary>
    /// Tinh thuong khi thang man theo bang ai trong GDD (5.6 & 5.7):
    /// - Moi man co so ngoc co dinh.
    /// - Phu kien roi 20% doc lap theo pool cua man, khong nhan trung 2 phu kien.
    /// </summary>
    public static class StageReward
    {
        /// <summary>Ty le roi cua tung mon phu kien (20% theo bang ai).</summary>
        public const float AccessoryDropChance = 0.5f;

        /// <summary>
        /// Quay thuong. <paramref name="rng"/> de test (truyen vao ham tra ve 0..1),
        /// mac dinh dung UnityEngine.Random.
        /// </summary>
        public static StageRewardResult Roll(StageDefinition definition, Func<float> rng = null)
        {
            if (definition == null) return new StageRewardResult(0, 0);

            int gems = Mathf.Max(0, definition.RewardGems);
            int minGuaranteed = Mathf.Max(0, definition.RewardAccessoryCount);
            IReadOnlyList<int> pool = definition.RewardAccessoryPool;

            if (pool == null || pool.Count == 0)
            {
                if (minGuaranteed == 0) return new StageRewardResult(gems, 0);
                int droppedCount = 0;
                for (int i = 0; i < minGuaranteed; i++)
                {
                    if (definition.GuaranteeAccessories) { droppedCount++; continue; }
                    float roll = rng != null ? rng() : UnityEngine.Random.value;
                    if (roll < AccessoryDropChance) droppedCount++;
                }
                return new StageRewardResult(gems, Mathf.Max(droppedCount, minGuaranteed));
            }

            // Loại bỏ trùng lặp trong pool -> Z loại phụ kiện có thể nhận
            var candidates = new List<int>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                if (!candidates.Contains(pool[i])) candidates.Add(pool[i]);
            }

            var droppedList = new List<int>();
            var remaining = new List<int>();

            // Bước 1: Roll Z lần độc lập với tỷ lệ X% (20%) cho mỗi phụ kiện -> có thể trúng 0..Z món
            for (int i = 0; i < candidates.Count; i++)
            {
                float roll = rng != null ? rng() : UnityEngine.Random.value;
                if (roll < AccessoryDropChance)
                {
                    droppedList.Add(candidates[i]);
                }
                else
                {
                    remaining.Add(candidates[i]);
                }
            }

            // Bước 2: Nếu số lượng trúng < Y (minGuaranteed), chọn bù ngẫu nhiên từ các món còn lại cho đủ Y (không trùng)
            int targetMin = Mathf.Min(minGuaranteed, candidates.Count);
            while (droppedList.Count < targetMin && remaining.Count > 0)
            {
                int pickIdx = UnityEngine.Random.Range(0, remaining.Count);
                droppedList.Add(remaining[pickIdx]);
                remaining.RemoveAt(pickIdx);
            }

            return new StageRewardResult(gems, droppedList.Count, droppedList.ToArray());
        }
    }
}
