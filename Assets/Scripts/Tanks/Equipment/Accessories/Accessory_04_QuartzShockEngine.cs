using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 4 (STT 5 GDD): Động Cơ Xung Kích Tinh Thể Thạch Anh.
    /// - Chỉ số nền: +15% Tấn công, +25% Tỉ lệ xuyên giáp.
    /// - Nội tại: Bắn liên tiếp vào cùng 1 mục tiêu trong 2s giúp tăng sát thương gây ra (+5% mỗi lần, tối đa +20%, 4 tầng).
    ///   Đổi mục tiêu hoặc hết 2s không trúng sẽ đặt lại cộng dồn.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_04_QuartzShockEngine : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_04_QuartzShockEngine_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseAttackMultiplier = 0.15f;
        [SerializeField, Range(0f, 1f)] private float baseArmorPenetrationMultiplier = 0.25f;

        [Header("Nội tại Tập trung hỏa lực")]
        [SerializeField, Min(0.5f)] private float resetWindowDuration = 2.0f;
        [SerializeField, Range(0f, 0.2f)] private float damageBonusPerStack = 0.05f;
        [SerializeField, Range(1, 10)] private int maxStacks = 4;

        private const string BuffKey = "Acc04_QuartzShock";
        private UnityEngine.Object m_LastTarget;
        private int m_CurrentStacks;
        private float m_Timer;


                protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { Attack = baseAttackMultiplier, ArmorPenetration = baseArmorPenetrationMultiplier });
            }
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            ResetStacks();
            base.OnUnequipped(owner);
        }

        public override void OnOwnerDealtDamage(DamageInfo info)
        {
            if (Owner == null || !Owner.IsAlive) return;
            if (info.Target == null) return;

            if (info.Target == m_LastTarget)
            {
                // Trúng cùng mục tiêu trong khung thời gian 2s
                m_Timer = resetWindowDuration;
                if (m_CurrentStacks < maxStacks)
                {
                    m_CurrentStacks++;
                    UpdateBuffStats();
                }
            }
            else
            {
                // Trúng mục tiêu mới -> reset stack và bắt đầu tích lũy cho mục tiêu này
                m_LastTarget = info.Target;
                m_CurrentStacks = 1;
                m_Timer = resetWindowDuration;
                UpdateBuffStats();
            }
        }

        public override void OnBrainTick(float deltaTime)
        {
            if (m_CurrentStacks <= 0) return;

            m_Timer -= deltaTime;
            if (m_Timer <= 0f)
            {
                ResetStacks();
            }
        }

        private void UpdateBuffStats()
        {
            float totalBonus = m_CurrentStacks * damageBonusPerStack;
            Owner.AddBonusStats(BuffKey, null, new TankStats { DamageRate = totalBonus });
        }

        private void ResetStacks()
        {
            m_CurrentStacks = 0;
            m_LastTarget = null;
            m_Timer = 0f;
            if (Owner != null)
            {
                Owner.RemoveBonusStats(BuffKey);
            }
        }
    }
}
