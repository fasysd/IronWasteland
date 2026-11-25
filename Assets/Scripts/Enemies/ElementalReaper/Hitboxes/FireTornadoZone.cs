using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.Hitboxes
{
    /// <summary>
    /// Vùng kỹ năng Lốc Xoáy Lửa (FireTornadoZone):
    /// - CircleCollider2D bán kính 2m, isTrigger = true.
    /// - Tồn tại 1.5s, tick mỗi 0.5s gây 10% ATK (25 sát thương chuẩn).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public class FireTornadoZone : MonoBehaviour
    {
        [Header("Zone Specs")]
        [Tooltip("Bán kính lốc xoáy (m). Chuẩn GDD là 2m.")]
        [SerializeField, Min(0.5f)] private float radius = 2.0f;

        [Tooltip("Tỷ lệ sát thương mỗi tick theo ATK của Boss (0.1 = 10%).")]
        [SerializeField, Min(0f)] private float damagePerTickRatio = 0.1f;

        [Tooltip("Chu kỳ gây sát thương (giây). Chuẩn GDD là 0.5s.")]
        [SerializeField, Min(0.1f)] private float tickInterval = 0.5f;

        [Tooltip("Thời gian tồn tại của lốc xoáy (giây). Chuẩn GDD là 1.5s.")]
        [SerializeField, Min(0.2f)] private float duration = 1.5f;

        private readonly List<TankBrain> m_InsideTanks = new List<TankBrain>();
        private EnemyBrain m_Owner;
        private float m_BossAttack;
        private float m_Age;
        private float m_NextTickTime;
        private bool m_Initialized;

        public float Radius => radius;
        public float DamagePerTickRatio => damagePerTickRatio;
        public float TickInterval => tickInterval;
        public float Duration => duration;
        public bool IsInitialized => m_Initialized;

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
            var col = GetComponent<CircleCollider2D>();
            if (col != null)
            {
                col.isTrigger = true;
                col.radius = radius;
            }
        }

        public void Initialize(EnemyBrain owner, float bossAttack)
        {
            m_Owner = owner;
            m_BossAttack = bossAttack;
            m_Initialized = true;
            m_Age = 0f;
            m_NextTickTime = 0f; // Tick ngay lập tức khi spawn
            UpdateCollider();
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
