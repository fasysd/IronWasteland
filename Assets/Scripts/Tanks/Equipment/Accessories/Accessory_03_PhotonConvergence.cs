using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 3 (STT 4 GDD): Lõi Ma Pháp Hội Tụ Quang Tử.
    /// - Chỉ số nền: +18% Tỉ lệ sát thương, +15% Tỉ lệ xuyên giáp.
    /// - Nội tại: Khi dùng Kỹ năng thường (NormalSkill) trúng mục tiêu cách xa > 7m, tăng 25% Tỉ lệ sát thương trong 1s.
    ///   Kích hoạt lại làm mới thời gian duy trì.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_03_PhotonConvergence : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_03_PhotonConvergence_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseDamageRateMultiplier = 0.18f;
        [SerializeField, Range(0f, 1f)] private float baseArmorPenetrationMultiplier = 0.15f;

        [Header("Nội tại Hội Tụ Bắn Xa")]
        [SerializeField, Min(1f)] private float distanceThreshold = 7.0f;
        [SerializeField, Range(0f, 1f)] private float bonusDamageRate = 0.25f;
        [SerializeField, Min(0.1f)] private float buffDuration = 1.0f;

        private const string BuffKey = "Acc03_PhotonConvergence";
        private float m_BuffTimer;
        private bool m_IsBuffActive;


                protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { DamageRate = baseDamageRateMultiplier, ArmorPenetration = baseArmorPenetrationMultiplier });
            }
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            RemoveBuff();
            base.OnUnequipped(owner);
        }

        public override void OnOwnerDealtDamage(DamageInfo info)
        {
            if (Owner == null || !Owner.IsAlive) return;

            // Chỉ kích hoạt khi đòn đánh là Kỹ năng thường (NormalSkill)
            if (info.SourceType != DamageSourceType.NormalSkill) return;

            float distance = Vector2.Distance(Owner.transform.position, info.HitPosition);
            if (distance >= distanceThreshold)
            {
                ApplyOrRefreshBuff();
            }
        }

        public override void OnBrainTick(float deltaTime)
        {
            if (!m_IsBuffActive) return;

            m_BuffTimer -= deltaTime;
            if (m_BuffTimer <= 0f)
            {
                RemoveBuff();
            }
        }

        private void ApplyOrRefreshBuff()
        {
            m_BuffTimer = buffDuration;
            if (!m_IsBuffActive)
            {
                m_IsBuffActive = true;
                Owner.AddBonusStats(BuffKey, null, new TankStats { DamageRate = bonusDamageRate });
            }
        }

        private void RemoveBuff()
        {
            if (m_IsBuffActive && Owner != null)
            {
                Owner.RemoveBonusStats(BuffKey);
            }
            m_IsBuffActive = false;
        }
    }
}
