using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    public enum Skill2Variant
    {
        Variant_2A, // Mark Explosion
        Variant_2B, // Fox Territory
        Variant_2C  // Nine-Tailed Assault: Cửu Vĩ bộc phát
    }

    [DisallowMultipleComponent]
    public class GreenFox_Skill2Sub : MonoBehaviour
    {
        [Header("Variant Selection")]
        [SerializeField] private Skill2Variant activeVariant = Skill2Variant.Variant_2C;

        [Header("2A - Mark Explosion Settings")]
        [SerializeField, Min(0.1f)] private float buff2AEnergyCost = 55f;
        [SerializeField, Min(0.01f)] private float buff2ACooldown = 12f;
        [SerializeField, Min(0f)] private float damagePerStackPercent = 20f;
        [SerializeField, Min(0.1f)] private float attackBuffDuration = 6f;
        [SerializeField, Range(0f, 2f)] private float attackBuffPercent = 0.3f;
        [SerializeField, Min(1f)] private float attackMultiplier = 1.3f;

        [Header("2B - Fox Territory Settings")]
        [SerializeField, Min(0.1f)] private float buff2BEnergyCost = 60f;
        [SerializeField, Min(0.01f)] private float buff2BCooldown = 15f;
        [SerializeField, Min(0.1f)] private float territoryDuration = 6f;
        [SerializeField, Range(0.1f, 0.9f)] private float slowAmount = 0.3f;
        [SerializeField, Range(0.1f, 2.0f)] private float speedBoostPercent = 0.3f;
        [SerializeField, Min(0f)] private float energyPerHit = 3f;
        [SerializeField, Min(0f)] private float maxEnergyGain = 30f;

        [Header("2C - Nine-Tailed Assault Settings (Cửu Vĩ)")]
        [SerializeField, Min(0.1f)] private float buff2CEnergyCost = 75f;       // Năng lượng tiêu hao: 75
        [SerializeField, Min(0.01f)] private float buff2CCooldown = 16f;      // Thời gian hồi: 16s
        [SerializeField, Min(0.1f)] private float assaultDuration = 6f;       // Thời gian duy trì: 6s
        [SerializeField, Min(0.01f)] private float attackInterval = 1f;       // Giãn cách tấn công mỗi đuôi: 1s
        [SerializeField, Min(0f)] private float tailDamagePercent = 15f;        // Giá trị sát thương mỗi đuôi: 15% ATK
        [SerializeField, Min(0.1f)] private float attackRange = 6f;

        public Skill2Variant ActiveVariant => activeVariant;

        // 2A Getters
        public float Buff2AEnergyCost => buff2AEnergyCost;
        public float Buff2ACooldown => buff2ACooldown;
        public float DamagePerStackPercent => damagePerStackPercent;
        public float AttackBuffDuration => attackBuffDuration;
        public float AttackBuffPercent => attackBuffPercent;
        public float AttackMultiplier => attackMultiplier;

        // 2B Getters
        public float Buff2BEnergyCost => buff2BEnergyCost;
        public float Buff2BCooldown => buff2BCooldown;
        public float TerritoryDuration => territoryDuration;
        public float SlowAmount => slowAmount;
        public float SpeedBoostPercent => speedBoostPercent;
        public float EnergyPerHit => energyPerHit;
        public float MaxEnergyGain => maxEnergyGain;

        // 2C Getters
        public float Buff2CEnergyCost => buff2CEnergyCost;
        public float Buff2CCooldown => buff2CCooldown;
        public float AssaultDuration => assaultDuration;
        public float AttackInterval => attackInterval;
        public float TailDamagePercent => tailDamagePercent;
        public float AttackRange => attackRange;
    }
}