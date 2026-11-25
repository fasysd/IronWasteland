using System;

namespace IronWasteland.UiModels
{
    /// <summary>Kết quả tính toán cho một lần phân rã phụ kiện.</summary>
    public readonly struct SalvageQuote
    {
        public readonly int Quantity;
        public readonly int TotalGems;
        public readonly bool CanSalvage;
        public readonly string Hint;

        public SalvageQuote(int quantity, int totalGems, bool canSalvage, string hint)
        {
            Quantity = quantity;
            TotalGems = totalGems;
            CanSalvage = canSalvage;
            Hint = hint;
        }
    }

    /// <summary>Tính số lượng phân rã hợp lệ và số ngọc nhận được.</summary>
    public static class SalvageQuoteModel
    {
        /// <summary>Gợi ý khi phụ kiện đang được một xe trang bị.</summary>
        public const string EquippedHint = "ĐANG TRANG BỊ · THÁO Ở MÀN CHỌN PHỤ KIỆN ĐỂ PHÂN RÃ";

        /// <summary>Gợi ý khi người chơi không còn bản sao nào.</summary>
        public const string NotOwnedHint = "CHƯA SỞ HỮU";

        /// <summary>Số lượng tối đa cho một lần phân rã.</summary>
        public const int MaxQuantity = 99;

        /// <summary>Số ngọc ngẫu nhiên tối thiểu & tối đa cho mỗi phụ kiện (GDD: 8–12).</summary>
        public const int MinGemsPerAccessory = 8;
        public const int MaxGemsPerAccessory = 12;

        public static SalvageQuote Build(int owned, int salvageValue, int requested, bool isEquipped)
        {
            // Toàn bộ phụ kiện có trong kho đều phân rã được nếu số lượng >= 1 (không chặn đang trang bị).
            if (owned <= 0)
            {
                return new SalvageQuote(0, 0, false, NotOwnedHint);
            }

            int max = Math.Min(owned, MaxQuantity);
            int quantity = requested < 1 ? 1 : requested;
            if (quantity > max)
            {
                quantity = max;
            }

            int minTotal = quantity * MinGemsPerAccessory;
            int maxTotal = quantity * MaxGemsPerAccessory;
            return new SalvageQuote(quantity, minTotal, true, $"NHẬN: {minTotal}–{maxTotal} NGỌC");
        }
    }
}
