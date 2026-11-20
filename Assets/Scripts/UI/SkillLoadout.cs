using UnityEngine;

namespace IronWasteland.UI
{
    /// <summary>
    /// Trạng thái lựa chọn kỹ năng tùy chọn trong phiên chơi.
    /// Garage ghi lựa chọn hiện tại, Picker đọc/ghi lựa chọn mới. Dữ liệu nằm ở
    /// static nên sống qua các lần chuyển scene; khi nối hệ lưu hồ sơ người chơi
    /// chỉ cần thay phần đọc/ghi trong lớp này.
    /// </summary>
    public static class SkillLoadout
    {
        private const string TankPreferenceKey = "iw.loadout.tank";
        private const string SkillPreferencePrefix = "iw.loadout.skill.";

        private static readonly int[,] Selected = new int[SkillCatalog.TankCount, SkillCatalog.TabCount];
        private static int s_LastTankIndex = 2;

        /// <summary>
        /// Nạp lựa chọn đã lưu từ PlayerPrefs khi lớp được dùng lần đầu.
        /// Dữ liệu này sống qua các lần chuyển scene và cả khi tắt game.
        /// </summary>
        static SkillLoadout()
        {
            s_LastTankIndex = Mathf.Clamp(
                PlayerPrefs.GetInt(TankPreferenceKey, s_LastTankIndex),
                0,
                SkillCatalog.TankCount - 1);

            for (int tank = 0; tank < SkillCatalog.TankCount; tank++)
            {
                for (int tab = 0; tab < SkillCatalog.TabCount; tab++)
                {
                    Selected[tank, tab] = Mathf.Clamp(
                        PlayerPrefs.GetInt(SkillPreferenceKey(tank, tab), 0),
                        0,
                        SkillCatalog.OptionCount - 1);
                }
            }
        }

        /// <summary>Xe đang mở trong Garage; ghi lại ngay vào PlayerPrefs mỗi khi đổi.</summary>
        public static int LastTankIndex
        {
            get => s_LastTankIndex;
            set
            {
                s_LastTankIndex = Mathf.Clamp(value, 0, SkillCatalog.TankCount - 1);
                PlayerPrefs.SetInt(TankPreferenceKey, s_LastTankIndex);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Xe mà Picker sẽ hiển thị; -1 nghĩa là dùng LastTankIndex.</summary>
        public static int PendingTankIndex = -1;

        /// <summary>Tab mà Picker sẽ mở; -1 nghĩa là mở tab 1.</summary>
        public static int PendingTabIndex = -1;

        /// <summary>Lấy lựa chọn đang dùng của một tab kỹ năng.</summary>
        public static int Get(int tankIndex, int tabIndex)
        {
            int tank = Mathf.Clamp(tankIndex, 0, SkillCatalog.TankCount - 1);
            int tab = Mathf.Clamp(tabIndex, 0, SkillCatalog.TabCount - 1);
            return Mathf.Clamp(Selected[tank, tab], 0, SkillCatalog.OptionCount - 1);
        }

        /// <summary>Ghi lựa chọn mới cho một tab kỹ năng.</summary>
        public static void Set(int tankIndex, int tabIndex, int optionIndex)
        {
            int tank = Mathf.Clamp(tankIndex, 0, SkillCatalog.TankCount - 1);
            int tab = Mathf.Clamp(tabIndex, 0, SkillCatalog.TabCount - 1);
            Selected[tank, tab] = Mathf.Clamp(optionIndex, 0, SkillCatalog.OptionCount - 1);
            PlayerPrefs.SetInt(SkillPreferenceKey(tank, tab), Selected[tank, tab]);
            PlayerPrefs.Save();
        }

        /// <summary>Khoá PlayerPrefs của một ô kỹ năng tùy chọn.</summary>
        private static string SkillPreferenceKey(int tankIndex, int tabIndex)
        {
            return SkillPreferencePrefix + tankIndex + "." + tabIndex;
        }

        /// <summary>Đặt yêu cầu mở Picker cho một xe và một tab.</summary>
        public static void Request(int tankIndex, int tabIndex)
        {
            PendingTankIndex = Mathf.Clamp(tankIndex, 0, SkillCatalog.TankCount - 1);
            PendingTabIndex = Mathf.Clamp(tabIndex, 0, SkillCatalog.TabCount - 1);
            LastTankIndex = PendingTankIndex;
        }

        /// <summary>Đọc yêu cầu đang chờ và xoá đi để lần mở sau dùng mặc định.</summary>
        public static void ConsumeRequest(out int tankIndex, out int tabIndex)
        {
            tankIndex = PendingTankIndex >= 0 ? PendingTankIndex : Mathf.Clamp(LastTankIndex, 0, SkillCatalog.TankCount - 1);
            tabIndex = PendingTabIndex >= 0 ? PendingTabIndex : 0;
            PendingTankIndex = -1;
            PendingTabIndex = -1;
        }
    }
}
