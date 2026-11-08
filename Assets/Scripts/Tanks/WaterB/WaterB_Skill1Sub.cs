using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Biến thể của Kỹ năng tùy chọn 1. Được chọn bởi TankBrainContext.OptionSkill1
    /// (mapping nằm trong TankBrain_WaterB.GetSkill1Variant()).
    /// </summary>
    public enum Skill1WaterVariant
    {
        SharedHealing,   // OptionSkill1 = 0 (mặc định)
        LifeDrain,       // OptionSkill1 = 1
        BubbleProtector  // OptionSkill1 = 2
    }

    /// <summary>
    /// Chỉ chứa thông số (theo GDD) của các biến thể Kỹ năng tùy chọn 1.
    /// Các giá trị dạng "Ratio" là hệ số nhân với Tấn công / Giới hạn máu của Water B tại thời điểm tung chiêu.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterB_Skill1Sub : MonoBehaviour
    {
        [Header("Shared Healing")]
        [SerializeField, Min(0f)] private float sharedEnergyCost = 50f;
        [SerializeField, Min(0.1f)] private float sharedCooldown = 12f;
        [SerializeField, Min(0.1f)] private float sharedHealingDuration = 10f;
        [SerializeField, Range(0.1f, 1f)] private float sharedPercent = 0.5f;
        [SerializeField, Min(0.1f)] private float maxSelectRange = 10f;
        [SerializeField] private WaterB_SharedHealingLink sharedHealingLinkPrefab;

        [Header("Life Drain")]
        [SerializeField, Min(0f)] private float drainEnergyCost = 45f;
        [SerializeField, Min(0.1f)] private float drainCooldown = 11f;
        [SerializeField, Min(0.1f)] private float drainDuration = 5f;
        [SerializeField, Min(0f)] private float drainAttackBonusPercent = 0.25f; // +25% Tấn công
        [SerializeField, Range(0.05f, 1f)] private float drainPercent = 0.35f;   // 35% sát thương -> hồi máu
        [SerializeField, Min(1f)] private float drainSearchRadius = 10f;         // Bán kính tìm đồng minh máu thấp nhất

        [Header("Bubble Protector")]
        [SerializeField, Min(0f)] private float domeEnergyCost = 45f;
        [SerializeField, Min(0.1f)] private float domeCooldown = 11f;
        [SerializeField, Min(0.1f)] private float domeDuration = 6f;
        [SerializeField, Min(0f)] private float domeDefenseBonus = 25f;          // +25 Phòng thủ (cộng phẳng)
        [SerializeField, Min(0f)] private float domeAttackBonusPercent = 0.25f;  // +25% Tấn công
        [SerializeField, Min(0f)] private float domeHealAtkRatio = 1.0f;         // 100% Tấn công
        [SerializeField, Min(0f)] private float domeHealMaxHpRatio = 0.06f;      // 6% Giới hạn máu Water B

        // Chung theo biến thể
        public float GetEnergyCost(Skill1WaterVariant variant)
        {
            switch (variant)
            {
                case Skill1WaterVariant.LifeDrain: return drainEnergyCost;
                case Skill1WaterVariant.BubbleProtector: return domeEnergyCost;
                default: return sharedEnergyCost;
            }
        }

        public float GetCooldown(Skill1WaterVariant variant)
        {
            switch (variant)
            {
                case Skill1WaterVariant.LifeDrain: return drainCooldown;
                case Skill1WaterVariant.BubbleProtector: return domeCooldown;
                default: return sharedCooldown;
            }
        }

        // Shared Healing
        public float SharedPercent => sharedPercent;
        public WaterB_SharedHealingLink SharedHealingLinkPrefab => sharedHealingLinkPrefab;
        public float SharedHealingDuration => sharedHealingDuration;
        public float MaxSelectRange => maxSelectRange;

        // Life Drain
        public float DrainPercent => drainPercent;
        public float DrainDuration => drainDuration;
        public float DrainAttackBonusPercent => drainAttackBonusPercent;
        public float DrainSearchRadius => drainSearchRadius;

        // Bubble Protector
        public float DomeDuration => domeDuration;
        public float DomeDefenseBonus => domeDefenseBonus;
        public float DomeAttackBonusPercent => domeAttackBonusPercent;
        public float DomeHealAtkRatio => domeHealAtkRatio;
        public float DomeHealMaxHpRatio => domeHealMaxHpRatio;
    }
}