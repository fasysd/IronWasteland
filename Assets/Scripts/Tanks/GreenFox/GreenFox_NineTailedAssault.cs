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
            m_TailDamage = tailDamage; // Giá trị 15% ATK truyền vào

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

                m_EnemiesInAssaultRange.RemoveWhere(enemy => enemy == null);
                List<EnemyBrain> targetsToAttack = new List<EnemyBrain>(m_EnemiesInAssaultRange);

                foreach (EnemyBrain enemy in targetsToAttack)
                {
                    if (enemy == null) continue;

                    // Nếu mục tiêu đang dính hiệu ứng thiêu đốt (1B) thì x2 sát thương đuôi
                    GreenFox_BurnEffect burn = enemy.GetComponent<GreenFox_BurnEffect>();
                    float multiplier = (burn != null && burn.IsBurning) ? 2.0f : 1.0f;

                    float damage = m_TailDamage * multiplier;
                    enemy.TakeDamage(damage);
                    TextDamageManager.Instance?.Show(damage, enemy.transform.position);

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
    }
}