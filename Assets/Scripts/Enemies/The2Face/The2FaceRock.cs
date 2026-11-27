using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.The2Face
{
    /// <summary>
    /// Viên đá hội tụ về Boss do HutVaDa của The2Face bắn ra.
    /// Gây sát thương và choáng cho Tank khi chạm Collider2D Trigger.
    /// </summary>
    public class The2FaceRock : MonoBehaviour
    {
        [Tooltip("Layer của Tank để quét va chạm.")]
        [SerializeField] private LayerMask tankLayer;

        [Tooltip("Tốc độ xoay hình ảnh (độ/giây).")]
        [SerializeField] private float rotateSpeed = 540f;

        private EnemyBrain m_Owner;
        private Vector2 m_Target;
        private float m_Damage;
        private float m_StunDuration;
        private float m_Speed;
        private Collider2D m_Collider;
        private readonly HashSet<TankBrain> m_HitTanks = new HashSet<TankBrain>();

        private void Awake()
        {
            m_Collider = GetComponent<Collider2D>();
            if (m_Collider != null)
            {
                m_Collider.isTrigger = true;
            }
        }

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

            if (Vector2.Distance(next, m_Target) <= 0.35f)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryHitTarget(other);
        }

        private void TryHitTarget(Collider2D hit)
        {
            if (hit == null || m_Damage <= 0f || m_Owner == null || !m_Owner.IsAlive) return;

            if (tankLayer.value != 0 && (tankLayer.value & (1 << hit.gameObject.layer)) == 0)
            {
                return;
            }

            ColliderOwnerRef ownerRef = ColliderOwnerRef.FromCollider(hit);
            TankBrain tank = ownerRef?.GetOwner<TankBrain>() ?? hit.GetComponentInParent<TankBrain>();
            if (tank == null || !tank.IsAlive || !m_HitTanks.Add(tank)) return;

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

            if (m_StunDuration > 0f)
            {
                tank.ApplyCrowdControl(CrowdControlType.Stun, m_StunDuration);
            }
        }
    }
}
