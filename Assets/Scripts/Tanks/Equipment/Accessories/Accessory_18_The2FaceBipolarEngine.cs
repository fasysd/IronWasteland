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
        private readonly List<float> m_AllyBuffTimers = new List<float>();


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

        public override void OnBrainTick(float deltaTime)
        {
            if (Owner == null || !Owner.IsAlive) return;

            // Quản lý các tầng buff độc lập cho bản thân/đồng minh
            for (int i = m_AllyBuffTimers.Count - 1; i >= 0; i--)
            {
                m_AllyBuffTimers[i] -= deltaTime;
                if (m_AllyBuffTimers[i] <= 0f)
                {
                    m_AllyBuffTimers.RemoveAt(i);
                    Owner.RemoveBonusStats(AllyBuffKeyPrefix + i);
                }
            }
        }

        public override void OnOwnerDealtDamage(DamageInfo info)
        {
            if (Owner == null || !Owner.IsAlive) return;
            if (!(info.Target is EnemyBrain enemy) || !enemy.IsAlive) return;

            // Áp dụng giảm phòng thủ 10% trong 4s cho kẻ địch
            if (enemy.Stats != null)
            {
                StartCoroutine(ApplyEnemyDefenseDebuffRoutine(enemy, enemyDefenseReductionPerStack, enemyDebuffDuration));
            }
        }

        public override void OnOwnerHealed(float amount)
        {
            if (amount <= 0f) return;
            TriggerAllyAttackBuff();
        }

        private void TriggerAllyAttackBuff()
        {
            if (Owner == null || !Owner.IsAlive) return;

            // Giới hạn tối đa 2 tầng độc lập
            if (m_AllyBuffTimers.Count < 2)
            {
                int stackIndex = m_AllyBuffTimers.Count;
                m_AllyBuffTimers.Add(allyBuffDuration);
                Owner.AddBonusStats(AllyBuffKeyPrefix + stackIndex, null, new TankStats { Attack = allyAttackBuffPerStack });
            }
            else
            {
                // Làm mới thời gian của tầng sớm hết hạn nhất
                m_AllyBuffTimers[0] = allyBuffDuration;
            }

            // Đồng thời buff cho các đồng minh lân cận
            TankBrain[] allTanks = Object.FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            for (int i = 0; i < allTanks.Length; i++)
            {
                TankBrain ally = allTanks[i];
                if (ally == null || !ally.IsAlive || ally == Owner) continue;

                if (Vector2.Distance(Owner.transform.position, ally.transform.position) <= allySearchRadius)
                {
                    StartCoroutine(ApplyAllyAttackBuffRoutine(ally, allyAttackBuffPerStack, allyBuffDuration));
                }
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
        }

        private System.Collections.IEnumerator ApplyAllyAttackBuffRoutine(TankBrain ally, float bonusAtkRatio, float duration)
        {
            string key = "Acc18_AllyAtkBuff_" + System.Guid.NewGuid().ToString("N");
            ally.AddBonusStats(key, null, new TankStats { Attack = bonusAtkRatio });

            yield return new WaitForSeconds(duration);

            if (ally != null)
            {
                ally.RemoveBonusStats(key);
            }
        }

        private void ClearAllyBuffs()
        {
            if (Owner != null)
            {
                Owner.RemoveBonusStats(AllyBuffKeyPrefix + "0");
                Owner.RemoveBonusStats(AllyBuffKeyPrefix + "1");
            }
            m_AllyBuffTimers.Clear();
        }
    }
}
