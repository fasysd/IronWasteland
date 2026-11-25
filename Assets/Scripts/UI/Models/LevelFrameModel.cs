using System;

namespace IronWasteland.UiModels
{
    /// <summary>Khung CẤP ĐỘ của main menu sau khi chuẩn hoá dữ liệu.</summary>
    public readonly struct LevelFrame
    {
        public readonly int Level;
        public readonly int MaxLevel;
        /// <summary>Số cấp hai chữ số (vd "05").</summary>
        public readonly string LevelText;
        /// <summary>Hậu tố mẫu số (vd "/ 90").</summary>
        public readonly string MaxText;

        public LevelFrame(int level, int maxLevel, string levelText, string maxText)
        {
            Level = level;
            MaxLevel = maxLevel;
            LevelText = levelText;
            MaxText = maxText;
        }
    }

    /// <summary>
    /// Chuẩn hoá cấp hiển thị của khung CẤP ĐỘ main menu: kẹp 1..cấp tối đa, in hai chữ số,
    /// mẫu số lấy từ TankDefinition.MaxLevel thay vì nướng sẵn trong art.
    /// </summary>
    public static class LevelFrameModel
    {
        /// <summary>Cấp tối đa mặc định khi dữ liệu đầu vào không hợp lệ.</summary>
        public const int DefaultMaxLevel = 90;

        public static LevelFrame Build(int level, int maxLevel)
        {
            int max = maxLevel < 1 ? DefaultMaxLevel : maxLevel;
            int clamped = level < 1 ? 1 : (level > max ? max : level);
            return new LevelFrame(clamped, max, clamped.ToString("00"), "/ " + max);
        }
    }
}
