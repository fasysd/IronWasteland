using UnityEngine;
using IronWasteland.Enemies;

namespace IronWasteland.Tanks.WaterB
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class WaterB_TurretProjectile : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 18f;
        [SerializeField] private float maxLifeTime = 3f;

        private ColliderOwnerRef m_OwnerRef;
        private Transform m_Target;
        private float m_Damage;
        private System.Action m_OnHitEnergyReturn;
        private System.Action<EnemyBrain, float> m_DamageHandler; // Water B xử lý sát thương (kèm Life Drain)
        private float m_SpawnTime;
        private bool m_Hit;

        private void Awake()
        {
            m_OwnerRef = GetComponent<ColliderOwnerRef>();
        }

        private void OnEnable()
        {
            if (m_OwnerRef == null) m_OwnerRef = GetComponent<ColliderOwnerRef>();
            if (m_OwnerRef != null) m_OwnerRef.TriggerEnter += OnOwnerRefTriggerEnter;
        }

        private void OnDisable()
        {
            if (m_OwnerRef != null) m_OwnerRef.TriggerEnter -= OnOwnerRefTriggerEnter;
        }

        public void Initialize(Transform target, float damage, System.Action onHitEnergyReturn,
                               System.Action<EnemyBrain, float> damageHandler = null)
        {
            m_Target = target;
            m_Damage = damage;
            m_OnHitEnergyReturn = onHitEnergyReturn;
            m_DamageHandler = damageHandler;
            m_SpawnTime = Time.time;
            m_Hit = false;

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
        }

        private void OnOwnerRefTriggerEnter(Collider2D collision)
        {
            if (m_Hit || collision == null) return;
            if (Time.time - m_SpawnTime < 0.05f) return;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collision);
            if (targetRef == null) return;

            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null)
            {
                HitTarget(enemy);
            }
        }

        private void HitTarget(EnemyBrain victim)
        {
            if (m_Hit || victim == null) return;
            m_Hit = true;

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

            // Hoàn trả năng lượng nước cho Water B
            m_OnHitEnergyReturn?.Invoke();
            Destroy(gameObject);
        }
    }
}
