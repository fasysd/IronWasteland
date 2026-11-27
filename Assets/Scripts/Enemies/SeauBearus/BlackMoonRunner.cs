using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Enemies.SeauBearus
{
    /// <summary>
    /// Mặt Trăng Đen (Black Moon Runner) — sinh ra trong giai đoạn Bùng nổ của SeauBearus.
    /// - Miễn nhiễm hoàn toàn sát thương đạn.
    /// - Vẫn chịu làm chậm / khống chế.
    /// - Bị chặn bởi tường đất (layer Terrain).
    /// - Chạm Boss: khóa Khiên Xanh đến hết giai đoạn Bùng nổ.
    /// </summary>
    [DisallowMultipleComponent]
    public class BlackMoonRunner : EnemyBrain
    {
        [Tooltip("Khoảng cách tới Boss được tính là đã chạm (m).")]
        [SerializeField, Min(0.05f)] private float reachDistance = 1.4f;

        [Tooltip("Độ nhạy đổi hướng di chuyển.")]
        [SerializeField, Min(0.01f)] private float turnSharpness = 8f;

        [Tooltip("Layer của tường đất để chặn Mặt Trăng Đen. Mặc định Terrain.")]
        [SerializeField] private LayerMask terrainMask;

        private SeauBearusBrain m_Boss;
        private float m_Lifetime;
        private float m_Age;
        private Rigidbody2D m_Body;
        private Vector2 m_DesiredVelocity;
        private float m_SlowPercent;
        private float m_SlowTimer;

        /// <summary>Khởi tạo Runner sau khi Instantiate từ SeauBearusBrain.</summary>
        public void InitializeRunner(SeauBearusBrain boss, float lifetime)
        {
            m_Boss = boss;
            m_Lifetime = Mathf.Max(0.1f, lifetime);
            m_Age = 0f;
            m_Body = GetComponent<Rigidbody2D>();
            if (m_Body != null)
            {
                m_Body.bodyType = RigidbodyType2D.Kinematic;
                m_Body.gravityScale = 0f;
            }

            if (terrainMask.value == 0)
            {
                terrainMask = LayerMask.GetMask("Terrain");
            }
        }

        protected override void OnBrainTick(float deltaTime)
        {
            m_Age += deltaTime;
            if (m_Age >= m_Lifetime || m_Boss == null || !m_Boss.IsAlive)
            {
                Destroy(gameObject);
                return;
            }

            if (m_SlowTimer > 0f)
            {
                m_SlowTimer -= deltaTime;
                if (m_SlowTimer <= 0f) m_SlowPercent = 0f;
            }

            Vector2 toBoss = (Vector2)m_Boss.transform.position - (Vector2)transform.position;
            float distance = toBoss.magnitude;

            if (distance <= reachDistance)
            {
                m_Boss.OnBlackMoonReached(this);
                Destroy(gameObject);
                return;
            }

            if (!CanMove)
            {
                m_DesiredVelocity = Vector2.zero;
                Model?.SetMoveSpeed(0f);
                return;
            }

            Vector2 direction = toBoss / Mathf.Max(0.0001f, distance);
            float speed = (this.Stats.MoveSpeed / 100f) * (1f - Mathf.Clamp01(m_SlowPercent));
            Vector2 targetVelocity = direction * speed;

            if (terrainMask.value != 0)
            {
                float castDistance = Mathf.Max(0.35f, speed * deltaTime + 0.4f);
                RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, castDistance, terrainMask);
                if (hit.collider != null)
                {
                    targetVelocity = Vector2.zero;
                }
            }

            float t = 1f - Mathf.Exp(-turnSharpness * deltaTime);
            m_DesiredVelocity = Vector2.Lerp(m_DesiredVelocity, targetVelocity, t);
            Model?.SetMoveSpeed(m_DesiredVelocity.magnitude);
        }

        private void FixedUpdate()
        {
            if (m_Body == null) return;

            if (m_Body.bodyType == RigidbodyType2D.Kinematic)
            {
                m_Body.MovePosition(m_Body.position + m_DesiredVelocity * Time.fixedDeltaTime);
            }
            else
            {
                m_Body.linearVelocity = m_DesiredVelocity;
            }
        }

        /// <summary>Ghi nhận làm chậm để giảm tốc độ di chuyển (base không tự áp dụng Slow).</summary>
        public override void ApplyCrowdControl(CrowdControlType type, float duration, float strength = 0f)
        {
            base.ApplyCrowdControl(type, duration, strength);

            if (type != CrowdControlType.Slow || duration <= 0f) return;

            float percent = Mathf.Clamp01(strength > 1f ? strength / 100f : strength);
            if (m_SlowTimer <= 0f || percent >= m_SlowPercent)
            {
                m_SlowPercent = Mathf.Max(m_SlowPercent, percent);
                m_SlowTimer = Mathf.Max(m_SlowTimer, duration);
            }
        }

        /// <summary>Miễn nhiễm sát thương đạn: chỉ phát hiệu ứng trúng đòn, không trừ máu.</summary>
        public override void TakeDamage(DamageInfo info)
        {
            Model?.PlayHit();
        }

        protected override void OnDeath()
        {
            Destroy(gameObject);
        }
    }
}
