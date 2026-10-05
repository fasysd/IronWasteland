using UnityEngine;

namespace IronWasteland.Tanks
{
    [System.Serializable]
    public struct StatsEntry
    {
        [Tooltip("Cap do co so. Chi chap nhan gia tri trong khoang 1..90.")]
        public int baseLevel;

        [Tooltip("Chi so tai cap do nay.")]
        public TankStats baseStats;
    }

    /// <summary>
    /// Lop dieu khien Tank. Day la noi chua "logic game": chi so, di chuyen, dung 4 ky nang.
    /// No KHONG cham vao truc tiep sprite/animator, ma chi ra lenh cho TankView.
    /// </summary>
    [DisallowMultipleComponent]
    public class TankBrain : MonoBehaviour
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 90;
        public const int SkillCount = 4;

        [Header("View")]
        [Tooltip("View dang chay. Se duoc gan luc runtime tu TankBrainFactory.")]
        [SerializeField] private TankView view;

        [Tooltip("Prefab TankView de spawn. Neu rong thi Tank khong co hinh anh.")]
        [SerializeField] private TankView viewPrefab;

        [Header("Level")]
        [SerializeField, Range(MinLevel, MaxLevel)] private int level = MinLevel;

        [Header("Stats")]
        [Tooltip("Bang chi so co so. Level duoc noi suy tu 2 cap ke nhau trong bang nay.")]
        [SerializeField] private System.Collections.Generic.List<StatsEntry> statsTable = new System.Collections.Generic.List<StatsEntry>
        {
            new StatsEntry { baseLevel = 1, baseStats = new TankStats() },
            new StatsEntry { baseLevel = 30, baseStats = new TankStats() },
            new StatsEntry { baseLevel = 60, baseStats = new TankStats() },
            new StatsEntry { baseLevel = 90, baseStats = new TankStats() },
        };

        [Header("Runtime")]
        [SerializeField] private float currentHealth;
        [SerializeField] private float currentEnergy;
        [SerializeField] private float[] skillCooldowns = new float[SkillCount];

        private Rigidbody2D m_Body;
        private TankStats m_Stats = new TankStats();
        private Vector2 m_MoveInput;
        private Vector2 m_LookTarget;
        private bool m_HasLookTarget;

        public TankView View => view;
        public TankView ViewPrefab => viewPrefab;
        public int Level => level;
        public TankStats Stats => m_Stats;
        public System.Collections.Generic.List<StatsEntry> StatsTable => statsTable;
        public float CurrentHealth => currentHealth;
        public float CurrentEnergy => currentEnergy;
        public float GetSkillCooldown(int index) => skillCooldowns[Mathf.Clamp(index, 0, SkillCount - 1)];

        #region Setup

        /// <summary>Do Factory gan Level. Chi so duoc tinh lai tu baseStats + growth.</summary>
        public void Initialize(int level)
        {
            SetLevel(level);
            RefreshStats();
        }

        public void SetLevel(int level)
        {
            this.level = Mathf.Clamp(level, MinLevel, MaxLevel);
        }

        /// <summary>Tinh lai chi so theo Level hien tai dua tren bang chi so co so.</summary>
        public void RefreshStats()
        {
            m_Stats = EvaluateStats(level);

            currentHealth = Mathf.Clamp(currentHealth <= 0f ? m_Stats.MaxHealth : currentHealth, 0f, m_Stats.MaxHealth);
            currentEnergy = Mathf.Clamp(currentEnergy <= 0f ? m_Stats.MaxEnergy : currentEnergy, 0f, m_Stats.MaxEnergy);
        }

