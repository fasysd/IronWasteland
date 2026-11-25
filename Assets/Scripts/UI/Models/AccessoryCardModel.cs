using System;

namespace IronWasteland.UiModels
{
    /// <summary>Dữ liệu thô của một phụ kiện, do controller map từ AccessoryCatalog.Entry sang.</summary>
    public readonly struct AccessoryCardSource
    {
        public readonly string DisplayName;
        /// <summary>Các dòng hiệu ứng chỉ số, mỗi phần tử là một dòng đã format sẵn.</summary>
        public readonly string[] StatLines;
        /// <summary>Mô tả hiệu ứng bị động (rỗng nếu không có).</summary>
        public readonly string PassiveText;

        public AccessoryCardSource(string displayName, string[] statLines, string passiveText)
        {
            DisplayName = displayName;
            StatLines = statLines;
            PassiveText = passiveText;
        }
    }

    /// <summary>Nội dung đã sẵn sàng để đổ vào panel "PHỤ KIỆN TRANG BỊ".</summary>
    public readonly struct AccessoryCard
    {
        public readonly bool HasAccessory;
        public readonly string NameText;
        /// <summary>Các dòng hiệu ứng chỉ số, nối bằng xuống dòng (rỗng nếu không có).</summary>
        public readonly string BonusText;
        /// <summary>Mô tả hiệu ứng bị động (rỗng nếu không có).</summary>
        public readonly string PassiveText;

        public AccessoryCard(bool hasAccessory, string nameText, string bonusText, string passiveText)
        {
            HasAccessory = hasAccessory;
            NameText = nameText;
            BonusText = bonusText;
            PassiveText = passiveText;
        }
    }

    /// <summary>Dựng nội dung panel phụ kiện đang trang bị.</summary>
    public static class AccessoryCardModel
    {
        /// <summary>Chữ hiển thị khi chưa trang bị gì.</summary>
        public const string EmptyName = "CHƯA TRANG BỊ";

        private static string JoinLines(string[] lines)
        {
            if (lines == null) return string.Empty;

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) continue;
                if (builder.Length > 0) builder.Append('\n');
                builder.Append(lines[i]);
            }

            return builder.ToString();
        }

        public static AccessoryCard Build(AccessoryCardSource? source)
        {
            if (source == null)
            {
                return new AccessoryCard(false, EmptyName, string.Empty, string.Empty);
            }

            return new AccessoryCard(
                true,
                source.Value.DisplayName,
                JoinLines(source.Value.StatLines),
                source.Value.PassiveText ?? string.Empty);
        }
    }
}
