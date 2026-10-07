using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Kho du lieu trang bi: moi trang bi duoc gan 1 <c>id</c> de tra cuu
    /// ma khong can biet ten lop (WeaponBrain / HullBrain / TrackBrain).
    ///
    /// ID duoc tach theo loai: 1 ID chi ton tai trong dung loai trang bi do.
    /// Nho do <c>hull_heavy</c> khong bao gio tra nham sang mot WeaponBrain,
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

        [Header("Weapon (Nong Phao)")]
        [SerializeField] private List<Entry> weaponPrefabs = new List<Entry>();

        [Header("Hull (Than Xe)")]
        [SerializeField] private List<Entry> hullPrefabs = new List<Entry>();

        [Header("Track (Bo Banh)")]
        [SerializeField] private List<Entry> trackPrefabs = new List<Entry>();

        public List<Entry> WeaponPrefabs => weaponPrefabs;
        public List<Entry> HullPrefabs => hullPrefabs;
        public List<Entry> TrackPrefabs => trackPrefabs;

        /// <summary>Tong so trang bi dang co trong database.</summary>
        public int Count => CountOf(EquipmentSlot.Weapon) + CountOf(EquipmentSlot.Hull) + CountOf(EquipmentSlot.Track);

        /// <summary>Danh sach cua 1 loai trang bi. None -> rong.</summary>
        private List<Entry> GetList(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon: return weaponPrefabs;
                case EquipmentSlot.Hull: return hullPrefabs;
                case EquipmentSlot.Track: return trackPrefabs;
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

        public EquipmentBrain GetWeapon(string id) => GetPrefab(EquipmentSlot.Weapon, id);
        public EquipmentBrain GetHull(string id) => GetPrefab(EquipmentSlot.Hull, id);
        public EquipmentBrain GetTrack(string id) => GetPrefab(EquipmentSlot.Track, id);

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
            foreach (string issue in GetIssues(EquipmentSlot.Weapon, weaponPrefabs)) Warn(issue);
            foreach (string issue in GetIssues(EquipmentSlot.Hull, hullPrefabs)) Warn(issue);
            foreach (string issue in GetIssues(EquipmentSlot.Track, trackPrefabs)) Warn(issue);
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

                // Prefab phai khop loai: tranh designer nham WeaponBrain vao danh sach Hull.
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