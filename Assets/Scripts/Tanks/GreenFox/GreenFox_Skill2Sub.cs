using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    public enum Skill2Variant
    {
        Variant_2A, // Mark Explosion: Nổ dấu ấn + Buff công cho Green Fox
        Variant_2B, // Fox Territory: Vùng lãnh địa ma thuật (Làm chậm enemy, tăng tốc tank, gây dame)
        Variant_2C  // Nine-Tailed Assault: 9 đuôi tự quất kẻ địch xung quanh (x2 dame nếu bị thiêu đốt)
    }

    [DisallowMultipleComponent]
    public class GreenFox_Skill2Sub : MonoBehaviour
    {
        [Header("Variant Selection")]
        [SerializeField] private Skill2Variant activeVariant = Skill2Variant.Variant_2A;

        [Header("2A - Mark Explosion Settings")]
        [SerializeField, Min(0.1f)] private float attackBuffDuration = 5f;
        [SerializeField, Min(1f)] private float attackMultiplier = 1.3f; // Tăng 30% dame sau khi nổ

        [Header("2B - Fox Territory Settings")]
        [SerializeField, Min(0.1f)] private float territoryDuration = 6f;
        [SerializeField, Min(0.1f)] private float territoryRadius = 4f;
        [SerializeField, Range(0.1f, 0.9f)] private float slowAmount = 0.3f; // Làm chậm 30%
        [SerializeField, Min(1f)] private float speedBoostMultiplier = 1.25f; // Tăng 25% tốc chạy
        [SerializeField, Min(0f)] private float territoryDps = 15f;

        [Header("2C - Nine-Tailed Assault Settings")]
        [SerializeField, Min(0.1f)] private float assaultDuration = 5f;
        [SerializeField, Min(0.1f)] private float attackInterval = 0.4f;
        [SerializeField, Min(0.1f)] private float attackRange = 6f;
        [SerializeField, Min(0f)] private float tailDamage = 20f;

        public Skill2Variant ActiveVariant => activeVariant;

        // 2A Getters
        public float AttackBuffDuration => attackBuffDuration;
        public float AttackMultiplier => attackMultiplier;

        // 2B Getters
        public float TerritoryDuration => territoryDuration;
        public float TerritoryRadius => territoryRadius;
        public float SlowAmount => slowAmount;
        public float SpeedBoostMultiplier => speedBoostMultiplier;
        public float TerritoryDps => territoryDps;

        // 2C Getters
        public float AssaultDuration => assaultDuration;
        public float AttackInterval => attackInterval;
        public float AttackRange => attackRange;
        public float TailDamage => tailDamage;
    }
}