using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    [DisallowMultipleComponent]
    public class Sign : MonoBehaviour
    {
        [Header("Sign Settings")]
        [SerializeField, Range(1, 10)] private int maxStacks = 4;
        [SerializeField] private int currentStacks = 1;

        [Header("Position Adjustment")]
        [SerializeField] private Vector3 centerOffset = Vector3.zero; // Dùng để tinh chỉnh lệch tâm nếu cần (X, Y)

        private Transform m_TargetEnemyTransform;
        private EnemyBrain m_OwnerEnemy;

        public int CurrentStacks => currentStacks;
        public int MaxStacks => maxStacks;
        public EnemyBrain OwnerEnemy => m_OwnerEnemy;

        public void SetTarget(EnemyBrain enemy)
        {
            if (enemy == null) return;

            m_OwnerEnemy = enemy;
            m_TargetEnemyTransform = enemy.transform;
            UpdatePosition();
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

                // Lấy tâm chính xác của khối hình/sprite kẻ địch
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

                // Cộng thêm offset tùy chỉnh để căn chỉnh tuyệt đối vào giữa tâm
                transform.position = targetCenter + centerOffset;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void AddStack(int amount = 1)
        {
            currentStacks = Mathf.Min(currentStacks + amount, maxStacks);
            Debug.Log($"[Sign] Kẻ địch {m_TargetEnemyTransform?.name} hiện có {currentStacks}/{maxStacks} dấu ấn.");
        }

        public void ResetStacks()
        {
            currentStacks = 1;
        }
    }
}