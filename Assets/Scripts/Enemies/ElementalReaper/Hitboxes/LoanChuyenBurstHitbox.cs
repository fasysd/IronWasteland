using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.Hitboxes
{
    /// <summary>
    /// Hitbox kỹ năng Loạn Chuyển & Nổ Độc (LoanChuyen):
    /// - Sử dụng CircleCollider2D hình cầu bán kính 3m, isTrigger = true.
    /// - Gây 100% ATK (250 sát thương chuẩn).
    /// - Gây Choáng (Stun) 2.0s qua tank.ApplyCrowdControl(CrowdControlType.Stun, 2.0f).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public class LoanChuyenBurstHitbox : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private SpriteRenderer shockwaveSprite;
        [SerializeField] private ParticleSystem poisonParticles;

        [Header("Hitbox Specs")]
        [Tooltip("Bán kính vùng nổ độc (m). Chuẩn GDD là 3m.")]
        [SerializeField, Min(0.5f)] private float radius = 3.0f;

        [Tooltip("Tỷ lệ sát thương theo ATK của Boss (1.0 = 100%).")]
        [SerializeField, Min(0f)] private float damageRatio = 1.0f;

        [Tooltip("Thời gian làm choáng (giây). Chuẩn GDD là 2.0s.")]
        [SerializeField, Min(0.1f)] private float stunDuration = 2.0f;

        [Tooltip("Thời gian tồn tại của hitbox (giây).")]
        [SerializeField, Min(0.05f)] private float lifetime = 0.5f;

        private readonly HashSet<TankBrain> m_DamagedTanks = new HashSet<TankBrain>();
        private EnemyBrain m_Owner;
        private float m_BossAttack;
        private float m_Age;
        private bool m_Initialized;

        public float Radius => radius;
        public float DamageRatio => damageRatio;
        public float StunDuration => stunDuration;
        public bool IsInitialized => m_Initialized;
        public IReadOnlyCollection<TankBrain> DamagedTanks => m_DamagedTanks;

        private void Awake()
        {
            UpdateCollider();
            UpdateVFXScale();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UpdateCollider();
            UpdateVFXScale();
        }
#endif

        public void UpdateCollider()
        {
            var col = GetComponent<CircleCollider2D>();
            if (col != null)
            {
                col.isTrigger = true;
                col.radius = radius;
            }
        }

        public void UpdateVFXScale()
        {
            if (shockwaveSprite == null)
            {
                shockwaveSprite = GetComponentInChildren<SpriteRenderer>();
            }
            if (shockwaveSprite != null && shockwaveSprite.sprite != null)
            {
                Vector2 spriteBounds = shockwaveSprite.sprite.bounds.size;
                float targetDiameter = radius * 2f;
                float sx = spriteBounds.x > 0.01f ? (targetDiameter / spriteBounds.x) : targetDiameter;
                float sy = spriteBounds.y > 0.01f ? (targetDiameter / spriteBounds.y) : targetDiameter;
                shockwaveSprite.transform.localScale = new Vector3(sx, sy, 1f);
            }

            if (poisonParticles == null)
            {
                poisonParticles = GetComponentInChildren<ParticleSystem>();
            }
            if (poisonParticles != null)
            {
                var shape = poisonParticles.shape;
                shape.radius = radius;
            }
        }

        public void Initialize(EnemyBrain owner, float bossAttack)
        {
            m_Owner = owner;
            m_BossAttack = bossAttack;
            m_Initialized = true;
            m_Age = 0f;
            UpdateCollider();
            UpdateVFXScale();
        }

        private void Update()
        {
            m_Age += Time.deltaTime;
            if (m_Age >= lifetime)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandleTrigger(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            HandleTrigger(other);
        }

        private void HandleTrigger(Collider2D other)
        {
            if (other == null) return;
            TankBrain tank = ResolveTank(other);
            if (tank != null && tank.IsAlive)
            {
                ApplyHitToTank(tank);
            }
        }

        public void ApplyHitToTank(TankBrain tank)
        {
            if (tank == null || !tank.IsAlive) return;
            if (!m_DamagedTanks.Add(tank)) return; // Mỗi Tank chịu đòn 1 lần

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

            // Choáng 2s
            tank.ApplyCrowdControl(CrowdControlType.Stun, stunDuration);
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
