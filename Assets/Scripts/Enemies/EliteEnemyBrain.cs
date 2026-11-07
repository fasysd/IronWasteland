using System;
using System.Collections.Generic;
using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Trang thai cua 1 Mach Nang Luong tren ke dich Tinh anh.
    /// </summary>
    public enum EnergyCoreState
    {
        /// <summary>Mac dinh - chua mo.</summary>
        Closed,

        /// <summary>Dang mo - nguoi choi can pha trong thoi gian cho phep.</summary>
        Open,

        /// <summary>Da pha thanh cong.</summary>
        Broken,
    }

    /// <summary>
    /// 1 Mach Nang Luong tren ke dich Tinh anh.
    /// </summary>
    [Serializable]
    public class EnergyCore
    {
        [Tooltip("Trang thai hien tai cua mach nang luong.")]
        public EnergyCoreState state = EnergyCoreState.Closed;

        public void Reset() => state = EnergyCoreState.Closed;
    }

    /// <summary>
    /// Ke dich TINH ANH theo Tai lieu thiet ke Game (GDD):
    /// - Chi so lon, kho bi tieu diet.
    /// - Co che "Bung no nang luong" (Mach Nang Luong):
    ///   + Mo khi HP giam duoi nguong chi dinh (vi du 50% HP).
    ///   + Nguoi choi can phoi hop pha toan bo mach trong thoi gian cho phep.
    ///   + Pha thanh cong: ke dich bi giam luong lon phong thu trong thoi gian nhat dinh.
    ///   + Khong pha het: ke dich thi trien ky nang gay sat thuong lon va hieu ung bat loi.
    /// - Mien nhiem khong che cung (Root/Stun).
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class EliteEnemyBrain : EnemyBrain
    {
        [Header("Energy Burst (Bung No Nang Luong)")]
        [Tooltip("Nguong mau kich hoat Bung no nang luong (0.5 = khi mau <= 50%).")]
        [SerializeField, Range(0.1f, 0.9f)] private float energyBurstHpThreshold = 0.5f;

        [Tooltip("So luong Mach Nang Luong cua ke dich Tinh anh.")]
        [SerializeField, Min(1)] private int energyCoreCount = 3;

        [Tooltip("Thoi gian cho phep pha mach sau khi mo (giay).")]
        [SerializeField, Min(0f)] private float breakWindowDuration = 10f;

        [Tooltip("Muc giam Phong thu (Defense) khi pha het toan bo mach (0.5 = giam 50% Defense).")]
        [SerializeField, Range(0f, 1f)] private float defenseBreakBonus = 0.5f;

        [Tooltip("Thoi gian giam phong thu sau khi pha het mach (giay).")]
        [SerializeField, Min(1f)] private float defenseBreakDuration = 6f;

        private const float WeakenCooldown = 5f;

        // Runtime
        private readonly List<EnergyCore> m_Cores = new List<EnergyCore>();
        private float m_BreakWindowRemaining;
        private bool m_BreakWindowOpen;
        private float m_WeakenCooldown;
        private bool m_BurstTriggered;

        private float m_DefenseBreakTimer;
        private float m_OriginalDefense;
        private bool m_IsDefenseBroken;

        public IReadOnlyList<EnergyCore> Cores => m_Cores;
        public int BrokenCoreCount { get; private set; }
        public bool AllCoresBroken => energyCoreCount > 0 && BrokenCoreCount >= energyCoreCount;
        public float BreakWindowRemaining => m_BreakWindowRemaining;
        public bool IsBreakWindowOpen => m_BreakWindowOpen;
        public int EnergyCoreCount => energyCoreCount;
        public float BreakWindowDuration => breakWindowDuration;
        public float DefenseBreakBonus => defenseBreakBonus;
        public float DefenseBreakDuration => defenseBreakDuration;

        public override void Initialize(EnemyStats overrideStats = null)
        {
            base.Initialize(overrideStats);
            // Mac dinh Boss Tinh anh mien nhiem khong che cung
            IsCrowdControlImmune = true;
            m_BurstTriggered = false;
            m_IsDefenseBroken = false;
            m_OriginalDefense = Stats != null ? Stats.Defense : 0f;
        }

        #region Energy Core & Burst

        protected override void OnDamaged(float amount)
        {
            base.OnDamaged(amount);

            // Kiem tra nguong mau de tu dong kich hoat Bung no nang luong
            if (!m_BurstTriggered && MaxHealth > 0f && (CurrentHealth / MaxHealth) <= energyBurstHpThreshold)
            {
                m_BurstTriggered = true;
                OpenCores();
            }
        }

        protected virtual void OpenCores()
        {
            EnsureCores();

            for (int i = 0; i < m_Cores.Count; i++)
            {
                if (m_Cores[i].state == EnergyCoreState.Closed)
                {
                    m_Cores[i].state = EnergyCoreState.Open;
                }
            }

            m_BreakWindowOpen = true;
            m_BreakWindowRemaining = breakWindowDuration;

            Debug.Log($"[{DisplayName}] Kich hoat Bung No Nang Luong! Hay pha huy {energyCoreCount} mach trong {breakWindowDuration}s!");
        }

        public virtual bool BreakCore(int index)
        {
            EnsureCores();
            if (index < 0 || index >= m_Cores.Count) return false;

            EnergyCore core = m_Cores[index];
            if (core.state != EnergyCoreState.Open) return false;

            core.state = EnergyCoreState.Broken;
            BrokenCoreCount++;

            if (AllCoresBroken)
            {
                OnAllCoresBroken();
            }

            return true;
        }

        protected virtual void OnAllCoresBroken()
        {
            m_BreakWindowOpen = false;
            m_BreakWindowRemaining = 0f;

            // Giam phong thu cua Boss trong defenseBreakDuration giay
            if (Stats != null && !m_IsDefenseBroken)
            {
                m_OriginalDefense = Stats.Defense;
                Stats.Defense = Mathf.Max(0f, m_OriginalDefense * (1f - defenseBreakBonus));
                m_IsDefenseBroken = true;
                m_DefenseBreakTimer = defenseBreakDuration;

                Debug.Log($"[{DisplayName}] Da pha huy toan bo mach! Phong thu giam {defenseBreakBonus * 100}% trong {defenseBreakDuration}s!");
            }
        }

        protected virtual void OnBreakWindowExpired()
        {
            Debug.LogWarning($"[{DisplayName}] Het thoi gian pha mach! Boss thi trien don danh huy diet!");
            TryCastWeakenSkill();
        }

        private bool TryCastWeakenSkill()
        {
            if (m_WeakenCooldown > 0f) return false;

            CastWeakenSkill();
            m_WeakenCooldown = WeakenCooldown;
            return true;
        }

        protected virtual void CastWeakenSkill()
        {
            Debug.Log($"[{nameof(EliteEnemyBrain)}] Boss thi trien ky nang suy yeu/huy diet len nguoi choi!", this);
        }

        /// <summary>
        /// Kẻ địch Tinh anh luôn được nhận diện là Elite cho các kỹ năng rẽ nhánh hành vi.
        /// </summary>
        public override bool IsElite => true;

        /// <summary>
        /// Tinh anh chỉ miễn nhiễm khống chế CỨNG (Root/Stun) theo GDD.
        /// Các hiệu ứng mềm như Slow vẫn có tác dụng để kỹ năng như Water Bubble
        /// gây sát thương + làm chậm thay vì nhốt.
        /// </summary>
        public override bool IsImmuneToCrowdControl(CrowdControlType type)
        {
            return IsCrowdControlImmune
                   && (type == CrowdControlType.Root || type == CrowdControlType.Stun);
        }

        protected override void OnCrowdControlResisted(CrowdControlType type, float strength)
        {
            Debug.Log($"[{DisplayName}] Khang hieu ung khong che {type}!");
        }

        private void EnsureCores()
        {
            if (m_Cores.Count == energyCoreCount) return;

            m_Cores.Clear();
            for (int i = 0; i < energyCoreCount; i++)
            {
                m_Cores.Add(new EnergyCore());
            }

            BrokenCoreCount = 0;
        }

        #endregion

        #region EnemyBrain

        protected override void OnBrainTick(float deltaTime)
        {
            if (m_WeakenCooldown > 0f)
            {
                m_WeakenCooldown = Mathf.Max(0f, m_WeakenCooldown - deltaTime);
            }

            // Xu ly thoi gian duy tri giam phong thu
            if (m_IsDefenseBroken)
            {
                m_DefenseBreakTimer -= deltaTime;
                if (m_DefenseBreakTimer <= 0f)
                {
                    m_IsDefenseBroken = false;
                    if (Stats != null) Stats.Defense = m_OriginalDefense;
                    Debug.Log($"[{DisplayName}] Phong thu da hoi phuc ve {m_OriginalDefense}!");
                }
            }

            if (!m_BreakWindowOpen) return;

            m_BreakWindowRemaining = Mathf.Max(0f, m_BreakWindowRemaining - deltaTime);
            if (m_BreakWindowRemaining <= 0f)
            {
                m_BreakWindowOpen = false;

                if (!AllCoresBroken)
                {
                    OnBreakWindowExpired();
                }
            }
        }

        #endregion

        protected override void OnValidate()
        {
            base.OnValidate();

            energyCoreCount = Mathf.Max(1, energyCoreCount);
            breakWindowDuration = Mathf.Max(0f, breakWindowDuration);
            defenseBreakDuration = Mathf.Max(1f, defenseBreakDuration);
        }
    }
}
