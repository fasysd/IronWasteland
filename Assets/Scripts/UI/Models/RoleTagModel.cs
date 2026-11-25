using System;
using System.Collections.Generic;

namespace IronWasteland.UiModels
{
    /// <summary>Một pill vai trò đã sẵn sàng đổ vào UI main menu.</summary>
    public readonly struct RoleTag
    {
        /// <summary>Chữ hiển thị trong pill (vd "TANKER").</summary>
        public readonly string Text;
        /// <summary>Màu pill dạng 0xRRGGBB, đã chốt theo từ khoá vai trò hoặc màu accent của xe.</summary>
        public readonly uint Argb;

        public RoleTag(string text, uint argb)
        {
            Text = text;
            Argb = argb;
        }
    }

    /// <summary>
    /// Dựng tối đa hai pill vai trò (chính + phụ) cho khu trưng bày main menu.
    /// Không phụ thuộc UnityEngine để test được ngoài scene.
    /// </summary>
    public static class RoleTagModel
    {
        /// <summary>Màu xám dùng khi xe không có accent lẫn từ khoá vai trò quen thuộc.</summary>
        public const uint DefaultArgb = 0x5A6675;

        /// <summary>Màu pill theo từ khoá vai trò; không khớp thì lấy màu accent của xe.</summary>
        public static RoleTag[] Build(string primary, string secondary, uint accentArgb)
        {
            List<RoleTag> tags = new List<RoleTag>(2);
            AddTag(tags, primary, accentArgb);
            AddTag(tags, secondary, accentArgb);
            return tags.ToArray();
        }

        private static void AddTag(List<RoleTag> tags, string raw, uint accentArgb)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;

            string text = raw.Trim();
            for (int i = 0; i < tags.Count; i++)
            {
                // Vai trò chính và phụ trùng nhau (vd DPS/DPS) thì chỉ vẽ một pill.
                if (string.Equals(tags[i].Text, text, StringComparison.OrdinalIgnoreCase)) return;
            }

            tags.Add(new RoleTag(text, ColorFor(text, accentArgb)));
        }

        private static uint ColorFor(string text, uint accentArgb)
        {
            if (Contains(text, "DPS")) return 0x8A4BF0u;
            if (Contains(text, "TANKER")) return 0xFF7A18u;
            if (Contains(text, "HỖ TRỢ") || Contains(text, "SUPPORT")) return 0x2FE8E0u;
            if (Contains(text, "SÁT THỦ")) return 0xFF3B4Eu;
            return accentArgb != 0u ? accentArgb : DefaultArgb;
        }

        private static bool Contains(string haystack, string needle)
        {
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
