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
    /// Day la lop ABSTRACT (framework) - dung lop con cu the (vi du TankBrain_Test).
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class TankBrain : MonoBehaviour
    {
        public const int SkillCount = 4;

        [Header("View")]
        [Tooltip("View dang chay. Se duoc gan luc runtime tu TankDefinition.")]
        [SerializeField, ReadOnly] private TankView view;

        [Header("Level")]
        [SerializeField] private int level = TankDefinition.MinLevel;

        [Header("Stats")]
        [Tooltip("Chi so co so do TankDefinition gui vao. KHONG sua tay.")]
        [ReadOnly]
        [SerializeField] private TankStats baseStats = new TankStats();

        [Tooltip("Chi so phu cong them tu nguoi khac (buff/debuff...). Mac dinh moi chi so = 0. Luon duoc tinh lai khi Equip/Unequip.")]
        [ReadOnly]
        [SerializeField] private TankStats bonusStats = TankStats.Zeroed();

        [Header("Runtime")]
        [SerializeField] private float currentHealth;
        [SerializeField] private float currentEnergy;
        [SerializeField, ReadOnly] private float[] skillCooldowns = new float[SkillCount];


        private Rigidbody2D m_Body;
        private TankStats m_FinalStats = new TankStats();
        private Vector2 m_MoveInput;
        private Vector2 m_LookTarget;
        private bool m_HasLookTarget;
        private Dictionary<string, TankStats> m_BonusStatsByKey = new Dictionary<string, TankStats>();

        // Trang bi dang trang bi: 1 slot -> 1 trang bi.
        private readonly Dictionary<EquipmentSlot, EquipmentBrain> m_Equipment = new Dictionary<EquipmentSlot, EquipmentBrain>();

        [Header("Context")]
        [SerializeField, ReadOnly] private TankBrainContext m_context;

        protected bool _initialized = false;
        private TankView viewPrefab;
        private TankStats stats;

        public TankView View => view;
        public TankBrainContext Context => m_context;

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
        public virtual void Initialize(TankBrainContext context, TankStats stats)
        {
            if (_initialized) return;
            _initialized = true;

            this.m_context = context;
            this.stats = stats;

            this.level = Mathf.Clamp(context.Level, TankDefinition.MinLevel, TankDefinition.MaxLevel);
            this.baseStats = stats != null ? stats.Clone() : new TankStats();

            RecalculateStats();

            currentHealth = m_FinalStats.MaxHealth;
            currentEnergy = m_FinalStats.MaxEnergy;
            for (int i = 0; i < skillCooldowns.Length; i++) skillCooldowns[i] = 0f;
        }

        /// <summary>Tinh lai chi so cuoi cung = base + bonus, va keo lai HP/Energy.</summary>
        protected virtual void RecalculateStats()
        {
            m_FinalStats = baseStats.Add(bonusStats);

            currentHealth = Mathf.Clamp(currentHealth, 0f, m_FinalStats.MaxHealth);
            currentEnergy = Mathf.Clamp(currentEnergy, 0f, m_FinalStats.MaxEnergy);
        }

        /// <summary>
        /// Cong 1 chi so bonus vao khoi danh sach (theo key).
        /// Neu key da ton tai thi thay the bock (hon cong hoac tru).
        /// </summary>
        public virtual void AddBonusStats(string key, TankStats delta)
        {
            if (delta == null) return;

            if (m_BonusStatsByKey.ContainsKey(key))
            {
                m_BonusStatsByKey[key] = delta;
            }
            else
            {
                m_BonusStatsByKey[key] = delta;
            }

            // Tinh lai bonusStats = tong cua tat ca keys.
            RecalculateBonusStats();
        }

        /// <summary>
        /// Xoa 1 chi so bonus khoi danh sach (theo key).
        /// </summary>
        public virtual void RemoveBonusStats(string key)
        {
            if (m_BonusStatsByKey.Remove(key))
            {
                RecalculateBonusStats();
            }
        }

        /// <summary>
        /// Tinh lai bonusStats = tong cua tat ca cac chi so theo key.
        /// </summary>
        private void RecalculateBonusStats()
        {
            bonusStats = TankStats.Zeroed();
            foreach (var entry in m_BonusStatsByKey.Values)
            {
                bonusStats = bonusStats.Add(entry);
            }
            RecalculateStats();
        }

        /// <summary>Xoa toan bo chi so bonus (het buff).</summary>
        protected virtual void ClearBonusStats()
        {
            m_BonusStatsByKey.Clear();
            RecalculateBonusStats();
        }

        /// <summary>Nhan TankView prefab, tao GameObject con va dung lam View cua Tank nay.</summary>
        public virtual void SetViewPrefab(TankView prefab)
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

            view.RegisterOwner(this);
        }

        #region Equipment
        
        /// <summary>Nong phao dang trang bi (slot Firepower).</summary>
        public virtual FirepowerCore GetFirepowerCore()
        {
            return m_Equipment.TryGetValue(EquipmentSlot.Firepower, out EquipmentBrain equipment)
                ? equipment as FirepowerCore
                : null;
        }

        /// <summary>Bo banh dang trang bi (slot Mobility).</summary>
        public virtual MobilityCore GetMobilityCore()
        {
            return m_Equipment.TryGetValue(EquipmentSlot.Mobility, out EquipmentBrain equipment)
                ? equipment as MobilityCore
                : null;
        }

        /// <summary>Vo xe dang trang bi (slot Defense).</summary>
        public virtual DefenseCore GetDefenseCore()
        {
            return m_Equipment.TryGetValue(EquipmentSlot.Defense, out EquipmentBrain equipment)
                ? equipment as DefenseCore
                : null;
        }

        /// <summary>Nong phao: cong stat + gan Owner (slot Firepower).</summary>
        public virtual FirepowerCore EquipFirepowerCore(FirepowerCore prefab)
        {
            if (prefab == null) return null;
            if (!typeof(FirepowerCore).IsAssignableFrom(prefab.GetType()))
            {
                Debug.LogError("[" + "TankBrain" + "] Can pass FirepowerCore prefab, get " + prefab.GetType().Name + ".", this);
                return null;
            }

            FirepowerCore instance = Instantiate(prefab, transform);
            instance.name = prefab.name;
            instance.Equip(this);

            m_Equipment[EquipmentSlot.Firepower] = instance;

            return instance;
        }

        /// <summary>Bo banh: cong stat + gan Owner (slot Mobility).</summary>
        public virtual MobilityCore EquipMobilityCore(MobilityCore prefab)
        {
            if (prefab == null) return null;
            if (!typeof(MobilityCore).IsAssignableFrom(prefab.GetType()))
            {
                Debug.LogError("[" + "TankBrain" + "] Can pass MobilityCore prefab, get " + prefab.GetType().Name + ".", this);
                return null;
            }

            MobilityCore instance = Instantiate(prefab, transform);
            instance.name = prefab.name;
            instance.Equip(this);

            m_Equipment[EquipmentSlot.Mobility] = instance;

            return instance;
        }

        /// <summary>Vo xe: cong stat + gan Owner (slot Defense).</summary>
        public virtual DefenseCore EquipDefenseCore(DefenseCore prefab)
        {
            if (prefab == null) return null;
            if (!typeof(DefenseCore).IsAssignableFrom(prefab.GetType()))
            {
                Debug.LogError("[" + "TankBrain" + "] Can pass DefenseCore prefab, get " + prefab.GetType().Name + ".", this);
                return null;
            }

            DefenseCore instance = Instantiate(prefab, transform);
            instance.name = prefab.name;
            instance.Equip(this);

            m_Equipment[EquipmentSlot.Defense] = instance;

            return instance;
        }

        /// <summary>Thoa trang bi o 1 slot. True neu co trang bi de thoa.</summary>
        public virtual bool UnequipEquipment(EquipmentSlot slot)
        {
            if (!m_Equipment.TryGetValue(slot, out EquipmentBrain equipment)) return false;

            m_Equipment.Remove(slot);
            equipment.UnEquip(this);

            return true;
        }

        #endregion

        private void Awake()
        {
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
        public virtual void SetMoveInput(Vector2 input)
        {
            m_MoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        /// <summary>Nhan diem nhin trong world (vi du vi tri con tro chuot).</summary>
        public virtual void SetLookTarget(Vector2 worldPoint)
        {
            m_LookTarget = worldPoint;
            m_HasLookTarget = true;
        }


        /// <summary>Dung 1 trong 4 ky nang. Chi tai thoi diem nay moi co Debug.Log.</summary>
        public virtual void UseSkill(int index)
        {
            if (index < 0 || index >= SkillCount) return;
            if (skillCooldowns[index] > 0f) return;

            Debug.Log($"[TankBrain] Level {level} su dung ky nang {index + 1}/{SkillCount}.");
        }

        /// <summary>Tat action Look. Noi sung giu nguyen huong hien tai.</summary>
        public virtual void ClearLookTarget()
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

        /// <summary>Nhan sat thuong. TODO: ap dung Defense/ArmorPenetration.</summary>
        public virtual void TakeDamage(float amount)
        {
            // TODO: tinh damage thuc te bang m_FinalStats.Defense va ArmorPenetration cua doi phuong.
            currentHealth -= amount;
        }

        public virtual void Heal(float amount)
        {
            currentHealth = Mathf.Min(currentHealth + amount, m_FinalStats.MaxHealth);
        }

        public virtual void GainEnergy(float amount)
        {
            currentEnergy = Mathf.Min(currentEnergy + amount, m_FinalStats.MaxEnergy);
        }

        public virtual void SpendEnergy(float amount)
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

        protected float GetCooldownWithReduction(float baseCooldown)
        {
            return baseCooldown * (1f - Mathf.Clamp(m_FinalStats.CooldownReduction, 0f, 1f));
        }

        protected virtual float GetBaseCooldown(int index)
        {
            // TODO: thay bang bang cooldown tung ky nang.
            return 5f;
        }

        protected float GetCooldown(int index)
        {
            return GetCooldownWithReduction(GetBaseCooldown(index));
        }

        #endregion
    }
}
