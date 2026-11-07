using UnityEngine;
using IronWasteland.Enemies;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Đạn của Healing Water. Giá trị (80% ATK + 1% Giới hạn máu) do TankBrain_WaterB tính sẵn và truyền vào.
    /// - Dạng thường: chạm xe tăng -> hồi máu (và hồi năng lượng cho Water B qua onSuccess).
    /// - Dạng Aqua Assault: chạm kẻ địch -> gây sát thương (mọi xử lý hậu quả như Life Drain nằm ở TankBrain_WaterB).
    /// - Đạn hình ảnh (power = 0) dùng cho tia liên kết Shared Healing: không hồi, không sát thương.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class WaterB_HealingProjectile : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float speed = 40f;
        [SerializeField] private float maxLifeTime = 3f;

        private TankBrain m_OwnerTank;
        private Vector3 m_MoveDirection;
        private float m_PowerAmount;
        private bool m_IsAssaultMode;
        private System.Action<float> m_OnSuccess;
        private float m_SpawnTime;
        private float m_Speed;
        private bool m_Consumed;

        public void Initialize(TankBrain owner, Vector3 direction, float powerAmount, bool isAssaultMode,
                               System.Action<float> onSuccess, float speedMultiplier = 1f, float sizeMultiplier = 1f)
        {
            m_OwnerTank = owner;
            m_MoveDirection = direction.normalized;
            m_PowerAmount = powerAmount;
            m_IsAssaultMode = isAssaultMode;
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

            if (m_IsAssaultMode)
            {
                EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
                if (enemy != null) HitEnemy(enemy);
            }
            else
            {
                TankBrain targetTank = targetRef.GetOwner<TankBrain>();
                if (targetTank != null) HitTank(targetTank);
            }
        }

        private void HitTank(TankBrain targetTank)
        {
            m_Consumed = true;

            if (m_PowerAmount > 0f)
            {
                if (m_OwnerTank is TankBrain_WaterB waterB)
                {
                    waterB.HealAlly(targetTank, m_PowerAmount);
                }
                else
                {
                    targetTank.Heal(m_PowerAmount, false);
                    TextDamageManager.Instance?.Show(m_PowerAmount, targetTank.transform.position);
                }

                // GDD: mỗi lần hồi máu thành công sẽ tích lũy năng lượng cho Water B
                m_OnSuccess?.Invoke(m_PowerAmount);
            }

            Destroy(gameObject);
        }

        private void HitEnemy(EnemyBrain enemy)
        {
            m_Consumed = true;

            if (m_PowerAmount > 0f)
            {
                if (m_OwnerTank is TankBrain_WaterB waterB)
                {
                    waterB.HandleProjectileDamage(enemy, m_PowerAmount);
                }
                else
                {
                    enemy.TakeDamage(m_PowerAmount);
                    TextDamageManager.Instance?.Show(m_PowerAmount, enemy.transform.position);
                }
            }

            Destroy(gameObject);
        }
    }
}