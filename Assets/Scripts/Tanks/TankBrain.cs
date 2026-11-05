using System;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Core.Effects;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Lop dieu khien Tank. Day la noi chua RUNTIME STATE:
    /// - base stats (do TankDefinition gui vao khi khoi tao theo Level)
    /// - bonus stats (phu kien, buff/debuff)
    /// - HP / Energy / Shield hien tai
    /// - Quan ly 1 Phu kien (Accessory)
    /// - Quan ly Khong che (CC) va Hieu ung (Status Effects)
    /// - 4 slot ky nang (Thuong, Chuyen biet, Tuy chon 1 & 2)
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class TankBrain : MonoBehaviour
    {
        public const int SkillCount = 4;

        [Header("View")]
        [Tooltip("View dang chay. Se duoc gan luc runtime tu TankDefinition.")]
        [SerializeField, ReadOnly] private TankView view;

        [Header("Level")]
        [SerializeField, ReadOnly] private int level = TankDefinition.MinLevel;

        [Header("Stats")]
        [Tooltip("Chi so co so do TankDefinition gui vao. KHONG sua tay.")]
        [ReadOnly]
        [SerializeField] private TankStats baseStats = new TankStats();

        [Tooltip("Chi so phu cong phang (flat) tong cong.")]
        [ReadOnly]
        [SerializeField] private TankStats flatBonusStats = TankStats.Zeroed();

        [Tooltip("Chi so phu cong phan tram (percent) tong cong.")]
        [ReadOnly]
        [SerializeField] private TankStats percentBonusStats = TankStats.Zeroed();

        [Header("Runtime")]
        [SerializeField] private float currentHealth;
        [SerializeField] private float currentEnergy;
        [SerializeField, ReadOnly] private float[] skillCooldowns = new float[SkillCount];
        /// <summary>(người nhận, lượng hồi, có phải máu do chia sẻ không)</summary>
        public event Action<TankBrain, float, bool> OnHealed;

        [Header("Context")]
        [SerializeField, ReadOnly] private TankBrainContext m_context;

        // References & Physics
        private Rigidbody2D m_Body;
        private TankStats m_FinalStats = new TankStats();
        private Vector2 m_MoveInput;
        private Vector2 m_LookTarget;
        private bool m_HasLookTarget;

        // Bonus stats storage by key
        private readonly Dictionary<string, TankStats> m_FlatBonusByKey = new Dictionary<string, TankStats>();
        private readonly Dictionary<string, TankStats> m_PercentBonusByKey = new Dictionary<string, TankStats>();

        // 1 Trang bi / Phu kien duy nhat theo GDD
        [Header("Equipment")]
        [SerializeField, ReadOnly] private EquipmentBrain m_EquippedAccessory;

        // Crowd Control (CC)
        private readonly Dictionary<CrowdControlType, ActiveCrowdControl> m_ActiveCC = new Dictionary<CrowdControlType, ActiveCrowdControl>();
        private readonly List<CrowdControlType> m_CCToRemove = new List<CrowdControlType>();

        // Status Effects (Buff, Debuff, DoT, HoT, Shield)
        private readonly List<StatusEffect> m_ActiveEffects = new List<StatusEffect>();

        protected bool _initialized = false;
        private TankView viewPrefab;

        #region Events
        public event Action<DamageInfo> OnTakeDamageEvent;
        public event Action<DamageInfo> OnDealDamageEvent;
        public event Action<float> OnHealEvent;
        public event Action<int> OnSkillUsedEvent;
        public event Action<float> OnShieldChangedEvent;
        public event Action<CrowdControlType> OnCrowdControlApplied;
        public event Action<CrowdControlType> OnCrowdControlRemoved;
        public event Action<StatusEffect> OnEffectAppliedEvent;
        public event Action<StatusEffect> OnEffectRemovedEvent;
        public event Action OnDeathEvent;
        #endregion

        #region Properties
        public TankView View => view;
        public TankBrainContext Context => m_context;
        public TankStats BaseStats => baseStats;
        public TankStats Stats => m_FinalStats;
        public Vector2 LookTarget => m_LookTarget;
        public bool HasLookTarget => m_HasLookTarget;
        public float CurrentHealth => currentHealth;
        public float CurrentEnergy => currentEnergy;

        /// <summary>
        /// Tong gia tri khien hien tai duoc tinh tu tat ca cac ShieldEffect dang hoat dong.
        /// </summary>
        public float CurrentShield
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < m_ActiveEffects.Count; i++)
                {
                    if (m_ActiveEffects[i] is ShieldEffect shield && !shield.IsExpired)
                    {
                        total += shield.RemainingShield;
                    }
                }
                return total;
            }
        }

        public EquipmentBrain Accessory => m_EquippedAccessory;
        public bool IsAlive => currentHealth > 0f;
        public bool IsCrowdControlImmune { get; set; }

        public bool CanMove => IsAlive && !HasCC(CrowdControlType.Stun) && !HasCC(CrowdControlType.Root);
        public bool CanUseSkill => IsAlive && !HasCC(CrowdControlType.Stun) && !HasCC(CrowdControlType.Silence);
        public bool CanLook => IsAlive && !HasCC(CrowdControlType.Stun);

        public IReadOnlyList<StatusEffect> ActiveEffects => m_ActiveEffects;
        public float GetSkillCooldown(int index) => skillCooldowns[Mathf.Clamp(index, 0, SkillCount - 1)];
        #endregion

        #region Setup

        /// <summary>Do TankDefinition goi luc khoi tao. Chi goi 1 lan.</summary>
        public virtual void Initialize(TankBrainContext context, TankStats stats)
        {
            if (_initialized) return;
            _initialized = true;

            this.m_context = context;
            this.level = Mathf.Clamp(context.Level, TankDefinition.MinLevel, TankDefinition.MaxLevel);
            this.baseStats = stats != null ? stats.Clone() : new TankStats();

            RecalculateStats();

            currentHealth = m_FinalStats.MaxHealth;
            currentEnergy = m_FinalStats.MaxEnergy;
            for (int i = 0; i < skillCooldowns.Length; i++) skillCooldowns[i] = 0f;
        }

        protected virtual void RecalculateStats()
        {
            m_FinalStats = TankStats.CalculateFinalStats(baseStats, flatBonusStats, percentBonusStats);

            currentHealth = Mathf.Clamp(currentHealth, 0f, m_FinalStats.MaxHealth);
            currentEnergy = Mathf.Clamp(currentEnergy, 0f, m_FinalStats.MaxEnergy);
        }

        public virtual void AddBonusStats(string key, TankStats flatDelta, TankStats percentDelta = null)
        {
            if (string.IsNullOrEmpty(key)) return;

            if (flatDelta != null) m_FlatBonusByKey[key] = flatDelta;
            if (percentDelta != null) m_PercentBonusByKey[key] = percentDelta;

            RecalculateBonusStats();
        }

        public virtual void AddBonusStats(string key, TankStats flatDelta)
        {
            AddBonusStats(key, flatDelta, null);
        }

        public virtual void RemoveBonusStats(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            bool removed = m_FlatBonusByKey.Remove(key);
            removed |= m_PercentBonusByKey.Remove(key);

            if (removed)
            {
                RecalculateBonusStats();
            }
        }

        private void RecalculateBonusStats()
        {
            flatBonusStats = TankStats.Zeroed();
            foreach (var entry in m_FlatBonusByKey.Values)
            {
                flatBonusStats.AddTo(entry);
            }

            percentBonusStats = TankStats.Zeroed();
            foreach (var entry in m_PercentBonusByKey.Values)
            {
                percentBonusStats.AddTo(entry);
            }

            RecalculateStats();
        }

        protected virtual void ClearBonusStats()
        {
            m_FlatBonusByKey.Clear();
            m_PercentBonusByKey.Clear();
            RecalculateBonusStats();
        }

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

        #endregion

        #region Equipment (1 o Phu kien duy nhat theo GDD)

        public virtual bool EquipAccessory(EquipmentBrain prefab)
        {
            if (prefab == null) return false;

            if (m_EquippedAccessory != null)
            {
                UnequipAccessory();
            }

            EquipmentBrain instance = Instantiate(prefab, transform);
            instance.name = prefab.name;

            if (instance.Equip(this))
            {
                m_EquippedAccessory = instance;
                return true;
            }

            Destroy(instance.gameObject);
            return false;
        }

        public virtual bool UnequipAccessory()
        {
            if (m_EquippedAccessory == null) return false;

            EquipmentBrain current = m_EquippedAccessory;
            m_EquippedAccessory = null;

            current.UnEquip(this);
            Destroy(current.gameObject);
            return true;
        }

        #endregion

        #region Crowd Control (CC)

        public bool HasCC(CrowdControlType type) => m_ActiveCC.ContainsKey(type);

        public virtual void ApplyCrowdControl(CrowdControlType type, float duration, float strength = 0f)
        {
            if (type == CrowdControlType.None) return;

            if (IsCrowdControlImmune)
            {
                return;
            }

            if (m_ActiveCC.TryGetValue(type, out var existing))
            {
                existing.Duration = Mathf.Max(existing.Duration, duration);
                existing.RemainingTime = Mathf.Max(existing.RemainingTime, duration);
                existing.Strength = Mathf.Max(existing.Strength, strength);
            }
            else
            {
                var cc = new ActiveCrowdControl(type, duration, strength);
                m_ActiveCC[type] = cc;
                OnCrowdControlApplied?.Invoke(type);
            }

            // Neu bi troi hoac choang, dung ngay di chuyen
            if (!CanMove && m_Body != null)
            {
                m_Body.linearVelocity = Vector2.zero;
                view?.Stop();
            }
        }

        public virtual void RemoveCrowdControl(CrowdControlType type)
        {
            if (m_ActiveCC.Remove(type))
            {
                OnCrowdControlRemoved?.Invoke(type);
            }
        }

        public virtual void ClearAllCrowdControl()
        {
            foreach (var type in m_ActiveCC.Keys)
            {
                OnCrowdControlRemoved?.Invoke(type);
            }
            m_ActiveCC.Clear();
        }

        private void TickCrowdControl(float deltaTime)
        {
            if (m_ActiveCC.Count == 0) return;

            m_CCToRemove.Clear();
            foreach (var kvp in m_ActiveCC)
            {
                kvp.Value.Tick(deltaTime);
                if (kvp.Value.IsExpired)
                {
                    m_CCToRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < m_CCToRemove.Count; i++)
            {
                RemoveCrowdControl(m_CCToRemove[i]);
            }
        }

        #endregion

        #region Status Effects

        public virtual void ApplyEffect(StatusEffect effect)
        {
            if (effect == null) return;

            for (int i = 0; i < m_ActiveEffects.Count; i++)
            {
                if (m_ActiveEffects[i].EffectId == effect.EffectId)
                {
                    m_ActiveEffects[i].OnStackAdded(this, m_ActiveEffects[i].CurrentStacks + 1);
                    return;
                }
            }

            m_ActiveEffects.Add(effect);
            effect.OnApplied(this);
            OnEffectAppliedEvent?.Invoke(effect);

            if (effect is ShieldEffect)
            {
                OnShieldChangedEvent?.Invoke(CurrentShield);
            }
        }

        public virtual bool RemoveEffect(string effectId)
        {
            for (int i = 0; i < m_ActiveEffects.Count; i++)
            {
                if (m_ActiveEffects[i].EffectId == effectId)
                {
                    StatusEffect effect = m_ActiveEffects[i];
                    m_ActiveEffects.RemoveAt(i);
                    effect.OnRemoved(this);
                    OnEffectRemovedEvent?.Invoke(effect);

                    if (effect is ShieldEffect)
                    {
                        OnShieldChangedEvent?.Invoke(CurrentShield);
                    }
                    return true;
                }
            }
            return false;
        }

        public virtual bool HasEffect(string effectId)
        {
            for (int i = 0; i < m_ActiveEffects.Count; i++)
            {
                if (m_ActiveEffects[i].EffectId == effectId) return true;
            }
            return false;
        }

        public virtual StatusEffect GetEffect(string effectId)
        {
            for (int i = 0; i < m_ActiveEffects.Count; i++)
            {
                if (m_ActiveEffects[i].EffectId == effectId) return m_ActiveEffects[i];
            }
            return null;
        }

        public virtual void ClearAllEffects()
        {
            bool hadShield = false;
            for (int i = m_ActiveEffects.Count - 1; i >= 0; i--)
            {
                StatusEffect effect = m_ActiveEffects[i];
                if (effect is ShieldEffect) hadShield = true;
                effect.OnRemoved(this);
                OnEffectRemovedEvent?.Invoke(effect);
            }
            m_ActiveEffects.Clear();

            if (hadShield)
            {
                OnShieldChangedEvent?.Invoke(0f);
            }
        }

        private void TickStatusEffects(float deltaTime)
        {
            bool shieldChanged = false;
            for (int i = m_ActiveEffects.Count - 1; i >= 0; i--)
            {
                StatusEffect effect = m_ActiveEffects[i];
                effect.UpdateTick(this, deltaTime);
                if (effect.IsExpired)
                {
                    if (effect is ShieldEffect) shieldChanged = true;
                    m_ActiveEffects.RemoveAt(i);
                    effect.OnRemoved(this);
                    OnEffectRemovedEvent?.Invoke(effect);
                }
            }

            if (shieldChanged)
            {
                OnShieldChangedEvent?.Invoke(CurrentShield);
            }
        }

        #endregion

        #region Shield System

        /// <summary>
        /// Them mot lop khien moi thong qua ShieldEffect voi nguon goc (Source) va thoi gian ton tai xac dinh.
        /// </summary>
        public virtual void AddShield(float amount, float duration, string effectId = null, UnityEngine.Object source = null, string displayName = "Khiên")
        {
            if (amount <= 0f) return;
            string id = string.IsNullOrEmpty(effectId) ? $"Shield_{Guid.NewGuid():N}" : effectId;
            ShieldEffect shield = new ShieldEffect(id, displayName, duration, amount, source);
            ApplyEffect(shield);
        }

        public virtual void ClearShield()
        {
            for (int i = m_ActiveEffects.Count - 1; i >= 0; i--)
            {
                if (m_ActiveEffects[i] is ShieldEffect)
                {
                    StatusEffect effect = m_ActiveEffects[i];
                    m_ActiveEffects.RemoveAt(i);
                    effect.OnRemoved(this);
                    OnEffectRemovedEvent?.Invoke(effect);
                }
            }
            OnShieldChangedEvent?.Invoke(0f);
        }

        /// <summary>
        /// Xu ly hap thu sat thuong bang he thong cac lop khien (ShieldEffect).
        /// Uu tien Effect co thoi gian con lai it nhat (RemainingTime nho nhat).
        /// Gia tri khien xu ly luon <= finalDamage.
        /// Tra ve tong gia tri khien da xu ly de giam tru vao finalDamage.
        /// </summary>
        public virtual float ProcessShieldAbsorption(float finalDamage)
        {
            if (finalDamage <= 0f) return 0f;

            // Thu thap cac ShieldEffect con hieu luc
            List<ShieldEffect> activeShields = new List<ShieldEffect>();
            for (int i = 0; i < m_ActiveEffects.Count; i++)
            {
                if (m_ActiveEffects[i] is ShieldEffect shield && !shield.IsExpired && shield.RemainingShield > 0f)
                {
                    activeShields.Add(shield);
                }
            }

            if (activeShields.Count == 0) return 0f;

            // Sap xep tang dan theo RemainingTime (uu tien effect het han truoc)
            activeShields.Sort((a, b) => a.RemainingTime.CompareTo(b.RemainingTime));

            float damageNeedToAbsorb = finalDamage;
            float totalShieldProcessed = 0f;

            for (int i = 0; i < activeShields.Count; i++)
            {
                if (damageNeedToAbsorb <= 0f) break;

                ShieldEffect currentShieldEffect = activeShields[i];
                float absorbed = currentShieldEffect.Absorb(damageNeedToAbsorb);

                totalShieldProcessed += absorbed;
                damageNeedToAbsorb -= absorbed;
            }

            // Don dep cac ShieldEffect da bi pha huy hoan toan (RemainingShield <= 0)
            for (int i = m_ActiveEffects.Count - 1; i >= 0; i--)
            {
                if (m_ActiveEffects[i] is ShieldEffect s && (s.IsExpired || s.RemainingShield <= 0f))
                {
                    m_ActiveEffects.RemoveAt(i);
                    s.OnRemoved(this);
                    OnEffectRemovedEvent?.Invoke(s);
                }
            }

            if (totalShieldProcessed > 0f)
            {
                OnShieldChangedEvent?.Invoke(CurrentShield);
            }

            return totalShieldProcessed;
        }

        #endregion

        #region Input commands

        public void SetMoveInput(Vector2 input)
        {
            m_MoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void SetLookTarget(Vector2 worldPoint)
        {
            m_LookTarget = worldPoint;
            m_HasLookTarget = true;
        }

        public virtual void ClearLookTarget()
        {
            m_HasLookTarget = false;
            view?.StopLook();
        }

        #endregion

        #region Skills & Energy

        /// <summary>
        /// Dung 1 trong 4 ky nang theo GDD:
        /// - index 0 (Ky nang thuong): khong ton nang luong, hoi nang luong cho xe, CD ngan.
        /// - index 1 (Ky nang chuyen biet): tieu hao nang luong.
        /// - index 2 (Ky nang tuy chon 1): tieu hao nang luong.
        /// - index 3 (Ky nang tuy chon 2): tieu hao nang luong.
        /// </summary>
        public void UseSkill(int index)
        {
            if (index < 0 || index >= SkillCount) return;
            if (!CanUseSkill) return;
            if (skillCooldowns[index] > 0f) return;

            if (index == 0)
            {
                // Ky nang thuong: hoi nang luong
                GainEnergy(GetSkillEnergyGain(index));
            }
            else
            {
                // Ky nang tieu hao nang luong: kiem tra du nang luong moi thi trien
                float energyCost = GetSkillEnergyCost(index);
                if (currentEnergy < energyCost)
                {
                    return;
                }
                SpendEnergy(energyCost);
            }

            skillCooldowns[index] = GetCooldown(index);

            OnSkillUsedEvent?.Invoke(index);
            m_EquippedAccessory?.OnOwnerUsedSkill(index);

            OnUseSkill(index);
        }

        protected virtual void OnUseSkill(int index)
        {
            Debug.Log($"[TankBrain] Level {level} su dung ky nang {index + 1}/{SkillCount}.");
            TextDamageManager.Instance?.Show(36, this.view.Muzzle.position);
        }

        protected virtual float GetSkillEnergyCost(int index)
        {
            if (index == 0) return 0f;
            return 30f; // Mac dinh cac skill 1, 2, 3 ton 30 energy (lop con ghi de theo GDD)
        }

        protected virtual float GetSkillEnergyGain(int index)
        {
            if (index == 0) return 10f; // Mac dinh hoi 10 energy cho ky nang thuong
            return 0f;
        }

        #endregion

        #region Combat & Damage

        /// <summary>
        /// Nhan sat thuong qua DamageInfo, ap dung cong thuc Phong thu va Xuyen giap theo GDD.
        /// </summary>
        public virtual void TakeDamage(DamageInfo info)
        {
            if (!IsAlive) return;

            // Tinh sat thuong thuc te bang CombatUtility
            float finalDamage = CombatUtility.ResolveDamage(info.RawDamage, m_FinalStats.Defense, info.ArmorPenetration);
            info.FinalDamage = finalDamage;

            if (info.IsDirectToHealth)
            {
                currentHealth -= finalDamage;
            }
            else
            {
                // Xu ly hap thu bang he thong cac lop khien: Uu tien effect co thoi gian con lai it nhat
                float absorbedShield = ProcessShieldAbsorption(finalDamage);
                float damageToHealth = finalDamage - absorbedShield;

                currentHealth -= damageToHealth;
            }

            currentHealth = Mathf.Max(0f, currentHealth);

            OnTakeDamageEvent?.Invoke(info);
            m_EquippedAccessory?.OnOwnerTookDamage(info);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>Overload de tuong thich code cu.</summary>
        public virtual void TakeDamage(float amount)
        {
            TakeDamage(new DamageInfo(null, this, amount));
        }

        /// <summary>Thong bao xe da gay sat thuong (de trigger event va phu kien).</summary>
        public virtual void NotifyDealtDamage(DamageInfo info)
        {
            OnDealDamageEvent?.Invoke(info);
            m_EquippedAccessory?.OnOwnerDealtDamage(info);
        }
        public virtual void Heal(float amount, bool isByShare = false)
        {
            if (amount <= 0f) return;
        }
        public virtual void Heal(float amount)
        {
            if (!IsAlive) return;
            currentHealth = Mathf.Min(currentHealth + amount, m_FinalStats.MaxHealth);
            OnHealEvent?.Invoke(amount);
        }
        public virtual void GainEnergy(float amount)
        {
            currentEnergy = Mathf.Min(currentEnergy + amount, m_FinalStats.MaxEnergy);
        }

        public virtual void SpendEnergy(float amount)
        {
            currentEnergy = Mathf.Max(currentEnergy - amount, 0f);
        }

        protected virtual void Die()
        {
            OnDeathEvent?.Invoke();
        }

        #endregion

        #region Lifecycle & Physics

        private void Awake()
        {
            m_Body = GetComponent<Rigidbody2D>();
            if (m_Body == null) m_Body = GetComponentInChildren<Rigidbody2D>();

            if (baseStats == null) baseStats = new TankStats();

            RecalculateStats();

            if (currentHealth <= 0f) currentHealth = m_FinalStats.MaxHealth;
            if (currentEnergy <= 0f) currentEnergy = m_FinalStats.MaxEnergy;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            TickCooldowns(dt);
            TickCrowdControl(dt);
            TickStatusEffects(dt);

            m_EquippedAccessory?.OnBrainTick(dt);
        }

        private void FixedUpdate()
        {
            float speed = m_FinalStats.MoveSpeed;

            // Giam toc do neu bi Slow
            if (m_ActiveCC.TryGetValue(CrowdControlType.Slow, out var slowCC))
            {
                speed *= Mathf.Clamp01(1f - slowCC.Strength);
            }

            if (m_Body != null)
            {
                if (CanMove)
                {
                    m_Body.linearVelocity = m_MoveInput * speed;
                }
                else
                {
                    m_Body.linearVelocity = Vector2.zero;
                }
            }

            if (view == null) return;

            // --- Action Move ---
            if (CanMove && m_MoveInput.sqrMagnitude > 0.0001f)
            {
                view.Move(m_MoveInput.normalized);
            }
            else
            {
                view.Stop();
            }

            // --- Action Look ---
            if (CanLook && m_HasLookTarget)
            {
                view.Look(m_LookTarget);
            }
        }

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
            return baseCooldown * (1f - Mathf.Clamp(m_FinalStats.CooldownReduction, 0f, TankStats.MaxCooldownReduction));
        }

        protected virtual float GetBaseCooldown(int index)
        {
            return 5f;
        }

        protected float GetCooldown(int index)
        {
            return GetCooldownWithReduction(GetBaseCooldown(index));
        }

        #endregion
    }
}
