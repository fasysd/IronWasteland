using UnityEngine;
using IronWasteland.Enemies;

namespace IronWasteland.Tanks.WaterB
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class WaterB_HealingProjectile : MonoBehaviour
    {
        [SerializeField] private float speed = 15f;
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
            if (Time.time - m_SpawnTime < 0.15f) return;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collision);
            if (targetRef == null) return;

            if (m_IsAssaultMode)
            {
                // CHẾ ĐỘ TẤN CÔNG (AQUA ASSAULT): Bắn trúng Kẻ Địch và gây sát thương
                EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
                if (enemy != null)
                {
                    ApplyDamageToEnemy(enemy);
                }
            }
            else
            {
                // CHẾ ĐỘ THƯỜNG: Hồi máu cho Xe Tăng (Bản thân hoặc Đồng minh)
                TankBrain targetTank = targetRef.GetOwner<TankBrain>();
                if (targetTank != null)
                {
                    ApplyHealingToTank(targetTank);
                }
            }
        }

        private void ApplyHealingToTank(TankBrain targetTank)
        {
            targetTank.Heal(m_PowerAmount);

            TextDamageManager.Instance?.Show(m_PowerAmount, targetTank.transform.position);
            Debug.Log($"[HealingProjectile] Hồi phục {m_PowerAmount} HP cho xe tăng {targetTank.name}");

            m_OnSuccess?.Invoke(m_PowerAmount);
            Destroy(gameObject);
        }

        private void ApplyDamageToEnemy(EnemyBrain enemy)
        {
            // 1. Gây sát thương lên kẻ địch
            enemy.TakeDamage(m_PowerAmount);
            TextDamageManager.Instance?.Show(m_PowerAmount, enemy.transform.position);
            Debug.Log($"[AquaAssault] Đã đánh trúng kẻ địch {enemy.name} gây {m_PowerAmount} sát thương!");

            // 2. Kiểm tra nếu chủ nhân là Water B
            if (m_OwnerTank is TankBrain_WaterB waterBTank)
            {
                float totalHeal = 0f;

                // Mặc định Aqua Assault khi đánh trúng tự hồi máu cơ bản (20% sát thương gây ra)
                float baseAssaultHeal = m_PowerAmount * 0.2f;
                totalHeal += baseAssaultHeal;

                // Nếu đang kích hoạt đồng thời chiêu phụ 1 (1B - LifeDrain), cộng thêm lượng hút máu từ LifeDrain
                if (waterBTank.IsLifeDrainActive)
                {
                    float lifeDrainHeal = m_PowerAmount * waterBTank.LifeDrainPercent;
                    totalHeal += lifeDrainHeal;
                    Debug.Log($"[LifeDrain + AquaAssault] Hút máu thành công! Hồi thêm {lifeDrainHeal} HP.");
                }

                // Thực hiện hồi tổng lượng máu cho xe tăng Water B
                if (totalHeal > 0f)
                {
                    waterBTank.Heal(totalHeal);
                    TextDamageManager.Instance?.Show(totalHeal, waterBTank.transform.position);
                }
            }

            m_OnSuccess?.Invoke(m_PowerAmount);
            Destroy(gameObject);
        }
    }
}