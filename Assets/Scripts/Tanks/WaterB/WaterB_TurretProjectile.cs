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
        private System.Action<EnemyBrain, float> m_DamageHandler; // Water B xử lý sát thương (kèm Life Drain)
        private float m_SpawnTime;
        private bool m_Hit; // chống trúng 2 lần (khoảng cách + trigger) trong cùng 1 frame

        public void Initialize(Transform target, float damage, System.Action onHitEnergyReturn,
                               System.Action<EnemyBrain, float> damageHandler = null)
        {
            m_Target = target;
            m_Damage = damage;
            m_OnHitEnergyReturn = onHitEnergyReturn;
            m_DamageHandler = damageHandler;
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
            if (m_Hit) return;

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
            if (m_Hit) return;
            m_Hit = true;

            // Va chạm trực tiếp qua trigger, hoặc bay tới mục tiêu đang theo dõi
            EnemyBrain victim = enemy != null
                ? enemy
                : (m_Target != null ? m_Target.GetComponentInParent<EnemyBrain>() : null);

            if (victim != null)
            {
                if (m_DamageHandler != null)
                {
                    m_DamageHandler(victim, m_Damage); // Water B: gây sát thương + Life Drain
                }
                else
                {
                    Vector3 pos = victim.transform.position;
                    victim.TakeDamage(m_Damage);
                    TextDamageManager.Instance?.Show(m_Damage, pos);
                }
                Debug.Log($"[WaterTurret] Trụ nước bắn trúng {victim.name} gây {m_Damage} sát thương!");
            }

            // Hoàn trả năng lượng nước cho Water B
            m_OnHitEnergyReturn?.Invoke();
            Destroy(gameObject);
        }
    }
}