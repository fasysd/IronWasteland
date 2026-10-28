using IronWasteland.Enemies;
using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace IronWasteland.Tanks.GreenFox
{
    [DisallowMultipleComponent]
    public class GreenFox_Skill0 : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 15f;
        [SerializeField, Min(0.05f)] private float lifeTime = 3f;

        [Header("Root Effect Settings")]
        [SerializeField] private GameObject rootEffectPrefab;

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
                float baseAtk = m_OwnerTank != null ? m_OwnerTank.Stats.Attack : 10f;
                float finalDamage = baseAtk * 1.0f;

                bool isElite = enemy.CompareTag("Elite") || enemy.name.Contains("Elite");

                if (isElite)
                {
                    finalDamage = baseAtk * 1.2f;
                    enemy.TakeDamage(finalDamage);
                    TextDamageManager.Instance?.Show(finalDamage, enemy.transform.position);
                }
                else
                {
                    enemy.TakeDamage(finalDamage);
                    TextDamageManager.Instance?.Show(finalDamage, enemy.transform.position);

                    float roll = Random.Range(0f, 100f);
                    if (roll <= 25f)
                    {
                        if (m_OwnerTank != null)
                        {
                            m_OwnerTank.StartCoroutine(ApplyRootEffectRoutine(enemy, 1f));
                        }
                        else
                        {
                            enemy.StartCoroutine(ApplyRootEffectRoutine(enemy, 1f));
                        }

                        if (rootEffectPrefab != null)
                        {
                            GameObject rootObj = Instantiate(rootEffectPrefab, enemy.transform.position, Quaternion.identity);
                            GreenFox_RootEffect rootEffect = rootObj.GetComponent<GreenFox_RootEffect>();
                            if (rootEffect == null)
                            {
                                rootEffect = rootObj.AddComponent<GreenFox_RootEffect>();
                            }
                            rootEffect.SetTarget(enemy, 1f);
                        }
                    }
                }

                // Gọi hàm xử lý trúng đích chung (hồi năng lượng, kiểm tra điều kiện hồi thêm năng lượng từ mục tiêu bị đốt, kích hoạt đốt 1B)
                if (m_OwnerTank != null)
                {
                    m_OwnerTank.HandleSkill0Hit(enemy, finalDamage);
                }

                ReturnSelf();
                return;
            }
        }

        private static IEnumerator ApplyRootEffectRoutine(EnemyBrain enemy, float duration)
        {
            if (enemy == null) yield break;

            float originalSpeed = enemy.Stats.MoveSpeed;
            enemy.Stats.MoveSpeed = 0f;

            yield return new WaitForSeconds(duration);

            if (enemy != null)
            {
                enemy.Stats.MoveSpeed = originalSpeed;
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