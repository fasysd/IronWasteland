using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    public enum Skill1WaterVariant
    {
        SharedHealing,
        LifeDrain,
        BubbleProtector // Hoặc BubbleProtector
    }

    [DisallowMultipleComponent]
    public class WaterB_Skill1Sub : MonoBehaviour
    {
        [Header("Variant Selection")]
        [SerializeField] private Skill1WaterVariant activeVariant = Skill1WaterVariant.SharedHealing;

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

        public Skill1WaterVariant ActiveVariant => activeVariant;

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