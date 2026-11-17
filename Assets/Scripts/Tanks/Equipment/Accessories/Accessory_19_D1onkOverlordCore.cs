using IronWasteland.Core;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 19 (STT 20 GDD): Lõi Siêu Cơ Giáp Thống Lĩnh D1ONK.
    /// - Chỉ số nền: +50% Tấn công, +50% Giới hạn máu, +50% Phòng thủ, +15% Tỉ lệ sát thương, +30% Tỉ lệ xuyên giáp.
    /// - Nội tại: Khi nhận phần thưởng từ kẻ địch Tinh anh (phá thành công toàn bộ mạch năng lượng Boss Tinh anh),
    ///   sẽ hồi 50% Giới hạn năng lượng, +30 Tỉ lệ giảm hồi chiêu và +30% Tỉ lệ sát thương trong 10s.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_19_D1onkOverlordCore : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_19_D1onkOverlordCore_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseAttackMultiplier = 0.50f;
        [SerializeField, Range(0f, 1f)] private float baseMaxHealthMultiplier = 0.50f;
        [SerializeField, Range(0f, 1f)] private float baseDefenseMultiplier = 0.50f;
        [SerializeField, Range(0f, 1f)] private float baseDamageRateMultiplier = 0.15f;
        [SerializeField, Range(0f, 1f)] private float baseArmorPenetrationMultiplier = 0.30f;

        [Header("Nội tại Thưởng Bùng Nổ Năng Lượng")]
        [SerializeField, Range(0f, 1f)] private float energyRestoreRatio = 0.50f;
        [SerializeField, Range(0f, 1f)] private float rewardCooldownReduction = 0.30f;
        [SerializeField, Range(0f, 1f)] private float rewardDamageRate = 0.30f;
        [SerializeField, Min(1f)] private float rewardDuration = 10.0f;

        private const string RewardBuffKey = "Acc19_D1onk_OverlordReward";
        private float m_RewardTimer;
        private bool m_IsRewardActive;


        protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats
            {
                Attack = baseAttackMultiplier,
                MaxHealth = baseMaxHealthMultiplier,
                Defense = baseDefenseMultiplier,
                DamageRate = baseDamageRateMultiplier,
                ArmorPenetration = baseArmorPenetrationMultiplier
            });
            }
            EliteEnemyBrain.OnEnergyBurstSuccess += HandleEnergyBurstReward;
            m_RewardTimer = 0f;
            m_IsRewardActive = false;
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            EliteEnemyBrain.OnEnergyBurstSuccess -= HandleEnergyBurstReward;
            RemoveRewardBuff();
            base.OnUnequipped(owner);
        }

        private void HandleEnergyBurstReward(EliteEnemyBrain elite)
        {
            if (Owner == null || !Owner.IsAlive) return;

            // Hồi 50% Giới hạn năng lượng
            float energyAmount = Owner.FinalStats.MaxEnergy * energyRestoreRatio;
            Owner.GainEnergy(energyAmount);

            // Nhận buff trong 10s
            ApplyRewardBuff();
        }

        public override void OnBrainTick(float deltaTime)
        {
            if (!m_IsRewardActive) return;

            m_RewardTimer -= deltaTime;
            if (m_RewardTimer <= 0f)
            {
                RemoveRewardBuff();
            }
        }

        private void ApplyRewardBuff()
        {
            m_RewardTimer = rewardDuration;
            if (!m_IsRewardActive)
            {
                m_IsRewardActive = true;
                Owner.AddBonusStats(RewardBuffKey, null, new TankStats
                {
                    CooldownReduction = rewardCooldownReduction,
                    DamageRate = rewardDamageRate
                });
            }
        }

        private void RemoveRewardBuff()
        {
            if (m_IsRewardActive && Owner != null)
            {
                Owner.RemoveBonusStats(RewardBuffKey);
            }
            m_IsRewardActive = false;
        }
    }
}
