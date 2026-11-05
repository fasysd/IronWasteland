using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Tinh Linh Nghia Dia (chuong 1): khong tan cong truc dien, cu di duoc 3m la
    /// de lai mot vet lua ban kinh 1.5m ton tai 5s, gay 100% Tan cong moi 1s va
    /// lam cham 20% toc do trong 2s.
    /// Theo tai lieu: HP 250 / ATK 35 / SPD 135.
    /// </summary>
    [DisallowMultipleComponent]
    public class GraveyardWispBrain : ChasingEnemyBrain
    {
        [Header("Quy dao")]
        [Tooltip("Ban kinh vong quanh tank (khong lao vao nhu cac quai khac).")]
        [SerializeField, Min(0.5f)] private float orbitRadius = 2.5f;

        [Tooltip("Ti le toc do khi bay vong quanh.")]
        [SerializeField, Range(0.1f, 1f)] private float orbitSpeedRatio = 0.85f;

        [Header("Vet lua")]
        [Tooltip("Cu di duoc bao nhieu met thi tha mot vet lua.")]
        [SerializeField, Min(0.5f)] private float trailSpacing = 3f;

        [SerializeField, Min(0.1f)] private float patchRadius = 1.5f;
        [SerializeField, Min(0.1f)] private float patchLifetime = 5f;

        [Tooltip("He so sat thuong moi 1s so voi Tan cong (1 = 100%).")]
        [SerializeField, Min(0f)] private float damageRatio = 1f;

        [SerializeField, Range(0f, 1f)] private float slowPercent = 0.2f;
        [SerializeField, Min(0f)] private float slowDuration = 2f;
        [SerializeField] private bool logTrail;

        private Vector2 m_LastPosition;
        private float m_Traveled;

        private float m_OrbitSign = 1f;
        private float m_OrbitTimer;

        protected override Vector2 ComputeDesiredVelocity(TankBrain target, float distance)
        {
            float speed = Mathf.Max(0f, Stats.MoveSpeed);
            Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;

            if (distance <= 0.0001f) return Vector2.zero;

            Vector2 forward = toTarget / distance;

            if (distance > orbitRadius)
            {
                // Tien lai nhung giam toc do khi da gan vong quy dao.
                float approachRatio = Mathf.Clamp01((distance - orbitRadius) / Mathf.Max(0.5f, orbitRadius));
                return forward * (speed * Mathf.Lerp(0.45f, 1f, approachRatio));
            }

            // Bay vong quanh tank, thinh thoang doi chieu de kho doan.
            m_OrbitTimer += Time.deltaTime;
            if (m_OrbitTimer >= 4f)
            {
                m_OrbitTimer = 0f;
                m_OrbitSign = -m_OrbitSign;
            }

            Vector2 tangent = new Vector2(-forward.y, forward.x) * m_OrbitSign;

            // Keo nhe ra khoi tam de khong bi hut vao sat tank.
            Vector2 outward = -forward * 0.35f;
            return (tangent + outward).normalized * (speed * orbitSpeedRatio);
        }

        protected override void OnBrainTick(float deltaTime)
        {
            base.OnBrainTick(deltaTime);

            Vector2 position = transform.position;
            m_Traveled += Vector2.Distance(position, m_LastPosition);
            m_LastPosition = position;

            if (Target == null) return;

            if (m_Traveled >= trailSpacing)
            {
                m_Traveled = 0f;
                DropTrail(position);
            }
        }

        private void DropTrail(Vector2 position)
        {
            float damage = Mathf.Max(0f, Stats.Attack * damageRatio);

            FlamePatch.Spawn(position, patchRadius, patchLifetime, damage, slowPercent, slowDuration);

            if (logTrail)
            {
                Debug.Log("[GraveyardWisp] Tha vet lua tai (" + position.x.ToString("0.0") + ", " + position.y.ToString("0.0") + "), " + damage.ToString("0") + " sat thuong/1s.");
            }
        }

        protected override void OnDeath()
        {
            Destroy(gameObject);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (orbitRadius < 0.5f) orbitRadius = 0.5f;
            if (trailSpacing < 0.5f) trailSpacing = 0.5f;
            if (patchRadius < 0.1f) patchRadius = 0.1f;
        }
    }
}
