using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Biến thể của Skill 1 phụ. Được chọn bởi TankBrainContext.OptionSkill1
    /// (mapping nằm trong TankBrain_WaterB.GetSkill1Variant()).
    /// </summary>
    public enum Skill1WaterVariant
    {
        SharedHealing,   // OptionSkill1 = 0 (mặc định)
        LifeDrain,       // OptionSkill1 = 1
        BubbleProtector  // OptionSkill1 = 2
    }

    /// <summary>
    /// Chỉ chứa thông số của các biến thể Skill 1 phụ.
    /// Việc chọn biến thể nào do TankBrain_WaterB quyết định qua Context.OptionSkill1.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterB_Skill1Sub : MonoBehaviour
    {
        [Header("Shared Healing Settings")]
        [SerializeField, Range(0.1f, 1f)] private float sharedPercent = 0.5f;
        [SerializeField] private WaterB_SharedHealingLink sharedHealingLinkPrefab;
        [SerializeField, Min(0.1f)] private float sharedHealingDuration = 10f;
        [SerializeField, Min(0.1f)] private float maxSelectRange = 10f;

        [Header("Life Drain Settings")]
        [SerializeField, Range(0.1f, 1f)] private float drainPercent = 0.3f;
        [SerializeField, Min(0.1f)] private float drainDuration = 6f;

        [Header("Bubble Protector Settings")]
        [SerializeField, Min(0.1f)] private float domeDuration = 6f;
        [SerializeField, Min(0.1f)] private float domeRadius = 5f;
        [SerializeField, Min(0f)] private float domeHealOnEnd = 40f;
        [SerializeField, Min(0f)] private float domeDefenseBonus = 1.0f; // +100% Defense

        public float SharedPercent => sharedPercent;
        public WaterB_SharedHealingLink SharedHealingLinkPrefab => sharedHealingLinkPrefab;
        public float SharedHealingDuration => sharedHealingDuration;
        public float MaxSelectRange => maxSelectRange;

        public float DrainPercent => drainPercent;
        public float DrainDuration => drainDuration;

        public float DomeDuration => domeDuration;
        public float DomeRadius => domeRadius;
        public float DomeHealOnEnd => domeHealOnEnd;
        public float DomeDefenseBonus => domeDefenseBonus;
    }
}