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

        /// <summary>Giá trị đánh dấu xe đã tháo hết phụ kiện.</summary>
        public const int None = -1;

        /// <summary>Lấy chỉ số phụ kiện đã lưu của xe; chưa trang bị (hoặc đã tháo) trả về None.</summary>
        public static int Get(int tankIndex)
        {
            int tank = Mathf.Clamp(tankIndex, 0, SkillCatalog.TankCount - 1);
            return Mathf.Max(None, PlayerPrefs.GetInt(KeyPrefix + tank, None));
        }

        /// <summary>Xe có đang trang bị phụ kiện nào không.</summary>
        public static bool Has(int tankIndex)
        {
            return Get(tankIndex) != None;
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

        /// <summary>Đánh dấu xe đã tháo phụ kiện; giữ khoá để phân biệt với xe chưa từng chọn.</summary>
        public static void Clear(int tankIndex)
        {
            int tank = Mathf.Clamp(tankIndex, 0, SkillCatalog.TankCount - 1);
            PlayerPrefs.SetInt(KeyPrefix + tank, None);
            PlayerPrefs.Save();
        }
    }
}
