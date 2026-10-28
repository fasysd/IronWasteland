using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    [DisallowMultipleComponent]
    public class Sign : MonoBehaviour
    {
        [Header("Sign Settings")]
        [SerializeField, Range(1, 20)] private int maxStacks = 10;
        [SerializeField] private int currentStacks = 1;

        [Header("Position Adjustment")]
        [SerializeField] private Vector3 centerOffset = Vector3.zero;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite normalSprite;   // Sprite mặc định
        [SerializeField] private Sprite buff1ASprite;   // Sprite khi bật chiêu 1A

        private Transform m_TargetEnemyTransform;
        private EnemyBrain m_OwnerEnemy;

        public int CurrentStacks => currentStacks;
        public int MaxStacks => maxStacks;
        public EnemyBrain OwnerEnemy => m_OwnerEnemy;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        public void SetTarget(EnemyBrain enemy)
        {
            if (enemy == null) return;

            m_OwnerEnemy = enemy;
            m_TargetEnemyTransform = enemy.transform;
            UpdatePosition();
        }

        // Tên hàm chuẩn để gọi từ TankBrain
        public void UpdateVisualState(bool is1AActive)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = is1AActive ? buff1ASprite : normalSprite;
            }
        }

        public void SetInitialStacks(int stacks)
        {
            currentStacks = Mathf.Clamp(stacks, 1, maxStacks);
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