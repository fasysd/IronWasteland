using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 18 (STT 19 GDD): Lõi Động Cơ Lưỡng Cực THE2FACE.
    /// - Chỉ số nền: +15% Tấn công, +15% Tỉ lệ giảm hồi chiêu.
    /// - Nội tại:
    ///   + Công: Gây sát thương khiến địch nhận giảm 10% phòng thủ trong 4s (tối đa 2 tầng, thời gian duy trì độc lập).
    ///   + Thủ: Hồi máu / tạo giáp giúp xe đồng minh +15% Tấn công trong 4s (tối đa 2 tầng, thời gian duy trì độc lập).
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_18_The2FaceBipolarEngine : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_18_The2FaceBipolarEngine_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseAttackMultiplier = 0.15f;
        [SerializeField, Range(0f, 1f)] private float baseCooldownReduction = 0.15f;

        [Header("Nội tại Công: Giảm Giáp Địch")]
        [SerializeField, Range(0f, 0.5f)] private float enemyDefenseReductionPerStack = 0.10f;
        [SerializeField, Min(1f)] private float enemyDebuffDuration = 4.0f;

        [Header("Nội tại Thủ: Tăng Công Đồng Minh")]
        [SerializeField, Range(0f, 0.5f)] private float allyAttackBuffPerStack = 0.15f;
        [SerializeField, Min(1f)] private float allyBuffDuration = 4.0f;
        [SerializeField, Min(1f)] private float allySearchRadius = 8.0f;

        private const string AllyBuffKeyPrefix = "Acc18_The2Face_Atk_";
        private readonly Dictionary<EnemyBrain, int> m_EnemyDebuffStacks = new Dictionary<EnemyBrain, int>();
        private readonly Dictionary<TankBrain, int> m_AllyBuffStacks = new Dictionary<TankBrain, int>();

        protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { Attack = baseAttackMultiplier, CooldownReduction = baseCooldownReduction });
            }
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            ClearAllyBuffs();
            base.OnUnequipped(owner);
        }

        public override void OnBrainTick(float deltaTime) { }

        public override void OnOwnerDealtDamage(DamageInfo info)
        {
            if (Owner == null || !Owner.IsAlive) return;
            if (!(info.Target is EnemyBrain enemy) || !enemy.IsAlive) return;

            // Tối đa 2 tầng độc lập (-10% DEF trong 4s)
            m_EnemyDebuffStacks.TryGetValue(enemy, out int stacks);
            if (stacks < 2 && enemy.Stats != null)
            {
                m_EnemyDebuffStacks[enemy] = stacks + 1;
                StartCoroutine(ApplyEnemyDefenseDebuffRoutine(enemy, enemyDefenseReductionPerStack, enemyDebuffDuration));
            }
        }

        public override void OnOwnerSupportedAlly(TankBrain ally)
        {
            if (Owner == null || !Owner.IsAlive || ally == null || !ally.IsAlive || ally == Owner) return;
            TriggerAllyAttackBuff(ally);
        }

        private void TriggerAllyAttackBuff(TankBrain ally)
        {
            m_AllyBuffStacks.TryGetValue(ally, out int stacks);
            if (stacks < 2)
            {
                m_AllyBuffStacks[ally] = stacks + 1;
                StartCoroutine(ApplyAllyAttackBuffRoutine(ally, allyAttackBuffPerStack, allyBuffDuration));
            }
        }

        private System.Collections.IEnumerator ApplyEnemyDefenseDebuffRoutine(EnemyBrain enemy, float reductionRatio, float duration)
        {
            if (enemy == null || enemy.Stats == null) yield break;

            float reduced = enemy.Stats.Defense * reductionRatio;
            enemy.Stats.Defense = Mathf.Max(0f, enemy.Stats.Defense - reduced);

            yield return new WaitForSeconds(duration);

            if (enemy != null && enemy.Stats != null)
            {
                enemy.Stats.Defense += reduced;
            }

            if (enemy != null && m_EnemyDebuffStacks.TryGetValue(enemy, out int stacks))
            {
                if (stacks <= 1) m_EnemyDebuffStacks.Remove(enemy);
                else m_EnemyDebuffStacks[enemy] = stacks - 1;
            }
        }

        private System.Collections.IEnumerator ApplyAllyAttackBuffRoutine(TankBrain ally, float bonusAtkRatio, float duration)
        {
            string key = AllyBuffKeyPrefix + System.Guid.NewGuid().ToString("N");
            ally.AddBonusStats(key, null, new TankStats { Attack = bonusAtkRatio });

            yield return new WaitForSeconds(duration);

            if (ally != null)
            {
                ally.RemoveBonusStats(key);
                if (m_AllyBuffStacks.TryGetValue(ally, out int stacks))
                {
                    if (stacks <= 1) m_AllyBuffStacks.Remove(ally);
                    else m_AllyBuffStacks[ally] = stacks - 1;
                }
            }
        }

        private void ClearAllyBuffs()
        {
            m_AllyBuffStacks.Clear();
            m_EnemyDebuffStacks.Clear();
        }
    }
}
