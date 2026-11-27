using System.Collections;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Core.Effects;
using IronWasteland.Stages;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.The2Face
{
    /// <summary>
    /// Boss 9 - The2Face (Boss 2 ải 2-4).
    /// Kế thừa EliteEnemyBrain, tập trung vào 3 cơ chế:
    /// - Hào quang từ trường làm chậm Tank quanh Boss và khiên năng lượng chặn 1 đòn mỗi 10 giây.
    /// - Bộ kỹ năng TuNhiet / CamLang / HutVaDa.
    /// - Bùng nổ tạo các Điểm Sáng, mở khóa kỹ năng Tank theo số đèn đang bật.
    /// </summary>
    public class The2FaceBrain : EliteEnemyBrain, IronWasteland.Net.INetVfxReceiver
    {
        private const string ShieldEffectId = "The2Face_Shield";
        private const string RewardAttackKey = "The2Face_Reward_Atk";
        private const string PenaltyKey = "The2Face_Penalty";
        private const float SkillLockCooldown = 9999f;

        [Header("Bị động - Hào quang từ trường")]
        [Tooltip("Bán kính hào quang làm chậm Tank (mét).")]
        [SerializeField, Min(0f)] private float auraRadius = 4f;

        [Tooltip("Tỷ lệ giảm tốc độ di chuyển của Tank trong hào quang (0.2 = 20%).")]
        [SerializeField, Range(0f, 1f)] private float auraSlowPercent = 0.2f;

        [Tooltip("Chu kỳ làm mới hiệu ứng làm chậm của hào quang (giây).")]
        [SerializeField, Min(0.05f)] private float auraRefreshInterval = 0.3f;

        [Tooltip("Vòng sáng thể hiện vùng hào quang. Có thể để trống nếu không cần hình ảnh.")]
        [SerializeField] private SpriteRenderer auraVisual;

        [Tooltip("Đường kính hình ảnh hào quang (mét).")]
        [SerializeField, Min(0.1f)] private float auraVisualDiameter = 8f;

        [Header("Bị động - Khiên năng lượng")]
        [Tooltip("Chu kỳ tự nhận một lớp khiên chặn hoàn toàn một đòn (giây).")]
        [SerializeField, Min(0.5f)] private float shieldInterval = 10f;

        [Tooltip("Hình ảnh lớp khiên. Có thể để trống nếu không cần hình ảnh.")]
        [SerializeField] private SpriteRenderer shieldVisual;

        [Tooltip("Đường kính hình ảnh khiên (mét).")]
        [SerializeField, Min(0.1f)] private float shieldVisualDiameter = 3.6f;

        [Tooltip("Tốc độ nhấp nháy của hình ảnh khiên.")]
        [SerializeField, Min(0.1f)] private float shieldPulseSpeed = 3f;

        [Header("TuNhiet - Xoáy năng lượng")]
        [Tooltip("Hồi chiêu kỹ năng TuNhiet (giây).")]
        [SerializeField, Min(0.5f)] private float tuNhietCooldown = 8f;

        [Tooltip("Prefab vùng xoáy năng lượng.")]
        [SerializeField] private GameObject vortexPrefab;

        [Tooltip("Bán kính vùng xoáy (mét).")]
        [SerializeField, Min(0.5f)] private float vortexRadius = 3.5f;

        [Tooltip("Thời gian tồn tại của vùng xoáy (giây).")]
        [SerializeField, Min(0.5f)] private float vortexDuration = 5f;

        [Tooltip("Tỷ lệ sát thương mỗi giây của trạng thái Buồn bã so với ATK Boss (0.2 = 20% ATK/s).")]
        [SerializeField, Range(0f, 5f)] private float vortexSadDamageRatio = 0.2f;

        [Tooltip("Tỷ lệ sát thương mỗi giây của trạng thái Tức giận so với ATK Boss (0.4 = 40% ATK/s).")]
        [SerializeField, Range(0f, 5f)] private float vortexAngerDamageRatio = 0.4f;

        [Tooltip("Thời gian duy trì hiệu ứng Buồn bã (giây).")]
        [SerializeField, Min(0.5f)] private float sadDuration = 5f;

        [Tooltip("Tỷ lệ giảm tốc độ của Buồn bã (0.2 = 20%).")]
        [SerializeField, Range(0f, 1f)] private float sadSlowPercent = 0.2f;

        [Tooltip("Tỷ lệ giảm tấn công của Buồn bã (0.1 = 10%).")]
        [SerializeField, Range(0f, 1f)] private float sadAttackReduction = 0.1f;

        [Tooltip("Tỷ lệ giảm phòng thủ của Buồn bã (0.1 = 10%).")]
        [SerializeField, Range(0f, 1f)] private float sadDefenseReduction = 0.1f;

        [Tooltip("Tỷ lệ năng lượng tối đa bị trừ khi bị đòn đánh đầu tiên của Tức giận (0.2 = 20%).")]
        [SerializeField, Range(0f, 1f)] private float angerEnergyDrainRatio = 0.2f;

        [Header("CamLang - Câm lặng")]
        [Tooltip("Hồi chiêu kỹ năng CamLang (giây).")]
        [SerializeField, Min(0.5f)] private float camLangCooldown = 7f;

        [Tooltip("Thời gian khóa toàn bộ kỹ năng của một Tank (giây).")]
        [SerializeField, Min(0.1f)] private float camLangSilenceDuration = 5f;

        [Header("HutVaDa - Kéo và đá")]
        [Tooltip("Hồi chiêu kỹ năng HutVaDa (giây).")]
        [SerializeField, Min(0.5f)] private float hutVaDaCooldown = 12f;

        [Tooltip("Thời gian kéo Tank về phía Boss (giây).")]
        [SerializeField, Min(0.1f)] private float pullDuration = 1.2f;

        [Tooltip("Tốc độ kéo Tank (mét/giây).")]
        [SerializeField, Min(0f)] private float pullSpeed = 8f;

        [Tooltip("Prefab viên đá hội tụ.")]
        [SerializeField] private GameObject rockPrefab;

        [Tooltip("Số viên đá mỗi lần dùng HutVaDa.")]
        [SerializeField, Min(1)] private int rockCount = 8;

        [Tooltip("Tỷ lệ sát thương mỗi viên đá so với ATK Boss (0.2 = 20%).")]
        [SerializeField, Range(0f, 2f)] private float rockDamageRatio = 0.2f;

        [Tooltip("Thời gian choáng của mỗi viên đá (giây).")]
        [SerializeField, Min(0f)] private float rockStunDuration = 0.8f;

        [Tooltip("Tốc độ bay của viên đá (mét/giây).")]
        [SerializeField, Min(1f)] private float rockSpeed = 14f;

        [Tooltip("Bán kính xuất phát của các viên đá quanh Boss (mét).")]
        [SerializeField, Min(1f)] private float rockSpawnRadius = 16f;

        [Header("Di chuyển")]
        [Tooltip("Khoảng cách dừng khi áp sát Tank (mét).")]
        [SerializeField, Min(0f)] private float stopDistance = 2.6f;

        [Tooltip("Độ mượt chuyển hướng di chuyển.")]
        [SerializeField, Min(0.1f)] private float turnSharpness = 5f;

        [Tooltip("Hệ số chia tốc độ di chuyển trong bảng stats (mặc định 100).")]
        [SerializeField, Min(1f)] private float moveSpeedDivisor = 100f;

        [Header("Bùng nổ - Điểm Sáng")]
        [Tooltip("Prefab Điểm Sáng (LightPointTotem).")]
        [SerializeField] private GameObject lightPointPrefab;

        [Tooltip("Máu tối đa của mỗi Điểm Sáng.")]
        [SerializeField, Min(1f)] private float lightPointHealth = 4000f;

        [Tooltip("Tốc độ di chuyển của Điểm Sáng (mét/giây).")]
        [SerializeField, Min(0f)] private float lightPointMoveSpeed = 0.8f;

        [Tooltip("Bán kính Tank đứng trong để bật Điểm Sáng (mét).")]
        [SerializeField, Min(0.1f)] private float lightPointLitRadius = 2f;

        [Tooltip("Bán kính đặt các Điểm Sáng quanh tâm sân (mét).")]
        [SerializeField, Min(0f)] private float lightPointSpawnRadius = 5.5f;

        [Tooltip("Prefab quái đầm lầy đi phá Điểm Sáng.")]
        [SerializeField] private GameObject swampCrawlerPrefab;

        [Tooltip("Số quái đầm lầy sinh ra cho mỗi Điểm Sáng.")]
        [SerializeField, Min(0)] private int crawlerCountPerLight = 1;

        [Tooltip("Bán kính sinh quái đầm lầy quanh tâm sân (mét).")]
        [SerializeField, Min(1f)] private float crawlerSpawnRadius = 12f;

        [Tooltip("Prefab Tinh linh sấm sét tấn công Tank trong đợt quái.")]
        [SerializeField] private GameObject lightningMinionPrefab;

        [Tooltip("Số Tinh linh sấm sét sinh ra mỗi lần bùng nổ.")]
        [SerializeField, Min(0)] private int lightningMinionCount = 2;

        [Tooltip("Bán kính sinh Tinh linh sấm sét quanh tâm sân (mét).")]
        [SerializeField, Min(1f)] private float lightningSpawnRadius = 7f;

        [Tooltip("Tỷ lệ máu hồi khi bắt đầu bùng nổ (0.2 = 20% Max HP).")]
        [SerializeField, Range(0f, 1f)] private float burstHealRatio = 0.2f;

        [Tooltip("Hệ số tấn công còn lại của Boss khi người chơi phá giải thành công (0.5 = -50% ATK).")]
        [SerializeField, Range(0f, 1f)] private float rewardBossWeakenMultiplier = 0.5f;

        [Tooltip("Tỷ lệ cộng tấn công cho Tank khi phá giải thành công (0.5 = +50% ATK).")]
        [SerializeField, Range(0f, 5f)] private float rewardTankAttackBonus = 0.5f;

        [Tooltip("Tỷ lệ giảm tấn công vĩnh viễn khi thất bại (0.2 = -20%).")]
        [SerializeField, Range(0f, 1f)] private float penaltyAttackReduction = 0.2f;

        [Tooltip("Tỷ lệ giảm phòng thủ vĩnh viễn khi thất bại (0.2 = -20%).")]
        [SerializeField, Range(0f, 1f)] private float penaltyDefenseReduction = 0.2f;

        [Tooltip("Tỷ lệ giảm tốc độ vĩnh viễn khi thất bại (0.3 = -30%).")]
        [SerializeField, Range(0f, 1f)] private float penaltySpeedReduction = 0.3f;

        [Tooltip("Tâm sân dự phòng khi không tìm thấy StageManager.")]
        [SerializeField] private Vector2 arenaCenter = Vector2.zero;

        private EnemyStats m_BaseStats;
        private Rigidbody2D m_Rigidbody;
        private Vector2 m_DesiredVelocity;
        private TankBrain m_CurrentTarget;

        private float m_AuraTimer;
        private float m_ShieldTimer;
        private float m_TuNhietTimer;
        private float m_CamLangTimer;
        private float m_HutVaDaTimer;
        private DamageInterceptEffect m_ShieldEffect;
        private float m_ShieldFlashUntil;
        private bool m_SentShieldVisualActive;
        private Vector3 m_ShieldBaseScale = Vector3.one;

        private readonly List<TankBrain> m_LockedTanks = new List<TankBrain>();
        private readonly List<The2FaceLightPoint> m_LightPoints = new List<The2FaceLightPoint>();
        private readonly List<GameObject> m_Crawlers = new List<GameObject>();
        private readonly List<GameObject> m_LightningMinions = new List<GameObject>();
        private int m_TotalLightPoints;
        private int m_UnlockedSlotCount = -1;
        private bool m_BurstMechanicActive;
        private bool m_PenaltyApplied;
        private Vector2 m_ArenaCenter;
        private Coroutine m_PullRoutine;

        /// <summary>True khi cơ chế Điểm Sáng của Bùng nổ đang hoạt động.</summary>
        public bool IsBurstMechanicActive => m_BurstMechanicActive;

        /// <summary>Số Điểm Sáng còn sống.</summary>
        public int AliveLightPoints => CountAliveLights();

        /// <summary>Số Điểm Sáng đang được bật.</summary>
        public int LitLightPoints => CountLitLights();

        /// <summary>Số slot kỹ năng đang được mở cho Tank (0..4).</summary>
        public int UnlockedSkillSlots => Mathf.Max(0, m_UnlockedSlotCount);

        #region Lifecycle

        protected override void Awake()
        {
            base.Awake();

            m_Rigidbody = GetComponent<Rigidbody2D>();
            CaptureBaseStats();
            FitVisual(auraVisual, auraVisualDiameter);
            FitVisual(shieldVisual, shieldVisualDiameter);
            if (shieldVisual != null) m_ShieldBaseScale = shieldVisual.transform.localScale;
            SetVisualActive(shieldVisual, false);
            m_AuraTimer = 0f;
            m_ShieldTimer = shieldInterval;
        }

        public override void Initialize(EnemyStats overrideStats = null)
        {
            base.Initialize(overrideStats);

            CaptureBaseStats();
            m_ShieldTimer = shieldInterval;
            m_TuNhietTimer = 0f;
            m_CamLangTimer = 0f;
            m_HutVaDaTimer = 0f;
            m_UnlockedSlotCount = -1;
            m_BurstMechanicActive = false;
            m_PenaltyApplied = false;
            SetVisualActive(shieldVisual, false);
        }

        protected override void OnDeath()
        {
            base.OnDeath();

            m_BurstMechanicActive = false;
            CleanupBurstObjects();
            RemoveRewardBuffs();
            ResetSkillLocks();
            SetVisualActive(shieldVisual, false);
            SetVisualActive(auraVisual, false);

            if (m_PullRoutine != null)
            {
                StopCoroutine(m_PullRoutine);
                m_PullRoutine = null;
            }
        }

        private void CaptureBaseStats()
        {
            m_BaseStats = Stats != null ? Stats.Clone() : new EnemyStats();
        }

        #endregion

        #region Elite Tick

        protected override void OnEliteTick(float deltaTime)
        {
            UpdateTarget();
            UpdateAura(deltaTime);
            UpdateShield(deltaTime);
            UpdateMovement(deltaTime);

            if (IsBurstActive)
            {
                StopMovement();
            }
            else
            {
                UpdateNormalSkills(deltaTime);
            }

            UpdateVisuals(deltaTime);
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

        private TankBrain FindRandomTank()
        {
            TankBrain[] tanks = FindObjectsByType<TankBrain>();
            int count = 0;
            for (int i = 0; i < tanks.Length; i++)
            {
                if (tanks[i] != null && tanks[i].IsAlive) count++;
            }

            if (count == 0) return null;

            int pick = Random.Range(0, count);
            for (int i = 0; i < tanks.Length; i++)
            {
                if (tanks[i] == null || !tanks[i].IsAlive) continue;
                if (pick == 0) return tanks[i];
                pick--;
            }

            return null;
        }

        private void UpdateAura(float deltaTime)
        {
            m_AuraTimer -= deltaTime;
            if (m_AuraTimer > 0f) return;

            m_AuraTimer = auraRefreshInterval;

            TankBrain[] tanks = FindObjectsByType<TankBrain>();
            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain tank = tanks[i];
                if (tank == null || !tank.IsAlive) continue;
                if (Vector2.Distance(transform.position, tank.transform.position) > auraRadius) continue;

                TankSlowDebuff.Apply(tank, auraSlowPercent, auraRefreshInterval + 0.1f);
            }
        }

        private void UpdateShield(float deltaTime)
        {
            if (!IsAlive) return;

            m_ShieldTimer -= deltaTime;
            if (m_ShieldTimer > 0f) return;

            m_ShieldTimer = shieldInterval;
            ApplyShield();
        }

        private void ApplyShield()
        {
            DamageInterceptEffect existing = GetEffect(ShieldEffectId) as DamageInterceptEffect;
            if (existing != null)
            {
                m_ShieldEffect = existing;
                existing.OnStackAdded(this, 1);
                return;
            }

            DamageInterceptEffect effect = new DamageInterceptEffect(
                ShieldEffectId,
                "Khiên năng lượng The2Face",
                shieldInterval + 0.5f,
                this,
                1,
                false,
                true);
            effect.OnNegated += HandleShieldNegated;
            m_ShieldEffect = effect;
            ApplyEffect(effect);
        }

        private void HandleShieldNegated(DamageInfo info)
        {
            m_ShieldFlashUntil = Time.time + 0.25f;
        }

        private void UpdateVisuals(float deltaTime)
        {
            if (shieldVisual != null)
            {
                bool active = (m_ShieldEffect != null && !m_ShieldEffect.IsExpired) || Time.time < m_ShieldFlashUntil;
                SetVisualActive(shieldVisual, active);
                if (active != m_SentShieldVisualActive)
                {
                    m_SentShieldVisualActive = active;
                    GetComponent<IronWasteland.Net.EnemyNetState>()?.BroadcastVfx(
                        "The2FaceShield", transform.position, value: active ? 1f : 0f);
                }

                if (active)
                {
                    float pulse = 1f + Mathf.Sin(Time.time * shieldPulseSpeed) * 0.04f;
                    shieldVisual.transform.localScale = m_ShieldBaseScale * pulse;
                }
            }

            if (auraVisual != null && auraVisual.gameObject.activeSelf)
            {
                auraVisual.transform.Rotate(0f, 0f, -24f * deltaTime);
            }
        }

        #endregion

        #region Movement

        private void UpdateMovement(float deltaTime)
        {
            if (IsBurstCharging || IsBurstActive || m_CurrentTarget == null || !m_CurrentTarget.IsAlive)
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
            m_TuNhietTimer = Mathf.Max(0f, m_TuNhietTimer - deltaTime);
            m_CamLangTimer = Mathf.Max(0f, m_CamLangTimer - deltaTime);
            m_HutVaDaTimer = Mathf.Max(0f, m_HutVaDaTimer - deltaTime);

            if (m_CurrentTarget == null || !m_CurrentTarget.IsAlive) return;
            if (!CanCastAnySkill) return;

            if (m_HutVaDaTimer <= 0f)
            {
                m_HutVaDaTimer = hutVaDaCooldown;
                TriggerGlobalSkillCooldown();
                CastHutVaDa();
                return;
            }

            if (m_CamLangTimer <= 0f)
            {
                m_CamLangTimer = camLangCooldown;
                TriggerGlobalSkillCooldown();
                CastCamLang();
                return;
            }

            if (m_TuNhietTimer <= 0f)
            {
                m_TuNhietTimer = tuNhietCooldown;
                TriggerGlobalSkillCooldown();
                CastTuNhiet();
            }
        }

        private void CastTuNhiet()
        {
            if (m_CurrentTarget == null || !m_CurrentTarget.IsAlive) return;

            Vector2 targetPosition = m_CurrentTarget.transform.position;
            The2FaceVortexMode mode = Random.value < 0.5f ? The2FaceVortexMode.Sad : The2FaceVortexMode.Anger;
            float damageRatio = mode == The2FaceVortexMode.Sad ? vortexSadDamageRatio : vortexAngerDamageRatio;

            if (vortexPrefab != null)
            {
                GameObject instance = Instantiate(vortexPrefab, targetPosition, Quaternion.identity);
                The2FaceVortexZone zone = instance.GetComponent<The2FaceVortexZone>();
                zone?.Initialize(
                    this,
                    mode,
                    vortexRadius,
                    vortexDuration,
                    damageRatio,
                    sadSlowPercent,
                    sadAttackReduction,
                    sadDefenseReduction,
                    sadDuration,
                    angerEnergyDrainRatio);
                GetComponent<IronWasteland.Net.EnemyNetState>()?.BroadcastVfx(
                    "VortexZone", targetPosition, 0f, default, vortexDuration, (int)mode);
            }

            Debug.Log($"[The2Face] TuNhiet ({mode}): tạo vùng xoáy tại {targetPosition}. Sát thương: {(damageRatio * 100f):0}% ATK/s.");
        }

        public void OnClientNetVfx(string key, Vector2 position, float rotationZ, Vector2 dir, float duration, float value)
        {
            if (key == "VortexZone" && vortexPrefab != null)
            {
                GameObject instance = Instantiate(vortexPrefab, position, Quaternion.identity);
                The2FaceVortexZone zone = instance.GetComponent<The2FaceVortexZone>();
                zone?.Initialize(
                    null,
                    (The2FaceVortexMode)(int)value,
                    vortexRadius,
                    duration > 0f ? duration : vortexDuration,
                    0f, 0f, 0f, 0f, 0f, 0f);
            }
            else if (key == "The2FaceShield")
            {
                SetVisualActive(shieldVisual, value > 0.5f);
            }
        }

        private void CastCamLang()
        {
            TankBrain target = FindRandomTank();
            if (target == null) return;

            target.ApplyCrowdControl(CrowdControlType.Silence, camLangSilenceDuration);
            Debug.Log("[The2Face] CamLang khóa kỹ năng " + target.name + " trong " + camLangSilenceDuration + " giây.");
        }

        private void CastHutVaDa()
        {
            if (m_PullRoutine != null)
            {
                StopCoroutine(m_PullRoutine);
            }

            m_PullRoutine = StartCoroutine(PullTanksRoutine());
            SpawnRocks();
            Debug.Log("[The2Face] HutVaDa: kéo Tank và bắn " + rockCount + " viên đá.");
        }

        private IEnumerator PullTanksRoutine()
        {
            TankBrain[] tanks = FindObjectsByType<TankBrain>();
            Rigidbody2D[] bodies = new Rigidbody2D[tanks.Length];

            for (int i = 0; i < tanks.Length; i++)
            {
                if (tanks[i] == null || !tanks[i].IsAlive) continue;

                bodies[i] = tanks[i].GetComponent<Rigidbody2D>();
                tanks[i].ApplyCrowdControl(CrowdControlType.Root, pullDuration);
            }

            float elapsed = 0f;
            while (elapsed < pullDuration)
            {
                yield return null;
                elapsed += Time.deltaTime;

                Vector2 destination = transform.position;
                for (int i = 0; i < tanks.Length; i++)
                {
                    TankBrain tank = tanks[i];
                    if (tank == null || !tank.IsAlive) continue;

                    Vector2 current = tank.transform.position;
                    Vector2 next = Vector2.MoveTowards(current, destination, pullSpeed * Time.deltaTime);
                    if (IronWasteland.Tanks.TankBrain.NetworkSessionActive)
                    {
                        IronWasteland.Net.NetTeleport.Move(tank, next);
                    }
                    else if (bodies[i] != null)
                    {
                        bodies[i].MovePosition(next);
                    }
                    else
                    {
                        tank.transform.position = next;
                    }
                }
            }

            m_PullRoutine = null;
        }

        private void SpawnRocks()
        {
            if (rockPrefab == null) return;

            for (int i = 0; i < rockCount; i++)
            {
                float angle = Mathf.PI * 2f * i / rockCount + Random.Range(-0.08f, 0.08f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 spawnPosition = (Vector2)transform.position + direction * rockSpawnRadius;

                GameObject instance = Instantiate(rockPrefab, spawnPosition, Quaternion.identity);
                if (IronWasteland.Tanks.TankBrain.NetworkSessionActive &&
                    instance.TryGetComponent(out Unity.Netcode.NetworkObject rockNetObj) && !rockNetObj.IsSpawned)
                {
                    var manager = Unity.Netcode.NetworkManager.Singleton;
                    if (manager != null && manager.IsServer) rockNetObj.Spawn(true);
                }
                The2FaceRock rock = instance.GetComponent<The2FaceRock>();
                rock?.Initialize(this, transform.position, Stats.Attack * rockDamageRatio, rockStunDuration, rockSpeed);
            }
        }

        #endregion

        #region Burst - Light Point Mechanic

        protected override void OnBurstStart()
        {
            m_BurstMechanicActive = true;
            m_PenaltyApplied = false;
            m_UnlockedSlotCount = -1;
            m_ArenaCenter = ResolveArenaCenter();

            StopMovement();
            transform.position = m_ArenaCenter;
            if (m_Rigidbody != null) m_Rigidbody.position = m_ArenaCenter;

            Heal(MaxHealth * burstHealRatio);
            LockAllTankSkills();
            SpawnLightPoints();
            SpawnCrawlers();
            SpawnLightningMinions();
            RefreshUnlockedSkills();

            Debug.Log("[The2Face] Bắt đầu bùng nổ: " + m_TotalLightPoints + " Điểm Sáng, " + m_Crawlers.Count + " quái đầm lầy, " + m_LightningMinions.Count + " tinh linh sấm sét.");
        }

        protected override void OnBurstTick(float deltaTime)
        {
            if (!m_BurstMechanicActive) return;

            RefreshUnlockedSkills();

            int alive = CountAliveLights();
            if (alive <= 0 && m_TotalLightPoints > 0)
            {
                m_BurstMechanicActive = false;
                CompleteBurstFailure();
                return;
            }

            if (BurstTimer <= 0f)
            {
                m_BurstMechanicActive = false;
                if (alive > 0)
                {
                    CompleteBurstSuccess();
                }
                else
                {
                    CompleteBurstFailure();
                }
            }
        }

        protected override void OnBurstSuccess()
        {
            m_BurstMechanicActive = false;
            CleanupBurstObjects();
            ResetSkillLocks();

            if (m_BaseStats != null)
            {
                Stats.Attack = m_BaseStats.Attack * rewardBossWeakenMultiplier;
                Stats.Defense = m_BaseStats.Defense * rewardBossWeakenMultiplier;
            }

            TankBrain[] tanks = FindObjectsByType<TankBrain>();
            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain tank = tanks[i];
                if (tank == null || !tank.IsAlive) continue;

                tank.AddBonusStats(RewardAttackKey, null, new TankStats { Attack = rewardTankAttackBonus });
                tank.ResetAllSkillCooldowns();
            }

            Debug.Log("[The2Face] Phá giải thành công: Boss suy yếu, Tank được cường hóa.");
        }

        protected override void OnBurstFail()
        {
            m_BurstMechanicActive = false;
            CleanupBurstObjects();
            ResetSkillLocks();

            if (m_PenaltyApplied) return;
            m_PenaltyApplied = true;

            TankBrain[] tanks = FindObjectsByType<TankBrain>();
            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain tank = tanks[i];
                if (tank == null || !tank.IsAlive) continue;

                tank.AddBonusStats(PenaltyKey, null, new TankStats
                {
                    Attack = -penaltyAttackReduction,
                    Defense = -penaltyDefenseReduction,
                    MoveSpeed = -penaltySpeedReduction,
                });
            }

            Debug.Log("[The2Face] Phá giải thất bại: toàn bộ Tank bị suy yếu vĩnh viễn.");
        }

        protected override void OnBurstEnded()
        {
            m_BurstMechanicActive = false;
            CleanupBurstObjects();
            RemoveRewardBuffs();
            ResetSkillLocks();
            SetVisualActive(shieldVisual, false);

            // Khôi phục chỉ số gốc của Boss sau khi kết thúc phase Thưởng/Phạt (8s)
            if (m_BaseStats != null && Stats != null)
            {
                Stats.Attack = m_BaseStats.Attack;
                Stats.Defense = m_BaseStats.Defense;
                Stats.MoveSpeed = m_BaseStats.MoveSpeed;
            }
            RevertBurstStatModifiers();

            Debug.Log("[The2Face] Kết thúc giai đoạn bùng nổ, khôi phục chỉ số Boss.");
        }

        private void SpawnLightPoints()
        {
            ClearLightPoints();

            int tankCount = 0;
            var allTanks = FindObjectsByType<TankBrain>();
            for (int i = 0; i < allTanks.Length; i++)
            {
                if (allTanks[i] != null && allTanks[i].IsAlive) tankCount++;
            }
            int count = Mathf.Clamp(tankCount, 1, 4);
            m_TotalLightPoints = count;

            if (lightPointPrefab == null)
            {
                m_TotalLightPoints = 0;
                Debug.LogWarning("[The2Face] Chưa gán Light Point Prefab, không thể tạo Điểm Sáng.");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f * i / count;
                Vector2 position = m_ArenaCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * lightPointSpawnRadius;
                GameObject instance = Instantiate(lightPointPrefab, position, Quaternion.identity);
                if (IronWasteland.Tanks.TankBrain.NetworkSessionActive &&
                    instance.TryGetComponent(out Unity.Netcode.NetworkObject lpNetObj) && !lpNetObj.IsSpawned)
                {
                    var manager = Unity.Netcode.NetworkManager.Singleton;
                    if (manager != null && manager.IsServer) lpNetObj.Spawn(true);
                }
                The2FaceLightPoint point = instance.GetComponent<The2FaceLightPoint>();
                if (point == null)
                {
                    Destroy(instance);
                    continue;
                }

                point.Initialize(this, lightPointHealth, lightPointMoveSpeed, lightPointLitRadius);
                m_LightPoints.Add(point);
            }
        }

        private GameObject SpawnNetworkOrLocalMinion(GameObject prefab, Vector2 position)
        {
            if (prefab == null) return null;

            if (IronWasteland.Tanks.TankBrain.NetworkSessionActive)
            {
                var manager = Unity.Netcode.NetworkManager.Singleton;
                if (manager == null || !manager.IsServer) return null;

                if (prefab.TryGetComponent(out EnemyBrain enemyPrefab) &&
                    IronWasteland.Net.NetSpawnService.Instance != null)
                {
                    EnemyBrain spawned = IronWasteland.Net.NetSpawnService.Instance.SpawnEnemy(
                        enemyPrefab, position, enemyPrefab.Stats);
                    return spawned != null ? spawned.gameObject : null;
                }
            }

            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            if (IronWasteland.Tanks.TankBrain.NetworkSessionActive &&
                instance.TryGetComponent(out Unity.Netcode.NetworkObject netObj) && !netObj.IsSpawned)
            {
                netObj.Spawn(true);
            }
            return instance;
        }

        private void SpawnCrawlers()
        {
            ClearCrawlers();
            if (swampCrawlerPrefab == null) return;

            int count = m_TotalLightPoints * Mathf.Max(0, crawlerCountPerLight);
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f * i / Mathf.Max(1, count);
                Vector2 position = m_ArenaCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * crawlerSpawnRadius;
                GameObject instance = SpawnNetworkOrLocalMinion(swampCrawlerPrefab, position);
                if (instance != null) m_Crawlers.Add(instance);
            }
        }

        private void SpawnLightningMinions()
        {
            ClearLightningMinions();
            if (lightningMinionPrefab == null) return;

            int count = Mathf.Max(0, lightningMinionCount);
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f * i / Mathf.Max(1, count) + Mathf.PI * 0.25f;
                Vector2 position = m_ArenaCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * lightningSpawnRadius;
                GameObject instance = SpawnNetworkOrLocalMinion(lightningMinionPrefab, position);
                if (instance != null) m_LightningMinions.Add(instance);
            }
        }

        private void RefreshUnlockedSkills()
        {
            if (m_LockedTanks.Count == 0) return;

            int alive = CountAliveLights();
            int lit = CountLitLights();
            // Toàn bộ Điểm Sáng ban đầu đều được bật đồng thời
            bool fullOpen = m_TotalLightPoints > 0 && lit == m_TotalLightPoints;
            int unlocked = fullOpen ? TankBrain.SkillCount : Mathf.Clamp(lit, 0, TankBrain.SkillCount);

            if (unlocked == m_UnlockedSlotCount) return;
            m_UnlockedSlotCount = unlocked;

            for (int t = 0; t < m_LockedTanks.Count; t++)
            {
                TankBrain tank = m_LockedTanks[t];
                if (tank == null || !tank.IsAlive) continue;

                for (int slot = 0; slot < TankBrain.SkillCount; slot++)
                {
                    if (slot < unlocked)
                    {
                        if (tank.GetSkillCooldown(slot) >= SkillLockCooldown - 1f)
                        {
                            tank.SetSkillCooldown(slot, 0f);
                        }
                    }
                    else if (tank.GetSkillCooldown(slot) < SkillLockCooldown - 1f)
                    {
                        tank.SetSkillCooldown(slot, SkillLockCooldown);
                    }
                }
            }
        }

        private void LockAllTankSkills()
        {
            m_LockedTanks.Clear();

            TankBrain[] tanks = FindObjectsByType<TankBrain>();
            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain tank = tanks[i];
                if (tank == null || !tank.IsAlive) continue;

                m_LockedTanks.Add(tank);
                for (int slot = 0; slot < TankBrain.SkillCount; slot++)
                {
                    tank.SetSkillCooldown(slot, SkillLockCooldown);
                }
            }
        }

        private void ResetSkillLocks()
        {
            for (int t = 0; t < m_LockedTanks.Count; t++)
            {
                TankBrain tank = m_LockedTanks[t];
                if (tank == null) continue;

                for (int slot = 0; slot < TankBrain.SkillCount; slot++)
                {
                    if (tank.GetSkillCooldown(slot) >= SkillLockCooldown - 1f)
                    {
                        tank.SetSkillCooldown(slot, 0f);
                    }
                }
            }

            m_LockedTanks.Clear();
            m_UnlockedSlotCount = -1;
        }

        private void CleanupBurstObjects()
        {
            ClearLightPoints();
            ClearCrawlers();
            ClearLightningMinions();
        }

        private void ClearLightPoints()
        {
            for (int i = 0; i < m_LightPoints.Count; i++)
            {
                if (m_LightPoints[i] != null)
                {
                    Destroy(m_LightPoints[i].gameObject);
                }
            }

            m_LightPoints.Clear();
            m_TotalLightPoints = 0;
            m_UnlockedSlotCount = -1;
        }

        private void ClearCrawlers()
        {
            for (int i = 0; i < m_Crawlers.Count; i++)
            {
                if (m_Crawlers[i] != null)
                {
                    Destroy(m_Crawlers[i]);
                }
            }

            m_Crawlers.Clear();
        }

        private void ClearLightningMinions()
        {
            for (int i = 0; i < m_LightningMinions.Count; i++)
            {
                if (m_LightningMinions[i] != null)
                {
                    Destroy(m_LightningMinions[i]);
                }
            }

            m_LightningMinions.Clear();
        }

        private void RemoveRewardBuffs()
        {
            TankBrain[] tanks = FindObjectsByType<TankBrain>();
            for (int i = 0; i < tanks.Length; i++)
            {
                if (tanks[i] != null) tanks[i].RemoveBonusStats(RewardAttackKey);
            }
        }

        private int CountAliveLights()
        {
            int count = 0;
            for (int i = 0; i < m_LightPoints.Count; i++)
            {
                if (m_LightPoints[i] != null && m_LightPoints[i].IsAlive) count++;
            }

            return count;
        }

        private int CountLitLights()
        {
            int count = 0;
            for (int i = 0; i < m_LightPoints.Count; i++)
            {
                if (m_LightPoints[i] != null && m_LightPoints[i].IsAlive && m_LightPoints[i].IsLit) count++;
            }

            return count;
        }

        /// <summary>Được Điểm Sáng gọi khi bị phá hủy để cập nhật lại số slot kỹ năng.</summary>
        public void OnLightPointDestroyed(The2FaceLightPoint point)
        {
            if (point != null) m_LightPoints.Remove(point);
            if (m_BurstMechanicActive) RefreshUnlockedSkills();
        }

        private Vector2 ResolveArenaCenter()
        {
            StageManager stageManager = FindAnyObjectByType<StageManager>();
            return stageManager != null ? stageManager.ArenaCenter : arenaCenter;
        }

        #endregion

        #region Visual Helpers

        private static void SetVisualActive(SpriteRenderer renderer, bool active)
        {
            if (renderer == null) return;
            if (renderer.gameObject.activeSelf != active)
            {
                renderer.gameObject.SetActive(active);
            }
        }

        private static void FitVisual(SpriteRenderer renderer, float diameter)
        {
            if (renderer == null || renderer.sprite == null) return;

            float width = Mathf.Max(0.001f, renderer.sprite.bounds.size.x);
            float scale = diameter / width;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        #endregion
    }
}
