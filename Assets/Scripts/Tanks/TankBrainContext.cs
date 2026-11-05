using System;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Context khoi tao cho TankBrain khi CreateTank tu TankDefinition.
    /// </summary>
    [Serializable]
    public struct TankBrainContext
    {
        public int Level;
        public int IdView;

        /// <summary>ID phu kien trong EquipmentDatabase (-1 = khong trang bi).</summary>
        public int EquipmentId;

        public int OptionSkill1;
        public int OptionSkill2;

        public TankBrainContext(int level, int idView, int equipmentId = -1, int optionSkill1 = 0, int optionSkill2 = 0)
        {
            Level = level;
            IdView = idView;
            EquipmentId = equipmentId;
            OptionSkill1 = optionSkill1;
            OptionSkill2 = optionSkill2;
        }
    }
}
