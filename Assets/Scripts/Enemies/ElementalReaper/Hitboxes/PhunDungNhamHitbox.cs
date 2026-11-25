using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.Hitboxes
{
    /// <summary>
    /// Hitbox kỹ năng Phun Dung Nham (PhunDungNham):
    /// - Sử dụng BoxCollider2D kích thước 16m x 1.2m, offset = (8m, 0m), isTrigger = true.
    /// - Gây 120% ATK (300 sát thương chuẩn) cho mọi Tank đứng trên đường tia.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PhunDungNhamHitbox : MonoBehaviour
    {
        [Header("Hitbox Specs")]
        [Tooltip("Chiều dài tia dung nham (m). Chuẩn GDD là 16m.")]
        [SerializeField, Min(1f)] private float beamLength = 16.0f;

        [Tooltip("Bề rộng tia dung nham (m). Chuẩn GDD là 1.2m.")]
        [SerializeField, Min(0.2f)] private float beamWidth = 1.2f;

        [Tooltip("Tỷ lệ sát thương theo ATK của Boss (1.2 = 120%).")]
        [SerializeField, Min(0f)] private float damageRatio = 1.2f;

        [Tooltip("Thời gian tồn tại của tia dung nham (giây).")]
        [SerializeField, Min(0.1f)] private float lifetime = 1.2f;

        private readonly HashSet<TankBrain> m_DamagedTanks = new HashSet<TankBrain>();
        private EnemyBrain m_Owner;
        private float m_BossAttack;
        private float m_Age;
        private bool m_Initialized;

        public float BeamLength => beamLength;
        public float BeamWidth => beamWidth;
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
        }
#endif

        public void UpdateCollider()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.isTrigger = true;
                box.size = new Vector2(beamLength, beamWidth);
                box.offset = new Vector2(beamLength * 0.5f, 0f);
            }
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
            if (!m_DamagedTanks.Add(tank)) return; // Mỗi Tank chịu sát thương 1 lần trong suốt tia

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
