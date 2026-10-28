using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    public enum Skill1Variant
    {
        Variant_1A, // Mark Hunter
        Variant_1B, // Thiêu đốt ma thuật
        Variant_1C  // Bẫy ma thuật
    }

    [DisallowMultipleComponent]
    public class GreenFox_Skill1Sub : MonoBehaviour
    {
        [Header("Variant Selection")]
        [SerializeField] private Skill1Variant activeVariant = Skill1Variant.Variant_1C;

        [Header("1A - Mark Hunter Settings")]
        [SerializeField, Min(0.1f)] private float buff1ADuration = 5f;
        [SerializeField, Min(0.01f)] private float buff1ACooldown = 10f;
        [SerializeField, Min(0)] private float buff1AEnergyCost = 45f;
        [SerializeField, Min(1)] private int buff1AStackAmount = 2;
        [SerializeField, Min(0f)] private float buff1AExplosionBonusDamage = 300f;
        [SerializeField, Min(0f)] private float buff1ACooldownReductionOnKill = 3f;

        [Header("1B - Burn Effect Settings (Variant 1B)")]
        [SerializeField, Min(0.1f)] private float buff1BEnergyCost = 40f;         // Năng lượng tiêu hao: 40
        [SerializeField, Min(0.01f)] private float buff1BCooldown = 11f;        // Thời gian hồi: 11s
        [SerializeField, Min(0.1f)] private float burnDuration = 6f;            // Thời gian duy trì hiệu ứng thiêu đốt: 6s
        [SerializeField, Min(0.01f)] private float burnInterval = 0.5f;         // Giãn cách gây sát thương: 0.5s
        [SerializeField, Range(0f, 2f)] private float burnDamagePercent = 0.25f;  // Sát thương thiêu đốt: 25% tấn công
        [SerializeField, Min(0f)] private float energyOnHitBurnTarget = 3f;     // Năng lượng hồi khi Fox Fang đánh trúng mục tiêu có cháy: 3 Năng lượng

        [Header("1C - Magic Trap Settings (Fox Trap)")]
        [SerializeField, Min(0.1f)] private float buff1CEnergyCost = 50f;          // Năng lượng tiêu hao: 50
        [SerializeField, Min(0.01f)] private float buff1CCooldown = 10f;         // Thời gian hồi: 10s
        [SerializeField, Min(0.1f)] private float trapDuration = 15f;            // Thời gian tồn tại bẫy: 15s
        [SerializeField, Min(0.1f)] private float rootDuration = 2f;             // Thời gian trói chân: 2s
        [SerializeField, Min(0f)] private float trapDamagePercent = 150f;        // Giá trị sát thương cơ bản: 150% ATK
        [SerializeField, Min(0f)] private float eliteTrapDamagePercent = 230f;   // Giá trị sát thương thay cho trói chân Tinh anh: 230% ATK

        public Skill1Variant ActiveVariant => activeVariant;

        // 1A Getters
        public float Buff1ADuration => buff1ADuration;
        public float Buff1ACooldown => buff1ACooldown;
        public float Buff1AEnergyCost => buff1AEnergyCost;
        public int Buff1AStackAmount => buff1AStackAmount;
        public float Buff1AExplosionBonusDamage => buff1AExplosionBonusDamage;
        public float Buff1ACooldownReductionOnKill => buff1ACooldownReductionOnKill;

        // 1B Getters
        public float Buff1BEnergyCost => buff1BEnergyCost;
        public float Buff1BCooldown => buff1BCooldown;
        public float BurnDuration => burnDuration;
        public float BurnInterval => burnInterval;
        public float BurnDamagePercent => burnDamagePercent;
        public float EnergyOnHitBurnTarget => energyOnHitBurnTarget;

        // 1C Getters
        public float Buff1CEnergyCost => buff1CEnergyCost;
        public float Buff1CCooldown => buff1CCooldown;
        public float TrapDuration => trapDuration;
        public float RootDuration => rootDuration;
        public float TrapDamagePercent => trapDamagePercent;
        public float EliteTrapDamagePercent => eliteTrapDamagePercent;
    }
}