using System;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk.VFX
{
    /// <summary>
    /// VFX Kỹ năng Tỏa Nhiệt Diện Rộng (ToaNhietDienRong):
    /// Sóng nhiệt áp suất cao bung tỏa dạng vòng tròn hình xuyến mở rộng từ tâm D1onk,
    /// kèm tia lửa đỏ cam, khói nhiệt cuộn trào và đẩy lùi xe tank.
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkToaNhietDienRongVFX : MonoBehaviour
    {
        [Header("Expansion Settings")]
        [Tooltip("Bán kính tối đa của vòng nhiệt (m).")]
        [SerializeField, Min(3f)] private float maxRadius = 9f;

        [Tooltip("Thời gian mở rộng sóng nhiệt từ 0 đến maxRadius (giây).")]
        [SerializeField, Min(0.2f)] private float expansionDuration = 1.0f;

        [Tooltip("Lực đẩy lùi đối với xe tank (m/s).")]
        [SerializeField, Min(0f)] private float knockbackForce = 6f;

        [Header("Components")]
        [SerializeField] private SpriteRenderer heatRingSprite;
        [SerializeField] private ParticleSystem emberParticles;

        private float m_Timer = 0f;
        private float m_CurrentRadius = 0f;
        private bool m_IsActive = false;
        private float m_Damage = 40f;
        private EnemyBrain m_Owner = null;
        private Action m_OnComplete = null;
        private readonly System.Collections.Generic.HashSet<TankBrain> m_DamagedTanks = new System.Collections.Generic.HashSet<TankBrain>();

        public float CurrentRadius => m_CurrentRadius;
        public float MaxRadius => maxRadius;
        public bool IsActive => m_IsActive;

        public void Initialize(float damage = 40f, EnemyBrain owner = null, Action onComplete = null)
        {
            m_Damage = damage;
            m_Owner = owner;
            m_OnComplete = onComplete;
            m_Timer = 0f;
            m_CurrentRadius = 0.5f;
            m_IsActive = true;
            m_DamagedTanks.Clear();

            if (heatRingSprite != null)
            {
                heatRingSprite.transform.localScale = Vector3.one * (m_CurrentRadius * 2f);
                Color c = heatRingSprite.color;
                c.a = 1f;
                heatRingSprite.color = c;
            }

            if (emberParticles != null)
            {
                emberParticles.Play();
            }
        }

        private void Update()
        {
            ManualTick(Time.deltaTime);
        }

        /// <summary>
        /// Tiến trình mở rộng sóng nhiệt. Hỗ trợ unit test.
        /// </summary>
        public void ManualTick(float dt)
        {
            if (!m_IsActive) return;

            m_Timer += dt;
            float progress = Mathf.Clamp01(m_Timer / expansionDuration);

            m_CurrentRadius = Mathf.Lerp(0.5f, maxRadius, Mathf.Sqrt(progress));

            if (heatRingSprite != null)
            {
                heatRingSprite.transform.localScale = Vector3.one * (m_CurrentRadius * 2f);
                Color c = heatRingSprite.color;
                c.a = Mathf.Lerp(1f, 0f, progress * progress);
                heatRingSprite.color = c;
            }

            // Quét và gây sát thương các tank trong phạm vi sóng nhiệt
            DamageAndKnockbackTanks(m_CurrentRadius);

            if (progress >= 1f)
            {
                m_IsActive = false;
                m_OnComplete?.Invoke();

                if (Application.isPlaying)
                    Destroy(gameObject, 0.4f);
                else
                    DestroyImmediate(gameObject);
            }
        }

        private void DamageAndKnockbackTanks(float radius)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
            foreach (var h in hits)
            {
                if (h == null || h.GetComponentInParent<EnemyBrain>() != null) continue;

                TankBrain tank = D1onkMissileProjectile.ResolveTankBrain(h);
                if (tank != null && tank.IsAlive && !m_DamagedTanks.Contains(tank))
                {
                    m_DamagedTanks.Add(tank);

                    if (m_Damage > 0f)
                    {
                        DamageInfo info = new DamageInfo(m_Owner, tank, m_Damage, 0f, tank.transform.position, sourceType: DamageSourceType.EnemyAttack);
                        tank.TakeDamage(info);
                    }

                    // Đẩy lùi tank ra xa tâm vụ nổ
                    if (knockbackForce > 0f && tank.TryGetComponent<Rigidbody2D>(out var rb) && rb != null)
                    {
                        Vector2 pushDir = ((Vector2)tank.transform.position - (Vector2)transform.position).normalized;
                        if (pushDir.sqrMagnitude < 0.01f) pushDir = Vector2.up;
                        rb.AddForce(pushDir * knockbackForce, ForceMode2D.Impulse);
                    }
                }
            }
        }
    }
}
