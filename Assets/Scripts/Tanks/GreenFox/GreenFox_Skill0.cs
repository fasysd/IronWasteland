using IronWasteland.Enemies;
using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace IronWasteland.Tanks.GreenFox
{
    /// <summary>
    /// Fox Fang (GDD): đạn ma thuật, sát thương 100% ATK. 25% trói chân 1s;
    /// Tinh anh không bị trói chân mà nhận thêm 120% ATK. Hồi năng lượng/dấu ấn/thiêu đốt do TankBrain_GreenFox xử lý.
    /// Va chạm qua trigger.
    /// </summary>
    [DisallowMultipleComponent]
    public class GreenFox_Skill0 : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 15f;
        [SerializeField, Min(0.05f)] private float lifeTime = 3f;

        [Header("Proc Settings (Trói chân / Sát thương thêm)")]
        [SerializeField, Range(0f, 100f)] private float procChancePercent = 25f;
        [SerializeField, Min(0.1f)] private float rootDuration = 1f;
        [SerializeField, Min(0f)] private float eliteBonusDamagePercent = 120f; // Tinh anh: +120% ATK thay cho trói chân
        [SerializeField] private GameObject rootEffectPrefab;

        private Action<GreenFox_Skill0> m_ReturnToPool;
        private float m_Damage;
        private TankBrain_GreenFox m_OwnerTank;
        private bool m_Consumed;

        /// <param name="damage">Sát thương cuối đã tính (100% ATK * (1 + tỉ lệ sát thương)).</param>
        public void Initialize(TankBrain_GreenFox owner, Vector2 spawnPos, Vector2 direction, float damage, Action<GreenFox_Skill0> returnToPool)
        {
            StopAllCoroutines();
            m_OwnerTank = owner;
            m_Damage = damage;
            m_ReturnToPool = returnToPool;
            m_Consumed = false;

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
                if (rb != null) rb.linearVelocity = direction * projectileSpeed;
                else transform.Translate(direction * projectileSpeed * Time.deltaTime, Space.World);
                yield return null;
            }

            ReturnSelf();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (m_Consumed || other == null || m_OwnerTank == null) return;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(other);
            if (targetRef == null) return;

            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy == null) return;

            m_Consumed = true;
            Vector3 hitPos = enemy.transform.position;

            // Sát thương cơ bản
            m_OwnerTank.DamageEnemy(enemy, m_Damage, hitPos);

            // 25%: thường bị trói chân, Tinh anh nhận thêm sát thương
            if (Random.Range(0f, 100f) < procChancePercent)
            {
                if (enemy is EliteEnemyBrain)
                {
                    float bonus = m_OwnerTank.ScaledDamage(eliteBonusDamagePercent / 100f);
                    m_OwnerTank.DamageEnemy(enemy, bonus, hitPos);
                }
                else if (m_OwnerTank.RootEnemy(enemy, rootDuration))
                {
                    SpawnRootEffect(enemy);
                }
            }

            // Hồi năng lượng, thiêu đốt (1B), cộng dồn dấu ấn
            m_OwnerTank.HandleSkill0Hit(enemy);

            ReturnSelf();
        }

        private void SpawnRootEffect(EnemyBrain enemy)
        {
            if (rootEffectPrefab == null) return;

            GameObject rootObj = Instantiate(rootEffectPrefab, enemy.transform.position, Quaternion.identity);
            GreenFox_RootEffect rootEffect = rootObj.GetComponent<GreenFox_RootEffect>();
            if (rootEffect == null) rootEffect = rootObj.AddComponent<GreenFox_RootEffect>();
            rootEffect.SetTarget(enemy, rootDuration);
        }

        private void ReturnSelf()
        {
            StopAllCoroutines();
            Action<GreenFox_Skill0> returnToPool = m_ReturnToPool;
            m_ReturnToPool = null;
            gameObject.SetActive(false);

            returnToPool?.Invoke(this);
        }

        public void Clear()
        {
            StopAllCoroutines();
            m_ReturnToPool = null;
        }
    }
}