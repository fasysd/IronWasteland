using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Mo ta 1 loai Tank: ten, mo ta, bang chi so theo Level, va prefab de khoi tao.
    /// Day la noi designer tinh ra chi so. TankBrain nhan ket qua co so.
    /// </summary>
    [CreateAssetMenu(menuName = "IronWasteland/Tank/Tank Definition", fileName = "TankDefinition")]
    public class TankDefinition : ScriptableObject
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 90;

        [Serializable]
        public struct StatsEntry
        {
            [Tooltip("Cap do co so. Chi chap nhan gia tri trong khoang 1..90.")]
            public int baseLevel;

            [Tooltip("Chi so tai cap do nay.")]
            public TankStats baseStats;
        }

        [Header("Info")]
        [SerializeField] private string tankName = "Tank";
        [TextArea(2, 4)]
        [SerializeField] private string description;

        [Header("Stats")]
        [SerializeField] private List<StatsEntry> statsTable = new List<StatsEntry>();

        [Header("Prefabs")]
        [Tooltip("Prefab chua TankBrain (khong co TankView).")]
        [SerializeField] private TankBrain tankPrefab;

        [Tooltip("Danh sach TankView prefab. Index trong mang la TankView ID.")]
        [SerializeField] private TankView[] viewPrefabs;

