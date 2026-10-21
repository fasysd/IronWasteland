using System.Collections;
using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Mặt Trời Đỏ - Kẻ địch thường LUÔN ĐỨNG YÊN (kế thừa NormalEnemy_BaseStationary).
    /// Kỹ năng đặc trưng:
    /// - Khi phát hiện Tank mục tiêu trong bán kính C và hết hồi chiêu: Triệu hồi vùng nổ tại vị trí Xe Tăng mục tiêu.
    /// - Vùng nổ đếm ngược trong 2.0s kèm hiệu ứng nhấp nháy cảnh báo.
    /// - Khi phát nổ:
    ///   1. Gây sát thương bằng 100% tấn công của Mặt Trời Đỏ lên các Tank trong vùng.
    ///   2. Sinh ra ngẫu nhiên từ 2 đến 5 con Ký Sinh Thiên Thạch phân tán trong phạm vi vụ nổ.
    /// - Ký Sinh Thiên Thạch được tạo từ template con lưu riêng trong MatTroiDo Prefab (không dùng Prefab Asset).
    /// - Ký Sinh Thiên Thạch tự nổ gây sát thương dựa trên 100% tấn công của chính nó, độc lập với Mặt Trời Đỏ.
    /// </summary>
    [DisallowMultipleComponent]
    public class NormalEnemy_MatTroiDo : NormalEnemy_BaseStationary
    {
        [Header("Skill & Zone Settings")]
        [Tooltip("Template vùng nổ (nằm trong Children của MatTroiDo Prefab). Kích thước điều chỉnh qua Transform.localScale.")]
        [SerializeField] private ExplosionZone_MatTroiDo zoneTemplate;

        [Tooltip("Template Ký Sinh Thiên Thạch riêng (nằm trong Children của MatTroiDo Prefab, KHÔNG dùng Prefab Asset ngoài).")]
        [SerializeField] private NormalEnemy_KySinhThienThach kySinhThienThachTemplate;

        [Tooltip("Thời gian hồi kỹ năng giữa các lần triệu hồi vùng nổ (giây).")]
        [SerializeField, Min(1f)] private float skillCooldown = 6.0f;

        [Tooltip("Tỷ lệ sát thương vụ nổ theo Stats.Attack của Mặt Trời Đỏ (1.0 = 100% Attack).")]
        [SerializeField, Min(0.1f)] private float explosionDamageMultiplier = 1.0f;

        [Header("Ky Sinh Thien Thach Spawn Settings")]
        [Tooltip("Số lượng Ký Sinh Thiên Thạch tối thiểu được sinh ra khi nổ.")]
        [SerializeField, Range(1, 10)] private int minSpawnCount = 2;

        [Tooltip("Số lượng Ký Sinh Thiên Thạch tối đa được sinh ra khi nổ.")]
        [SerializeField, Range(1, 10)] private int maxSpawnCount = 5;

        [Tooltip("Tỷ lệ bán kính vùng nổ dùng để phân tán vị trí xuất hiện của KSTT.")]
        [SerializeField, Range(0.1f, 1f)] private float spawnRadiusRatio = 0.7f;

        [Header("Runtime Info")]
        [SerializeField, ReadOnly] private float currentCooldownTimer;
        [SerializeField, ReadOnly] private bool isCasting;
        [SerializeField, ReadOnly] private int activeZoneCount;
        [SerializeField, ReadOnly] private int pooledZoneCount;

        private Coroutine m_CastCoroutine;
        private readonly Queue<ExplosionZone_MatTroiDo> m_ZonePool = new Queue<ExplosionZone_MatTroiDo>();
        private readonly List<ExplosionZone_MatTroiDo> m_ActiveZones = new List<ExplosionZone_MatTroiDo>();

        public ExplosionZone_MatTroiDo ZoneTemplate { get => zoneTemplate; set => zoneTemplate = value; }
        public NormalEnemy_KySinhThienThach KySinhThienThachTemplate { get => kySinhThienThachTemplate; set => kySinhThienThachTemplate = value; }
        public float SkillCooldown { get => skillCooldown; set => skillCooldown = value; }
        public float ExplosionDamageMultiplier { get => explosionDamageMultiplier; set => explosionDamageMultiplier = value; }
        public int MinSpawnCount { get => minSpawnCount; set => minSpawnCount = value; }
        public int MaxSpawnCount { get => maxSpawnCount; set => maxSpawnCount = value; }
        public float SpawnRadiusRatio { get => spawnRadiusRatio; set => spawnRadiusRatio = value; }
        public float CurrentCooldownTimer => currentCooldownTimer;
        public bool IsCasting => isCasting;
        public int ActiveZoneCount => m_ActiveZones.Count;
        public int PooledZoneCount => m_ZonePool.Count;

        protected override void Awake()
        {
            base.Awake();

            if (zoneTemplate == null)
            {
                zoneTemplate = GetComponentInChildren<ExplosionZone_MatTroiDo>(true);
            }

            if (zoneTemplate != null)
            {
                zoneTemplate.gameObject.SetActive(false);
            }

            if (kySinhThienThachTemplate == null)
            {
                kySinhThienThachTemplate = GetComponentInChildren<NormalEnemy_KySinhThienThach>(true);
            }

            if (kySinhThienThachTemplate != null)
            {
                kySinhThienThachTemplate.gameObject.SetActive(false);
            }
        }

        protected override void OnBrainTick(float deltaTime)
        {
            base.OnBrainTick(deltaTime);

            if (currentCooldownTimer > 0f)
            {
                currentCooldownTimer = Mathf.Max(0f, currentCooldownTimer - deltaTime);
            }

            // Khi phát hiện mục tiêu và hết hồi chiêu: Triệu hồi vùng nổ tại vị trí Xe Tăng
            if (!isCasting && currentCooldownTimer <= 0f && currentTarget != null)
            {
                StartCastSkill(currentTarget.position);
            }

            activeZoneCount = m_ActiveZones.Count;
            pooledZoneCount = m_ZonePool.Count;
        }

        public void StartCastSkill(Vector2 targetPosition)
        {
            if (isCasting) return;

            if (m_CastCoroutine != null)
            {
                StopCoroutine(m_CastCoroutine);
            }
            m_CastCoroutine = StartCoroutine(SkillCastRoutine(targetPosition));
        }

        private IEnumerator SkillCastRoutine(Vector2 targetPosition)
        {
            isCasting = true;

            // 1. Sinh vùng nổ tại vị trí mục tiêu
            ExplosionZone_MatTroiDo activeZone = SpawnExplosionZone(targetPosition);

            // 2. Chờ vùng nổ hoàn thành đếm ngược và kích nổ
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

            // 3. Kết thúc thi triển và bắt đầu tính thời gian hồi chiêu
            currentCooldownTimer = skillCooldown;
            isCasting = false;
            m_CastCoroutine = null;
        }

        private ExplosionZone_MatTroiDo SpawnExplosionZone(Vector2 position)
        {
            if (zoneTemplate == null) return null;

            ExplosionZone_MatTroiDo zone = null;

            // Lấy từ Object Pool nếu có
            while (m_ZonePool.Count > 0)
            {
                ExplosionZone_MatTroiDo candidate = m_ZonePool.Dequeue();
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
                zone.OnExploded += HandleZoneExploded;
            }

            zone.SpawnAt(position, ReturnZoneToPool);
            m_ActiveZones.Add(zone);
            return zone;
        }

        private void ReturnZoneToPool(ExplosionZone_MatTroiDo zone)
        {
            if (zone == null) return;

            m_ActiveZones.Remove(zone);
            m_ZonePool.Enqueue(zone);
        }

        /// <summary>
        /// Xử lý sự kiện khi vùng nổ phát nổ:
        /// 1. Gây sát thương 100% tấn công của Mặt Trời Đỏ lên các Tank trong vùng nổ.
        /// 2. Sinh ngẫu nhiên từ 2-5 con Ký Sinh Thiên Thạch từ template riêng lưu trong Prefab.
        /// </summary>
        private void HandleZoneExploded(ExplosionZone_MatTroiDo zone, HashSet<TankBrain> targets, Vector2 explosionPos, float radius)
        {
            // 1. Gây sát thương 100% tấn công của Mặt Trời Đỏ
            if (targets != null && targets.Count > 0)
            {
                float baseAtk = Stats != null ? Stats.Attack : 10f;
                float damage = baseAtk * explosionDamageMultiplier;

                foreach (TankBrain tank in targets)
                {
                    if (tank == null || !tank.IsAlive) continue;

                    tank.TakeDamage(damage);
                    TextDamageManager.Instance?.Show(damage, tank.transform.position);
                }
            }

            // 2. Tạo ngẫu nhiên 2 - 5 con Ký Sinh Thiên Thạch phân tán trong phạm vi vụ nổ
            SpawnKySinhThienThach(explosionPos, radius);
        }

        private void SpawnKySinhThienThach(Vector2 explosionPos, float explosionRadius)
        {
            if (kySinhThienThachTemplate == null)
            {
                Debug.LogWarning($"[{name}] Không tìm thấy kySinhThienThachTemplate để triệu hồi Ký Sinh Thiên Thạch!");
                return;
            }

            int spawnCount = Random.Range(minSpawnCount, maxSpawnCount + 1);
            float maxOffset = explosionRadius * spawnRadiusRatio;

            for (int i = 0; i < spawnCount; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * maxOffset;
                Vector2 spawnPos = explosionPos + randomOffset;

                // Instantiate từ template con trong MatTroiDo.prefab (KHÔNG dùng Prefab Asset)
                NormalEnemy_KySinhThienThach ksttInstance = Instantiate(kySinhThienThachTemplate, spawnPos, Quaternion.identity, null);
                if (ksttInstance != null)
                {
                    ksttInstance.gameObject.SetActive(true);
                    ksttInstance.AnchorPointD = spawnPos;
                }
            }
        }

        #region Debug / Context Menu

        [ContextMenu("Test/Cast Skill At Target")]
        public void TestCastSkill()
        {
            Vector2 targetPos = currentTarget != null ? (Vector2)currentTarget.position : (Vector2)transform.position + Vector2.right * 3f;
            StartCastSkill(targetPos);
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
                ExplosionZone_MatTroiDo pooled = m_ZonePool.Dequeue();
                if (pooled != null && pooled != zoneTemplate)
                {
                    pooled.OnExploded -= HandleZoneExploded;
                    Destroy(pooled.gameObject);
                }
            }

            for (int i = 0; i < m_ActiveZones.Count; i++)
            {
                if (m_ActiveZones[i] != null && m_ActiveZones[i] != zoneTemplate)
                {
                    m_ActiveZones[i].OnExploded -= HandleZoneExploded;
                    Destroy(m_ActiveZones[i].gameObject);
                }
            }
            m_ActiveZones.Clear();
        }
    }
}
