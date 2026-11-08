using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    /// <summary>
    /// Biến thể của Kỹ năng tùy chọn 2. Được chọn bởi TankBrainContext.OptionSkill2
    /// (mapping nằm trong TankBrain_GreenFox.GetSkill2Variant()).
    /// </summary>
    public enum Skill2Variant
    {
        Variant_2A, // OptionSkill2 = 0 (mặc định) - Mark Explosion
        Variant_2B, // OptionSkill2 = 1 - Fox Territory
        Variant_2C  // OptionSkill2 = 2 - Nine-Tailed Assault
    }

    /// <summary>Thông số (theo GDD) của các biến thể Kỹ năng tùy chọn 2.</summary>
    [DisallowMultipleComponent]
    public class GreenFox_Skill2Sub : MonoBehaviour
    {
        [Header("2A - Mark Explosion")]
        [SerializeField, Min(0f)] private float buff2AEnergyCost = 55f;
        [SerializeField, Min(0.01f)] private float buff2ACooldown = 12f;
        [SerializeField, Min(0f)] private float damagePerStackPercent = 20f;   // 20% ATK mỗi cộng dồn
        [SerializeField, Min(0.1f)] private float attackBuffDuration = 6f;
        [SerializeField, Range(0f, 2f)] private float attackBuffPercent = 0.3f; // +30% Tấn công
        [SerializeField, Min(1f)] private float markExplosionRange = 20f;       // GDD ghi "trong vùng" nhưng không có số, chỉnh tay

        [Header("2B - Fox Territory")]
        [SerializeField, Min(0f)] private float buff2BEnergyCost = 60f;
        [SerializeField, Min(0.01f)] private float buff2BCooldown = 15f;
        [SerializeField, Min(0.1f)] private float territoryDuration = 6f;
        [SerializeField, Range(0.1f, 0.9f)] private float slowAmount = 0.3f;
        [SerializeField, Range(0.1f, 2.0f)] private float speedBoostPercent = 0.3f;
        [SerializeField, Min(0f)] private float energyPerHit = 3f;
        [SerializeField, Min(0f)] private float maxEnergyGain = 30f;

        [Header("2C - Nine-Tailed Assault")]
        [SerializeField, Min(0f)] private float buff2CEnergyCost = 75f;
        [SerializeField, Min(0.01f)] private float buff2CCooldown = 16f;
        [SerializeField, Min(0.1f)] private float assaultDuration = 6f;
        [SerializeField, Min(1)] private int tailCount = 9;
        [SerializeField, Min(0.01f)] private float attackInterval = 1f;          // Giãn cách tấn công mỗi đuôi
        [SerializeField, Min(0f)] private float tailDamagePercent = 15f;         // 15% ATK mỗi đuôi

        // 2A
        public float Buff2AEnergyCost => buff2AEnergyCost;
        public float Buff2ACooldown => buff2ACooldown;
        public float DamagePerStackPercent => damagePerStackPercent;
        public float AttackBuffDuration => attackBuffDuration;
        public float AttackBuffPercent => attackBuffPercent;
        public float MarkExplosionRange => markExplosionRange;

        // 2B
        public float Buff2BEnergyCost => buff2BEnergyCost;
        public float Buff2BCooldown => buff2BCooldown;
        public float TerritoryDuration => territoryDuration;
        public float SlowAmount => slowAmount;
        public float SpeedBoostPercent => speedBoostPercent;
        public float EnergyPerHit => energyPerHit;
        public float MaxEnergyGain => maxEnergyGain;

        // 2C
        public float Buff2CEnergyCost => buff2CEnergyCost;
        public float Buff2CCooldown => buff2CCooldown;
        public float AssaultDuration => assaultDuration;
        public int TailCount => tailCount;
        public float AttackInterval => attackInterval;
        public float TailDamagePercent => tailDamagePercent;
    }
}