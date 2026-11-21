using System;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk.VFX
{
    /// <summary>
    /// VFX Kỹ năng Laze Nguyên Tố (LazeNguyenTo):
    /// Tia laser năng lượng cao tần tập trung phóng thẳng từ lõi năng lượng của D1onk,
    /// có lõi sáng chói, hào quang nguyên tố bao quanh và bụi tia lửa nổ tại điểm chạm.
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkLazeNguyenToVFX : MonoBehaviour
    {
        [Header("Laser Beam Settings")]
        [Tooltip("Chiều dài tối đa của tia laser (m).")]
        [SerializeField, Min(5f)] private float maxBeamLength = 25f;

        [Tooltip("Bề rộng của tia laser (m).")]
        [SerializeField, Min(0.1f)] private float beamWidth = 0.8f;

        [Tooltip("Tốc độ quét xoay góc (độ/giây, 0 = cố định).")]
        [SerializeField] private float sweepAngleSpeed = 0f;

        [Header("Components")]
        [SerializeField] private LineRenderer laserCoreLine;
        [SerializeField] private SpriteRenderer laserBeamSprite;
        [SerializeField] private ParticleSystem muzzleChargeParticles;
        [SerializeField] private ParticleSystem impactSparksParticles;

        private Vector2 m_Direction = Vector2.right;
        private float m_Duration = 2.5f;
        private float m_Timer = 0f;
        private bool m_IsActive = false;
        private float m_DamagePerSecond = 50f;
        private EnemyBrain m_Owner = null;
        private Action m_OnComplete = null;
        private float m_CurrentBeamLength = 25f;

        public bool IsActive => m_IsActive;
        public Vector2 Direction => m_Direction;
        public float MaxBeamLength => maxBeamLength;
        public float CurrentBeamLength => m_CurrentBeamLength;
        public float BeamWidth => beamWidth;

        public void Initialize(Vector2 direction, float duration = 2.5f, float damagePerSecond = 50f, EnemyBrain owner = null, Action onComplete = null)
        {
            m_Direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            m_Duration = duration;
            m_DamagePerSecond = damagePerSecond;
            m_Owner = owner;
            m_OnComplete = onComplete;
            m_Timer = 0f;
            m_IsActive = true;

            UpdateLaserTransform();

            if (muzzleChargeParticles != null) muzzleChargeParticles.Play();
            if (impactSparksParticles != null) impactSparksParticles.Play();
        }

        private void Update()
        {
            ManualTick(Time.deltaTime);
        }

        /// <summary>
        /// Cập nhật tia laser theo thời gian. Hỗ trợ unit test.
        /// </summary>
        public void ManualTick(float dt)
        {
            if (!m_IsActive) return;

            m_Timer += dt;

            // Xoay quét nếu có góc sweep
            if (Mathf.Abs(sweepAngleSpeed) > 0.01f)
            {
                float angle = Mathf.Atan2(m_Direction.y, m_Direction.x) * Mathf.Rad2Deg;
                angle += sweepAngleSpeed * dt;
                m_Direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            }

            UpdateLaserTransform();

            // Gây sát thương theo chu kỳ
            DealDamageTick(dt);

            if (m_Timer >= m_Duration)
            {
                StopLaser();
            }
        }

        private void UpdateLaserTransform()
        {
            float angle = Mathf.Atan2(m_Direction.y, m_Direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // Bắn raycast để xác định điểm va chạm, bỏ qua bản thân EnemyModel nếu xuất phát từ trong thân
            m_CurrentBeamLength = maxBeamLength;
            RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, m_Direction, maxBeamLength);
            foreach (var h in hits)
            {
                if (h.collider != null && h.collider.GetComponentInParent<EnemyBrain>() == null)
                {
                    m_CurrentBeamLength = h.distance;
                    break;
                }
            }

            // Cập nhật LineRenderer nếu có
            if (laserCoreLine != null)
            {
                laserCoreLine.enabled = true;
                laserCoreLine.positionCount = 2;
                laserCoreLine.SetPosition(0, transform.position);
                laserCoreLine.SetPosition(1, transform.position + (Vector3)(m_Direction * m_CurrentBeamLength));
                laserCoreLine.startWidth = beamWidth;
                laserCoreLine.endWidth = beamWidth * 0.9f;
            }

            // Cập nhật SpriteRenderer nếu dùng dạng kéo dãn Sprite
            if (laserBeamSprite != null)
            {
                laserBeamSprite.enabled = true;
                laserBeamSprite.size = new Vector2(m_CurrentBeamLength, beamWidth);
                laserBeamSprite.transform.localPosition = new Vector3(m_CurrentBeamLength * 0.5f, 0f, 0f);
            }

            // Đặt điểm nổ tia lửa tại đầu cuối laser
            if (impactSparksParticles != null)
            {
                impactSparksParticles.transform.position = transform.position + (Vector3)(m_Direction * m_CurrentBeamLength);
            }
        }

        private void DealDamageTick(float dt)
        {
            if (m_DamagePerSecond <= 0f) return;

            RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, beamWidth * 0.5f, m_Direction, m_CurrentBeamLength);
            foreach (var h in hits)
            {
                if (h.collider == null || h.collider.GetComponentInParent<EnemyBrain>() != null) continue;

                TankBrain tank = D1onkMissileProjectile.ResolveTankBrain(h.collider);
                if (tank != null && tank.IsAlive)
                {
                    DamageInfo info = new DamageInfo(m_Owner, tank, m_DamagePerSecond * dt, 0f, h.point, sourceType: DamageSourceType.EnemyAttack);
                    tank.TakeDamage(info);
                }
            }
        }

        public void StopLaser()
        {
            if (!m_IsActive) return;
            m_IsActive = false;

            if (muzzleChargeParticles != null) muzzleChargeParticles.Stop();
            if (impactSparksParticles != null) impactSparksParticles.Stop();
            if (laserCoreLine != null) laserCoreLine.enabled = false;
            if (laserBeamSprite != null) laserBeamSprite.enabled = false;

            m_OnComplete?.Invoke();

            if (Application.isPlaying)
                Destroy(gameObject, 0.3f);
            else
                DestroyImmediate(gameObject);
        }
    }
}
