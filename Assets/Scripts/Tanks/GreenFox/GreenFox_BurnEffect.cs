using System;
using System.Collections;
using IronWasteland.Core;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    /// <summary>
    /// Dùng cho 2 việc:
    /// 1) ApplyBurn: hiệu ứng thiêu đốt gắn trên kẻ địch (gây sát thương mỗi 0.5s trong 6s, đòn mới làm mới thời gian).
    ///    Mỗi nhịp sát thương đi qua TankBrain_GreenFox.DamageEnemy nên cũng CỘNG DỒN DẤU ẤN
    ///    (và tính hạ gục cho Mark Hunter, hồi năng lượng Fox Territory như mọi sát thương khác của Green Fox).
    /// 2) Initialize: vòng tròn hiển thị buff Burning Fox quanh xe tăng.
    /// </summary>
    [DisallowMultipleComponent]
    public class GreenFox_BurnEffect : MonoBehaviour
    {
        private bool m_IsBurning;
        private bool m_VisualOnly;
        private TankBrain_GreenFox m_Owner;
        private EnemyBrain m_OwnerEnemy;
        private Transform m_TargetTransform;

        private float m_TickDamage;
        private float m_Interval;
        private float m_EndTime;
        private Action<GreenFox_BurnEffect> m_OnEnded;

        public bool IsBurning => m_IsBurning;
        public bool IsVisualOnly => m_VisualOnly;
        public EnemyBrain OwnerEnemy => m_OwnerEnemy;

        // --- DẤU ẤN CHÁY TRÊN KẺ ĐỊCH ---
        public void ApplyBurn(TankBrain_GreenFox owner, EnemyBrain enemy, float tickDamage, float duration, float interval, Action<GreenFox_BurnEffect> onEnded)
        {
            if (enemy == null)
            {
                Destroy(gameObject);
                return;
            }

            m_Owner = owner;
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

        /// <summary>
        /// Client: chỉ chạy hiệu ứng hình ảnh của vết cháy theo tín hiệu host, KHÔNG gây sát thương.
        /// Tự huỷ khi hết thời gian hoặc khi quái chết/despawn.
        /// </summary>
        public void InitializeVisualOnly(EnemyBrain enemy, float duration)
        {
            m_VisualOnly = true;
            m_IsBurning = true;
            m_Owner = null;
            m_OwnerEnemy = enemy;
            m_TargetTransform = enemy != null ? enemy.transform : null;
            m_EndTime = Time.time + Mathf.Max(0.05f, duration);

            if (m_TargetTransform == null)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>Client: host báo hiệu ứng được làm mới (đòn mới trúng) — chỉ gia hạn hiển thị.</summary>
        public void RefreshVisualOnly(EnemyBrain enemy, float duration)
        {
            if (!m_VisualOnly) return;

            if (enemy != null)
            {
                m_OwnerEnemy = enemy;
                m_TargetTransform = enemy.transform;
            }

            m_EndTime = Time.time + Mathf.Max(0.05f, duration);
            m_IsBurning = true;
        }

        // --- VÒNG TRÒN BUFF QUANH XE TANK ---
        public void Initialize(Transform target, float duration)
        {
            m_TargetTransform = target;
            Destroy(gameObject, duration);
        }

        private void LateUpdate()
        {
            if (m_VisualOnly)
            {
                if (m_TargetTransform == null || m_OwnerEnemy == null || !m_OwnerEnemy.IsAlive || Time.time >= m_EndTime)
                {
                    m_IsBurning = false;
                    Destroy(gameObject);
                    return;
                }
            }

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

                if (m_OwnerEnemy == null || !m_OwnerEnemy.IsAlive) break;

                if (m_Owner != null)
                {
                    // Qua DamageEnemy: hiện số, cộng dồn ấn, tính hạ gục (1A), hồi năng lượng Fox Territory
                    m_Owner.DamageEnemy(m_OwnerEnemy, m_TickDamage, transform.position, sourceType: DamageSourceType.DamageOverTime);
                }
                else
                {
                    m_OwnerEnemy.TakeDamage(m_TickDamage);
                    CombatFeedback.Show(m_TickDamage, transform.position);
                }
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