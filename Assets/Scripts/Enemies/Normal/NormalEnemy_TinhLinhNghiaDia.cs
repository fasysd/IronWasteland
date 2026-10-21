using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Tinh Linh Nghĩa Địa - Kẻ địch thường di chuyển theo quỹ đạo (Waypoint Patrol).
    /// Trong quá trình di chuyển, quái liên tục tạo ra các vùng hình tròn (DamageZone) trên mặt đất thông qua Object Pooling.
    /// Cấu hình bán kính (Radius) và thời gian tồn tại (Duration) của vùng được thiết lập trực tiếp trên
    /// DamageZone_TinhLinhNghiaDia GameObject nằm trong TinhLinhNghiaDia Prefab.
    /// Tất cả các vùng gây sát thương đồng thời sau mỗi khoảng thời gian damageTickInterval.
    /// Nếu xe tăng đứng trên nhiều vùng cùng lúc, chỉ nhận 1 lần sát thương trong nhịp tick đó.
    /// </summary>
    [DisallowMultipleComponent]
    public class NormalEnemy_TinhLinhNghiaDia : NormalEnemy_BasePatrol
    {
        [Header("Hazard Zone Settings (Vùng Nghĩa Địa)")]
        [Tooltip("Template vùng sát thương (nằm trong Children của TinhLinhNghiaDia Prefab). Cấu hình bán kính và thời gian tồn tại trực tiếp trên component này.")]
        [SerializeField] private DamageZone_TinhLinhNghiaDia zoneTemplate;

        [Tooltip("Khoảng cách di chuyển giữa mỗi lần tạo vùng (mét).")]
        [SerializeField, Min(0.2f)] private float spawnDistanceInterval = 1.0f;

        [Header("Damage Tick Settings")]
        [Tooltip("Chu kỳ gây sát thương đồng loạt lên tất cả xe tăng trong các vùng (giây).")]
        [SerializeField, Min(0.1f)] private float damageTickInterval = 0.5f;

        [Tooltip("Tỷ lệ sát thương dựa trên chỉ số tấn công Stats.Attack ở mỗi nhịp tick (ví dụ: 0.5 = 50% Attack, 1.0 = 100% Attack).")]
        [SerializeField, Min(0.1f)] private float damageMultiplierPerTick = 0.5f;

        [Tooltip("Dọn dẹp toàn bộ các vùng khi quái chết.")]
        [SerializeField] private bool clearZonesOnDeath = true;

        [Header("Runtime Info (Pooling & Stats)")]
        [SerializeField, ReadOnly] private int activeZoneCount;
        [SerializeField, ReadOnly] private int pooledZoneCount;
        [SerializeField, ReadOnly] private float damageTickTimer;

        private Vector2 m_LastSpawnPosition;
        private bool m_HasSpawnedFirstZone;
        private readonly Queue<DamageZone_TinhLinhNghiaDia> m_ZonePool = new Queue<DamageZone_TinhLinhNghiaDia>();
        private readonly List<DamageZone_TinhLinhNghiaDia> m_ActiveZones = new List<DamageZone_TinhLinhNghiaDia>();

        public DamageZone_TinhLinhNghiaDia ZoneTemplate { get => zoneTemplate; set => zoneTemplate = value; }
        public float ZoneDuration => zoneTemplate != null ? zoneTemplate.Lifetime : 4.0f;
        public float SpawnDistanceInterval { get => spawnDistanceInterval; set => spawnDistanceInterval = value; }
        public float DamageTickInterval { get => damageTickInterval; set => damageTickInterval = value; }
        public float DamageMultiplierPerTick { get => damageMultiplierPerTick; set => damageMultiplierPerTick = value; }
        public int ActiveZoneCount => m_ActiveZones.Count;
        public int PooledZoneCount => m_ZonePool.Count;

        protected override void Awake()
        {
            base.Awake();

            if (zoneTemplate == null)
            {
                zoneTemplate = GetComponentInChildren<DamageZone_TinhLinhNghiaDia>(true);
            }

            if (zoneTemplate != null)
            {
                zoneTemplate.gameObject.SetActive(false);
            }
        }

        protected override void Start()
        {
            base.Start();

            m_LastSpawnPosition = transform.position;
            m_HasSpawnedFirstZone = false;
        }

        protected override void OnBrainTick(float deltaTime)
        {
            // 1. Chạy logic tuần tra waypoint cơ sở
            base.OnBrainTick(deltaTime);

            Vector2 currentPos = transform.position;

            // 2. Tạo vùng sát thương khi di chuyển đạt khoảng cách spawnDistanceInterval
            if (!m_HasSpawnedFirstZone || Vector2.Distance(currentPos, m_LastSpawnPosition) >= spawnDistanceInterval)
            {
                SpawnZone(currentPos);
            }

            // 3. Chu kỳ gây sát thương đồng loạt lên tất cả xe tăng trong các vùng
            damageTickTimer += deltaTime;
            if (damageTickTimer >= damageTickInterval)
            {
                damageTickTimer = 0f;
                TickDamage();
            }

            activeZoneCount = m_ActiveZones.Count;
            pooledZoneCount = m_ZonePool.Count;
        }

        public void SpawnZone(Vector2 position)
        {
            if (zoneTemplate == null) return;

            DamageZone_TinhLinhNghiaDia zone = null;

            // Lấy từ Object Pool nếu có
            while (m_ZonePool.Count > 0)
            {
                DamageZone_TinhLinhNghiaDia candidate = m_ZonePool.Dequeue();
                if (candidate != null)
                {
                    zone = candidate;
                    break;
                }
            }

            // Nếu Pool trống, khởi tạo thêm instance mới từ template vào thế giới
            if (zone == null)
            {
                zone = Instantiate(zoneTemplate, null);
            }

            zone.SpawnAt(position, ReturnZoneToPool);
            m_ActiveZones.Add(zone);
            m_LastSpawnPosition = position;
            m_HasSpawnedFirstZone = true;
        }

        private void ReturnZoneToPool(DamageZone_TinhLinhNghiaDia zone)
        {
            if (zone == null) return;

            m_ActiveZones.Remove(zone);
            m_ZonePool.Enqueue(zone);
        }

        private void TickDamage()
        {
            // Dọn dẹp các vùng đã bị hủy hoặc không còn active
            m_ActiveZones.RemoveAll(z => z == null || !z.IsActive);
            if (m_ActiveZones.Count == 0) return;

            // Tổng hợp tất cả các xe tăng từ tất cả các vùng vào HashSet
            // HashSet đảm bảo mỗi xe tăng chỉ được lưu 1 lần duy nhất kể cả đứng trên nhiều vùng giao nhau
            var targetBatch = new HashSet<TankBrain>();
            for (int i = 0; i < m_ActiveZones.Count; i++)
            {
                DamageZone_TinhLinhNghiaDia zone = m_ActiveZones[i];
                if (zone != null && zone.IsActive)
                {
                    zone.CollectTanksInZone(targetBatch);
                }
            }

            if (targetBatch.Count == 0) return;

            // Đồng loạt gây sát thương lên từng mục tiêu tìm được
            float baseAtk = Stats != null ? Stats.Attack : 10f;
            float damage = baseAtk * damageMultiplierPerTick;

            foreach (TankBrain tank in targetBatch)
            {
                if (tank == null || !tank.IsAlive) continue;

                tank.TakeDamage(damage);
                TextDamageManager.Instance?.Show(damage, tank.transform.position);
            }
        }

        protected override void Die()
        {
            if (clearZonesOnDeath)
            {
                for (int i = m_ActiveZones.Count - 1; i >= 0; i--)
                {
                    if (m_ActiveZones[i] != null)
                    {
                        m_ActiveZones[i].Despawn();
                    }
                }
                m_ActiveZones.Clear();
            }

            base.Die();
        }

        protected virtual void OnDestroy()
        {
            while (m_ZonePool.Count > 0)
            {
                DamageZone_TinhLinhNghiaDia pooled = m_ZonePool.Dequeue();
                if (pooled != null && pooled != zoneTemplate)
                {
                    Destroy(pooled.gameObject);
                }
            }

            for (int i = 0; i < m_ActiveZones.Count; i++)
            {
                if (m_ActiveZones[i] != null && m_ActiveZones[i] != zoneTemplate)
                {
                    Destroy(m_ActiveZones[i].gameObject);
                }
            }
            m_ActiveZones.Clear();
        }
    }
}
