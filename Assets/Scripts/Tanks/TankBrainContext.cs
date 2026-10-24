using System;

namespace IronWasteland.Tanks
{
    [Serializable]
    public struct TankBrainContext
    {
        public int Level;
        public int IdView;
        public int FirepowerEquipmentId;
        public int DefenseEquipmentId;
        public int MobilityEquipmentId;
        public int OptionSkill1;
        public int OptionSkill2;
    }
}
