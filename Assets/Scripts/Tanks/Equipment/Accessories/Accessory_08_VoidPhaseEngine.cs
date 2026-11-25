using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 8 (STT 9 GDD): Động Cơ Phân Pha Hư Không.
    /// - Chỉ số nền: +25% Tấn công, +15% Tốc độ di chuyển.
    /// - Nội tại: Sau khi di chuyển quãng đường 3m, nhận 30% Tỉ lệ xuyên giáp và 40% Tỉ lệ sát thương.
    ///   Hiệu quả sẽ biến mất ngay khi gây sát thương.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_08_VoidPhaseEngine : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_08_VoidPhaseEngine_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseAttackMultiplier = 0.25f;
        [SerializeField, Range(0f, 1f)] private float baseMoveSpeedMultiplier = 0.15f;

        [Header("Nội tại Tích Tốc Đột Phá")]
        [SerializeField, Min(0.5f)] private float distanceToCharge = 3.0f;
        [SerializeField, Range(0f, 1f)] private float bonusArmorPenetration = 0.30f;
        [SerializeField, Range(0f, 1f)] private float bonusDamageRate = 0.40f;

        private const string ChargedBuffKey = "Acc08_VoidPhase_Charged";
        private Vector2 m_LastPosition;
        private float m_AccumulatedDistance;
        private bool m_IsCharged;


        protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { Attack = baseAttackMultiplier, MoveSpeed = baseMoveSpeedMultiplier });
            }
            if (owner != null)
            {
                m_LastPosition = owner.transform.position;
            }
            m_AccumulatedDistance = 0f;
            m_IsCharged = false;
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            RemoveChargedBuff();
            base.OnUnequipped(owner);
        }

        public override void OnBrainTick(float deltaTime)
        {
            if (Owner == null || !Owner.IsAlive) return;

            Vector2 currentPos = Owner.transform.position;
            float step = Vector2.Distance(currentPos, m_LastPosition);
            m_LastPosition = currentPos;

            if (!m_IsCharged)
            {
                m_AccumulatedDistance += step;
                if (m_AccumulatedDistance >= distanceToCharge)
                {
                    m_AccumulatedDistance = 0f;
                    ApplyChargedBuff();
                }
            }
        }

        public override void OnOwnerDealtDamage(DamageInfo info)
        {
            // Hiệu quả biến mất khi gây sát thương
            if (m_IsCharged && info.FinalDamage > 0f)
            {
                RemoveChargedBuff();
            }
        }

        private void ApplyChargedBuff()
        {
            m_IsCharged = true;
            Owner.AddBonusStats(ChargedBuffKey, null, new TankStats
            {
                ArmorPenetration = bonusArmorPenetration,
                DamageRate = bonusDamageRate
            });
        }

        private void RemoveChargedBuff()
        {
            if (m_IsCharged && Owner != null)
            {
                Owner.RemoveBonusStats(ChargedBuffKey);
            }
            m_IsCharged = false;
            m_AccumulatedDistance = 0f;
        }
    }
}
