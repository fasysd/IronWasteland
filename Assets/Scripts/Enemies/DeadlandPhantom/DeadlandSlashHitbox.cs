using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.DeadlandPhantom
{
    /// <summary>
    /// Hitbox gây sát thương diện rộng của kỹ năng Vết Chém Tử Địa (DeadlandSlash):
    /// - Sử dụng CircleCollider2D Trigger kết hợp ColliderOwnerRef.
    /// - Mọi cấu hình (bán kính, tỷ lệ sát thương phân tầng, thời gian tồn tại) đều có thể điều chỉnh trên Inspector.
    /// - Bán kính được đồng bộ trực tiếp với CircleCollider2D của Hitbox.
    /// - Quy tắc phân tầng sát thương theo GDD:
    ///   + Mục tiêu trong cự ly <= innerThreshold: chịu innerDamageRatio (mặc định 150% ATK của Boss).
    ///   + Mục tiêu trong cự ly innerThreshold - maxRadius: chịu outerDamageRatio (mặc định 100% ATK của Boss).
    /// - Đảm bảo mỗi Tank chỉ nhận sát thương đúng 1 lần trong mỗi lần vung chém.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeadlandSlashHitbox : MonoBehaviour
    {
        [Header("Hitbox Specs")]

        [Tooltip("Ngưỡng cự ly cận chiến chịu đòn nặng (m). Chuẩn GDD là 2m.")]
        [SerializeField, Min(0.1f)] private float innerThreshold = 2.0f;

        [Tooltip("Tỷ lệ sát thương vùng trong (<= innerThreshold). 1.5 = 150% ATK.")]
        [SerializeField, Min(0f)] private float innerDamageRatio = 1.5f;

        [Tooltip("Tỷ lệ sát thương vùng ngoài (innerThreshold -> maxRadius). 1.0 = 100% ATK.")]
        [SerializeField, Min(0f)] private float outerDamageRatio = 1.0f;

        [Tooltip("Thời gian tồn tại của hitbox trước khi tự hủy (giây).")]
        [SerializeField, Min(0.05f)] private float lifetime = 0.35f;

        private readonly HashSet<TankBrain> m_DamagedTanks = new HashSet<TankBrain>();
        private EnemyBrain m_Owner;
        private float m_BossAttack;
        private CircleCollider2D m_CircleCollider;
        private float m_Age;
        private bool m_Initialized;

        public float InnerThreshold => innerThreshold;
        public float InnerDamageRatio => innerDamageRatio;
        public float OuterDamageRatio => outerDamageRatio;
        public IReadOnlyCollection<TankBrain> DamagedTanks => m_DamagedTanks;
        public bool IsInitialized => m_Initialized;

        private void Awake()
        {
            m_CircleCollider = GetComponent<CircleCollider2D>();

            if (m_CircleCollider == null)
            {
                Debug.LogWarning($"[DeadlandSlashHitbox] CircleCollider2D is not found on '{gameObject.name}'. Hitbox cannot detect collisions.");
            }
        }

        public void Initialize(EnemyBrain owner, float bossAttack, float customRadius = -1f)
        {
            m_Owner = owner;
            m_BossAttack = bossAttack;

            m_Initialized = true;
            m_Age = 0f;
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
            HandleTriggerEnter(other);
        }

        private void HandleTriggerEnter(Collider2D other)
        {
            if (!m_Initialized || other == null) return;
            TankBrain tank = ResolveTank(other);
            if (tank != null && tank.IsAlive)
            {
                ApplyDamageToTank(tank);
            }
        }

        public void ApplyDamageToTank(TankBrain tank)
        {
            if (tank == null || !tank.IsAlive) return;
            if (!m_DamagedTanks.Add(tank)) return; // Đã nhận sát thương từ vết chém này rồi

            float distance = Vector2.Distance(transform.position, tank.transform.position);
            float ratio = (distance <= innerThreshold) ? innerDamageRatio : outerDamageRatio;
            float finalDamage = m_BossAttack * ratio;

            DamageInfo info = new DamageInfo(
                m_Owner,
                tank,
                finalDamage,
                0f,
                tank.transform.position,
                sourceType: DamageSourceType.EnemyAttack);

            tank.TakeDamage(info);
            TextDamageManager.Instance?.Show(info.FinalDamage, tank.transform.position);
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
