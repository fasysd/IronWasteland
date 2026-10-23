using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    public class WaterB_WaterTurret : MonoBehaviour
    {
        [Header("Turret Settings")]
        [SerializeField] private float attackRange = 12f;
        [SerializeField] private float attackRate = 1f;
        [SerializeField] private float damagePerHit = 20f;
        [SerializeField] private float turretDuration = 8f;
        [SerializeField] private LayerMask enemyLayer;

        [Header("References")]
        [SerializeField] private WaterB_TurretProjectile projectilePrefab;

        private float m_FireTimer = 0f;
        private Transform m_CurrentTarget;
        private System.Action m_OnHitEnergyReturn;

        public void Initialize(System.Action onHitEnergyReturn)
        {
            m_OnHitEnergyReturn = onHitEnergyReturn;
            Destroy(gameObject, turretDuration);
        }

        private void Update()
        {
            FindTarget();

            if (m_CurrentTarget != null)
            {
                // Xoay hướng 2D về phía kẻ địch
                Vector3 targetDir = (m_CurrentTarget.position - transform.position);
                targetDir.z = 0f;

                if (targetDir != Vector3.zero)
                {
                    float angle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
                    Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 8f);
                }

                m_FireTimer += Time.deltaTime;
                if (m_FireTimer >= (1f / attackRate))
                {
                    m_FireTimer = 0f;
                    Shoot();
                }
            }
        }

        private void FindTarget()
        {
            // Quét mục tiêu kẻ địch trong bán kính 2D bằng Physics2D
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, attackRange, enemyLayer);
            float minDistance = float.MaxValue;
            Transform closestEnemy = null;

            foreach (var col in hitEnemies)
            {
                float dist = Vector2.Distance(transform.position, col.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestEnemy = col.transform;
                }
            }

            m_CurrentTarget = closestEnemy;
        }

        private void Shoot()
        {
            if (projectilePrefab == null || m_CurrentTarget == null) return;

            // Đạn xuất phát ngay tại TÂM TRỤ NƯỚC (Z = 0)
            Vector3 spawnPos = transform.position;
            spawnPos.z = 0f;

            WaterB_TurretProjectile projectile = Instantiate(projectilePrefab, spawnPos, transform.rotation);
            projectile.Initialize(m_CurrentTarget, damagePerHit, () =>
            {
                m_OnHitEnergyReturn?.Invoke();
            });
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}