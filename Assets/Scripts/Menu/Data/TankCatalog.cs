using System;
using System.Collections.Generic;
using UnityEngine;
using IronWasteland.Tanks;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Thông tin TRÌNH BÀY (UI) của một kỹ năng: tên, icon, mô tả...
    /// Không chứa logic skill — logic nằm ở TankBrain_* của từng xe.
    /// </summary>
    [Serializable]
    public class SkillUIInfo
    {
        public string displayName = "";
        public Sprite icon;

        [Tooltip("Dòng phụ dưới tên, vd: SÁT THƯƠNG / HỖ TRỢ.")]
        public string subtitle = "";

        [Tooltip("Nhãn hiện ở ô kỹ năng trong Nhà xe, vd: KHÔNG TỐN NĂNG LƯỢNG.")]
        public string costLabel = "";

        [Tooltip("Năng lượng tiêu hao, hiện trong bảng chọn kỹ năng. Vd: 45")]
        public string energyText = "-";

        [Tooltip("Hồi chiêu, hiện trong bảng chọn kỹ năng. Vd: 11s")]
        public string cooldownText = "-";

        [TextArea(2, 4)] public string description = "";

        public bool IsValid => !string.IsNullOrEmpty(displayName);
    }

    /// <summary>
    /// Một xe tăng trong Nhà xe / Main Menu / Chọn ải.
    /// Chỉ giữ dữ liệu trình bày + tham chiếu tới <see cref="TankDefinition"/> (nguồn chỉ số thật).
    /// </summary>
    [Serializable]
    public class TankEntry
    {
        [Tooltip("Id duy nhất, dùng làm khóa lưu file (không đổi sau khi đã phát hành).")]
        public string id = "tank_id";

        public string displayName = "TANK";

        [Tooltip("Dòng vai trò ở danh sách, vd: TANKER / HỖ TRỢ")]
        public string roleLine = "";

        [Tooltip("Dòng mô tả ngắn dưới tên, vd: SÁT THỦ CƠ ĐỘNG")]
        public string subtitle = "";

        [Tooltip("Thẻ nhỏ ở Main Menu, vd: DPS, SUPPORT, BÓNG TỐI (tối đa 3).")]
        public string[] tags = new string[0];

        public Sprite listIcon;
        public Sprite portrait;

        [Tooltip("Nguồn chỉ số + prefab của xe. Dùng để spawn xe khi vào trận.")]
        public TankDefinition definition;

        [Tooltip("Prefab TankView dùng để HIỂN THỊ xe trong Menu (Leviathan_View...). Để trống = tự tìm trong TankDefinition theo viewId.")]
        public TankView viewPrefab;

        [Tooltip("Index trong danh sách TankView của TankDefinition (skin).")]
        public int viewId = 0;

        [Tooltip("Id ải cần hoàn thành để mở khóa xe này (để trống = mở sẵn). Vd: 2-5")]
        public string unlockStageId = "";

        [Header("Kỹ năng (chỉ để hiển thị)")]
        public SkillUIInfo normalSkill = new SkillUIInfo();
        public SkillUIInfo specialSkill = new SkillUIInfo();

        [Tooltip("Các lựa chọn của Tùy chọn 1 (thứ tự = OptionSkill1: phần tử 0 ứng với giá trị 1).")]
        public SkillUIInfo[] option1 = { new SkillUIInfo(), new SkillUIInfo(), new SkillUIInfo() };

        [Tooltip("Các lựa chọn của Tùy chọn 2 (thứ tự = OptionSkill2: phần tử 0 ứng với giá trị 1).")]
        public SkillUIInfo[] option2 = { new SkillUIInfo(), new SkillUIInfo(), new SkillUIInfo() };

        /// <summary>optionTab: 0 = Tùy chọn 1, 1 = Tùy chọn 2.</summary>
        public SkillUIInfo[] GetOptions(int optionTab) => optionTab == 0 ? option1 : option2;

        /// <summary>variant là giá trị lưu trong TankBrainContext (bắt đầu từ 1).</summary>
        public SkillUIInfo GetOption(int optionTab, int variant)
        {
            SkillUIInfo[] arr = GetOptions(optionTab);
            if (arr == null || arr.Length == 0) return new SkillUIInfo();
            int i = Mathf.Clamp(variant - 1, 0, arr.Length - 1);
            return arr[i] ?? new SkillUIInfo();
        }
    }

    [CreateAssetMenu(menuName = "IronWasteland/Menu/Tank Catalog", fileName = "TankCatalog")]
    public class TankCatalog : ScriptableObject
    {
        public List<TankEntry> tanks = new List<TankEntry>();

        public TankEntry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < tanks.Count; i++)
                if (tanks[i] != null && tanks[i].id == id) return tanks[i];
            return null;
        }
    }
}
