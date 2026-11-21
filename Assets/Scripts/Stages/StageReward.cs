using System;
using UnityEngine;

namespace IronWasteland.Stages
{
    /// <summary>Ket qua thuong cua 1 man.</summary>
    [Serializable]
    public struct StageRewardResult
    {
        public int Gems;
        public int AccessoryCount;

        public StageRewardResult(int gems, int accessoryCount)
        {
            Gems = gems;
            AccessoryCount = accessoryCount;
        }

        public override string ToString() => $"Ngoc {Gems}, phu kien {AccessoryCount}";
    }

    /// <summary>
    /// Tinh thuong khi thang man. Chi tinh SO LUONG - viec cong ngoc vao vi,
    /// chon phu kien nao roi va luu lai thuoc ve he thong Garage/Profile (chua lam).
    ///
    /// Quy uoc lay tu bang ai trong GDD (PROJECT_CONTEXT 10.4 + 5.6):
    /// - Moi man co so ngoc co dinh.
    /// - Phu kien roi 20% moi mon; man boss thi chac chan du so mon.
    /// </summary>
    public static class StageReward
    {
        /// <summary>Ty le roi cua tung mon phu kien (20% theo bang ai).</summary>
        public const float AccessoryDropChance = 0.2f;

        /// <summary>
        /// Quay thuong. <paramref name="rng"/> de test (truyen vao ham tra ve 0..1),
        /// mac dinh dung UnityEngine.Random.
        /// </summary>
        public static StageRewardResult Roll(StageDefinition definition, Func<float> rng = null)
        {
            if (definition == null) return new StageRewardResult(0, 0);

            int gems = Mathf.Max(0, definition.RewardGems);
            int wanted = Mathf.Max(0, definition.RewardAccessoryCount);
            int dropped = 0;

            for (int i = 0; i < wanted; i++)
            {
                if (definition.GuaranteeAccessories)
                {
                    dropped++;
                    continue;
                }

                float roll = rng != null ? rng() : UnityEngine.Random.value;
                if (roll < AccessoryDropChance) dropped++;
            }

            return new StageRewardResult(gems, dropped);
        }
    }
}
