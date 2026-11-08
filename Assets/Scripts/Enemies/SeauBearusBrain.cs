using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Boss 2 cua man 1-4 - "SeauBearus" (tai lieu thiet ke, TABLE 9):
    /// HP 1800 / ATK 180 / Giap 40 / SPD 85.
    /// - Bi dong: moi 10s gay sat thuong bang 10% HP cua muc tieu va hoi mau cho ban than.
    /// - Phun lua: ngon lua hinh quat ve phia truoc (100% Tan cong) va de lai vet lua
    ///   ton tai 8s, gay 20% Tan cong moi 0.5s + lam cham 20%.
    /// - Gam thet: lam cham 30% toc do cac tank trong ban kinh.
    /// </summary>
    [DisallowMultipleComponent]
    public class SeauBearusBrain : ChasingEnemyBrain
    {
        [Header("Phun lua")]
        [Tooltip("Hoi chieu phun lua (giay).")]
        [SerializeField, Min(1f)] private float flameCooldown = 5f;

        [Tooltip("Tam phun lua (m).")]
        [SerializeField, Min(1f)] private float flameRange = 9f;

        [Tooltip("Nua goc cua hinh quat (do).")]
        [SerializeField, Range(5f, 90f)] private float flameHalfAngle = 32f;

        [Tooltip("Ty le sat thuong cua don phun lua so voi Tan cong.")]
        [SerializeField, Min(0f)] private float flameDamageRatio = 1f;

        [Tooltip("Ty le sat thuong moi tick cua vet lua so voi Tan cong.")]
        [SerializeField, Min(0f)] private float trailDamageRatio = 0.2f;

        [Tooltip("So vet lua de lai tren duong phun.")]
        [SerializeField, Range(1, 6)] private int trailPatchCount = 3;

        [Header("Gam thet")]
        [Tooltip("Hoi chieu gam thet (giay).")]
        [SerializeField, Min(1f)] private float roarCooldown = 9f;

        [Tooltip("Ban kinh gam thet (m).")]
        [SerializeField, Min(1f)] private float roarRadius = 10f;

        [Tooltip("Muc lam cham cua tieng gam (0..1).")]
        [SerializeField, Range(0f, 1f)] private float roarSlowPercent = 0.3f;

        [Tooltip("Thoi gian lam cham (giay).")]
        [SerializeField, Min(0f)] private float roarSlowDuration = 3f;

        [Header("Hut mau")]
        [Tooltip("Chu ky hut mau (giay).")]
        [SerializeField, Min(1f)] private float lifestealInterval = 10f;

        [Tooltip("Ty le HP cua muc tieu bi hut moi lan (0..1).")]
        [SerializeField, Range(0f, 1f)] private float lifestealPercent = 0.1f;

        private float m_FlameTimer;
        private float m_RoarTimer;
        private float m_LifestealTimer;

        protected override void OnBrainTick(float deltaTime)
        {
            base.OnBrainTick(deltaTime);

            TankBrain target = Target;
            if (target == null) return;

            float distance = DistanceToTarget;

            m_LifestealTimer += deltaTime;
            if (m_LifestealTimer >= lifestealInterval)
            {
                m_LifestealTimer = 0f;
                Lifesteal(target);
            }

            m_FlameTimer += deltaTime;
            if (m_FlameTimer >= flameCooldown && distance <= flameRange)
            {
                m_FlameTimer = 0f;
                BreatheFire();
            }

            m_RoarTimer += deltaTime;
            if (m_RoarTimer >= roarCooldown && distance <= roarRadius)
            {
                m_RoarTimer = 0f;
                Roar();
            }
        }

        private void Lifesteal(TankBrain target)
        {
            float damage = target.Stats.MaxHealth * lifestealPercent;
            if (damage <= 0f) return;

            target.TakeDamage(damage);
            Heal(damage);
            TextDamageManager.Instance?.Show(damage, target.transform.position);
            Debug.Log("[SeauBearus] hut mau " + damage.ToString("0.0") + " -> HP " + CurrentHealth.ToString("0.0"));
        }

        private void BreatheFire()
        {
            Vector2 origin = transform.position;
            Vector2 forward = DirectionToTarget;
            float damage = Stats.Attack * flameDamageRatio;

            // Chi tinh tren than tank (transform goc) de khong phu thuoc vao thu tu
            // cac collider con (tank co 3 collider skill rong, trigger).
            TankBrain[] tanks = FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            HashSet<TankBrain> damaged = new HashSet<TankBrain>();

            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain tank = tanks[i];
                if (tank == null || !tank.IsAlive) continue;

                Vector2 toTank = (Vector2)tank.transform.position - origin;
                if (toTank.magnitude > flameRange) continue;

                // Sat qua gan thi luon trung (goc khong con y nghia).
                bool inCone = toTank.magnitude <= 1.5f || Vector2.Angle(forward, toTank) <= flameHalfAngle;
                if (!inCone) continue;
                if (!damaged.Add(tank)) continue;

                tank.TakeDamage(damage);
                TextDamageManager.Instance?.Show(damage, tank.transform.position);
            }

            for (int i = 0; i < trailPatchCount; i++)
            {
                float ratio = (i + 1f) / trailPatchCount;
                Vector2 patchPosition = origin + forward * (flameRange * ratio);
                FlamePatch.Spawn(patchPosition, 1.6f, 8f, Stats.Attack * trailDamageRatio, 0.2f, 2f);
            }

            Debug.Log("[SeauBearus] phun lua: " + damage.ToString("0.0") + " dmg, trung " + damaged.Count + " tank, " + trailPatchCount + " vet lua");
        }

        private void Roar()
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, roarRadius);
            HashSet<TankBrain> affected = new HashSet<TankBrain>();

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null) continue;

                TankBrain tank = collider.GetComponentInParent<TankBrain>();
                if (tank == null || !tank.IsAlive) continue;
                if (!affected.Add(tank)) continue;

                TankSlowDebuff.Apply(tank, roarSlowPercent, roarSlowDuration);
            }

            Debug.Log("[SeauBearus] gam thet: lam cham " + affected.Count + " tank " + (roarSlowPercent * 100f).ToString("0") + "% trong " + roarSlowDuration.ToString("0.0") + "s");
        }

        protected override void OnDeath()
        {
            Destroy(gameObject);
        }
    }
}
