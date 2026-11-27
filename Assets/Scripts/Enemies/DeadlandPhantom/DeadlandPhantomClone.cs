using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Net;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.DeadlandPhantom
{
    /// <summary>
    /// Bản sao Bóng Ma Tử Địa trong buồng tối Sub-Arena (DarkZone):
    /// - Kế thừa EnemyBrain, bất tử máu (không thể bị hạ gục bởi đòn đánh thông thường).
    /// - Đồng bộ sát thương khiên: Mọi đòn đánh từ Tank trong buồng tối gây lên Bản sao sẽ được trừ thẳng
    ///   vào Khiên Tứ Hướng của Boss ngoài sân chính (hoàn toàn bỏ qua góc mở khiên).
    /// - Định kỳ soulSummonInterval giây tự động kích hoạt CastSoulSummon:
    ///   + Quét làm chậm Tank trong DarkZone bằng slowRatio trong slowDuration.
    ///   + Bắn ra soulProjectileCount đạn linh hồn DeadlandSoulProjectile tự tìm mục tiêu gây projectileDamageRatio * ATK.
    /// - Khi thi triển kỹ năng: tự động xoay mặt về hướng Tank mục tiêu; nếu không có mục tiêu thì đặt góc quay về 0.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeadlandPhantomClone : EnemyBrain, IronWasteland.Net.INetVfxReceiver
    {
        [Header("Clone Abilities & Multipliers")]
        [Tooltip("Chu kỳ thi triển Soul Summon trong buồng tối (giây). GDD: 3s.")]
        [SerializeField, Min(1f)] private float soulSummonInterval = 3f;

        [Tooltip("Số đạn linh hồn bắn ra mỗi lần thi triển.")]
        [SerializeField, Range(1, 20)] private int soulProjectileCount = 5;

        [Tooltip("Tỷ lệ sát thương của mỗi viên đạn theo ATK của Boss (0.5 = 50% ATK).")]
        [SerializeField, Range(0.05f, 2f)] private float projectileDamageRatio = 0.5f;

        [Tooltip("Tốc độ bay của đạn linh hồn (m/s).")]
        [SerializeField, Min(1f)] private float projectileSpeed = 7f;

        [Tooltip("Góc tỏa hình quạt khi bắn đạn (độ).")]
        [SerializeField, Range(10f, 180f)] private float spreadArc = 60f;

        [Tooltip("Tỷ lệ làm chậm Tank (0.2 = 20%).")]
        [SerializeField, Range(0f, 1f)] private float slowRatio = 0.2f;

        [Tooltip("Thời gian làm chậm Tank (giây). GDD: 5s.")]
        [SerializeField, Min(0.1f)] private float slowDuration = 5f;

        [Header("Prefabs & References")]
        [Tooltip("Prefab đạn linh hồn ma hỏa (VFX_DeadlandSoulProjectile).")]
        [SerializeField] private GameObject soulProjectilePrefab;

        [Tooltip("Prefab sóng xung kích (VFX_SoulSummonPulse).")]
        [SerializeField] private GameObject soulSummonPulsePrefab;

        [Header("Rotation Settings")]
        [Tooltip("Góc bù độ lệch ban đầu của Sprite/Model (độ).")]
        [SerializeField] private float rotationOffset = 0f;

        [Tooltip("Transform mục tiêu xoay. Tự tìm Model nếu để trống.")]
        [SerializeField] private Transform rotationPivot;

        private DeadlandPhantomBrain m_Boss;
        private DarkZoneSubArena m_Arena;
        private BongMaTuDiaVFXController m_VfxController;
        private Animator m_Animator;
        private float m_SoulSummonTimer;

        public DeadlandPhantomBrain Boss => m_Boss;
        public DarkZoneSubArena Arena => m_Arena;
        public float SoulSummonTimer => m_SoulSummonTimer;
        public float SoulSummonInterval => soulSummonInterval;
        public int SoulProjectileCount => soulProjectileCount;
        public float SlowRatio => slowRatio;
        public float SlowDuration => slowDuration;

        protected override void Awake()
        {
            base.Awake();
            m_VfxController = GetComponent<BongMaTuDiaVFXController>();
            m_Animator = GetComponentInChildren<Animator>();
        }

        protected override void OnDeath()
        {
            // Bản sao không tự chết theo cách thông thường, chỉ bị hủy khi buồng tối đóng
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        public GameObject SoulProjectilePrefab { get => soulProjectilePrefab; set => soulProjectilePrefab = value; }
        public GameObject SoulSummonPulsePrefab { get => soulSummonPulsePrefab; set => soulSummonPulsePrefab = value; }

        public void InitializeClone(DeadlandPhantomBrain boss, DarkZoneSubArena arena)
        {
            m_Boss = boss;
            m_Arena = arena;

            EnemyStats cloneStats = new EnemyStats
            {
                MaxHealth = 999999f,
                Attack = (boss != null && boss.Stats != null) ? boss.Stats.Attack : 120f,
                Defense = (boss != null && boss.Stats != null) ? boss.Stats.Defense : 45f,
                MoveSpeed = 0f
            };

            Initialize(cloneStats);
            m_SoulSummonTimer = soulSummonInterval;

            if (m_VfxController == null) m_VfxController = GetComponent<BongMaTuDiaVFXController>();
            if (m_Boss != null && m_Boss.IsCardinalShieldActive)
            {
                m_VfxController?.ActivateCardinalShield(m_Boss.CurrentShieldDirection);
                GetComponent<IronWasteland.Net.EnemyNetState>()?.BroadcastVfx(
                    "CardinalShieldActivate", transform.position, value: (int)m_Boss.CurrentShieldDirection);
            }
        }

        public void SyncShieldDirection(CardinalDirection dir)
        {
            if (m_VfxController == null) m_VfxController = GetComponent<BongMaTuDiaVFXController>();
            m_VfxController?.SetCardinalShieldDirection(dir);
            GetComponent<IronWasteland.Net.EnemyNetState>()?.BroadcastVfx(
                "CardinalShieldDir", transform.position, value: (int)dir);
        }

        public void DeactivateShield()
        {
            if (m_VfxController == null) m_VfxController = GetComponent<BongMaTuDiaVFXController>();
            m_VfxController?.DeactivateCardinalShield();
            GetComponent<IronWasteland.Net.EnemyNetState>()?.BroadcastVfx(
                "CardinalShieldDeactivate", transform.position);
        }

        public void OnClientNetVfx(string key, Vector2 position, float rotationZ, Vector2 dir, float duration, float value)
        {
            if (m_VfxController == null) m_VfxController = GetComponent<BongMaTuDiaVFXController>();
            if (m_VfxController == null) return;

            switch (key)
            {
                case "CardinalShieldActivate":
                    m_VfxController.ActivateCardinalShield((CardinalDirection)(int)value);
                    break;
                case "CardinalShieldDir":
                    m_VfxController.SetCardinalShieldDirection((CardinalDirection)(int)value);
                    break;
                case "CardinalShieldDeactivate":
                    m_VfxController.DeactivateCardinalShield();
                    break;
                case "SoulSummonPulse":
                    m_VfxController.PlaySoulSummonPulse(position);
                    break;
                case "SoulProjectile":
                    m_VfxController.SpawnSoulProjectile(position, null, dir, speed: duration > 0f ? duration : projectileSpeed, damage: 0f, owner: null);
                    break;
            }
        }

        protected override void OnBrainTick(float deltaTime)
        {
            m_SoulSummonTimer += deltaTime;
            if (m_SoulSummonTimer >= soulSummonInterval)
            {
                m_SoulSummonTimer = 0f;
                CastSoulSummon();
            }
        }

        /// <summary>
        /// Xử lý sát thương từ Tank trong buồng tối:
        /// - Bất tử máu.
        /// - Đồng bộ cơ chế Khiên Tứ Hướng với Boss:
        ///   + Đòn đánh vào hướng đóng: Bị cản, phản đòn (PlayCardinalReflect) và hồi 5% khiên cho Boss ngoài sân.
        ///   + Đòn đánh vào hướng mở: Trừ thẳng sát thương vào Khiên Tứ Hướng dùng chung của Boss.
        ///   + Khi khiên Boss vỡ: Khiên của bản sao cũng lập tức tắt đồng bộ.
        /// </summary>
        public override void TakeDamage(DamageInfo info)
        {
            if (info.RawDamage <= 0f) return;

            if (m_Boss != null && m_Boss.IsCardinalShieldActive)
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
                Vector2 relativeDir = (attackerPos - (Vector2)transform.position).normalized;
                CardinalDirection incomingQuad = CardinalShieldVFX.GetDirectionFromVector(relativeDir);
                bool isOpen = incomingQuad == m_Boss.CurrentShieldDirection;

                if (!isOpen)
                {
                    // Chặn đòn: hồi 5% khiên cho Boss và phản đòn
                    m_Boss.HealCardinalShield(m_Boss.MaxCardinalShield * 0.05f);
                    if (m_VfxController == null) m_VfxController = GetComponent<BongMaTuDiaVFXController>();
                    m_VfxController?.PlayCardinalReflect(hitPoint);
                    info.FinalDamage = 0f;
                    return;
                }
                else
                {
                    // Trúng hướng mở: trừ khiên dùng chung
                    float finalDamage = CombatUtility.ResolveDamage(info.RawDamage, Stats.Defense, info.ArmorPenetration);
                    info.FinalDamage = finalDamage;

                    Model?.PlayHit();
                    if (finalDamage > 0.01f)
                    {
                        CombatFeedback.Show(finalDamage, hitPoint, TextValueType.ShieldAbsorb);
                    }

                    m_Boss.DamageCardinalShield(finalDamage);
                    if (!m_Boss.IsCardinalShieldActive)
                    {
                        DeactivateShield();
                    }
                    return;
                }
            }

            Model?.PlayHit();
        }

        /// <summary>
        /// Kích hoạt kỹ năng Triệu Hồi Linh Hồn trong buồng tối:
        /// Làm chậm Tank và bắn đạn linh hồn.
        /// Đồng thời xoay mặt về hướng Tank mục tiêu (hoặc về 0 nếu không có mục tiêu).
        /// </summary>
        public void CastSoulSummon()
        {
            TankBrain targetTank = (m_Arena != null && m_Arena.TrappedTank != null)
                ? m_Arena.TrappedTank
                : FindAnyObjectByType<TankBrain>();

            if (targetTank != null && targetTank.IsAlive)
            {
                RotateTowardsTarget(targetTank.transform.position);
            }
            else
            {
                RotateToZero();
                return;
            }

            var netState = GetComponent<IronWasteland.Net.EnemyNetState>();
            if (m_Animator == null) m_Animator = GetComponentInChildren<Animator>();
            if (m_Animator != null)
            {
                NetEnemyAnimation.Trigger(gameObject, m_Animator, "SoulSummon");
            }

            if (m_VfxController == null) m_VfxController = GetComponent<BongMaTuDiaVFXController>();

            // 1. Chạy VFX xung kích nếu có
            if (m_VfxController != null)
            {
                m_VfxController.PlaySoulSummonPulse(transform.position);
            }
            else if (soulSummonPulsePrefab != null)
            {
                Instantiate(soulSummonPulsePrefab, transform.position, Quaternion.identity);
            }
            netState?.BroadcastVfx("SoulSummonPulse", transform.position);

            // 2. Làm chậm Tank
            TankSlowDebuff.Apply(targetTank, slowRatio, slowDuration);

            // 3. Bắn đạn linh hồn gây projectileDamageRatio * ATK
            float bossAtk = (m_Boss != null && m_Boss.Stats != null) ? m_Boss.Stats.Attack : 120f;
            float projDamage = bossAtk * projectileDamageRatio;

            Vector2 toTarget = ((Vector2)targetTank.transform.position - (Vector2)transform.position).normalized;
            if (toTarget.sqrMagnitude < 0.001f) toTarget = Vector2.up;

            float baseAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            float angleStep = soulProjectileCount > 1 ? spreadArc / (soulProjectileCount - 1) : 0f;
            float startAngle = baseAngle - spreadArc * 0.5f;

            for (int i = 0; i < soulProjectileCount; i++)
            {
                float currentAngle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle));

                if (m_VfxController != null)
                {
                    m_VfxController.SpawnSoulProjectile(transform.position, targetTank.transform, dir, speed: projectileSpeed, damage: projDamage, owner: this);
                }
                else if (soulProjectilePrefab != null)
                {
                    GameObject go = Instantiate(soulProjectilePrefab, transform.position, Quaternion.identity);
                    DeadlandSoulProjectile proj = go.GetComponent<DeadlandSoulProjectile>();
                    if (proj != null)
                    {
                        proj.Initialize(targetTank.transform, dir, speed: projectileSpeed, damage: projDamage, owner: this);
                    }
                }
                else
                {
                    Debug.LogWarning($"[DeadlandPhantomClone] soulProjectilePrefab is unassigned and BongMaTuDiaVFXController is missing on '{gameObject.name}'. Cannot spawn soul projectile.");
                }
                netState?.BroadcastVfx("SoulProjectile", transform.position, 0f, dir, projectileSpeed, 0f);
            }
        }

        #region Rotation

        public void RotateTowardsTarget(Vector2 targetPos)
        {
            Vector2 dir = targetPos - (Vector2)transform.position;
            RotateTowardsDirection(dir);
        }

        public void RotateTowardsDirection(Vector2 direction)
        {
            Transform pivot = rotationPivot != null ? rotationPivot : (Model != null ? Model.transform : transform);
            if (pivot == null) return;

            if (direction.sqrMagnitude < 0.0001f)
            {
                RotateToZero();
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;
            pivot.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void RotateToZero()
        {
            Transform pivot = rotationPivot != null ? rotationPivot : (Model != null ? Model.transform : transform);
            if (pivot == null) return;

            pivot.rotation = Quaternion.Euler(0f, 0f, 0f);
        }

        #endregion

        public void ManualTriggerSoulSummon()
        {
            CastSoulSummon();
        }

        public void ManualTick(float deltaTime)
        {
            OnBrainTick(deltaTime);
        }
    }
}
