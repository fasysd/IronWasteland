using IronWasteland.Enemies;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class GreenFox_MagicTrap : MonoBehaviour
    {
        private float m_AttackPower;
        private float m_DamagePercent;
        private float m_EliteDamagePercent;
        private float m_RootDuration;
        private float m_TrapDuration;
        private bool m_Triggered;

        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
                col.isTrigger = true;
        }

        public void Initialize(float attackPower, float damagePercent, float eliteDamagePercent, float rootDuration, float trapDuration)
        {
            m_AttackPower = attackPower;
            m_DamagePercent = damagePercent;
            m_EliteDamagePercent = eliteDamagePercent;
            m_RootDuration = rootDuration;
            m_TrapDuration = trapDuration;
            m_Triggered = false;

            // Tự hủy bẫy sau 15 giây nếu không có kẻ địch nào dẫm phải
            Destroy(gameObject, m_TrapDuration);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (m_Triggered)
                return;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(other);
            if (targetRef == null)
                return;

            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy == null)
                return;

            m_Triggered = true;

            StartCoroutine(TrapTriggerRoutine(enemy));
        }

        private IEnumerator TrapTriggerRoutine(EnemyBrain enemy)
        {
            // Kiểm tra xem kẻ địch có phải là loại Tinh anh (Elite) hay không
            bool isElite = enemy is EliteEnemyBrain;

            // Nếu không phải Tinh anh, trói chân trong 2 giây (nếu hệ thống hỗ trợ)
            if (!isElite)
            {
                // enemy.SetRooted(true, m_RootDuration);
            }

            // Chờ 0.2 giây mô phỏng hiệu ứng hàm nanh sập xuống
            yield return new WaitForSeconds(0.2f);

            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                // Nếu là Tinh anh: dùng 230% ATK | Thường: dùng 150% ATK
                float activeDamagePercent = isElite ? m_EliteDamagePercent : m_DamagePercent;

                // Tính toán sát thương chuẩn: Tấn công của xe tăng * (% sát thương / 100)[cite: 1]
                float finalDamage = m_AttackPower * (activeDamagePercent / 100f);

                enemy.TakeDamage(finalDamage);
                TextDamageManager.Instance?.Show(finalDamage, transform.position);
            }

            Destroy(gameObject);
        }
    }
}