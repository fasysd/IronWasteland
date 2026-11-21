using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk
{
    /// <summary>
    /// Boss 10 (Final Boss Ải 2-5) — "Trưởng Quản Ngục D1onk":
    /// 
    /// - THÔNG SỐ CHUẨN GDD:
    ///   + HP: 27.000 (Solo) / 54.000 (Co-op 2-4 Tank)
    ///   + ATK: 500
    ///   + Giáp (DEF): 60
    ///   + SPD: 70 (Tốc độ chuẩn hóa: MoveSpeed / 100 = 0.7 unit/s)
    /// 
    /// - BỊ ĐỘNG — PHÁO HẠM ĐA NÒNG:
    ///   + Thi triển bất kỳ kỹ năng nào: Tạo 5 đạn truy đuổi (20% ATK/viên).
    ///   + Mỗi 5s tự tạo 2 đạn truy đuổi tương tự (20% ATK/viên).
    /// 
    /// - BỘ 4 KỸ NĂNG:
    ///   + Trừng Phạt (TrungPhat): Gây -20% DEF 5s và 150% ATK lên Tank gần nhất; giảm -30% ATK, -10% SPD 5s lên Tank xa nhất (Solo chỉ nhận nhánh gần nhất).
    ///   + Laze Nguyên Tố (LazeNguyenTo): Phóng laser thiêu đốt mục tiêu xa nhất 5s (40% ATK mỗi 0.5s).
    ///   + Tỏa Nhiệt Diện Rộng (ToaNhietDienRong): Nổ sóng nhiệt toàn map gây 100% ATK (Tank chịu đòn đầu x2 sát thương, các Tank sau giảm dần).
    ///   + Viện Binh Tinh Linh (VienBinhTinhLinh): Triệu hồi 1-3 bản sao thu nhỏ (20% chỉ số Boss gốc).
    /// 
    /// - VÒNG ĐỜI BÙNG NỔ NĂNG LƯỢNG (Energy Burst):
    ///   + Điều kiện: HP dưới 60% lần đầu (burstHpThreshold = 0.6f).
    ///   + Cơ chế: Duy trì 40s. Hồi 100% HP, +30% ATK, +100% DEF.
    ///   + Kéo 2 Tank gần nhất (Solo: 1 Tank) vào bên trong pháo hạm phá hủy Trái Tim Boss.
    ///   + BÊN TRONG pháo hạm: Trái Tim Boss 10% Max HP, khiên Bất Hoại, quái thường mỗi 5s, 4 cột năng lượng 500 HP.
    ///     * Solo: Phá 3/4 cột lập tức gỡ khiên Bất Hoại.
    ///     * Co-op: Phá cột mở thử thách bên ngoài; hoàn thành 3 thử thách gỡ khiên Bất Hoại.
    ///   + BÊN NGOÀI pháo hạm (Co-op): Tạo bản sao của 2 Tank bị kéo vào trong để chiến đấu với các Tank còn lại.
    ///   + Thưởng (Success): Boss -30% HP tối đa, -50% ATK/DEF 10s, choáng 5s. Tank bên trong +100% chỉ số 10s;
    ///     Tank bên ngoài -50% hồi chiêu, hồi năng lượng liên tục 10s. Reset toàn bộ hồi chiêu.
    ///   + Hình phạt (Penalty): D1onk xả hàng trăm đạn pháo tiêu diệt lập tức toàn bộ người chơi (INSTANT KILL).
    /// 
    /// - RÀNG BUỘC KIẾN TRÚC & HOẠT ẢNH:
    ///   + Hoạt ảnh điều khiển trực tiếp qua Animator trên EnemyModel (controller TruongQuanNgucD1onk).
    ///   + ColliderOwnerRef trên VFX/cột/đạn buồng lái TUYỆT ĐỐI KHÔNG trỏ về Boss Brain!
    ///   + Giãn cách kỹ năng toàn cục (globalSkillInterval) tối thiểu 2.0s.
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkBrain : EliteEnemyBrain
    {
        [Header("Movement & Facing")]
        [Tooltip("Khoảng cách dừng tiếp cận Tank mục tiêu (m).")]
        [SerializeField, Min(0f)] private float stopDistance = 3.5f;

        [Tooltip("Hệ số chia tốc độ di chuyển (MoveSpeed / 100). Mặc định 100.")]
        [SerializeField, Min(1f)] private float moveSpeedDivisor = 100f;

        [Tooltip("Tốc độ xoay (độ/giây).")]
        [SerializeField] private float rotationSpeed = 360f;

        [Tooltip("Transform mục tiêu xoay pivot. Mặc định tự lấy Model hoặc GameObject.")]
        [SerializeField] private Transform rotationPivot;

        [Header("Skill Cooldowns")]
        [Tooltip("Hồi chiêu Trừng Phạt (giây).")]
        [SerializeField, Min(0.1f)] private float trungPhatCooldown = 8.0f;

        [Tooltip("Hồi chiêu Laze Nguyên Tố (giây).")]
        [SerializeField, Min(0.1f)] private float lazeNguyenToCooldown = 12.0f;

        [Tooltip("Hồi chiêu Tỏa Nhiệt Diện Rộng (giây).")]
        [SerializeField, Min(0.1f)] private float toaNhietDienRongCooldown = 15.0f;

        [Tooltip("Hồi chiêu Viện Binh Tinh Linh (giây).")]
        [SerializeField, Min(0.1f)] private float vienBinhTinhLinhCooldown = 20.0f;

        [Header("Sub-Arena & Sub-Prefabs")]
        [Tooltip("Prefab Buồng Lõi Pháo Hạm (D1onkMothershipSubArena).")]
        [SerializeField] private GameObject subArenaPrefab;

        [Tooltip("Tọa độ buồng lái lõi pháo hạm trên Scene.")]
        [SerializeField] private Vector2 cockpitCenter = new Vector2(200f, 100f);

        [Tooltip("Prefab Bản sao Tank ngoài sân (D1onkTankClone).")]
        [SerializeField] private GameObject tankClonePrefab;

        [Tooltip("Prefab Bản sao Mini triệu hồi (D1onkMiniClone).")]
        [SerializeField] private GameObject miniClonePrefab;

        // References
        private Animator m_Animator;
        private D1onkVFXController m_VfxController;

        // Runtime Skill & Passive Timers
        private float m_PassiveMissileTimer;
        private float m_TrungPhatTimer;
        private float m_LazeTimer;
        private float m_ToaNhietTimer;
        private float m_VienBinhTimer;

        // Sub-Arena & Outside Challenges
        private D1onkMothershipSubArena m_SubArena;
        private D1onkCoopChallenges m_CoopChallenges;
        private readonly List<D1onkTankClone> m_ActiveTankClones = new List<D1onkTankClone>();
        private readonly List<D1onkMiniClone> m_ActiveMiniClones = new List<D1onkMiniClone>();

        // Debuff / Buff timers & storage for Reward / Debuff phases
        private float m_BossDebuffTimer;
        private bool m_IsBossDebuffed;
        private float m_BossStunTimer;
        private float m_OutsideEnergyBuffTimer;
        private bool m_IsOutsideEnergyBuffActive;
        private readonly List<TankBrain> m_RewardInsideTanks = new List<TankBrain>();
        private readonly List<TankBrain> m_RewardOutsideTanks = new List<TankBrain>();

        // Laser Coroutine tracking
        private Coroutine m_LaserCoroutine;

        // Animator parameter hashes
        private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
        private static readonly int HashTrungPhat = Animator.StringToHash("TrungPhat");
        private static readonly int HashLazeNguyenTo = Animator.StringToHash("LazeNguyenTo");
        private static readonly int HashToaNhietDienRong = Animator.StringToHash("ToaNhietDienRong");
        private static readonly int HashVienBinhTinhLinh = Animator.StringToHash("VienBinhTinhLinh");
        private static readonly int HashBurstCharging = Animator.StringToHash("BurstCharging");
        private static readonly int HashCharging = Animator.StringToHash("Charging");
        private static readonly int HashStun = Animator.StringToHash("Stun");
        private static readonly int HashDie = Animator.StringToHash("Die");
        private static readonly int HashPhaoHamDaNong = Animator.StringToHash("PhaoHamDaNong");

        public D1onkVFXController VFXController => m_VfxController;
        public D1onkMothershipSubArena SubArena => m_SubArena;
        public D1onkCoopChallenges CoopChallenges => m_CoopChallenges;
        public IReadOnlyList<D1onkTankClone> ActiveTankClones => m_ActiveTankClones;
        public IReadOnlyList<D1onkMiniClone> ActiveMiniClones => m_ActiveMiniClones;

        public float TrungPhatTimer => m_TrungPhatTimer;
        public float LazeTimer => m_LazeTimer;
        public float ToaNhietTimer => m_ToaNhietTimer;
        public float VienBinhTimer => m_VienBinhTimer;
        public float PassiveMissileTimer => m_PassiveMissileTimer;
        public float BossStunTimer => m_BossStunTimer;
        public bool IsBossStunned => m_BossStunTimer > 0f;

        public float BurstAttackMultiplier => burstAttackMultiplier;
        public float BurstDefenseMultiplier => burstDefenseMultiplier;

        protected override void Awake()
        {
            base.Awake();

            m_VfxController = GetComponent<D1onkVFXController>();
            if (m_Animator == null && Model != null)
            {
                m_Animator = Model.Animator;
            }
            if (m_Animator == null)
            {
                m_Animator = GetComponentInChildren<Animator>();
            }
            if (rotationPivot == null)
            {
                rotationPivot = Model != null ? Model.transform : transform;
            }

            // Cấu hình chuẩn GDD
            burstHpThreshold = 0.6f;
            burstChargeTime = 2.0f;
            burstDuration = 40.0f;
            rewardPhaseDuration = 10.0f;
            penaltyPhaseDuration = 5.0f;
            globalSkillInterval = 2.0f;
        }

        public override void Initialize(EnemyStats overrideStats = null)
        {
            // Kiểm tra số lượng Tank để tự động nhận diện Solo hay Co-op
            var tanks = UnityEngine.Object.FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            bool isCoop = tanks != null && tanks.Length >= 2;

            base.Initialize(overrideStats);

            burstHpThreshold = 0.6f;
            burstChargeTime = 2.0f;
            burstDuration = 40.0f;
            rewardPhaseDuration = 10.0f;
            penaltyPhaseDuration = 5.0f;
            globalSkillInterval = 2.0f;

            m_PassiveMissileTimer = 0f;
            m_TrungPhatTimer = 0f;
            m_LazeTimer = 0f;
            m_ToaNhietTimer = 0f;
            m_VienBinhTimer = 0f;

            m_BossDebuffTimer = 0f;
            m_IsBossDebuffed = false;
            m_BossStunTimer = 0f;
            m_OutsideEnergyBuffTimer = 0f;
            m_IsOutsideEnergyBuffActive = false;
        }

        public override void SetCoopMode(bool isCoop)
        {
            if (Stats == null) return;
            Stats.MaxHealth = isCoop ? 54000f : 27000f;
            Heal(MaxHealth);
        }

        #region Animation Helpers

        private void TriggerAnimator(int hash)
        {
            if (m_Animator != null)
            {
                m_Animator.SetTrigger(hash);
            }
        }

        private void SetAnimatorMoveSpeed(float speed)
        {
            if (m_Animator != null)
            {
                m_Animator.SetFloat(HashMoveSpeed, speed);
            }
            Model?.SetMoveSpeed(speed);
        }

        #endregion

        #region Brain Tick & Normal AI

        protected override void OnEliteTick(float deltaTime)
        {
            base.OnEliteTick(deltaTime);

            if (!IsAlive) return;

            // Xử lý đếm lùi buff/debuff
            UpdateRewardBuffs(deltaTime);

            // Nếu đang bị choáng trong phase Thưởng (choáng 5s) thì dừng toàn bộ di chuyển và kỹ năng
            if (m_BossStunTimer > 0f)
            {
                SetAnimatorMoveSpeed(0f);
                return;
            }

            // Nếu đang trong Energy Burst (Charging hoặc Active) thì dừng di chuyển thường
            if (IsBurstInProgress)
            {
                SetAnimatorMoveSpeed(0f);
                return;
            }

            // Cập nhật bộ đếm hồi chiêu kỹ năng
            if (m_TrungPhatTimer > 0f) m_TrungPhatTimer = Mathf.Max(0f, m_TrungPhatTimer - deltaTime);
            if (m_LazeTimer > 0f) m_LazeTimer = Mathf.Max(0f, m_LazeTimer - deltaTime);
            if (m_ToaNhietTimer > 0f) m_ToaNhietTimer = Mathf.Max(0f, m_ToaNhietTimer - deltaTime);
            if (m_VienBinhTimer > 0f) m_VienBinhTimer = Mathf.Max(0f, m_VienBinhTimer - deltaTime);

            // BỊ ĐỘNG — PHÁO HẠM ĐA NÒNG (Định kỳ mỗi 5s tự tạo 2 đạn truy đuổi)
            m_PassiveMissileTimer += deltaTime;
            if (m_PassiveMissileTimer >= 5.0f)
            {
                m_PassiveMissileTimer = 0f;
                FirePassiveMissiles(2);
            }

            // AI di chuyển và thi triển kỹ năng
            TankBrain closestTank = FindClosestTank();
            if (closestTank != null)
            {
                Vector2 toTarget = (Vector2)closestTank.transform.position - (Vector2)transform.position;
                float dist = toTarget.magnitude;

                if (dist > stopDistance && CanMove)
                {
                    float currentSpeed = (Stats != null ? Stats.MoveSpeed : 70f) / moveSpeedDivisor;
                    Vector2 moveDir = toTarget.normalized;
                    transform.position += (Vector3)(moveDir * (currentSpeed * deltaTime));
                    SetAnimatorMoveSpeed(currentSpeed);
                    RotateFacing(moveDir, deltaTime);
                }
                else
                {
                    SetAnimatorMoveSpeed(0f);
                    if (dist > 0.1f)
                    {
                        RotateFacing(toTarget.normalized, deltaTime);
                    }
                }

                // Thi triển kỹ năng nếu đủ điều kiện (gated bởi CanCastAnySkill / globalSkillInterval)
                if (CanCastAnySkill && CanUseSkill)
                {
                    TryCastAvailableSkill(closestTank);
                }
            }
            else
            {
                SetAnimatorMoveSpeed(0f);
            }
        }

        private void RotateFacing(Vector2 dir, float dt)
        {
            if (rotationPivot == null || dir.sqrMagnitude < 0.001f) return;
            float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (rotationSpeed <= 0f)
            {
                rotationPivot.rotation = Quaternion.Euler(0f, 0f, targetAngle);
            }
            else
            {
                float currentAngle = rotationPivot.eulerAngles.z;
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * dt);
                rotationPivot.rotation = Quaternion.Euler(0f, 0f, newAngle);
            }
        }

        #endregion

        #region Bị Động — Pháo Hạm Đa Nòng

        /// <summary>
        /// Bắn số lượng đạn tên lửa truy đuổi chỉ định (20% ATK/viên).
        /// Kích hoạt diễn hoạt PhaoHamDaNong trên Animator.
        /// </summary>
        public void FirePassiveMissiles(int count)
        {
            if (count <= 0) return;

            TriggerAnimator(HashPhaoHamDaNong);

            TankBrain target = FindClosestTank();
            Transform targetTransform = target != null ? target.transform : null;
            float missileDmg = (Stats != null ? Stats.Attack : 500f) * 0.20f;

            if (m_VfxController != null)
            {
                m_VfxController.FireRandomBarrels(count, targetTransform, missileDmg);
            }
            else
            {
                // Fallback nếu không có VFX Controller trong headless unit test: gây sát thương trực tiếp lên mục tiêu
                if (target != null && target.IsAlive)
                {
                    for (int i = 0; i < count; i++)
                    {
                        DamageInfo info = new DamageInfo(this, target, missileDmg, 0f, target.transform.position, sourceType: DamageSourceType.EnemyAttack);
                        target.TakeDamage(info);
                    }
                }
            }
        }

        #endregion

        #region Bộ Kỹ Năng

        private void TryCastAvailableSkill(TankBrain target)
        {
            if (m_TrungPhatTimer <= 0f)
            {
                CastTrungPhat();
            }
            else if (m_LazeTimer <= 0f)
            {
                CastLazeNguyenTo();
            }
            else if (m_ToaNhietTimer <= 0f)
            {
                CastToaNhietDienRong();
            }
            else if (m_VienBinhTimer <= 0f)
            {
                CastVienBinhTinhLinh();
            }
        }

        /// <summary>
        /// Kỹ năng 1: Trừng Phạt (TrungPhat):
        /// - Gây 150% ATK và -20% DEF trong 5s lên Tank gần nhất.
        /// - Giảm -30% ATK và -10% SPD trong 5s lên Tank xa nhất.
        /// - Solo (1 Tank): Chỉ nhận nhánh Tank gần nhất.
        /// - Kích hoạt bị động: Tạo 5 đạn truy đuổi.
        /// </summary>
        public void CastTrungPhat()
        {
            m_TrungPhatTimer = trungPhatCooldown;
            TriggerGlobalSkillCooldown(globalSkillInterval);
            TriggerAnimator(HashTrungPhat);

            var (nearest, furthest) = FindNearestAndFurthestTanks();

            float atk = Stats != null ? Stats.Attack : 500f;

            if (nearest != null)
            {
                // Nhánh gần nhất: 150% ATK + giảm 20% DEF trong 5s
                DamageInfo dmg = new DamageInfo(this, nearest, atk * 1.50f, 0f, nearest.transform.position, sourceType: DamageSourceType.EnemyAttack);
                nearest.TakeDamage(dmg);
                TextDamageManager.Instance?.Show(dmg.FinalDamage, nearest.transform.position);

                nearest.AddBonusStats("D1onk_TrungPhat_Def", null, new TankStats { Defense = -0.20f });
                StartCoroutine(RemoveBonusAfterDelay(nearest, "D1onk_TrungPhat_Def", 5.0f));

                m_VfxController?.PlayTrungPhat(nearest.transform.position, atk * 1.50f);
            }

            // Nhánh xa nhất (chỉ kích hoạt khi có từ 2 Tank trở lên và khác Tank gần nhất)
            if (furthest != null && furthest != nearest)
            {
                furthest.AddBonusStats("D1onk_TrungPhat_Debuff", null, new TankStats { Attack = -0.30f, MoveSpeed = -0.10f });
                StartCoroutine(RemoveBonusAfterDelay(furthest, "D1onk_TrungPhat_Debuff", 5.0f));
            }

            // Bị động: Tạo 5 đạn truy đuổi
            FirePassiveMissiles(5);
        }

        /// <summary>
        /// Kỹ năng 2: Laze Nguyên Tố (LazeNguyenTo):
        /// - Phóng laser thiêu đốt mục tiêu xa nhất 5s (40% ATK mỗi 0.5s = 10 ticks).
        /// - Kích hoạt bị động: Tạo 5 đạn truy đuổi.
        /// </summary>
        public void CastLazeNguyenTo()
        {
            m_LazeTimer = lazeNguyenToCooldown;
            TriggerGlobalSkillCooldown(globalSkillInterval);
            TriggerAnimator(HashLazeNguyenTo);

            var (_, furthest) = FindNearestAndFurthestTanks();
            TankBrain target = furthest ?? FindClosestTank();

            if (target != null)
            {
                Vector3 dir = (target.transform.position - transform.position).normalized;
                m_VfxController?.PlayLazeNguyenTo(dir, 5.0f, (Stats != null ? Stats.Attack : 500f) * 0.80f);

                if (m_LaserCoroutine != null) StopCoroutine(m_LaserCoroutine);
                m_LaserCoroutine = StartCoroutine(LaserDamageTicks(target, 5.0f, 0.5f));
            }

            // Bị động: Tạo 5 đạn truy đuổi
            FirePassiveMissiles(5);
        }

        private IEnumerator LaserDamageTicks(TankBrain target, float duration, float interval)
        {
            float elapsed = 0f;
            float atk = Stats != null ? Stats.Attack : 500f;
            float tickDmg = atk * 0.40f;

            while (elapsed < duration)
            {
                yield return new WaitForSeconds(interval);
                elapsed += interval;

                if (target != null && target.IsAlive)
                {
                    DamageInfo info = new DamageInfo(this, target, tickDmg, 0f, target.transform.position, sourceType: DamageSourceType.EnemyAttack);
                    target.TakeDamage(info);
                    TextDamageManager.Instance?.Show(info.FinalDamage, target.transform.position);
                }
            }
            m_LaserCoroutine = null;
        }

        /// <summary>
        /// Kỹ năng 3: Tỏa Nhiệt Diện Rộng (ToaNhietDienRong):
        /// - Nổ sóng nhiệt toàn map gây 100% ATK.
        /// - Tank chịu đòn đầu x2 sát thương (200% ATK), các Tank sau giảm dần (1.7x, 1.3x, 1.0x).
        /// - Kích hoạt bị động: Tạo 5 đạn truy đuổi.
        /// </summary>
        public void CastToaNhietDienRong()
        {
            m_ToaNhietTimer = toaNhietDienRongCooldown;
            TriggerGlobalSkillCooldown(globalSkillInterval);
            TriggerAnimator(HashToaNhietDienRong);

            float atk = Stats != null ? Stats.Attack : 500f;
            m_VfxController?.PlayToaNhietDienRong(atk);

            // Sắp xếp các Tank theo thứ tự từ gần đến xa
            var sortedTanks = GetSortedTanksByDistance();
            float[] multipliers = { 2.0f, 1.7f, 1.3f, 1.0f };

            for (int i = 0; i < sortedTanks.Count; i++)
            {
                TankBrain t = sortedTanks[i];
                if (t != null && t.IsAlive)
                {
                    float mult = i < multipliers.Length ? multipliers[i] : 1.0f;
                    float finalDmg = atk * mult;

                    DamageInfo info = new DamageInfo(this, t, finalDmg, 0f, t.transform.position, sourceType: DamageSourceType.EnemyAttack);
                    t.TakeDamage(info);
                    TextDamageManager.Instance?.Show(info.FinalDamage, t.transform.position);
                }
            }

            // Bị động: Tạo 5 đạn truy đuổi
            FirePassiveMissiles(5);
        }

        /// <summary>
        /// Kỹ năng 4: Viện Binh Tinh Linh (VienBinhTinhLinh):
        /// - Triệu hồi 1-3 bản sao thu nhỏ (20% chỉ số Boss gốc).
        /// - Kích hoạt bị động: Tạo 5 đạn truy đuổi.
        /// </summary>
        public void CastVienBinhTinhLinh(int count = -1)
        {
            m_VienBinhTimer = vienBinhTinhLinhCooldown;
            TriggerGlobalSkillCooldown(globalSkillInterval);
            TriggerAnimator(HashVienBinhTinhLinh);

            if (count < 1) count = UnityEngine.Random.Range(1, 4);
            count = Mathf.Clamp(count, 1, 3);

            for (int i = 0; i < count; i++)
            {
                float a = (i * (360f / count) + UnityEngine.Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
                Vector3 spawnPos = transform.position + new Vector3(Mathf.Cos(a) * 4f, Mathf.Sin(a) * 4f, 0f);

                GameObject go = null;
                D1onkMiniClone clone = null;

                if (miniClonePrefab != null)
                {
                    go = Instantiate(miniClonePrefab, spawnPos, Quaternion.identity);
                    clone = go.GetComponent<D1onkMiniClone>();
                }
                else
                {
                    go = new GameObject($"D1onkMiniClone_{m_ActiveMiniClones.Count + 1}");
                    clone = go.AddComponent<D1onkMiniClone>();
                }

                if (clone != null)
                {
                    clone.InitializeClone(this, spawnPos);
                    m_ActiveMiniClones.Add(clone);
                }
            }

            m_VfxController?.SummonReinforcements(count);

            // Bị động: Tạo 5 đạn truy đuổi
            FirePassiveMissiles(5);
        }

        #endregion

        #region Vòng Đời Bùng Nổ Năng Lượng (Energy Burst)

        protected override void OnBurstChargingStart()
        {
            base.OnBurstChargingStart();
            StopLaser();
            TriggerAnimator(HashBurstCharging);
            m_VfxController?.StartCharging();
        }

        protected override void OnBurstStart()
        {
            m_VfxController?.StopCharging();

            // 1. Hồi 100% HP theo GDD
            Heal(MaxHealth);

            // 2. Tăng 30% ATK, tăng 100% DEF đã được áp dụng tự động qua burstAttackMultiplier & burstDefenseMultiplier trong base TransitionToState

            // 3. Quét các Tank còn sống
            var allTanks = UnityEngine.Object.FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            var aliveTanks = new List<TankBrain>();
            for (int i = 0; i < allTanks.Length; i++)
            {
                if (allTanks[i] != null && allTanks[i].IsAlive)
                {
                    aliveTanks.Add(allTanks[i]);
                }
            }

            bool isCoop = aliveTanks.Count >= 2;

            // 4. Khởi tạo Sub-Arena buồng lái
            if (m_SubArena == null)
            {
                if (subArenaPrefab != null)
                {
                    GameObject saObj = Instantiate(subArenaPrefab, cockpitCenter, Quaternion.identity);
                    m_SubArena = saObj.GetComponent<D1onkMothershipSubArena>();
                }
                else
                {
                    GameObject saObj = new GameObject("D1onkMothershipSubArena");
                    m_SubArena = saObj.AddComponent<D1onkMothershipSubArena>();
                }
            }

            // 5. Khởi tạo Outside Challenges Manager cho Co-op
            if (isCoop && m_CoopChallenges == null)
            {
                GameObject ccObj = new GameObject("D1onkCoopChallenges");
                m_CoopChallenges = ccObj.AddComponent<D1onkCoopChallenges>();
            }

            m_SubArena.InitializeArena(this, m_CoopChallenges, isCoop, cockpitCenter);

            if (m_VfxController != null && m_VfxController.NormalEnemyPrefabs != null)
            {
                m_SubArena.SetNormalEnemyPrefabs(m_VfxController.NormalEnemyPrefabs);
                if (m_CoopChallenges != null)
                {
                    m_CoopChallenges.SetNormalEnemyPrefabs(m_VfxController.NormalEnemyPrefabs);
                }
            }

            // 6. Phân bổ Tank vào buồng lái
            aliveTanks.Sort((a, b) =>
                Vector2.Distance(transform.position, a.transform.position).CompareTo(
                Vector2.Distance(transform.position, b.transform.position)));

            m_RewardInsideTanks.Clear();
            m_RewardOutsideTanks.Clear();
            m_ActiveTankClones.Clear();

            if (!isCoop)
            {
                // Solo: 1 Tank được kéo vào trong buồng lái
                if (aliveTanks.Count > 0)
                {
                    TankBrain soloTank = aliveTanks[0];
                    m_SubArena.TrapTank(soloTank);
                    m_RewardInsideTanks.Add(soloTank);
                }
            }
            else
            {
                // Co-op: Kéo 2 Tank gần nhất vào buồng lái
                TankBrain tank1 = aliveTanks[0];
                TankBrain tank2 = aliveTanks[1];

                m_SubArena.TrapTank(tank1);
                m_SubArena.TrapTank(tank2);
                m_RewardInsideTanks.Add(tank1);
                m_RewardInsideTanks.Add(tank2);

                // Các Tank còn lại ở ngoài
                for (int i = 2; i < aliveTanks.Count; i++)
                {
                    m_RewardOutsideTanks.Add(aliveTanks[i]);
                }

                // Sinh bản sao của 2 Tank bị kéo vào trong để chiến đấu bên ngoài
                SpawnTankCloneOutside(tank1, transform.position + new Vector3(-3f, 2f, 0f));
                SpawnTankCloneOutside(tank2, transform.position + new Vector3(3f, 2f, 0f));

                m_CoopChallenges.Initialize(this, m_ActiveTankClones);
            }
        }

        private void SpawnTankCloneOutside(TankBrain originalTank, Vector3 spawnPos)
        {
            if (originalTank == null) return;

            GameObject go = null;
            D1onkTankClone clone = null;

            if (tankClonePrefab != null)
            {
                go = Instantiate(tankClonePrefab, spawnPos, Quaternion.identity);
                clone = go.GetComponent<D1onkTankClone>();
            }
            else
            {
                go = new GameObject($"TankClone_{originalTank.name}");
                clone = go.AddComponent<D1onkTankClone>();
            }

            if (clone != null)
            {
                clone.InitializeTankClone(originalTank, spawnPos);
                m_ActiveTankClones.Add(clone);
            }
        }

        protected override void OnBurstTick(float deltaTime)
        {
            base.OnBurstTick(deltaTime);

            if (m_SubArena != null)
            {
                m_SubArena.ManualTick(deltaTime);
            }

            if (m_CoopChallenges != null)
            {
                m_CoopChallenges.ManualTick(deltaTime);
            }
        }

        /// <summary>
        /// THƯỞNG BÙNG NỔ NĂNG LƯỢNG (Khi Trái Tim Boss bị tiêu diệt):
        /// - Boss: Trừ ngay 30% Max HP, giảm 50% ATK & DEF trong 10s, Choáng 5s.
        /// - 2 Tank bên trong: +100% toàn bộ chỉ số trong 10s.
        /// - 2 Tank bên ngoài: -50% hồi chiêu, hồi năng lượng liên tục trong 10s.
        /// - Toàn bộ Tank: Reset hồi chiêu toàn bộ.
        /// </summary>
        protected override void OnBurstSuccess()
        {
            // 1. Dọn dẹp Sub-Arena và đưa Tank ra ngoài
            if (m_SubArena != null)
            {
                m_SubArena.CleanupArena();
                m_SubArena = null;
            }

            if (m_CoopChallenges != null)
            {
                m_CoopChallenges.Cleanup();
                if (Application.isPlaying) Destroy(m_CoopChallenges.gameObject);
                else DestroyImmediate(m_CoopChallenges.gameObject);
                m_CoopChallenges = null;
            }

            CleanupTankClones();

            // 2. Phạt Boss: Trừ 30% Max HP
            float hpLoss = MaxHealth * 0.30f;
            TakeDamage(new DamageInfo(this, this, hpLoss, 1.0f, transform.position, isDirectToHealth: true));

            // Hoàn trả Burst Stat Modifiers trước để đưa Attack & Defense về chỉ số gốc (500 ATK, 60 DEF)
            RevertBurstStatModifiers();

            // Giảm 50% ATK & DEF trong 10s (500 -> 250, 60 -> 30)
            if (Stats != null)
            {
                Stats.Attack = 250f;
                Stats.Defense = 30f;
            }
            m_BossDebuffTimer = 10.0f;
            m_IsBossDebuffed = true;

            // Choáng 5s: ngăn di chuyển và thi triển kỹ năng
            m_BossStunTimer = 5.0f;
            TriggerAnimator(HashStun);
            m_VfxController?.PlayStun(5.0f);

            // 3. Thưởng Tank bên trong: +100% tất cả chỉ số trong 10s
            for (int i = 0; i < m_RewardInsideTanks.Count; i++)
            {
                TankBrain t = m_RewardInsideTanks[i];
                if (t != null && t.IsAlive)
                {
                    t.AddBonusStats("D1onk_Inside100Buff", null, new TankStats
                    {
                        Attack = 1.0f,
                        Defense = 1.0f,
                        MaxHealth = 1.0f,
                        MoveSpeed = 1.0f
                    });
                    StartCoroutine(RemoveBonusAfterDelay(t, "D1onk_Inside100Buff", 10.0f));
                }
            }

            // 4. Thưởng Tank bên ngoài: -50% hồi chiêu, hồi năng lượng liên tục 10s
            for (int i = 0; i < m_RewardOutsideTanks.Count; i++)
            {
                TankBrain t = m_RewardOutsideTanks[i];
                if (t != null && t.IsAlive)
                {
                    t.AddBonusStats("D1onk_OutsideCooldownBuff", null, new TankStats
                    {
                        CooldownReduction = 0.50f
                    });
                    StartCoroutine(RemoveBonusAfterDelay(t, "D1onk_OutsideCooldownBuff", 10.0f));
                }
            }
            m_OutsideEnergyBuffTimer = 10.0f;
            m_IsOutsideEnergyBuffActive = true;

            // 5. Reset hồi chiêu toàn bộ Tank
            var allTanks = UnityEngine.Object.FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            for (int i = 0; i < allTanks.Length; i++)
            {
                if (allTanks[i] != null)
                {
                    allTanks[i].ResetAllSkillCooldowns();
                }
            }
        }

        /// <summary>
        /// HÌNH PHẠT BÙNG NỔ NĂNG LƯỢNG (Khi hết 40s mà Trái Tim chưa bị tiêu diệt):
        /// - Đưa Tank ra ngoài sân.
        /// - Xả hàng trăm đạn pháo tiêu diệt lập tức toàn bộ người chơi (INSTANT KILL).
        /// </summary>
        protected override void OnBurstFail()
        {
            // 1. Dọn dẹp buồng lái và trả Tank ra ngoài
            if (m_SubArena != null)
            {
                m_SubArena.CleanupArena();
                m_SubArena = null;
            }

            if (m_CoopChallenges != null)
            {
                m_CoopChallenges.Cleanup();
                if (Application.isPlaying) Destroy(m_CoopChallenges.gameObject);
                else DestroyImmediate(m_CoopChallenges.gameObject);
                m_CoopChallenges = null;
            }

            CleanupTankClones();

            // 2. Diễn hoạt xả đại pháo hủy diệt (hàng trăm đạn pháo / toàn bộ nòng súng)
            TriggerAnimator(HashPhaoHamDaNong);
            if (m_VfxController != null)
            {
                m_VfxController.FireMultiBarrelSalvo(null, 999999f);
            }
            else
            {
                FirePassiveMissiles(14);
            }

            // 3. INSTANT KILL toàn bộ Tank người chơi!
            var allTanks = UnityEngine.Object.FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            for (int i = 0; i < allTanks.Length; i++)
            {
                TankBrain tank = allTanks[i];
                if (tank != null && tank.IsAlive)
                {
                    DamageInfo fatalDmg = new DamageInfo(this, tank, 999999f, 1.0f, tank.transform.position, isDirectToHealth: true, sourceType: DamageSourceType.EnemyAttack);
                    tank.TakeDamage(fatalDmg);
                }
            }
        }

        private void UpdateRewardBuffs(float dt)
        {
            if (m_BossStunTimer > 0f)
            {
                m_BossStunTimer = Mathf.Max(0f, m_BossStunTimer - dt);
            }

            if (m_IsBossDebuffed)
            {
                m_BossDebuffTimer -= dt;
                if (m_BossDebuffTimer <= 0f)
                {
                    m_IsBossDebuffed = false;
                    if (Stats != null)
                    {
                        Stats.Attack = 500f;
                        Stats.Defense = 60f;
                    }
                }
            }

            if (m_IsOutsideEnergyBuffActive)
            {
                m_OutsideEnergyBuffTimer -= dt;
                for (int i = 0; i < m_RewardOutsideTanks.Count; i++)
                {
                    TankBrain t = m_RewardOutsideTanks[i];
                    if (t != null && t.IsAlive)
                    {
                        t.GainEnergy(t.FinalStats.MaxEnergy * 0.20f * dt);
                    }
                }

                if (m_OutsideEnergyBuffTimer <= 0f)
                {
                    m_IsOutsideEnergyBuffActive = false;
                }
            }
        }

        private void CleanupTankClones()
        {
            for (int i = 0; i < m_ActiveTankClones.Count; i++)
            {
                if (m_ActiveTankClones[i] != null)
                {
                    if (Application.isPlaying) Destroy(m_ActiveTankClones[i].gameObject);
                    else DestroyImmediate(m_ActiveTankClones[i].gameObject);
                }
            }
            m_ActiveTankClones.Clear();
        }

        #endregion

        #region Helpers & Targeting

        private TankBrain FindClosestTank()
        {
            var tanks = UnityEngine.Object.FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            TankBrain closest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain t = tanks[i];
                if (t != null && t.IsAlive)
                {
                    float d = Vector2.Distance(transform.position, t.transform.position);
                    if (d < minDist)
                    {
                        minDist = d;
                        closest = t;
                    }
                }
            }
            return closest;
        }

        private (TankBrain nearest, TankBrain furthest) FindNearestAndFurthestTanks()
        {
            var allTanks = UnityEngine.Object.FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            var tanks = new List<TankBrain>();
            for (int i = 0; i < allTanks.Length; i++)
            {
                if (allTanks[i] != null && allTanks[i].IsAlive) tanks.Add(allTanks[i]);
            }

            if (tanks.Count == 0) return (null, null);
            if (tanks.Count == 1) return (tanks[0], tanks[0]);

            TankBrain nearest = null;
            TankBrain furthest = null;
            float minDist = float.MaxValue;
            float maxDist = float.MinValue;

            for (int i = 0; i < tanks.Count; i++)
            {
                TankBrain t = tanks[i];
                float d = Vector2.Distance(transform.position, t.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = t;
                }
                if (d > maxDist)
                {
                    maxDist = d;
                    furthest = t;
                }
            }

            if (furthest == nearest && tanks.Count >= 2)
            {
                furthest = (nearest == tanks[0]) ? tanks[1] : tanks[0];
            }

            return (nearest, furthest);
        }

        private List<TankBrain> GetSortedTanksByDistance()
        {
            var tanks = UnityEngine.Object.FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            var list = new List<TankBrain>();
            for (int i = 0; i < tanks.Length; i++)
            {
                if (tanks[i] != null && tanks[i].IsAlive)
                {
                    list.Add(tanks[i]);
                }
            }
            list.Sort((a, b) =>
                Vector2.Distance(transform.position, a.transform.position).CompareTo(
                Vector2.Distance(transform.position, b.transform.position)));
            return list;
        }

        private IEnumerator RemoveBonusAfterDelay(TankBrain tank, string key, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (tank != null)
            {
                tank.RemoveBonusStats(key);
            }
        }

        private void StopLaser()
        {
            if (m_LaserCoroutine != null)
            {
                StopCoroutine(m_LaserCoroutine);
                m_LaserCoroutine = null;
            }
        }

        public override void TakeDamage(DamageInfo info)
        {
            // Trong khi Bùng Nổ Năng Lượng (Charging hoặc Active), bảo vệ Boss không bị hạ gục từ bên ngoài
            // Mục tiêu cốt lõi là hạ Trái Tim trong buồng lái Sub-Arena
            if (IsBurstInProgress && !info.IsDirectToHealth)
            {
                float potentialHp = CurrentHealth - info.RawDamage;
                if (potentialHp < 1f)
                {
                    float safeDmg = Mathf.Max(0f, CurrentHealth - 1f);
                    if (safeDmg > 0f)
                    {
                        base.TakeDamage(new DamageInfo(info.Attacker, this, safeDmg, info.ArmorPenetration, info.HitPosition, info.IsDirectToHealth, sourceType: info.SourceType));
                    }
                    return;
                }
            }

            base.TakeDamage(info);
        }

        protected override void OnDeath()
        {
            StopLaser();
            if (m_SubArena != null)
            {
                m_SubArena.CleanupArena();
                m_SubArena = null;
            }
            if (m_CoopChallenges != null)
            {
                m_CoopChallenges.Cleanup();
                if (Application.isPlaying) Destroy(m_CoopChallenges.gameObject);
                else DestroyImmediate(m_CoopChallenges.gameObject);
                m_CoopChallenges = null;
            }
            CleanupTankClones();
            base.OnDeath();
            TriggerAnimator(HashDie);
            m_VfxController?.PlayDie();
        }

        #endregion
    }
}
