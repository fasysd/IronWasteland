using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Kho du lieu trang bi.
    ///
    /// Moi trang bi duoc dinh danh bang int, trong do ID chinh la index
    /// cua trang bi trong danh sach tuong ung voi EquipmentSlot.
    ///
    /// Vi du:
    /// Firepower ID = 0 -> firepowerPrefabs[0]
    /// Defense ID    = 2 -> defensePrefabs[2]
    /// Mobility ID   = 1 -> mobilityPrefabs[1]
    /// </summary>
    [CreateAssetMenu(
        menuName = "IronWasteland/Tank/Equipment Database",
        fileName = "EquipmentDatabase")]
    public class EquipmentDatabase : ScriptableObject
    {
        [Header("Firepower")]
        [SerializeField] private List<FirepowerCore> firepowerPrefabs = new List<FirepowerCore>();

        [Header("Defense")]
        [SerializeField] private List<DefenseCore> defensePrefabs = new List<DefenseCore>();

        [Header("Mobility")]
        [SerializeField] private List<MobilityCore> mobilityPrefabs = new List<MobilityCore>();

        public List<FirepowerCore> FirepowerPrefabs => firepowerPrefabs;
        public List<DefenseCore> DefensePrefabs => defensePrefabs;
        public List<MobilityCore> MobilityPrefabs => mobilityPrefabs;

        /// <summary>
        /// Tong so trang bi dang co trong database.
        /// </summary>
        public int Count =>
            firepowerPrefabs.Count +
            defensePrefabs.Count +
            mobilityPrefabs.Count;

        /// <summary>
        /// Lay trang bi theo slot va ID.
        /// ID chinh la index trong list cua slot tuong ung.
        ///
        /// Tra null neu ID khong hop le.
        /// </summary>
        public EquipmentBrain GetPrefab(EquipmentSlot slot, int id)
        {
            if (id < 0) return null;

            switch (slot)
            {
                case EquipmentSlot.Firepower:
                    return GetAt(firepowerPrefabs, id);

                case EquipmentSlot.Defense:
                    return GetAt(defensePrefabs, id);

                case EquipmentSlot.Mobility:
                    return GetAt(mobilityPrefabs, id);

                default:
                    return null;
            }
        }

        public FirepowerCore GetFirepower(int id) =>
            GetAt(firepowerPrefabs, id);

        public DefenseCore GetDefense(int id) =>
            GetAt(defensePrefabs, id);

        public MobilityCore GetMobility(int id) =>
            GetAt(mobilityPrefabs, id);

        /// <summary>
        /// True neu ID nam trong danh sach va prefab tai vi tri do khong null.
        /// </summary>
        public bool Has(EquipmentSlot slot, int id) =>
            GetPrefab(slot, id) != null;

        private static T GetAt<T>(List<T> list, int index)
            where T : EquipmentBrain
        {
            if (list == null || index < 0 || index >= list.Count)
                return null;

            return list[index];
        }

        private void Awake()
        {
            WarnInvalidEntries();
        }

        private void OnValidate()
        {
            WarnInvalidEntries();
        }

        /// <summary>
        /// Kiem tra du lieu trong database.
        /// Khong tu dong sua du lieu.
        /// </summary>
        private void WarnInvalidEntries()
        {
            WarnInvalidList(EquipmentSlot.Firepower, firepowerPrefabs);
            WarnInvalidList(EquipmentSlot.Defense, defensePrefabs);
            WarnInvalidList(EquipmentSlot.Mobility, mobilityPrefabs);
        }

        private void WarnInvalidList<T>(
            EquipmentSlot slot,
            List<T> list)
            where T : EquipmentBrain
        {
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                T prefab = list[i];

                if (prefab == null)
                {
                    Warn($"{slot}[{i}] chua gan prefab.");
                    continue;
                }

                if (prefab.Slot != slot)
                {
                    Warn(
                        $"{slot}[{i}] dang tro toi prefab '{prefab.name}' "
                        + $"thuoc slot {prefab.Slot} (phai la {slot}).");
                }
            }
        }

        private void Warn(string issue)
        {
            Debug.LogWarning(
                $"[{nameof(EquipmentDatabase)}] {issue}",
                this);
        }
    }
}