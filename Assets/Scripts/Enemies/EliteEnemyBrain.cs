using System;
using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Các trạng thái trong vòng đời Bùng Nổ Năng Lượng (Energy Burst State Machine) của Boss Tinh Anh.
    /// </summary>
    public enum EliteBurstState
    {
        /// <summary>Trạng thái bình thường trước khi đạt ngưỡng máu bùng nổ.</summary>
        Inactive = 0,
        Normal = 0,

        /// <summary>Trạng thái vận sức / tích tụ năng lượng cảnh báo người chơi.</summary>
        Charging = 1,

        /// <summary>Giai đoạn Bùng nổ đang diễn ra, người chơi phải thực hiện cơ chế giải cứu/phá vỡ.</summary>
        Active = 2,

        /// <summary>Người chơi giải cơ chế thành công, Boss chịu suy yếu / Tank nhận bùa lợi thưởng.</summary>
        RewardPhase = 3,

        /// <summary>Người chơi thất bại trong việc giải cơ chế, Boss tung đòn trừng phạt diện rộng.</summary>
        PenaltyPhase = 4,

        /// <summary>Vòng đời Bùng nổ hoàn tất, Boss quay về trạng thái chiến đấu thường hoặc kết thúc trận.</summary>
        Finished = 5
    }

    /// <summary>
    /// Lớp cơ sở trừu tượng (Abstract Base Class) cho toàn bộ Boss Tinh Anh (Elite Enemy / Boss):
    /// - Quản trị vòng đời Bùng Nổ Năng Lượng (Energy Burst Lifecycle) dạng State Machine.
    /// - Tự động kích hoạt khi máu giảm dưới ngưỡng quy định (burstHpThreshold) hoặc kích hoạt thủ công.
    /// - Quản lý miễn nhiễm khống chế cứng (Stun / Root) theo GDD.
    /// - Hệ thống đệm tăng / giảm chỉ số tạm thời (ApplyBurstStatModifiers / RevertBurstStatModifiers).
    /// - Bảo toàn sự kiện toàn cục OnEnergyBurstSuccess để tương thích với Phụ kiện 20 (D1ONK) và các hệ thống khác.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class EliteEnemyBrain : EnemyBrain
    {
        [Header("Energy Burst (Bùng Nổ Năng Lượng)")]
        [Tooltip("Ngưỡng máu kích hoạt Bùng nổ năng lượng (0.5 = khi máu <= 50%).")]
        [SerializeField, Range(0.1f, 0.9f)] protected float burstHpThreshold = 0.5f;

        [Tooltip("Thời gian vận sức tích tụ trước khi kích hoạt cơ chế Bùng nổ (giây).")]
        [SerializeField, Min(0f)] protected float burstChargeTime = 2f;

        [Tooltip("Thời gian tồn tại tối đa của phase Bùng nổ để người chơi giải cơ chế (giây).")]
        [SerializeField, Min(0f)] protected float burstDuration = 30f;

        [Tooltip("Thời gian duy trì trạng thái Thưởng sau khi giải cơ chế thành công (giây).")]
        [SerializeField, Min(0f)] protected float rewardPhaseDuration = 8f;

        [Tooltip("Thời gian duy trì trạng thái Phạt sau khi thất bại cơ chế (giây).")]
        [SerializeField, Min(0f)] protected float penaltyPhaseDuration = 8f;

        [Header("Burst Stat Modifiers (Tùy chọn)")]
        [Tooltip("Hệ số tấn công nhân thêm trong phase Bùng nổ (1 = không đổi, 1.25 = +25%).")]
        [SerializeField] protected float burstAttackMultiplier = 1f;

        [Tooltip("Hệ số phòng thủ nhân thêm trong phase Bùng nổ (1 = không đổi, 1.5 = +50%).")]
        [SerializeField] protected float burstDefenseMultiplier = 1f;

        [Tooltip("Hệ số tốc độ di chuyển nhân thêm trong phase Bùng nổ (1 = không đổi, 1.2 = +20%).")]
        [SerializeField] protected float burstMoveSpeedMultiplier = 1f;

        // Runtime State
        private EliteBurstState m_BurstState = EliteBurstState.Inactive;
        private float m_BurstTimer;
        private bool m_BurstTriggered;

        private EnemyStats m_PreBurstStats;
        private bool m_BurstStatsApplied;

        #region Events & Properties

        /// <summary>
        /// Sự kiện toàn cục phát ra khi một kẻ địch Tinh anh bị phá vỡ Bùng nổ năng lượng thành công.
        /// Bảo toàn tính tương thích với Phụ kiện 20 (D1ONK) và hệ thống bùa lợi.
        /// </summary>
        public static event Action<EliteEnemyBrain> OnEnergyBurstSuccess;

        /// <summary>Sự kiện cục bộ khi trạng thái State Machine của Boss thay đổi.</summary>
        public event Action<EliteBurstState> OnBurstStateChanged;

        public EliteBurstState BurstState => m_BurstState;
        public float BurstTimer => m_BurstTimer;
        public float BurstDuration => burstDuration;
        public float BurstChargeTime => burstChargeTime;
        public float BurstHpThreshold => burstHpThreshold;
        public float RewardPhaseDuration => rewardPhaseDuration;
        public float PenaltyPhaseDuration => penaltyPhaseDuration;
        public bool HasBurstTriggered => m_BurstTriggered;

        public bool IsBurstCharging => m_BurstState == EliteBurstState.Charging;
        public bool IsBurstActive => m_BurstState == EliteBurstState.Active;
        public bool IsBurstInProgress => m_BurstState == EliteBurstState.Charging || m_BurstState == EliteBurstState.Active;
        public bool IsBurstResolved => m_BurstState == EliteBurstState.RewardPhase || m_BurstState == EliteBurstState.PenaltyPhase || m_BurstState == EliteBurstState.Finished;

        // Thuộc tính tương thích (Compatibility)
        public float BreakWindowRemaining => m_BurstTimer;
        public bool IsBreakWindowOpen => m_BurstState == EliteBurstState.Active;

        /// <summary>
        /// Kẻ địch Tinh anh luôn được nhận diện là Elite cho các kỹ năng rẽ nhánh hành vi.
        /// </summary>
        public override bool IsElite => true;

        #endregion

        #region Initialization & CC Immunity

        public override void Initialize(EnemyStats overrideStats = null)
        {
            base.Initialize(overrideStats);

            // Mặc định Boss Tinh anh miễn nhiễm khống chế cứng
            IsCrowdControlImmune = true;
            m_BurstTriggered = false;
            m_BurstState = EliteBurstState.Inactive;
            m_BurstTimer = 0f;
            RevertBurstStatModifiers();
        }

        /// <summary>
        /// Tinh anh chỉ miễn nhiễm khống chế CỨNG (Root/Stun) theo GDD.
        /// Các hiệu ứng mềm như Slow vẫn có tác dụng.
        /// </summary>
        public override bool IsImmuneToCrowdControl(CrowdControlType type)
        {
            return IsCrowdControlImmune
                   && (type == CrowdControlType.Root || type == CrowdControlType.Stun);
        }

        protected override void OnCrowdControlResisted(CrowdControlType type, float strength)
        {
            Debug.Log($"[{DisplayName}] Kháng hiệu ứng khống chế {type}!");
        }

        #endregion

        #region Damage & State Machine Trigger

        protected override void OnDamaged(float amount)
        {
            base.OnDamaged(amount);

            // Kiểm tra ngưỡng máu để tự động kích hoạt Bùng nổ năng lượng
            if (!m_BurstTriggered && IsAlive && MaxHealth > 0f && (CurrentHealth / MaxHealth) <= burstHpThreshold)
            {
                TriggerEnergyBurst();
            }
        }

        /// <summary>
        /// Bắt đầu vòng đời Bùng Nổ Năng Lượng.
        /// </summary>
        public virtual void TriggerEnergyBurst()
        {
            if (m_BurstTriggered || m_BurstState != EliteBurstState.Inactive) return;
            m_BurstTriggered = true;

            if (burstChargeTime > 0f)
            {
                TransitionToState(EliteBurstState.Charging);
            }
            else
            {
                TransitionToState(EliteBurstState.Active);
            }
        }

        /// <summary>
        /// Chuyển đổi trạng thái State Machine nội bộ và gọi các hook tương ứng.
        /// </summary>
        protected virtual void TransitionToState(EliteBurstState newState)
        {
            if (m_BurstState == newState) return;

            m_BurstState = newState;

            switch (newState)
            {
                case EliteBurstState.Charging:
                    m_BurstTimer = burstChargeTime;
                    OnBurstChargingStart();
                    break;

                case EliteBurstState.Active:
                    m_BurstTimer = burstDuration;
                    ApplyBurstStatModifiers(burstAttackMultiplier, burstDefenseMultiplier, burstMoveSpeedMultiplier);
                    OnBurstStart();
                    break;

                case EliteBurstState.RewardPhase:
                    m_BurstTimer = rewardPhaseDuration;
                    OnBurstSuccess();
                    OnEnergyBurstSuccess?.Invoke(this);
                    if (rewardPhaseDuration <= 0f)
                    {
                        FinishBurst();
                    }
                    break;

                case EliteBurstState.PenaltyPhase:
                    m_BurstTimer = penaltyPhaseDuration;
                    OnBurstFail();
                    if (penaltyPhaseDuration <= 0f)
                    {
                        FinishBurst();
                    }
                    break;

                case EliteBurstState.Finished:
                    m_BurstTimer = 0f;
                    RevertBurstStatModifiers();
                    OnBurstEnded();
                    break;
            }

            OnBurstStateChanged?.Invoke(m_BurstState);
        }

        #endregion

        #region Burst Resolution & Control

        /// <summary>
        /// Người chơi giải cơ chế thành công: Chuyển sang RewardPhase, kích hoạt OnBurstSuccess và phát OnEnergyBurstSuccess.
        /// </summary>
        public virtual void CompleteBurstSuccess()
        {
            if (m_BurstState != EliteBurstState.Active && m_BurstState != EliteBurstState.Charging) return;
            TransitionToState(EliteBurstState.RewardPhase);
        }

        /// <summary>
        /// Người chơi thất bại (hết giờ hoặc vi phạm điều kiện): Chuyển sang PenaltyPhase và gọi OnBurstFail.
        /// </summary>
        public virtual void CompleteBurstFailure()
        {
            if (m_BurstState != EliteBurstState.Active && m_BurstState != EliteBurstState.Charging) return;
            TransitionToState(EliteBurstState.PenaltyPhase);
        }

        /// <summary>
        /// Kết thúc vòng đời Bùng nổ năng lượng, phục hồi chỉ số và chuyển sang Finished.
        /// </summary>
        public virtual void FinishBurst()
        {
            if (m_BurstState == EliteBurstState.Finished) return;
            TransitionToState(EliteBurstState.Finished);
        }

        public void TriggerEnergyBurstManual() => TriggerEnergyBurst();
        public void ForceBurstSuccess() => CompleteBurstSuccess();
        public void ForceBurstFailure() => CompleteBurstFailure();

        /// <summary>
        /// Đặt lại toàn bộ trạng thái Bùng nổ (phục vụ test hoặc phase mới).
        /// </summary>
        public virtual void ResetBurstManual()
        {
            m_BurstTriggered = false;
            m_BurstTimer = 0f;
            RevertBurstStatModifiers();
            m_BurstState = EliteBurstState.Inactive;
            OnBurstStateChanged?.Invoke(m_BurstState);
        }

        #endregion

        #region Stat Modifiers

        public virtual void ApplyBurstStatModifiers(float attackMultiplier = 1f, float defenseMultiplier = 1f, float speedMultiplier = 1f)
        {
            if (Stats == null || m_BurstStatsApplied) return;
            m_PreBurstStats = Stats.Clone();

            if (!Mathf.Approximately(attackMultiplier, 1f)) Stats.Attack *= attackMultiplier;
            if (!Mathf.Approximately(defenseMultiplier, 1f)) Stats.Defense *= defenseMultiplier;
            if (!Mathf.Approximately(speedMultiplier, 1f)) Stats.MoveSpeed *= speedMultiplier;

            m_BurstStatsApplied = true;
        }

        public virtual void RevertBurstStatModifiers()
        {
            if (Stats == null || !m_BurstStatsApplied || m_PreBurstStats == null) return;

            Stats.Attack = m_PreBurstStats.Attack;
            Stats.Defense = m_PreBurstStats.Defense;
            Stats.MoveSpeed = m_PreBurstStats.MoveSpeed;

            m_BurstStatsApplied = false;
            m_PreBurstStats = null;
        }

        #endregion

        #region Update & Brain Tick

        protected override void OnBrainTick(float deltaTime)
        {
            switch (m_BurstState)
            {
                case EliteBurstState.Charging:
                    m_BurstTimer = Mathf.Max(0f, m_BurstTimer - deltaTime);
                    OnBurstChargingTick(deltaTime);
                    if (m_BurstTimer <= 0f)
                    {
                        TransitionToState(EliteBurstState.Active);
                    }
                    break;

                case EliteBurstState.Active:
                    m_BurstTimer = Mathf.Max(0f, m_BurstTimer - deltaTime);
                    OnBurstTick(deltaTime);
                    if (m_BurstTimer <= 0f)
                    {
                        CompleteBurstFailure();
                    }
                    break;

                case EliteBurstState.RewardPhase:
                    m_BurstTimer = Mathf.Max(0f, m_BurstTimer - deltaTime);
                    OnBurstRewardTick(deltaTime);
                    if (m_BurstTimer <= 0f)
                    {
                        FinishBurst();
                    }
                    break;

                case EliteBurstState.PenaltyPhase:
                    m_BurstTimer = Mathf.Max(0f, m_BurstTimer - deltaTime);
                    OnBurstPenaltyTick(deltaTime);
                    if (m_BurstTimer <= 0f)
                    {
                        FinishBurst();
                    }
                    break;
            }

            OnEliteTick(deltaTime);
        }

        protected override void OnDeath()
        {
            if (m_BurstStatsApplied)
            {
                RevertBurstStatModifiers();
            }
        }

        #endregion

        #region Hooks for Concrete Bosses

        /// <summary>Gọi khi bắt đầu giai đoạn vận sức (Charging).</summary>
        protected virtual void OnBurstChargingStart() { }

        /// <summary>Cập nhật mỗi frame trong giai đoạn vận sức.</summary>
        protected virtual void OnBurstChargingTick(float deltaTime) { }

        /// <summary>Bắt đầu phase Bùng Nổ Năng Lượng (Active) - Bắt buộc lớp con cài đặt.</summary>
        protected abstract void OnBurstStart();

        /// <summary>Cập nhật mỗi frame trong phase Bùng Nổ Năng Lượng (Active).</summary>
        protected virtual void OnBurstTick(float deltaTime) { }

        /// <summary>Thực thi phần thưởng khi giải cơ chế thành công - Bắt buộc lớp con cài đặt.</summary>
        protected abstract void OnBurstSuccess();

        /// <summary>Thực thi hình phạt khi giải cơ chế thất bại - Bắt buộc lớp con cài đặt.</summary>
        protected abstract void OnBurstFail();

        /// <summary>Gọi khi phase Bùng nổ kết thúc hoàn toàn (chuyển sang Finished).</summary>
        protected virtual void OnBurstEnded() { }

        /// <summary>Cập nhật mỗi frame trong giai đoạn Thưởng (RewardPhase).</summary>
        protected virtual void OnBurstRewardTick(float deltaTime) { }

        /// <summary>Cập nhật mỗi frame trong giai đoạn Phạt (PenaltyPhase).</summary>
        protected virtual void OnBurstPenaltyTick(float deltaTime) { }

        /// <summary>AI tick thường của Boss (di chuyển, dùng chiêu ngoài burst...).</summary>
        protected virtual void OnEliteTick(float deltaTime) { }

        #endregion

        protected override void OnValidate()
        {
            base.OnValidate();

            burstHpThreshold = Mathf.Clamp(burstHpThreshold, 0.05f, 0.95f);
            burstChargeTime = Mathf.Max(0f, burstChargeTime);
            burstDuration = Mathf.Max(0f, burstDuration);
            rewardPhaseDuration = Mathf.Max(0f, rewardPhaseDuration);
            penaltyPhaseDuration = Mathf.Max(0f, penaltyPhaseDuration);
        }
    }
}
