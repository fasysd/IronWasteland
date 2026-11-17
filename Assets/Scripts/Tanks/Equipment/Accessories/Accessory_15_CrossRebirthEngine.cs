using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 15 (STT 16 GDD): Lõi Động Cơ Tái Sinh Chữ Thập.
    /// - Chỉ số nền: +30% Giới hạn máu.
    /// - Nội tại: Tự sửa chữa 1.5% máu tối đa mỗi giây; khi máu dưới 40%, tốc độ sửa chữa tăng gấp đôi (3%/s) và +25% phòng thủ.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_15_CrossRebirthEngine : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_15_CrossRebirthEngine_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseMaxHealthMultiplier = 0.30f;

        [Header("Nội tại Tái Sinh Chữ Thập")]
        [SerializeField, Range(0f, 0.1f)] private float normalRegenPerSecond = 0.015f;
        [SerializeField, Range(0.1f, 0.8f)] private float lowHealthThreshold = 0.40f;
        [SerializeField, Range(0f, 0.2f)] private float lowHealthRegenPerSecond = 0.030f;
        [SerializeField, Range(0f, 1f)] private float lowHealthBonusDefense = 0.25f;

        private const string DefenseBuffKey = "Acc15_CrossRebirth_Defense";
        private bool m_IsLowHealthBuffActive;


                protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { MaxHealth = baseMaxHealthMultiplier });
            }
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            RemoveLowHealthDefenseBuff();
            base.OnUnequipped(owner);
        }

        public override void OnBrainTick(float deltaTime)
        {
            if (Owner == null || !Owner.IsAlive) return;

            float currentHealth = Owner.CurrentHealth;
            float maxHealth = Owner.FinalStats.MaxHealth;
            if (maxHealth <= 0f) return;

            float healthRatio = currentHealth / maxHealth;
            bool isLow = healthRatio < lowHealthThreshold;

            // Xử lý buff phòng thủ
            if (isLow && !m_IsLowHealthBuffActive)
            {
                m_IsLowHealthBuffActive = true;
                Owner.AddBonusStats(DefenseBuffKey, null, new TankStats { Defense = lowHealthBonusDefense });
            }
            else if (!isLow && m_IsLowHealthBuffActive)
            {
                RemoveLowHealthDefenseBuff();
            }

            // Hồi máu liên tục
            float regenRate = isLow ? lowHealthRegenPerSecond : normalRegenPerSecond;
            float healAmount = maxHealth * regenRate * deltaTime;
            Owner.Heal(healAmount);
        }

        private void RemoveLowHealthDefenseBuff()
        {
            if (m_IsLowHealthBuffActive && Owner != null)
            {
                Owner.RemoveBonusStats(DefenseBuffKey);
            }
            m_IsLowHealthBuffActive = false;
        }
    }
}
