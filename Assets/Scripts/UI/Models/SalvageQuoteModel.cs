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

        public static SalvageQuote Build(int owned, int salvageValue, int requested, bool isEquipped)
        {
            // Không còn bản sao nào thì luôn báo CHƯA SỞ HỮU, kể cả khi save cũ còn trỏ
            // loadout tới món đó: người chơi cần biết mình không có gì để phân rã.
            if (owned <= 0)
            {
                return new SalvageQuote(0, 0, false, NotOwnedHint);
            }

            if (isEquipped)
            {
                return new SalvageQuote(0, 0, false, EquippedHint);
            }

            int max = Math.Min(owned, MaxQuantity);
            int quantity = requested < 1 ? 1 : requested;
            if (quantity > max)
            {
                quantity = max;
            }

            int total = quantity * Math.Max(0, salvageValue);
            return new SalvageQuote(quantity, total, true, "NHẬN: " + total + " NGỌC");
        }
    }
}
