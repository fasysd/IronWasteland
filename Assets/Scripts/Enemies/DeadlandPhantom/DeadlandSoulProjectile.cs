using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.DeadlandPhantom
{
    /// <summary>
    /// VFX 3: Đạn Linh Hồn Ma Hỏa (DeadlandSoul Projectile).
    /// Quả cầu ma hỏa tím xanh tự tìm mục tiêu, có trail hạt phát sáng kéo dài phía sau
    /// và hiệu ứng nổ linh hồn khi va chạm hoặc hết thời gian sống.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeadlandSoulProjectile : MonoBehaviour
    {
        [Header("Flight & Homing Settings")]
        [Tooltip("Tốc độ bay của linh hồn (m/s).")]
        [SerializeField, Min(1f)] private float flySpeed = 8f;

        [Tooltip("Tốc độ bẻ lái tìm mục tiêu (độ/giây).")]
        [SerializeField, Min(0f)] private float turnSpeed = 180f;

        [Tooltip("Thời gian sống tối đa trước khi tự phát nổ (giây).")]
        [SerializeField, Min(0.5f)] private float maxLifetime = 6f;

        [Tooltip("Bán kính nổ / va chạm.")]
        [SerializeField, Min(0.2f)] private float hitRadius = 0.6f;

        [Header("Components")]
        [SerializeField] private SpriteRenderer soulCoreSprite;
        [SerializeField] private ParticleSystem flameAuraParticles;
        [SerializeField] private TrailRenderer soulTrail;
        [SerializeField] private GameObject impactExplosionPrefab;

        private Transform m_Target;
        private Vector2 m_FlyDirection = Vector2.right;
        private float m_Age;
        private bool m_Detonated;
        private float m_Damage;
        private EnemyBrain m_Owner;

        public float Damage => m_Damage;
        public EnemyBrain Owner => m_Owner;

        public void Initialize(Transform target, Vector2 initialDirection, float speed = 8f)
        {
            Initialize(target, initialDirection, speed, 0f, null);
        }

        public void Initialize(Transform target, Vector2 initialDirection, float speed, float damage, EnemyBrain owner)
        {
            m_Target = target;
            m_FlyDirection = initialDirection.sqrMagnitude > 0.001f ? initialDirection.normalized : Vector2.right;
            flySpeed = speed;
            m_Damage = damage;
            m_Owner = owner;
            m_Age = 0f;
            m_Detonated = false;

            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(m_FlyDirection.y, m_FlyDirection.x) * Mathf.Rad2Deg);
        }

        private void Update()
        {
            SimulateFlight(Time.deltaTime);
        }

        public void ManualTick(float dt)
        {
            SimulateFlight(dt);
        }

        private void SimulateFlight(float dt)
        {
            if (m_Detonated) return;

            m_Age += dt;

            // Homing logic towards target
            if (m_Target != null)
            {
                Vector2 toTarget = ((Vector2)m_Target.position - (Vector2)transform.position);
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Vector2 desiredDir = toTarget.normalized;
                    m_FlyDirection = Vector3.RotateTowards(m_FlyDirection, desiredDir, turnSpeed * Mathf.Deg2Rad * dt, 0f);
                }

                if (toTarget.magnitude <= hitRadius)
                {
                    TankBrain tank = m_Target.GetComponent<TankBrain>() ?? m_Target.GetComponentInParent<TankBrain>();
                    if (tank != null) DamageTank(tank);
                    Detonate();
                    return;
                }
            }

            // Move
            transform.position += (Vector3)(m_FlyDirection * (flySpeed * dt));
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(m_FlyDirection.y, m_FlyDirection.x) * Mathf.Rad2Deg);

            // Core pulsation
            if (soulCoreSprite != null)
            {
                float pulse = 1f + 0.15f * Mathf.Sin(m_Age * 12f);
                soulCoreSprite.transform.localScale = new Vector3(pulse, pulse, 1f);
            }

            if (m_Age >= maxLifetime)
            {
                Detonate();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (m_Detonated || other == null) return;

            // Bỏ qua chính boss và quái khác
            if (other.GetComponentInParent<EnemyBrain>() != null) return;

            TankBrain tank = ResolveTankBrain(other);
            if (tank != null)
            {
                DamageTank(tank);
                Detonate();
                return;
            }

            // Nếu va chạm chướng ngại vật / địa hình (không phải trigger)
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

            // Detach trail và ngừng emit để vệt sáng tiêu biến tự nhiên, không rách hình
            if (soulTrail != null)
            {
                soulTrail.transform.SetParent(null);
                soulTrail.emitting = false;
                if (Application.isPlaying)
                    Destroy(soulTrail.gameObject, Mathf.Max(0.5f, soulTrail.time + 0.1f));
                else
                    DestroyImmediate(soulTrail.gameObject);
            }

            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }
    }
}
