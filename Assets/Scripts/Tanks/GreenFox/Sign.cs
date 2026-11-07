using System;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    /// <summary>
    /// Dấu ấn Fox Mark trên 1 kẻ địch: tồn tại 8s (làm mới khi đặt lại), cộng dồn tối đa 10.
    /// </summary>
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
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite buff1ASprite;

        private Transform m_TargetEnemyTransform;
        private EnemyBrain m_OwnerEnemy;
        private float m_ExpireTime;
        private bool m_HasLifetime;
        private bool m_Expiring;
        private Action<Sign> m_OnRemoved;

        public int CurrentStacks => currentStacks;
        public int MaxStacks => maxStacks;
        public bool IsFull => currentStacks >= maxStacks;
        public EnemyBrain OwnerEnemy => m_OwnerEnemy;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <param name="lifetime">Thời gian tồn tại ấn (GDD: 8s). &lt;= 0: không hết hạn.</param>
        /// <param name="onRemoved">Gọi khi ấn biến mất (hết hạn, nổ, kẻ địch chết) để TankBrain dọn danh sách.</param>
        public void SetTarget(EnemyBrain enemy, float lifetime, Action<Sign> onRemoved)
        {
            if (enemy == null) return;

            m_OwnerEnemy = enemy;
            m_TargetEnemyTransform = enemy.transform;
            m_OnRemoved = onRemoved;
            Refresh(lifetime);
            UpdatePosition();
        }

        /// <summary>Đặt lại thời gian tồn tại (khi đặt lại Fox Mark lên mục tiêu đã có ấn).</summary>
        public void Refresh(float lifetime)
        {
            m_HasLifetime = lifetime > 0f;
            m_ExpireTime = Time.time + lifetime;
        }

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

        public void AddStack(int amount = 1)
        {
            currentStacks = Mathf.Min(currentStacks + amount, maxStacks);
        }

        public void ResetStacks()
        {
            currentStacks = 1;
        }

        private void Update()
        {
            if (m_HasLifetime && !m_Expiring && Time.time >= m_ExpireTime)
            {
                m_Expiring = true;
                Destroy(gameObject);
            }
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
                    if (col != null) targetCenter = col.bounds.center;
                }

                transform.position = targetCenter + centerOffset;
            }
            else if (!m_Expiring)
            {
                m_Expiring = true;
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            m_OnRemoved?.Invoke(this);
            m_OnRemoved = null;
        }
    }
}