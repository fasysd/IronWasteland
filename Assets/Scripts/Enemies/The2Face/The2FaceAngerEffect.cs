using IronWasteland.Core;
using IronWasteland.Core.Effects;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.The2Face
{
    /// <summary>
    /// Hiệu ứng Tức giận của TuNhiet:
    /// - Tăng sát thương Tank nhận vào trong thời gian hiệu lực.
    /// - Rút một phần năng lượng hiện tại ngay khi bị gán.
    /// TankBrain không có chỉ số "sát thương nhận vào" nên hiệu ứng lắng nghe
    /// OnTakeDamageEvent rồi cộng thêm phần sát thương tương ứng.
    /// </summary>
    public class The2FaceAngerEffect : StatusEffect
    {
        private readonly float m_DamageBonus;
        private readonly float m_EnergyDrainRatio;
        private readonly Object m_Source;
        private TankBrain m_Tank;
        private bool m_ApplyingExtraDamage;

        public The2FaceAngerEffect(
            float duration,
            float energyDrainRatio,
            Object source)
            : base("The2Face_Anger", "Tức giận", duration)
        {
            m_EnergyDrainRatio = Mathf.Clamp01(energyDrainRatio);
            m_Source = source;
        }

        public override void OnApplied(Object target)
        {
            m_Tank = target as TankBrain;
            if (m_Tank == null) return;

            float drain = m_Tank.CurrentEnergy * m_EnergyDrainRatio;
            if (drain > 0f) m_Tank.SpendEnergy(drain);
        }
    }
}
