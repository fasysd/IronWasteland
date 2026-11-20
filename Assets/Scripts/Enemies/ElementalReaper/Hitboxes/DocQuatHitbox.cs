using System.Collections;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Core.Effects;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.Hitboxes
{
    /// <summary>
    /// Hitbox kỹ năng Độc Quật (DocQuat 180°):
    /// - Sử dụng PolygonCollider2D dạng bán nguyệt 180 độ bán kính 4m, isTrigger = true.
    /// - Gây 100% ATK (250 sát thương chuẩn).
    /// - Làm chậm 20% qua tank.ApplyCrowdControl(CrowdControlType.Slow, 5f, 0.2f).
    /// - Giảm 10% DEF qua Stat Modifier trong 5s.
    /// </summary>
    [DisallowMultipleComponent]
    public class DocQuatHitbox : MonoBehaviour
    {
        [Header("Hitbox Specs")]
        [Tooltip("Bán kính bán nguyệt quét (m). Chuẩn GDD là 4m.")]
        [SerializeField, Min(0.5f)] private float radius = 4f;

        [Tooltip("Tỷ lệ sát thương theo ATK của Boss (1.0 = 100%).")]
        [SerializeField, Min(0f)] private float damageRatio = 1.0f;

        [Tooltip("Tỷ lệ làm chậm (0.2 = 20%).")]
        [SerializeField, Range(0.01f, 1f)] private float slowRatio = 0.2f;

        [Tooltip("Thời gian làm chậm (giây).")]
        [SerializeField, Min(0.5f)] private float slowDuration = 5.0f;

        [Tooltip("Tỷ lệ giảm phòng thủ DEF (0.1 = 10%).")]
        [SerializeField, Range(0.01f, 1f)] private float defReductionRatio = 0.1f;

        [Tooltip("Thời gian giảm phòng thủ (giây).")]
        [SerializeField, Min(0.5f)] private float defReductionDuration = 5.0f;

        [Tooltip("Thời gian tồn tại của hitbox (giây).")]
        [SerializeField, Min(0.05f)] private float lifetime = 0.45f;

        private readonly HashSet<TankBrain> m_DamagedTanks = new HashSet<TankBrain>();
        private EnemyBrain m_Owner;
        private float m_BossAttack;
        private float m_Age;
        private bool m_Initialized;

        public float Radius => radius;
        public float DamageRatio => damageRatio;
        public float SlowRatio => slowRatio;
        public float DefReductionRatio => defReductionRatio;
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
        }
#endif

        public void UpdateCollider()
        {
            var poly = GetComponent<PolygonCollider2D>();
            if (poly == null) return;

            poly.isTrigger = true;
            int count = 13;
            Vector2[] points = new Vector2[count + 1];
            points[0] = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, (float)i / (count - 1));
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
            TextDamageManager.Instance?.Show(info.FinalDamage, tank.transform.position);

            // Làm chậm 20% trong 5s
            tank.ApplyCrowdControl(CrowdControlType.Slow, slowDuration, slowRatio);

            // Giảm 10% DEF qua StatModifierEffect trong 5s chuẩn kiến trúc TankBrain
            tank.ApplyEffect(new StatModifierEffect(
                "DocQuat_DefDebuff",
                "Độc Quật Giảm Thủ",
                defReductionDuration,
                null,
                new TankStats { Defense = -defReductionRatio }));
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