        /// <summary>
        /// Lay chi so tai mot level. Chi dung cac muc hop le trong bang
        /// (muc vi pham se bi bo qua, xem <see cref="ValidateStatsTable"/>).
        /// - Co cap trung khop: dung nguyen chi so do.
        /// - Nguoc giua 2 cap: noi suy tuyen tinh giua 2 cap ke nhau.
        /// - Ngoai khoang: dung chi so cua cap gan nhat.
        /// </summary>
        public TankStats EvaluateStats(int level)
        {
            if (statsTable == null || statsTable.Count == 0) return new TankStats();

            level = Mathf.Clamp(level, MinLevel, MaxLevel);

            System.Collections.Generic.List<StatsEntry> valid = GetValidEntries(out _);
            if (valid.Count == 0)
            {
                Debug.LogWarning($"[{nameof(TankBrain)}] Bang chi so khong co muc hop le nao.", this);
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

        /// <summary>
        /// noi suy: stats = (statsCao - statsThap) / (capCao - capThap) + statsThap
        /// </summary>
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

        /// <summary>Nhan TankView prefab, tao GameObject con va dung lam View cua Tank nay.</summary>
        public void SetViewPrefab(TankView prefab)
        {
            viewPrefab = prefab;
            SpawnView();
        }

        private void SpawnView()
        {
            if (viewPrefab == null) return;

            if (view != null) Destroy(view.gameObject);

            view = Instantiate(viewPrefab, transform);
            view.name = viewPrefab.name;

            RegisterInto(view);
        }

        /// <summary>TankBrain dang ky lam Owner cua View (va cac collider ben trong).</summary>
        public void RegisterInto(TankView target)
        {
            target?.RegisterOwner(this);
        }

        private void Awake()
        {
            if (view == null) view = GetComponentInChildren<TankView>();

            // View co the da ton tai san trong hierarchy (khong qua factory).
            RegisterInto(view);

            // Rigidbody2D nam tren chinh TankBrain. Fallback sang child cho
            // trong hop prefab cu / de an toan.
            m_Body = GetComponent<Rigidbody2D>();
            if (m_Body == null) m_Body = GetComponentInChildren<Rigidbody2D>();

            if (m_Stats == null) m_Stats = new TankStats();
            if (currentHealth <= 0f) currentHealth = m_Stats.MaxHealth;
            if (currentEnergy <= 0f) currentEnergy = m_Stats.MaxEnergy;

            // OnValidate khong chay trong build -> canh bao them o runtime.
            WarnInvalidStatsEntries();
        }

        private void WarnInvalidStatsEntries()
        {
            foreach (string issue in GetStatsTableIssues())
            {
                Debug.LogWarning($"[{nameof(TankBrain)}] Bang chi so co van de: {issue}", this);
            }
        }

        /// <summary>
        /// KIEM TRA bang chi so trong Inspector (khong tu dong sua du lieu):
        /// - baseLevel phai nam trong [MinLevel, MaxLevel].
        /// - baseStats phai tang dan theo cap.
        /// - baseLevel khong duoc trung nhau.
        /// Muc vi pham se duoc bo qua khi tinh chi so va ghi log canh bao.
        /// </summary>
        private void OnValidate()
        {
            ValidateStatsTable();
        }

        private void ValidateStatsTable()
        {
            if (statsTable == null || statsTable.Count == 0) return;

            foreach (string issue in GetStatsTableIssues())
            {
                Debug.LogWarning($"[{nameof(TankBrain)}] Bang chi so co van de: {issue}", this);
            }
        }

        /// <summary>
        /// Mo ta loi cua tung muc vi pham trong bang (danh sach rong = khong loi).
        /// </summary>
        private System.Collections.Generic.List<string> GetStatsTableIssues()
        {
            System.Collections.Generic.List<string> issues = new System.Collections.Generic.List<string>();
            if (statsTable == null || statsTable.Count == 0) return issues;

            GetValidEntries(out System.Collections.Generic.HashSet<int> validIndices);

            // Lay "muc hop le gan nhat truoc do" theo thu tu sort de gan loi tung muc.
            int[] order = new int[statsTable.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            System.Array.Sort(order, (a, b) =>
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

        /// <summary>Mo ta ngan gon cac chi so bi giam giua 2 muc.</summary>
        private static string DescribeDiff(TankStats higher, TankStats lower)
        {
            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
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

        /// <summary>
        /// Lay cac muc hop le cua bang, da sort theo baseLevel tang dan.
        /// Cac muc vi pham bi bo qua (khong xoa khoi bang goc).
        /// </summary>
        private System.Collections.Generic.List<StatsEntry> GetValidEntries(out System.Collections.Generic.HashSet<int> validIndices)
        {
            int count = statsTable != null ? statsTable.Count : 0;

            System.Collections.Generic.List<StatsEntry> result =
                new System.Collections.Generic.List<StatsEntry>(count);
            validIndices = new System.Collections.Generic.HashSet<int>();

            if (count == 0) return result;

            // Sort ON DINH theo baseLevel: sap xep mang index, key bang nhau thi
            // giu theo thu tu goc de muc "dung truoc" duoc uu tien.
            int[] order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;

            System.Array.Sort(order, (a, b) =>
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

        #endregion

        #region Input commands

        /// <summary>Nhan lenh di chuyen (Vector2) tu GameController.</summary>
        public void SetMoveInput(Vector2 input)
        {
            m_MoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        /// <summary>Nhan lenh huong nhin. Point la vi tri muc tieu trong world (con tro chuot).</summary>
        public void SetLookTarget(Vector2 worldPoint)
        {
            m_LookTarget = worldPoint;
            m_HasLookTarget = true;
        }

        /// <summary>Tat action Look. Noi sung giu nguyen huong hien tai.</summary>
        public void ClearLookTarget()
        {
            m_HasLookTarget = false;
            view?.StopLook();
        }

        #endregion

        #region Gameplay

        private void Update()
        {
            TickCooldowns(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            // TODO: thay bang he thong di chuyen that su (dash, do loi, terrain...).
            if (m_Body != null)
            {
                m_Body.linearVelocity = m_MoveInput * m_Stats.MoveSpeed;
            }

            if (view == null) return;

            // --- Action Move ---------------------------------------------------
            if (m_MoveInput.sqrMagnitude > 0.0001f)
            {
                view.Move(m_MoveInput.normalized);
            }
            else
            {
                view.Stop();
            }

            // --- Action Look: LUON chay, xoay noi sung ve con tro chuot -------
            if (m_HasLookTarget)
            {
                view.Look(m_LookTarget);
            }
        }

        /// <summary>Dung 1 trong 4 ky nang. Chi tai thoi diem nay moi co Debug.Log.</summary>
        public void UseSkill(int index)
        {
            if (index < 0 || index >= SkillCount) return;
            if (skillCooldowns[index] > 0f) return;

            // TODO: them logic tung ky nang (damage, buff, heal, dash...).

            Debug.Log($"[TankBrain] Level {level} su dung ky nang {index + 1}/{SkillCount}.");
            skillCooldowns[index] = GetCooldownWithReduction(GetBaseCooldown(index));
            view?.UseSkill(index);
        }

        /// <summary>Nhan sat thuong. TODO: ap dung Defense/ArmorPenetration.</summary>
        public void TakeDamage(float amount)
        {
            // TODO: tinh damage thuc te bang stats.Defense va stats.ArmorPenetration cua doi phuong.
            currentHealth -= amount;
        }

        public void Heal(float amount)
        {
            currentHealth = Mathf.Min(currentHealth + amount, m_Stats.MaxHealth);
        }

        public void GainEnergy(float amount)
        {
            currentEnergy = Mathf.Min(currentEnergy + amount, m_Stats.MaxEnergy);
        }

        public void SpendEnergy(float amount)
        {
            currentEnergy = Mathf.Max(currentEnergy - amount, 0f);
        }

        #endregion

        #region Helpers

        private void TickCooldowns(float deltaTime)
        {
            for (int i = 0; i < skillCooldowns.Length; i++)
            {
                if (skillCooldowns[i] > 0f)
                {
                    skillCooldowns[i] = Mathf.Max(0f, skillCooldowns[i] - deltaTime);
                }
            }
        }

        private float GetCooldownWithReduction(float baseCooldown)
        {
            return baseCooldown * (1f - Mathf.Clamp(m_Stats.CooldownReduction, 0f, 1f));
        }

        private float GetBaseCooldown(int index)
        {
            // TODO: thay bang bang cooldown tung ky nang.
            return 5f;
        }

        #endregion
    }
}