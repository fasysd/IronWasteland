using IronWasteland.Core;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 11 (STT 12 GDD): Động Cơ Tua-Bin Xoáy Nước.
    /// - Chỉ số nền: +15% Tỉ lệ sát thương, +15% Tốc độ di chuyển.
    /// - Nội tại: Kẻ địch bị hạ gục trong phạm vi 10m -> hồi 25 Năng lượng và +20% Tốc độ di chuyển trong 2s.
    ///   Đồng thời nhận 25% Tấn công (cộng dồn tối đa 2 tầng = 50%). Kích hoạt lại đặt lại thời gian duy trì.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_11_VortexTurbine : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_11_VortexTurbine_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseDamageRate = 0.15f;
        [SerializeField, Range(0f, 1f)] private float baseMoveSpeed = 0.15f;

        [Header("Nội tại Thu Hoạch Xoáy Nước")]
        [SerializeField, Min(1f)] private float triggerRadius = 10.0f;
        [SerializeField, Min(0f)] private float energyRecoverAmount = 25f;
        [SerializeField, Range(0f, 1f)] private float bonusMoveSpeed = 0.20f;
        [SerializeField, Range(0f, 1f)] private float attackBonusPerStack = 0.25f;
        [SerializeField, Range(1, 5)] private int maxAttackStacks = 2;
        [SerializeField, Min(0.5f)] private float buffDuration = 2.0f;

        private const string BuffKey = "Acc11_VortexTurbine_Buff";
        private int m_CurrentStacks;
        private float m_BuffTimer;


        protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { DamageRate = baseDamageRate, MoveSpeed = baseMoveSpeed });
            }
            EnemyBrain.OnAnyEnemyDied += HandleAnyEnemyDied;
            m_CurrentStacks = 0;
            m_BuffTimer = 0f;
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            EnemyBrain.OnAnyEnemyDied -= HandleAnyEnemyDied;
            RemoveBuff();
            base.OnUnequipped(owner);
        }

        private void HandleAnyEnemyDied(EnemyBrain enemy)
        {
            if (Owner == null || !Owner.IsAlive || !Owner.HasGameplayAuthority || enemy == null) return;

            float distance = Vector2.Distance(Owner.transform.position, enemy.transform.position);
            if (distance <= triggerRadius)
            {
                // Hồi 25 Năng lượng
                Owner.GainEnergy(energyRecoverAmount);

                // Tăng tầng hoặc làm mới thời gian
                if (m_CurrentStacks < maxAttackStacks)
                {
                    m_CurrentStacks++;
                }
                m_BuffTimer = buffDuration;
                ApplyBuff();
            }
        }

        public override void OnBrainTick(float deltaTime)
        {
            if (m_CurrentStacks <= 0) return;

            m_BuffTimer -= deltaTime;
            if (m_BuffTimer <= 0f)
            {
                RemoveBuff();
            }
        }

        private void ApplyBuff()
        {
            float totalAtkBonus = m_CurrentStacks * attackBonusPerStack;
            Owner.AddBonusStats(BuffKey, null, new TankStats
            {
                MoveSpeed = bonusMoveSpeed,
                Attack = totalAtkBonus
            });
        }

        private void RemoveBuff()
        {
            m_CurrentStacks = 0;
            m_BuffTimer = 0f;
            if (Owner != null)
            {
                Owner.RemoveBonusStats(BuffKey);
            }
        }
    }
}
