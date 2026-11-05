using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Kho du lieu Phu kien (Accessory / Equipment Database).
    /// Theo GDD, moi Tank co 1 o de mang Phu kien.
    /// ID cua phu kien chinh la index trong danh sach accessoryPrefabs.
    /// </summary>
    [CreateAssetMenu(
        menuName = "IronWasteland/Tank/Equipment Database",
        fileName = "EquipmentDatabase")]
    public class EquipmentDatabase : ScriptableObject
    {
        [Header("Accessories")]
        [Tooltip("Danh sach prefab cac Phu kien trong game (20 loai theo GDD).")]
        [SerializeField] private List<EquipmentBrain> accessoryPrefabs = new List<EquipmentBrain>();

        public List<EquipmentBrain> AccessoryPrefabs => accessoryPrefabs;

        /// <summary>Tong so phu kien trong database.</summary>
        public int Count => accessoryPrefabs != null ? accessoryPrefabs.Count : 0;

        /// <summary>
        /// Lay prefab phu kien theo ID (index trong danh sach).
        /// Tra ve null neu ID khong hop le.
        /// </summary>
        public EquipmentBrain GetPrefab(int id)
        {
            if (accessoryPrefabs == null || id < 0 || id >= accessoryPrefabs.Count)
                return null;

            return accessoryPrefabs[id];
        }

        public bool Has(int id) => GetPrefab(id) != null;
    }
}
