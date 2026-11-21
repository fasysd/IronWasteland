using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.The2Face
{
    /// <summary>
    /// Viên đá hội tụ về Boss do HutVaDa của The2Face bắn ra.
    /// Gây sát thương và choáng cho Tank trên đường bay.
    /// </summary>
    public class The2FaceRock : MonoBehaviour
    {
        [Tooltip("Layer của Tank để quét va chạm.")]
        [SerializeField] private LayerMask tankLayer;

        [Tooltip("Bán kính va chạm với Tank (mét).")]
        [SerializeField, Min(0.1f)] private float hitRadius = 1.1f;

        [Tooltip("Tốc độ xoay hình ảnh (độ/giây).")]
        [SerializeField] private float rotateSpeed = 540f;

        private EnemyBrain m_Owner;
        private Vector2 m_Target;
        private float m_Damage;
        private float m_StunDuration;
        private float m_Speed;
        private readonly HashSet<TankBrain> m_HitTanks = new HashSet<TankBrain>();

        /// <summary>Khởi tạo viên đá.</summary>
        public void Initialize(EnemyBrain owner, Vector2 target, float damage, float stunDuration, float speed)
        {
            m_Owner = owner;
            m_Target = target;
            m_Damage = Mathf.Max(0f, damage);
            m_StunDuration = Mathf.Max(0f, stunDuration);
            m_Speed = Mathf.Max(0.1f, speed);
            m_HitTanks.Clear();
        }

        private void Update()
        {
            if (m_Owner == null || !m_Owner.IsAlive)
            {
                Destroy(gameObject);
                return;
            }

            Vector2 current = transform.position;
            Vector2 next = Vector2.MoveTowards(current, m_Target, m_Speed * Time.deltaTime);
            transform.position = next;
            transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);

            DamageTanksInRange(next);

            if (Vector2.Distance(next, m_Target) <= 0.35f)
            {
                Destroy(gameObject);
            }
        }

        private void DamageTanksInRange(Vector2 position)
        {
            if (m_Damage <= 0f) return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(position, hitRadius, tankLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;

                ColliderOwnerRef ownerRef = hit.GetComponent<ColliderOwnerRef>() ?? hit.GetComponentInParent<ColliderOwnerRef>();
                TankBrain tank = ownerRef?.GetOwner<TankBrain>();
                if (tank == null || !tank.IsAlive || m_HitTanks.Contains(tank)) continue;

                m_HitTanks.Add(tank);
                DamageInfo info = new DamageInfo(
                    m_Owner,
                    tank,
                    m_Damage,
                    0f,
                    hit.bounds.center,
                    false,
                    false,
                    false,
                    DamageSourceType.EnemyAttack);
                tank.TakeDamage(info);
                IronWasteland.TextDamageManager.Instance?.Show(info.FinalDamage, hit.bounds.center);

                if (m_StunDuration > 0f)
                {
                    tank.ApplyCrowdControl(CrowdControlType.Stun, m_StunDuration);
                }
            }
        }
    }
}
