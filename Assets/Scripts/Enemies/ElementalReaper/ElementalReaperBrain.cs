using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Core.Effects;
using IronWasteland.Enemies.DeadlandPhantom;
using IronWasteland.Enemies.ElementalReaper.Hitboxes;
using IronWasteland.Enemies.ElementalReaper.VFX;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper
{
    /// <summary>
    /// Boss 8 (Ải 2-4 Phase 2) — "Tử Thần Nguyên Tố" (Elemental Reaper):
    /// - Chỉ số chuẩn GDD: HP 9500 (Co-op 2p: 19000), ATK 250, DEF 25, SPD 90 (0.9 unit/s).
    /// - Miễn nhiễm khống chế cứng (Stun / Root).
    /// - Tốc độ di chuyển chuẩn hóa: MoveSpeed / 100 unit/s.
    /// - Cơ chế Xoay:
    ///   + Khi di chuyển: Tự động xoay theo hướng di chuyển.
    ///   + Khi dùng kỹ năng: Tự động xoay về hướng tấn công / mục tiêu.
    ///   + Nếu kỹ năng không cần mục tiêu (ThuyLieu / Tự buff) hoặc không có mục tiêu: Đặt góc quay về 0.
    /// - Chu kỳ chuyển dạng nguyên tố: 15s (Thủy -> Độc -> Hỏa -> Thủy...).
    ///   Trong Normal Phase chỉ dùng 2 kỹ năng của hệ hiện tại.
    /// - Bộ 6 kỹ năng Tam Nguyên Tố:
    ///   + Thủy: ThuyTrieu (Sóng nón 4m 60°, đẩy lùi 3m), ThuyLieu (Tự hồi 5% HP, DEF +25% trong 5s, xoay 0°).
    ///   + Độc: DocQuat (Bán nguyệt 180° 4m, Slow 20%, DEF -10%), LoanChuyen (Teleport tới Tank, nổ 3m + Stun 2s).
    ///   + Hỏa: PhunDungNham (Tia thẳng 16m x 1.2m, 120% ATK), HoaToc (Lốc xoáy xa nhất -> Lướt 20m/s hất 2m -> Vệt lửa 10s).
    /// - Vòng đời Bùng Nổ Năng Lượng (HP <= 45%):
    ///   + Charging (2s): Dừng di chuyển, xoay về 0, phát hoạt ảnh & vòng ma pháp tam nguyên.
    ///   + Active (30s): Hồi 20% HP, buff ATK/DEF/SPD, Khiên Nguyên Tố 25% HP, dựng 3 Tế Đàn Sub-Arena.
    ///     * Solo: Chỉ kích hoạt Tế Đàn Hỏa.
    ///     * Co-op: Tank gần -> Hỏa, xa -> Độc, thứ 3 -> Thủy.
    ///     * Sinh Tinh Thể (Tròn: Stun 2s, Vuông: -10% HP, Tam Giác: Khiên 1-hit + 25% Xuyên Giáp trong 3s).
    ///   + Thưởng (Success): Xóa khiên, Boss Choáng 3s, Boss giảm 80% DEF trong 10s, Tank +25% ATK & hồi 20% HP.
    ///   + Phạt (Failure): Nổ 100% ATK toàn map / bản sao còn sống, Boss hồi 10% HP, MỞ KHÓA VĨNH VIỄN CẢ 6 KỸ NĂNG ĐẾN HẾT TRẬN.
    /// </summary>
    [DisallowMultipleComponent]
    public class ElementalReaperBrain : EliteEnemyBrain
    {
        [Header("Movement & Speed")]
        [Tooltip("Khoảng cách dừng tiếp cận Tank mục tiêu (m).")]
        [SerializeField, Min(0f)] private float stopDistance = 2.0f;

        [Tooltip("Độ nhạy khi đổi hướng di chuyển (mượt chuyển động).")]
        [SerializeField, Min(0.01f)] private float turnSharpness = 14f;

        [Tooltip("Hệ số chia tốc độ di chuyển để quy đổi MoveSpeed sang unit/s (MoveSpeed / 100). Mặc định 100.")]
        [SerializeField, Min(1f)] private float moveSpeedDivisor = 100f;

        [Header("Rotation & Facing")]
        [Tooltip("Góc bù độ lệch ban đầu của Sprite/Model (độ).")]
        [SerializeField] private float rotationOffset = 0f;

        [Tooltip("Tốc độ xoay (độ/giây). Giá trị <= 0 xoay tức thời.")]
        [SerializeField] private float rotationSpeed = 720f;

        [Tooltip("Transform mục tiêu xoay. Mặc định tự lấy Model hoặc chính GameObject.")]
        [SerializeField] private Transform rotationPivot;

        [Header("Elemental Shift (Chu kỳ 15s)")]
        [Tooltip("Chu kỳ chuyển đổi dạng nguyên tố trong Normal phase (giây). GDD: 15s.")]
        [SerializeField, Min(1f)] private float elementalShiftInterval = 15f;

        [Tooltip("Hệ nguyên tố hiện tại của Boss.")]
        [SerializeField] private ElementalType currentElement = ElementalType.Water;

        [Header("Skill Cooldowns: Water (Thủy)")]
        [Tooltip("Hồi chiêu Sóng Thủy Triều (giây). GDD: 8s.")]
        [SerializeField, Min(0.1f)] private float thuyTrieuCooldown = 8.0f;

        [Tooltip("Hồi chiêu Thủy Liệu Trị Liệu (giây). GDD: 14s.")]
        [SerializeField, Min(0.1f)] private float thuyLieuCooldown = 14.0f;

        [Header("Skill Cooldowns: Poison (Độc)")]
        [Tooltip("Hồi chiêu Độc Quật (giây). GDD: 6s.")]
        [SerializeField, Min(0.1f)] private float docQuatCooldown = 6.0f;

        [Tooltip("Hồi chiêu Loạn Chuyển & Nổ Độc (giây). GDD: 12s.")]
        [SerializeField, Min(0.1f)] private float loanChuyenCooldown = 12.0f;

        [Header("Skill Cooldowns: Fire (Hỏa)")]
        [Tooltip("Hồi chiêu Phun Dung Nham (giây). GDD: 7s.")]
        [SerializeField, Min(0.1f)] private float phunDungNhamCooldown = 7.0f;

        [Tooltip("Hồi chiêu Hỏa Tốc & Lốc Xoáy Lửa (giây). GDD: 13s.")]
        [SerializeField, Min(0.1f)] private float hoaTocCooldown = 13.0f;

        [Header("Sub-Arena & Sub-Prefabs")]
        [Tooltip("Prefab Sub-Arena Tế Đàn Tam Nguyên (ElementalAltarsArena).")]
        [SerializeField] private GameObject elementalAltarsArenaPrefab;

        [Header("Sub-Arena Altar Coordinates (Inspector Configurable)")]
        [Tooltip("Tọa độ tâm tế đàn Hỏa")]
        [SerializeField] private Vector2 fireAltarCenter = new Vector2(150f, 100f);

        [Tooltip("Tọa độ tâm tế đàn Độc")]
        [SerializeField] private Vector2 poisonAltarCenter = new Vector2(150f, 150f);

        [Tooltip("Tọa độ tâm tế đàn Thủy")]
        [SerializeField] private Vector2 waterAltarCenter = new Vector2(150f, 200f);

        [Tooltip("Bán kính của mỗi tế đàn")]
        [SerializeField, Min(4f)] private float altarRadius = 8.0f;

        // References
        private ElementalReaperVFXController m_VfxController;
        private EnemySensorArea m_SensorArea;
        private Rigidbody2D m_Rigidbody;
        private Animator m_Animator;
        private ElementalAltarsSubArena m_SubArena;

        // Runtime Movement & Targeting
        private Vector2 m_DesiredVelocity;
        private TankBrain m_CurrentTarget;

        // Skill Timers
        private float m_ElementalShiftTimer;
        private float m_ThuyTrieuTimer;
        private float m_ThuyLieuTimer;
        private float m_DocQuatTimer;
        private float m_LoanChuyenTimer;
        private float m_PhunDungNhamTimer;
        private float m_HoaTocTimer;

        // Burst & Permanent Unlock State
        private bool m_PermanentAllSkillsUnlocked;
        private bool m_IsDashing;
        private float m_ElementalShieldAmount;

        public ElementalType CurrentElement => currentElement;
        public bool IsPermanentAllSkillsUnlocked => m_PermanentAllSkillsUnlocked;
        public float ElementalShiftTimer => m_ElementalShiftTimer;
        public TankBrain CurrentTarget => m_CurrentTarget;
        public ElementalAltarsSubArena SubArena => m_SubArena;

        public float ThuyTrieuTimer => m_ThuyTrieuTimer;
        public float ThuyLieuTimer => m_ThuyLieuTimer;
        public float DocQuatTimer => m_DocQuatTimer;
        public float LoanChuyenTimer => m_LoanChuyenTimer;
        public float PhunDungNhamTimer => m_PhunDungNhamTimer;
        public float HoaTocTimer => m_HoaTocTimer;

        public GameObject ElementalAltarsArenaPrefab { get => elementalAltarsArenaPrefab; set => elementalAltarsArenaPrefab = value; }
        public Vector2 FireAltarCenter { get => fireAltarCenter; set => fireAltarCenter = value; }
        public Vector2 PoisonAltarCenter { get => poisonAltarCenter; set => poisonAltarCenter = value; }
        public Vector2 WaterAltarCenter { get => waterAltarCenter; set => waterAltarCenter = value; }
        public float AltarRadius { get => altarRadius; set => altarRadius = value; }

        #region Unity Lifecycle & Init

        protected override void Awake()
        {
            base.Awake();

            m_VfxController = GetComponent<ElementalReaperVFXController>();
            m_SensorArea = GetComponentInChildren<EnemySensorArea>();
            m_Rigidbody = GetComponent<Rigidbody2D>();
            m_Animator = GetComponentInChildren<Animator>();

            if (m_Rigidbody != null)
            {
                m_Rigidbody.bodyType = RigidbodyType2D.Kinematic;
                m_Rigidbody.gravityScale = 0f;
            }

            // Thiết lập mặc định vòng đời Bùng nổ năng lượng
            burstHpThreshold = 0.45f;
            burstChargeTime = 2.0f;
            burstDuration = 30.0f;
            rewardPhaseDuration = 10.0f;
            penaltyPhaseDuration = 10.0f;
        }

        public override void Initialize(EnemyStats overrideStats = null)
        {

            base.Initialize(overrideStats);

            currentElement = ElementalType.Water;
            m_ElementalShiftTimer = elementalShiftInterval;
            m_PermanentAllSkillsUnlocked = false;
            m_IsDashing = false;

            // Khởi tạo các timer kỹ năng
            m_ThuyTrieuTimer = thuyTrieuCooldown * 0.5f;
            m_ThuyLieuTimer = thuyLieuCooldown * 0.3f;
            m_DocQuatTimer = docQuatCooldown * 0.5f;
            m_LoanChuyenTimer = loanChuyenCooldown * 0.4f;
            m_PhunDungNhamTimer = phunDungNhamCooldown * 0.5f;
            m_HoaTocTimer = hoaTocCooldown * 0.3f;

            if (m_VfxController != null)
            {
                m_VfxController.SetElementalAura(currentElement);
            }
        }

        protected virtual void OnDestroy()
        {
            if (m_SubArena != null)
            {
                m_SubArena.CleanupArena();
            }
        }

        #endregion

        #region Brain & AI Tick

        protected override void OnEliteTick(float deltaTime)
        {
            if (!IsAlive) return;

            // Trong lúc vận sức (Charging), dừng di chuyển và dùng chiêu, đặt góc quay về 0
            if (IsBurstCharging)
            {
                m_DesiredVelocity = Vector2.zero;
                Model?.SetMoveSpeed(0f);
                RotateToZero(instant: true);
                return;
            }

            // Nếu đang trong hoạt cảnh lướt Hỏa Tốc
            if (m_IsDashing)
            {
                return;
            }

            // 1. Cập nhật chu kỳ chuyển đổi nguyên tố (trong Normal Phase)
            UpdateElementalShift(deltaTime);

            // 2. Tìm mục tiêu Tank gần nhất (không bị nhốt trong tế đàn)
            UpdateTarget();

            // 3. Di chuyển Kinematic & Xoay theo hướng di chuyển
            UpdateKinematicMovement(deltaTime);

            // 4. Xử lý kỹ năng (tích cực trong cả Normal, Active, Reward, Penalty, Finished)
            UpdateSkills(deltaTime);
        }

        private void FixedUpdate()
        {
            if (!IsAlive || m_Rigidbody == null || m_IsDashing) return;

            if (m_Rigidbody.bodyType == RigidbodyType2D.Kinematic)
            {
                m_Rigidbody.MovePosition(m_Rigidbody.position + m_DesiredVelocity * Time.fixedDeltaTime);
            }
            else
            {
                m_Rigidbody.linearVelocity = m_DesiredVelocity;
            }
        }

        private void UpdateElementalShift(float deltaTime)
        {
            // Nếu đã mở khóa vĩnh viễn cả 6 chiêu do hình phạt thì không cần luân chuyển khóa chiêu
            if (m_PermanentAllSkillsUnlocked) return;

            // Trong lúc Bùng nổ (Active), giữ nguyên hệ hoặc luân chuyển bình thường
            m_ElementalShiftTimer -= deltaTime;
            if (m_ElementalShiftTimer <= 0f)
            {
                m_ElementalShiftTimer = elementalShiftInterval;
                ShiftToNextElement();
            }
        }

        public void ShiftToNextElement()
        {
            switch (currentElement)
            {
                case ElementalType.Water:
                    currentElement = ElementalType.Poison;
                    break;
                case ElementalType.Poison:
                    currentElement = ElementalType.Fire;
                    break;
                case ElementalType.Fire:
                default:
                    currentElement = ElementalType.Water;
                    break;
            }

            if (m_VfxController != null)
            {
                m_VfxController.SetElementalAura(currentElement);
            }
        }

        public void SetCurrentElement(ElementalType newElement)
        {
            currentElement = newElement;
            m_ElementalShiftTimer = elementalShiftInterval;
            if (m_VfxController != null)
            {
                m_VfxController.SetElementalAura(currentElement);
            }
        }

        private void UpdateTarget()
        {
            if (m_SensorArea == null) m_SensorArea = GetComponentInChildren<EnemySensorArea>();

            // Lọc loại trừ bất kỳ Tank nào đang bị nhốt trong tế đàn
            TankBrain trappedTank = null;
            if (m_SubArena != null)
            {
                for (int i = 0; i < m_SubArena.AllAltars.Count; i++)
                {
                    if (m_SubArena.AllAltars[i].isActive && m_SubArena.AllAltars[i].trappedTank != null)
                    {
                        trappedTank = m_SubArena.AllAltars[i].trappedTank;
                        break;
                    }
                }
            }

            if (m_SensorArea != null)
            {
                m_CurrentTarget = m_SensorArea.GetNearestTarget(transform.position, trappedTank);
            }
            else
            {
                m_CurrentTarget = FindNearestTank(trappedTank);
            }
        }

        private void UpdateKinematicMovement(float deltaTime)
        {
            if (m_CurrentTarget == null || !m_CurrentTarget.IsAlive)
            {
                m_DesiredVelocity = Vector2.zero;
                Model?.SetMoveSpeed(0f);
                return;
            }

            Vector2 toTarget = (Vector2)m_CurrentTarget.transform.position - (Vector2)transform.position;
            float distance = toTarget.magnitude;

            Vector2 targetVelocity = Vector2.zero;
            if (distance > stopDistance)
            {
                float divisor = moveSpeedDivisor > 0f ? moveSpeedDivisor : 100f;
                float actualMoveSpeed = (Stats != null ? Stats.MoveSpeed : 90f) / divisor;
                targetVelocity = toTarget.normalized * actualMoveSpeed;
            }

            float t = 1f - Mathf.Exp(-turnSharpness * deltaTime);
            m_DesiredVelocity = Vector2.Lerp(m_DesiredVelocity, targetVelocity, t);
            Model?.SetMoveSpeed(m_DesiredVelocity.magnitude);

            if (m_DesiredVelocity.sqrMagnitude > 0.001f)
            {
                RotateTowardsDirection(m_DesiredVelocity, instant: false, dt: deltaTime);
            }
        }

        #endregion

        #region Skills System

        private void UpdateSkills(float deltaTime)
        {
            // Trừ timer toàn bộ kỹ năng
            m_ThuyTrieuTimer -= deltaTime;
            m_ThuyLieuTimer -= deltaTime;
            m_DocQuatTimer -= deltaTime;
            m_LoanChuyenTimer -= deltaTime;
            m_PhunDungNhamTimer -= deltaTime;
            m_HoaTocTimer -= deltaTime;

            // Nếu đang trong thời gian giãn cách kỹ năng toàn cục (GlobalSkillInterval), không thi triển chiêu mới
            if (!CanCastAnySkill) return;

            // Kiểm tra thi triển kỹ năng
            // Nếu mở khóa vĩnh viễn cả 6 chiêu (sau PenaltyPhase): thi triển bất kỳ chiêu nào hồi xong
            if (m_PermanentAllSkillsUnlocked)
            {
                if (m_ThuyTrieuTimer <= 0f) { CastThuyTrieu(); return; }
                if (m_DocQuatTimer <= 0f) { CastDocQuat(); return; }
                if (m_PhunDungNhamTimer <= 0f) { CastPhunDungNham(); return; }
                if (m_ThuyLieuTimer <= 0f) { CastThuyLieu(); return; }
                if (m_LoanChuyenTimer <= 0f) { CastLoanChuyen(); return; }
                if (m_HoaTocTimer <= 0f) { CastHoaToc(); return; }
                return;
            }

            // Normal phase: Chỉ thi triển 2 chiêu của hệ hiện tại
            switch (currentElement)
            {
                case ElementalType.Water:
                    if (m_ThuyTrieuTimer <= 0f) CastThuyTrieu();
                    else if (m_ThuyLieuTimer <= 0f) CastThuyLieu();
                    break;

                case ElementalType.Poison:
                    if (m_DocQuatTimer <= 0f) CastDocQuat();
                    else if (m_LoanChuyenTimer <= 0f) CastLoanChuyen();
                    break;

                case ElementalType.Fire:
                default:
                    if (m_PhunDungNhamTimer <= 0f) CastPhunDungNham();
                    else if (m_HoaTocTimer <= 0f) CastHoaToc();
                    break;
            }
        }

        public void CastThuyTrieu()
        {
            m_ThuyTrieuTimer = thuyTrieuCooldown;
            TriggerGlobalSkillCooldown();
            if (m_CurrentTarget == null) return;

            RotateTowardsTarget(m_CurrentTarget.transform.position, instant: true);
            Vector3 dir = (m_CurrentTarget.transform.position - transform.position).normalized;

            if (m_VfxController != null)
            {
                m_VfxController.PlayThuyTrieu(dir);
            }
        }

        public void CastThuyLieu()
        {
            m_ThuyLieuTimer = thuyLieuCooldown;
            TriggerGlobalSkillCooldown();

            // Chiêu tự buff: Xoay về góc 0
            RotateToZero(instant: true);

            // Hồi 5% Max HP
            Heal(MaxHealth * 0.05f);

            // Tăng 25% DEF trong 5s
            StartCoroutine(BuffDefenseTemporary(5.0f, 0.25f));

            if (m_VfxController != null)
            {
                m_VfxController.PlayThuyLieu();
            }
        }

        public void CastDocQuat()
        {
            m_DocQuatTimer = docQuatCooldown;
            TriggerGlobalSkillCooldown();
            if (m_CurrentTarget == null) return;

            Vector3 targetPos = m_CurrentTarget.transform.position;
            Vector3 dir = (targetPos - transform.position).normalized;
            RotateTowardsTarget(targetPos, instant: true);

            if (m_VfxController != null)
            {
                m_VfxController.PlayDocQuat(dir);
            }
        }

        public void CastLoanChuyen()
        {
            m_LoanChuyenTimer = loanChuyenCooldown;
            TriggerGlobalSkillCooldown();

            // Chọn Tank ngẫu nhiên
            TankBrain target = null;
            if (m_SensorArea != null && m_SensorArea.DetectedTanks.Count > 0)
            {
                var tanks = new List<TankBrain>(m_SensorArea.DetectedTanks);
                target = tanks[UnityEngine.Random.Range(0, tanks.Count)];
            }
            if (target == null) target = m_CurrentTarget;
            if (target == null) return;

            Vector2 targetPos = target.transform.position;
            transform.position = targetPos;
            RotateTowardsTarget(targetPos, instant: true);

            if (m_VfxController != null)
            {
                m_VfxController.PlayLoanChuyenBurst(targetPos);
            }
        }

        public void CastPhunDungNham()
        {
            m_PhunDungNhamTimer = phunDungNhamCooldown;
            TriggerGlobalSkillCooldown();
            if (m_CurrentTarget == null) return;

            RotateTowardsTarget(m_CurrentTarget.transform.position, instant: true);
            Vector3 dir = (m_CurrentTarget.transform.position - transform.position).normalized;

            if (m_VfxController != null)
            {
                m_VfxController.PlayPhunDungNham(dir);
            }
        }

        public void CastHoaToc()
        {
            m_HoaTocTimer = hoaTocCooldown;
            TriggerGlobalSkillCooldown();

            // Tìm Tank xa nhất
            TankBrain furthestTank = null;
            if (m_SensorArea != null)
            {
                furthestTank = m_SensorArea.GetFurthestTarget(transform.position);
            }
            if (furthestTank == null) furthestTank = m_CurrentTarget;
            if (furthestTank == null) return;

            Vector2 targetPos = furthestTank.transform.position;
            StartCoroutine(RoutineHoaToc(targetPos));
        }

        private IEnumerator RoutineHoaToc(Vector2 targetPos)
        {
            m_IsDashing = true;
            m_DesiredVelocity = Vector2.zero;
            Model?.SetMoveSpeed(0f);

            // Pha 1: Vận sức 1.5s và sinh Lốc Xoáy Lửa tại vị trí mục tiêu
            RotateTowardsTarget(targetPos, instant: true);
            if (m_VfxController != null)
            {
                m_VfxController.SpawnFireTornado(targetPos);
            }
            yield return new WaitForSeconds(1.5f);

            // Pha 2: Lao vọt 20 m/s tới vị trí lốc xoáy
            Vector2 startPos = transform.position;
            Vector2 dashDir = (targetPos - startPos).normalized;
            RotateTowardsDirection(dashDir, instant: true);

            if (m_VfxController != null)
            {
                m_VfxController.ActivateHoaTocDash(dashDir);
            }

            float distance = Vector2.Distance(startPos, targetPos);
            float dashSpeed = 20.0f;
            float dashDuration = distance > 0.01f ? (distance / dashSpeed) : 0.1f;
            float elapsed = 0f;

            while (elapsed < dashDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / dashDuration);
                Vector2 currentPos = Vector2.Lerp(startPos, targetPos, progress);
                if (m_Rigidbody != null) m_Rigidbody.position = currentPos;
                transform.position = currentPos;
                yield return null;
            }

            transform.position = targetPos;
            if (m_Rigidbody != null) m_Rigidbody.position = targetPos;

            if (m_VfxController != null)
            {
                m_VfxController.DeactivateHoaTocDash();
                // Pha 3: Sinh vệt lửa 10s trên đường lướt
                m_VfxController.SpawnHoaTocFireTrail(startPos, targetPos);
            }

            m_IsDashing = false;
        }

        private IEnumerator BuffDefenseTemporary(float duration, float ratio)
        {
            if (Stats == null) yield break;
            float originalDef = Stats.Defense;
            Stats.Defense += originalDef * ratio;
            yield return new WaitForSeconds(duration);
            if (Stats != null)
            {
                Stats.Defense = originalDef;
            }
        }

        #endregion

        #region Burst Lifecycle Hooks

        protected override void OnBurstChargingStart()
        {
            base.OnBurstChargingStart();

            m_DesiredVelocity = Vector2.zero;
            Model?.SetMoveSpeed(0f);
            RotateToZero(instant: true);

            if (m_VfxController != null)
            {
                m_VfxController.StartBurstCharging(burstChargeTime);
            }
        }

        protected override void OnBurstStart()
        {
            if (m_VfxController != null)
            {
                m_VfxController.StopBurstCharging();
            }

            // 1. Hồi 20% Max HP
            Heal(MaxHealth * 0.20f);

            // 2. Kích hoạt Khiên Nguyên Tố 25% HP
            m_ElementalShieldAmount = MaxHealth * 0.25f;
            if (m_VfxController != null)
            {
                m_VfxController.ActivateBurstShield();
            }

            // 3. Khởi tạo Sub-Arena Tế Đàn Tam Nguyên
            EnsureSubArena();
            if (m_SubArena != null)
            {
                List<TankBrain> tanks = new List<TankBrain>();
                if (m_SensorArea != null) tanks.AddRange(m_SensorArea.DetectedTanks);
                else
                {
                    var allTanks = FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
                    tanks.AddRange(allTanks);
                }

                EnemyStats cloneStats = new EnemyStats
                {
                    MaxHealth = MaxHealth * 0.25f,
                    Attack = Stats != null ? Stats.Attack : 250f,
                    Defense = Stats != null ? Stats.Defense : 25f,
                    MoveSpeed = Stats != null ? Stats.MoveSpeed : 90f
                };

                m_SubArena.StartBurstPhase(tanks, cloneStats);
            }

            // 4. Reset toàn bộ timer kỹ năng để Boss có thể tung chiêu ngay lập tức ngoài sân
            ResetAllSkillTimersReady();
        }

        protected override void OnBurstTick(float deltaTime)
        {
            base.OnBurstTick(deltaTime);

            // Kiểm tra điều kiện THƯỞNG: Toàn bộ Bản sao tại các tế đàn đã bị tiêu diệt
            if (m_SubArena != null && m_SubArena.AreAllClonesDefeated())
            {
                CompleteBurstSuccess();
            }
        }

        protected override void OnBurstSuccess()
        {
            // 1. Tắt khiên nguyên tố
            if (m_VfxController != null)
            {
                m_VfxController.DeactivateBurstShield();
            }

            // 2. Dọn dẹp Sub-Arena và giải phóng Tank
            if (m_SubArena != null)
            {
                m_SubArena.CleanupArena();
            }

            // 3. Boss bị Choáng 3s: Tạm gỡ miễn nhiễm CC và áp dụng Stun
            StartCoroutine(RoutineRewardStunAndDebuff());

            // 4. Bùa lợi cho toàn bộ Tank: +25% ATK trong 10s và hồi 20% HP
            BuffAllTanksReward();

            // 5. Reset timer kỹ năng để sẵn sàng sau khi hết Stun
            ResetAllSkillTimersReady();
        }

        private IEnumerator RoutineRewardStunAndDebuff()
        {
            IsCrowdControlImmune = false;
            ApplyCrowdControl(CrowdControlType.Stun, 3.0f);
            if (m_VfxController != null)
            {
                m_VfxController.PlayStunAnimation();
            }

            // Giảm 80% DEF của Boss trong 10s (DEF = 25 -> 5)
            float originalDef = Stats != null ? Stats.Defense : 25f;
            if (Stats != null)
            {
                Stats.Defense = originalDef * 0.20f;
            }

            yield return new WaitForSeconds(3.0f);
            IsCrowdControlImmune = true; // Khôi phục miễn nhiễm sau khi hết Choáng

            yield return new WaitForSeconds(7.0f);
            if (Stats != null)
            {
                Stats.Defense = originalDef; // Hoàn trả DEF gốc
            }
        }

        private void BuffAllTanksReward()
        {
            var allTanks = FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            for (int i = 0; i < allTanks.Length; i++)
            {
                TankBrain tank = allTanks[i];
                if (tank != null && tank.IsAlive)
                {
                    float maxHp = (tank.Stats != null && tank.Stats.MaxHealth > 0f) ? tank.Stats.MaxHealth : tank.CurrentHealth;
                    tank.Heal(maxHp * 0.20f);
                    var atkBuff = new StatModifierEffect(
                        "Boss8_Reward_AtkBuff",
                        "Thưởng Tam Nguyên",
                        rewardPhaseDuration > 0f ? rewardPhaseDuration : 10.0f,
                        percentModifier: new TankStats { Attack = 0.25f }
                    );
                    tank.ApplyEffect(atkBuff);
                }
            }
        }

        protected override void OnBurstFail()
        {
            // 1. Tắt khiên nguyên tố
            if (m_VfxController != null)
            {
                m_VfxController.DeactivateBurstShield();
            }

            // 2. Tính số Bản sao còn sống
            int livingClones = m_SubArena != null ? m_SubArena.CountLivingClones() : 1;
            if (livingClones < 1) livingClones = 1;

            // 3. Dọn dẹp Sub-Arena và trả Tank về sân chính
            if (m_SubArena != null)
            {
                m_SubArena.CleanupArena();
            }

            // 4. Nổ trừng phạt toàn bản đồ: 100% ATK (GDD)
            float penaltyDamage = Stats != null ? Stats.Attack : 250f;
            var allTanks = FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            for (int i = 0; i < allTanks.Length; i++)
            {
                TankBrain tank = allTanks[i];
                if (tank != null && tank.IsAlive)
                {
                    tank.TakeDamage(penaltyDamage);
                }
            }

            // 5. Boss hồi 10% Max HP cho mỗi bản sao chưa bị tiêu diệt
            Heal(MaxHealth * 0.10f * livingClones);

            // 6. HÌNH PHẠT VĨNH VIỄN: Mở khóa cả 6 kỹ năng của cả 3 hệ đến hết trận đấu!
            m_PermanentAllSkillsUnlocked = true;

            // 7. Reset timer kỹ năng để tung chiêu ngay lập tức
            ResetAllSkillTimersReady();
        }

        public override void TakeDamage(DamageInfo info)
        {
            if (IsBurstActive && m_ElementalShieldAmount > 0f && info.RawDamage > 0f)
            {
                float resolved = CombatUtility.ResolveDamage(info.RawDamage, Stats != null ? Stats.Defense : 0f, info.ArmorPenetration);
                float absorbed = Mathf.Min(m_ElementalShieldAmount, resolved);
                m_ElementalShieldAmount -= absorbed;
                if (absorbed > 0.01f)
                {
                    Vector3 showPos = info.HitPosition != Vector2.zero ? (Vector3)info.HitPosition : transform.position;
                    CombatFeedback.Show(absorbed, showPos, TextValueType.ShieldAbsorb);
                }

                if (m_ElementalShieldAmount <= 0f)
                {
                    m_ElementalShieldAmount = 0f;
                    m_VfxController?.DeactivateBurstShield();
                }

                float remaining = resolved - absorbed;
                if (remaining <= 0f) return;

                info = new DamageInfo(info.Attacker, this, remaining, armorPenetration: 1f, hitPosition: info.HitPosition, isDirectToHealth: true, sourceType: info.SourceType);
            }

            base.TakeDamage(info);
        }

        protected override void OnBurstEnded()
        {
            base.OnBurstEnded();
            ResetAllSkillTimersReady();
        }

        private void ResetAllSkillTimersReady()
        {
            m_ThuyTrieuTimer = 0f;
            m_ThuyLieuTimer = 0f;
            m_DocQuatTimer = 0f;
            m_LoanChuyenTimer = 0f;
            m_PhunDungNhamTimer = 0f;
            m_HoaTocTimer = 0f;
            ResetGlobalSkillCooldown();
        }

        private void EnsureSubArena()
        {
            if (m_SubArena == null)
            {
                m_SubArena = GetComponentInChildren<ElementalAltarsSubArena>();
            }
            if (m_SubArena == null)
            {
                m_SubArena = FindFirstObjectByType<ElementalAltarsSubArena>();
            }
            if (m_SubArena == null && elementalAltarsArenaPrefab != null)
            {
                GameObject go = Instantiate(elementalAltarsArenaPrefab, Vector3.zero, Quaternion.identity);
                m_SubArena = go.GetComponent<ElementalAltarsSubArena>();
            }
            if (m_SubArena != null)
            {
                m_SubArena.ConfigureAltars(fireAltarCenter, poisonAltarCenter, waterAltarCenter, altarRadius);
                m_SubArena.InitializeArena(this);
            }
        }

        #endregion

        #region Rotation Logic

        public void RotateTowardsDirection(Vector2 direction, bool instant = false, float dt = -1f)
        {
            Transform pivot = GetRotationPivot();
            if (pivot == null) return;

            if (direction.sqrMagnitude < 0.0001f)
            {
                RotateToZero(instant);
                return;
            }

            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;
            ApplyRotationAngle(pivot, targetAngle, instant, dt);
        }

        public void RotateTowardsTarget(Vector2 targetPosition, bool instant = false)
        {
            Vector2 dir = targetPosition - (Vector2)transform.position;
            RotateTowardsDirection(dir, instant);
        }

        public void RotateToZero(bool instant = false)
        {
            Transform pivot = GetRotationPivot();
            if (pivot == null) return;

            ApplyRotationAngle(pivot, 0f, instant);
        }

        public Transform RotationPivot => GetRotationPivot();

        private Transform GetRotationPivot()
        {
            if (rotationPivot != null) return rotationPivot;
            if (Model != null) return Model.transform;
            return transform;
        }

        private void ApplyRotationAngle(Transform pivot, float targetAngle, bool instant, float dt = -1f)
        {
            if (instant || rotationSpeed <= 0f)
            {
                pivot.rotation = Quaternion.Euler(0f, 0f, targetAngle);
                return;
            }

            float actualDt = dt > 0f ? dt : Time.deltaTime;
            float currentAngle = pivot.eulerAngles.z;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * actualDt);
            pivot.rotation = Quaternion.Euler(0f, 0f, newAngle);
        }

        private TankBrain FindNearestTank(TankBrain excludeTank = null)
        {
            TankBrain[] allTanks = FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            TankBrain nearest = null;
            float minSqrDist = float.MaxValue;

            for (int i = 0; i < allTanks.Length; i++)
            {
                TankBrain t = allTanks[i];
                if (t == null || !t.IsAlive || t == excludeTank) continue;

                float sqrDist = ((Vector2)t.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    nearest = t;
                }
            }

            return nearest;
        }

        #endregion
    }
}
