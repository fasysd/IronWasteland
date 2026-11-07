using System;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Core.Effects;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Lop cha (abstract) cua moi ke dich. Day la noi chua LOGIC + RUNTIME STATE:
    /// - Bang chi so (EnemyStats)
    /// - Mau hien tai, giam tru sat thuong theo CombatUtility
    /// - Quan ly Khong che (CC) va Hieu ung (Status Effects)
    /// - Co che Co-op (x2 HP)
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class EnemyBrain : MonoBehaviour
    {
        [Header("Model")]
        [Tooltip("Model con (visual + collider). Tu tim trong hierarchy neu de trong.")]
        [SerializeField] private EnemyModel model;

        [Header("Info")]
        [Tooltip("Ten hien thi cho UI / Debug.")]
        [SerializeField] private string displayName;

        [Header("Stats")]
        [Tooltip("Chi so cua ke dich (Attack / MaxHealth / Defense / MoveSpeed).")]
        [SerializeField] private EnemyStats stats = new EnemyStats();

        [Header("Runtime")]
        [SerializeField] private float currentHealth;

        private bool m_Initialized;
        private bool m_Dead;

        // Crowd Control
        private readonly Dictionary<CrowdControlType, ActiveCrowdControl> m_ActiveCC = new Dictionary<CrowdControlType, ActiveCrowdControl>();
        private readonly List<CrowdControlType> m_CCToRemove = new List<CrowdControlType>();

        // Status Effects
        private readonly List<StatusEffect> m_ActiveEffects = new List<StatusEffect>();

        // Bộ đệm tạm dùng khi duyệt các hiệu ứng chặn đòn, tránh cấp phát mới ở mỗi đòn đánh
        private readonly List<IDamageInterceptor> m_InterceptorBuffer = new List<IDamageInterceptor>();

        #region Events
        public event Action<DamageInfo> OnTakeDamageEvent;
        public event Action<DamageInfo> OnDealDamageEvent;
        public event Action<float> OnHealEvent;
        public event Action<float> OnShieldChangedEvent;
        public event Action<CrowdControlType> OnCrowdControlApplied;
        public event Action<CrowdControlType> OnCrowdControlRemoved;
        public event Action<StatusEffect> OnEffectAppliedEvent;
        public event Action<StatusEffect> OnEffectRemovedEvent;
        public event Action OnDeathEvent;
        #endregion

        #region Properties
        public EnemyModel Model => model;
        public string DisplayName => displayName;
        public EnemyStats Stats => stats;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => stats != null ? stats.MaxHealth : 0f;
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
        public bool IsAlive => !m_Dead && currentHealth > 0f;

        public bool IsCrowdControlImmune { get; set; }
        public bool CanMove => IsAlive && !HasCC(CrowdControlType.Stun) && !HasCC(CrowdControlType.Root);
        public bool CanLook => IsAlive && !HasCC(CrowdControlType.Stun);
        public bool CanUseSkill => IsAlive && !HasCC(CrowdControlType.Stun) && !HasCC(CrowdControlType.Silence);

        public IReadOnlyList<StatusEffect> ActiveEffects => m_ActiveEffects;
        #endregion

        #region Setup

        public virtual void Initialize(EnemyStats overrideStats = null)
        {
            if (overrideStats != null) stats = overrideStats;
            if (stats == null) stats = new EnemyStats();

            currentHealth = stats.MaxHealth;
            m_Dead = false;
            m_Initialized = true;

            if (model == null) model = GetComponentInChildren<EnemyModel>();
            model?.RegisterOwner(this);
            model?.PlaySpawn();
        }

        /// <summary>
        /// Ap dung che do Co-op theo GDD (nhan doi mau toan bo ke dich).
        /// </summary>
        public virtual void SetCoopMode(bool isCoop)
        {
            if (!isCoop || stats == null) return;
            stats.MaxHealth *= 2f;
            currentHealth = stats.MaxHealth;
        }

        #endregion

        #region Damage Interception

        /// <summary>
        /// Chặng chặn đòn: hỏi lần lượt các hiệu ứng có khả năng chặn sạch một đòn
        /// (né đòn, đỡ đòn, bất tử tạm thời — xem <see cref="IDamageInterceptor"/>).
        /// <para>
        /// Chạy TRƯỚC bước tính giáp, để việc "chặn" không phụ thuộc vào con số giáp của đối tượng.
        /// Trả về true nghĩa là đòn bị chặn sạch: không trừ máu, không trừ khiên, không phát sự kiện sát thương.
        /// </para>
        /// <para>
        /// Thứ tự ưu tiên: hiệu ứng có thời gian còn lại ít nhất được hỏi trước (cùng nguyên tắc với
        /// hệ khiên), tránh lãng phí lượt chặn của hiệu ứng sắp hết hạn.
        /// </para>
        /// </summary>
        protected virtual bool ProcessDamageInterception(DamageInfo info)
        {
            if (info.RawDamage <= 0f) return false;

            m_InterceptorBuffer.Clear();
            for (int i = 0; i < m_ActiveEffects.Count; i++)
            {
                StatusEffect effect = m_ActiveEffects[i];
                if (effect.IsExpired) continue;
                if (effect is IDamageInterceptor interceptor)
                {
                    m_InterceptorBuffer.Add(interceptor);
                }
            }

            if (m_InterceptorBuffer.Count == 0) return false;

            // Ưu tiên hiệu ứng sắp hết hạn trước.
            m_InterceptorBuffer.Sort((a, b) =>
                ((StatusEffect)a).RemainingTime.CompareTo(((StatusEffect)b).RemainingTime));

            for (int i = 0; i < m_InterceptorBuffer.Count; i++)
            {
                IDamageInterceptor interceptor = m_InterceptorBuffer[i];
                if (interceptor is StatusEffect e && e.IsExpired) continue;
                if (interceptor.TryIntercept(info)) return true;
            }

            return false;
        }

        #endregion

        #region Combat & Damage

        /// <summary>
        /// Nhan sat thuong qua DamageInfo va ap dung cong thuc Phong thu / Xuyen giap va He thong Khiên.
        /// Thứ tự xử lý: chặn đòn → tính giáp → hấp thụ khiên → trừ máu.
        /// </summary>
        public virtual void TakeDamage(DamageInfo info)
        {
            if (!IsAlive) return;

            // [1] Chặng chặn đòn (né/đỡ đòn/bất tử tạm thời) — chặn sạch trước khi tính giáp.
            if (ProcessDamageInterception(info)) return;

            // [2] Tinh sat thuong thuc te bang CombatUtility
            float finalDamage = CombatUtility.ResolveDamage(info.RawDamage, stats.Defense, info.ArmorPenetration);
            info.FinalDamage = finalDamage;

            if (info.IsDirectToHealth)
            {
                currentHealth -= finalDamage;
            }
            else
            {
                // [3] Xu ly hap thu bang he thong cac lop khien
                float absorbedShield = ProcessShieldAbsorption(finalDamage);
                float damageToHealth = finalDamage - absorbedShield;

                // [4] Tru mau
                currentHealth -= damageToHealth;
            }

            model?.PlayHit();

            OnDamaged(finalDamage);
            OnTakeDamageEvent?.Invoke(info);
            IronWasteland.Tanks.GreenFox.GreenFox_FoxTerritory.ReportDamage(this, info.Attacker);
            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                Die();
            }
        }

        /// <summary>Overload ho tro code cu.</summary>
        public virtual void TakeDamage(float amount)
        {
            TakeDamage(new DamageInfo(null, this, amount));

        }

        public virtual void NotifyDealtDamage(DamageInfo info)
        {
            OnDealDamageEvent?.Invoke(info);
        }

        public virtual void Heal(float amount)
        {
            if (!IsAlive) return;
            currentHealth = Mathf.Min(currentHealth + amount, MaxHealth);
            OnHealEvent?.Invoke(amount);
        }

        protected virtual void Die()
        {
            if (m_Dead) return;
            m_Dead = true;

            model?.PlayDeath();
            OnDeath();
            OnDeathEvent?.Invoke();
        }

        #endregion

        #region Crowd Control (CC)

        public bool HasCC(CrowdControlType type) => m_ActiveCC.ContainsKey(type);

        public virtual void ApplyCrowdControl(CrowdControlType type, float duration, float strength = 0f)
        {
            if (type == CrowdControlType.None) return;

            if (IsCrowdControlImmune)
            {
                OnCrowdControlResisted(type, strength);
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

        /// <summary>
        /// Hook khi ke dich khang/mien nhiem khong che (vi du: Boss Tinh anh nhan them sat thuong thay vi bi troi).
        /// </summary>
        protected virtual void OnCrowdControlResisted(CrowdControlType type, float strength) { }

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

            OnShieldChangedEvent?.Invoke(CurrentShield);
            return totalShieldProcessed;
        }

        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (model == null) model = GetComponentInChildren<EnemyModel>();
            model?.RegisterOwner(this);

            if (!m_Initialized) Initialize();
        }

        private void OnEnable()
        {
            if (model != null) model.Contact += HandleModelContact;
        }

        private void OnDisable()
        {
            if (model != null) model.Contact -= HandleModelContact;
        }

        private void Update()
        {
            if (!IsAlive) return;

            float dt = Time.deltaTime;
            TickCrowdControl(dt);
            TickStatusEffects(dt);

            OnBrainTick(dt);
        }

        private void HandleModelContact(ColliderOwnerRef other) => OnContact(other);

        #endregion

        #region Hooks - lop con cai dat

        protected abstract void OnBrainTick(float deltaTime);
        protected abstract void OnDeath();
        protected virtual void OnDamaged(float amount) { }
        protected virtual void OnContact(ColliderOwnerRef other) { }

        #endregion

        protected virtual void OnValidate()
        {
            if (model == null) model = GetComponentInChildren<EnemyModel>();
            if (stats == null) stats = new EnemyStats();
            if (string.IsNullOrEmpty(displayName)) displayName = name;
        }

    }
}
