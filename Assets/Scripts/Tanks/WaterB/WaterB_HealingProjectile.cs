using UnityEngine;
using IronWasteland.Enemies;

namespace IronWasteland.Tanks.WaterB
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class WaterB_HealingProjectile : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float speed = 40f; // Tốc độ bay cực nhanh
        [SerializeField] private float maxLifeTime = 3f;

        private TankBrain m_OwnerTank;
        private Vector3 m_MoveDirection;
        private float m_PowerAmount;
        private bool m_IsAssaultMode;
        private System.Action<float> m_OnSuccess;
        private float m_SpawnTime;

        public void Initialize(TankBrain owner, Vector3 direction, float powerAmount, bool isAssaultMode, System.Action<float> onSuccess)
        {
            m_OwnerTank = owner;
            m_MoveDirection = direction.normalized;
            m_PowerAmount = powerAmount;
            m_IsAssaultMode = isAssaultMode;
            m_OnSuccess = onSuccess;
            m_SpawnTime = Time.time;

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
            transform.position += m_MoveDirection * speed * Time.deltaTime;

            if (m_MoveDirection != Vector3.zero)
            {
                float angle = Mathf.Atan2(m_MoveDirection.y, m_MoveDirection.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (Time.time - m_SpawnTime < 0.05f) return;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collision);
            if (targetRef == null) return;

            if (m_IsAssaultMode)
            {
                // DẠNG 2C (AQUA ASSAULT): Bắn trúng kẻ địch
                EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
                if (enemy != null)
                {
                    ApplyDamageToEnemy(enemy);
                }
            }
            else
            {
                // DẠNG THƯỜNG: Bắn trúng Tank (Bản thân hoặc Đồng minh)
                TankBrain targetTank = targetRef.GetOwner<TankBrain>();
                if (targetTank != null)
                {
                    ApplyHealingToTank(targetTank);
                }
            }
        }

        private void ApplyHealingToTank(TankBrain targetTank)
        {
            if (m_PowerAmount > 0f)
            {
                if (m_OwnerTank is TankBrain_WaterB waterBTank)
                {
                    waterBTank.HandleProjectileHeal(targetTank, m_PowerAmount);
                }
                else
                {
                    targetTank.Heal(m_PowerAmount, false);
                    TextDamageManager.Instance?.Show(m_PowerAmount, targetTank.transform.position);
                }
            }

            m_OnSuccess?.Invoke(m_PowerAmount);
            Destroy(gameObject); // Chạm vào tank là hủy ngay lập tức
        }

        private void ApplyDamageToEnemy(EnemyBrain enemy)
        {
            // 1. Gây sát thương lên kẻ địch
            enemy.TakeDamage(m_PowerAmount);
            if (m_PowerAmount > 0f)
            {
                TextDamageManager.Instance?.Show(m_PowerAmount, enemy.transform.position);
            }

            // 2. KHÔI PHỤC LOGIC 2C: Hồi máu lại cho Water B khi gây sát thương lên kẻ địch[cite: 7]
            if (m_OwnerTank is TankBrain_WaterB waterBTank)
            {
                float totalHeal = 0f;

                // Mặc định Aqua Assault khi đánh trúng tự hồi máu cơ bản (20% sát thương gây ra)[cite: 7]
                float baseAssaultHeal = m_PowerAmount * 0.2f;
                totalHeal += baseAssaultHeal;

                // Nếu đang bật đồng thời chiêu phụ 1B (Life Drain), cộng thêm lượng % hút máu[cite: 7]
                if (waterBTank.IsLifeDrainActive)
                {
                    float lifeDrainHeal = m_PowerAmount * waterBTank.LifeDrainPercent;
                    totalHeal += lifeDrainHeal;
                }

                // Thực hiện hồi máu cho Water B và kích hoạt truyền/chia sẻ nếu đang có liên kết[cite: 7]
                if (totalHeal > 0f)
                {
                    waterBTank.TriggerHealAndShare(totalHeal, false);
                }
            }

            m_OnSuccess?.Invoke(m_PowerAmount);
            Destroy(gameObject); // Chạm vào kẻ địch là hủy ngay lập tức[cite: 7]
        }
    }
}