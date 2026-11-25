using System;

namespace IronWasteland.UiModels
{
    /// <summary>
    /// Dữ liệu thô của một xe trong danh sách Garage, do controller map từ TankDefinition sang.
    /// Không phụ thuộc UnityEngine để test được ngoài scene.
    /// </summary>
    public readonly struct TankRowSource
    {
        public readonly string Key;
        public readonly string DisplayName;
        public readonly string RolePrimary;
        public readonly string RoleSecondary;
        /// <summary>Mã ải mở khoá (vd "2-5"); rỗng = mở sẵn.</summary>
        public readonly string UnlockStageCode;

        public TankRowSource(string key, string displayName, string rolePrimary, string roleSecondary, string unlockStageCode)
        {
            Key = key;
            DisplayName = displayName;
            RolePrimary = rolePrimary;
            RoleSecondary = roleSecondary;
            UnlockStageCode = unlockStageCode;
        }
    }

    /// <summary>Một hàng đã sẵn sàng để đổ vào UI danh sách xe.</summary>
    public readonly struct TankRow
    {
        public readonly string Key;
        public readonly string DisplayName;
        public readonly string RoleText;
        public readonly string LevelText;
        public readonly string UnlockText;
        public readonly bool IsLocked;
        /// <summary>Độ dài thanh tiến trình 0..1 (cấp hiện tại / cấp tối đa).</summary>
        public readonly float BarFraction;

        public TankRow(string key, string displayName, string roleText, string levelText, string unlockText, bool isLocked, float barFraction)
        {
            Key = key;
            DisplayName = displayName;
            RoleText = roleText;
            LevelText = levelText;
            UnlockText = unlockText;
            IsLocked = isLocked;
            BarFraction = barFraction;
        }
    }

    /// <summary>
    /// Dựng danh sách hàng của panel "DANH SÁCH XE TĂNG" từ dữ liệu roster + tiến trình người chơi.
    /// </summary>
    public static class TankListModel
    {
        /// <summary>Cấp tối đa của một xe (khớp TankDefinition.MaxLevel).</summary>
        public const int MaxLevel = 90;

        public static TankRow[] Build(TankRowSource[] roster, Func<string, int> levelOf, Func<string, bool> isStageUnlocked)
        {
            if (roster == null || roster.Length == 0) return Array.Empty<TankRow>();

            TankRow[] rows = new TankRow[roster.Length];
            for (int i = 0; i < roster.Length; i++)
            {
                TankRowSource src = roster[i];
                int level = levelOf != null ? levelOf(src.Key) : 1;
                if (level < 1) level = 1;
                if (level > MaxLevel) level = MaxLevel;

                string roleText = string.IsNullOrEmpty(src.RoleSecondary)
                    ? src.RolePrimary
                    : src.RolePrimary + " / " + src.RoleSecondary;

                // Xe có mã ải mở khoá mà ải đó chưa mở thì bị khoá.
                bool isLocked = !string.IsNullOrEmpty(src.UnlockStageCode)
                    && (isStageUnlocked == null || !isStageUnlocked(src.UnlockStageCode));
                string unlockText = isLocked ? "ẢI " + src.UnlockStageCode : string.Empty;

                rows[i] = new TankRow(
                    src.Key,
                    src.DisplayName,
                    roleText,
                    "CẤP " + level.ToString("00") + " / " + MaxLevel,
                    unlockText,
                    isLocked,
                    level / (float)MaxLevel);
            }

            return rows;
        }
    }
}
