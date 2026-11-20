using UnityEngine;

namespace IronWasteland.UI
{
    /// <summary>
    /// Lưu phụ kiện đang trang bị của từng xe vào PlayerPrefs.
    /// Mỗi xe chỉ có một ô phụ kiện nên chỉ cần một chỉ số duy nhất.
    /// </summary>
    public static class AccessoryLoadout
    {
        private const string KeyPrefix = "iw.loadout.accessory.";

        /// <summary>Lấy chỉ số phụ kiện đã lưu của xe; mặc định là 0.</summary>
        public static int Get(int tankIndex)
        {
            int tank = Mathf.Clamp(tankIndex, 0, SkillCatalog.TankCount - 1);
            return Mathf.Max(0, PlayerPrefs.GetInt(KeyPrefix + tank, 0));
        }

        /// <summary>Lưu phụ kiện cho xe. Chỉ số âm bị bỏ qua.</summary>
        public static void Set(int tankIndex, int itemIndex)
        {
            if (itemIndex < 0)
            {
                return;
            }

            int tank = Mathf.Clamp(tankIndex, 0, SkillCatalog.TankCount - 1);
            PlayerPrefs.SetInt(KeyPrefix + tank, itemIndex);
            PlayerPrefs.Save();
        }

        /// <summary>Xoá lựa chọn đã lưu của một xe.</summary>
        public static void Clear(int tankIndex)
        {
            int tank = Mathf.Clamp(tankIndex, 0, SkillCatalog.TankCount - 1);
            PlayerPrefs.DeleteKey(KeyPrefix + tank);
            PlayerPrefs.Save();
        }
    }
}
