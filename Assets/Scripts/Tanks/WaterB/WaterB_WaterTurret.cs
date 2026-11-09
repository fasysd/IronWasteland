using UnityEngine;
using IronWasteland.Enemies;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Trụ nước (Water Turrets, GDD): tồn tại 8s, mỗi 1s bắn kẻ địch gần nhất, sát thương = 50% ATK của Water B
    /// (tính sẵn khi tung chiêu). Mỗi phát trúng hồi 3 năng lượng cho Water B.
    /// Tìm mục tiêu xác thực qua ColliderOwnerRef.
    /// </summary>
    public class WaterB_WaterTurret : MonoBehaviour
    {
        [Header("Turret Settings")]
        [SerializeField] private float attackRange = 12f;
        [SerializeField] private LayerMask enemyLayer;

        [Header("References")]
        [SerializeField] private WaterB_TurretProjectile projectilePrefab;

        private float m_Damage;
        private float m_AttackInterval = 1f;
        private float m_FireTimer;
        private Transform m_CurrentTarget;
        private System.Action m_OnHitEnergyReturn;
        private System.Action<EnemyBrain, float> m_DamageHandler;

        public void Initialize(float damage, float lifetime, float attackInterval, System.Action onHitEnergyReturn,
                               System.Action<EnemyBrain, float> damageHandler = null)
        {
            m_DamageHandler = damageHandler;
            m_Damage = damage;
            m_AttackInterval = Mathf.Max(0.05f, attackInterval);
            m_FireTimer = m_AttackInterval; // có mục tiêu là bắn phát đầu ngay
            m_OnHitEnergyReturn = onHitEnergyReturn;

            Destroy(gameObject, lifetime > 0f ? lifetime : 8f);
        }

        private void Update()
        {
            FindTarget();

            if (m_CurrentTarget == null) return;

            Vector3 targetDir = m_CurrentTarget.position - transform.position;
            targetDir.z = 0f;

            if (targetDir != Vector3.zero)
            {
                float angle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
                Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 8f);
            }

            m_FireTimer += Time.deltaTime;
            if (m_FireTimer >= m_AttackInterval)
            {
                m_FireTimer = 0f;
                Shoot();
            }
        }

        private void FindTarget()
        {
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, attackRange, enemyLayer);
            float minDistance = float.MaxValue;
            Transform closest = null;

            foreach (var col in hitEnemies)
            {
                if (col == null) continue;
                ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
                if (targetRef == null) continue;

                EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
                if (enemy == null || !enemy.IsAlive) continue;

                float dist = Vector2.Distance(transform.position, enemy.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = enemy.transform;
                }
            }

            m_CurrentTarget = closest;
        }

        private void Shoot()
        {
            if (projectilePrefab == null || m_CurrentTarget == null) return;

            Vector3 spawnPos = transform.position;
            spawnPos.z = 0f;

            WaterB_TurretProjectile projectile = Instantiate(projectilePrefab, spawnPos, transform.rotation);
            projectile.Initialize(m_CurrentTarget, m_Damage, () => m_OnHitEnergyReturn?.Invoke(), m_DamageHandler);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
