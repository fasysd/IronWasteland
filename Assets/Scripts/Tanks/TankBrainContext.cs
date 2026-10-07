using System;

namespace IronWasteland.Tanks
{
    [Serializable]
    public struct TankBrainContext
    {
        public int Level;
        public int IdView;
        public string FirepowerEquipmentId;
        public string DefenseEquipmentId;
        public string MobilityEquipmentId;
        public int OptionSkill1;
        public int OptionSkill2;
    }
}
