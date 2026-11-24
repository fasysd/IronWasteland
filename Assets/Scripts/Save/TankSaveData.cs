using System;

namespace IronWasteland.Save
{
    /// <summary>
    /// Thông tin lưu trữ của một Tank.
    /// Level = 0: chưa sở hữu. Level >= 1: đã sở hữu (Level 1..90).
    /// EquippedAccessoryIndex: index phụ kiện trong AccessoryCatalog (0..19), -1 là chưa trang bị.
    /// OptionSkill1, OptionSkill2: nhánh kỹ năng tùy chọn được chọn (0..2).
    /// </summary>
    [Serializable]
    public class TankSaveData
    {
        public string TankId;
        public int Level;
        public int EquippedAccessoryIndex = -1;
        public int OptionSkill1 = 0;
        public int OptionSkill2 = 0;
    }
}
