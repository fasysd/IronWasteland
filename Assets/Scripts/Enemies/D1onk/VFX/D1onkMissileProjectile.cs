using System;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk.VFX
{
    /// <summary>
    /// Đạn tên lửa bắn từ 14 nòng súng của Trưởng Quản Ngục D1onk (Bị động Pháo Hạm Đa Nòng).
    /// Hướng bắn ban đầu ra khỏi nòng súng là -gunTransform.right.
    /// Có khói lửa đẩy phía sau và phát nổ khi va chạm hoặc hết thời gian sống.
    /// 
    /// RÀNG BUỘC KIẾN TRÚC:
    /// - ColliderRef ở VFX prefab hoặc đạn prefab TUYỆT ĐỐI KHÔNG được lấy EliteEnemyBrain của D1onk làm owner.
    /// - Không tự ý AddComponent ngầm.
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkMissileProjectile : MonoBehaviour
    {
        [Header("Flight Settings")]
        [Tooltip("Tốc độ bay của tên lửa (m/s).")]
        [SerializeField, Min(1f)] private float flySpeed = 12f;

        [Tooltip("Gia tốc tăng tốc khi bay (m/s^2).")]
        [SerializeField, Min(0f)] private float acceleration = 6f;

        [Tooltip("Tốc độ quay bẻ lái hướng về mục tiêu (độ/giây, 0 = bay thẳng).")]
        [SerializeField, Min(0f)] private float turnSpeed = 60f;

        [Tooltip("Thời gian sống tối đa trước khi tự phát nổ (giây).")]
        [SerializeField, Min(0.5f)] private float maxLifetime = 5f;

        [Tooltip("Bán kính nổ sát thương.")]
        [SerializeField, Min(0.2f)] private float hitRadius = 0.8f;

        [Header("Components & Prefabs")]
        [SerializeField] private SpriteRenderer missileSprite;
        [SerializeField] private TrailRenderer thrusterTrail;
        [SerializeField] private ParticleSystem exhaustParticles;
        [SerializeField] private GameObject impactExplosionPrefab;

        private Transform m_Target;
        private Vector2 m_FlyDirection = Vector2.right;
        private float m_CurrentSpeed;
        private float m_Age = 0f;
        private bool m_Detonated = false;
        private float m_Damage = 25f;
        private EnemyBrain m_Owner = null;

        public float Damage => m_Damage;
        public float CurrentSpeed => m_CurrentSpeed;
        public Vector2 FlyDirection => m_FlyDirection;
        public bool IsDetonated => m_Detonated;
        public EnemyBrain Owner => m_Owner;

        public void Initialize(Vector2 initialDirection, Transform target = null, float initialSpeed = -1f, float damage = 25f, EnemyBrain owner = null)
        {
            m_FlyDirection = initialDirection.sqrMagnitude > 0.001f ? initialDirection.normalized : Vector2.right;
            m_Target = target;
            m_CurrentSpeed = initialSpeed > 0f ? initialSpeed : flySpeed;
            m_Damage = damage;
            m_Owner = owner;
            m_Age = 0f;
            m_Detonated = false;

            // Xoay đầu tên lửa theo hướng bay
            float angle = Mathf.Atan2(m_FlyDirection.y, m_FlyDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (exhaustParticles != null)
            {
                exhaustParticles.Play();
            }
        }

        private void Update()
        {
            ManualTick(Time.deltaTime);
        }

        /// <summary>
        /// Tiến trình mô phỏng bay của tên lửa. Phục vụ unit test mô phỏng headless.
        /// </summary>
        public void ManualTick(float dt)
        {
            if (m_Detonated) return;

            m_Age += dt;
            m_CurrentSpeed += acceleration * dt;

            // Bẻ lái nhẹ về mục tiêu nếu có
            if (m_Target != null && m_Target.gameObject.activeInHierarchy && turnSpeed > 0f)
            {
                Vector2 toTarget = (Vector2)m_Target.position - (Vector2)transform.position;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    m_FlyDirection = Vector3.RotateTowards(m_FlyDirection, toTarget.normalized, turnSpeed * Mathf.Deg2Rad * dt, 0f);
                    m_FlyDirection.Normalize();
                }

                if (toTarget.magnitude <= hitRadius)
                {
                    TankBrain tank = ResolveTankBrain(m_Target.GetComponent<Collider2D>()) ?? m_Target.GetComponent<TankBrain>() ?? m_Target.GetComponentInParent<TankBrain>();
                    if (tank != null) DamageTank(tank);
                    Detonate();
                    return;
                }
            }

            // Di chuyển
            transform.position += (Vector3)(m_FlyDirection * (m_CurrentSpeed * dt));
            float angle = Mathf.Atan2(m_FlyDirection.y, m_FlyDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (m_Age >= maxLifetime)
            {
                Detonate();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (m_Detonated || other == null) return;

            // Bỏ qua bản thân boss và quái khác
            if (other.GetComponentInParent<EnemyBrain>() != null) return;

            TankBrain tank = ResolveTankBrain(other);
            if (tank != null)
            {
                DamageTank(tank);
                Detonate();
                return;
            }

            // Va chạm tường hoặc vật cản không phải trigger
            if (!other.isTrigger)
            {
                Detonate();
            }
        }

        private void DamageTank(TankBrain tank)
        {
            if (tank == null || !tank.IsAlive || m_Damage <= 0f) return;
            DamageInfo info = new DamageInfo(m_Owner, tank, m_Damage, 0f, tank.transform.position, sourceType: DamageSourceType.EnemyAttack);
            tank.TakeDamage(info);
            TextDamageManager.Instance?.Show(info.FinalDamage, tank.transform.position);
        }

        public static TankBrain ResolveTankBrain(Collider2D col)
        {
            if (col == null) return null;
            if (col.TryGetComponent<ColliderOwnerRef>(out var targetRef) && targetRef != null)
            {
                if (targetRef.TryGetOwner<TankBrain>(out var directBrain) && directBrain != null)
                    return directBrain;
                if (targetRef.Owner is TankBrain brain)
                    return brain;
                if (targetRef.Owner is Component comp)
                {
                    var compBrain = comp.GetComponent<TankBrain>() ?? comp.GetComponentInParent<TankBrain>();
                    if (compBrain != null) return compBrain;
                }
                if (targetRef.Owner is GameObject go)
                {
                    var goBrain = go.GetComponent<TankBrain>() ?? go.GetComponentInParent<TankBrain>();
                    if (goBrain != null) return goBrain;
                }
            }
            return col.GetComponentInParent<TankBrain>();
        }

        public void Detonate()
        {
            if (m_Detonated) return;
            m_Detonated = true;

            if (impactExplosionPrefab != null)
            {
                Instantiate(impactExplosionPrefab, transform.position, Quaternion.identity);
            }

            if (thrusterTrail != null)
            {
                thrusterTrail.transform.SetParent(null);
                thrusterTrail.emitting = false;
                if (Application.isPlaying)
                    Destroy(thrusterTrail.gameObject, Mathf.Max(0.5f, thrusterTrail.time + 0.1f));
                else
                    DestroyImmediate(thrusterTrail.gameObject);
            }

            if (exhaustParticles != null)
            {
                exhaustParticles.transform.SetParent(null);
                exhaustParticles.Stop();
                if (Application.isPlaying)
                    Destroy(exhaustParticles.gameObject, 1.0f);
                else
                    DestroyImmediate(exhaustParticles.gameObject);
            }

            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }
    }
}
