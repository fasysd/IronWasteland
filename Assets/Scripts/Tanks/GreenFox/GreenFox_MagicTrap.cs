using IronWasteland.Enemies;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class GreenFox_MagicTrap : MonoBehaviour
    {
        private float m_BaseDamage;
        private float m_DamagePercent;
        private float m_RootDuration;
        private bool m_Triggered;

        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
                col.isTrigger = true;
        }

        public void Initialize(float baseDamage, float damagePercent, float rootDuration)
        {
            m_BaseDamage = baseDamage;
            m_DamagePercent = damagePercent;
            m_RootDuration = rootDuration;
            m_Triggered = false;
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

            // Bắt đầu đếm ngược 2 giây trước khi gây sát thương
            StartCoroutine(DelayExplosionRoutine(enemy));
        }

        private IEnumerator DelayExplosionRoutine(EnemyBrain enemy)
        {
            // Trói chân ngay lập tức khi dẫm vào bẫy (nếu cần)
            // enemy.SetRooted(true);

            // Chờ đúng 2 giây
            yield return new WaitForSeconds(2f);

            // Kiểm tra xem enemy còn tồn tại trong game không trước khi gây sát thương
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                // Kiểm tra xem mục tiêu có đang bị thiêu đốt (1B) hay không để x2 sát thương
                GreenFox_BurnEffect burn = enemy.GetComponent<GreenFox_BurnEffect>();
                float multiplier = (burn != null && burn.IsBurning) ? 2.0f : 1.0f;

                float finalDamage = m_BaseDamage * m_DamagePercent * multiplier;
                enemy.TakeDamage(finalDamage);
                TextDamageManager.Instance?.Show(finalDamage, transform.position);
            }

            // Hủy bẫy sau khi nổ xong
            Destroy(gameObject);
        }
    }
}