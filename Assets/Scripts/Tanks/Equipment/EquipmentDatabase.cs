using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Kho du lieu trang bi: moi trang bi duoc gan 1 <c>id</c> de tra cuu
    /// ma khong can biet ten lop (FirepowerCore / DefenseCore / MobilityCore).
    ///
    /// ID duoc tach theo loai: 1 ID chi ton tai trong dung loai trang bi do.
    /// Nho do <c>defense_heavy</c> khong bao gio tra nham sang mot FirepowerCore,
    /// va nguoc lai TankDefinition chi can mot ID cho moi loai.
    ///
    /// Tap trang bi mac dinh (TankDefinition): trong ID.
    /// </summary>
    [CreateAssetMenu(menuName = "IronWasteland/Tank/Equipment Database", fileName = "EquipmentDatabase")]
    public class EquipmentDatabase : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("ID tra cuu trong TankDefinition. Phai duy nhat trong loai trang bi nay.")]
            public string id;

            [Tooltip("Prefab EquipmentBrain tuong ung.")]
            public EquipmentBrain prefab;
        }

        [Header("Firepower (Nong Phao)")]
        [SerializeField] private List<Entry> firepowerPrefabs = new List<Entry>();

        [Header("Defense (Than Xe)")]
        [SerializeField] private List<Entry> defensePrefabs = new List<Entry>();

        [Header("Mobility (Bo Banh)")]
        [SerializeField] private List<Entry> mobilityPrefabs = new List<Entry>();

        public List<Entry> FirepowerPrefabs => firepowerPrefabs;
        public List<Entry> DefensePrefabs => defensePrefabs;
        public List<Entry> MobilityPrefabs => mobilityPrefabs;

        /// <summary>Tong so trang bi dang co trong database.</summary>
        public int Count => CountOf(EquipmentSlot.Firepower) + CountOf(EquipmentSlot.Defense) + CountOf(EquipmentSlot.Mobility);

        /// <summary>Danh sach cua 1 loai trang bi. None -> rong.</summary>
        private List<Entry> GetList(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Firepower: return firepowerPrefabs;
                case EquipmentSlot.Defense: return defensePrefabs;
                case EquipmentSlot.Mobility: return mobilityPrefabs;
                default: return null;
            }
        }

        private int CountOf(EquipmentSlot slot) => GetList(slot)?.Count ?? 0;

        /// <summary>
        /// Prefab trang bi theo ID. Tra null neu ID rong / khong ton tai.
        /// Can dung ID rong thi tra null (khong phai loi) de Tank bo trang bi.
        /// </summary>
        public EquipmentBrain GetPrefab(EquipmentSlot slot, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            List<Entry> list = GetList(slot);
            if (list == null) return null;

            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].id, id, StringComparison.Ordinal)) return list[i].prefab;
            }

            return null;
        }

        public EquipmentBrain GetFirepower(string id) => GetPrefab(EquipmentSlot.Firepower, id);
        public EquipmentBrain GetDefense(string id) => GetPrefab(EquipmentSlot.Defense, id);
        public EquipmentBrain GetMobility(string id) => GetPrefab(EquipmentSlot.Mobility, id);

        /// <summary>True neu ID co trong database (co prefab hop le).</summary>
        public bool Has(EquipmentSlot slot, string id) => GetPrefab(slot, id) != null;

        private void Awake() => WarnInvalidEntries();

        /// <summary>
        /// KIEM TRA du lieu (khong tu dong sua): muc loi se bi bo qua khi tra cuu
        /// va ghi log canh bao - cung phong cach voi TankDefinition.
        /// </summary>
        private void OnValidate() => WarnInvalidEntries();

        private void WarnInvalidEntries()
        {
            foreach (string issue in GetIssues(EquipmentSlot.Firepower, firepowerPrefabs)) Warn(issue);
            foreach (string issue in GetIssues(EquipmentSlot.Defense, defensePrefabs)) Warn(issue);
            foreach (string issue in GetIssues(EquipmentSlot.Mobility, mobilityPrefabs)) Warn(issue);
        }

        private void Warn(string issue) => Debug.LogWarning($"[{nameof(EquipmentDatabase)}] {issue}", this);

        /// <summary>Moi tao trong 1 loai trang bi (rong = khong loi).</summary>
        private static List<string> GetIssues(EquipmentSlot slot, List<Entry> list)
        {
            List<string> issues = new List<string>();
            if (list == null) return issues;

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < list.Count; i++)
            {
                Entry entry = list[i];

                if (string.IsNullOrEmpty(entry.id))
                    issues.Add($"{slot}[{i}] thieu id.");

                if (entry.prefab == null)
                    issues.Add($"{slot} '{entry.id}' chua gan prefab.");

                // Prefab phai khop loai: tranh designer nham FirepowerCore vao danh sach Defense.
                if (entry.prefab != null && entry.prefab.Slot != slot)
                    issues.Add($"{slot} '{entry.id}' dang tro toi prefab "
                        + $"'{entry.prefab.name}' thuoc slot {entry.prefab.Slot} (phai la {slot}).");

                if (string.IsNullOrEmpty(entry.id)) continue;

                if (!seen.Add(entry.id))
                    issues.Add($"{slot} '{entry.id}' trung ID (muc truoc duoc dung).");
            }

            return issues;
        }
    }
}