using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Vùng hồi máu do Sao Đen tạo ra xung quanh bản thân.
    /// - Quản lý mục tiêu và va chạm 100% thông qua ColliderOwnerRef.
    /// - Kích thước vùng hồi máu được điều chỉnh thông qua Transform.localScale của GameObject (Collider2D giữ kích thước mặc định).
    /// - KHÔNG chứa logic hồi máu trực tiếp: Chỉ kích hoạt event trả về danh sách Enemy mục tiêu
    ///   tìm được theo từng chu kỳ (tick) để EnemyBrain tự kích hoạt hồi máu.
    /// - Tồn tại trong khoảng thời gian zoneDuration trước khi tự động thu hồi về Pool.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class HealZone_SaoDen : MonoBehaviour
    {
        [Header("Heal Zone Settings")]
        [Tooltip("Thời gian tồn tại của vùng hồi máu (giây).")]
        [SerializeField, Min(0.5f)] private float zoneDuration = 2.0f;

        [Tooltip("Chu kỳ quét và phát sự kiện mục tiêu Enemy cần hồi máu (giây).")]
        [SerializeField, Min(0.1f)] private float healTickInterval = 0.5f;

        [Tooltip("Thời gian chờ ngắn ban đầu để physics 2D nạp trigger collider (giây).")]
        [SerializeField, Min(0f)] private float hitCheckDelay = 0.05f;

        [Header("Layers")]
        [SerializeField] private LayerMask enemyLayer;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color activeColor = new Color(0.2f, 0.9f, 0.4f, 0.45f);
        [SerializeField] private Color fadeOutColor = new Color(0.2f, 0.9f, 0.4f, 0.05f);

        /// <summary>
        /// Kích hoạt định kỳ mỗi healTickInterval khi quét được Enemy trong vùng.
        /// EnemyBrain đăng ký sự kiện này để kích hoạt hồi máu.
        /// </summary>
        public event Action<HealZone_SaoDen, HashSet<EnemyBrain>> OnTargetsDetected;

        private Collider2D m_Collider;
        private ColliderOwnerRef m_OwnerRef;
        private Rigidbody2D m_Rigidbody;
        private float m_ElapsedTime;
        private bool m_IsActive;
        private Coroutine m_LifeCoroutine;
        private Action<HealZone_SaoDen> m_OnDespawn;

        public float ZoneDuration { get => zoneDuration; set => zoneDuration = value; }
        public float HealTickInterval { get => healTickInterval; set => healTickInterval = value; }
        public float RemainingTime => Mathf.Max(0f, zoneDuration - m_ElapsedTime);
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

            if (enemyLayer.value == 0)
            {
                enemyLayer = LayerMask.GetMask("Enemy");
            }
        }

        public void SpawnAt(Vector2 position, Action<HealZone_SaoDen> onDespawn = null)
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
            TriggerHealTick();

            float tickTimer = 0f;

            while (m_ElapsedTime < zoneDuration)
            {
                yield return null;
                if (!m_IsActive) yield break;

                float dt = Time.deltaTime;
                m_ElapsedTime += dt;
                tickTimer += dt;

                // Hiệu ứng mờ dần theo thời gian
                if (spriteRenderer != null)
                {
                    float t = Mathf.Clamp01(m_ElapsedTime / zoneDuration);
                    spriteRenderer.color = Color.Lerp(activeColor, fadeOutColor, t);
                }

                // Nhịp tick hồi máu định kỳ
                if (tickTimer >= healTickInterval)
                {
                    tickTimer -= healTickInterval;
                    TriggerHealTick();
                }
            }

            Despawn();
        }

        private void TriggerHealTick()
        {
            HashSet<EnemyBrain> targets = GetCurrentTargets();
            if (targets.Count > 0)
            {
                OnTargetsDetected?.Invoke(this, targets);
            }
        }

        /// <summary>
        /// Quét và trả về danh sách các Enemy đang có mặt trong trigger của ColliderOwnerRef tại thời điểm hiện tại.
        /// Sử dụng 100% ColliderOwnerRef. Không chứa logic hồi máu.
        /// </summary>
        public HashSet<EnemyBrain> GetCurrentTargets()
        {
            var targets = new HashSet<EnemyBrain>();
            CollectEnemiesInZone(targets);
            return targets;
        }

        /// <summary>
        /// Thu thập các Enemy đang có mặt trong trigger của ColliderOwnerRef nạp vào tập kết quả đã cấp.
        /// </summary>
        public void CollectEnemiesInZone(HashSet<EnemyBrain> results)
        {
            if (results == null || m_OwnerRef == null) return;

            foreach (Collider2D col in m_OwnerRef.GetTriggerColliders())
            {
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;

                if (enemyLayer.value != 0 && (enemyLayer.value & (1 << col.gameObject.layer)) == 0)
                {
                    continue;
                }

                EnemyBrain enemy = ResolveEnemyBrain(col);
                if (enemy != null && enemy.IsAlive)
                {
                    results.Add(enemy);
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

            Action<HealZone_SaoDen> callback = m_OnDespawn;
            m_OnDespawn = null;
            callback?.Invoke(this);
        }

        private EnemyBrain ResolveEnemyBrain(Collider2D col)
        {
            if (col == null) return null;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
            if (targetRef != null)
            {
                if (targetRef.TryGetOwner<EnemyBrain>(out EnemyBrain directBrain) && directBrain != null)
                {
                    return directBrain;
                }

                if (targetRef.Owner is EnemyBrain brain)
                {
                    return brain;
                }

                if (targetRef.Owner is Component comp)
                {
                    EnemyBrain compBrain = comp.GetComponent<EnemyBrain>() ?? comp.GetComponentInParent<EnemyBrain>();
                    if (compBrain != null) return compBrain;
                }
                else if (targetRef.Owner is GameObject go)
                {
                    EnemyBrain goBrain = go.GetComponent<EnemyBrain>() ?? go.GetComponentInParent<EnemyBrain>();
                    if (goBrain != null) return goBrain;
                }
            }

            return col.GetComponentInParent<EnemyBrain>();
        }

        private void OnValidate()
        {
            zoneDuration = Mathf.Max(0.5f, zoneDuration);
            healTickInterval = Mathf.Max(0.1f, healTickInterval);
        }
    }
}
