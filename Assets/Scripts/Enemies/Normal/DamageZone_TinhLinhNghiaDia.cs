using System;
using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Vùng sát thương hình tròn do Tinh Linh Nghĩa Địa tạo ra trên mặt đất.
    /// Thiết lập bán kính (zoneRadius) và thời gian tồn tại (lifetime) trực tiếp trên Inspector của component này.
    /// Hỗ trợ cơ chế Object Pooling: khi hết thời gian, gọi Despawn để tự SetActive(false) và trả về Pool thay vì Destroy.
    /// Không tự ý trừ máu của xe tăng mà cung cấp hàm CollectTanksInZone để NormalEnemy_TinhLinhNghiaDia
    /// tổng hợp và gây sát thương đồng loạt, tránh việc xe tăng nhận nhiều lần sát thương khi đứng đè lên nhiều vùng.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class DamageZone_TinhLinhNghiaDia : MonoBehaviour
    {
        [Tooltip("Thời gian tồn tại của vùng trước khi biến mất (giây).")]
        [SerializeField, Min(0.5f)] private float lifetime = 4.0f;

        [Header("Layers")]
        [SerializeField] private LayerMask tankLayer;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color activeColor = new Color(0.2f, 0.8f, 0.5f, 0.35f);
        [SerializeField] private Color fadeOutColor = new Color(0.2f, 0.8f, 0.5f, 0.0f);

        private CircleCollider2D m_Collider;
        private ColliderOwnerRef m_OwnerRef;
        private float m_ElapsedTime;
        private bool m_IsActive;
        private Action<DamageZone_TinhLinhNghiaDia> m_OnDespawn;

        public float Lifetime { get => lifetime; set => lifetime = value; }
        public bool IsActive => m_IsActive;

        private void Awake()
        {
            m_Collider = GetComponent<CircleCollider2D>();
            m_OwnerRef = GetComponent<ColliderOwnerRef>();

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (m_Collider != null)
            {
                m_Collider.isTrigger = true;
            }

            if (tankLayer.value == 0)
            {
                tankLayer = LayerMask.GetMask("Tank");
            }
        }

        /// <summary>
        /// Kích hoạt vùng tại tọa độ thế giới chỉ định.
        /// Sử dụng chính các cấu hình zoneRadius và lifetime đã thiết lập trên Inspector của component này.
        /// </summary>
        public void SpawnAt(Vector2 position, Action<DamageZone_TinhLinhNghiaDia> onDespawn = null)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            m_OnDespawn = onDespawn;
            m_ElapsedTime = 0f;
            m_IsActive = true;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = activeColor;
            }

            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!m_IsActive) return;

            m_ElapsedTime += Time.deltaTime;

            // Hiệu ứng mờ dần (fade out) trong 0.8 giây cuối
            float timeLeft = lifetime - m_ElapsedTime;
            if (spriteRenderer != null && timeLeft <= 0.8f)
            {
                float t = Mathf.Clamp01(timeLeft / 0.8f);
                spriteRenderer.color = Color.Lerp(fadeOutColor, activeColor, t);
            }

            if (m_ElapsedTime >= lifetime)
            {
                Despawn();
            }
        }

        /// <summary>
        /// Thu thập tất cả các xe tăng đang đứng trong phạm vi của vùng vào HashSet chung.
        /// Sử dụng HashSet để đảm bảo xe tăng đứng trên nhiều vùng cùng lúc chỉ xuất hiện duy nhất 1 lần.
        /// </summary>
        public void CollectTanksInZone(HashSet<TankBrain> results)
        {
            if (!m_IsActive || results == null) return;

            // 1. Quét từ ColliderOwnerRef trigger tracking
            if (m_OwnerRef != null)
            {
                foreach (Collider2D col in m_OwnerRef.GetTriggerColliders())
                {
                    if (col == null) continue;
                    TankBrain tank = ResolveTankBrain(col);
                    if (tank != null && tank.IsAlive)
                    {
                        results.Add(tank);
                    }
                }
            }
        }

        private TankBrain ResolveTankBrain(Collider2D col)
        {
            if (col == null) return null;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
            if (targetRef != null && targetRef.Owner != null)
            {
                if (targetRef.Owner is TankBrain directBrain)
                {
                    return directBrain;
                }

                if (targetRef.Owner is Component comp)
                {
                    TankBrain brain = comp.GetComponent<TankBrain>() ?? comp.GetComponentInParent<TankBrain>();
                    if (brain != null) return brain;
                }
                else if (targetRef.Owner is GameObject go)
                {
                    TankBrain brain = go.GetComponent<TankBrain>() ?? go.GetComponentInParent<TankBrain>();
                    if (brain != null) return brain;
                }
            }

            return col.GetComponentInParent<TankBrain>();
        }

        /// <summary>
        /// Thu hồi vùng về Pool (tắt Active và gọi callback để Pool quản lý, không gọi Destroy).
        /// </summary>
        public void Despawn()
        {
            if (!m_IsActive) return;

            m_IsActive = false;
            gameObject.SetActive(false);

            Action<DamageZone_TinhLinhNghiaDia> callback = m_OnDespawn;
            m_OnDespawn = null;
            callback?.Invoke(this);
        }

        private void OnValidate()
        {
            lifetime = Mathf.Max(0.5f, lifetime);
        }
    }
}
