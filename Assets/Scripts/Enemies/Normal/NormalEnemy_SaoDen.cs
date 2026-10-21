using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Sao Đen - Kẻ địch thường LUÔN ĐỨNG YÊN (kế thừa NormalEnemy_BaseStationary).
    /// Kỹ năng đặc trưng:
    /// - Hồi máu cho các Enemy trong phạm vi xung quanh bằng 100% tấn công (Stats.Attack).
    /// - Sử dụng HealZone_SaoDen để quét mục tiêu thông qua 100% ColliderOwnerRef.
    /// - Kích thước vùng hồi máu được điều chỉnh trực quan bằng Transform.localScale trên Prefab.
    /// - EnemyBrain trực tiếp kích hoạt logic hồi máu (EnemyBrain.Heal) khi nhận được sự kiện từ Zone.
    /// </summary>
    [DisallowMultipleComponent]
    public class NormalEnemy_SaoDen : NormalEnemy_BaseStationary
    {
        [Header("Heal Skill Settings")]
        [Tooltip("Template vùng hồi máu (nằm trong Children của SaoDen Prefab). Cấu hình kích thước qua Transform Scale của GameObject.")]
        [SerializeField] private HealZone_SaoDen zoneTemplate;

        [Tooltip("Thời gian hồi kỹ năng giữa các lần thi triển hồi máu (giây).")]
        [SerializeField, Min(1f)] private float skillCooldown = 5.0f;

        [Tooltip("Tỷ lệ hồi máu dựa trên chỉ số Stats.Attack (1.0 = 100% Attack).")]
        [SerializeField, Min(0.1f)] private float healMultiplier = 1.0f;

        [Tooltip("Có hồi máu cho chính bản thân Sao Đen hay không.")]
        [SerializeField] private bool includeSelf = true;

        [Header("Runtime Info")]
        [SerializeField, ReadOnly] private float currentCooldownTimer;
        [SerializeField, ReadOnly] private bool isCasting;
        [SerializeField, ReadOnly] private int activeZoneCount;
        [SerializeField, ReadOnly] private int pooledZoneCount;

        private Coroutine m_CastCoroutine;
        private readonly Queue<HealZone_SaoDen> m_ZonePool = new Queue<HealZone_SaoDen>();
        private readonly List<HealZone_SaoDen> m_ActiveZones = new List<HealZone_SaoDen>();

        public HealZone_SaoDen ZoneTemplate { get => zoneTemplate; set => zoneTemplate = value; }
        public float SkillCooldown { get => skillCooldown; set => skillCooldown = value; }
        public float HealMultiplier { get => healMultiplier; set => healMultiplier = value; }
        public bool IncludeSelf { get => includeSelf; set => includeSelf = value; }
        public float CurrentCooldownTimer => currentCooldownTimer;
        public bool IsCasting => isCasting;
        public int ActiveZoneCount => m_ActiveZones.Count;
        public int PooledZoneCount => m_ZonePool.Count;

        protected override void Awake()
        {
            base.Awake();

            if (zoneTemplate == null)
            {
                zoneTemplate = GetComponentInChildren<HealZone_SaoDen>(true);
            }

            if (zoneTemplate != null)
            {
                zoneTemplate.gameObject.SetActive(false);
            }
        }

        protected override void OnBrainTick(float deltaTime)
        {
            base.OnBrainTick(deltaTime);

            if (currentCooldownTimer > 0f)
            {
                currentCooldownTimer = Mathf.Max(0f, currentCooldownTimer - deltaTime);
            }

            // Tự động thi triển chiêu hồi máu định kỳ mỗi khi hết hồi chiêu
            if (!isCasting && currentCooldownTimer <= 0f)
            {
                StartCastSkill();
            }

            activeZoneCount = m_ActiveZones.Count;
            pooledZoneCount = m_ZonePool.Count;
        }

        public void StartCastSkill()
        {
            if (isCasting) return;

            if (m_CastCoroutine != null)
            {
                StopCoroutine(m_CastCoroutine);
            }
            m_CastCoroutine = StartCoroutine(SkillCastRoutine());
        }

        private IEnumerator SkillCastRoutine()
        {
            isCasting = true;

            // 1. Thi triển vùng hồi máu xung quanh bản thân Sao Đen
            HealZone_SaoDen activeZone = SpawnHealZone(transform.position);

            // 2. Chờ vùng hồi máu hoàn thành thời gian tồn tại và biến mất
            if (activeZone != null)
            {
                while (activeZone != null && activeZone.IsActive)
                {
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(2.0f);
            }

            // 3. Kết thúc thi triển và bắt đầu hồi chiêu
            currentCooldownTimer = skillCooldown;
            isCasting = false;
            m_CastCoroutine = null;
        }

        private HealZone_SaoDen SpawnHealZone(Vector2 position)
        {
            if (zoneTemplate == null) return null;

            HealZone_SaoDen zone = null;

            // Lấy từ Object Pool nếu có
            while (m_ZonePool.Count > 0)
            {
                HealZone_SaoDen candidate = m_ZonePool.Dequeue();
                if (candidate != null)
                {
                    zone = candidate;
                    break;
                }
            }

            // Nếu Pool trống, khởi tạo instance mới từ template
            if (zone == null)
            {
                zone = Instantiate(zoneTemplate, null);
                // Giữ nguyên localScale cấu hình từ template
                zone.transform.localScale = zoneTemplate.transform.localScale;
                zone.OnTargetsDetected += HandleZoneTargetsDetected;
            }

            zone.SpawnAt(position, ReturnZoneToPool);
            m_ActiveZones.Add(zone);
            return zone;
        }

        private void ReturnZoneToPool(HealZone_SaoDen zone)
        {
            if (zone == null) return;

            m_ActiveZones.Remove(zone);
            m_ZonePool.Enqueue(zone);
        }

        /// <summary>
        /// Xử lý hồi máu khi vùng phát hiện các Enemy trong phạm vi.
        /// EnemyBrain chịu trách nhiệm tính toán và kích hoạt hồi máu (100% Attack).
        /// </summary>
        private void HandleZoneTargetsDetected(HealZone_SaoDen zone, HashSet<EnemyBrain> targets)
        {
            if (targets == null || targets.Count == 0) return;

            float baseAtk = Stats != null ? Stats.Attack : 10f;
            float healAmount = baseAtk * healMultiplier;

            foreach (EnemyBrain enemy in targets)
            {
                if (enemy == null || !enemy.IsAlive) continue;

                // Nếu không cho phép hồi máu cho chính mình
                if (!includeSelf && enemy == this) continue;

                enemy.Heal(healAmount);
                TextDamageManager.Instance?.Show(healAmount, enemy.transform.position);
            }
        }

        #region Debug / Context Menu

        [ContextMenu("Test/Cast Heal Skill")]
        public void TestCastHealSkill()
        {
            StartCastSkill();
        }

        #endregion

        protected override void OnDeath()
        {
            if (m_CastCoroutine != null)
            {
                StopCoroutine(m_CastCoroutine);
                m_CastCoroutine = null;
            }

            base.OnDeath();
        }

        protected virtual void OnDestroy()
        {
            while (m_ZonePool.Count > 0)
            {
                HealZone_SaoDen pooled = m_ZonePool.Dequeue();
                if (pooled != null && pooled != zoneTemplate)
                {
                    pooled.OnTargetsDetected -= HandleZoneTargetsDetected;
                    Destroy(pooled.gameObject);
                }
            }

            for (int i = 0; i < m_ActiveZones.Count; i++)
            {
                if (m_ActiveZones[i] != null && m_ActiveZones[i] != zoneTemplate)
                {
                    m_ActiveZones[i].OnTargetsDetected -= HandleZoneTargetsDetected;
                    Destroy(m_ActiveZones[i].gameObject);
                }
            }
            m_ActiveZones.Clear();
        }
    }
}
