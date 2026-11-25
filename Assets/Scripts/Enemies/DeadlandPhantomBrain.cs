using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Enemies.DeadlandPhantom;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Boss 6 (Ải 1-4 Phase 2) - "Bóng Ma Tử Địa" (Deadland Phantom):
    /// - Chỉ số chuẩn GDD: HP 2000 (Co-op: 4000), ATK 120, DEF 45, SPD 90.
    /// - Tốc độ di chuyển chuẩn hóa: MoveSpeed / 100 unit/s (điều chỉnh qua moveSpeedDivisor).
    /// - Cơ chế Xoay:
    ///   + Khi di chuyển: Tự động xoay theo hướng di chuyển.
    ///   + Khi dùng kỹ năng: Tự động xoay về hướng tấn công / mục tiêu.
    ///   + Nếu kỹ năng không cần mục tiêu (hoặc không có mục tiêu): Đặt góc quay về 0.
    /// - Cảm biến mục tiêu: Dùng EnemySensorArea (Collider2D Trigger + ColliderOwnerRef) để bám đuổi Tank gần nhất.
    /// - Mọi cấu hình đều được serialize trên Inspector, không gán cứng mã nguồn (no hardcode).
    /// - Bị động:
    ///   + Tàng hình chu kỳ stealthInterval (mờ alpha stealthAlpha trong stealthDuration).
    ///   + Quái thường chết có deathRiftSpawnChance tạo SpatialRift tại tọa độ tử trận.
    ///   + Giới hạn tối đa maxRiftsAllowed SpatialRift cùng lúc (FIFO: sinh vết nứt mới khi đầy sẽ xóa vết nứt lâu nhất).
    /// - Bộ kỹ năng đồng bộ Animator & VFXController:
    ///   + CastDeadlandSlash: Xoay về mục tiêu, kích hoạt DeadlandSlashHitbox.
    ///   + CastSoulSummon: Xoay về mục tiêu, kích hoạt SoulSummonPulseArea làm chậm và bắn soulProjectileCount đạn linh hồn.
    ///   + CastSpatialRift: Xoay về hướng sinh vết nứt, sinh SpatialRift tồn tại theo lifetime.
    /// - Vòng đời Bùng Nổ Năng Lượng (Ngưỡng HP <= burstHpThreshold, duy trì burstDuration):
    ///   + Charging: Trigger Charging, đặt góc quay về 0, chạy VFX tụ lực, khởi tạo DarkZoneSubArena.
    ///   + Active: Buff ATK/DEF, tạo Khiên Tứ Hướng, nhốt 1 Tank vào DarkZone, sinh DeadlandPhantomClone.
    ///   + Khiên Tứ Hướng: Mỗi shieldRotationInterval xoay góc mở (Đông -> Nam -> Tây -> Bắc).
    ///   + Thưởng / Phạt: Chuẩn hóa theo thiết kế GDD.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeadlandPhantomBrain : EliteEnemyBrain
    {
        [Header("Movement & Speed")]
        [Tooltip("Khoảng cách dừng tiếp cận Tank mục tiêu (m).")]
        [SerializeField, Min(0f)] private float stopDistance = 1.5f;

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

        [Tooltip("Có sử dụng flipX của SpriteRenderer khi di chuyển hay không (mặc định tắt vì đã xoay rotation).")]
        [SerializeField] private bool useFlipX = false;

        [Header("Skill Settings: Deadland Slash (Vết Chém Tử Địa)")]
        [Tooltip("Hồi chiêu Vết Chém Tử Địa (giây). GDD: 7s.")]
        [SerializeField, Min(0.1f)] private float slashCooldown = 7f;

        [Tooltip("Tầm kích hoạt chém (m). Nếu <= 0 sẽ tự đọc từ CircleCollider2D của Hitbox Prefab.")]
        [SerializeField] private float slashRange = 4.0f;

        [Header("Skill Settings: Soul Summon (Triệu Hồi Linh Hồn)")]
        [Tooltip("Hồi chiêu Triệu Hồi Linh Hồn (giây). GDD: 12s.")]
        [SerializeField, Min(0.1f)] private float soulSummonCooldown = 12f;

        [Tooltip("Bán kính vùng quét làm chậm của sóng xung kích (m).")]
        [SerializeField, Min(0.5f)] private float soulPulseRadius = 10f;

        [Tooltip("Tỷ lệ làm chậm (0.2 = 20%).")]
        [SerializeField, Range(0f, 1f)] private float soulPulseSlowRatio = 0.2f;

        [Tooltip("Thời gian làm chậm (giây). GDD: 5s.")]
        [SerializeField, Min(0.1f)] private float soulPulseSlowDuration = 5f;

        [Tooltip("Số đạn linh hồn ma hỏa bắn ra.")]
        [SerializeField, Range(1, 20)] private int soulProjectileCount = 5;

        [Tooltip("Tỷ lệ sát thương mỗi viên đạn theo ATK của Boss (0.5 = 50% ATK).")]
        [SerializeField, Range(0.05f, 2f)] private float soulProjectileDamageRatio = 0.5f;

        [Tooltip("Tốc độ bay của đạn linh hồn (m/s).")]
        [SerializeField, Min(1f)] private float soulProjectileSpeed = 8f;

        [Tooltip("Góc tỏa hình quạt khi bắn đạn (độ).")]
        [SerializeField, Range(10f, 180f)] private float soulSpreadArc = 60f;

        [Header("Skill Settings: Spatial Rift (Vết Nứt Không Gian)")]
        [Tooltip("Hồi chiêu Vết Nứt Không Gian (giây). GDD: 10s.")]
        [SerializeField, Min(0.1f)] private float riftCooldown = 10f;

        [Tooltip("Khoảng cách tối thiểu sinh vết nứt quanh mục tiêu (m).")]
        [SerializeField, Min(0.5f)] private float riftSpawnMinDistance = 5f;

        [Tooltip("Khoảng cách tối đa sinh vết nứt quanh mục tiêu (m).")]
        [SerializeField, Min(0.5f)] private float riftSpawnMaxDistance = 10f;

        [Tooltip("Số vết nứt tối đa tồn tại cùng lúc trên sân (FIFO). Chuẩn GDD: 5.")]
        [SerializeField, Min(1)] private int maxRiftsAllowed = 5;

        [Tooltip("Tỉ lệ sinh vết nứt khi quái thường tử trận (0.5 = 50%).")]
        [SerializeField, Range(0f, 1f)] private float deathRiftSpawnChance = 0.5f;

        [Header("Cardinal Shield (Khiên Tứ Hướng)")]
        [Tooltip("Tỷ lệ máu Khiên Tứ Hướng theo Max HP của Boss (0.4 = 40%).")]
        [SerializeField, Range(0.1f, 1f)] private float cardinalShieldRatio = 0.4f;

        [Tooltip("Chu kỳ xoay hướng mở của khiên (giây). GDD: 3s.")]
        [SerializeField, Min(0.5f)] private float shieldRotationInterval = 3.0f;

        [Tooltip("Tỷ lệ hồi phục khiên khi Tank bắn vào góc đóng (0.05 = +5% Max Shield).")]
        [SerializeField, Range(0f, 0.5f)] private float wrongAngleShieldHealRatio = 0.05f;

        [Tooltip("Tỷ lệ mất khiên cần tích lũy để kích hoạt hồi máu cho Tank trong DarkZone (0.10 = 10%).")]
        [SerializeField, Range(0.01f, 0.5f)] private float darkZoneHealShieldLossThreshold = 0.10f;

        [Tooltip("Tỷ lệ hồi máu cho Tank trong DarkZone mỗi lần đạt ngưỡng (0.10 = 10% Max HP).")]
        [SerializeField, Range(0.01f, 0.5f)] private float darkZoneHealRatio = 0.10f;

        [Header("DarkZone Sub-Arena Settings")]
        [Tooltip("Tọa độ không gian buồng tối trên Scene.")]
        [SerializeField] private Vector2 darkZoneCoordinates = new Vector2(100f, 100f);

        [Header("Burst Reward & Penalty Settings")]
        [Tooltip("Tỷ lệ máu mất của Boss khi người chơi giải cơ chế thành công (0.20 = 20% Max HP).")]
        [SerializeField, Range(0f, 1f)] private float rewardBossHpLossRatio = 0.20f;

        [Tooltip("Hệ số phòng thủ còn lại của Boss trong giai đoạn Thưởng (0.10 = giảm 90% DEF).")]
        [SerializeField, Range(0f, 1f)] private float rewardBossDefenseMultiplier = 0.10f;

        [Tooltip("Tỷ lệ tăng tấn công cho người chơi khi thắng cơ chế (0.25 = +25% ATK).")]
        [SerializeField, Range(0f, 1f)] private float rewardTankAttackBuff = 0.25f;

        [Tooltip("Tỷ lệ hồi máu cho Tank bị nhốt trong buồng tối khi thắng cơ chế (0.20 = 20% HP).")]
        [SerializeField, Range(0f, 1f)] private float rewardTrappedTankHealRatio = 0.20f;

        [Tooltip("Tỷ lệ hồi máu cho các Tank ngoài sân khi thắng cơ chế (0.10 = 10% HP).")]
        [SerializeField, Range(0f, 1f)] private float rewardOtherTankHealRatio = 0.10f;

        [Tooltip("Hệ số sát thương trừng phạt toàn bản đồ khi thất bại cơ chế (2.0 = 200% ATK).")]
        [SerializeField, Range(0.5f, 10f)] private float penaltyDamageMultiplier = 2.0f;

        [Tooltip("Tỷ lệ làm chậm trừng phạt khi thất bại (0.20 = 20%).")]
        [SerializeField, Range(0f, 1f)] private float penaltySlowRatio = 0.20f;

        [Tooltip("Thời gian duy trì làm chậm trừng phạt (giây). GDD: 10s.")]
        [SerializeField, Min(1f)] private float penaltySlowDuration = 10f;

        [Header("Prefabs & References")]
        [SerializeField] private GameObject deadlandSlashHitboxPrefab;
        [SerializeField] private GameObject soulSummonPulsePrefab;
        [SerializeField] private GameObject soulProjectilePrefab;
        [SerializeField] private GameObject spatialRiftPrefab;
        [SerializeField] private GameObject darkZoneSubArenaPrefab;
        [SerializeField] private GameObject clonePrefab;
        [SerializeField] private GameObject[] chapter1MinionPrefabs;

        // Runtime Controllers & Components
        private BongMaTuDiaVFXController m_VfxController;
        private EnemySensorArea m_SensorArea;
        private Rigidbody2D m_Rigidbody;
        private Animator m_Animator;
        private SpriteRenderer m_SpriteRenderer;

        // Movement & Target
        private Vector2 m_DesiredVelocity;
        private TankBrain m_CurrentTarget;

        // Skill Timers
        private float m_SlashTimer;
        private float m_SoulSummonTimer;
        private float m_RiftTimer;

        // Passive State
        private readonly List<SpatialRift> m_ActiveRifts = new List<SpatialRift>();

        // Cardinal Shield & Burst State
        private float m_MaxCardinalShield;
        [SerializeField, ReadOnly] private float m_CurrentCardinalShield;
        private bool m_CardinalShieldActive;
        private int m_CardinalShieldDirIndex;
        private float m_ShieldRotationTimer;
        private float m_CumulativeShieldLost;
        private DarkZoneSubArena m_DarkZoneArena;
        private float m_OriginalDefenseBeforeReward = -1f;

        // Rotation cycle: Đông (0°) -> Nam (270°) -> Tây (180°) -> Bắc (90°)
        private static readonly CardinalDirection[] CardinalRotationCycle = new CardinalDirection[]
        {
            CardinalDirection.East,
            CardinalDirection.South,
            CardinalDirection.West,
            CardinalDirection.North
        };

        #region Public Properties

        public float MoveSpeedDivisor => moveSpeedDivisor;
        public float SlashRange => slashRange;
        public float SoulPulseRadius => soulPulseRadius;
        public int SoulProjectileCount => soulProjectileCount;
        public int MaxRiftsAllowed => maxRiftsAllowed;
        public float CurrentCardinalShield => m_CurrentCardinalShield;
        public float MaxCardinalShield => m_MaxCardinalShield;
        public bool IsCardinalShieldActive => m_CardinalShieldActive;
        public CardinalDirection CurrentShieldDirection => CardinalRotationCycle[m_CardinalShieldDirIndex % 4];
        public float CumulativeShieldLost => m_CumulativeShieldLost;
        public float SlashTimer => m_SlashTimer;
        public float SoulSummonTimer => m_SoulSummonTimer;
        public float RiftTimer => m_RiftTimer;
        public IReadOnlyList<SpatialRift> ActiveRifts => m_ActiveRifts;
        public DarkZoneSubArena DarkZoneArena => m_DarkZoneArena;
        public EnemySensorArea SensorArea => m_SensorArea;
        public TankBrain CurrentTarget => m_CurrentTarget;

        public GameObject DeadlandSlashHitboxPrefab { get => deadlandSlashHitboxPrefab; set => deadlandSlashHitboxPrefab = value; }
        public GameObject SoulSummonPulsePrefab { get => soulSummonPulsePrefab; set => soulSummonPulsePrefab = value; }
        public GameObject SoulProjectilePrefab { get => soulProjectilePrefab; set => soulProjectilePrefab = value; }
        public GameObject SpatialRiftPrefab { get => spatialRiftPrefab; set => spatialRiftPrefab = value; }
        public GameObject DarkZoneSubArenaPrefab { get => darkZoneSubArenaPrefab; set => darkZoneSubArenaPrefab = value; }
        public GameObject ClonePrefab { get => clonePrefab; set => clonePrefab = value; }
        public GameObject[] Chapter1MinionPrefabs { get => chapter1MinionPrefabs; set => chapter1MinionPrefabs = value; }

        #endregion

        #region Initialization

        protected override void Awake()
        {
            base.Awake();

            burstAttackMultiplier = 1.25f;
            burstDefenseMultiplier = 3.0f;

            m_VfxController = GetComponent<BongMaTuDiaVFXController>();
            m_SensorArea = GetComponentInChildren<EnemySensorArea>();
            m_Rigidbody = GetComponent<Rigidbody2D>();
            m_Animator = GetComponentInChildren<Animator>();
            if (Model != null) m_SpriteRenderer = Model.SpriteRenderer;
            if (m_SpriteRenderer == null) m_SpriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (m_Rigidbody != null)
            {
                m_Rigidbody.bodyType = RigidbodyType2D.Kinematic;
                m_Rigidbody.gravityScale = 0f;
            }
        }

        public override void Initialize(EnemyStats overrideStats = null)
        {
            base.Initialize(overrideStats);

            // Khởi tạo các bộ đếm kỹ năng
            m_SlashTimer = slashCooldown * 0.5f;
            m_SoulSummonTimer = soulSummonCooldown * 0.3f;
            m_RiftTimer = riftCooldown * 0.4f;
            m_CardinalShieldActive = false;
            m_CumulativeShieldLost = 0f;

            // Đăng ký sự kiện quái thường tử trận
            EnemyBrain.OnAnyEnemyDied -= HandleAnyEnemyDied;
            EnemyBrain.OnAnyEnemyDied += HandleAnyEnemyDied;
        }

        protected virtual void OnDestroy()
        {
            EnemyBrain.OnAnyEnemyDied -= HandleAnyEnemyDied;
            ClearAllRifts();
            if (m_DarkZoneArena != null)
            {
                m_DarkZoneArena.CleanupArena();
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
                RotateToZero();
                return;
            }

            // 1. Cảm biến mục tiêu: Tìm Tank gần nhất
            UpdateTarget();

            // 2. Xử lý di chuyển Kinematic bám đuổi & Xoay theo hướng di chuyển
            UpdateKinematicMovement(deltaTime);

            // 3. Xử lý bộ chiêu thức (trong lúc bùng nổ Active và sau khi kết thúc Reward/Penalty/Finished)
            if (!IsBurstCharging)
            {
                UpdateSkills(deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (!IsAlive || m_Rigidbody == null) return;

            if (m_Rigidbody.bodyType == RigidbodyType2D.Kinematic)
            {
                m_Rigidbody.MovePosition(m_Rigidbody.position + m_DesiredVelocity * Time.fixedDeltaTime);
            }
            else
            {
                m_Rigidbody.linearVelocity = m_DesiredVelocity;
            }
        }

        private void UpdateTarget()
        {
            TankBrain trappedTank = (m_DarkZoneArena != null && m_DarkZoneArena.IsTankTrapped) ? m_DarkZoneArena.TrappedTank : null;
            if (m_SensorArea == null) m_SensorArea = GetComponentInChildren<EnemySensorArea>();
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
            TankBrain trappedTank = (m_DarkZoneArena != null && m_DarkZoneArena.IsTankTrapped) ? m_DarkZoneArena.TrappedTank : null;
            if (m_CurrentTarget == null || !m_CurrentTarget.IsAlive || m_CurrentTarget == trappedTank)
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
                float actualMoveSpeed = (Stats != null ? Stats.MoveSpeed : 0f) / divisor;
                targetVelocity = toTarget.normalized * actualMoveSpeed;
            }

            float t = 1f - Mathf.Exp(-turnSharpness * deltaTime);
            m_DesiredVelocity = Vector2.Lerp(m_DesiredVelocity, targetVelocity, t);
            Model?.SetMoveSpeed(m_DesiredVelocity.magnitude);

            // Tự động xoay theo hướng di chuyển khi đang di chuyển
            if (m_DesiredVelocity.sqrMagnitude > 0.001f)
            {
                RotateTowardsDirection(m_DesiredVelocity, instant: false, dt: deltaTime);
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

            if (useFlipX)
            {
                UpdateFlipX(direction.x);
            }
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
            }
            else
            {
                float effectiveDt = dt > 0f ? dt : Time.deltaTime;
                float currentAngle = pivot.eulerAngles.z;
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * effectiveDt);
                pivot.rotation = Quaternion.Euler(0f, 0f, newAngle);
            }
        }

        private void UpdateFlipX(float dirX)
        {
            if (Mathf.Abs(dirX) > 0.01f)
            {
                if (m_SpriteRenderer == null && Model != null) m_SpriteRenderer = Model.SpriteRenderer;
                if (m_SpriteRenderer != null)
                {
                    m_SpriteRenderer.flipX = dirX < 0f;
                }
            }
        }

        #endregion

        #region Passive: Vết nứt quái chết

        protected override void OnDeath()
        {
            base.OnDeath();
            ClearAllRifts();
            IronWasteland.Net.NetEntity.Despawn(gameObject);
        }

        private void HandleAnyEnemyDied(EnemyBrain enemy)
        {
            if (this == null || gameObject == null || !IsAlive) return;
            if (enemy == null || enemy == this || enemy.IsElite) return;
            if (enemy is DeadlandPhantomClone) return;

            if (UnityEngine.Random.value <= deathRiftSpawnChance)
            {
                CreateSpatialRift(enemy.transform.position);
            }
        }

        #endregion

        #region Skills (Bộ Kỹ Năng)

        private void UpdateSkills(float deltaTime)
        {
            if (m_CurrentTarget == null || !m_CurrentTarget.IsAlive) return;

            float distance = Vector2.Distance(transform.position, m_CurrentTarget.transform.position);

            m_SlashTimer += deltaTime;
            m_SoulSummonTimer += deltaTime;
            m_RiftTimer += deltaTime;

            // Nếu đang trong thời gian giãn cách kỹ năng toàn cục (GlobalSkillInterval), không thi triển chiêu mới
            if (!CanCastAnySkill) return;

            if (m_SlashTimer >= slashCooldown && distance <= slashRange)
            {
                m_SlashTimer = 0f;
                CastDeadlandSlash();
                return;
            }

            if (m_SoulSummonTimer >= soulSummonCooldown)
            {
                m_SoulSummonTimer = 0f;
                CastSoulSummon();
                return;
            }

            if (m_RiftTimer >= riftCooldown)
            {
                m_RiftTimer = 0f;
                CastSpatialRift();
            }
        }

        /// <summary>
        /// Kỹ năng 1: Vết Chém Tử Địa (DeadlandSlash).
        /// Tự động xoay về hướng Tank mục tiêu và chém 360 độ.
        /// </summary>
        public void CastDeadlandSlash()
        {
            if (m_CurrentTarget != null && m_CurrentTarget.IsAlive)
            {
                RotateTowardsTarget(m_CurrentTarget.transform.position, instant: true);
            }
            else
            {
                RotateToZero(instant: true);
            }

            if (m_Animator != null) m_Animator.SetTrigger("DeadlandSlash");

            if (m_VfxController != null)
            {
                m_VfxController.PlayDeadlandSlash(transform.position);
            }

            TriggerGlobalSkillCooldown();
            SpawnSlashHitbox();
        }

        private void SpawnSlashHitbox()
        {
            if (deadlandSlashHitboxPrefab == null)
            {
                Debug.LogWarning($"[DeadlandPhantomBrain] deadlandSlashHitboxPrefab is not assigned on '{gameObject.name}'. Cannot spawn Deadland Slash hitbox.");
                return;
            }

            GameObject hitboxObj = Instantiate(deadlandSlashHitboxPrefab, transform.position, Quaternion.identity);
            DeadlandSlashHitbox hitbox = hitboxObj.GetComponent<DeadlandSlashHitbox>();
            if (hitbox == null)
            {
                Debug.LogError($"[DeadlandPhantomBrain] Prefab '{deadlandSlashHitboxPrefab.name}' is missing DeadlandSlashHitbox component! Please attach DeadlandSlashHitbox to the prefab.");
                return;
            }

            hitbox.Initialize(this, Stats != null ? Stats.Attack : 120f, customRadius: slashRange);
        }

        /// <summary>
        /// Kỹ năng 2: Triệu Hồi Linh Hồn (SoulSummon).
        /// Tự động xoay về hướng Tank mục tiêu (nếu có), làm chậm Tank trong bán kính và bắn đạn linh hồn.
        /// </summary>
        public void CastSoulSummon()
        {
            TankBrain target = m_CurrentTarget ?? FindNearestTank();

            if (target != null && target.IsAlive)
            {
                RotateTowardsTarget(target.transform.position, instant: true);
            }
            else
            {
                RotateToZero(instant: true);
            }

            if (m_Animator != null) m_Animator.SetTrigger("SoulSummon");

            if (m_VfxController != null)
            {
                m_VfxController.PlaySoulSummonPulse(transform.position);
            }

            TriggerGlobalSkillCooldown();

            // 1. Quét làm chậm Tank trong phạm vi thông qua Trigger
            ApplySoulSummonPulseSlow();

            // 2. Bắn đạn linh hồn truy đuổi Tank mục tiêu
            if (target != null)
            {
                float projDamage = Stats.Attack * soulProjectileDamageRatio;
                Vector2 toTarget = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
                if (toTarget.sqrMagnitude < 0.001f) toTarget = Vector2.right;

                float baseAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                float angleStep = soulProjectileCount > 1 ? soulSpreadArc / (soulProjectileCount - 1) : 0f;
                float startAngle = baseAngle - soulSpreadArc * 0.5f;

                for (int i = 0; i < soulProjectileCount; i++)
                {
                    float currentAngle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle));

                    if (m_VfxController != null)
                    {
                        m_VfxController.SpawnSoulProjectile(transform.position, target.transform, dir, speed: soulProjectileSpeed, damage: projDamage, owner: this);
                    }
                    else if (soulProjectilePrefab != null)
                    {
                        GameObject go = Instantiate(soulProjectilePrefab, transform.position, Quaternion.identity);
                        DeadlandSoulProjectile proj = go.GetComponent<DeadlandSoulProjectile>();
                        if (proj != null)
                        {
                            proj.Initialize(target.transform, dir, speed: soulProjectileSpeed, damage: projDamage, owner: this);
                        }
                    }
                    else
                    {
                        SpawnFallbackSoulProjectile(target, dir, projDamage);
                    }
                }
            }
        }

        private void ApplySoulSummonPulseSlow()
        {
            // Nếu có prefab pulse area chuyên biệt -> spawn và để collider trigger tự xử lý
            if (soulSummonPulsePrefab != null)
            {
                GameObject pulseObj = Instantiate(soulSummonPulsePrefab, transform.position, Quaternion.identity);
                SoulSummonPulseArea area = pulseObj.GetComponent<SoulSummonPulseArea>();
                if (area != null)
                {
                    area.Initialize(this, soulPulseRadius, soulPulseSlowRatio, soulPulseSlowDuration);
                }
                return;
            }
        }

        private void SpawnFallbackSoulProjectile(TankBrain target, Vector2 dir, float damage)
        {
            Debug.LogWarning($"[DeadlandPhantomBrain] BongMaTuDiaVFXController is missing on '{gameObject.name}'. Cannot spawn soul projectile.");
        }

        /// <summary>
        /// Kỹ năng 3: Mở Vết Nứt Không Gian (CastSpatialRift).
        /// Tự động xoay về hướng sinh vết nứt.
        /// </summary>
        public void CastSpatialRift()
        {
            TankBrain target = m_CurrentTarget ?? FindNearestTank();
            Vector2 targetPos = target != null ? (Vector2)target.transform.position : (Vector2)transform.position;

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float dist = UnityEngine.Random.Range(riftSpawnMinDistance, riftSpawnMaxDistance);
            Vector2 spawnPos = targetPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

            RotateTowardsTarget(spawnPos, instant: true);

            if (m_Animator != null) m_Animator.SetTrigger("CastRift");

            TriggerGlobalSkillCooldown();
            CreateSpatialRift(spawnPos);
        }

        public SpatialRift CreateSpatialRift(Vector2 worldPos)
        {
            if (this == null || gameObject == null) return null;

            m_ActiveRifts.RemoveAll(r => r == null);

            // FIFO: Tiêu hủy vết nứt lâu nhất nếu đã đạt giới hạn
            while (m_ActiveRifts.Count >= maxRiftsAllowed)
            {
                SpatialRift oldest = m_ActiveRifts[0];
                m_ActiveRifts.RemoveAt(0);
                if (oldest != null)
                {
                    oldest.DestroyRift();
                }
            }

            if (spatialRiftPrefab == null)
            {
                Debug.LogWarning($"[DeadlandPhantomBrain] spatialRiftPrefab is not assigned on '{gameObject.name}'. Cannot create Spatial Rift.");
                return null;
            }

            GameObject riftObj = Instantiate(spatialRiftPrefab, worldPos, Quaternion.identity);
            SpatialRift rift = riftObj.GetComponent<SpatialRift>();
            if (rift == null)
            {
                Debug.LogError($"[DeadlandPhantomBrain] Prefab '{spatialRiftPrefab.name}' is missing SpatialRift component! Please attach SpatialRift to the prefab.");
                return null;
            }

            rift.Initialize(this, chapter1MinionPrefabs);
            rift.OnRiftDestroyed += HandleRiftDestroyed;

            m_ActiveRifts.Add(rift);
            return rift;
        }

        private void HandleRiftDestroyed(SpatialRift rift)
        {
            if (rift != null)
            {
                rift.OnRiftDestroyed -= HandleRiftDestroyed;
                m_ActiveRifts.Remove(rift);
            }
        }

        private void ClearAllRifts()
        {
            for (int i = 0; i < m_ActiveRifts.Count; i++)
            {
                if (m_ActiveRifts[i] != null)
                {
                    m_ActiveRifts[i].OnRiftDestroyed -= HandleRiftDestroyed;
                    m_ActiveRifts[i].DestroyRift();
                }
            }
            m_ActiveRifts.Clear();
        }

        #endregion

        #region Energy Burst Lifecycle (Bùng Nổ Năng Lượng)

        protected override void OnBurstChargingStart()
        {
            // Vận sức là kỹ năng bản thân, không cần mục tiêu -> Đặt góc quay về 0
            RotateToZero(instant: true);

            if (m_Animator != null) m_Animator.SetTrigger("Charging");

            if (m_VfxController != null)
            {
                m_VfxController.StartBurstCharging(burstChargeTime);
            }

            InitializeDarkZoneSubArena();
        }

        private void InitializeDarkZoneSubArena()
        {
            if (m_DarkZoneArena != null) return;

            if (darkZoneSubArenaPrefab == null)
            {
                Debug.LogWarning($"[DeadlandPhantomBrain] darkZoneSubArenaPrefab is not assigned on '{gameObject.name}'. Cannot initialize DarkZone Sub-Arena.");
                return;
            }

            GameObject arenaObj = Instantiate(darkZoneSubArenaPrefab, darkZoneCoordinates, Quaternion.identity);
            m_DarkZoneArena = arenaObj.GetComponent<DarkZoneSubArena>();
            if (m_DarkZoneArena == null)
            {
                Debug.LogError($"[DeadlandPhantomBrain] Prefab '{darkZoneSubArenaPrefab.name}' is missing DarkZoneSubArena component! Please attach DarkZoneSubArena to the prefab.");
                return;
            }

            m_DarkZoneArena.InitializeArena(this, darkZoneCoordinates);
        }

        protected override void OnBurstStart()
        {
            if (m_VfxController != null)
            {
                m_VfxController.StopBurstCharging();
            }

            if (m_DarkZoneArena == null)
            {
                InitializeDarkZoneSubArena();
            }

            // Thiết lập hồi chiêu sẵn sàng để dùng kỹ năng ngay khi bước vào Bùng nổ (Active)
            m_SlashTimer = slashCooldown;
            m_SoulSummonTimer = soulSummonCooldown;
            m_RiftTimer = riftCooldown;
            ResetGlobalSkillCooldown();

            // 1. Tạo Khiên Tứ Hướng
            m_MaxCardinalShield = MaxHealth * cardinalShieldRatio;
            m_CurrentCardinalShield = m_MaxCardinalShield;
            m_CardinalShieldActive = true;
            m_CardinalShieldDirIndex = 0; // Hướng Đông (0°)
            m_ShieldRotationTimer = 0f;
            m_CumulativeShieldLost = 0f;

            if (m_VfxController != null)
            {
                m_VfxController.ActivateCardinalShield(CurrentShieldDirection);
            }

            // 2. Kéo Tank gần nhất vào Sub-Arena DarkZone
            TankBrain nearestTank = m_SensorArea != null
                ? m_SensorArea.GetNearestTarget(transform.position)
                : FindNearestTank();

            if (nearestTank != null && m_DarkZoneArena != null)
            {
                m_DarkZoneArena.TrapTank(nearestTank, clonePrefab);
            }
        }

        protected override void OnBurstTick(float deltaTime)
        {
            if (!m_CardinalShieldActive) return;

            m_ShieldRotationTimer += deltaTime;
            if (m_ShieldRotationTimer >= shieldRotationInterval)
            {
                m_ShieldRotationTimer = 0f;
                RotateCardinalShield();
            }
        }

        private void RotateCardinalShield()
        {
            m_CardinalShieldDirIndex = (m_CardinalShieldDirIndex + 1) % CardinalRotationCycle.Length;
            CardinalDirection newDir = CurrentShieldDirection;

            if (m_VfxController != null)
            {
                m_VfxController.SetCardinalShieldDirection(newDir);
            }

            if (m_DarkZoneArena != null && m_DarkZoneArena.Clone != null)
            {
                m_DarkZoneArena.Clone.SyncShieldDirection(newDir);
            }
        }

        public void SetCardinalDirection(CardinalDirection dir)
        {
            for (int i = 0; i < CardinalRotationCycle.Length; i++)
            {
                if (CardinalRotationCycle[i] == dir)
                {
                    m_CardinalShieldDirIndex = i;
                    break;
                }
            }

            if (m_VfxController != null)
            {
                m_VfxController.SetCardinalShieldDirection(dir);
            }

            if (m_DarkZoneArena != null && m_DarkZoneArena.Clone != null)
            {
                m_DarkZoneArena.Clone.SyncShieldDirection(dir);
            }
        }

        #endregion

        #region Cardinal Shield Combat & Damage Processing

        public override void TakeDamage(DamageInfo info)
        {
            if (!IsAlive) return;

            if (IsBurstActive && m_CardinalShieldActive)
            {
                Vector2 attackerPos = (Vector2)transform.position;
                bool hasAttacker = false;

                if (info.Attacker is Component comp && comp != null)
                {
                    attackerPos = comp.transform.position;
                    hasAttacker = true;
                }
                else if (info.Attacker is GameObject go && go != null)
                {
                    attackerPos = go.transform.position;
                    hasAttacker = true;
                }

                if (!hasAttacker && info.HitPosition != Vector2.zero)
                {
                    attackerPos = info.HitPosition;
                    hasAttacker = true;
                }

                Vector2 hitPoint = info.HitPosition != Vector2.zero ? info.HitPosition : (Vector2)transform.position;
                bool isBlocked = !IsDirectionOpen(attackerPos);

                if (isBlocked)
                {
                    HealCardinalShield(m_MaxCardinalShield * wrongAngleShieldHealRatio);
                    if (m_VfxController != null) m_VfxController.PlayCardinalReflect(hitPoint);
                    return;
                }
                else
                {
                    float finalDmg = CombatUtility.ResolveDamage(info.RawDamage, Stats.Defense, info.ArmorPenetration);
                    DamageCardinalShield(finalDmg);
                    return;
                }
            }

            base.TakeDamage(info);
        }

        public bool IsDirectionOpen(Vector2 worldAttackerPos)
        {
            Vector2 relativeDir = (worldAttackerPos - (Vector2)transform.position).normalized;
            CardinalDirection incomingQuad = CardinalShieldVFX.GetDirectionFromVector(relativeDir);
            return incomingQuad == CurrentShieldDirection;
        }

        public void DamageCardinalShield(float amount)
        {
            if (!m_CardinalShieldActive || amount <= 0f) return;

            float prevShield = m_CurrentCardinalShield;
            m_CurrentCardinalShield = Mathf.Max(0f, m_CurrentCardinalShield - amount);
            float actualDamageDealt = prevShield - m_CurrentCardinalShield;

            // Tích lũy lượng khiên mất: Cứ mất darkZoneHealShieldLossThreshold thì hồi darkZoneHealRatio cho Tank buồng tối
            m_CumulativeShieldLost += actualDamageDealt;
            float healThreshold = m_MaxCardinalShield * darkZoneHealShieldLossThreshold;

            while (m_CumulativeShieldLost >= healThreshold)
            {
                m_CumulativeShieldLost -= healThreshold;
                if (m_DarkZoneArena != null)
                {
                    m_DarkZoneArena.HealTrappedTank(darkZoneHealRatio);
                }
            }

            if (m_CurrentCardinalShield <= 0f)
            {
                m_CardinalShieldActive = false;
                if (m_VfxController != null) m_VfxController.DeactivateCardinalShield();
                if (m_DarkZoneArena != null && m_DarkZoneArena.Clone != null)
                {
                    m_DarkZoneArena.Clone.DeactivateShield();
                }
                CompleteBurstSuccess();
            }
        }

        public void DamageCardinalShieldFromClone(float damage)
        {
            DamageCardinalShield(damage);
        }

        public void HealCardinalShield(float amount)
        {
            if (!m_CardinalShieldActive || amount <= 0f) return;
            m_CurrentCardinalShield = Mathf.Min(m_MaxCardinalShield, m_CurrentCardinalShield + amount);
        }

        #endregion

        #region Reward & Penalty Execution

        protected override void OnBurstSuccess()
        {
            m_CardinalShieldActive = false;
            if (m_VfxController != null) m_VfxController.DeactivateCardinalShield();
            if (m_DarkZoneArena != null && m_DarkZoneArena.Clone != null)
            {
                m_DarkZoneArena.Clone.DeactivateShield();
            }

            RevertBurstStatModifiers();

            //Thiết lập lại thời gian hồi
            m_SlashTimer = slashCooldown;
            m_SoulSummonTimer = soulSummonCooldown;
            m_RiftTimer = riftCooldown;
            ResetGlobalSkillCooldown();

            // 1. Boss mất rewardBossHpLossRatio máu tối đa
            float hpLoss = MaxHealth * rewardBossHpLossRatio;
            TakeDamage(new DamageInfo(null, this, hpLoss, armorPenetration: 100f, isDirectToHealth: true));
            TextDamageManager.Instance?.Show(hpLoss, transform.position);

            // 2. Boss giảm phòng thủ còn rewardBossDefenseMultiplier trong rewardPhaseDuration
            if (Stats != null)
            {
                m_OriginalDefenseBeforeReward = Stats.Defense;
                Stats.Defense = m_OriginalDefenseBeforeReward * rewardBossDefenseMultiplier;
            }

            // 3. Tank nhận buff tấn công và hồi máu
            TankBrain trappedTank = m_DarkZoneArena != null ? m_DarkZoneArena.TrappedTank : null;
            TankBrain[] allTanks = FindObjectsByType<TankBrain>();

            for (int i = 0; i < allTanks.Length; i++)
            {
                TankBrain t = allTanks[i];
                if (t == null || !t.IsAlive) continue;

                t.AddBonusStats("PhantomReward_AtkBuff", null, new TankStats { Attack = rewardTankAttackBuff });
                if (Application.isPlaying)
                {
                    StartCoroutine(RemoveTankAtkBuffAfterDelay(t, rewardPhaseDuration));
                }
                else
                {
                    t.RemoveBonusStats("PhantomReward_AtkBuff");
                }

                float healRatio = (t == trappedTank) ? rewardTrappedTankHealRatio : rewardOtherTankHealRatio;
                t.Heal(t.Stats.MaxHealth * healRatio);
            }

            // 4. Trả Tank về sân chính và dọn dẹp DarkZone
            if (m_DarkZoneArena != null)
            {
                m_DarkZoneArena.ReleaseTank();
                m_DarkZoneArena.CleanupArena();
                m_DarkZoneArena = null;
            }
        }

        private IEnumerator RemoveTankAtkBuffAfterDelay(TankBrain tank, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (tank != null)
            {
                tank.RemoveBonusStats("PhantomReward_AtkBuff");
            }
        }

        protected override void OnBurstFail()
        {
            m_CardinalShieldActive = false;
            if (m_VfxController != null) m_VfxController.DeactivateCardinalShield();
            if (m_DarkZoneArena != null && m_DarkZoneArena.Clone != null)
            {
                m_DarkZoneArena.Clone.DeactivateShield();
            }

            //Thiết lập lại thời gian hồi
            m_SlashTimer = slashCooldown;
            m_SoulSummonTimer = soulSummonCooldown;
            m_RiftTimer = riftCooldown;
            ResetGlobalSkillCooldown();

            // 1. Nổ trừng phạt toàn map và làm chậm Tank
            float penaltyDamage = Stats.Attack * penaltyDamageMultiplier;
            TankBrain[] allTanks = FindObjectsByType<TankBrain>();

            for (int i = 0; i < allTanks.Length; i++)
            {
                TankBrain t = allTanks[i];
                if (t == null || !t.IsAlive) continue;

                DamageInfo info = new DamageInfo(
                    this,
                    t,
                    penaltyDamage,
                    0f,
                    t.transform.position,
                    sourceType: DamageSourceType.EnemyAttack);

                t.TakeDamage(info);
                TextDamageManager.Instance?.Show(info.FinalDamage, t.transform.position);

                TankSlowDebuff.Apply(t, penaltySlowRatio, penaltySlowDuration);
            }

            // 2. Trả Tank về sân chính và dọn dẹp DarkZone
            if (m_DarkZoneArena != null)
            {
                m_DarkZoneArena.ReleaseTank();
                m_DarkZoneArena.CleanupArena();
                m_DarkZoneArena = null;
            }
        }

        protected override void OnBurstEnded()
        {
            base.OnBurstEnded();

            if (m_OriginalDefenseBeforeReward >= 0f && Stats != null)
            {
                Stats.Defense = m_OriginalDefenseBeforeReward;
                m_OriginalDefenseBeforeReward = -1f;
            }

            if (m_DarkZoneArena != null)
            {
                m_DarkZoneArena.CleanupArena();
                m_DarkZoneArena = null;
            }

            if (m_VfxController != null)
            {
                m_VfxController.DeactivateCardinalShield();
            }
        }

        public override void ResetBurstManual()
        {
            base.ResetBurstManual();
            if (m_OriginalDefenseBeforeReward >= 0f && Stats != null)
            {
                Stats.Defense = m_OriginalDefenseBeforeReward;
                m_OriginalDefenseBeforeReward = -1f;
            }
            m_CardinalShieldActive = false;
            if (m_VfxController != null) m_VfxController.DeactivateCardinalShield();
        }

        #endregion

        #region Helpers & Test Support

        private TankBrain FindNearestTank(TankBrain excludeTank = null)
        {
            TankBrain[] allTanks = FindObjectsByType<TankBrain>();
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
