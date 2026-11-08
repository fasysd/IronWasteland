using System;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Core.Effects
{
    /// <summary>
    /// Hieu ung tang chi so Attack cho Enemy trong mot khoang thoi gian.
    /// </summary>
    public class EnemyAttackBuffEffect : StatusEffect
    {
        public float BonusAttack { get; private set; }

        public EnemyAttackBuffEffect(
            string effectId,
            string displayName,
            float duration,
            float bonusAttack,
            int maxStacks = 1,
            EffectStackPolicy stackPolicy = EffectStackPolicy.RefreshDuration)
            : base(effectId, displayName, duration, maxStacks, stackPolicy)
        {
            BonusAttack = bonusAttack;
        }

        public override void OnApplied(UnityEngine.Object target)
        {
            if (target is EnemyBrain enemy && enemy.Stats != null)
            {
                enemy.Stats.Attack += BonusAttack;
            }
        }

        public override void OnRemoved(UnityEngine.Object target)
        {
            if (target is EnemyBrain enemy && enemy.Stats != null)
            {
                enemy.Stats.Attack = Mathf.Max(0f, enemy.Stats.Attack - BonusAttack);
            }
        }
    }
}
