using System;
using IronWasteland.Core;
using IronWasteland.Core.Effects;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper
{
    public enum CrystalShape
    {
        Circle = 0,   // Tròn: Gây Choáng (Stun) 2s lên Bản sao cùng hệ
        Square = 1,   // Vuông: Trừ 10% Max HP của Bản sao cùng hệ
        Triangle = 2  // Tam giác: Tank nhận Khiên chặn 1 đòn + 25% Xuyên giáp trong 3s
    }

    /// <summary>
    /// Tinh thể nguyên tố sinh ra định kỳ trong Tế Đàn Sub-Arena:
    /// - Không có ColliderOwnerRef trỏ tới Boss (tránh Tank bắn trúng tinh thể gây sát thương nhầm lên Boss).
    /// - Khi Tank chạm vào:
    ///   + Circle: Choáng Bản sao cùng hệ 2s.
    ///   + Square: Trừ 10% Max HP Bản sao cùng hệ.
    ///   + Triangle: Tank nhận Khiên chặn 1 hit + tăng 25% Xuyên giáp trong 3s.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public class ElementalCrystal : MonoBehaviour
    {
        [Header("Crystal Identity")]
        [Tooltip("Hình dạng tinh thể quyết định hiệu ứng (Tròn/Vuông/Tam giác).")]
        [SerializeField] private CrystalShape shape = CrystalShape.Circle;

        [Tooltip("Hệ nguyên tố của tinh thể (Thủy/Độc/Hỏa).")]
        [SerializeField] private ElementalType element = ElementalType.Fire;

        [Header("Effect Durations & Values")]
        [Tooltip("Thời gian làm choáng bản sao (giây) khi nhặt tinh thể hình Tròn.")]
        [SerializeField, Min(0.1f)] private float stunDuration = 2.0f;

        [Tooltip("Tỷ lệ sát thương trừ máu bản sao (0.1 = 10% Max HP) khi nhặt tinh thể hình Vuông.")]
        [SerializeField, Range(0.01f, 1f)] private float damageRatio = 0.10f;

        [Tooltip("Thời gian tồn tại của khiên 1 đòn (giây) khi nhặt tinh thể hình Tam giác.")]
        [SerializeField, Min(0.5f)] private float shieldDuration = 3.0f;

        [Tooltip("Tỷ lệ tăng Xuyên giáp (0.25 = 25%) khi nhặt tinh thể hình Tam giác.")]
        [SerializeField, Range(0.01f, 1f)] private float armorPenRatio = 0.25f;

        [Tooltip("Thời gian tăng Xuyên giáp (giây).")]
        [SerializeField, Min(0.5f)] private float armorPenDuration = 3.0f;

        [Tooltip("Thời gian tự biến mất của tinh thể nếu không ai nhặt (giây).")]
        [SerializeField, Min(1f)] private float lifetime = 12.0f;

        [Header("Visual Components")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private ElementalReaperClone m_TargetClone;
        private float m_Age;
        private bool m_Collected;

        public CrystalShape Shape => shape;
        public ElementalType Element => element;
        public float StunDuration => stunDuration;
        public float DamageRatio => damageRatio;
        public float ShieldDuration => shieldDuration;
        public float ArmorPenRatio => armorPenRatio;
        public ElementalReaperClone TargetClone => m_TargetClone;
        public bool IsCollected => m_Collected;

        private void Awake()
        {
            var col = GetComponent<CircleCollider2D>();
            if (col != null)
            {
                col.isTrigger = true;
                col.radius = 0.8f;
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        public void SetupCrystal(CrystalShape crystalShape, ElementalType elementType, ElementalReaperClone targetClone)
        {
            shape = crystalShape;
            element = elementType;
            m_TargetClone = targetClone;
            m_Age = 0f;
            m_Collected = false;

            UpdateVisualColor();
        }

        private void UpdateVisualColor()
        {
            if (spriteRenderer == null) return;

            Color c;
            switch (element)
            {
                case ElementalType.Water:
                    c = new Color(0f, 0.75f, 1f, 0.95f); // Lam Thủy
                    break;
                case ElementalType.Poison:
                    c = new Color(0.3f, 0.85f, 0.2f, 0.95f); // Lục Độc
                    break;
                case ElementalType.Fire:
                default:
                    c = new Color(1f, 0.3f, 0f, 0.95f); // Đỏ Cam Hỏa
                    break;
            }
            spriteRenderer.color = c;
        }

        private void Update()
        {
            m_Age += Time.deltaTime;
            if (m_Age >= lifetime && !m_Collected)
            {
                Despawn();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (m_Collected || other == null) return;

            TankBrain tank = ResolveTank(other);
            if (tank != null && tank.IsAlive)
            {
                Collect(tank);
            }
        }

        public void Collect(TankBrain tank)
        {
            if (m_Collected || tank == null) return;
            m_Collected = true;

            switch (shape)
            {
                case CrystalShape.Circle:
                    if (m_TargetClone != null && m_TargetClone.IsAlive)
                    {
                        m_TargetClone.ApplyCrowdControl(CrowdControlType.Stun, stunDuration);
                    }
                    break;

                case CrystalShape.Square:
                    if (m_TargetClone != null && m_TargetClone.IsAlive)
                    {
                        float dmg = m_TargetClone.MaxHealth * damageRatio;
                        m_TargetClone.TakeDamage(new DamageInfo { RawDamage = dmg, ArmorPenetration = 1.0f, Target = m_TargetClone });
                    }
                    break;

                case CrystalShape.Triangle:
                    tank.AddShield(9999f, shieldDuration, $"CrystalShield_{Guid.NewGuid():N}", this, "Khiên Tinh Thể", maxAbsorbHits: 1);
                    var armorPenBuff = new StatModifierEffect(
                        $"CrystalArmorPen_{Guid.NewGuid():N}",
                        "Xuyên Giáp Tinh Thể",
                        armorPenDuration,
                        flatModifier: new TankStats { ArmorPenetration = armorPenRatio }
                    );
                    tank.ApplyEffect(armorPenBuff);
                    break;
            }

            Despawn();
        }

        private void Despawn()
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private TankBrain ResolveTank(Collider2D col)
        {
            if (col == null) return null;
            if (col.TryGetComponent<ColliderOwnerRef>(out var ownerRef) && ownerRef != null)
            {
                if (ownerRef.Owner is TankBrain tb) return tb;
            }
            return col.GetComponentInParent<TankBrain>();
        }
    }
}
