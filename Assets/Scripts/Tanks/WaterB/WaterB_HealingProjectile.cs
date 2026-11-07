using UnityEngine;
using IronWasteland.Enemies;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Đạn của Healing Water (kỹ năng thường): dòng nước tấn công KẺ ĐỊCH.
    /// Giá trị (80% ATK + 1% Giới hạn máu) do TankBrain_WaterB tính sẵn và truyền vào.
    /// - Chạm kẻ địch -> TankBrain_WaterB.HandleProjectileDamage xử lý sát thương (kèm Life Drain nếu đang bật),
    ///   sau đó gọi onSuccess để Water B tích lũy năng lượng.
    /// - Không còn tương tác với đồng minh.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class WaterB_HealingProjectile : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float speed = 40f;
        [SerializeField] private float maxLifeTime = 3f;

        [Header("Visual")]
        [Tooltip("Hiệu ứng bong bóng nước lấp lánh sinh ra tại điểm chạm (particle...). Để trống = không có.")]
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField, Min(0.1f)] private float hitEffectLifetime = 2f;

        private TankBrain m_OwnerTank;
        private Vector3 m_MoveDirection;
        private float m_PowerAmount;
        private System.Action<float> m_OnSuccess;
        private float m_SpawnTime;
        private float m_Speed;
        private bool m_Consumed;

        public void Initialize(TankBrain owner, Vector3 direction, float powerAmount,
                               System.Action<float> onSuccess, float speedMultiplier = 1f, float sizeMultiplier = 1f)
        {
            m_OwnerTank = owner;
            m_MoveDirection = direction.normalized;
            m_PowerAmount = powerAmount;
            m_OnSuccess = onSuccess;
            m_SpawnTime = Time.time;
            m_Speed = speed * Mathf.Max(0.1f, speedMultiplier);

            // Aqua Assault: phát bắn lớn hơn
            if (!Mathf.Approximately(sizeMultiplier, 1f)) transform.localScale *= sizeMultiplier;

            Destroy(gameObject, maxLifeTime);
        }

        private void Start()
        {
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;

            Collider2D col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void Update()
        {
            transform.position += m_MoveDirection * m_Speed * Time.deltaTime;

            if (m_MoveDirection != Vector3.zero)
            {
                float angle = Mathf.Atan2(m_MoveDirection.y, m_MoveDirection.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (m_Consumed) return;
            if (Time.time - m_SpawnTime < 0.05f) return;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collision);
            if (targetRef == null) return;

            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null) HitEnemy(enemy);
        }

        private void HitEnemy(EnemyBrain enemy)
        {
            m_Consumed = true;

            Vector3 hitPos = enemy.transform.position; // lấy trước vì kẻ địch có thể chết ngay

            if (m_PowerAmount > 0f)
            {
                if (m_OwnerTank is TankBrain_WaterB waterB)
                {
                    waterB.HandleProjectileDamage(enemy, m_PowerAmount);
                }
                else
                {
                    enemy.TakeDamage(m_PowerAmount);
                    TextDamageManager.Instance?.Show(m_PowerAmount, hitPos);
                }

                // GDD: mỗi lần đánh trúng thành công sẽ tích lũy năng lượng cho Water B
                m_OnSuccess?.Invoke(m_PowerAmount);
            }

            SpawnHitEffect(hitPos);
            Destroy(gameObject);
        }

        private void SpawnHitEffect(Vector3 position)
        {
            if (hitEffectPrefab == null) return;

            position.z = 0f;
            GameObject fx = Instantiate(hitEffectPrefab, position, Quaternion.identity);
            Destroy(fx, hitEffectLifetime);
        }
    }
}