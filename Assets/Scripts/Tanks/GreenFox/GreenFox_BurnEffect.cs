using System;
using System.Collections;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    /// <summary>
    /// Dùng cho 2 việc:
    /// 1) ApplyBurn: hiệu ứng thiêu đốt gắn trên kẻ địch (gây sát thương mỗi 0.5s trong 6s, đòn mới làm mới thời gian).
    /// 2) Initialize: vòng tròn hiển thị buff Burning Fox quanh xe tăng.
    /// </summary>
    [DisallowMultipleComponent]
    public class GreenFox_BurnEffect : MonoBehaviour
    {
        private bool m_IsBurning;
        private EnemyBrain m_OwnerEnemy;
        private Transform m_TargetTransform;

        private float m_TickDamage;
        private float m_Interval;
        private float m_EndTime;
        private Action<GreenFox_BurnEffect> m_OnEnded;

        public bool IsBurning => m_IsBurning;
        public EnemyBrain OwnerEnemy => m_OwnerEnemy;

        // --- DẤU ẤN CHÁY TRÊN KẺ ĐỊCH ---
        public void ApplyBurn(EnemyBrain enemy, float tickDamage, float duration, float interval, Action<GreenFox_BurnEffect> onEnded)
        {
            if (enemy == null)
            {
                Destroy(gameObject);
                return;
            }

            m_OwnerEnemy = enemy;
            m_TargetTransform = enemy.transform;
            m_Interval = Mathf.Max(0.05f, interval);
            m_OnEnded = onEnded;
            m_TickDamage = tickDamage;
            m_EndTime = Time.time + duration;

            StartCoroutine(BurnRoutine());
        }

        /// <summary>Đòn mới trúng kẻ địch đang cháy: làm mới thời gian và cập nhật sát thương mỗi nhịp.</summary>
        public void Refresh(float tickDamage, float duration)
        {
            m_TickDamage = tickDamage;
            m_EndTime = Time.time + duration;
        }

        // --- VÒNG TRÒN BUFF QUANH XE TANK ---
        public void Initialize(Transform target, float duration)
        {
            m_TargetTransform = target;
            Destroy(gameObject, duration);
        }

        private void LateUpdate()
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (m_TargetTransform != null)
            {
                Vector3 targetCenter = m_TargetTransform.position;

                SpriteRenderer spriteRenderer = m_TargetTransform.GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    targetCenter = spriteRenderer.bounds.center;
                }
                else
                {
                    Collider2D col = m_TargetTransform.GetComponentInChildren<Collider2D>();
                    if (col != null) targetCenter = col.bounds.center;
                }

                transform.position = targetCenter;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private IEnumerator BurnRoutine()
        {
            m_IsBurning = true;
            var wait = new WaitForSeconds(m_Interval);

            while (Time.time < m_EndTime)
            {
                yield return wait;

                if (m_OwnerEnemy == null) break;

                m_OwnerEnemy.TakeDamage(m_TickDamage);
                TextDamageManager.Instance?.Show(m_TickDamage, transform.position);
            }

            m_IsBurning = false;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            m_IsBurning = false;
            m_OnEnded?.Invoke(this);
            m_OnEnded = null;
        }
    }
}