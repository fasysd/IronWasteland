using IronWasteland.Enemies;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public class GreenFox_NineTailedAssault : MonoBehaviour
    {
        private CircleCollider2D m_CircleCollider;
        private TankBrain_GreenFox m_OwnerTank;
        private Transform m_TankTransform;

        private float m_Duration;
        private float m_Interval;
        private float m_TailDamage;

        // Lưu danh sách các kẻ địch đang đứng trong vùng vòng tròn tấn công của Cửu Vĩ
        private readonly HashSet<EnemyBrain> m_EnemiesInAssaultRange = new HashSet<EnemyBrain>();

        private void Awake()
        {
            m_CircleCollider = GetComponent<CircleCollider2D>();
            if (m_CircleCollider != null)
            {
                m_CircleCollider.isTrigger = true;
            }
        }

        public void Initialize(TankBrain_GreenFox owner, Transform tankTransform, float duration, float interval, float tailDamage)
        {
            m_OwnerTank = owner;
            m_TankTransform = tankTransform;
            m_Duration = duration;
            m_Interval = interval;
            m_TailDamage = tailDamage;

            // Đặt vị trí ban đầu trùng với Tank và gán làm con để di chuyển theo Tank
            if (m_TankTransform != null)
            {
                transform.position = m_TankTransform.position;
                transform.SetParent(m_TankTransform);
            }

            StartCoroutine(AssaultRoutine());
        }

        private IEnumerator AssaultRoutine()
        {
            float elapsed = 0f;
            Debug.Log("[NineTailedAssault] Kích hoạt vòng tròn Cửu Vĩ bám theo Tank!");

            while (elapsed < m_Duration)
            {
                yield return new WaitForSeconds(m_Interval);
                elapsed += m_Interval;

                // Dọn dẹp các đối tượng quái đã chết hoặc bị hủy
                m_EnemiesInAssaultRange.RemoveWhere(enemy => enemy == null);

                // Chuyển sang danh sách tạm để tránh lỗi "Collection was modified"
                List<EnemyBrain> targetsToAttack = new List<EnemyBrain>(m_EnemiesInAssaultRange);

                foreach (EnemyBrain enemy in targetsToAttack)
                {
                    if (enemy == null) continue;

                    // Kiểm tra nếu mục tiêu đang dính hiệu ứng thiêu đốt -> Gấp đôi sát thương
                    GreenFox_BurnEffect burn = enemy.GetComponent<GreenFox_BurnEffect>();
                    float multiplier = (burn != null && burn.IsBurning) ? 2.0f : 1.0f;

                    float damage = m_TailDamage * multiplier;
                    enemy.TakeDamage(damage);
                    TextDamageManager.Instance?.Show(damage, enemy.transform.position);

                    Debug.Log($"[NineTailedAssault] Đuôi ma thuật tấn công {enemy.name} gây {damage} sát thương (Multiplier: {multiplier}x)");

                    // Kích hoạt hiệu ứng cháy nếu 1B đang bật trên Tank chủ nhân
                    if (m_OwnerTank != null)
                    {
                        m_OwnerTank.TryApplyBurn(enemy, damage);
                    }
                }
            }

            Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null) return;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(other);
            if (targetRef == null) return;

            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null)
            {
                m_EnemiesInAssaultRange.Add(enemy);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == null) return;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(other);
            if (targetRef == null) return;

            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null)
            {
                m_EnemiesInAssaultRange.Remove(enemy);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (m_CircleCollider == null)
                m_CircleCollider = GetComponent<CircleCollider2D>();

            if (m_CircleCollider != null)
            {
                Gizmos.color = Color.magenta;
                float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
                Gizmos.DrawWireSphere(transform.position, m_CircleCollider.radius * maxScale);
            }
        }
    }
}