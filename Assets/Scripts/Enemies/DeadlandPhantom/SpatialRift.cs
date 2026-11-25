using System;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.DeadlandPhantom
{
    /// <summary>
    /// Thực thể Vết Nứt Không Gian (SpatialRift):
    /// - Quản lý vòng đời vết nứt hư không (tồn tại tối đa lifetime giây, mặc định 14s).
    /// - Định kỳ spawnInterval giây (mặc định 8s) tự động triệu hồi 1 quái thường ngẫu nhiên từ pool quái Chương 1.
    /// - Khi Tank chạm vào Trigger Collider:
    ///   + Dịch chuyển tức thời Tank tới vị trí của Boss Bóng Ma Tử Địa.
    ///   + Gây sát thương bằng damageRatio (mặc định 100% ATK) của Boss lên Tank.
    ///   + Kích hoạt hiệu ứng nổ xoáy và tự hủy vết nứt.
    /// - Bán kính tương tác được đồng bộ trực tiếp với CircleCollider2D trên Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpatialRift : MonoBehaviour
    {
        [Header("Rift Lifespan & Spawning")]
        [Tooltip("Thời gian tồn tại tối đa trước khi tự biến mất (giây). GDD: 14s.")]
        [SerializeField, Min(1f)] private float lifetime = 14f;

        [Tooltip("Chu kỳ triệu hồi quái thường Chương 1 (giây). GDD: 8s.")]
        [SerializeField, Min(1f)] private float spawnInterval = 8f;

        [Tooltip("Tỷ lệ sát thương gây lên Tank khi chạm vết nứt (1.0 = 100% ATK Boss).")]
        [SerializeField, Min(0f)] private float damageRatio = 1.0f;

        [Tooltip("Bán kính tương tác của vết nứt (m). Nếu <= 0 sẽ tự lấy từ CircleCollider2D.")]
        [SerializeField] private float riftRadius = 1.8f;

        [Header("Chapter 1 Minion Pool (Ải 1-4)")]
        [SerializeField] private GameObject[] chapter1MinionPrefabs;

        private EnemyBrain m_Boss;
        private CircleCollider2D m_Collider;
        private SpatialRiftVFX m_Vfx;
        private float m_Age;
        private float m_SpawnTimer;
        private bool m_Triggered;
        private bool m_IsDestroyed;

        public event Action<SpatialRift> OnRiftDestroyed;

        public float Lifetime => lifetime;
        public float SpawnInterval => spawnInterval;
        public float RiftRadius => m_Collider != null ? m_Collider.radius : riftRadius;
        public float Age => m_Age;
        public float SpawnTimer => m_SpawnTimer;
        public bool IsTriggered => m_Triggered;
        public EnemyBrain Boss => m_Boss;

        private void Awake()
        {
            m_Collider = GetComponent<CircleCollider2D>();
            m_Vfx = GetComponent<SpatialRiftVFX>() ?? GetComponentInChildren<SpatialRiftVFX>();

            if (m_Collider == null)
            {
                Debug.LogWarning($"[SpatialRift] CircleCollider2D is not found on '{gameObject.name}'. Contact absorption via physical collider is disabled.");
            }
            else
            {
                SyncColliderRadius();
            }

            if (m_Vfx != null)
            {
                m_Vfx.TankEntered += HandleVfxTankEntered;
            }
        }

        private void OnDestroy()
        {

            if (m_Vfx != null)
            {
                m_Vfx.TankEntered -= HandleVfxTankEntered;
            }

            if (!m_IsDestroyed)
            {
                m_IsDestroyed = true;
                OnRiftDestroyed?.Invoke(this);
            }
        }

        private void SyncColliderRadius()
        {
            if (m_Collider == null) m_Collider = GetComponent<CircleCollider2D>();
            if (m_Collider != null)
            {
                m_Collider.isTrigger = true;
                if (m_Collider.radius > 0f)
                {
                    riftRadius = m_Collider.radius;
                }
                else if (riftRadius > 0f)
                {
                    m_Collider.radius = riftRadius;
                }
            }
        }

        public void Initialize(EnemyBrain boss, GameObject[] minionPrefabs = null)
        {
            m_Boss = boss;
            if (minionPrefabs != null && minionPrefabs.Length > 0)
            {
                chapter1MinionPrefabs = minionPrefabs;
            }

            SyncColliderRadius();

            m_Age = 0f;
            m_SpawnTimer = 0f;
            m_Triggered = false;
            m_IsDestroyed = false;
        }

        private void Update()
        {
            if (m_Triggered || m_IsDestroyed) return;

            float dt = Time.deltaTime;
            m_Age += dt;
            m_SpawnTimer += dt;

            // Kiểm tra chu kỳ sinh quái Chương 1
            if (m_SpawnTimer >= spawnInterval)
            {
                m_SpawnTimer = 0f;
                SpawnChapterOneMinion();
            }

            // Hết thời gian sống tự tiêu biến
            if (m_Age >= lifetime)
            {
                DestroyRift();
            }
        }

        public void ManualTick(float dt)
        {
            if (m_Triggered || m_IsDestroyed) return;

            m_Age += dt;
            m_SpawnTimer += dt;

            if (m_SpawnTimer >= spawnInterval)
            {
                m_SpawnTimer = 0f;
                SpawnChapterOneMinion();
            }

            if (m_Age >= lifetime)
            {
                DestroyRift();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandleTriggerEnter(other);
        }

        private void HandleTriggerEnter(Collider2D other)
        {
            if (m_Triggered || m_IsDestroyed || other == null) return;
            TankBrain tank = ResolveTank(other);
            if (tank != null && tank.IsAlive)
            {
                TriggerAbsorbTank(tank);
            }
        }

        private void HandleVfxTankEntered(SpatialRiftVFX sender, TankBrain tank)
        {
            if (m_Triggered || m_IsDestroyed || tank == null || !tank.IsAlive) return;
            TriggerAbsorbTank(tank);
        }

        public void TriggerRiftAbsorption(TankBrain tank)
        {
            TriggerAbsorbTank(tank);
        }

        public void TriggerAbsorbTank(TankBrain tank)
        {
            if (m_Triggered || m_IsDestroyed || tank == null || !tank.IsAlive) return;
            m_Triggered = true;

            // 1. Dịch chuyển Tank tới vị trí của Boss
            if (m_Boss != null && m_Boss.gameObject != null)
            {
                tank.transform.position = m_Boss.transform.position;
            }

            // 2. Gây 100% ATK sát thương lên Tank
            float bossAtk = (m_Boss != null && m_Boss.Stats != null) ? m_Boss.Stats.Attack : 120f;
            float finalDamage = bossAtk * damageRatio;

            DamageInfo info = new DamageInfo(
                m_Boss,
                tank,
                finalDamage,
                0f,
                tank.transform.position,
                sourceType: DamageSourceType.EnemyAttack);

            tank.TakeDamage(info);

            // 3. Hiệu ứng VFX nổ xoáy khi hút
            if (m_Vfx != null)
            {
                m_Vfx.TriggerExplosion();
            }

            // 4. Tiêu hủy vết nứt
            DestroyRift();
        }

        public GameObject SpawnChapterOneMinion()
        {
            if (this == null || gameObject == null || m_Triggered || m_IsDestroyed) return null;

            if (chapter1MinionPrefabs == null || chapter1MinionPrefabs.Length == 0)
            {
                Debug.LogWarning($"[SpatialRift] chapter1MinionPrefabs is empty on '{gameObject.name}'. Cannot spawn Chapter 1 minion.");
                return null;
            }

            int index = UnityEngine.Random.Range(0, chapter1MinionPrefabs.Length);
            GameObject prefab = chapter1MinionPrefabs[index];
            if (prefab == null)
            {
                Debug.LogWarning($"[SpatialRift] Minion prefab at index {index} is null on '{gameObject.name}'.");
                return null;
            }

            return Instantiate(prefab, transform.position, Quaternion.identity);
        }

        public void DestroyRift()
        {
            if (m_IsDestroyed) return;
            m_IsDestroyed = true;

            OnRiftDestroyed?.Invoke(this);

            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private static TankBrain ResolveTank(Collider2D col)
        {
            if (col == null) return null;

            if (col.TryGetComponent<ColliderOwnerRef>(out var ownerRef) && ownerRef != null)
            {
                if (ownerRef.TryGetOwner<TankBrain>(out var directBrain) && directBrain != null)
                    return directBrain;

                if (ownerRef.Owner is TankBrain brain)
                    return brain;

                if (ownerRef.Owner is Component comp)
                {
                    var compBrain = comp.GetComponent<TankBrain>() ?? comp.GetComponentInParent<TankBrain>();
                    if (compBrain != null) return compBrain;
                }

                if (ownerRef.Owner is GameObject go)
                {
                    var goBrain = go.GetComponent<TankBrain>() ?? go.GetComponentInParent<TankBrain>();
                    if (goBrain != null) return goBrain;
                }
            }

            return col.GetComponentInParent<TankBrain>();
        }

        private void OnValidate()
        {
            SyncColliderRadius();
        }
    }
}
