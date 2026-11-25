using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Progression
{
    /// <summary>
    /// Tiến trình tối giản của người chơi: cấp của từng xe.
    /// Hiện lưu bằng PlayerPrefs theo tên asset TankDefinition; mặc định cấp 1.
    /// </summary>
    public static class PlayerProgress
    {
        private const string LevelKeyPrefix = "tank.level.";

        private static readonly Dictionary<string, int> Cache = new Dictionary<string, int>();

        /// <summary>Cấp hiện tại của xe (mặc định 1 nếu chưa có dữ liệu).</summary>
        public static int GetTankLevel(string tankKey)
        {
            if (string.IsNullOrEmpty(tankKey)) return 1;

            if (Cache.TryGetValue(tankKey, out int cached)) return cached;

            int level = PlayerPrefs.GetInt(LevelKeyPrefix + tankKey, 1);
            if (level < 1) level = 1;
            Cache[tankKey] = level;
            return level;
        }

        /// <summary>Kiểm tra xe đã có cấp riêng do người chơi lưu chưa (chưa có thì dùng cấp khởi điểm của asset).</summary>
        public static bool HasTankLevel(string tankKey)
        {
            return !string.IsNullOrEmpty(tankKey) && PlayerPrefs.HasKey(LevelKeyPrefix + tankKey);
        }

        /// <summary>Ghi cấp mới cho xe; cấp được kẹp trong 1..90.</summary>
        public static void SetTankLevel(string tankKey, int level)
        {
            if (string.IsNullOrEmpty(tankKey)) return;

            int clamped = Mathf.Clamp(level, 1, Tanks.TankDefinition.MaxLevel);
            Cache[tankKey] = clamped;
            PlayerPrefs.SetInt(LevelKeyPrefix + tankKey, clamped);
        }
    }
}
