using UnityEngine;
using IronWasteland.Enemies;

namespace IronWasteland.Tanks.WaterB
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class WaterB_TurretProjectile : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 18f;
        [SerializeField] private float maxLifeTime = 3f;

        private Transform m_Target;
        private float m_Damage;
        private System.Action m_OnHitEnergyReturn;
        private float m_SpawnTime;

        public void Initialize(Transform target, float damage, System.Action onHitEnergyReturn)
        {
            m_Target = target;
            m_Damage = damage;
            m_OnHitEnergyReturn = onHitEnergyReturn;
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
            if (m_Target == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 targetPosition = m_Target.position;
            targetPosition.z = 0f;

            Vector3 direction = (targetPosition - transform.position).normalized;
            transform.position += direction * moveSpeed * Time.deltaTime;

            if (direction != Vector3.zero)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);
            }

            // Kiểm tra khoảng cách để chạm mục tiêu
            if (Vector3.Distance(transform.position, targetPosition) < 0.8f)
            {
                HitTarget();
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (Time.time - m_SpawnTime < 0.1f) return;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collision);
            if (targetRef == null) return;

            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null)
            {
                HitTarget(enemy);
            }
        }

        private void HitTarget(EnemyBrain enemy = null)
        {
            // Nếu va chạm trực tiếp với EnemyBrain qua trigger
            if (enemy != null)
            {
                enemy.TakeDamage(m_Damage);
                TextDamageManager.Instance?.Show(m_Damage, enemy.transform.position);
                Debug.Log($"[WaterTurret] Trụ nước bắn trúng kẻ địch {enemy.name} gây {m_Damage} sát thương!");
            }
            else if (m_Target != null)
            {
                // Hoặc nếu bay đến mục tiêu đang theo dõi
                EnemyBrain targetEnemy = m_Target.GetComponentInParent<EnemyBrain>();
                if (targetEnemy != null)
                {
                    targetEnemy.TakeDamage(m_Damage);
                    TextDamageManager.Instance?.Show(m_Damage, m_Target.position);
                    Debug.Log($"[WaterTurret] Trụ nước tiêu diệt mục tiêu gây {m_Damage} sát thương!");
                }
            }

            // Hoàn trả năng lượng nước cho Water B
            m_OnHitEnergyReturn?.Invoke();
            Destroy(gameObject);
        }
    }
}