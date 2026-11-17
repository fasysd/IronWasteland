using IronWasteland.Core;
using IronWasteland.Core.Effects;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 9 (STT 10 GDD): Lõi Nhiệt Hạch Ba Lõi.
    /// - Chỉ số nền: +20% Tấn công, +10% Tỉ lệ sát thương.
    /// - Nội tại: Kỹ năng thường trúng địch gây Thiêu đốt (tối đa 3 cộng dồn, duy trì 3s, CapOnly).
    ///   Mỗi cộng dồn gây 25% Tấn công/s. Khi bất kỳ kỹ năng nào bắn vào mục tiêu đủ 3 cộng dồn ->
    ///   nhận thêm 25% Tỉ lệ sát thương trong 3s và xóa toàn bộ cộng dồn Thiêu đốt.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_09_TripleFusionCore : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_09_TripleFusionCore_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseAttackMultiplier = 0.20f;
        [SerializeField, Range(0f, 1f)] private float baseDamageRateMultiplier = 0.10f;

        [Header("Nội tại Thiêu Đốt Ba Lõi")]
        [SerializeField, Range(0f, 1f)] private float burnAttackRatioPerSecond = 0.25f;
        [SerializeField, Min(1f)] private float burnDuration = 3.0f;
        [SerializeField, Range(1, 5)] private int maxBurnStacks = 3;
        [SerializeField, Range(0f, 1f)] private float bonusDamageRate = 0.25f;
        [SerializeField, Min(1f)] private float damageRateBuffDuration = 3.0f;

        private const string BuffKey = "Acc09_TripleFusion_Empower";
        private float m_BuffTimer;
        private bool m_IsBuffActive;


                protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { Attack = baseAttackMultiplier, DamageRate = baseDamageRateMultiplier });
            }
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            RemoveBuff();
            base.OnUnequipped(owner);
        }

        public override void OnOwnerDealtDamage(DamageInfo info)
        {
            if (Owner == null || !Owner.IsAlive) return;
            if (!(info.Target is EnemyBrain enemy) || !enemy.IsAlive) return;

            // Kiểm tra xem mục tiêu đã có hiệu ứng Burn chưa
            BurnEffect existingBurn = null;
            for (int i = 0; i < enemy.ActiveEffects.Count; i++)
            {
                if (enemy.ActiveEffects[i] is BurnEffect b && !b.IsExpired)
                {
                    existingBurn = b;
                    break;
                }
            }

            // Nếu mục tiêu đã đạt tối đa 3 cộng dồn và bị đánh trúng bởi kỹ năng
            if (existingBurn != null && existingBurn.CurrentStacks >= maxBurnStacks && info.IsTankSkill)
            {
                ApplyOrRefreshEmpowerBuff();
                enemy.RemoveEffect(existingBurn.EffectId);
                return;
            }

            // Kỹ năng thường trúng địch thì gây hoặc cộng dồn Thiêu đốt
            if (info.SourceType == DamageSourceType.NormalSkill)
            {
                float dps = Owner.FinalStats.Attack * burnAttackRatioPerSecond;
                BurnEffect burn = new BurnEffect(
                    Owner,
                    duration: burnDuration,
                    damagePerSecondPerStack: dps,
                    maxStacks: maxBurnStacks,
                    tickInterval: 0.5f,
                    stackPolicy: EffectStackPolicy.CapOnly);

                enemy.ApplyEffect(burn);
            }
        }

        public override void OnBrainTick(float deltaTime)
        {
            if (!m_IsBuffActive) return;

            m_BuffTimer -= deltaTime;
            if (m_BuffTimer <= 0f)
            {
                RemoveBuff();
            }
        }

        private void ApplyOrRefreshEmpowerBuff()
        {
            m_BuffTimer = damageRateBuffDuration;
            if (!m_IsBuffActive)
            {
                m_IsBuffActive = true;
                Owner.AddBonusStats(BuffKey, null, new TankStats { DamageRate = bonusDamageRate });
            }
        }

        private void RemoveBuff()
        {
            if (m_IsBuffActive && Owner != null)
            {
                Owner.RemoveBonusStats(BuffKey);
            }
            m_IsBuffActive = false;
        }
    }
}
