using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using IronWasteland.Tanks;

namespace IronWasteland.Menu
{
    public enum AccessoryRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3,
    }

    /// <summary>
    /// Thông tin BỔ SUNG (chỉ để hiển thị) cho một phụ kiện: loại, độ hiếm, và các giá trị ghi đè tùy chọn.
    /// Tên / icon / mô tả / chỉ số mặc định được đọc thẳng từ EquipmentBrain trong EquipmentDatabase,
    /// nên KHÔNG cần nhập lại ở đây. Không có meta thì phụ kiện hiện với loại "PHỤ KIỆN", độ hiếm Thường.
    /// </summary>
    [Serializable]
    public class AccessoryMeta
    {
        [Tooltip("ID trong EquipmentDatabase (= vị trí trong danh sách accessoryPrefabs).")]
        public int equipmentId;

        [Tooltip("Vd: TẤN CÔNG / HỖ TRỢ / PHÒNG THỦ.")]
        public string typeLabel = "";

        public AccessoryRarity rarity = AccessoryRarity.Common;

        [Header("Ghi đè (để trống = lấy từ EquipmentBrain)")]
        public string displayNameOverride = "";
        public Sprite iconOverride;
        [TextArea(2, 5)] public string passiveOverride = "";
    }

    /// <summary>Phụ kiện đã được ghép đầy đủ thông tin để hiển thị (chỉ tồn tại lúc chạy).</summary>
    public sealed class AccessoryEntry
    {
        public int equipmentId;
        public string displayName;
        public string typeLabel;
        public AccessoryRarity rarity;
        public Sprite icon;
        public string[] bonusLines;
        public string passiveText;
    }

    /// <summary>
    /// Danh mục phụ kiện cho UI. ID phụ kiện = chỉ số trong EquipmentDatabase.AccessoryPrefabs
    /// (đúng quy ước của EquipmentDatabase), nên giá trị lưu trong hồ sơ chính là TankBrainContext.EquipmentId.
    /// </summary>
    [CreateAssetMenu(menuName = "IronWasteland/Menu/Accessory Catalog", fileName = "AccessoryCatalog")]
    public class AccessoryCatalog : ScriptableObject
    {
        [SerializeField] private EquipmentDatabase database;

        [Tooltip("Tùy chọn: loại / độ hiếm / ghi đè cho từng phụ kiện.")]
        [SerializeField] private List<AccessoryMeta> meta = new List<AccessoryMeta>();

        [Header("Phân rã (theo GDD: ngẫu nhiên 8-12 Ngọc ma pháp mỗi phụ kiện)")]
        [Min(0)] public int dismantleMinGems = 8;
        [Min(0)] public int dismantleMaxGems = 12;

        private List<AccessoryEntry> m_All;
        private Dictionary<int, AccessoryEntry> m_ById;

        public EquipmentDatabase Database => database;

        private void OnEnable() => Invalidate();
        private void OnValidate() => Invalidate();

        private void Invalidate()
        {
            m_All = null;
            m_ById = null;
        }

        /// <summary>Mọi phụ kiện có trong EquipmentDatabase, theo thứ tự ID.</summary>
        public IReadOnlyList<AccessoryEntry> All
        {
            get { Build(); return m_All; }
        }

        public AccessoryEntry Find(int equipmentId)
        {
            Build();
            return m_ById.TryGetValue(equipmentId, out AccessoryEntry e) ? e : null;
        }

        public int RollDismantleGems()
        {
            int lo = Mathf.Min(dismantleMinGems, dismantleMaxGems);
            int hi = Mathf.Max(dismantleMinGems, dismantleMaxGems);
            return UnityEngine.Random.Range(lo, hi + 1);
        }

        /// <summary>Tìm ID theo tên hiển thị (bỏ dấu, không phân biệt hoa thường). -1 nếu không thấy.</summary>
        public int FindIdByName(string displayName)
        {
            string key = NormalizeName(displayName);
            IReadOnlyList<AccessoryEntry> all = All;
            for (int i = 0; i < all.Count; i++)
                if (NormalizeName(all[i].displayName) == key) return all[i].equipmentId;
            return -1;
        }

        // ============================================================
        // GHÉP THÔNG TIN
        // ============================================================

        private AccessoryMeta FindMeta(int id)
        {
            for (int i = 0; i < meta.Count; i++)
                if (meta[i] != null && meta[i].equipmentId == id) return meta[i];
            return null;
        }

        private void Build()
        {
            if (m_All != null) return;

            m_All = new List<AccessoryEntry>();
            m_ById = new Dictionary<int, AccessoryEntry>();
            if (database == null) return;

            for (int id = 0; id < database.Count; id++)
            {
                EquipmentBrain prefab = database.GetPrefab(id);
                if (prefab == null) continue;

                AccessoryMeta m = FindMeta(id);

                var entry = new AccessoryEntry
                {
                    equipmentId = id,
                    displayName = !string.IsNullOrEmpty(m?.displayNameOverride) ? m.displayNameOverride
                                 : (!string.IsNullOrEmpty(prefab.DisplayName) ? prefab.DisplayName : prefab.name),
                    typeLabel = !string.IsNullOrEmpty(m?.typeLabel) ? m.typeLabel : "PHỤ KIỆN",
                    rarity = m != null ? m.rarity : AccessoryRarity.Common,
                    icon = m != null && m.iconOverride != null ? m.iconOverride : prefab.Icon,
                    bonusLines = BuildBonusLines(prefab.FlatBonus, prefab.PercentBonus),
                    passiveText = !string.IsNullOrEmpty(m?.passiveOverride) ? m.passiveOverride : prefab.Description,
                };

                m_All.Add(entry);
                m_ById[id] = entry;
            }
        }

        // ============================================================
        // DÒNG CHỈ SỐ TỰ SINH TỪ flatBonus / percentBonus
        // ============================================================

        private static string[] BuildBonusLines(TankStats flat, TankStats pct)
        {
            var lines = new List<string>();

            AddScaled(lines, "Tấn công", flat != null ? flat.Attack : 0f, pct != null ? pct.Attack : 0f);
            AddScaled(lines, "Giới hạn máu", flat != null ? flat.MaxHealth : 0f, pct != null ? pct.MaxHealth : 0f);
            AddScaled(lines, "Giới hạn năng lượng", flat != null ? flat.MaxEnergy : 0f, pct != null ? pct.MaxEnergy : 0f);
            AddScaled(lines, "Phòng thủ", flat != null ? flat.Defense : 0f, pct != null ? pct.Defense : 0f);
            AddScaled(lines, "Tốc độ di chuyển", flat != null ? flat.MoveSpeed : 0f, pct != null ? pct.MoveSpeed : 0f);
            AddRate(lines, "Tỉ lệ sát thương", flat != null ? flat.DamageRate : 0f, pct != null ? pct.DamageRate : 0f);
            AddRate(lines, "Tỉ lệ xuyên giáp", flat != null ? flat.ArmorPenetration : 0f, pct != null ? pct.ArmorPenetration : 0f);
            AddRate(lines, "Giảm hồi chiêu", flat != null ? flat.CooldownReduction : 0f, pct != null ? pct.CooldownReduction : 0f);

            return lines.ToArray();
        }

        // Chỉ số thường: cộng phẳng (+N) và cộng phần trăm theo chỉ số cơ bản (+P%).
        private static void AddScaled(List<string> lines, string label, float flat, float percent)
        {
            if (Mathf.Abs(percent) > 0.0001f) lines.Add(Signed(percent * 100f) + "% " + label);
            if (Mathf.Abs(flat) > 0.0001f) lines.Add(Signed(flat) + " " + label);
        }

        // Chỉ số tỉ lệ: cả flat lẫn percent đều cộng thẳng vào tỉ lệ (giống TankStats.CalculateFinalStats).
        private static void AddRate(List<string> lines, string label, float flat, float percent)
        {
            float v = flat + percent;
            if (Mathf.Abs(v) > 0.0001f) lines.Add(Signed(v * 100f) + "% " + label);
        }

        private static string Signed(float v)
        {
            string body = Mathf.Abs(v - Mathf.Round(v)) < 0.05f
                ? Mathf.RoundToInt(Mathf.Abs(v)).ToString()
                : Mathf.Abs(v).ToString("0.#", CultureInfo.InvariantCulture);
            return (v < 0f ? "-" : "+") + body;
        }

        /// <summary>Chuẩn hóa tên để so khớp: bỏ dấu, chỉ giữ chữ + số, viết thường.</summary>
        public static string NormalizeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";

            string decomposed = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                char lower = (c == 'đ' || c == 'Đ') ? 'd' : char.ToLowerInvariant(c);
                if (char.IsLetterOrDigit(lower)) sb.Append(lower);
            }
            return sb.ToString();
        }
    }
}
