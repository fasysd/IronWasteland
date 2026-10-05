using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Lop dieu khien Tank. Day la noi chua RUNTIME STATE:
    /// - base stats (do TankDefinition gui vao khi khoi tao)
    /// - bonus stats - chi so phu cong them tu nguoi khac (buff/debuff, mac dinh 0)
    /// - HP / Energy hien tai
    /// Khong con bang chi co so - phan do thuoc ve TankDefinition.
    ///
    /// Chi so cuoi cung = Base stats + Bonus stats.
    /// Level khong doi duoc sau khi khoi tao.
    /// </summary>
    [DisallowMultipleComponent]
    public class TankBrain : MonoBehaviour
    {
        public const int SkillCount = 4;

        [Header("View")]
        [Tooltip("View dang chay. Se duoc gan luc runtime tu TankDefinition.")]
        [SerializeField] private TankView view;

        [Tooltip("Prefab TankView de spawn. Neu rong thi Tank khong co hinh anh.")]
        [SerializeField] private TankView viewPrefab;

        [Header("Level")]
        [SerializeField] private int level = TankDefinition.MinLevel;

        [Header("Stats")]
        [Tooltip("Chi so co so do TankDefinition gui vao. KHONG sua tay.")]
        [ReadOnly]
        [SerializeField] private TankStats baseStats = new TankStats();

        [Tooltip("Chi so phu cong them tu nguoi khac (buff/debuff...). Mac dinh moi chi so = 0. Luon duoc tinh lai khi Equip/Unequip.")]
        [ReadOnly]
        [SerializeField] private TankStats bonusStats = TankStats.Zeroed();

        [SerializeField, HideInInspector] private TankDefinition definition;

        [Header("Runtime")]
        [SerializeField] private float currentHealth;
        [SerializeField] private float currentEnergy;
        [SerializeField] private float[] skillCooldowns = new float[SkillCount];

        private Rigidbody2D m_Body;
        private TankStats m_FinalStats = new TankStats();
        private Vector2 m_MoveInput;
        private Vector2 m_LookTarget;
        private bool m_HasLookTarget;

        // Trang bi dang trang bi: 1 slot -> 1 trang bi.
        private readonly Dictionary<EquipmentSlot, EquipmentBrain> m_Equipment
            = new Dictionary<EquipmentSlot, EquipmentBrain>();

        // Trang bi cho doi View co xuat hienh (SetViewPrefab chay sau Initialize).
        private readonly List<EquipmentBrain> m_PendingEquipment = new List<EquipmentBrain>();

        public TankView View => view;
        public TankView ViewPrefab => viewPrefab;
        public TankDefinition Definition => definition;
        public int Level => level;

        /// <summary>Chi so goc (chua tinh bonus).</summary>
        public TankStats BaseStats => baseStats;

        /// <summary>Chi so phu cong them tu nguoi khac (buff/debuff...).</summary>
        public TankStats BonusStats => bonusStats;

        /// <summary>Chi so cuoi cung = base + bonus. Dung de tinh toan.</summary>
        public TankStats Stats => m_FinalStats;

        public float CurrentHealth => currentHealth;
        public float CurrentEnergy => currentEnergy;
        public float GetSkillCooldown(int index) => skillCooldowns[Mathf.Clamp(index, 0, SkillCount - 1)];

        #region Setup

        /// <summary>Do TankDefinition goi luc khoi tao. Chi goi 1 lan.</summary>
        public void Initialize(int level, TankStats stats, TankDefinition definition = null)
        {
            this.level = Mathf.Clamp(level, TankDefinition.MinLevel, TankDefinition.MaxLevel);
            this.baseStats = stats != null ? stats.Clone() : new TankStats();
            this.definition = definition;

            RecalculateStats();

            currentHealth = m_FinalStats.MaxHealth;
            currentEnergy = m_FinalStats.MaxEnergy;
            for (int i = 0; i < skillCooldowns.Length; i++) skillCooldowns[i] = 0f;
        }

        /// <summary>Tinh lai chi so cuoi cung = base + bonus, va keo lai HP/Energy.</summary>
        public void RecalculateStats()
        {
            m_FinalStats = baseStats.Add(bonusStats);

            currentHealth = Mathf.Clamp(currentHealth, 0f, m_FinalStats.MaxHealth);
            currentEnergy = Mathf.Clamp(currentEnergy, 0f, m_FinalStats.MaxEnergy);
        }

        /// <summary>Them/tru chi so bonus tu nguoi khac (buff, debuff...). Tu keo lai HP/Energy.</summary>
        public void ModifyBonusStats(TankStats delta)
        {
            if (delta == null) return;
            bonusStats.AddTo(delta);
            RecalculateStats();
        }

        /// <summary>Xoa toan bo chi so bonus (het buff).</summary>
        public void ClearBonusStats()
        {
            bonusStats = TankStats.Zeroed();
            RecalculateStats();
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

            // View vua san -> gan lai cac trang bi cho doi.
            FlushPendingEquipment();
        }

        #region Equipment

        /// <summary>Trang bi dang trang bi cua 1 slot (null neu rong).</summary>
        public EquipmentBrain GetEquipment(EquipmentSlot slot)
        {
            return m_Equipment.TryGetValue(slot, out EquipmentBrain equipment) ? equipment : null;
        }

        /// <summary>So trang bi dang trang bi.</summary>
        public int EquipmentCount => m_Equipment.Count;

        /// <summary>Tat ca trang bi dang trang bi (khong phai ban sao).</summary>
        public IEnumerable<EquipmentBrain> GetEquipments() => m_Equipment.Values;

        /// <summary>
        /// Trang bi 1 trang bi. Tu tao instance tu prefab roi goi
        /// <see cref="EquipmentBrain.Equip"/> - noi dung gi vao TankView.
        /// Neu View chua co (Tank moi Initialize) thi cho vao hang doi, gan lai
        /// ngay khi View xuat hienh.
        /// </summary>
        public EquipmentBrain EquipEquipment(EquipmentBrain prefab)
        {
            if (prefab == null) return null;

            // Slot da co trang bi -> thoa truoc de khong cong chieu stat.
            EquipmentSlot slot = prefab.Slot;
            if (m_Equipment.TryGetValue(slot, out EquipmentBrain existing))
            {
                UnequipEquipment(slot);
            }

            // Khong parent vao tank: giu local pose thiet ke cua prefab.
            // AttachEquipment sau do dua vao slot voi worldPositionStays = false
            // -> trang bi dung dung theo he toa do cua slot.
            EquipmentBrain instance = Instantiate(prefab);
            instance.name = prefab.name;

            if (view == null)
            {
                // Chua co View -> cho doi, chi ghi nho de gan lai sau.
                // Tat truoc de khong ve ra world origin trong khi cho.
                instance.gameObject.SetActive(false);
                m_PendingEquipment.Add(instance);
            }
            else
            {
                instance.Equip(this);
            }

            m_Equipment[slot] = instance;
            return instance;
        }

        /// <summary>Trang bi 1 trang bi theo ID tra cuu tu <paramref name="database"/>.</summary>
        public EquipmentBrain EquipEquipmentById(EquipmentDatabase database, EquipmentSlot slot, string id)
        {
            if (database == null)
            {
                Debug.LogError($"[{nameof(TankBrain)}] Chua gan EquipmentDatabase.", this);
                return null;
            }

            return EquipEquipment(database.GetPrefab(slot, id));
        }

        /// <summary>Thoa trang bi o 1 slot. True neu co trang bi de thoa.</summary>
        public bool UnequipEquipment(EquipmentSlot slot)
        {
            if (!m_Equipment.TryGetValue(slot, out EquipmentBrain equipment)) return false;

            m_Equipment.Remove(slot);
            m_PendingEquipment.Remove(equipment);
            equipment.UnEquip(this);

            return true;
        }

        /// <summary>Thoa het trang bi.</summary>
        public void UnequipAll()
        {
            foreach (EquipmentBrain equipment in m_Equipment.Values)
            {
                equipment.UnEquip(this);
            }

            m_Equipment.Clear();
            m_PendingEquipment.Clear();
        }

        /// <summary>
        /// Thong bao cho trang bi bi dong (DamageLifesteal) khi Tank vua gay sat thuong.
        /// He thong tan cong se goi ham nay khi biet danh trung.
        /// </summary>
        public void NotifyDamageDealt(float damage)
        {
            foreach (EquipmentBrain equipment in m_Equipment.Values)
            {
                equipment.OnOwnerDamageDealt(damage);
            }
        }

        /// <summary>Gan lai trang bi cho doi (chay khi View vua duoc tao).</summary>
        private void FlushPendingEquipment()
        {
            if (m_PendingEquipment.Count == 0) return;

            foreach (EquipmentBrain equipment in m_PendingEquipment)
            {
                // Mo lai truoc khi Equip -> AttachEquipment se dua vao slot.
                equipment.gameObject.SetActive(true);
                equipment.Equip(this);
            }

            m_PendingEquipment.Clear();
        }

        #endregion

        /// <summary>TankBrain dang ky lam Owner cua View (va cac collider ben trong).</summary>
        public void RegisterInto(TankView target)
        {
            target?.RegisterOwner(this);
        }

        private void Awake()
        {
            if (view == null) view = GetComponentInChildren<TankView>();

            // View co the da ton tai san trong hierarchy (khong qua definition).
            RegisterInto(view);

            // Rigidbody2D nam tren chinh TankBrain. Fallback sang child cho an toan.
            m_Body = GetComponent<Rigidbody2D>();
            if (m_Body == null) m_Body = GetComponentInChildren<Rigidbody2D>();

            if (bonusStats == null) bonusStats = TankStats.Zeroed();
            if (baseStats == null) baseStats = new TankStats();

            RecalculateStats();

            if (currentHealth <= 0f) currentHealth = m_FinalStats.MaxHealth;
            if (currentEnergy <= 0f) currentEnergy = m_FinalStats.MaxEnergy;
        }

        #endregion

        #region Input commands

        /// <summary>Nhan lenh di chuyen (Vector2) tu GameController.</summary>
        public void SetMoveInput(Vector2 input)
        {
            m_MoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        /// <summary>Nhan diem nhin trong world (vi du vi tri con tro chuot).</summary>
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
                m_Body.linearVelocity = m_MoveInput * m_FinalStats.MoveSpeed;
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

            // --- Action Look: LUON chay, xoay noi sung ve muc tieu ------------
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
            // TODO: tinh damage thuc te bang m_FinalStats.Defense va ArmorPenetration cua doi phuong.
            currentHealth -= amount;
        }

        public void Heal(float amount)
        {
            currentHealth = Mathf.Min(currentHealth + amount, m_FinalStats.MaxHealth);
        }

        public void GainEnergy(float amount)
        {
            currentEnergy = Mathf.Min(currentEnergy + amount, m_FinalStats.MaxEnergy);
        }

        public void SpendEnergy(float amount)
        {
            currentEnergy = Mathf.Max(currentEnergy - amount, 0f);
        }

        public bool IsAlive => currentHealth > 0f;

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
            return baseCooldown * (1f - Mathf.Clamp(m_FinalStats.CooldownReduction, 0f, 1f));
        }

        private float GetBaseCooldown(int index)
        {
            // TODO: thay bang bang cooldown tung ky nang.
            return 5f;
        }

        #endregion
    }
}
