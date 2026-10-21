using System;
using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Vùng quét mục tiêu lân cận phục vụ lan truyền sét (Chain Lightning).
    /// - Quản lý mục tiêu và va chạm 100% thông qua ColliderOwnerRef.
    /// - Kích thước vùng quét được điều chỉnh thông qua Transform.localScale của GameObject (Collider2D giữ kích thước mặc định unit radius = 0.5f).
    /// - KHÔNG chứa logic gây sát thương trực tiếp: Chỉ quét và trả về danh sách Tank trong tầm nhảy sét để EnemyBrain kích hoạt sát thương.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class LightningChainScanner_TinhLinhSamSetNho : MonoBehaviour
    {
        [Header("Scanner Settings")]
        [Tooltip("Layer nhận diện Xe Tăng.")]
        [SerializeField] private LayerMask tankLayer;

        private CircleCollider2D m_Collider;
        private ColliderOwnerRef m_OwnerRef;
        private Rigidbody2D m_Rigidbody;
        private readonly List<Collider2D> m_OverlapBuffer = new List<Collider2D>(16);
        private ContactFilter2D m_ContactFilter;

        public CircleCollider2D Collider => m_Collider;
        public ColliderOwnerRef OwnerRef => m_OwnerRef;

        private void Awake()
        {
            m_Collider = GetComponent<CircleCollider2D>();
            m_OwnerRef = GetComponent<ColliderOwnerRef>();
            m_Rigidbody = GetComponent<Rigidbody2D>();

            if (m_Rigidbody == null)
            {
                m_Rigidbody = gameObject.AddComponent<Rigidbody2D>();
            }
            m_Rigidbody.bodyType = RigidbodyType2D.Kinematic;
            m_Rigidbody.simulated = true;
            m_Rigidbody.useFullKinematicContacts = true;

            if (m_Collider != null)
            {
                m_Collider.isTrigger = true;
                m_Collider.radius = 0.5f; // Kích thước mặc định chuẩn unit circle
            }

            if (tankLayer.value == 0)
            {
                tankLayer = LayerMask.GetMask("Tank");
            }

            m_ContactFilter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = true,
                layerMask = tankLayer
            };
        }

        /// <summary>
        /// Di chuyển scanner đến tâm vị trí centerPos và thiết lập bán kính quét thông qua Transform.localScale.
        /// </summary>
        public void PositionAt(Vector2 centerPos, float jumpDistance)
        {
            transform.position = new Vector3(centerPos.x, centerPos.y, transform.position.z);
            // CircleCollider2D có radius = 0.5f, đường kính là 1.0f.
            // Để vùng quét có bán kính = jumpDistance, đường kính là jumpDistance * 2f -> localScale = jumpDistance * 2f.
            float scale = Mathf.Max(0.1f, jumpDistance * 2f);
            transform.localScale = new Vector3(scale, scale, 1f);

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            Physics2D.SyncTransforms();
        }

        /// <summary>
        /// Thu thập tất cả các Tank đang có mặt trong vùng quét của scanner.
        /// Sử dụng 100% ColliderOwnerRef để định danh mục tiêu.
        /// </summary>
        public void CollectNearbyTanks(HashSet<TankBrain> results)
        {
            if (results == null || m_OwnerRef == null) return;

            // 1. Quét các Collider2D đang được ColliderOwnerRef theo dõi
            foreach (Collider2D col in m_OwnerRef.GetTriggerColliders())
            {
                ProcessCollider(col, results);
            }

            // 2. Quét overlap tức thì thông qua Collider2D.OverlapCollider làm dữ liệu bổ sung tin cậy
            if (m_Collider != null)
            {
                m_OverlapBuffer.Clear();
                int hitCount = m_Collider.Overlap(m_ContactFilter, m_OverlapBuffer);
                for (int i = 0; i < hitCount; i++)
                {
                    ProcessCollider(m_OverlapBuffer[i], results);
                }
            }
        }

        private void ProcessCollider(Collider2D col, HashSet<TankBrain> results)
        {
            if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) return;

            if (tankLayer.value != 0 && (tankLayer.value & (1 << col.gameObject.layer)) == 0)
            {
                return;
            }

            TankBrain tank = ResolveTankBrain(col);
            if (tank != null && tank.IsAlive)
            {
                results.Add(tank);
            }
        }

        /// <summary>
        /// Trích xuất TankBrain hoàn toàn thông qua ColliderOwnerRef.
        /// </summary>
        public static TankBrain ResolveTankBrain(Collider2D col)
        {
            if (col == null) return null;

            ColliderOwnerRef ownerRef = col.GetComponent<ColliderOwnerRef>() ?? col.GetComponentInParent<ColliderOwnerRef>();
            if (ownerRef == null) return null;

            if (ownerRef.Owner is TankBrain tb)
            {
                return tb;
            }

            if (ownerRef.Owner is GameObject go)
            {
                return go.GetComponent<TankBrain>() ?? go.GetComponentInParent<TankBrain>();
            }

            if (ownerRef.Owner is Component comp)
            {
                return comp.GetComponent<TankBrain>() ?? comp.GetComponentInParent<TankBrain>();
            }

            return col.GetComponentInParent<TankBrain>();
        }

        public void Deactivate()
        {
            gameObject.SetActive(false);
        }
    }
}
