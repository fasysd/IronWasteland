using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    public enum Skill2WaterVariant
    {
        MobileAltar,   // Đài tế thủy triều di động (Hồi máu/kéo dài aura quanh tank)
        WaterTurrets,  // Triệu hồi tháp nước tấn công & hồi năng lượng
        AquaAssault    // Chuyển hóa tấn công mạnh, giảm cooldown, tự hồi máu
    }

    [DisallowMultipleComponent]
    public class WaterB_Skill2Sub : MonoBehaviour
    {
        [Header("Variant Selection")]
        [SerializeField] private Skill2WaterVariant activeVariant = Skill2WaterVariant.MobileAltar;

        [Header("Mobile Altar Settings")]
        [SerializeField, Min(0.1f)] private float altarDuration = 8f;
        [SerializeField, Min(0.1f)] private float altarInterval = 1f;
        [SerializeField, Min(0f)] private float altarHealPerTick = 20f;

        [Header("Water Turrets Settings")]
        [SerializeField, Min(0.1f)] private float turretDuration = 10f;
        [SerializeField, Min(0.1f)] private float turretAttackInterval = 1.5f;
        [SerializeField, Min(0f)] private float turretDamage = 15f;
        [SerializeField, Min(0f)] private float energyRegenPerShot = 5f;

        [Header("Aqua Assault Settings")]
        [SerializeField, Min(0.1f)] private float assaultDuration = 6f;
        [SerializeField, Range(0.1f, 0.9f)] private float cooldownMultiplier = 0.6f; // Giảm 40% cooldown
        [SerializeField, Range(0.1f, 1f)] private float selfHealOnHitPercent = 0.2f;

        public Skill2WaterVariant ActiveVariant => activeVariant;

        // Getters Altar
        public float AltarDuration => altarDuration;
        public float AltarInterval => altarInterval;
        public float AltarHealPerTick => altarHealPerTick;

        // Getters Turrets
        public float TurretDuration => turretDuration;
        public float TurretAttackInterval => turretAttackInterval;
        public float TurretDamage => turretDamage;
        public float EnergyRegenPerShot => energyRegenPerShot;

        // Getters Assault
        public float AssaultDuration => assaultDuration;
        public float CooldownMultiplier => cooldownMultiplier;
        public float SelfHealOnHitPercent => selfHealOnHitPercent;
    }
}