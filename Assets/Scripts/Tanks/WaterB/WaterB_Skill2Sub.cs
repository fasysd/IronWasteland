using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Biến thể của Kỹ năng tùy chọn 2. Được chọn bởi TankBrainContext.OptionSkill2
    /// (mapping nằm trong TankBrain_WaterB.GetSkill2Variant()).
    /// </summary>
    public enum Skill2WaterVariant
    {
        MobileAltar,   // OptionSkill2 = 0 (mặc định) - Đài tế thủy triều di động
        WaterTurrets,  // OptionSkill2 = 1 - Triệu hồi 3 tháp nước
        AquaAssault    // OptionSkill2 = 2 - Chuyển sang thiên hướng tấn công
    }

    /// <summary>
    /// Chỉ chứa thông số (theo GDD) của các biến thể Kỹ năng tùy chọn 2.
    /// Các giá trị dạng "Ratio" là hệ số nhân với Tấn công / Giới hạn máu của Water B tại thời điểm tung chiêu.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterB_Skill2Sub : MonoBehaviour
    {
        [Header("Mobile Altar")]
        [SerializeField, Min(0f)] private float altarEnergyCost = 75f;
        [SerializeField, Min(0.1f)] private float altarCooldown = 16f;
        [SerializeField, Min(0.1f)] private float altarDuration = 10f;
        [SerializeField, Min(0.1f)] private float altarInterval = 2f;            // Giãn cách hồi máu & khiên
        [SerializeField, Min(0f)] private float altarHealAtkRatio = 0.5f;        // 50% Tấn công
        [SerializeField, Min(0f)] private float altarHealMaxHpRatio = 0.02f;     // 2% Giới hạn máu Water B
        [SerializeField, Min(0f)] private float altarShieldAtkRatio = 0.8f;      // 80% Tấn công
        [SerializeField, Min(0f)] private float altarShieldMaxHpRatio = 0.05f;   // 5% Giới hạn máu Water B
        [SerializeField, Min(0.1f)] private float altarShieldDuration = 5f;

        [Header("Water Turrets")]
        [SerializeField, Min(0f)] private float turretEnergyCost = 75f;
        [SerializeField, Min(0.1f)] private float turretCooldown = 14f;
        [SerializeField, Min(0.1f)] private float turretDuration = 8f;
        [SerializeField, Min(1)] private int turretCount = 3;
        [SerializeField, Min(0.05f)] private float turretAttackInterval = 1f;
        [SerializeField, Min(0f)] private float turretDamageAtkRatio = 0.5f;     // 50% Tấn công
        [SerializeField, Min(0f)] private float energyRegenPerHit = 3f;
        [SerializeField, Min(0f)] private float turretSpacing = 1.5f;            // Khoảng cách các tháp quanh điểm đặt

        [Header("Aqua Assault")]
        [SerializeField, Min(0f)] private float assaultEnergyCost = 65f;
        [SerializeField, Min(0.1f)] private float assaultCooldown = 14f;
        [SerializeField, Min(0.1f)] private float assaultDuration = 7f;
        [SerializeField, Min(0f)] private float assaultAttackBonusPercent = 0.25f;   // +25% Tấn công
        [SerializeField, Min(0f)] private float assaultCooldownReduction = 0.20f;    // +20% Giảm hồi chiêu
        [SerializeField, Min(0f)] private float assaultDamageBonus = 0.15f;          // +15% Tỉ lệ sát thương
        [SerializeField, Min(0f)] private float assaultArmorPenetration = 0.20f;     // +20% Tỉ lệ xuyên giáp
        [SerializeField, Min(1f)] private float assaultProjectileSpeedMultiplier = 1.3f; // GDD: "tốc độ lớn hơn" (chỉnh tay)
        [SerializeField, Min(1f)] private float assaultProjectileSizeMultiplier = 1.5f;  // GDD: "kích thước lớn hơn" (chỉnh tay)

        public float GetEnergyCost(Skill2WaterVariant variant)
        {
            switch (variant)
            {
                case Skill2WaterVariant.WaterTurrets: return turretEnergyCost;
                case Skill2WaterVariant.AquaAssault: return assaultEnergyCost;
                default: return altarEnergyCost;
            }
        }

        public float GetCooldown(Skill2WaterVariant variant)
        {
            switch (variant)
            {
                case Skill2WaterVariant.WaterTurrets: return turretCooldown;
                case Skill2WaterVariant.AquaAssault: return assaultCooldown;
                default: return altarCooldown;
            }
        }

        // Mobile Altar
        public float AltarDuration => altarDuration;
        public float AltarInterval => altarInterval;
        public float AltarHealAtkRatio => altarHealAtkRatio;
        public float AltarHealMaxHpRatio => altarHealMaxHpRatio;
        public float AltarShieldAtkRatio => altarShieldAtkRatio;
        public float AltarShieldMaxHpRatio => altarShieldMaxHpRatio;
        public float AltarShieldDuration => altarShieldDuration;

        // Water Turrets
        public float TurretDuration => turretDuration;
        public int TurretCount => turretCount;
        public float TurretAttackInterval => turretAttackInterval;
        public float TurretDamageAtkRatio => turretDamageAtkRatio;
        public float EnergyRegenPerHit => energyRegenPerHit;
        public float TurretSpacing => turretSpacing;

        // Aqua Assault
        public float AssaultDuration => assaultDuration;
        public float AssaultAttackBonusPercent => assaultAttackBonusPercent;
        public float AssaultCooldownReduction => assaultCooldownReduction;
        public float AssaultDamageBonus => assaultDamageBonus;
        public float AssaultArmorPenetration => assaultArmorPenetration;
        public float AssaultProjectileSpeedMultiplier => assaultProjectileSpeedMultiplier;
        public float AssaultProjectileSizeMultiplier => assaultProjectileSizeMultiplier;
    }
}