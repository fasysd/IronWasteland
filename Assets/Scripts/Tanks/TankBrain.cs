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

        // Cơ chế gánh hộ sát thương (Guardian Current, Guardian Wall, Earth Guardian...)
        private readonly List<IDamageRedirector> m_DamageRedirectors = new List<IDamageRedirector>();

        // Bản sao tạm dùng khi duyệt redirector, tránh lỗi sửa collection ngay trong lúc duyệt
        private readonly List<IDamageRedirector> m_RedirectorBuffer = new List<IDamageRedirector>();

        // Bộ đệm tạm dùng khi duyệt các hiệu ứng chặn đòn, tránh cấp phát mới ở mỗi đòn đánh
        private readonly List<IDamageInterceptor> m_InterceptorBuffer = new List<IDamageInterceptor>();

        protected bool _initialized = false;
        private TankView viewPrefab;

        #region Events
        public event Action<DamageInfo> OnTakeDamageEvent;
        public event Action<DamageInfo> OnDealDamageEvent;
        public event Action<float> OnHealEvent;
        public event Action<int> OnSkillUsedEvent;
        public event Action<float> OnShieldChangedEvent;

        /// <summary>
        /// Phát khi một lớp khiên vừa hấp thụ sát thương: (lớp khiên, lượng hấp thụ, đòn có bị chặn sạch hay không).
        /// Phát SAU khi việc dọn dẹp hiệu ứng đã xong, nên listener có thể nhận được lớp khiên đã cạn
        /// (<see cref="ShieldEffect.IsExpired"/> = true, <see cref="ShieldEffect.RemainingShield"/> = 0).
        /// </summary>
        public event Action<ShieldEffect, float, bool> OnShieldAbsorbedEvent;
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
        /// Thêm một lớp khiên mới qua <see cref="ShieldEffect"/> với nguồn gốc (Source) và thời gian tồn tại xác định.
        /// </summary>
        /// <param name="maxAbsorbHits">Số đòn tối đa lớp khiên này chịu được; mặc định không giới hạn.</param>
        /// <returns>
        /// Lớp khiên đang thực sự hoạt động trên Brain. Nếu trùng <paramref name="effectId"/> với một lớp khiên đã có,
        /// hiệu ứng cũ được làm mới và chính nó được trả về (instance mới không được giữ lại).
        /// </returns>
        public virtual ShieldEffect AddShield(float amount, float duration, string effectId = null, UnityEngine.Object source = null, string displayName = "Khiên", int maxAbsorbHits = int.MaxValue)
        {
            if (amount <= 0f) return null;
            string id = string.IsNullOrEmpty(effectId) ? $"Shield_{Guid.NewGuid():N}" : effectId;
            ShieldEffect shield = new ShieldEffect(id, displayName, duration, amount, source, maxAbsorbHits);
            ApplyEffect(shield);

            // ApplyEffect có thể cộng dồn vào lớp khiên cùng EffectId đã tồn tại → trả về lớp đang hoạt động thật.
            return GetEffect(id) as ShieldEffect ?? shield;
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

            // Chỉ cấp phát danh sách khi thật sự có listener, tránh rác GC ở mỗi đòn đánh.
            List<ShieldEffect> absorbedShields = OnShieldAbsorbedEvent != null ? new List<ShieldEffect>() : null;
            List<float> absorbedAmounts = OnShieldAbsorbedEvent != null ? new List<float>() : null;

            for (int i = 0; i < activeShields.Count; i++)
            {
                if (damageNeedToAbsorb <= 0f) break;

                ShieldEffect currentShieldEffect = activeShields[i];
                float absorbed = currentShieldEffect.Absorb(damageNeedToAbsorb);

                totalShieldProcessed += absorbed;
                damageNeedToAbsorb -= absorbed;

                if (absorbed > 0f && absorbedShields != null)
                {
                    absorbedShields.Add(currentShieldEffect);
                    absorbedAmounts.Add(absorbed);
                }
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

            // Thông báo cho từng lớp khiên biết mình vừa hấp thụ bao nhiêu, sau khi đã dọn dẹp xong.
            if (absorbedShields != null)
            {
                bool fullyBlocked = damageNeedToAbsorb <= 0f;
                for (int i = 0; i < absorbedShields.Count; i++)
                {
                    OnShieldAbsorbedEvent?.Invoke(absorbedShields[i], absorbedAmounts[i], fullyBlocked);
                }
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

        #region Damage Redirection

        /// <summary>
        /// Đăng ký một cơ chế gánh hộ sát thương (ví dụ Guardian Current của Leviathan chia một phần đòn đánh sang đồng minh gánh thay).
        /// </summary>
        public virtual void AddDamageRedirector(IDamageRedirector redirector)
        {
            if (redirector == null) return;
            if (m_DamageRedirectors.Contains(redirector)) return;
            m_DamageRedirectors.Add(redirector);
        }

        /// <summary>Gỡ đăng ký cơ chế gánh hộ sát thương.</summary>
        public virtual void RemoveDamageRedirector(IDamageRedirector redirector)
        {
            if (redirector == null) return;
            m_DamageRedirectors.Remove(redirector);
        }

        /// <summary>
        /// Chặng chuyển hướng sát thương: cho phép các cơ chế gánh hộ lấy đi một phần
        /// <see cref="DamageInfo.RawDamage"/> TRƯỚC khi tính giáp và TRƯỚC khi trừ khiên.
        /// <para>
        /// Vì sao phải đứng trước bước tính giáp: phần gánh hộ được chia trên sát thương gốc, sau đó mỗi bên
        /// tự tính giáp của mình đúng một lần. Nếu đặt sau bước hấp thụ khiên, mục tiêu đang có khiên sẽ làm
        /// phần sát thương còn lại về 0 và cơ chế gánh hộ mất tác dụng.
        /// </para>
        /// <para>
        /// Phần sát thương gánh hộ được tạo thành <see cref="DamageInfo"/> mới mang cờ
        /// <see cref="DamageInfo.IsDirectToHealth"/> = true (đi thẳng vào máu bên gánh, bỏ qua khiên của họ)
        /// và <see cref="DamageInfo.IsRedirected"/> = true. Đòn đã mang cờ chuyển hướng sẽ KHÔNG bị
        /// chuyển hướng thêm lần nữa, nhờ đó chặn đệ quy vô hạn.
        /// </para>
        /// <para>
        /// Sát thương theo thời gian (<see cref="DamageInfo.IsDamageOverTime"/>) không bị chuyển hướng.
        /// </para>
        /// </summary>
        protected virtual DamageInfo ProcessDamageRedirection(DamageInfo info)
        {
            if (m_DamageRedirectors.Count == 0) return info;
            if (info.IsRedirected) return info;
            if (info.IsDamageOverTime) return info;
            if (info.RawDamage <= 0f) return info;

            // Duyệt trên bản sao: bên gánh có thể chết và gỡ đăng ký ngay trong lúc ta đang duyệt.
            m_RedirectorBuffer.Clear();
            m_RedirectorBuffer.AddRange(m_DamageRedirectors);

            for (int i = 0; i < m_RedirectorBuffer.Count; i++)
            {
                IDamageRedirector redirector = m_RedirectorBuffer[i];
                if (redirector == null) continue;
                if (!redirector.TryRedirect(info, out TankBrain protector, out float sharedAmount)) continue;
                if (protector == null || protector == this) continue;

                sharedAmount = Mathf.Min(sharedAmount, info.RawDamage);
                if (sharedAmount <= 0f) continue;

                info.RawDamage -= sharedAmount;

                DamageInfo redirected = new DamageInfo(
                    info.Attacker,
                    protector,
                    sharedAmount,
                    info.ArmorPenetration,
                    info.HitPosition,
                    isDirectToHealth: true,
                    isRedirected: true);

                protector.TakeDamage(redirected);

                if (info.RawDamage <= 0f) break;
            }

            return info;
        }

        #endregion

        #region Damage Interception

        /// <summary>
        /// Chặng chặn đòn: hỏi lần lượt các hiệu ứng có khả năng chặn sạch một đòn
        /// (né đòn, đỡ đòn, bất tử tạm thời — xem <see cref="IDamageInterceptor"/>).
        /// <para>
        /// Chạy SAU chặng chuyển hướng gánh hộ và TRƯỚC bước tính giáp. Đặt trước giáp để việc "chặn"
        /// không phụ thuộc vào con số giáp của người nhận: chặn là chặn sạch, không phải hấp thụ một phần.
        /// Tại thời điểm này <see cref="DamageInfo.FinalDamage"/> vẫn bằng <see cref="DamageInfo.RawDamage"/>.
        /// </para>
        /// <para>
        /// Trả về true nghĩa là đòn đã bị chặn sạch: Brain dừng xử lý ngay, không trừ máu, không trừ khiên
        /// và không phát <see cref="OnTakeDamageEvent"/> (vì đây không phải là một lần nhận sát thương).
        /// Hiệu ứng chặn tự phát sự kiện riêng của nó để bật hiệu ứng hình ảnh.
        /// </para>
        /// <para>
        /// Thứ tự ưu tiên: hiệu ứng có thời gian còn lại ít nhất được hỏi trước (cùng nguyên tắc với
        /// hệ khiên), để lượt chặn của hiệu ứng sắp hết hạn được dùng trước thay vì bị lãng phí.
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
        /// Nhận sát thương qua <see cref="DamageInfo"/>, áp dụng công thức Phòng thủ và Xuyên giáp theo GDD.
        /// Thứ tự xử lý: chuyển hướng gánh hộ → chặn đòn → tính giáp → hấp thụ khiên → trừ máu.
        /// </summary>
        public virtual void TakeDamage(DamageInfo info)
        {
            if (!IsAlive) return;

            // [1] Chặng chuyển hướng (gánh hộ) — phải chạy trước bước tính giáp, xem ProcessDamageRedirection.
            float rawDamageBeforeRedirect = info.RawDamage;
            info = ProcessDamageRedirection(info);

            // Đòn đã được gánh hộ toàn bộ thì không còn gì để xử lý tiếp (không phát event sát thương).
            if (rawDamageBeforeRedirect > 0f && info.RawDamage <= 0f) return;

            // [2] Chặng chặn đòn (né/đỡ đòn/bất tử tạm thời) — chặn sạch trước khi tính giáp.
            if (ProcessDamageInterception(info)) return;

            // [3] Tinh sat thuong thuc te bang CombatUtility
            float finalDamage = CombatUtility.ResolveDamage(info.RawDamage, m_FinalStats.Defense, info.ArmorPenetration);
            info.FinalDamage = finalDamage;

            if (info.IsDirectToHealth)
            {
                info.AbsorbedByShield = 0f;
                currentHealth -= finalDamage;
            }
            else
            {
                // [4] Xu ly hap thu bang he thong cac lop khien: Uu tien effect co thoi gian con lai it nhat
                float absorbedShield = ProcessShieldAbsorption(finalDamage);
                info.AbsorbedByShield = absorbedShield;
                float damageToHealth = finalDamage - absorbedShield;

                // [5] Tru mau
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
