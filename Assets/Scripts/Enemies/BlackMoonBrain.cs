using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Mat trang den (Chuong 1): lao vao tan cong tam gan va phat xung buc xa
    /// quanh ban than, gay sat thuong dien moi 0.5s trong ban kinh 2.5m.
    ///
    /// Chi so theo tai lieu thiet ke: HP 260 / ATK 15 / Giap 50 / SPD 95.
    /// </summary>
    public class BlackMoonBrain : ChasingEnemyBrain
    {
        [Header("Xung buc xa")]
        [Tooltip("Ban kinh xung (met).")]
        [SerializeField, Min(0f)] private float auraRadius = 2.5f;

        [Tooltip("Chu ky gay sat thuong cua xung (giay).")]
        [SerializeField, Min(0.01f)] private float auraInterval = 0.5f;

        [Tooltip("He so sat thuong moi nhip so voi Attack (1 = 100% ATK).")]
        [SerializeField, Range(0f, 5f)] private float auraAttackRatio = 1f;

        [Header("Debug")]
        [SerializeField] private bool logAura = false;

        private float m_AuraTimer;

        protected override void OnBrainTick(float deltaTime)
        {
            base.OnBrainTick(deltaTime);

            m_AuraTimer -= deltaTime;
            if (m_AuraTimer > 0f) return;

            m_AuraTimer = auraInterval;

            if (DistanceToTarget > auraRadius) return;

            float damage = Stats != null ? Stats.Attack * auraAttackRatio : 0f;
            int hits = DamageTanksInRadius(transform.position, auraRadius, damage);

            if (logAura && hits > 0)
            {
                Debug.Log($"[BlackMoon] Xung buc xa: {damage:0} sat thuong, trung {hits} tank.");
            }
        }

        protected override void OnDeath()
        {
            Destroy(gameObject);
        }
    }
}
