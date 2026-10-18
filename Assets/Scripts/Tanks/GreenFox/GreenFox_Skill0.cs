using IronWasteland.Enemies;
using System;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    [DisallowMultipleComponent]
    public class GreenFox_Skill0 : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private GameObject projectilePrefab; // Prefab viên đạn thường (nếu có)
        [SerializeField, Min(0.1f)] private float projectileSpeed = 15f;
        [SerializeField, Min(0.05f)] private float lifeTime = 3f;

        private Action<GreenFox_Skill0> m_ReturnToPool;
        private float m_Damage;
        private TankBrain_GreenFox m_OwnerTank;

        public void Initialize(TankBrain_GreenFox owner, Vector2 spawnPos, Vector2 direction, float damage, Action<GreenFox_Skill0> returnToPool)
        {
            StopAllCoroutines();
            m_OwnerTank = owner;
            m_Damage = damage;
            m_ReturnToPool = returnToPool;

            transform.position = spawnPos;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            gameObject.SetActive(true);

            StartCoroutine(MoveAndLifeRoutine(direction));
        }

        private IEnumerator MoveAndLifeRoutine(Vector2 direction)
        {
            float elapsed = 0f;
            Rigidbody2D rb = GetComponent<Rigidbody2D>();

            while (elapsed < lifeTime)
            {
                elapsed += Time.deltaTime;
                if (rb != null)
                {
                    rb.linearVelocity = direction * projectileSpeed;
                }
                else
                {
                    transform.Translate(direction * projectileSpeed * Time.deltaTime, Space.World);
                }
                yield return null;
            }

            ReturnSelf();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null) return;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(other);
            if (targetRef == null) return;

            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null)
            {
                // Gây sát thương đòn đánh thường
                enemy.TakeDamage(m_Damage);
                TextDamageManager.Instance?.Show(m_Damage, enemy.transform.position);

                // Nếu bật 1B, đòn đánh thường cũng sẽ kích hoạt hiệu ứng cháy thông qua Tank chủ nhân
                if (m_OwnerTank != null)
                {
                    m_OwnerTank.TryApplyBurn(enemy, m_Damage);
                }

                ReturnSelf();
            }
        }

        private void ReturnSelf()
        {
            StopAllCoroutines();
            Action<GreenFox_Skill0> returnToPool = m_ReturnToPool;
            m_ReturnToPool = null;
            gameObject.SetActive(false);

            if (returnToPool != null)
            {
                returnToPool(this);
            }
        }

        public void Clear()
        {
            StopAllCoroutines();
            m_ReturnToPool = null;
        }
    }
}