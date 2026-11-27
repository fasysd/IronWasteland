using System.Collections;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.Hitboxes
{
    /// <summary>
    /// Hitbox kỹ năng Sóng Thủy Triều (ThuyTrieu):
    /// - Sử dụng PolygonCollider2D hình nón bán kính 4m (cung góc 60° phía trước), isTrigger = true.
    /// - Gây 100% ATK lên mọi Tank trong vùng ảnh hưởng.
    /// - Cơ chế Knockback 3m: đẩy lùi Tank theo hướng (tankPos - bossPos).normalized trong 0.25s theo Ease-out cubic.
    /// - Kết hợp tank.ApplyCrowdControl(CrowdControlType.Knockback, 0.25f, 3.0f).
    /// </summary>
    [DisallowMultipleComponent]
    public class ThuyTrieuHitbox : MonoBehaviour
    {
        [Header("Hitbox Specs")]
        [Tooltip("Bán kính luồng sóng nón (m). Chuẩn GDD là 4m.")]
        [SerializeField, Min(0.5f)] private float radius = 4f;

        [Tooltip("Góc mở hình nón (độ). Chuẩn GDD là 60°.")]
        [SerializeField, Range(10f, 180f)] private float coneAngle = 60f;

        [Tooltip("Khoảng cách đẩy lùi Knockback (m). Chuẩn GDD là 3m.")]
        [SerializeField, Min(0.1f)] private float knockbackDistance = 3f;

        [Tooltip("Thời gian đẩy lùi Knockback (giây). Chuẩn GDD là 0.25s.")]
        [SerializeField, Min(0.05f)] private float knockbackDuration = 0.25f;

        [Tooltip("Tỷ lệ sát thương theo ATK của Boss (1.0 = 100%).")]
        [SerializeField, Min(0f)] private float damageRatio = 1.0f;

        [Tooltip("Thời gian tồn tại của hitbox (giây).")]
        [SerializeField, Min(0.05f)] private float lifetime = 0.6f;

        private readonly HashSet<TankBrain> m_DamagedTanks = new HashSet<TankBrain>();
        private EnemyBrain m_Owner;
        private float m_BossAttack;
        private float m_Age;
        private bool m_Initialized;

        public float Radius => radius;
        public float ConeAngle => coneAngle;
        public float KnockbackDistance => knockbackDistance;
        public float DamageRatio => damageRatio;
        public bool IsInitialized => m_Initialized;
        public IReadOnlyCollection<TankBrain> DamagedTanks => m_DamagedTanks;

        private void Awake()
        {
            UpdateCollider();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UpdateCollider();
            var vfx = GetComponent<IronWasteland.Enemies.ElementalReaper.VFX.ThuyTrieuVFX>();
            if (vfx != null)
            {
                vfx.SyncSettingsFromHitbox();
            }
        }
#endif

        public void UpdateCollider()
        {
            var poly = GetComponent<PolygonCollider2D>();
            if (poly == null) return;

            poly.isTrigger = true;
            int pointCount = 13;
            Vector2[] points = new Vector2[pointCount + 1];
            points[0] = Vector2.zero;
            float halfAngle = coneAngle * 0.5f * Mathf.Deg2Rad;
            float startAngle = -halfAngle;
            float endAngle = halfAngle;
            for (int i = 0; i < pointCount; i++)
            {
                float a = Mathf.Lerp(startAngle, endAngle, (float)i / (pointCount - 1));
                points[i + 1] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
            }
            poly.points = points;
        }

        public void Initialize(EnemyBrain owner, float bossAttack)
        {
            m_Owner = owner;
            m_BossAttack = bossAttack;
            m_Initialized = true;
            m_Age = 0f;
            UpdateCollider();
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
            if (!m_DamagedTanks.Add(tank)) return; // Mỗi Tank chỉ chịu đòn 1 lần

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

            // Cơ chế Knockback 3m
            Vector2 origin = m_Owner != null ? (Vector2)m_Owner.transform.position : (Vector2)transform.position;
            Vector2 dir = ((Vector2)tank.transform.position - origin).normalized;
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = transform.right; // Fallback theo hướng mặt của hitbox
            }

            tank.ApplyCrowdControl(CrowdControlType.Knockback, knockbackDuration, knockbackDistance);
            if (tank.gameObject.activeInHierarchy)
            {
                tank.StartCoroutine(SmoothKnockbackRoutine(tank, dir, knockbackDistance, knockbackDuration));
            }
        }

        private IEnumerator SmoothKnockbackRoutine(TankBrain tank, Vector2 direction, float distance, float duration)
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
                // Ease-out cubic: 1 - (1 - t)^3
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
                IronWasteland.Net.NetTeleport.Move(tank, targetPos);
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
