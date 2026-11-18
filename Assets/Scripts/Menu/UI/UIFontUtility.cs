using System.Text;
using TMPro;
using UnityEngine;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Chuẩn bị font TMP cho UI tiếng Việt.
    ///
    /// Vấn đề hay gặp: font Dynamic chỉ thêm ký tự vào atlas khi chữ đó được vẽ lần đầu. Tiếng Việt có hơn 130 ký tự
    /// có dấu, nếu atlas nhỏ hoặc cỡ lấy mẫu (Sampling Point Size) lớn thì atlas hết chỗ giữa chừng và những nhãn
    /// dựng SAU sẽ mất chữ. <see cref="Warmup"/> nạp trước toàn bộ ký tự cần dùng (và bật Multi Atlas) ngay lúc khởi động.
    /// </summary>
    public static class UIFontUtility
    {
        public static void Warmup(TMP_FontAsset font)
        {
            if (font == null) return;

            // Font tĩnh (Static) đã nướng sẵn ký tự, không thêm được nữa -> bỏ qua để khỏi cảnh báo thừa.
            if (font.atlasPopulationMode.ToString() != "Dynamic") return;

            font.isMultiAtlasTexturesEnabled = true;

            var sb = new StringBuilder(600);
            AppendRange(sb, 0x20, 0x7E);       // ASCII
            AppendRange(sb, 0xA0, 0x17F);      // Latin-1 + Latin Extended-A (Ă, Đ, ...)
            AppendRange(sb, 0x1A0, 0x1B0);     // Ơ ơ Ư ư
            AppendRange(sb, 0x1EA0, 0x1EF9);   // Latin Extended Additional (Ạ Ả Ấ Ầ ...)
            sb.Append("\u2013\u2014\u2018\u2019\u201C\u201D\u2026\u00B7\u2022");

            if (!font.TryAddCharacters(sb.ToString(), out string missing) && !string.IsNullOrEmpty(missing))
            {
                Debug.LogWarning(
                    $"[{nameof(UIFontUtility)}] Font \"{font.name}\" chưa nạp được {missing.Length} ký tự " +
                    "(font không có glyph đó hoặc atlas hết chỗ). Tăng Atlas Size, giảm Sampling Point Size " +
                    "hoặc bật Multi Atlas Textures trong Font Asset.");
            }
        }

        private static void AppendRange(StringBuilder sb, int from, int to)
        {
            for (int c = from; c <= to; c++) sb.Append((char)c);
        }
    }
}
