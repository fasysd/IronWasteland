using System;
using System.Collections.Generic;

namespace IronWasteland.Menu
{
    /// <summary>Trạng thái lưu của một xe tăng.</summary>
    [Serializable]
    public class TankSave
    {
        public string id;
        public int level = 1;

        /// <summary>Lựa chọn Tùy chọn 1 (bắt đầu từ 1) -> TankBrainContext.OptionSkill1.</summary>
        public int optionSkill1 = 1;

        /// <summary>Lựa chọn Tùy chọn 2 (bắt đầu từ 1) -> TankBrainContext.OptionSkill2.</summary>
        public int optionSkill2 = 1;

        /// <summary>ID phụ kiện đang gắn (-1 = trống) -> TankBrainContext.EquipmentId.</summary>
        public int equipmentId = -1;
    }

    /// <summary>Số lượng một loại phụ kiện người chơi đang sở hữu.</summary>
    [Serializable]
    public class AccessoryStackSave
    {
        public int id;
        public int count;
    }

    [Serializable]
    public class PlayerProfile
    {
        public int version = 1;

        /// <summary>Ngọc ma pháp.</summary>
        public int gems;

        public string selectedTankId = "";
        public string selectedStageId = "";
        public bool coopMode;

        public List<TankSave> tanks = new List<TankSave>();
        public List<AccessoryStackSave> accessories = new List<AccessoryStackSave>();
        public List<string> clearedStageIds = new List<string>();
    }

    /// <summary>Giá trị khởi tạo cho hồ sơ MỚI (chỉ dùng lần đầu, khi chưa có file lưu).</summary>
    [Serializable]
    public class ProfileDefaults
    {
        public int startingGems = 125;

        [UnityEngine.Tooltip("Phụ kiện tặng sẵn cho hồ sơ mới (ID trong EquipmentDatabase). Dùng để thử nghiệm.")]
        public int[] starterAccessoryIds = new int[0];
    }

    public struct StageResult
    {
        public int Gems;

        /// <summary>ID các phụ kiện rơi ra (rỗng nếu không rơi).</summary>
        public int[] AccessoryIds;

        /// <summary>Id các xe được mở khóa nhờ lần vượt ải đầu tiên này (rỗng nếu không có).</summary>
        public string[] UnlockedTankIds;
    }

    public enum UpgradeResult
    {
        Ok,
        MaxLevel,
        NotEnoughGems,
    }
}
