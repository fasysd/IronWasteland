using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    [DisallowMultipleComponent]
    public class GreenFox_RootEffect : MonoBehaviour
    {
        [SerializeField] private Vector3 centerOffset = Vector3.zero;
        [SerializeField, Min(0.1f)] private float scaleMultiplier = 1f; // Tăng giảm kích thước hiệu ứng trực tiếp tại đây

        private Transform m_TargetEnemyTransform;

        public void SetTarget(EnemyBrain enemy, float duration)
        {
            if (enemy == null)
            {
                Destroy(gameObject);
                return;
            }

            m_TargetEnemyTransform = enemy.transform;

            // Cố định kích thước theo scale mặc định của prefab nhân với hệ số scaleMultiplier tùy chỉnh
            transform.localScale = Vector3.one * scaleMultiplier;

            UpdatePosition();
            Destroy(gameObject, duration);
        }

        private void LateUpdate()
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (m_TargetEnemyTransform != null)
            {
                // Bám chính xác vào tâm của kẻ địch (giống Sign)
                Vector3 targetCenter = m_TargetEnemyTransform.position;

                SpriteRenderer sr = m_TargetEnemyTransform.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    targetCenter = sr.bounds.center;
                }
                else
                {
                    Collider2D col = m_TargetEnemyTransform.GetComponentInChildren<Collider2D>();
                    if (col != null)
                    {
                        targetCenter = col.bounds.center;
                    }
                }

                transform.position = targetCenter + centerOffset;
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}