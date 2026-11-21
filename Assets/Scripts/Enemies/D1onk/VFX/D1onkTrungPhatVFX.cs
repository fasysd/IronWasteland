using System;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk.VFX
{
    /// <summary>
    /// VFX Kỹ năng Trừng Phạt (TrungPhat): Sấm sét trừng phạt giáng từ trên trời xuống điểm chỉ định.
    /// Gồm 2 pha:
    /// - Pha 1 (Cảnh báo): Vòng hồng tâm / ấn chú cảnh báo nhấp nháy trên mặt đất.
    /// - Pha 2 (Sét đánh): Cột sét lôi quang thẳng đứng giáng xuống mặt đất kèm tia lửa hồ quang và sóng xung kích.
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkTrungPhatVFX : MonoBehaviour
    {
        [Header("Timing Settings")]
        [Tooltip("Thời gian hiện cảnh báo trước khi sét giáng (giây).")]
        [SerializeField, Min(0.1f)] private float warningDuration = 0.6f;

        [Tooltip("Thời gian cột sét lưu lại và phát sáng (giây).")]
        [SerializeField, Min(0.1f)] private float strikeDuration = 0.5f;

        [Tooltip("Bán kính sát thương / ảnh hưởng của sét đánh (m).")]
        [SerializeField, Min(0.5f)] private float blastRadius = 2.5f;

        [Header("Components")]
        [SerializeField] private SpriteRenderer warningReticle;
        [SerializeField] private LineRenderer lightningBoltLine;
        [SerializeField] private ParticleSystem groundImpactSparks;
        [SerializeField] private SpriteRenderer lightningBoltSprite;

        private float m_Timer = 0f;
        private bool m_HasStruck = false;
        private bool m_IsFinished = false;
        private float m_Damage = 0f;
        private EnemyBrain m_Owner = null;
        private Action m_OnComplete = null;

        public float BlastRadius => blastRadius;
        public bool HasStruck => m_HasStruck;
        public bool IsFinished => m_IsFinished;

        public void Initialize(Vector3 targetPosition, float damage = 50f, EnemyBrain owner = null, Action onComplete = null)
        {
            transform.position = targetPosition;
            m_Damage = damage;
            m_Owner = owner;
            m_OnComplete = onComplete;
            m_Timer = 0f;
            m_HasStruck = false;
            m_IsFinished = false;

            if (warningReticle != null)
            {
                warningReticle.gameObject.SetActive(true);
                warningReticle.transform.localScale = Vector3.one * (blastRadius * 0.8f);
            }

            if (lightningBoltLine != null) lightningBoltLine.enabled = false;
            if (lightningBoltSprite != null) lightningBoltSprite.gameObject.SetActive(false);
        }

        private void Update()
        {
            ManualTick(Time.deltaTime);
        }

        /// <summary>
        /// Tiến trình mô phỏng sét đánh. Phục vụ unit test.
        /// </summary>
        public void ManualTick(float dt)
        {
            if (m_IsFinished) return;

            m_Timer += dt;

            if (!m_HasStruck)
            {
                // Pha 1: Cảnh báo nhấp nháy
                if (warningReticle != null)
                {
                    float alpha = Mathf.PingPong(m_Timer * 8f, 0.6f) + 0.3f;
                    Color c = warningReticle.color;
                    c.a = alpha;
                    warningReticle.color = c;
                }

                if (m_Timer >= warningDuration)
                {
                    ExecuteStrike();
                }
            }

            if (m_HasStruck)
            {
                // Pha 2: Cột sét mờ dần
                float strikeElapsed = m_Timer - warningDuration;
                float progress = Mathf.Clamp01(strikeElapsed / strikeDuration);

                if (lightningBoltSprite != null)
                {
                    Color c = lightningBoltSprite.color;
                    c.a = 1f - progress;
                    lightningBoltSprite.color = c;
                }

                if (progress >= 1f)
                {
                    m_IsFinished = true;
                    m_OnComplete?.Invoke();
                    if (Application.isPlaying)
                        Destroy(gameObject);
                    else
                        DestroyImmediate(gameObject);
                }
            }
        }

        private void ExecuteStrike()
        {
            m_HasStruck = true;

            if (warningReticle != null)
                warningReticle.gameObject.SetActive(false);

            // Kích hoạt cột sét
            if (lightningBoltSprite != null)
            {
                lightningBoltSprite.gameObject.SetActive(true);
                Color c = lightningBoltSprite.color;
                c.a = 1f;
                lightningBoltSprite.color = c;
            }

            if (lightningBoltLine != null)
            {
                lightningBoltLine.enabled = true;
                // Vẽ tia sét từ trên trời (y + 15) xuống mặt đất
                lightningBoltLine.positionCount = 6;
                Vector3 start = transform.position + new Vector3(0f, 15f, 0f);
                Vector3 end = transform.position;
                for (int i = 0; i < 6; i++)
                {
                    float t = i / 5f;
                    Vector3 pt = Vector3.Lerp(start, end, t);
                    if (i > 0 && i < 5)
                    {
                        pt.x += UnityEngine.Random.Range(-0.4f, 0.4f);
                    }
                    lightningBoltLine.SetPosition(i, pt);
                }
            }

            if (groundImpactSparks != null)
            {
                groundImpactSparks.Play();
            }

            // Gây sát thương nổ sét quanh điểm giáng
            DamageTanksInRadius();
        }

        private void DamageTanksInRadius()
        {
            if (m_Damage <= 0f) return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, blastRadius);
            foreach (var hit in hits)
            {
                if (hit == null || hit.GetComponentInParent<EnemyBrain>() != null) continue;

                TankBrain tank = D1onkMissileProjectile.ResolveTankBrain(hit);
                if (tank != null && tank.IsAlive)
                {
                    DamageInfo info = new DamageInfo(m_Owner, tank, m_Damage, 0f, transform.position, sourceType: DamageSourceType.EnemyAttack);
                    tank.TakeDamage(info);
                    TextDamageManager.Instance?.Show(info.FinalDamage, tank.transform.position);
                }
            }
        }
    }
}
