using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Vùng nổ do Mặt Trời Đỏ tạo ra.
    /// - Quản lý mục tiêu và va chạm 100% thông qua ColliderOwnerRef.
    /// - Kích thước vùng nổ được điều chỉnh thông qua Transform.localScale của GameObject (Collider2D giữ kích thước mặc định).
    /// - KHÔNG chứa logic gây sát thương hay spawn quái trực tiếp:
    ///   Khi hết thời gian đếm ngược (phát nổ), kích hoạt sự kiện OnExploded trả về danh sách Tank trong vùng,
    ///   vị trí tâm vụ nổ, và bán kính hiệu dụng để EnemyBrain tự kích hoạt sát thương và triệu hồi Ký Sinh Thiên Thạch.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class ExplosionZone_MatTroiDo : MonoBehaviour
    {
        [Header("Explosion Zone Settings")]
        [Tooltip("Thời gian từ lúc xuất hiện đến khi phát nổ (giây).")]
        [SerializeField, Min(0.5f)] private float detonationDelay = 2.0f;

        [Tooltip("Thời gian chờ ngắn để Physics2D nạp trigger collider trước khi quét mục tiêu (giây).")]
        [SerializeField, Min(0f)] private float hitCheckDelay = 0.05f;

        [Header("Layers")]
        [SerializeField] private LayerMask tankLayer;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color warningStartColor = new Color(1f, 0.45f, 0.1f, 0.35f);
        [SerializeField] private Color warningEndColor = new Color(1f, 0.15f, 0.1f, 0.8f);
        [SerializeField] private float flashSpeed = 8f;

        /// <summary>
        /// Kích hoạt khi vùng phát nổ: trả về (zone, danh sách Tank trong vùng, vị trí tâm nổ, bán kính nổ).
        /// EnemyBrain đăng ký sự kiện này để kích hoạt sát thương và spawn Ký Sinh Thiên Thạch.
        /// </summary>
        public event Action<ExplosionZone_MatTroiDo, HashSet<TankBrain>, Vector2, float> OnExploded;

        private Collider2D m_Collider;
        private ColliderOwnerRef m_OwnerRef;
        private Rigidbody2D m_Rigidbody;
        private float m_ElapsedTime;
        private bool m_IsActive;
        private Coroutine m_LifeCoroutine;
        private Action<ExplosionZone_MatTroiDo> m_OnDespawn;

        public float DetonationDelay { get => detonationDelay; set => detonationDelay = value; }
        public float RemainingTime => Mathf.Max(0f, detonationDelay - m_ElapsedTime);
        public bool IsActive => m_IsActive;

        private void Awake()
        {
            m_Collider = GetComponent<Collider2D>();
            m_OwnerRef = GetComponent<ColliderOwnerRef>();
            m_Rigidbody = GetComponent<Rigidbody2D>();

            // Đảm bảo có Kinematic Rigidbody2D để trigger tracking luôn tin cậy trong Unity 2D
            if (m_Rigidbody == null)
            {
                m_Rigidbody = gameObject.AddComponent<Rigidbody2D>();
            }
            m_Rigidbody.bodyType = RigidbodyType2D.Kinematic;
            m_Rigidbody.simulated = true;
            m_Rigidbody.useFullKinematicContacts = true;

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

        public void SpawnAt(Vector2 position, Action<ExplosionZone_MatTroiDo> onDespawn = null)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            m_OnDespawn = onDespawn;
            m_ElapsedTime = 0f;
            m_IsActive = true;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = warningStartColor;
            }

            gameObject.SetActive(true);

            if (m_LifeCoroutine != null)
            {
                StopCoroutine(m_LifeCoroutine);
            }
            m_LifeCoroutine = StartCoroutine(ZoneLifetimeRoutine());
        }

        private IEnumerator ZoneLifetimeRoutine()
        {
            // Nhấp nháy và chuyển dần sang màu đỏ cảnh báo trước khi nổ
            while (m_ElapsedTime < detonationDelay)
            {
                yield return null;
                if (!m_IsActive) yield break;

                float dt = Time.deltaTime;
                m_ElapsedTime += dt;

                float t = Mathf.Clamp01(m_ElapsedTime / detonationDelay);

                if (spriteRenderer != null)
                {
                    // Tăng tốc độ nhấp nháy khi càng gần thời điểm nổ
                    float currentFlashSpeed = Mathf.Lerp(flashSpeed * 0.5f, flashSpeed * 2.0f, t);
                    float pulse = (Mathf.Sin(m_ElapsedTime * currentFlashSpeed) + 1f) * 0.5f;

                    Color baseColor = Color.Lerp(warningStartColor, warningEndColor, t);
                    Color pulseColor = baseColor;
                    pulseColor.a = Mathf.Lerp(baseColor.a * 0.6f, Mathf.Min(1f, baseColor.a * 1.3f), pulse);

                    spriteRenderer.color = pulseColor;
                }
            }

            // Chờ ngắn để physics nạp trigger collider đồng bộ nếu cần
            if (hitCheckDelay > 0f)
            {
                yield return new WaitForSeconds(hitCheckDelay);
            }

            TriggerExplosion();
        }

        private void TriggerExplosion()
        {
            HashSet<TankBrain> targets = GetCurrentTargets();
            Vector2 explosionPos = transform.position;
            float radius = GetExplosionRadius();

            OnExploded?.Invoke(this, targets, explosionPos, radius);

            Despawn();
        }

        /// <summary>
        /// Quét và trả về danh sách các xe tăng đang có mặt trong trigger của ColliderOwnerRef tại thời điểm nổ.
        /// Sử dụng 100% ColliderOwnerRef. Không chứa logic gây sát thương.
        /// </summary>
        public HashSet<TankBrain> GetCurrentTargets()
        {
            var targets = new HashSet<TankBrain>();
            CollectTanksInZone(targets);
            return targets;
        }

        /// <summary>
        /// Thu thập các xe tăng đang có mặt trong trigger của ColliderOwnerRef nạp vào tập kết quả.
        /// </summary>
        public void CollectTanksInZone(HashSet<TankBrain> results)
        {
            if (results == null || m_OwnerRef == null) return;

            foreach (Collider2D col in m_OwnerRef.GetTriggerColliders())
            {
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;

                if (tankLayer.value != 0 && (tankLayer.value & (1 << col.gameObject.layer)) == 0)
                {
                    continue;
                }

                TankBrain tank = ResolveTankBrain(col);
                if (tank != null && tank.IsAlive)
                {
                    results.Add(tank);
                }
            }
        }

        /// <summary>
        /// Tính bán kính nổ thực tế dựa trên Transform.localScale / lossyScale (Collider mặc định radius = 0.5f).
        /// </summary>
        public float GetExplosionRadius()
        {
            float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
            float baseRadius = 0.5f;
            if (m_Collider is CircleCollider2D circle)
            {
                baseRadius = circle.radius;
            }
            return scale * baseRadius;
        }

        public void Despawn()
        {
            if (!m_IsActive) return;

            m_IsActive = false;

            if (m_LifeCoroutine != null)
            {
                StopCoroutine(m_LifeCoroutine);
                m_LifeCoroutine = null;
            }

            gameObject.SetActive(false);

            Action<ExplosionZone_MatTroiDo> callback = m_OnDespawn;
            m_OnDespawn = null;
            callback?.Invoke(this);
        }

        private TankBrain ResolveTankBrain(Collider2D col)
        {
            if (col == null) return null;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
            if (targetRef != null)
            {
                if (targetRef.TryGetOwner<TankBrain>(out TankBrain directBrain) && directBrain != null)
                {
                    return directBrain;
                }

                if (targetRef.Owner is TankBrain brain)
                {
                    return brain;
                }

                if (targetRef.Owner is Component comp)
                {
                    TankBrain compBrain = comp.GetComponent<TankBrain>() ?? comp.GetComponentInParent<TankBrain>();
                    if (compBrain != null) return compBrain;
                }
                else if (targetRef.Owner is GameObject go)
                {
                    TankBrain goBrain = go.GetComponent<TankBrain>() ?? go.GetComponentInParent<TankBrain>();
                    if (goBrain != null) return goBrain;
                }
            }

            return col.GetComponentInParent<TankBrain>();
        }

        private void OnDisable()
        {
            if (m_LifeCoroutine != null)
            {
                StopCoroutine(m_LifeCoroutine);
                m_LifeCoroutine = null;
            }
            m_IsActive = false;
        }
    }
}
