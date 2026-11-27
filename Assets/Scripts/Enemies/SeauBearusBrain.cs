using System.Collections;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Enemies.DeadlandPhantom;
using IronWasteland.Enemies.SeauBearus;
using IronWasteland.Stages;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Boss 7 - SeauBearus (BOSS Tinh Anh, màn 1-4).
    /// HP 1800 / ATK 180 / Giáp 40 / SPD 85.
    /// - Bị động: mỗi 10s rút 10% HP hiện tại của Tank mục tiêu và hồi đúng lượng đó.
    /// - Phun lửa: hình quạt 64 độ, tầm 9m, 100% ATK; để lại 3 vết lửa 8s
    ///   gây 20% ATK mỗi 0.5s và làm chậm 20%.
    /// - Gầm thét: làm chậm 30% trong 10m; sau 0.5s Boss +25% ATK +30% SPD 5s
    ///   và tạo cột lửa dưới chân tất cả Tank gây 100% ATK.
    /// - Khiên gai: +25% DEF 10s, phản 25% ATK cho mọi đòn đánh trúng Boss.
    /// - Năng lượng hủy diệt (HP < 60%): dịch chuyển về tâm sân, +25% ATK +50% DEF
    ///   trong 35s, tạo Khiên Đỏ/Khiên Xanh, bắn tia lửa truy đuổi Tank xa nhất
    ///   mỗi 0.5s và triệu hồi Mặt Trăng Đen mỗi 10s.
    /// </summary>
    [DisallowMultipleComponent]
    public class SeauBearusBrain : EliteEnemyBrain, IronWasteland.Net.INetVfxReceiver
    {
        private const string RewardAttackKey = "SeauReward_AtkBuff";

        private static readonly Color RedShieldColor = new Color(0.95f, 0.18f, 0.12f, 0.45f);
        private static readonly Color BlueShieldColor = new Color(0.15f, 0.55f, 1f, 0.45f);
        private static readonly Color SpikeShieldColor = new Color(1f, 0.62f, 0.12f, 0.35f);

        [Header("Phun lửa")]
        [Tooltip("Hồi chiêu phun lửa (giây).")]
        [SerializeField, Min(1f)] private float flameCooldown = 5f;

        [Tooltip("Tầm phun lửa (m).")]
        [SerializeField, Min(1f)] private float flameRange = 9f;

        [Tooltip("Nửa góc của hình quạt (độ).")]
        [SerializeField, Range(5f, 90f)] private float flameHalfAngle = 32f;

        [Tooltip("Tỷ lệ sát thương của đòn phun lửa so với Tấn công.")]
        [SerializeField, Min(0f)] private float flameDamageRatio = 1f;

        [Tooltip("Hình quạt lửa hiển thị khi phun lửa.")]
        [SerializeField] private SpriteRenderer fireConeVisual;

        [Tooltip("Thời gian hiển thị hình quạt lửa (giây).")]
        [SerializeField, Min(0.1f)] private float fireConeDuration = 0.55f;

        [Tooltip("Tỷ lệ sát thương mỗi tick của vết lửa so với Tấn công.")]
        [SerializeField, Min(0f)] private float trailDamageRatio = 0.2f;

        [Tooltip("Số vết lửa để lại trên đường phun.")]
        [SerializeField, Range(1, 6)] private int trailPatchCount = 3;

        [Header("Vết lửa")]
        [Tooltip("Bán kính mỗi vết lửa (m).")]
        [SerializeField, Min(0.1f)] private float trailPatchRadius = 1.6f;

        [Tooltip("Thời gian tồn tại của vết lửa (giây).")]
        [SerializeField, Min(0.5f)] private float trailPatchLifetime = 8f;

        [Tooltip("Chu kỳ gây sát thương của vết lửa (giây).")]
        [SerializeField, Min(0.1f)] private float trailTickInterval = 0.5f;

        [Tooltip("Mức làm chậm của vết lửa (0..1).")]
        [SerializeField, Range(0f, 1f)] private float trailSlowPercent = 0.2f;

        [Tooltip("Thời gian làm chậm của vết lửa (giây).")]
        [SerializeField, Min(0f)] private float trailSlowDuration = 2f;

        [Header("Gầm thét")]
        [Tooltip("Hồi chiêu gầm thét (giây).")]
        [SerializeField, Min(1f)] private float roarCooldown = 9f;

        [Tooltip("Bán kính gầm thét (m).")]
        [SerializeField, Min(1f)] private float roarRadius = 10f;

        [Tooltip("Mức làm chậm của tiếng gầm (0..1).")]
        [SerializeField, Range(0f, 1f)] private float roarSlowPercent = 0.3f;

        [Tooltip("Thời gian làm chậm (giây).")]
        [SerializeField, Min(0f)] private float roarSlowDuration = 3f;

        [Tooltip("Trễ trước khi Boss nhận buff sau tiếng gầm (giây).")]
        [SerializeField, Min(0f)] private float roarBuffDelay = 0.5f;

        [Tooltip("Thời gian buff Tấn công/Tốc độ của Boss (giây).")]
        [SerializeField, Min(0.1f)] private float roarBuffDuration = 5f;

        [Tooltip("Tỷ lệ buff Tấn công của Boss (0.25 = +25%).")]
        [SerializeField, Range(0f, 1f)] private float roarAttackBonus = 0.25f;

        [Tooltip("Tỷ lệ buff Tốc độ của Boss (0.30 = +30%).")]
        [SerializeField, Range(0f, 1f)] private float roarSpeedBonus = 0.3f;

        [Tooltip("Tỷ lệ sát thương cột lửa so với Tấn công.")]
        [SerializeField, Min(0f)] private float roarPillarDamageRatio = 1f;

        [Tooltip("Bán kính hiển thị cột lửa (m).")]
        [SerializeField, Min(0.1f)] private float roarPillarRadius = 1.3f;

        [Tooltip("Thời gian tồn tại cột lửa (giây).")]
        [SerializeField, Min(0.1f)] private float roarPillarLifetime = 0.8f;

        [Header("Khiên gai")]
        [Tooltip("Hồi chiêu khiên gai (giây).")]
        [SerializeField, Min(1f)] private float spikeCooldown = 14f;

        [Tooltip("Thời gian hiệu lực khiên gai (giây).")]
        [SerializeField, Min(0.1f)] private float spikeDuration = 10f;

        [Tooltip("Tỷ lệ buff Giáp của khiên gai (0.25 = +25%).")]
        [SerializeField, Range(0f, 1f)] private float spikeDefenseBonus = 0.25f;

        [Tooltip("Tỷ lệ phản sát thương so với Tấn công của Boss (0.25 = 25%).")]
        [SerializeField, Range(0f, 2f)] private float spikeReflectRatio = 0.25f;

        [Header("Hút máu")]
        [Tooltip("Chu kỳ hút máu (giây).")]
        [SerializeField, Min(1f)] private float lifestealInterval = 10f;

        [Tooltip("Tỷ lệ HP hiện tại của mục tiêu bị hút mỗi lần (0..1).")]
        [SerializeField, Range(0f, 1f)] private float lifestealPercent = 0.1f;

        [Header("Di chuyển")]
        [Tooltip("Khoảng cách đứng trước mục tiêu (m).")]
        [SerializeField, Min(0.1f)] private float stopDistance = 1.5f;

        [Tooltip("Độ mượt khi đổi hướng di chuyển và xoay mặt.")]
        [SerializeField, Min(0.1f)] private float turnSharpness = 14f;

        [Tooltip("Hệ số chia Tốc độ để đổi ra đơn vị/giây.")]
        [SerializeField, Min(1f)] private float moveSpeedDivisor = 100f;

        [Tooltip("Góc lệch khi xoay model về phía mục tiêu.")]
        [SerializeField] private float rotationOffset = 0f;

        [Tooltip("Có xoay model về phía mục tiêu không.")]
        [SerializeField] private bool faceTarget = true;

        [Header("Năng lượng hủy diệt - Khiên")]
        [Tooltip("Tỷ lệ HP tối đa của Khiên Đỏ (0.3 = 30%).")]
        [SerializeField, Range(0f, 1f)] private float redShieldHpRatio = 0.3f;

        [Tooltip("Thời gian tồn tại của Khiên Xanh (giây).")]
        [SerializeField, Min(0f)] private float blueShieldDuration = 30f;

        [Tooltip("Hình ảnh Khiên Đỏ (bên phải Boss).")]
        [SerializeField] private SpriteRenderer redShieldVisual;

        [Tooltip("Hình ảnh Khiên Xanh (bên trái Boss).")]
        [SerializeField] private SpriteRenderer blueShieldVisual;

        [Tooltip("Hình ảnh Khiên Gai quanh Boss.")]
        [SerializeField] private SpriteRenderer spikeShieldVisual;

        [Header("Năng lượng hủy diệt - Tia lửa")]
        [Tooltip("Prefab tia lửa truy đuổi.")]
        [SerializeField] private DeadlandSoulProjectile sparkProjectilePrefab;

        [Tooltip("Chu kỳ bắn tia lửa (giây).")]
        [SerializeField, Min(0.1f)] private float sparkInterval = 0.5f;

        [Tooltip("Tỷ lệ sát thương tia lửa so với Tấn công (0.25 = 25%).")]
        [SerializeField, Range(0f, 2f)] private float sparkDamageRatio = 0.25f;

        [Tooltip("Tốc độ bay của tia lửa (m/giây).")]
        [SerializeField, Min(0.1f)] private float sparkSpeed = 11f;

        [Header("Năng lượng hủy diệt - Mặt Trăng Đen")]
        [Tooltip("Prefab Mặt Trăng Đen di về phía Boss.")]
        [SerializeField] private BlackMoonRunner blackMoonRunnerPrefab;

        [Tooltip("Vị trí sinh Mặt Trăng Đen so với tâm sân.")]
        [SerializeField] private Vector2 moonSpawnOffset = new Vector2(-14f, 0f);

        [Tooltip("Chu kỳ triệu hồi Mặt Trăng Đen (giây).")]
        [SerializeField, Min(0.1f)] private float moonSpawnInterval = 10f;

        [Tooltip("Thời gian tồn tại của Mặt Trăng Đen (giây).")]
        [SerializeField, Min(0.1f)] private float moonLifetime = 10f;

        [Header("Năng lượng hủy diệt - Thưởng/Phạt")]
        [Tooltip("Tỷ lệ Giáp còn lại khi phá cả 2 khiên (0.1 = còn 10%).")]
        [SerializeField, Range(0f, 1f)] private float rewardDefenseMultiplier = 0.1f;

        [Tooltip("Thời gian choáng của Boss khi phá cả 2 khiên (giây).")]
        [SerializeField, Min(0f)] private float rewardStunDuration = 3f;

        [Tooltip("Tỷ lệ buff Tấn công cho Tank khi phá cả 2 khiên (0.25 = +25%).")]
        [SerializeField, Range(0f, 1f)] private float rewardTankAttackBonus = 0.25f;

        [Tooltip("Tỷ lệ hồi HP cho Tank khi phá cả 2 khiên (0.1 = 10%).")]
        [SerializeField, Range(0f, 1f)] private float rewardTankHealRatio = 0.1f;

        [Tooltip("Tỷ lệ sát thương toàn map khi hết 35s mà còn khiên (2 = 200%).")]
        [SerializeField, Min(0f)] private float penaltyDamageMultiplier = 2f;

        [Tooltip("Thời gian choáng Tank khi Boss phạt (giây).")]
        [SerializeField, Min(0f)] private float penaltyStunDuration = 2f;

        [Header("Sân đấu")]
        [Tooltip("Tâm sân dự phòng khi không tìm thấy StageManager.")]
        [SerializeField] private Vector2 arenaCenter = Vector2.zero;

        private float m_FlameTimer;
        private float m_RoarTimer;
        private float m_LifestealTimer;
        private float m_SpikeTimer;
        private float m_SpikeCooldownTimer;
        private float m_SelfStunTimer;

        private float m_RedShieldMax;
        private float m_RedShieldHp;
        private bool m_RedShieldActive;
        private float m_BlueShieldRemaining;
        private bool m_BlueShieldActive;
        private bool m_BlueShieldLocked;
        private bool m_MoonSpawning;
        private float m_MoonSpawnTimer;
        private float m_SparkTimer;

        // Chỉ số gốc được chụp lại sau khi khởi tạo để mọi buff đều nhận từ gốc,
        // tránh cộng dồn sai khi nhiều hiệu ứng chồng nhau.
        private float m_BaseAttack;
        private float m_BaseDefense;
        private float m_BaseMoveSpeed;
        private bool m_StatsInitialized;
        private bool m_RoarBuffActive;
        private bool m_SpikeShieldActive;
        private float m_BurstAttackMul = 1f;
        private float m_BurstDefenseMul = 1f;
        private float m_BurstSpeedMul = 1f;
        private float m_RewardDefenseMul = 1f;

        private Rigidbody2D m_Rigidbody;
        private TankBrain m_CurrentTarget;
        private Vector2 m_DesiredVelocity;
        private Vector2 m_BurstCenter;
        private Coroutine m_RoarRoutine;
        private Coroutine m_FireConeRoutine;
        private readonly List<BlackMoonRunner> m_ActiveMoons = new List<BlackMoonRunner>();

        public bool IsRedShieldActive => m_RedShieldActive;
        public bool IsBlueShieldActive => m_BlueShieldActive;
        public bool IsBlueShieldLocked => m_BlueShieldLocked;
        public float RedShieldHealth => m_RedShieldHp;
        public float BlueShieldRemaining => m_BlueShieldRemaining;
        public TankBrain CurrentTarget => m_CurrentTarget;
        public bool IsSelfStunned => m_SelfStunTimer > 0f;

        protected override void Awake()
        {
            base.Awake();

            m_Rigidbody = GetComponent<Rigidbody2D>();
            if (m_Rigidbody != null)
            {
                m_Rigidbody.bodyType = RigidbodyType2D.Kinematic;
                m_Rigidbody.gravityScale = 0f;
            }

            HideAllShields();
            HideFireCone();
        }

        public override void Initialize(EnemyStats overrideStats = null)
        {
            m_StatsInitialized = false;
            base.Initialize(overrideStats);

            m_RoarBuffActive = false;
            m_SpikeShieldActive = false;
            m_BurstAttackMul = 1f;
            m_BurstDefenseMul = 1f;
            m_BurstSpeedMul = 1f;
            m_RewardDefenseMul = 1f;
            CaptureBaseStats();

            m_FlameTimer = 0f;
            m_RoarTimer = 0f;
            m_LifestealTimer = 0f;
            m_SpikeTimer = 0f;
            m_SpikeCooldownTimer = 0f;
            m_SelfStunTimer = 0f;
            m_RedShieldMax = 0f;
            m_RedShieldHp = 0f;
            m_RedShieldActive = false;
            m_BlueShieldRemaining = 0f;
            m_BlueShieldActive = false;
            m_BlueShieldLocked = false;
            m_MoonSpawning = false;
            m_MoonSpawnTimer = 0f;
            m_SparkTimer = 0f;
            m_CurrentTarget = null;
            m_DesiredVelocity = Vector2.zero;
            m_BurstCenter = arenaCenter;

            if (m_RoarRoutine != null)
            {
                StopCoroutine(m_RoarRoutine);
                m_RoarRoutine = null;
            }

            if (m_FireConeRoutine != null)
            {
                StopCoroutine(m_FireConeRoutine);
                m_FireConeRoutine = null;
            }

            m_ActiveMoons.Clear();
            HideAllShields();
            HideFireCone();
        }

        #region Tick & Movement

        protected override void OnEliteTick(float deltaTime)
        {
            if (!IsAlive) return;

            UpdateTarget();
            UpdateFacing(deltaTime);

            if (m_SelfStunTimer > 0f)
            {
                m_SelfStunTimer = Mathf.Max(0f, m_SelfStunTimer - deltaTime);
                StopMovement();
            }
            else if (IsBurstCharging || IsBurstActive)
            {
                StopMovement();
            }
            else
            {
                UpdateMovement(deltaTime);
                UpdateNormalSkills(deltaTime);
            }

            UpdateLifesteal(deltaTime);
            UpdateSpikeShield(deltaTime);
        }

        private void UpdateTarget()
        {
            TankBrain nearest = FindNearestTank();
            if (nearest != null)
            {
                m_CurrentTarget = nearest;
            }
            else if (m_CurrentTarget != null && !m_CurrentTarget.IsAlive)
            {
                m_CurrentTarget = null;
            }
        }

        private TankBrain FindNearestTank()
        {
            TankBrain[] tanks = FindObjectsByType<TankBrain>();
            TankBrain result = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain tank = tanks[i];
                if (tank == null || !tank.IsAlive) continue;

                float sqr = ((Vector2)tank.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    result = tank;
                }
            }

            return result;
        }

        private TankBrain FindFarthestTank()
        {
            TankBrain[] tanks = FindObjectsByType<TankBrain>();
            TankBrain result = null;
            float bestSqr = -1f;

            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain tank = tanks[i];
                if (tank == null || !tank.IsAlive) continue;

                float sqr = ((Vector2)tank.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqr > bestSqr)
                {
                    bestSqr = sqr;
                    result = tank;
                }
            }

            return result;
        }

        private void UpdateMovement(float deltaTime)
        {
            if (m_CurrentTarget == null || !m_CurrentTarget.IsAlive)
            {
                StopMovement();
                return;
            }

            Vector2 toTarget = (Vector2)m_CurrentTarget.transform.position - (Vector2)transform.position;
            float distance = toTarget.magnitude;
            float speed = Stats != null ? Mathf.Max(0f, Stats.MoveSpeed / Mathf.Max(1f, moveSpeedDivisor)) : 0f;

            Vector2 desired = distance > stopDistance && speed > 0f
                ? toTarget / Mathf.Max(0.0001f, distance) * speed
                : Vector2.zero;

            float lerp = 1f - Mathf.Exp(-turnSharpness * deltaTime);
            m_DesiredVelocity = Vector2.Lerp(m_DesiredVelocity, desired, lerp);

            Model?.SetMoveSpeed(m_DesiredVelocity.magnitude);
        }

        private void StopMovement()
        {
            m_DesiredVelocity = Vector2.zero;
            Model?.SetMoveSpeed(0f);
        }

        private void UpdateFacing(float deltaTime)
        {
            if (!faceTarget || Model == null) return;
            if (m_CurrentTarget == null || !m_CurrentTarget.IsAlive) return;

            Vector2 direction = (Vector2)m_CurrentTarget.transform.position - (Vector2)transform.position;
            if (direction.sqrMagnitude < 0.0001f) return;

            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;
            float smooth = 1f - Mathf.Exp(-turnSharpness * deltaTime);
            Model.transform.rotation = Quaternion.Slerp(Model.transform.rotation, Quaternion.Euler(0f, 0f, targetAngle), smooth);
        }

        private void FixedUpdate()
        {
            if (m_Rigidbody == null) return;

            if (m_Rigidbody.bodyType == RigidbodyType2D.Kinematic)
            {
                m_Rigidbody.MovePosition(m_Rigidbody.position + m_DesiredVelocity * Time.fixedDeltaTime);
            }
            else
            {
                m_Rigidbody.linearVelocity = m_DesiredVelocity;
            }
        }

        #endregion

        #region Normal Skills

        private void UpdateNormalSkills(float deltaTime)
        {
            if (!CanUseSkill) return;

            float distance = m_CurrentTarget != null
                ? Vector2.Distance(transform.position, m_CurrentTarget.transform.position)
                : float.MaxValue;

            m_FlameTimer += deltaTime;
            if (m_FlameTimer >= flameCooldown && distance <= flameRange)
            {
                m_FlameTimer = 0f;
                BreatheFire();
            }

            m_RoarTimer += deltaTime;
            if (m_RoarTimer >= roarCooldown && distance <= roarRadius)
            {
                m_RoarTimer = 0f;
                Roar();
            }
        }

        private void UpdateLifesteal(float deltaTime)
        {
            if (m_SelfStunTimer > 0f) return;

            m_LifestealTimer += deltaTime;
            if (m_LifestealTimer < lifestealInterval) return;

            m_LifestealTimer = 0f;
            if (m_CurrentTarget == null || !m_CurrentTarget.IsAlive) return;

            Lifesteal(m_CurrentTarget);
        }

        private void Lifesteal(TankBrain target)
        {
            float damage = target.CurrentHealth * lifestealPercent;
            if (damage <= 0f) return;

            // Hút đúng 10% HP hiện tại: xuyên toàn bộ giáp và đi thẳng vào máu.
            DamageInfo info = new DamageInfo(
                this,
                target,
                damage,
                armorPenetration: 1f,
                hitPosition: target.transform.position,
                isDirectToHealth: true,
                sourceType: DamageSourceType.EnemyAttack);

            target.TakeDamage(info);
            Heal(info.FinalDamage);
            Debug.Log("[SeauBearus] hút máu " + info.FinalDamage.ToString("0.0") + " -> HP " + CurrentHealth.ToString("0.0"));
        }

        private void BreatheFire()
        {
            if (m_CurrentTarget == null) return;

            Vector2 origin = transform.position;
            Vector2 forward = ((Vector2)m_CurrentTarget.transform.position - origin).normalized;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector2.right;

            PlayFireCone(forward);

            float damage = Stats.Attack * flameDamageRatio;

            // Chỉ tính trên thân tank (transform gốc) để không phụ thuộc vào thứ tự
            // các collider con (tank có 3 collider skill rỗng, trigger).
            TankBrain[] tanks = FindObjectsByType<TankBrain>();
            HashSet<TankBrain> damaged = new HashSet<TankBrain>();

            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain tank = tanks[i];
                if (tank == null || !tank.IsAlive) continue;

                Vector2 toTank = (Vector2)tank.transform.position - origin;
                if (toTank.magnitude > flameRange) continue;

                // Sát quá gần thì luôn trúng (góc không còn ý nghĩa).
                bool inCone = toTank.magnitude <= 1.5f || Vector2.Angle(forward, toTank) <= flameHalfAngle;
                if (!inCone) continue;
                if (!damaged.Add(tank)) continue;

                DamageInfo info = new DamageInfo(this, tank, damage, 0f, tank.transform.position, sourceType: DamageSourceType.EnemyAttack);
                tank.TakeDamage(info);
            }

            for (int i = 0; i < trailPatchCount; i++)
            {
                float ratio = (i + 1f) / trailPatchCount;
                Vector2 patchPosition = origin + forward * (flameRange * ratio);
                FlamePatch.Spawn(
                    patchPosition,
                    trailPatchRadius,
                    trailPatchLifetime,
                    Stats.Attack * trailDamageRatio,
                    trailSlowPercent,
                    trailSlowDuration,
                    trailTickInterval);
            }

            Debug.Log("[SeauBearus] phun lửa: " + damage.ToString("0.0") + " dmg, trúng " + damaged.Count + " tank, " + trailPatchCount + " vết lửa");
        }

        private void PlayFireCone(Vector2 forward)
        {
            if (fireConeVisual == null)
            {
                Debug.LogWarning("[SeauBearus] Chưa gán Fire Cone Visual trên prefab.");
                return;
            }

            if (fireConeVisual.sprite == null)
            {
                Debug.LogWarning("[SeauBearus] Fire Cone Visual chưa có sprite; kiểm tra import texture.");
                return;
            }

            Debug.Log("[SeauBearus] phát VFX quạt lửa " + (flameHalfAngle * 2f).ToString("0") + "° / " + flameRange.ToString("0.0") + "m.");

            if (m_FireConeRoutine != null)
            {
                StopCoroutine(m_FireConeRoutine);
                m_FireConeRoutine = null;
            }

            m_FireConeRoutine = StartCoroutine(FireConeRoutine(forward));
            GetComponent<IronWasteland.Net.EnemyNetState>()?.BroadcastVfx(
                "FireCone", transform.position, 0f, forward);
        }

        private IEnumerator FireConeRoutine(Vector2 forward)
        {
            Transform coneTransform = fireConeVisual.transform;
            coneTransform.localPosition = Vector3.zero;
            coneTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg);

            // Co giãn sprite theo đúng tầm và góc của vùng sát thương hình quạt.
            Vector3 baseScale = new Vector3(flameRange, flameRange, 1f);
            if (fireConeVisual.sprite != null)
            {
                Vector2 spriteSize = fireConeVisual.sprite.bounds.size;
                float coneWidth = 2f * flameRange * Mathf.Tan(flameHalfAngle * Mathf.Deg2Rad);
                float scaleX = spriteSize.x > 0.01f ? flameRange / spriteSize.x : flameRange;
                float scaleY = spriteSize.y > 0.01f ? coneWidth / spriteSize.y : flameRange;
                baseScale = new Vector3(scaleX, scaleY, 1f);
            }

            fireConeVisual.gameObject.SetActive(true);

            Color color = fireConeVisual.color;
            color.a = 0f;
            fireConeVisual.color = color;

            float elapsed = 0f;
            while (elapsed < fireConeDuration)
            {
                if (!IsAlive) break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fireConeDuration);

                float grow = t < 0.15f ? Mathf.Lerp(0.82f, 1f, t / 0.15f) : 1f;
                coneTransform.localScale = baseScale * grow;

                color.a = t < 0.55f
                    ? Mathf.Lerp(0f, 1f, t / 0.15f)
                    : 1f - Mathf.Clamp01((t - 0.55f) / 0.45f);
                fireConeVisual.color = color;

                yield return null;
            }

            fireConeVisual.gameObject.SetActive(false);
            m_FireConeRoutine = null;
        }

        private void HideFireCone()
        {
            if (fireConeVisual != null)
            {
                fireConeVisual.gameObject.SetActive(false);
            }
        }

        private void Roar()
        {
            CancelRoarFollowUp();

            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, roarRadius);
            HashSet<TankBrain> affected = new HashSet<TankBrain>();

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null) continue;

                TankBrain tank = collider.GetComponentInParent<TankBrain>();
                if (tank == null || !tank.IsAlive) continue;
                if (!affected.Add(tank)) continue;

                TankSlowDebuff.Apply(tank, roarSlowPercent, roarSlowDuration);
            }

            m_RoarRoutine = StartCoroutine(RoarFollowUp());

            Debug.Log("[SeauBearus] gầm thét: làm chậm " + affected.Count + " tank " + (roarSlowPercent * 100f).ToString("0") + "% trong " + roarSlowDuration.ToString("0.0") + "s");
        }

        private IEnumerator RoarFollowUp()
        {
            yield return new WaitForSeconds(roarBuffDelay);

            if (!IsAlive)
            {
                m_RoarRoutine = null;
                yield break;
            }

            m_RoarBuffActive = true;
            RefreshStats();

            SpawnFlamePillars();

            yield return new WaitForSeconds(roarBuffDuration);

            m_RoarBuffActive = false;
            RefreshStats();
            m_RoarRoutine = null;
        }

        private void SpawnFlamePillars()
        {
            TankBrain[] tanks = FindObjectsByType<TankBrain>();
            float damage = Stats.Attack * roarPillarDamageRatio;

            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain tank = tanks[i];
                if (tank == null || !tank.IsAlive) continue;

                // Cột lửa chỉ để hiển thị, sát thương đánh thẳng một lần bên dưới.
                FlamePatch.Spawn(tank.transform.position, roarPillarRadius, roarPillarLifetime, 0f, 0f, 0f, 0.5f);

                if (damage <= 0f) continue;

                DamageInfo info = new DamageInfo(this, tank, damage, 0f, tank.transform.position, sourceType: DamageSourceType.EnemyAttack);
                tank.TakeDamage(info);
            }
        }

        private void CancelRoarFollowUp()
        {
            if (m_RoarRoutine != null)
            {
                StopCoroutine(m_RoarRoutine);
                m_RoarRoutine = null;
            }

            if (m_RoarBuffActive)
            {
                m_RoarBuffActive = false;
                RefreshStats();
            }
        }

        #endregion

        #region Spike Shield

        private void UpdateSpikeShield(float deltaTime)
        {
            if (m_SpikeCooldownTimer > 0f)
            {
                m_SpikeCooldownTimer = Mathf.Max(0f, m_SpikeCooldownTimer - deltaTime);
            }

            if (m_SpikeTimer > 0f)
            {
                m_SpikeTimer = Mathf.Max(0f, m_SpikeTimer - deltaTime);
                if (m_SpikeTimer <= 0f)
                {
                    DeactivateSpikeShield();
                }
                else if (spikeShieldVisual != null)
                {
                    Color color = SpikeShieldColor;
                    color.a = 0.22f + 0.12f * Mathf.PingPong(Time.time * 2f, 1f);
                    spikeShieldVisual.color = color;
                }
            }

            if (m_SpikeTimer <= 0f
                && m_SpikeCooldownTimer <= 0f
                && m_SelfStunTimer <= 0f
                && !IsBurstCharging
                && !IsBurstActive
                && CanUseSkill)
            {
                ActivateSpikeShield();
            }
        }

        private void ActivateSpikeShield()
        {
            m_SpikeShieldActive = true;
            RefreshStats();
            m_SpikeTimer = spikeDuration;
            m_SpikeCooldownTimer = spikeCooldown;

            if (spikeShieldVisual != null)
            {
                spikeShieldVisual.gameObject.SetActive(true);
            }
            GetComponent<IronWasteland.Net.EnemyNetState>()?.BroadcastVfx(
                "SpikeShield", transform.position, value: 1f);

            Debug.Log("[SeauBearus] bật Khiên Gai: +" + (spikeDefenseBonus * 100f).ToString("0") + "% DEF, phản " + (spikeReflectRatio * 100f).ToString("0") + "% ATK");
        }

        private void DeactivateSpikeShield()
        {
            m_SpikeTimer = 0f;

            if (m_SpikeShieldActive)
            {
                m_SpikeShieldActive = false;
                RefreshStats();
            }

            if (spikeShieldVisual != null)
            {
                spikeShieldVisual.gameObject.SetActive(false);
            }
            GetComponent<IronWasteland.Net.EnemyNetState>()?.BroadcastVfx(
                "SpikeShield", transform.position, value: 0f);
        }

        private void ReflectSpikeDamage(DamageInfo info)
        {
            TankBrain tank = info.Attacker as TankBrain;

            if (tank == null && info.Attacker is Component component)
            {
                tank = component.GetComponentInParent<TankBrain>();
            }

            if (tank == null && info.Attacker is GameObject gameObjectAttacker)
            {
                tank = gameObjectAttacker.GetComponentInParent<TankBrain>();
            }

            if (tank == null || !tank.IsAlive) return;

            float damage = Stats.Attack * spikeReflectRatio;
            if (damage <= 0f) return;

            DamageInfo reflect = new DamageInfo(this, tank, damage, 0f, tank.transform.position, sourceType: DamageSourceType.EnemyAttack);
            tank.TakeDamage(reflect);
        }

        #endregion

        #region Stat Modifiers

        /// <summary>
        /// Ghi đè cơ chế nhận chỉ số của Boss Tinh Anh: chỉ lưu hệ số Bùng nổ,
        /// mỗi lần tính lại từ chỉ số gốc để không cộng dồn sai khi nhiều buff chồng nhau.
        /// </summary>
        public override void ApplyBurstStatModifiers(float attackMultiplier = 1f, float defenseMultiplier = 1f, float speedMultiplier = 1f)
        {
            m_BurstAttackMul = Mathf.Max(0f, attackMultiplier);
            m_BurstDefenseMul = Mathf.Max(0f, defenseMultiplier);
            m_BurstSpeedMul = Mathf.Max(0f, speedMultiplier);
            RefreshStats();
        }

        /// <summary>
        /// Gỡ hệ số Bùng nổ và tính lại chỉ số từ chỉ số gốc.
        /// </summary>
        public override void RevertBurstStatModifiers()
        {
            m_BurstAttackMul = 1f;
            m_BurstDefenseMul = 1f;
            m_BurstSpeedMul = 1f;
            RefreshStats();
        }

        private void CaptureBaseStats()
        {
            if (Stats == null) return;

            m_BaseAttack = Stats.Attack;
            m_BaseDefense = Stats.Defense;
            m_BaseMoveSpeed = Stats.MoveSpeed;
            m_StatsInitialized = true;
            RefreshStats();
        }

        /// <summary>
        /// Tính lại Tấn công / Giáp / Tốc độ từ chỉ số gốc và các hệ số đang bật.
        /// MaxHealth và các chỉ số khác được giữ nguyên.
        /// </summary>
        private void RefreshStats()
        {
            if (!m_StatsInitialized || Stats == null) return;

            float roarAttack = m_RoarBuffActive ? 1f + roarAttackBonus : 1f;
            float roarSpeed = m_RoarBuffActive ? 1f + roarSpeedBonus : 1f;
            float spikeDefense = m_SpikeShieldActive ? 1f + spikeDefenseBonus : 1f;

            Stats.Attack = m_BaseAttack * m_BurstAttackMul * roarAttack;
            Stats.Defense = m_BaseDefense * m_BurstDefenseMul * spikeDefense * m_RewardDefenseMul;
            Stats.MoveSpeed = m_BaseMoveSpeed * m_BurstSpeedMul * roarSpeed;
        }

        #endregion

        #region Energy Burst

        protected override void OnBurstChargingStart()
        {
            CancelRoarFollowUp();
            DeactivateSpikeShield();
            StopMovement();
            m_SparkTimer = 0f;
            m_MoonSpawnTimer = 0f;
            m_BlueShieldLocked = false;
            m_MoonSpawning = false;
            Debug.Log("[SeauBearus] năng lượng hủy diệt: bắt đầu tích tụ!");
        }

        protected override void OnBurstStart()
        {
            CancelRoarFollowUp();
            StopMovement();

            m_BurstCenter = ResolveArenaCenter();
            transform.position = new Vector3(m_BurstCenter.x, m_BurstCenter.y, transform.position.z);

            if (m_Rigidbody != null)
            {
                m_Rigidbody.position = m_BurstCenter;
            }

            m_RedShieldMax = Mathf.Max(0f, MaxHealth * redShieldHpRatio);
            m_RedShieldHp = m_RedShieldMax;
            m_RedShieldActive = m_RedShieldMax > 0f;

            m_BlueShieldRemaining = Mathf.Max(0f, blueShieldDuration);
            m_BlueShieldActive = m_BlueShieldRemaining > 0f;
            m_BlueShieldLocked = false;

            m_MoonSpawning = blackMoonRunnerPrefab != null;
            m_MoonSpawnTimer = 0f;
            m_SparkTimer = 0f;
            m_SelfStunTimer = 0f;
            m_SpikeCooldownTimer = Mathf.Max(m_SpikeCooldownTimer, spikeCooldown);

            SetShieldVisualActive(redShieldVisual, m_RedShieldActive);
            SetShieldVisualActive(blueShieldVisual, m_BlueShieldActive);
            UpdateShieldVisuals();
            BroadcastBurstShieldsState();
            SpawnMoon();

            if (blackMoonRunnerPrefab == null)
            {
                Debug.LogWarning("[SeauBearus] Chưa gắn BlackMoonRunner prefab, sẽ không triệu hồi Mặt Trăng Đen!");
            }

            Debug.Log("[SeauBearus] năng lượng hủy diệt: Khiên Đỏ " + m_RedShieldMax.ToString("0") + " HP, Khiên Xanh " + m_BlueShieldRemaining.ToString("0.0") + "s");
        }

        protected override void OnBurstTick(float deltaTime)
        {
            if (m_BlueShieldActive && !m_BlueShieldLocked)
            {
                m_BlueShieldRemaining = Mathf.Max(0f, m_BlueShieldRemaining - deltaTime);
                if (m_BlueShieldRemaining <= 0f)
                {
                    m_BlueShieldActive = false;
                    SetShieldVisualActive(blueShieldVisual, false);
                    UpdateShieldVisuals();
                    BroadcastBurstShieldsState();
                    TryCompleteBurst();
                    if (!IsBurstActive) return;
                }
            }

            UpdateShieldVisuals();

            m_SparkTimer += deltaTime;
            if (m_SparkTimer >= sparkInterval)
            {
                m_SparkTimer -= sparkInterval;
                SpawnSpark();
            }

            if (!m_MoonSpawning) return;

            m_MoonSpawnTimer += deltaTime;
            if (m_MoonSpawnTimer >= moonSpawnInterval)
            {
                m_MoonSpawnTimer = 0f;
                SpawnMoon();
            }
        }

        private void TryCompleteBurst()
        {
            if (!IsBurstActive) return;
            if (m_RedShieldActive || m_BlueShieldActive) return;

            CompleteBurstSuccess();
        }

        protected override void OnBurstSuccess()
        {
            RevertBurstStatModifiers();

            DeactivateBurstShields();
            ClearMoons();
            CancelRoarFollowUp();
            DeactivateSpikeShield();

            m_RedShieldActive = false;
            m_BlueShieldActive = false;
            m_BlueShieldLocked = false;
            m_MoonSpawning = false;
            m_SelfStunTimer = rewardStunDuration;
            m_SparkTimer = 0f;

            m_RewardDefenseMul = rewardDefenseMultiplier;
            RefreshStats();

            m_FlameTimer = flameCooldown;
            m_RoarTimer = roarCooldown;
            ResetGlobalSkillCooldown();

            TankBrain[] tanks = FindObjectsByType<TankBrain>();
            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain tank = tanks[i];
                if (tank == null || !tank.IsAlive) continue;

                tank.RemoveBonusStats(RewardAttackKey);
                tank.AddBonusStats(RewardAttackKey, null, new TankStats { Attack = rewardTankAttackBonus });
                StartCoroutine(RemoveRewardAttackAfterDelay(tank, rewardPhaseDuration));
                StartCoroutine(HealTankOverTime(tank, tank.Stats.MaxHealth * rewardTankHealRatio, rewardPhaseDuration));
                tank.ResetAllSkillCooldowns();
            }

            Debug.Log("[SeauBearus] phá cả 2 khiên: -90% DEF, choáng " + rewardStunDuration.ToString("0.0") + "s, toàn bộ Tank được buff!");
        }

        protected override void OnBurstFail()
        {
            DeactivateBurstShields();
            ClearMoons();
            CancelRoarFollowUp();
            DeactivateSpikeShield();

            m_RedShieldActive = false;
            m_BlueShieldActive = false;
            m_BlueShieldLocked = false;
            m_MoonSpawning = false;
            m_SelfStunTimer = 0f;
            m_SparkTimer = 0f;

            float damage = Stats.Attack * penaltyDamageMultiplier;
            TankBrain[] tanks = FindObjectsByType<TankBrain>();

            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain tank = tanks[i];
                if (tank == null || !tank.IsAlive) continue;

                DamageInfo info = new DamageInfo(this, tank, damage, 0f, tank.transform.position, sourceType: DamageSourceType.EnemyAttack);
                tank.TakeDamage(info);

                if (tank.IsAlive)
                {
                    tank.ApplyCrowdControl(CrowdControlType.Stun, penaltyStunDuration);
                }
            }

            Debug.Log("[SeauBearus] hết thời gian phá khiên: phạt " + damage.ToString("0") + " dmg toàn map, choáng " + penaltyStunDuration.ToString("0.0") + "s!");
        }

        protected override void OnBurstEnded()
        {
            DeactivateBurstShields();
            ClearMoons();
            CancelRoarFollowUp();
            DeactivateSpikeShield();

            m_RewardDefenseMul = 1f;
            RefreshStats();

            m_RedShieldActive = false;
            m_BlueShieldActive = false;
            m_BlueShieldLocked = false;
            m_MoonSpawning = false;
            m_SelfStunTimer = 0f;
            m_SpikeCooldownTimer = 0f;
            m_SparkTimer = 0f;
            m_FlameTimer = flameCooldown;
            m_RoarTimer = roarCooldown;
            StopMovement();
        }

        public override void ResetBurstManual()
        {
            base.ResetBurstManual();

            DeactivateBurstShields();
            ClearMoons();
            CancelRoarFollowUp();
            DeactivateSpikeShield();

            m_RewardDefenseMul = 1f;
            RefreshStats();

            m_RedShieldActive = false;
            m_BlueShieldActive = false;
            m_BlueShieldLocked = false;
            m_MoonSpawning = false;
            m_SelfStunTimer = 0f;
            m_SpikeCooldownTimer = 0f;
            m_SparkTimer = 0f;
            StopMovement();
        }

        private void DeactivateBurstShields()
        {
            m_RedShieldActive = false;
            m_BlueShieldActive = false;
            m_RedShieldHp = 0f;
            m_BlueShieldRemaining = 0f;

            SetShieldVisualActive(redShieldVisual, false);
            SetShieldVisualActive(blueShieldVisual, false);
            BroadcastBurstShieldsState();
        }

        private void BroadcastBurstShieldsState()
        {
            GetComponent<IronWasteland.Net.EnemyNetState>()?.BroadcastVfx(
                "BurstShields",
                transform.position,
                duration: m_RedShieldActive ? 1f : 0f,
                value: m_BlueShieldActive ? 1f : 0f);
        }

        private void UpdateShieldVisuals()
        {
            if (redShieldVisual != null && m_RedShieldActive)
            {
                float ratio = m_RedShieldMax > 0f ? Mathf.Clamp01(m_RedShieldHp / m_RedShieldMax) : 0f;
                Color color = RedShieldColor;
                color.a = Mathf.Lerp(0.18f, 0.58f, ratio);
                redShieldVisual.color = color;
            }

            if (blueShieldVisual != null && m_BlueShieldActive)
            {
                Color color = BlueShieldColor;
                color.a = 0.34f + 0.12f * Mathf.Sin(Time.time * 5f);
                blueShieldVisual.color = color;
            }
        }

        private void SetShieldVisualActive(SpriteRenderer visual, bool active)
        {
            if (visual != null && visual.gameObject.activeSelf != active)
            {
                visual.gameObject.SetActive(active);
            }
        }

        private void HideAllShields()
        {
            SetShieldVisualActive(redShieldVisual, false);
            SetShieldVisualActive(blueShieldVisual, false);
            SetShieldVisualActive(spikeShieldVisual, false);
            GetComponent<IronWasteland.Net.EnemyNetState>()?.BroadcastVfx("HideAllShields", transform.position);
        }

        public override void TakeDamage(DamageInfo info)
        {
            if (!IsAlive) return;

            if (IsBurstActive)
            {
                // Cả hai khiên cùng tồn tại: thân Boss miễn nhiễm hoàn toàn,
                // chỉ Khiên Đỏ phía bên phải nhận sát thương đánh từ nửa phải.
                if (m_RedShieldActive && m_BlueShieldActive)
                {
                    if (IsAttackFromRight(info))
                    {
                        ApplyDamageToRedShield(info);
                    }

                    Model?.PlayHit();
                    return;
                }

                // Chỉ còn Khiên Đỏ: nửa phải đánh vào khiên, nửa trái đánh thẳng vào thân.
                if (m_RedShieldActive && IsAttackFromRight(info))
                {
                    ApplyDamageToRedShield(info);
                    Model?.PlayHit();
                    return;
                }
            }

            float healthBefore = CurrentHealth;
            base.TakeDamage(info);

            if (m_SpikeShieldActive && IsAlive && CurrentHealth < healthBefore)
            {
                ReflectSpikeDamage(info);
            }
        }

        private Vector2 ResolveDamageOrigin(DamageInfo info)
        {
            if (info.Attacker is Component component)
            {
                return component.transform.position;
            }

            if (info.Attacker is GameObject gameObjectAttacker)
            {
                return gameObjectAttacker.transform.position;
            }

            return info.HitPosition;
        }

        private bool IsAttackFromRight(DamageInfo info)
        {
            Vector2 toOrigin = ResolveDamageOrigin(info) - (Vector2)transform.position;
            return toOrigin.sqrMagnitude > 0.0001f && Vector2.Dot(toOrigin.normalized, Vector2.right) > 0f;
        }

        private void ApplyDamageToRedShield(DamageInfo info)
        {
            float finalDamage = CombatUtility.ResolveDamage(info.RawDamage, Stats.Defense, info.ArmorPenetration);
            info.FinalDamage = finalDamage;
            DamageRedShield(finalDamage);
        }

        private void DamageRedShield(float amount)
        {
            if (!m_RedShieldActive) return;

            m_RedShieldHp = Mathf.Max(0f, m_RedShieldHp - Mathf.Max(0f, amount));

            Vector2 showPosition = redShieldVisual != null ? (Vector2)redShieldVisual.transform.position : (Vector2)transform.position;
            if (amount > 0.01f)
            {
                CombatFeedback.Show(amount, showPosition, TextValueType.ShieldAbsorb);
            }

            if (m_RedShieldHp <= 0f)
            {
                m_RedShieldActive = false;
                SetShieldVisualActive(redShieldVisual, false);
                BroadcastBurstShieldsState();
                TryCompleteBurst();
            }
            else
            {
                UpdateShieldVisuals();
            }
        }

        private Vector2 ResolveArenaCenter()
        {
            StageManager stageManager = FindAnyObjectByType<StageManager>();
            return stageManager != null ? stageManager.ArenaCenter : arenaCenter;
        }

        #endregion

        #region Burst Helpers

        private void SpawnSpark()
        {
            if (sparkProjectilePrefab == null) return;

            TankBrain target = FindFarthestTank();
            if (target == null) return;

            Vector2 origin = transform.position;
            Vector2 direction = ((Vector2)target.transform.position - origin).normalized;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;

            DeadlandSoulProjectile projectile = Instantiate(sparkProjectilePrefab, origin, Quaternion.identity);
            projectile.Initialize(target.transform, direction, sparkSpeed, Stats.Attack * sparkDamageRatio, this);

            SpriteRenderer[] renderers = projectile.GetComponentsInChildren<SpriteRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null) continue;
                renderer.color = new Color(1f, 0.55f, 0.12f, renderer.color.a);
            }

            GetComponent<IronWasteland.Net.EnemyNetState>()?.BroadcastVfx(
                "SparkProjectile", origin, 0f, direction, sparkSpeed, 0f);
        }

        private void SpawnMoon()
        {
            if (blackMoonRunnerPrefab == null) return;

            m_ActiveMoons.RemoveAll(runner => runner == null);

            Vector2 spawnPosition = m_BurstCenter + moonSpawnOffset;
            BlackMoonRunner runner = Instantiate(blackMoonRunnerPrefab, spawnPosition, Quaternion.identity);
            runner.name = "BlackMoonRunner";
            if (TankBrain.NetworkSessionActive &&
                runner.TryGetComponent(out Unity.Netcode.NetworkObject moonNetObj) && !moonNetObj.IsSpawned)
            {
                var manager = Unity.Netcode.NetworkManager.Singleton;
                if (manager != null && manager.IsServer) moonNetObj.Spawn(true);
            }
            runner.InitializeRunner(this, moonLifetime);
            m_ActiveMoons.Add(runner);
        }

        public void OnClientNetVfx(string key, Vector2 position, float rotationZ, Vector2 dir, float duration, float value)
        {
            if (key == "SparkProjectile" && sparkProjectilePrefab != null)
            {
                DeadlandSoulProjectile projectile = Instantiate(sparkProjectilePrefab, position, Quaternion.identity);
                projectile.Initialize(null, dir, duration > 0f ? duration : sparkSpeed, 0f, null);
                SpriteRenderer[] renderers = projectile.GetComponentsInChildren<SpriteRenderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                        renderers[i].color = new Color(1f, 0.55f, 0.12f, renderers[i].color.a);
                }
            }
            else if (key == "BlackMoonRunner" && blackMoonRunnerPrefab != null)
            {
                BlackMoonRunner runner = Instantiate(blackMoonRunnerPrefab, position, Quaternion.identity);
                runner.name = "BlackMoonRunner";
                runner.InitializeRunner(this, duration > 0f ? duration : moonLifetime);
            }
            else if (key == "FireCone" && fireConeVisual != null)
            {
                if (m_FireConeRoutine != null)
                {
                    StopCoroutine(m_FireConeRoutine);
                    m_FireConeRoutine = null;
                }
                m_FireConeRoutine = StartCoroutine(FireConeRoutine(dir));
            }
            else if (key == "SpikeShield")
            {
                bool active = value > 0.5f;
                SetShieldVisualActive(spikeShieldVisual, active);
                if (active && spikeShieldVisual != null)
                {
                    spikeShieldVisual.color = SpikeShieldColor;
                }
            }
            else if (key == "BurstShields")
            {
                bool redActive = duration > 0.5f;
                bool blueActive = value > 0.5f;
                SetShieldVisualActive(redShieldVisual, redActive);
                SetShieldVisualActive(blueShieldVisual, blueActive);
                if (redActive && redShieldVisual != null) redShieldVisual.color = RedShieldColor;
                if (blueActive && blueShieldVisual != null) blueShieldVisual.color = BlueShieldColor;
            }
            else if (key == "HideAllShields")
            {
                SetShieldVisualActive(redShieldVisual, false);
                SetShieldVisualActive(blueShieldVisual, false);
                SetShieldVisualActive(spikeShieldVisual, false);
            }
        }

        public void OnBlackMoonReached(BlackMoonRunner runner)
        {
            if (runner != null)
            {
                m_ActiveMoons.Remove(runner);
            }

            m_BlueShieldLocked = true;
            m_MoonSpawning = false;

            Debug.Log("[SeauBearus] Mặt Trăng Đen chạm Boss: Khiên Xanh bị khóa, ngừng triệu hồi!");
        }

        private void ClearMoons()
        {
            for (int i = 0; i < m_ActiveMoons.Count; i++)
            {
                BlackMoonRunner runner = m_ActiveMoons[i];
                if (runner != null)
                {
                    Destroy(runner.gameObject);
                }
            }

            m_ActiveMoons.Clear();
        }

        private IEnumerator RemoveRewardAttackAfterDelay(TankBrain tank, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (tank != null)
            {
                tank.RemoveBonusStats(RewardAttackKey);
            }
        }

        private IEnumerator HealTankOverTime(TankBrain tank, float totalHeal, float duration)
        {
            if (tank == null || totalHeal <= 0f || duration <= 0f) yield break;

            float healed = 0f;
            float rate = totalHeal / duration;

            while (healed < totalHeal)
            {
                if (tank == null || !tank.IsAlive) yield break;

                float step = Mathf.Min(totalHeal - healed, rate * Time.deltaTime);
                if (step > 0f)
                {
                    tank.Heal(step);
                    healed += step;
                }

                yield return null;
            }
        }

        private void CleanupRewardBuffs()
        {
            TankBrain[] tanks = FindObjectsByType<TankBrain>();
            for (int i = 0; i < tanks.Length; i++)
            {
                if (tanks[i] != null)
                {
                    tanks[i].RemoveBonusStats(RewardAttackKey);
                }
            }
        }

        #endregion

        protected override void OnDeath()
        {
            HideFireCone();
            CleanupRewardBuffs();
            ClearMoons();
            CancelRoarFollowUp();
            DeactivateSpikeShield();
            IronWasteland.Net.NetEntity.Despawn(gameObject);
        }
    }
}
