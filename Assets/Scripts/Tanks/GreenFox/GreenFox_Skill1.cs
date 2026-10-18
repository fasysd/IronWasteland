using IronWasteland.Enemies;
using System;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    [DisallowMultipleComponent]
    public class GreenFox_Skill1 : MonoBehaviour
    {
        [Header("Targeting Settings")]
        [SerializeField, Min(0.1f)] private float castRadius = 1.5f;
        [SerializeField, Min(0.05f)] private float displayDuration = 0.2f;

        private Action<GreenFox_Skill1> m_ReturnToPool;

        public void Initialize(Vector2 targetPosition, Action<EnemyBrain> onEnemyFound, Action<GreenFox_Skill1> returnToPool)
        {
            StopAllCoroutines();
            m_ReturnToPool = returnToPool;

            transform.position = targetPosition;
            gameObject.SetActive(true);

            // Quét kẻ địch
            Collider2D[] colliders = Physics2D.OverlapCircleAll(targetPosition, castRadius);
            EnemyBrain targetEnemy = null;

            foreach (Collider2D col in colliders)
            {
                if (col == null) continue;

                ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
                if (targetRef == null) continue;

                EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
                if (enemy != null)
                {
                    targetEnemy = enemy;
                    break;
                }
            }

            if (targetEnemy != null)
            {
                onEnemyFound?.Invoke(targetEnemy);
                Debug.Log($"[GreenFox_Skill1] Đã đặt dấu ấn lên: {targetEnemy.name}");
            }
            else
            {
                Debug.Log("[GreenFox_Skill1] Đặt dấu ấn bị trượt!");
            }

            StartCoroutine(WaitAndFinishRoutine());
        }

        private IEnumerator WaitAndFinishRoutine()
        {
            yield return new WaitForSeconds(displayDuration);

            Action<GreenFox_Skill1> returnToPool = m_ReturnToPool;
            m_ReturnToPool = null;

            if (returnToPool != null)
                returnToPool(this);
            else
                gameObject.SetActive(false);
        }

        public void Clear()
        {
            StopAllCoroutines();
            m_ReturnToPool = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, castRadius);
        }
    }
}