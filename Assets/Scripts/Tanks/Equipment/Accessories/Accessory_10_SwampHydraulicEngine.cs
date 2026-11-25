using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 10 (STT 11 GDD): Động Cơ Thủy Lực Ma Pháp Đầm Lầy.
    /// - Chỉ số nền: +30% Phòng thủ, +10% Tỷ lệ giảm hồi chiêu.
    /// - Nội tại: Nhận sát thương giúp cộng dồn giảm sát thương (3%/tầng, tối đa 18% = 6 tầng);
    ///   đủ 6 tầng được miễn nhiễm làm chậm và 15% Tỷ lệ giảm hồi chiêu trong 5s. Hết hạn xóa hết cộng dồn.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_10_SwampHydraulicEngine : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_10_SwampHydraulicEngine_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseDefenseMultiplier = 0.30f;
        [SerializeField, Range(0f, 1f)] private float baseCooldownReduction = 0.10f;

        [Header("Nội tại Thủy Lực Đầm Lầy")]
        [SerializeField, Range(0f, 0.1f)] private float damageReductionPerStack = 0.03f;
        [SerializeField, Range(1, 10)] private int maxStacks = 6;
        [SerializeField, Range(0f, 0.5f)] private float empoweredBonusCooldownReduction = 0.15f;
        [SerializeField, Min(1f)] private float empoweredDuration = 5.0f;

        private const string EmpBuffKey = "Acc10_Swamp_Empowered";
        private int m_CurrentStacks;
        private bool m_IsEmpowered;
        private float m_EmpoweredTimer;


                protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { Defense = baseDefenseMultiplier, CooldownReduction = baseCooldownReduction });
            }
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            ResetAllStacks();
            base.OnUnequipped(owner);
        }

        public override void OnOwnerTookDamage(DamageInfo info)
        {
            if (Owner == null || !Owner.IsAlive) return;
            if (m_IsEmpowered) return;

            if (m_CurrentStacks < maxStacks)
            {
                m_CurrentStacks++;
                if (m_CurrentStacks >= maxStacks)
                {
                    ActivateEmpoweredState();
                }
            }
        }

        public override float GetIncomingDamageMultiplier(DamageInfo info)
        {
            if (m_CurrentStacks <= 0) return 1f;
            return Mathf.Clamp01(1f - m_CurrentStacks * damageReductionPerStack);
        }

        public override void OnBrainTick(float deltaTime)
        {
            if (!m_IsEmpowered) return;

            // Miễn nhiễm làm chậm trong suốt thời gian cường hóa
            Owner?.RemoveCrowdControl(CrowdControlType.Slow);

            m_EmpoweredTimer -= deltaTime;
            if (m_EmpoweredTimer <= 0f)
            {
                ResetAllStacks();
            }
        }

        private void ActivateEmpoweredState()
        {
            m_IsEmpowered = true;
            m_EmpoweredTimer = empoweredDuration;

            // Đủ 6 tầng: Miễn nhiễm làm chậm và +15% CooldownReduction trong 5s
            Owner.AddBonusStats(EmpBuffKey, null, new TankStats { CooldownReduction = empoweredBonusCooldownReduction });
        }

        private void ResetAllStacks()
        {
            if (m_IsEmpowered && Owner != null)
            {
                Owner.RemoveBonusStats(EmpBuffKey);
            }
            m_IsEmpowered = false;
            m_CurrentStacks = 0;
            m_EmpoweredTimer = 0f;
        }
    }
}
