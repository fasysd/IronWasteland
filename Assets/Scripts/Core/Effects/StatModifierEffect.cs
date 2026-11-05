using System;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Core.Effects
{
    /// <summary>
    /// Hieu ung tang/giam chi so (Buff / Debuff chi so) trong mot khoang thoi gian.
    /// </summary>
    public class StatModifierEffect : StatusEffect
    {
        public TankStats FlatModifier { get; private set; }
        public TankStats PercentModifier { get; private set; }

        public StatModifierEffect(
            string effectId,
            string displayName,
            float duration,
            TankStats flatModifier = null,
            TankStats percentModifier = null,
            int maxStacks = 1,
            EffectStackPolicy stackPolicy = EffectStackPolicy.RefreshDuration)
            : base(effectId, displayName, duration, maxStacks, stackPolicy)
        {
            FlatModifier = flatModifier ?? TankStats.Zeroed();
            PercentModifier = percentModifier ?? TankStats.Zeroed();
        }

        public override void OnApplied(UnityEngine.Object target)
        {
            if (target is TankBrain tank)
            {
                tank.AddBonusStats(EffectId, FlatModifier, PercentModifier);
            }
        }

        public override void OnStackAdded(UnityEngine.Object target, int newStacks)
        {
            base.OnStackAdded(target, newStacks);
            if (target is TankBrain tank)
            {
                // Nhan he so theo so stack
                TankStats scaledFlat = FlatModifier.Clone();
                TankStats scaledPercent = PercentModifier.Clone();

                for (int i = 1; i < currentStacks; i++)
                {
                    scaledFlat.AddTo(FlatModifier);
                    scaledPercent.AddTo(PercentModifier);
                }

                tank.AddBonusStats(EffectId, scaledFlat, scaledPercent);
            }
        }

        public override void OnRemoved(UnityEngine.Object target)
        {
            if (target is TankBrain tank)
            {
                tank.RemoveBonusStats(EffectId);
            }
        }
    }
}