[Header("Equipment")]
        [Tooltip("Kho du lieu dung de tra cuu prefab trang bi theo ID.")]
        [SerializeField] private EquipmentDatabase equipmentDatabase;

        [Header("Context")]
        [Tooltip("Context khởi tạo cho TankBrain khi CreateTank.")]
        [SerializeField] private TankBrainContext context;

        public string TankName => tankName;
        public string Description => description;
        public List<StatsEntry> StatsTable => statsTable;
        public TankBrain TankPrefab => tankPrefab;
        public TankView[] ViewPrefabs => viewPrefabs;
        public int ViewCount => viewPrefabs != null ? viewPrefabs.Length : 0;
        public EquipmentDatabase EquipmentDatabase => equipmentDatabase;

        /// <summary>Lay TankView prefab theo ID (index trong mang).</summary>
        public TankView GetViewPrefab(int viewId)
        {
            if (viewPrefabs == null || viewId < 0 || viewId >= viewPrefabs.Length) return null;
            return viewPrefabs[viewId];
        }

        /// <summary>
        /// Tao Tank moi voi bo trang bi chi dinh (ghi de ID mac dinh tren asset).
        /// ID rong / khong ton tai -> Tank van tao duoc, chi khong co trang bi do.
        /// </summary>
        public TankBrain CreateTank(TankBrainContext context)
        {
            if (tankPrefab == null)
            {
                Debug.LogError($"[{nameof(TankDefinition)}] Chua gan TankPrefab.", this);
                return null;
            }

            TankView viewPrefab = GetViewPrefab(context.IdView);
            if (viewPrefab == null)
            {
                Debug.LogError($"[{nameof(TankDefinition)}] TankView ID {context.IdView} khong hop le.", this);
                return null;
            }

            TankBrain brain = Instantiate(tankPrefab, Vector3.zero, Quaternion.identity, null);
            brain.Initialize(context, GetStatsForLevel(context.Level));
            brain.SetViewPrefab(viewPrefab);

            EquipLoadout(brain, context.FirepowerEquipmentId, context.DefenseEquipmentId, context.MobilityEquipmentId);

            return brain;
        }

        /// <summary>
        /// Gan 3 trang bi theo ID. ID rong hoac khong tra cuo duoc -> bo qua
        /// (Tank van chay binh thuong, chi khong co trang bi do).
        /// </summary>
        private void EquipLoadout(TankBrain brain, string firepowerEquipmentId, string defenseEquipmentId, string mobilityEquipmentId)
        {
            if (equipmentDatabase == null)
            {
                if (HasAnyEquipmentId(firepowerEquipmentId, defenseEquipmentId, mobilityEquipmentId))
                {
                    Debug.LogWarning($"[{nameof(TankDefinition)}] Co ID trang bi nhung chua gan EquipmentDatabase.", this);
                }

                return;
            }

            brain.EquipEquipmentById(equipmentDatabase, EquipmentSlot.Firepower, firepowerEquipmentId);
            brain.EquipEquipmentById(equipmentDatabase, EquipmentSlot.Defense, defenseEquipmentId);
            brain.EquipEquipmentById(equipmentDatabase, EquipmentSlot.Mobility, mobilityEquipmentId);
        }

        private static bool HasAnyEquipmentId(params string[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                if (!string.IsNullOrEmpty(ids[i])) return true;
            }

            return false;
        }

        /// <summary>
        /// Chi so tai mot level. Chi dung cac muc hop le trong bang
        /// (muc vi pham se bi bo qua, xem <see cref="OnValidate"/>).
        /// </summary>
        public TankStats GetStatsForLevel(int level)
        {
            if (statsTable == null || statsTable.Count == 0)
            {
                Debug.LogWarning($"[{nameof(TankDefinition)}] Bang chi so rong.", this);
                return new TankStats();
            }

            level = Mathf.Clamp(level, MinLevel, MaxLevel);

            List<StatsEntry> valid = GetValidEntries(out _);
            if (valid.Count == 0)
            {
                Debug.LogWarning($"[{nameof(TankDefinition)}] Bang chi so khong co muc hop le nao.", this);
                return new TankStats();
            }

            StatsEntry? exact = null;
            StatsEntry? lower = null;
            StatsEntry? upper = null;

            for (int i = 0; i < valid.Count; i++)
            {
                StatsEntry entry = valid[i];

                if (entry.baseLevel == level) { exact = entry; break; }

                if (entry.baseLevel < level && (!lower.HasValue || entry.baseLevel > lower.Value.baseLevel))
                    lower = entry;

                if (entry.baseLevel > level && (!upper.HasValue || entry.baseLevel < upper.Value.baseLevel))
                    upper = entry;
            }

            if (exact.HasValue) return exact.Value.baseStats.Clone();
            if (!lower.HasValue) return upper.Value.baseStats.Clone();
            if (!upper.HasValue) return lower.Value.baseStats.Clone();

            return Interpolate(lower.Value, upper.Value, level);
        }

        /// <summary>noi suy: a + (b - a) / (hb - la) * (level - la)</summary>
        private static TankStats Interpolate(StatsEntry low, StatsEntry high, int level)
        {
            int span = high.baseLevel - low.baseLevel;
            if (span <= 0) return low.baseStats.Clone();

            float t = (float)(level - low.baseLevel) / span;
            TankStats a = low.baseStats;
            TankStats b = high.baseStats;
            TankStats r = a.Clone();

            r.Attack = Mathf.Lerp(a.Attack, b.Attack, t);
            r.MaxHealth = Mathf.Lerp(a.MaxHealth, b.MaxHealth, t);
            r.MaxEnergy = Mathf.Lerp(a.MaxEnergy, b.MaxEnergy, t);
            r.Defense = Mathf.Lerp(a.Defense, b.Defense, t);
            r.MoveSpeed = Mathf.Lerp(a.MoveSpeed, b.MoveSpeed, t);
            r.CooldownReduction = Mathf.Lerp(a.CooldownReduction, b.CooldownReduction, t);
            r.DamageMultiplier = Mathf.Lerp(a.DamageMultiplier, b.DamageMultiplier, t);
            r.ArmorPenetration = Mathf.Lerp(a.ArmorPenetration, b.ArmorPenetration, t);

            return r;
        }

        private void Awake() => WarnInvalidStatsEntries();

        /// <summary>
        /// KIEM TRA bang chi so (khong tu dong sua du lieu):
        /// muc vi pham se duoc bo qua khi tinh chi so va ghi log canh bao.
        /// </summary>
        private void OnValidate() => WarnInvalidStatsEntries();

        private void WarnInvalidStatsEntries()
        {
            foreach (string issue in GetStatsTableIssues())
            {
                Debug.LogWarning($"[{nameof(TankDefinition)}] Bang chi so co van de: {issue}", this);
            }
        }

        /// <summary>Mo ta loi cua tung muc vi pham trong bang (danh sach rong = khong loi).</summary>
        private List<string> GetStatsTableIssues()
        {
            List<string> issues = new List<string>();
            if (statsTable == null || statsTable.Count == 0) return issues;

            GetValidEntries(out HashSet<int> validIndices);

            int[] order = new int[statsTable.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int c = statsTable[a].baseLevel.CompareTo(statsTable[b].baseLevel);
                return c != 0 ? c : a.CompareTo(b);
            });

            StatsEntry? prevValid = null;

            foreach (int index in order)
            {
                StatsEntry entry = statsTable[index];

                if (validIndices.Contains(index)) { prevValid = entry; continue; }

                if (entry.baseLevel < MinLevel || entry.baseLevel > MaxLevel)
                    issues.Add($"baseLevel {entry.baseLevel} ngoai khoang [{MinLevel}, {MaxLevel}] (muc nay bi bo qua).");
                else if (entry.baseStats == null)
                    issues.Add($"baseLevel {entry.baseLevel} thieu baseStats (muc nay bi bo qua).");
                else if (prevValid.HasValue && prevValid.Value.baseLevel == entry.baseLevel)
                    issues.Add($"baseLevel {entry.baseLevel} trung nhau (muc o truoc duoc dung, muc nay bi bo qua).");
                else if (prevValid.HasValue && !IsStatsAtLeast(entry.baseStats, prevValid.Value.baseStats))
                    issues.Add($"baseLevel {entry.baseLevel} co chi so nho hon muc L{prevValid.Value.baseLevel} "
                        + $"({DescribeDiff(prevValid.Value.baseStats, entry.baseStats)}) - muc nay bi bo qua.");
                else
                    issues.Add($"baseLevel {entry.baseLevel} khong hop le (muc nay bi bo qua).");
            }

            return issues;
        }

        /// <summary>Lay cac muc hop le da sort tang dan. Muc vi pham bi bo qua (khong xoa).</summary>
        private List<StatsEntry> GetValidEntries(out HashSet<int> validIndices)
        {
            int count = statsTable != null ? statsTable.Count : 0;

            List<StatsEntry> result = new List<StatsEntry>(count);
            validIndices = new HashSet<int>();

            if (count == 0) return result;

            // Sort ON DINH theo baseLevel (key bang nhau thi giu thu tu goc).
            int[] order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;

            Array.Sort(order, (a, b) =>
            {
                int c = statsTable[a].baseLevel.CompareTo(statsTable[b].baseLevel);
                return c != 0 ? c : a.CompareTo(b);
            });

            for (int i = 0; i < count; i++)
            {
                int index = order[i];
                StatsEntry entry = statsTable[index];

                if (entry.baseLevel < MinLevel || entry.baseLevel > MaxLevel) continue;
                if (entry.baseStats == null) continue;

                // Trung baseLevel -> bo qua muc o sau
                if (result.Count > 0 && result[result.Count - 1].baseLevel == entry.baseLevel) continue;

                // Chi so phai tang dan theo cap
                if (result.Count > 0 && !IsStatsAtLeast(entry.baseStats, result[result.Count - 1].baseStats)) continue;

                result.Add(entry);
                validIndices.Add(index);
            }

            return result;
        }

        /// <summary>True neu <paramref name="candidate"/> co moi chi so >= <paramref name="reference"/>.</summary>
        private static bool IsStatsAtLeast(TankStats candidate, TankStats reference)
        {
            return candidate.Attack >= reference.Attack
                && candidate.MaxHealth >= reference.MaxHealth
                && candidate.MaxEnergy >= reference.MaxEnergy
                && candidate.Defense >= reference.Defense
                && candidate.MoveSpeed >= reference.MoveSpeed
                && candidate.CooldownReduction >= reference.CooldownReduction
                && candidate.DamageMultiplier >= reference.DamageMultiplier
                && candidate.ArmorPenetration >= reference.ArmorPenetration;
        }

        /// <summary>Mo ta ngan gon cac chi so bi giam giua 2 muc.</summary>
        private static string DescribeDiff(TankStats higher, TankStats lower)
        {
            List<string> parts = new List<string>();
            if (lower.Attack < higher.Attack) parts.Add($"Attack {higher.Attack:0.##}>{lower.Attack:0.##}");
            if (lower.MaxHealth < higher.MaxHealth) parts.Add($"MaxHealth {higher.MaxHealth:0.##}>{lower.MaxHealth:0.##}");
            if (lower.MaxEnergy < higher.MaxEnergy) parts.Add($"MaxEnergy {higher.MaxEnergy:0.##}>{lower.MaxEnergy:0.##}");
            if (lower.Defense < higher.Defense) parts.Add($"Defense {higher.Defense:0.##}>{lower.Defense:0.##}");
            if (lower.MoveSpeed < higher.MoveSpeed) parts.Add($"MoveSpeed {higher.MoveSpeed:0.##}>{lower.MoveSpeed:0.##}");
            if (lower.CooldownReduction < higher.CooldownReduction) parts.Add($"CooldownReduction {higher.CooldownReduction:0.###}>{lower.CooldownReduction:0.###}");
            if (lower.DamageMultiplier < higher.DamageMultiplier) parts.Add($"DamageMultiplier {higher.DamageMultiplier:0.##}>{lower.DamageMultiplier:0.##}");
            if (lower.ArmorPenetration < higher.ArmorPenetration) parts.Add($"ArmorPenetration {higher.ArmorPenetration:0.###}>{lower.ArmorPenetration:0.###}");
            return string.Join(", ", parts);
        }
    }
}
