using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    public enum Skill1Variant
    {
        Variant_1A, // Tăng tích ấn + 150% dame nổ trong 8s
        Variant_1B, // Thiêu đốt ma thuật (DOT 10s, 10%/s)
        Variant_1C  // Bẫy ma thuật (1500% dame, trói chân, x2 dame nếu đang bị thiêu đốt)
    }

    [DisallowMultipleComponent]
    public class GreenFox_Skill1Sub : MonoBehaviour
    {
        [Header("Variant Selection")]
        [SerializeField] private Skill1Variant activeVariant = Skill1Variant.Variant_1A;

        [Header("1A - Overload Buff Settings")]
        [SerializeField, Min(0.1f)] private float buff1ADuration = 8f;

        [Header("1B - Burn Effect Settings")]
        [SerializeField, Min(0.1f)] private float burnDuration = 10f;
        [SerializeField, Min(0.01f)] private float burnInterval = 1f;
        [SerializeField, Range(0f, 1f)] private float burnDamagePercent = 0.10f; // 10% dame

        [Header("1C - Magic Trap Settings")]
        [SerializeField, Min(0.1f)] private float trapDuration = 15f; // Thời gian bẫy tồn tại dưới đất
        [SerializeField, Min(0.1f)] private float rootDuration = 2f;  // Thời gian trói chân
        [SerializeField, Min(0f)] private float trapDamagePercent = 15f; // 1500% dame

        public Skill1Variant ActiveVariant => activeVariant;
        public float Buff1ADuration => buff1ADuration;
        public float BurnDuration => burnDuration;
        public float BurnInterval => burnInterval;
        public float BurnDamagePercent => burnDamagePercent;
        public float TrapDamagePercent => trapDamagePercent;
        public float RootDuration => rootDuration;
    }
}