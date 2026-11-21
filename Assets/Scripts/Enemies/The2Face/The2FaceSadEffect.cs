using IronWasteland.Core.Effects;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.The2Face
{
    /// <summary>
    /// Hiệu ứng Buồn bã của TuNhiet:
    /// - Giảm tốc độ di chuyển.
    /// - Giảm tấn công và phòng thủ theo phần trăm.
    /// </summary>
    public class The2FaceSadEffect : StatusEffect
    {
        private const string StatKey = "The2Face_Sad_Stats";

        private readonly float m_SlowPercent;
        private readonly float m_AttackReduction;
        private readonly float m_DefenseReduction;
        private TankBrain m_Tank;

        public The2FaceSadEffect(
            float duration,
            float slowPercent,
            float attackReduction,
            float defenseReduction)
            : base("The2Face_Sad", "Buồn bã", duration)
        {
            m_SlowPercent = Mathf.Clamp01(slowPercent);
            m_AttackReduction = Mathf.Clamp01(attackReduction);
            m_DefenseReduction = Mathf.Clamp01(defenseReduction);
        }

        public override void OnApplied(Object target)
        {
            m_Tank = target as TankBrain;
            ApplyStats();
            TankSlowDebuff.Apply(m_Tank, m_SlowPercent, Duration);
        }

        public override void OnStackAdded(Object target, int newStacks)
        {
            base.OnStackAdded(target, newStacks);
            ApplyStats();
            TankSlowDebuff.Apply(m_Tank, m_SlowPercent, Duration);
        }

        public override void OnRemoved(Object target)
        {
            if (m_Tank != null) m_Tank.RemoveBonusStats(StatKey);
        }

        private void ApplyStats()
        {
            if (m_Tank == null) return;

            TankStats delta = TankStats.Zeroed();
            delta.Attack = -m_AttackReduction;
            delta.Defense = -m_DefenseReduction;
            m_Tank.AddBonusStats(StatKey, null, delta);
        }
    }
}
