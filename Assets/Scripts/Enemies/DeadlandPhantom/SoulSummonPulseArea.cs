using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.DeadlandPhantom
{
    /// <summary>
    /// Vùng xung kích làm chậm của kỹ năng Triệu Hồi Linh Hồn:
    /// - Quản lý CircleCollider2D Trigger để làm chậm mọi Tank trong phạm vi.
    /// - Không sử dụng Physics2D.Overlap mà dùng Trigger Collider kết hợp ColliderOwnerRef.
    /// </summary>
    [DisallowMultipleComponent]
    public class SoulSummonPulseArea : MonoBehaviour
    {
        [Header("Slow Pulse Settings")]
        [Tooltip("Bán kính vùng quét làm chậm (m). Nếu <= 0 sẽ tự lấy từ CircleCollider2D.")]
        [SerializeField] private float pulseRadius = 10f;

        [Tooltip("Tỷ lệ làm chậm (0.2 = 20%).")]
        [SerializeField, Range(0f, 1f)] private float slowRatio = 0.2f;

        [Tooltip("Thời gian làm chậm (giây). GDD: 5s.")]
        [SerializeField, Min(0.1f)] private float slowDuration = 5f;

        [Tooltip("Thời gian tồn tại của vùng xung kích trước khi tự hủy (giây).")]
        [SerializeField, Min(0.05f)] private float lifetime = 0.5f;

        private readonly HashSet<TankBrain> m_AffectedTanks = new HashSet<TankBrain>();
        private CircleCollider2D m_CircleCollider;
        private ColliderOwnerRef m_OwnerRef;
        private float m_Age;

        public float PulseRadius => pulseRadius;
        public float SlowRatio => slowRatio;
        public float SlowDuration => slowDuration;
        public IReadOnlyCollection<TankBrain> AffectedTanks => m_AffectedTanks;

        private void Awake()
        {
            m_CircleCollider = GetComponent<CircleCollider2D>();
            m_OwnerRef = GetComponent<ColliderOwnerRef>();

            if (m_CircleCollider == null)
            {
                Debug.LogWarning($"[SoulSummonPulseArea] CircleCollider2D is not found on '{gameObject.name}'. Pulse area cannot detect targets via triggers.");
            }
            else
            {
                m_CircleCollider.isTrigger = true;
                if (m_CircleCollider.radius > 0f && pulseRadius <= 0f)
                {
                    pulseRadius = m_CircleCollider.radius;
                }
                else if (pulseRadius > 0f)
                {
                    m_CircleCollider.radius = pulseRadius;
                }
            }

            if (m_OwnerRef != null)
            {
                m_OwnerRef.TriggerEnter += HandleTriggerEnter;
            }
        }

        private void OnDestroy()
        {
            if (m_OwnerRef != null)
            {
                m_OwnerRef.TriggerEnter -= HandleTriggerEnter;
            }
        }

        private void Update()
        {
            m_Age += Time.deltaTime;
            if (m_Age >= lifetime)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandleTriggerEnter(other);
        }

        private void HandleTriggerEnter(Collider2D other)
        {
            if (other == null) return;
            TankBrain tank = ResolveTank(other);
            if (tank != null && tank.IsAlive)
            {
                ApplySlow(tank);
            }
        }

        public void ApplySlow(TankBrain tank)
        {
            if (tank == null || !tank.IsAlive) return;
            if (!m_AffectedTanks.Add(tank)) return;

            TankSlowDebuff.Apply(tank, slowRatio, slowDuration);
        }

        public void Initialize(EnemyBrain owner, float radius = -1f, float customSlowRatio = -1f, float customSlowDuration = -1f)
        {
            if (m_OwnerRef == null) m_OwnerRef = GetComponent<ColliderOwnerRef>();
            if (m_OwnerRef != null && owner != null) m_OwnerRef.SetOwner(owner);

            if (radius > 0f)
            {
                pulseRadius = radius;
                if (m_CircleCollider == null) m_CircleCollider = GetComponent<CircleCollider2D>();
                if (m_CircleCollider != null) m_CircleCollider.radius = pulseRadius;
            }

            if (customSlowRatio >= 0f) slowRatio = customSlowRatio;
            if (customSlowDuration > 0f) slowDuration = customSlowDuration;
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
            }
            return col.GetComponentInParent<TankBrain>();
        }

        private void OnValidate()
        {
            if (m_CircleCollider == null) m_CircleCollider = GetComponent<CircleCollider2D>();
            if (m_CircleCollider != null)
            {
                if (pulseRadius > 0f) m_CircleCollider.radius = pulseRadius;
                else if (m_CircleCollider.radius > 0f) pulseRadius = m_CircleCollider.radius;
            }
        }
    }
}
