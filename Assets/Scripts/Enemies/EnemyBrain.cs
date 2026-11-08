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
            IronWasteland.Tanks.GreenFox.GreenFox_FoxTerritory.ReportDamage(this, ResolveAttacker(info));
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

        #region Attacker inference (cho nguồn sát thương không khai báo Attacker)

        private Collider2D[] m_OwnColliders;
        private readonly System.Collections.Generic.List<Collider2D> m_OverlapBuffer = new System.Collections.Generic.List<Collider2D>(16);

        // Bật log để xem đòn nào không suy ra được người đánh. Đặt false khi xong.
        private const bool LogAttackerInference = true;

        /// <summary>
        /// Trả về người gây sát thương. Nếu nguồn không khai báo (Attacker = null) thì suy ra xe tăng chủ
        /// từ các collider đang chạm kẻ địch này (đạn/vật thể có OwnerRef trỏ tới TankBrain).
        /// </summary>
        private UnityEngine.Object ResolveAttacker(DamageInfo info)
        {
            if (info.Attacker != null) return info.Attacker;

            // Cách 1: xem hàm nào của xe tăng nào đã gọi TakeDamage (mỗi xe có thể có DealDamage riêng).
            var fromStack = InferTankFromCallStack(out string stackHow);
            if (fromStack != null)
            {
                if (LogAttackerInference) Debug.Log($"[Dmg] Attacker=NULL -> suy ra xe '{fromStack.name}' (qua {stackHow})", this);
                return fromStack;
            }

            // Cách 2: suy ra từ các collider đang chạm kẻ địch (đạn/skill có OwnerRef).
            if (m_OwnColliders == null || m_OwnColliders.Length == 0)
                m_OwnColliders = GetComponentsInChildren<Collider2D>();

            ContactFilter2D filter = ContactFilter2D.noFilter;
            filter.useTriggers = true;

            System.Text.StringBuilder names = LogAttackerInference ? new System.Text.StringBuilder() : null;

            for (int i = 0; i < m_OwnColliders.Length; i++)
            {
                Collider2D own = m_OwnColliders[i];
                if (own == null || !own.enabled) continue;

                m_OverlapBuffer.Clear();
                own.Overlap(filter, m_OverlapBuffer);

                for (int j = 0; j < m_OverlapBuffer.Count; j++)
                {
                    Collider2D other = m_OverlapBuffer[j];
                    if (other == null) continue;
                    if (names != null) names.Append(other.name).Append(", ");

                    // Bỏ qua kẻ địch khác và chính vòng Fox Territory (không phải nguồn sát thương của xe tăng).
                    if (other.GetComponentInParent<EnemyBrain>() != null) continue;
                    if (other.GetComponentInParent<IronWasteland.Tanks.GreenFox.GreenFox_FoxTerritory>() != null) continue;

                    string how = "OwnerRef/TankBrain";
                    IronWasteland.OwnerRef ownerRef = other.GetComponentInParent<IronWasteland.OwnerRef>();
                    IronWasteland.Tanks.TankBrain tank = ownerRef != null ? ownerRef.GetOwner<IronWasteland.Tanks.TankBrain>() : null;
                    if (tank == null) tank = other.GetComponentInParent<IronWasteland.Tanks.TankBrain>();

                    // OwnerRef có Owner nhưng không phải TankBrain (vd GameObject / prefab asset của xe): ánh xạ sang xe đang chạy.
                    if (tank == null && ownerRef != null) tank = ResolveTankFromOwnerObject(ownerRef.Owner, out how);

                    // Skill/đạn không có OwnerRef: dò field nào trong script của nó đang giữ TankBrain (owner, caster...).
                    if (tank == null) tank = FindTankReference(other.gameObject, out how);

                    if (tank != null)
                    {
                        if (LogAttackerInference) Debug.Log($"[Dmg] Attacker=NULL -> suy ra xe '{tank.name}' từ collider '{other.name}' (qua {how})", this);
                        return tank;
                    }
                }
            }

            if (LogAttackerInference)
                Debug.Log($"[Dmg] Attacker=NULL, KHÔNG suy ra được xe tăng. Collider đang chạm kẻ địch: [{(names != null ? names.ToString() : "")}]", this);

            return null;
        }

        /// <summary>
        /// Duyệt call stack tìm hàm của một lớp xe tăng (TankBrain_X) đã gọi TakeDamage, rồi ánh xạ sang xe đang chạy
        /// thuộc đúng lớp đó. Chỉ trả về khi tìm được DUY NHẤT một xe (nhiều xe cùng loại thì không đoán, trả về null).
        /// Lưu ý: có thể không hoạt động trong bản build IL2CPP đã strip; khi đó rơi về cách suy ra khác.
        /// </summary>
        private static IronWasteland.Tanks.TankBrain InferTankFromCallStack(out string how)
        {
            how = null;
            try
            {
                var stack = new System.Diagnostics.StackTrace(false);
                int frames = Mathf.Min(stack.FrameCount, 12);
                for (int i = 0; i < frames; i++)
                {
                    var method = stack.GetFrame(i)?.GetMethod();
                    System.Type type = method != null ? method.DeclaringType : null;
                    if (type == null) continue;
                    if (typeof(EnemyBrain).IsAssignableFrom(type)) continue;
                    if (!typeof(IronWasteland.Tanks.TankBrain).IsAssignableFrom(type)) continue;

                    IronWasteland.Tanks.TankBrain found = null;
                    int count = 0;
                    var live = GetLiveTanks();
                    for (int k = 0; k < live.Length; k++)
                    {
                        var t = live[k];
                        if (t == null || !t.isActiveAndEnabled) continue;
                        if (!type.IsAssignableFrom(t.GetType())) continue;
                        count++;
                        found = t;
                    }

                    // Lớp cha chung (vd TankBrain.DealDamage) khớp nhiều xe -> đi tiếp lên frame trên.
                    if (count == 1)
                    {
                        how = $"call stack {type.Name}.{method.Name}";
                        return found;
                    }
                }
            }
            catch (System.Exception) { }

            return null;
        }

        private static IronWasteland.Tanks.TankBrain[] s_LiveTanks;
        private static int s_LiveTanksFrame = -1;

        private static IronWasteland.Tanks.TankBrain[] GetLiveTanks()
        {
            if (s_LiveTanks == null || s_LiveTanksFrame != Time.frameCount)
            {
                s_LiveTanks = UnityEngine.Object.FindObjectsByType<IronWasteland.Tanks.TankBrain>();
                s_LiveTanksFrame = Time.frameCount;
            }
            return s_LiveTanks;
        }

        /// <summary>
        /// Owner có thể là TankBrain, hoặc GameObject/Component của xe. Nếu là object trong Scene thì dùng luôn;
        /// nếu là prefab asset (không thuộc Scene) thì tìm xe đang chạy được tạo từ prefab đó (cùng tên "X(Clone)" hoặc cùng kiểu).
        /// </summary>
        private static IronWasteland.Tanks.TankBrain ResolveTankFromOwnerObject(UnityEngine.Object owner, out string how)
        {
            how = null;
            if (owner == null) return null;

            if (owner is IronWasteland.Tanks.TankBrain direct) { how = "Owner là TankBrain"; return direct; }

            GameObject go = owner as GameObject;
            if (go == null && owner is Component comp) go = comp.gameObject;
            if (go == null) return null;

            // Object thật trong Scene: lấy TankBrain trên nó.
            if (go.scene.IsValid())
            {
                var t = go.GetComponentInParent<IronWasteland.Tanks.TankBrain>();
                if (t != null) { how = "Owner (GameObject trong Scene)"; return t; }
            }

            // Prefab asset: tìm xe đang chạy tương ứng.
            IronWasteland.Tanks.TankBrain prefabTank = go.GetComponent<IronWasteland.Tanks.TankBrain>();
            string prefabName = go.name;
            var live = GetLiveTanks();

            for (int i = 0; i < live.Length; i++)
            {
                var t = live[i];
                if (t == null) continue;
                if (t.name == prefabName + "(Clone)" || t.name == prefabName)
                {
                    how = $"Owner là prefab '{prefabName}' -> xe đang chạy cùng tên";
                    return t;
                }
            }

            if (prefabTank != null)
            {
                for (int i = 0; i < live.Length; i++)
                {
                    var t = live[i];
                    if (t != null && t.GetType() == prefabTank.GetType())
                    {
                        how = $"Owner là prefab '{prefabName}' -> xe đang chạy cùng kiểu";
                        return t;
                    }
                }
            }

            return null;
        }

        private static readonly System.Collections.Generic.Dictionary<System.Type, System.Reflection.FieldInfo[]> s_TankFieldCache
            = new System.Collections.Generic.Dictionary<System.Type, System.Reflection.FieldInfo[]>();

        /// <summary>Danh sách field của type có thể đang giữ một TankBrain (kể cả field private / ở lớp cha).</summary>
        private static System.Reflection.FieldInfo[] GetTankFields(System.Type type)
        {
            if (s_TankFieldCache.TryGetValue(type, out var cached)) return cached;

            var result = new System.Collections.Generic.List<System.Reflection.FieldInfo>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.DeclaredOnly;

            for (System.Type t = type; t != null && t != typeof(MonoBehaviour) && t != typeof(object); t = t.BaseType)
            {
                foreach (var f in t.GetFields(flags))
                {
                    System.Type ft = f.FieldType;
                    bool holdsTank = typeof(IronWasteland.Tanks.TankBrain).IsAssignableFrom(ft)   // field kiểu TankBrain (hoặc lớp con)
                                  || ft == typeof(UnityEngine.Object) || ft == typeof(Component) || ft == typeof(MonoBehaviour);
                    if (holdsTank) result.Add(f);
                }
            }

            var arr = result.ToArray();
            s_TankFieldCache[type] = arr;
            return arr;
        }

        private static IronWasteland.Tanks.TankBrain FindTankReference(GameObject go, out string how)
        {
            how = null;
            MonoBehaviour[] comps = go.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < comps.Length; i++)
            {
                MonoBehaviour c = comps[i];
                if (c == null || c is EnemyBrain || c is IronWasteland.Tanks.TankBrain) continue;

                foreach (var f in GetTankFields(c.GetType()))
                {
                    var tank = f.GetValue(c) as IronWasteland.Tanks.TankBrain;
                    if (tank != null)
                    {
                        how = $"field '{f.Name}' của {c.GetType().Name}";
                        return tank;
                    }
                }
            }
            return null;
        }

        #endregion

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