using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 16 (STT 17 GDD): Lõi Thủy Tinh Trị Liệu.
    /// - Chỉ số nền: +15% Giới hạn máu, +20% Tỉ lệ giảm hồi chiêu.
    /// - Nội tại: Khi hồi máu sẽ tạo khiên bằng 50% lượng hồi phục nhận, tồn tại trong 5s.
    ///   Khi nhận hồi phục trong thời gian có khiên sẽ tăng lượng khiên hiện tại bằng 50% lượng hồi phục đó
    ///   và đặt lại thời gian tồn tại giáp. Giá trị khiên tối đa bằng 30% Giới hạn máu.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_16_HealingCrystalCore : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_16_HealingCrystalCore_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseMaxHealthMultiplier = 0.15f;
        [SerializeField, Range(0f, 1f)] private float baseCooldownReduction = 0.20f;

        [Header("Nội tại Khiên Thủy Tinh")]
        [SerializeField, Range(0f, 1f)] private float healToShieldRatio = 0.50f;
        [SerializeField, Min(1f)] private float shieldDuration = 5.0f;
        [SerializeField, Range(0.1f, 1f)] private float maxShieldHealthCapRatio = 0.30f;


        protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { MaxHealth = baseMaxHealthMultiplier, CooldownReduction = baseCooldownReduction });
            }
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            base.OnUnequipped(owner);
        }

        private const string CrystalShieldId = "Acc16_HealingCrystal_Shield";

        public override void OnOwnerHealed(float amount)
        {
            if (Owner == null || !Owner.IsAlive || amount <= 0f) return;

            float shieldToAdd = amount * healToShieldRatio;
            float maxShieldCap = Owner.FinalStats.MaxHealth * maxShieldHealthCapRatio;

            // Nếu lượng khiên hiện tại đã chạm trần thì không cộng thêm, nhưng vẫn làm mới thời gian 5s
            float currentShield = Owner.CurrentShield;
            if (currentShield + shieldToAdd > maxShieldCap)
            {
                shieldToAdd = Mathf.Max(0f, maxShieldCap - currentShield);
            }

            if (shieldToAdd > 0f)
            {
                Owner.AddShield(shieldToAdd, shieldDuration, CrystalShieldId, Owner, "Khiên Thủy Tinh");
            }
            else if (currentShield > 0f)
            {
                Owner.AddShield(0.001f, shieldDuration, CrystalShieldId, Owner, "Khiên Thủy Tinh");
            }
        }
    }
}
