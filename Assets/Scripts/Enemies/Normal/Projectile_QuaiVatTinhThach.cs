using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Đạn đuổi mục tiêu (Homing) của Quái Vật Thạch Anh.
    /// Bay theo hướng ban đầu, sau khoảng trễ ngắn sẽ liên tục bẻ lái bám đuổi theo xe tăng mục tiêu.
    /// Tự động biến mất khi chạm mục tiêu hoặc hết thời gian tồn tại.
    /// </summary>
    [DisallowMultipleComponent]
    public class Projectile_QuaiVatTinhThach : EnemyProjectileBase
    {
        [Header("Homing Settings")]
        [Tooltip("Tốc độ bẻ lái ban đầu đuổi theo mục tiêu (độ/giây).")]
        [SerializeField, Min(10f)] private float homingTurnSpeed = 270f;

        [Tooltip("Thời gian trễ bay thẳng trước khi bắt đầu bẻ lái (giây).")]
        [SerializeField, Min(0f)] private float homingDelay = 0.15f;

        [Tooltip("Thời gian hiệu lực truy đuổi tối đa (giây). Nếu <= 0 sẽ dùng lifetime của đạn. Càng tồn tại lâu, tốc độ xoay góc càng giảm dần về 0.")]
        [SerializeField, Min(0f)] private float homingDuration = 0f;

        private Transform m_HomingTarget;
        private int m_VolleyId;

        public int VolleyId { get => m_VolleyId; set => m_VolleyId = value; }
        public float HomingTurnSpeed { get => homingTurnSpeed; set => homingTurnSpeed = value; }
        public float HomingDelay { get => homingDelay; set => homingDelay = value; }
        public float HomingDuration { get => homingDuration; set => homingDuration = value; }
        public float CurrentTurnSpeed { get => m_CurrentTurnSpeed; private set => m_CurrentTurnSpeed = value; }

        private float m_CurrentTurnSpeed;

        public void SetHomingTarget(Transform target)
        {
            m_HomingTarget = target;
        }

        public void InitializeHoming(
            Vector2 direction,
            float damageAmount,
            Transform target,
            int volleyId,
            System.Action<EnemyProjectileBase, TankBrain> onTargetHit = null,
            System.Action<EnemyProjectileBase> onFinished = null)
        {
            Initialize(direction, damageAmount, onTargetHit, onFinished);
            m_HomingTarget = target;
            m_VolleyId = volleyId;
            m_CurrentTurnSpeed = homingTurnSpeed;
        }

        public override void Clear()
        {
            m_HomingTarget = null;
            m_VolleyId = 0;
            m_CurrentTurnSpeed = 0f;
            base.Clear();
        }

        protected override void FixedUpdate()
        {
            if (!m_Initialized || m_Body == null) return;

            // Sau thời gian trễ homingDelay, bắt đầu bẻ lái hướng về phía xe tăng.
            // Càng tồn tại lâu, tốc độ xoay góc càng giảm tuyến tính cho đến khi = 0.
            if (m_ElapsedTime >= homingDelay && m_HomingTarget != null)
            {
                float effectiveDuration = homingDuration > 0f ? homingDuration : lifetime;
                float durationWindow = Mathf.Max(0.001f, effectiveDuration - homingDelay);
                float progress = Mathf.Clamp01((m_ElapsedTime - homingDelay) / durationWindow);
                m_CurrentTurnSpeed = Mathf.Lerp(homingTurnSpeed, 0f, progress);

                if (m_CurrentTurnSpeed > 0f)
                {
                    Vector2 toTarget = ((Vector2)m_HomingTarget.position - (Vector2)transform.position);
                    if (toTarget.sqrMagnitude > 0.0001f)
                    {
                        float currentAngle = Mathf.Atan2(m_Direction.y, m_Direction.x) * Mathf.Rad2Deg;
                        float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, m_CurrentTurnSpeed * Time.fixedDeltaTime);
                        m_Direction = new Vector2(Mathf.Cos(newAngle * Mathf.Deg2Rad), Mathf.Sin(newAngle * Mathf.Deg2Rad)).normalized;
                        transform.right = m_Direction;
                    }
                }
            }
            else
            {
                m_CurrentTurnSpeed = homingTurnSpeed;
            }

            m_Body.linearVelocity = m_Direction * speed;
        }

        protected override void OnTanksHit(HashSet<TankBrain> tanks)
        {
            foreach (TankBrain tank in tanks)
            {
                if (tank == null || !tank.IsAlive) continue;

                // Gây sát thương và biến mất ngay lập tức khi trúng xe tăng
                NotifyTargetHit(tank);
                Despawn();
                return;
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            homingTurnSpeed = Mathf.Max(0f, homingTurnSpeed);
            homingDelay = Mathf.Max(0f, homingDelay);
            homingDuration = Mathf.Max(0f, homingDuration);
        }
    }
}
