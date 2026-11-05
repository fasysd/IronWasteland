using System.Collections.Generic;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Xa Thu Tinh The (chuong 1): giu khoang cach tam xa, tich nang luong roi ban
    /// mot tia laser thang xuyen ve phia muc tieu, sat thuong = 100% Tan cong.
    /// Theo tai lieu: HP 200 / ATK 75 / SPD 80 / CD 1.5s.
    /// </summary>
    [DisallowMultipleComponent]
    public class CrystalShooterBrain : ChasingEnemyBrain
    {
        [Header("Giu khoang cach")]
        [Tooltip("Gan hon khoang nay thi lui ra.")]
        [SerializeField, Min(0f)] private float keepMinDistance = 6f;

        [Tooltip("Xa hon khoang nay thi tien lai.")]
        [SerializeField, Min(0f)] private float keepMaxDistance = 9f;

        [Tooltip("He so toc do khi di ngang (1 = toi da).")]
        [SerializeField, Range(0f, 1f)] private float strafeSpeedRatio = 0.55f;

        [Header("Ban laser")]
        [Tooltip("Chi ban khi muc tieu trong tam nay.")]
        [SerializeField, Min(0.1f)] private float attackRange = 12f;

        [Tooltip("Thoi gian hoi chieu (giay).")]
        [SerializeField, Min(0.05f)] private float cooldown = 1.5f;

        [Tooltip("He so sat thuong so voi Tan cong (1 = 100%).")]
        [SerializeField, Min(0f)] private float damageRatio = 1f;

        [Tooltip("Nua do rong tia laser (don vi the gioi).")]
        [SerializeField, Min(0.05f)] private float beamHalfWidth = 0.5f;

        [Tooltip("Thoi gian hien tia laser (giay).")]
        [SerializeField, Min(0.01f)] private float beamDuration = 0.12f;

        [SerializeField] private bool logShot;

        private float m_Cooldown;
        private float m_BeamTimer;
        private SpriteRenderer m_Beam;
        private float m_StrafeSign = 1f;
        private float m_StrafeTimer;

        protected override Vector2 ComputeDesiredVelocity(TankBrain target, float distance)
        {
            float speed = Mathf.Max(0f, Stats.MoveSpeed);
            Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;

            if (distance <= 0.0001f) return Vector2.zero;

            Vector2 forward = toTarget / distance;

            if (distance > keepMaxDistance) return forward * speed;
            if (distance < keepMinDistance) return -forward * speed;

            // Trong khoang giu: di ngang de kho giam danh hon, doi huong moi ~3s.
            m_StrafeTimer += Time.deltaTime;
            if (m_StrafeTimer >= 3f)
            {
                m_StrafeTimer = 0f;
                m_StrafeSign = -m_StrafeSign;
            }

            Vector2 tangent = new Vector2(-forward.y, forward.x) * m_StrafeSign;
            return tangent * (speed * strafeSpeedRatio);
        }

        protected override void OnBrainTick(float deltaTime)
        {
            base.OnBrainTick(deltaTime);

            if (m_BeamTimer > 0f)
            {
                m_BeamTimer -= deltaTime;
                if (m_BeamTimer <= 0f && m_Beam != null) m_Beam.enabled = false;
            }

            TankBrain target = Target;
            if (target == null) return;

            m_Cooldown -= deltaTime;
            if (m_Cooldown > 0f) return;

            if (DistanceToTarget > attackRange) return;

            m_Cooldown = cooldown;
            FireBeam(target);
        }

        private void FireBeam(TankBrain target)
        {
            Vector2 origin = transform.position;
            Vector2 direction = DirectionToTarget;
            float length = Mathf.Max(1f, attackRange);
            float damage = Mathf.Max(0f, Stats.Attack * damageRatio);

            int hits = DamageTanksAlongLine(origin, direction, length, beamHalfWidth, damage);
            ShowBeam(direction, length);

            if (logShot)
            {
                Debug.Log("[CrystalShooter] Ban laser: " + damage.ToString("0") + " sat thuong, trung " + hits + " tank.");
            }
        }

        private void ShowBeam(Vector2 direction, float length)
        {
            if (m_Beam == null)
            {
                GameObject beamObject = new GameObject("LaserBeam");
                beamObject.transform.SetParent(transform, false);
                m_Beam = beamObject.AddComponent<SpriteRenderer>();
                m_Beam.sprite = SpriteFactory.Circle;
                m_Beam.color = new Color(0.55f, 0.95f, 1f, 0.9f);
                m_Beam.sortingOrder = 5;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            m_Beam.transform.localPosition = direction * (length * 0.5f);
            m_Beam.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            // Sprite tron 1 don vi -> keo dai theo truc X thanh tia.
            m_Beam.transform.localScale = new Vector3(length, beamHalfWidth * 2f, 1f);
            m_Beam.enabled = true;
            m_BeamTimer = beamDuration;
        }

        /// <summary>Gay sat thuong cho tank nam tren duong thang tu origin theo huong direction.</summary>
        private static int DamageTanksAlongLine(Vector2 origin, Vector2 direction, float length, float halfWidth, float damage)
        {
            if (damage <= 0f) return 0;

            Vector2 end = origin + direction * length;
            Vector2 midpoint = (origin + end) * 0.5f;

            Collider2D[] candidates = Physics2D.OverlapCircleAll(midpoint, length * 0.5f + 2f);
            HashSet<TankBrain> damaged = new HashSet<TankBrain>();

            for (int i = 0; i < candidates.Length; i++)
            {
                Collider2D candidate = candidates[i];
                if (candidate == null) continue;

                TankBrain tank = candidate.GetComponentInParent<TankBrain>();
                if (tank == null || !tank.IsAlive) continue;
                if (!damaged.Add(tank)) continue;

                float tankRadius = Mathf.Max(0.1f, candidate.bounds.extents.magnitude);
                float gap = DistancePointToSegment(tank.transform.position, origin, end);
                if (gap > halfWidth + tankRadius) continue;

                tank.TakeDamage(damage);
                TextDamageManager.Instance?.Show(damage, tank.transform.position);
            }

            return damaged.Count;
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared <= 0.0001f) return Vector2.Distance(point, a);

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
            Vector2 closest = a + ab * t;
            return Vector2.Distance(point, closest);
        }

        protected override void OnDeath()
        {
            Destroy(gameObject);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (keepMinDistance > keepMaxDistance) keepMinDistance = keepMaxDistance;
            if (cooldown < 0.05f) cooldown = 0.05f;
            if (attackRange < keepMaxDistance) attackRange = keepMaxDistance;
        }
    }
}
