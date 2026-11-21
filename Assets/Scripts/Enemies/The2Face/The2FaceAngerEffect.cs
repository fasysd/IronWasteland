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
            float damageBonus,
            float energyDrainRatio,
            Object source)
            : base("The2Face_Anger", "Tức giận", duration)
        {
            m_DamageBonus = Mathf.Max(0f, damageBonus);
            m_EnergyDrainRatio = Mathf.Clamp01(energyDrainRatio);
            m_Source = source;
        }

        public override void OnApplied(Object target)
        {
            m_Tank = target as TankBrain;
            if (m_Tank == null) return;

            m_Tank.OnTakeDamageEvent += HandleTankTookDamage;

            float drain = m_Tank.CurrentEnergy * m_EnergyDrainRatio;
            if (drain > 0f) m_Tank.SpendEnergy(drain);
        }

        public override void OnRemoved(Object target)
        {
            if (m_Tank != null)
            {
                m_Tank.OnTakeDamageEvent -= HandleTankTookDamage;
            }
        }

        private void HandleTankTookDamage(DamageInfo info)
        {
            if (m_ApplyingExtraDamage || m_Tank == null || !m_Tank.IsAlive) return;

            float extraDamage = Mathf.Max(0f, info.HealthDamage) * m_DamageBonus;
            if (extraDamage <= 0f) return;

            m_ApplyingExtraDamage = true;
            DamageInfo extraInfo = new DamageInfo(
                m_Source,
                m_Tank,
                extraDamage,
                1f,
                info.HitPosition,
                true,
                false,
                false,
                DamageSourceType.EnemyAttack);
            m_Tank.TakeDamage(extraInfo);
            m_ApplyingExtraDamage = false;
        }
    }
}
