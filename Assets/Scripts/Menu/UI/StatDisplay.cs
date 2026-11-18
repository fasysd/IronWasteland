using System;
using UnityEngine;
using IronWasteland.Tanks;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Mô tả 8 dòng chỉ số hiển thị (đúng thứ tự trong mockup Nhà xe).
    /// Chỉnh cách hiển thị (thang đo, đơn vị, màu thanh) ở một chỗ duy nhất này.
    /// </summary>
    public static class StatDisplay
    {
        public sealed class Row
        {
            public string Label;
            public string Unit;
            public Color Color;
            public bool IsPercent;
            public float BarMax;
            public Func<TankStats, float> Get;
        }

        // Nhân thêm khi hiển thị Tốc độ di chuyển (mockup ghi 110 trong khi dữ liệu có thể là 11).
        // Đổi thành 10f nếu muốn hiển thị theo thang đó.
        public const float MoveSpeedDisplayScale = 1f;

        public static readonly Row[] Rows =
        {
            new Row { Label = "TẤN CÔNG", Unit = "ATK", Color = UITheme.Orange, BarMax = 100f, Get = s => s.Attack },
            new Row { Label = "MÁU TỐI ĐA", Unit = "HP", Color = UITheme.Red, BarMax = 2000f, Get = s => s.MaxHealth },
            new Row { Label = "GIỚI HẠN NĂNG LƯỢNG", Unit = "EN", Color = UITheme.Blue, BarMax = 300f, Get = s => s.MaxEnergy },
            new Row { Label = "PHÒNG THỦ", Unit = "DEF", Color = UITheme.Teal, BarMax = 100f, Get = s => s.Defense },
            new Row { Label = "TỐC ĐỘ DI CHUYỂN", Unit = "SPD", Color = UITheme.Green, BarMax = 200f, Get = s => s.MoveSpeed * MoveSpeedDisplayScale },
            new Row { Label = "TỈ LỆ GIẢM HỒI CHIÊU", Unit = "%", Color = UITheme.TextDim, IsPercent = true, BarMax = TankStats.MaxCooldownReduction, Get = s => s.CooldownReduction },
            new Row { Label = "TỈ LỆ SÁT THƯƠNG", Unit = "%", Color = UITheme.TextDim, IsPercent = true, BarMax = 1f, Get = s => s.DamageRate },
            new Row { Label = "TỈ LỆ XUYÊN GIÁP", Unit = "%", Color = UITheme.TextDim, IsPercent = true, BarMax = TankStats.MaxArmorPenetration, Get = s => s.ArmorPenetration },
        };

        // Chỉ số hiện ở 4 thẻ của Main Menu: ATK, HP, EN, SPD.
        public static readonly int[] MainMenuRows = { 0, 1, 2, 4 };

        public static float Value(Row row, TankStats stats) => stats == null ? 0f : row.Get(stats);

        public static string Format(Row row, float value)
        {
            if (row.IsPercent) return Mathf.RoundToInt(value * 100f).ToString();

            // Chỉ số nhỏ (Tấn công, Phòng thủ...) tăng lẻ mỗi cấp (nội suy tuyến tính) nên hiện 1 số thập phân,
            // nếu không thì lên cấp mà số trông như đứng yên.
            if (value >= 100f || Mathf.Abs(value - Mathf.Round(value)) < 0.05f) return Mathf.RoundToInt(value).ToString();
            return value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Chuỗi chênh lệch khi lên cấp, vd "+0.4". Rỗng nếu không đáng kể.</summary>
        public static string FormatDelta(Row row, float delta)
        {
            float shown = row.IsPercent ? delta * 100f : delta;
            if (shown < 0.05f) return "";
            return "+" + (shown >= 10f ? Mathf.RoundToInt(shown).ToString() : shown.ToString("0.#"));
        }

        public static float Fill01(Row row, float value) => row.BarMax <= 0f ? 0f : Mathf.Clamp01(value / row.BarMax);
    }
}
