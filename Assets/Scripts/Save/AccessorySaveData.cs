using System;

namespace IronWasteland.Save
{
    /// <summary>
    /// Thông tin phụ kiện đã sở hữu.
    /// AccessoryIndex: index trong AccessoryCatalog / EquipmentDatabase (0..19).
    /// Quantity: số lượng sở hữu (>= 1).
    /// </summary>
    [Serializable]
    public class AccessorySaveData
    {
        public int AccessoryIndex;
        public int Quantity = 1;
    }
}
