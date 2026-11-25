using System.Collections;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.Hitboxes
{
    /// <summary>
    /// Hitbox càn quét khi Boss lướt Hỏa Tốc (HoaTocDashHitbox):
    /// - Gây 100% ATK (250 sát thương).
    /// - Đẩy dạt sang 2 bên 2m theo vector vuông góc của hướng lướt.
    /// </summary>
    [DisallowMultipleComponent]
    public class HoaTocDashHitbox : MonoBehaviour
    {
        [Header("Dash Hitbox Specs")]
        [Tooltip("Tỷ lệ sát thương theo ATK của Boss (1.0 = 100%).")]
        [SerializeField, Min(0f)] private float damageRatio = 1.0f;

        [Tooltip("Khoảng cách hất dạt 2 bên (m). Chuẩn GDD là 2m.")]
        [SerializeField, Min(0.1f)] private float pushDistance = 2.0f;

        [Tooltip("Thời gian hất dạt (giây).")]
        [SerializeField, Min(0.05f)] private float pushDuration = 0.2f;

        private readonly HashSet<TankBrain> m_DamagedTanks = new HashSet<TankBrain>();
        private EnemyBrain m_Owner;
        private float m_BossAttack;
        private Vector2 m_DashDirection;
        private bool m_Active;

        public float PushDistance => pushDistance;
        public float DamageRatio => damageRatio;
        public IReadOnlyCollection<TankBrain> DamagedTanks => m_DamagedTanks;

        public void Activate(EnemyBrain owner, float bossAttack, Vector2 dashDir)
        {
            m_Owner = owner;
            m_BossAttack = bossAttack;
            m_DashDirection = dashDir.normalized;
            m_DamagedTanks.Clear();
            m_Active = true;
        }

        public void Deactivate()
        {
            m_Active = false;
            m_DamagedTanks.Clear();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!m_Active) return;
            TankBrain tank = ResolveTank(other);
            if (tank != null && tank.IsAlive)
            {
                ApplyDashHit(tank);
            }
        }

        private void ApplyDashHit(TankBrain tank)
        {
            if (tank == null || !tank.IsAlive) return;
            if (!m_DamagedTanks.Add(tank)) return;

            float attackValue = m_BossAttack > 0f ? m_BossAttack : 250f;
            float finalDamage = attackValue * damageRatio;

            DamageInfo info = new DamageInfo(
                m_Owner,
                tank,
                finalDamage,
                0f,
                tank.transform.position,
                sourceType: DamageSourceType.EnemyAttack);

            tank.TakeDamage(info);

            // Xác định hướng đẩy dạt sang bên trái hoặc bên phải của đường lướt
            Vector2 perp = new Vector2(-m_DashDirection.y, m_DashDirection.x);
            Vector2 toTank = (Vector2)tank.transform.position - (Vector2)transform.position;
            // Chọn hướng vuông góc cùng phía với vị trí của Tank
            Vector2 sideDir = Vector2.Dot(toTank, perp) >= 0f ? perp : -perp;

            tank.ApplyCrowdControl(CrowdControlType.Knockback, pushDuration, pushDistance);
            if (tank.gameObject.activeInHierarchy)
            {
                tank.StartCoroutine(SmoothSidePushRoutine(tank, sideDir, pushDistance, pushDuration));
            }
        }

        private IEnumerator SmoothSidePushRoutine(TankBrain tank, Vector2 direction, float distance, float duration)
        {
            if (tank == null) yield break;
            Vector2 startPos = tank.transform.position;
            Vector2 targetPos = startPos + direction * distance;
            float elapsed = 0f;
            var rb = tank.GetComponent<Rigidbody2D>();

            while (elapsed < duration)
            {
                if (tank == null || !tank.IsAlive) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                Vector2 currentPos = Vector2.Lerp(startPos, targetPos, ease);
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.position = currentPos;
                }
                tank.transform.position = currentPos;
                yield return null;
            }

            if (tank != null)
            {
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.position = targetPos;
                }
                tank.transform.position = targetPos;
            }
        }

        private static TankBrain ResolveTank(Collider2D col)
        {
            if (col == null) return null;
            if (col.TryGetComponent<ColliderOwnerRef>(out var ownerRef) && ownerRef != null)
            {
                if (ownerRef.TryGetOwner<TankBrain>(out var directBrain) && directBrain != null)
                    return directBrain;
                if (ownerRef.Owner is TankBrain brain)
                    return brain;
                if (ownerRef.Owner is Component comp)
                {
                    var compBrain = comp.GetComponent<TankBrain>() ?? comp.GetComponentInParent<TankBrain>();
                    if (compBrain != null) return compBrain;
                }
                if (ownerRef.Owner is GameObject go)
                {
                    var goBrain = go.GetComponent<TankBrain>() ?? go.GetComponentInParent<TankBrain>();
                    if (goBrain != null) return goBrain;
                }
            }
            return col.GetComponentInParent<TankBrain>();
        }
    }
}
