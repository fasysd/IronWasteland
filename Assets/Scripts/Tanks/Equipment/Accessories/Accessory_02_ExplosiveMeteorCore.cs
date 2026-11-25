using IronWasteland.Core;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 2 (STT 3 GDD): Lõi Năng Lượng Thiên Thạch Nổ.
    /// - Chỉ số nền: +20% Giới hạn máu, +20% Phòng thủ.
    /// - Nội tại: Mỗi khi nhận sát thương tích lũy đủ 15% Máu tối đa -> giáp phát nổ trong bán kính 1.5m,
    ///   gây sát thương bằng 100% Phòng thủ (DamageSourceType.Accessory) và đẩy lùi quái thường xung quanh.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_02_ExplosiveMeteorCore : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_02_ExplosiveMeteorCore_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseMaxHealthMultiplier = 0.20f;
        [SerializeField, Range(0f, 1f)] private float baseDefenseMultiplier = 0.20f;

        [Header("Nội tại Nổ Giáp")]
        [SerializeField, Range(0.05f, 0.5f)] private float hpLossThresholdRatio = 0.15f;
        [SerializeField, Min(0.5f)] private float explosionRadius = 1.5f;
        [SerializeField, Min(0f)] private float knockbackDistance = 2.5f;

        private float m_AccumulatedDamageTaken;


        protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { MaxHealth = baseMaxHealthMultiplier, Defense = baseDefenseMultiplier });
            }
            m_AccumulatedDamageTaken = 0f;
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            base.OnUnequipped(owner);
        }

        public override void OnOwnerTookDamage(DamageInfo info)
        {
            if (Owner == null || !Owner.IsAlive) return;

            m_AccumulatedDamageTaken += info.HealthDamage;
            float threshold = Owner.FinalStats.MaxHealth * hpLossThresholdRatio;

            while (threshold > 0f && m_AccumulatedDamageTaken >= threshold)
            {
                m_AccumulatedDamageTaken -= threshold;
                TriggerMeteorExplosion();
            }
        }

        private void TriggerMeteorExplosion()
        {
            Vector2 center = Owner.transform.position;
            float rawDamage = Owner.FinalStats.Defense;

            Collider2D[] hits = Physics2D.OverlapCircleAll(center, explosionRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                ColliderOwnerRef ownerRef = ColliderOwnerRef.FromCollider(hits[i]);
                if (ownerRef == null) continue;

                EnemyBrain enemy = ownerRef.GetOwner<EnemyBrain>();
                if (enemy != null && enemy.IsAlive)
                {
                    DamageInfo info = new DamageInfo(Owner, enemy, rawDamage, Owner.FinalStats.ArmorPenetration, enemy.transform.position, sourceType: DamageSourceType.Accessory);
                    enemy.TakeDamage(info);

                    // Đẩy lùi quái thường (tinh anh miễn nhiễm khống chế cứng)
                    if (!enemy.IsElite)
                    {
                        enemy.ApplyCrowdControl(CrowdControlType.Knockback, 0.25f, knockbackDistance);
                    }
                }
            }
        }
    }
}
