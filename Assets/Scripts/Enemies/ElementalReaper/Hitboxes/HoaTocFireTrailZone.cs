using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.Hitboxes
{
    /// <summary>
    /// Vệt lửa dung nham trên đường lướt Hỏa Tốc (HoaTocFireTrailZone):
    /// - BoxCollider2D kéo dài trên quãng đường lướt, isTrigger = true.
    /// - Tồn tại 10s, tick mỗi 0.5s gây 20% ATK (50 sát thương chuẩn) cho mọi Tank đứng trên vệt lửa.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public class HoaTocFireTrailZone : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private SpriteRenderer trailSprite;
        [SerializeField] private ParticleSystem groundFlamesParticles;

        [Header("Trail Specs")]
        [Tooltip("Bề rộng vệt lửa (m).")]
        [SerializeField, Min(0.5f)] private float trailWidth = 2.0f;

        [Tooltip("Tỷ lệ sát thương mỗi tick theo ATK của Boss (0.2 = 20%).")]
        [SerializeField, Min(0f)] private float damagePerTickRatio = 0.2f;

        [Tooltip("Chu kỳ gây sát thương (giây). Chuẩn GDD là 0.5s.")]
        [SerializeField, Min(0.1f)] private float tickInterval = 0.5f;

        [Tooltip("Thời gian tồn tại của vệt lửa (giây). Chuẩn GDD là 10.0s.")]
        [SerializeField, Min(1f)] private float duration = 10.0f;

        private readonly List<TankBrain> m_InsideTanks = new List<TankBrain>();
        private EnemyBrain m_Owner;
        private float m_BossAttack;
        private float m_Age;
        private float m_NextTickTime;
        private bool m_Initialized;
        private BoxCollider2D m_BoxCollider;

        public float TrailWidth => trailWidth;
        public float DamagePerTickRatio => damagePerTickRatio;
        public float Duration => duration;
        public bool IsInitialized => m_Initialized;

        private void Awake()
        {
            m_BoxCollider = GetComponent<BoxCollider2D>();
            if (m_BoxCollider != null)
            {
                m_BoxCollider.isTrigger = true;
                UpdateDimensions(m_BoxCollider.size.x, trailWidth);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_BoxCollider == null) m_BoxCollider = GetComponent<BoxCollider2D>();
            float len = m_BoxCollider != null ? m_BoxCollider.size.x : 4f;
            UpdateDimensions(len, trailWidth);
        }
#endif

        public void SetupTrail(Vector2 startPoint, Vector2 endPoint, EnemyBrain owner, float bossAttack)
        {
            m_Owner = owner;
            m_BossAttack = bossAttack;
            m_Initialized = true;
            m_Age = 0f;
            m_NextTickTime = 0f;

            Vector2 diff = endPoint - startPoint;
            float length = diff.magnitude;
            Vector2 mid = (startPoint + endPoint) * 0.5f;

            transform.position = mid;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            UpdateDimensions(length, trailWidth);
        }

        public void UpdateDimensions(float length, float width)
        {
            float actualLength = Mathf.Max(0.5f, length);
            if (m_BoxCollider == null) m_BoxCollider = GetComponent<BoxCollider2D>();
            if (m_BoxCollider != null)
            {
                m_BoxCollider.size = new Vector2(actualLength, width);
                m_BoxCollider.offset = Vector2.zero;
            }

            // Đồng bộ kích thước hiển thị sprite vệt lửa theo tỷ lệ chính xác của texture bounds
            if (trailSprite != null && trailSprite.sprite != null)
            {
                Vector2 spriteBounds = trailSprite.sprite.bounds.size;
                float sx = spriteBounds.x > 0.01f ? (actualLength / spriteBounds.x) : actualLength;
                float sy = spriteBounds.y > 0.01f ? (width / spriteBounds.y) : width;
                trailSprite.transform.localScale = new Vector3(sx, sy, 1f);
            }

            if (groundFlamesParticles != null)
            {
                var shape = groundFlamesParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(actualLength, width, 0.1f);
                var emission = groundFlamesParticles.emission;
                emission.rateOverTime = Mathf.Max(10f, actualLength * 5f);
            }
        }

        private void Update()
        {
            m_Age += Time.deltaTime;

            if (m_Age >= m_NextTickTime)
            {
                TriggerDamageTick();
                m_NextTickTime += tickInterval;
            }

            if (m_Age >= duration)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
            }
        }

        private void TriggerDamageTick()
        {
            for (int i = m_InsideTanks.Count - 1; i >= 0; i--)
            {
                TankBrain tank = m_InsideTanks[i];
                if (tank == null || !tank.IsAlive)
                {
                    m_InsideTanks.RemoveAt(i);
                    continue;
                }

                float attackValue = m_BossAttack > 0f ? m_BossAttack : 250f;
                float tickDamage = attackValue * damagePerTickRatio;

                DamageInfo info = new DamageInfo(
                    m_Owner,
                    tank,
                    tickDamage,
                    0f,
                    tank.transform.position,
                    sourceType: DamageSourceType.EnemyAttack);

                tank.TakeDamage(info);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TankBrain tank = ResolveTank(other);
            if (tank != null && !m_InsideTanks.Contains(tank))
            {
                m_InsideTanks.Add(tank);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            TankBrain tank = ResolveTank(other);
            if (tank != null)
            {
                m_InsideTanks.Remove(tank);
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
