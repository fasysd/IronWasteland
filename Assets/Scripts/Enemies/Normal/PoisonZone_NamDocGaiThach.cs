using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Vùng độc do Nấm Độc Gai Thạch tạo ra xung quanh bản thân.
    /// - Quản lý mục tiêu và va chạm 100% thông qua ColliderOwnerRef.
    /// - Kích thước vùng độc được điều chỉnh thông qua Transform.localScale của GameObject (Collider2D giữ kích thước mặc định).
    /// - KHÔNG chứa logic gây sát thương trực tiếp: Chỉ kích hoạt event trả về danh sách mục tiêu
    ///   tìm được theo từng chu kỳ (tick) để EnemyBrain tự kích hoạt sát thương.
    /// - Tồn tại trong khoảng thời gian poisonDuration trước khi tự động thu hồi về Pool.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PoisonZone_NamDocGaiThach : MonoBehaviour
    {
        [Header("Poison Zone Settings")]
        [Tooltip("Thời gian tồn tại của vùng độc trước khi biến mất (giây).")]
        [SerializeField, Min(0.5f)] private float poisonDuration = 3.5f;

        [Tooltip("Chu kỳ quét và phát sự kiện mục tiêu trúng độc (giây).")]
        [SerializeField, Min(0.1f)] private float damageTickInterval = 0.5f;

        [Tooltip("Thời gian chờ ngắn ban đầu để physics 2D nạp trigger collider (giây).")]
        [SerializeField, Min(0f)] private float hitCheckDelay = 0.05f;

        [Header("Layers")]
        [SerializeField] private LayerMask tankLayer;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color activeColor = new Color(0.45f, 0.15f, 0.6f, 0.4f);
        [SerializeField] private Color warningColor = new Color(0.65f, 0.1f, 0.35f, 0.6f);

        /// <summary>
        /// Kích hoạt định kỳ mỗi damageTickInterval khi quét được mục tiêu.
        /// EnemyBrain đăng ký sự kiện này để kích hoạt sát thương.
        /// </summary>
        public event Action<PoisonZone_NamDocGaiThach, HashSet<TankBrain>> OnTargetsDetected;

        private Collider2D m_Collider;
        private ColliderOwnerRef m_OwnerRef;
        private Rigidbody2D m_Rigidbody;
        private float m_ElapsedTime;
        private bool m_IsActive;
        private Coroutine m_LifeCoroutine;
        private Action<PoisonZone_NamDocGaiThach> m_OnDespawn;

        public float PoisonDuration { get => poisonDuration; set => poisonDuration = value; }
        public float DamageTickInterval { get => damageTickInterval; set => damageTickInterval = value; }
        public float RemainingTime => Mathf.Max(0f, poisonDuration - m_ElapsedTime);
        public bool IsActive => m_IsActive;

        private void Awake()
        {
            m_Collider = GetComponent<Collider2D>();
            m_OwnerRef = GetComponent<ColliderOwnerRef>();
            m_Rigidbody = GetComponent<Rigidbody2D>();

            // Đảm bảo có Kinematic Rigidbody2D để trigger events luôn tin cậy trong Unity 2D
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

        public void SpawnAt(Vector2 position, Action<PoisonZone_NamDocGaiThach> onDespawn = null)
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

            if (m_LifeCoroutine != null)
            {
                StopCoroutine(m_LifeCoroutine);
            }
            m_LifeCoroutine = StartCoroutine(ZoneLifetimeRoutine());
        }

        private IEnumerator ZoneLifetimeRoutine()
        {
            // Chờ ngắn để physics nạp trigger collider
            if (hitCheckDelay > 0f)
            {
                yield return new WaitForSeconds(hitCheckDelay);
            }
            else
            {
                yield return new WaitForFixedUpdate();
            }

            // Nhịp tick đầu tiên khi vừa xuất hiện
            TriggerDamageTick();

            float tickTimer = 0f;

            while (m_ElapsedTime < poisonDuration)
            {
                yield return null;
                if (!m_IsActive) yield break;

                float dt = Time.deltaTime;
                m_ElapsedTime += dt;
                tickTimer += dt;

                // Hiệu ứng cảnh báo khi sắp hết thời gian (0.8s cuối)
                float timeLeft = poisonDuration - m_ElapsedTime;
                if (spriteRenderer != null && timeLeft <= 0.8f)
                {
                    float t = Mathf.PingPong(m_ElapsedTime * 6f, 1f);
                    spriteRenderer.color = Color.Lerp(activeColor, warningColor, t);
                }

                // Nhịp tick gây sát thương định kỳ
                if (tickTimer >= damageTickInterval)
                {
                    tickTimer -= damageTickInterval;
                    TriggerDamageTick();
                }
            }

            Despawn();
        }

        private void TriggerDamageTick()
        {
            HashSet<TankBrain> targets = GetCurrentTargets();
            if (targets.Count > 0)
            {
                OnTargetsDetected?.Invoke(this, targets);
            }
        }

        /// <summary>
        /// Quét và trả về danh sách các xe tăng đang có mặt trong trigger của ColliderOwnerRef tại thời điểm hiện tại.
        /// Sử dụng 100% ColliderOwnerRef. Không chứa logic gây sát thương.
        /// </summary>
        public HashSet<TankBrain> GetCurrentTargets()
        {
            var targets = new HashSet<TankBrain>();
            CollectTanksInZone(targets);
            return targets;
        }

        /// <summary>
        /// Thu thập các xe tăng đang có mặt trong trigger của ColliderOwnerRef nạp vào tập kết quả đã cấp.
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

            Action<PoisonZone_NamDocGaiThach> callback = m_OnDespawn;
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

        private void OnValidate()
        {
            poisonDuration = Mathf.Max(0.5f, poisonDuration);
            damageTickInterval = Mathf.Max(0.1f, damageTickInterval);
        }
    }
}
