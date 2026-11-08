using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    /// <summary>
    /// Biến thể của Kỹ năng tùy chọn 1. Được chọn bởi TankBrainContext.OptionSkill1
    /// (mapping nằm trong TankBrain_GreenFox.GetSkill1Variant()).
    /// </summary>
    public enum Skill1Variant
    {
        Variant_1A, // OptionSkill1 = 0 (mặc định) - Mark Hunter
        Variant_1B, // OptionSkill1 = 1 - Burning Fox
        Variant_1C  // OptionSkill1 = 2 - Fox Trap
    }

    /// <summary>Thông số (theo GDD) của các biến thể Kỹ năng tùy chọn 1.</summary>
    [DisallowMultipleComponent]
    public class GreenFox_Skill1Sub : MonoBehaviour
    {
        [Header("1A - Mark Hunter")]
        [SerializeField, Min(0f)] private float buff1AEnergyCost = 45f;
        [SerializeField, Min(0.01f)] private float buff1ACooldown = 10f;
        [SerializeField, Min(0.1f)] private float buff1ADuration = 5f;
        [SerializeField, Min(1)] private int buff1AStackAmount = 2;                    // Cộng dồn mỗi đòn: 2
        [SerializeField, Min(0f)] private float buff1AExplosionDamagePercent = 300f;  // Nổ ấn: 300% ATK (thay cho 200%)
        [SerializeField, Min(0f)] private float buff1ACooldownReductionOnKill = 3f;   // Hạ gục: giảm hồi Fox Mark 3s

        [Header("1B - Burning Fox")]
        [SerializeField, Min(0f)] private float buff1BEnergyCost = 40f;
        [SerializeField, Min(0.01f)] private float buff1BCooldown = 11f;
        [SerializeField, Min(0.1f)] private float burnDuration = 6f;                   // Thời gian duy trì buff & mỗi hiệu ứng cháy
        [SerializeField, Min(0.01f)] private float burnInterval = 0.5f;
        [SerializeField, Range(0f, 2f)] private float burnDamagePercent = 0.25f;       // 25% Tấn công mỗi nhịp
        [SerializeField, Min(0f)] private float energyOnHitBurnTarget = 3f;            // Fox Fang trúng mục tiêu đang cháy: +3 năng lượng

        [Header("1C - Fox Trap")]
        [SerializeField, Min(0f)] private float buff1CEnergyCost = 50f;
        [SerializeField, Min(0.01f)] private float buff1CCooldown = 10f;
        [SerializeField, Min(0.1f)] private float trapDuration = 15f;
        [SerializeField, Min(0.1f)] private float rootDuration = 2f;
        [SerializeField, Min(0f)] private float trapDamagePercent = 150f;              // 150% ATK
        [SerializeField, Min(0f)] private float eliteTrapDamagePercent = 230f;         // Tinh anh: 230% ATK thay cho trói chân

        // 1A
        public float Buff1AEnergyCost => buff1AEnergyCost;
        public float Buff1ACooldown => buff1ACooldown;
        public float Buff1ADuration => buff1ADuration;
        public int Buff1AStackAmount => buff1AStackAmount;
        public float Buff1AExplosionDamagePercent => buff1AExplosionDamagePercent;
        public float Buff1ACooldownReductionOnKill => buff1ACooldownReductionOnKill;

        // 1B
        public float Buff1BEnergyCost => buff1BEnergyCost;
        public float Buff1BCooldown => buff1BCooldown;
        public float BurnDuration => burnDuration;
        public float BurnInterval => burnInterval;
        public float BurnDamagePercent => burnDamagePercent;
        public float EnergyOnHitBurnTarget => energyOnHitBurnTarget;

        // 1C
        public float Buff1CEnergyCost => buff1CEnergyCost;
        public float Buff1CCooldown => buff1CCooldown;
        public float TrapDuration => trapDuration;
        public float RootDuration => rootDuration;
        public float TrapDamagePercent => trapDamagePercent;
        public float EliteTrapDamagePercent => eliteTrapDamagePercent;
    }
}