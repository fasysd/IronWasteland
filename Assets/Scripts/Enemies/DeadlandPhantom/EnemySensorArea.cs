using System;
using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.DeadlandPhantom
{
    /// <summary>
    /// Cảm biến mục tiêu dành cho Boss Bóng Ma Tử Địa và quái Tinh anh:
    /// - Kết hợp Collider2D Trigger và ColliderOwnerRef để phát hiện và theo dõi toàn bộ Tank trong phạm vi.
    /// - Bán kính quét có thể tinh chỉnh trực tiếp qua CircleCollider2D trên Inspector hoặc trường sensorRadius.
    /// - Tự động cập nhật danh sách khi Tank đi vào/ra hoặc bị tiêu diệt.
    /// - Cung cấp API truy vấn Tank gần nhất phục vụ di chuyển bám đuổi Kinematic và định vị kỹ năng.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemySensorArea : MonoBehaviour
    {
        [Header("Sensor Configuration")]
        [Tooltip("Bán kính vùng quét cảm biến (m). Nếu <= 0 sẽ tự lấy từ CircleCollider2D.")]
        [SerializeField] private float sensorRadius = 20f;

        private readonly HashSet<TankBrain> m_DetectedTanks = new HashSet<TankBrain>();
        private Collider2D m_Collider;
        private ColliderOwnerRef m_OwnerRef;

        public IReadOnlyCollection<TankBrain> DetectedTanks => m_DetectedTanks;
        public float SensorRadius => sensorRadius;

        private void Awake()
        {
            m_Collider = GetComponent<Collider2D>();
            m_OwnerRef = GetComponent<ColliderOwnerRef>();

            if (m_Collider == null)
            {
                Debug.LogWarning($"[EnemySensorArea] Collider2D is not found on '{gameObject.name}'. Sensor cannot detect targets via triggers.");
            }
            else
            {
                SyncColliderRadius();
                if (m_OwnerRef == null)
                {
                    Debug.LogWarning($"[EnemySensorArea] ColliderOwnerRef is missing on '{gameObject.name}'.");
                }
            }

            if (m_OwnerRef != null)
            {
                m_OwnerRef.TriggerEnter += HandleTriggerEnter;
                m_OwnerRef.TriggerExit += HandleTriggerExit;
            }
        }

        private void OnDestroy()
        {
            if (m_OwnerRef != null)
            {
                m_OwnerRef.TriggerEnter -= HandleTriggerEnter;
                m_OwnerRef.TriggerExit -= HandleTriggerExit;
            }

            foreach (var tank in m_DetectedTanks)
            {
                if (tank != null) tank.OnDeathEvent -= HandleTankDied;
            }
            m_DetectedTanks.Clear();
        }

        private void SyncColliderRadius()
        {
            if (m_Collider == null) m_Collider = GetComponent<Collider2D>();
            if (m_Collider != null)
            {
                m_Collider.isTrigger = true;
                if (m_Collider is CircleCollider2D circle)
                {
                    if (circle.radius > 0f && sensorRadius <= 0f)
                    {
                        sensorRadius = circle.radius;
                    }
                    else if (sensorRadius > 0f)
                    {
                        circle.radius = sensorRadius;
                    }
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandleTriggerEnter(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            HandleTriggerExit(other);
        }

        private void HandleTriggerEnter(Collider2D other)
        {
            if (other == null) return;
            TankBrain tank = ResolveTank(other);
            if (tank != null && tank.IsAlive)
            {
                RegisterTank(tank);
            }
        }

        private void HandleTriggerExit(Collider2D other)
        {
            if (other == null) return;
            TankBrain tank = ResolveTank(other);
            if (tank != null)
            {
                UnregisterTank(tank);
            }
        }

        public void RegisterTank(TankBrain tank)
        {
            if (tank == null || !tank.IsAlive) return;
            if (m_DetectedTanks.Add(tank))
            {
                tank.OnDeathEvent -= HandleTankDied;
                tank.OnDeathEvent += HandleTankDied;
            }
        }

        public void UnregisterTank(TankBrain tank)
        {
            if (tank == null) return;
            tank.OnDeathEvent -= HandleTankDied;
            m_DetectedTanks.Remove(tank);
        }

        private void HandleTankDied()
        {
            m_DetectedTanks.RemoveWhere(t => t == null || !t.IsAlive);
        }

        /// <summary>
        /// Tìm Tank gần vị trí chỉ định nhất trong vùng cảm biến (hỗ trợ loại trừ Tank bị nhốt trong Sub-Arena).
        /// </summary>
        public TankBrain GetNearestTarget(Vector2 fromPosition, TankBrain excludeTank = null)
        {
            m_DetectedTanks.RemoveWhere(t => t == null || !t.IsAlive);

            TankBrain nearest = null;
            float minSqrDistance = float.MaxValue;

            foreach (var tank in m_DetectedTanks)
            {
                if (tank == null || !tank.IsAlive || tank == excludeTank) continue;

                float sqrDist = ((Vector2)tank.transform.position - fromPosition).sqrMagnitude;
                if (sqrDist < minSqrDistance)
                {
                    minSqrDistance = sqrDist;
                    nearest = tank;
                }
            }

            return nearest;
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
