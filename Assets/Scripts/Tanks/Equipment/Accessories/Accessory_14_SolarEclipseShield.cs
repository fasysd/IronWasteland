using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 14 (STT 15 GDD): Lõi Ma Trận Khiên Nhật Thực.
    /// - Chỉ số nền: +20% Giới hạn máu, +60 Giới hạn năng lượng.
    /// - Nội tại: Mỗi khi dùng kỹ năng tiêu hao năng lượng, nhận lá chắn hấp thụ sát thương bằng 12% máu tối đa trong 6s (hồi 6s).
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_14_SolarEclipseShield : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_14_SolarEclipseShield_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseMaxHealthMultiplier = 0.20f;
        [SerializeField, Min(0f)] private float baseMaxEnergy = 60f;

        [Header("Nội tại Lá Chắn Nhật Thực")]
        [SerializeField, Range(0f, 0.5f)] private float shieldHealthRatio = 0.12f;
        [SerializeField, Min(1f)] private float shieldDuration = 6.0f;
        [SerializeField, Min(1f)] private float shieldCooldown = 6.0f;

        private float m_CooldownTimer;



        protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, new TankStats { MaxEnergy = baseMaxEnergy }, new TankStats { MaxHealth = baseMaxHealthMultiplier });
            }
            m_CooldownTimer = 0f;
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            base.OnUnequipped(owner);
        }

        public override void OnBrainTick(float deltaTime)
        {
            if (m_CooldownTimer > 0f)
            {
                m_CooldownTimer -= deltaTime;
            }
        }

        public override void OnOwnerUsedSkill(int skillIndex)
        {
            if (Owner == null || !Owner.IsAlive) return;

            // Kiểm tra năng lượng tiêu hao của skillIndex
            float cost = Owner.GetSkillEnergyCost(skillIndex);
            if (cost > 0f && m_CooldownTimer <= 0f)
            {
                m_CooldownTimer = shieldCooldown;
                float shieldAmount = Owner.FinalStats.MaxHealth * shieldHealthRatio;
                Owner.AddShield(shieldAmount, shieldDuration);
            }
        }
    }
}
