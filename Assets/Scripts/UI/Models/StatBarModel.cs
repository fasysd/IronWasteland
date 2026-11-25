using System;

namespace IronWasteland.UiModels
{
    /// <summary>
    /// Dựng 8 thanh chỉ số của panel "CHỈ SỐ CHIẾN XA".
    /// Thứ tự thanh cố định theo đúng thứ tự hiển thị trên panel.
    /// </summary>
    public static class StatBarModel
    {
        /// <summary>Số thanh chỉ số trên panel.</summary>
        public const int Count = 8;

        /// <summary>
        /// Trả về độ dài 0..1 cho từng thanh, chuẩn hoá theo giá trị lớn nhất trong roster.
        /// </summary>
        public static float[] Build(float[] values, float[] maxima)
        {
            float[] bars = new float[Count];
            if (values == null) return bars;

            for (int i = 0; i < Count && i < values.Length; i++)
            {
                float max = maxima != null && i < maxima.Length ? maxima[i] : 0f;
                // Trục bằng 0 thì không có gì để so sánh: thanh rỗng.
                bars[i] = max > 0f ? Clamp01(values[i] / max) : 0f;
            }

            return bars;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
