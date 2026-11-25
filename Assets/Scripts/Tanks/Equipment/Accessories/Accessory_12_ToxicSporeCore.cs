using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 12 (STT 13 GDD): Lõi Ma Pháp Bào Tử Độc.
    /// - Chỉ số nền: +50 Giới hạn năng lượng, +15% Tỉ lệ giảm hồi chiêu.
    /// - Nội tại: Kỹ năng gây sát thương phủ khói độc xung quanh mục tiêu bán kính 2.5m,
    ///   làm kẻ địch bị giảm 20% Phòng thủ. Khói tồn tại 3.0s, giãn cách tạo khói là 3.0s (cooldown).
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_12_ToxicSporeCore : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_12_ToxicSporeCore_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Min(0f)] private float baseMaxEnergy = 50f;
        [SerializeField, Range(0f, 1f)] private float baseCooldownReduction = 0.15f;

        [Header("Nội tại Màn Khói Độc")]
        [SerializeField, Min(0.5f)] private float smokeRadius = 2.5f;
        [SerializeField, Min(0.5f)] private float smokeDuration = 3.0f;
        [SerializeField, Min(0.5f)] private float smokeCooldown = 3.0f;
        [SerializeField] private Color smokeColor = new Color(0.2f, 0.8f, 0.2f, 0.4f); // Xanh lá độc

        private float m_CooldownTimer;



        protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, new TankStats { MaxEnergy = baseMaxEnergy }, new TankStats { CooldownReduction = baseCooldownReduction });
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

        public override void OnOwnerDealtDamage(DamageInfo info)
        {
            if (Owner == null || !Owner.IsAlive) return;
            if (m_CooldownTimer > 0f) return;

            // Kỹ năng gây sát thương -> tạo vùng khói độc xung quanh xe tăng sở hữu
            if (info.IsTankSkill && info.FinalDamage > 0f)
            {
                m_CooldownTimer = smokeCooldown;
                SpawnToxicSmoke(Owner.transform.position);
            }
        }

        private void SpawnToxicSmoke(Vector2 position)
        {
            AccessoryAreaZone.Spawn(position, AccessoryAreaZone.ZoneEffectType.ToxicSpore, smokeRadius, smokeDuration, smokeColor);
        }
    }
}
