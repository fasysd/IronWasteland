using IronWasteland.Enemies;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    [DisallowMultipleComponent]
    public class GreenFox_BurnEffect : MonoBehaviour
    {
        private bool m_IsBurning;
        private EnemyBrain m_OwnerEnemy;
        private Transform m_TargetEnemyTransform;

        public bool IsBurning => m_IsBurning;
        public EnemyBrain OwnerEnemy => m_OwnerEnemy;

        // --- HÀM KHỞI TẠO CHO DẤU ẤN CHÁY TRÊN KẺ ĐỊCH ---
        public void ApplyBurn(EnemyBrain enemy, float baseDamage, float duration, float interval, float damagePercent)
        {
            if (m_IsBurning || enemy == null)
                return;

            m_OwnerEnemy = enemy;
            m_TargetEnemyTransform = enemy.transform;

            StartCoroutine(BurnRoutine(enemy, baseDamage, duration, interval, damagePercent));
        }

        // --- HÀM KHỞI TẠO CHO VÒNG TRÒN BUFF QUANH XE TANK ---
        public void Initialize(Transform target, float duration)
        {
            m_TargetEnemyTransform = target;
            Destroy(gameObject, duration); // Tự động xóa Prefab vòng tròn sau khi hết thời gian duration
        }

        private void LateUpdate()
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (m_TargetEnemyTransform != null)
            {
                Vector3 targetCenter = m_TargetEnemyTransform.position;

                SpriteRenderer spriteRenderer = m_TargetEnemyTransform.GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    targetCenter = spriteRenderer.bounds.center;
                }
                else
                {
                    Collider2D col = m_TargetEnemyTransform.GetComponentInChildren<Collider2D>();
                    if (col != null)
                    {
                        targetCenter = col.bounds.center;
                    }
                }

                transform.position = targetCenter;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private IEnumerator BurnRoutine(EnemyBrain enemy, float baseDamage, float duration, float interval, float damagePercent)
        {
            m_IsBurning = true;
            float elapsed = 0f;
            float tickDamage = baseDamage * damagePercent;

            while (elapsed < duration)
            {
                yield return new WaitForSeconds(interval);
                elapsed += interval;

                if (m_OwnerEnemy != null)
                {
                    m_OwnerEnemy.TakeDamage(tickDamage);
                    TextDamageManager.Instance?.Show(tickDamage, transform.position);
                }
                else
                {
                    break;
                }
            }

            m_IsBurning = false;
            Destroy(gameObject);
        }
    }
}