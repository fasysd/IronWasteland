using System;
using UnityEngine;

namespace IronWasteland.Core.Effects
{
    /// <summary>
    /// Chinh sach cong don (stacking) cua Hieu ung.
    /// </summary>
    public enum EffectStackPolicy
    {
        /// <summary>Them tang moi se dat lai thoi gian ton tai ve Duration ban dau.</summary>
        RefreshDuration,

        /// <summary>Chi tang so tang, khong dat lai thoi gian duy tri (vi du: Thieu dot trong GDD).</summary>
        CapOnly,
    }

    /// <summary>
    /// Lop co so truu tuong cua moi Hieu ung (Buff, Debuff, DoT, HoT, Shield, Stat modifier...).
    /// </summary>
    [Serializable]
    public abstract class StatusEffect
    {
        [SerializeField] protected string effectId;
        [SerializeField] protected string displayName;
        [SerializeField] protected float duration;
        [SerializeField] protected int maxStacks = 1;
        [SerializeField] protected EffectStackPolicy stackPolicy = EffectStackPolicy.RefreshDuration;

        [SerializeField] protected float tickInterval;

        protected float remainingTime;
        protected int currentStacks = 1;
        protected float tickTimer;

        public string EffectId => effectId;
        public string DisplayName => displayName;
        public float Duration => duration;
        public float RemainingTime => remainingTime;
        public int CurrentStacks => currentStacks;
        public int MaxStacks => maxStacks;
        public EffectStackPolicy StackPolicy => stackPolicy;
        public float TickInterval => tickInterval;
        public bool IsExpired => remainingTime <= 0f;

        protected StatusEffect(string effectId, string displayName, float duration, int maxStacks = 1, EffectStackPolicy stackPolicy = EffectStackPolicy.RefreshDuration, float tickInterval = 0f)
        {
            this.effectId = effectId;
            this.displayName = displayName;
            this.duration = duration;
            this.remainingTime = duration;
            this.maxStacks = Mathf.Max(1, maxStacks);
            this.stackPolicy = stackPolicy;
            this.tickInterval = tickInterval;
            this.currentStacks = 1;
            this.tickTimer = 0f;
        }

        /// <summary>Goi 1 lan khi hieu ung duoc add vao doi tuong.</summary>
        public virtual void OnApplied(UnityEngine.Object target) { }

        /// <summary>Goi khi co them 1 tang moi duoc cong don vao hieu ung nay.</summary>
        public virtual void OnStackAdded(UnityEngine.Object target, int newStacks)
        {
            currentStacks = Mathf.Clamp(newStacks, 1, maxStacks);
            if (stackPolicy == EffectStackPolicy.RefreshDuration)
            {
                remainingTime = duration;
            }
        }

        /// <summary>Goi dinh ky moi khi troi qua khoang thoi gian tickInterval.</summary>
        public virtual void OnTick(UnityEngine.Object target) { }

        /// <summary>Goi khi hieu ung bi go bo hoac het han.</summary>
        public virtual void OnRemoved(UnityEngine.Object target) { }

        /// <summary>Tick moi frame.</summary>
        public virtual void UpdateTick(UnityEngine.Object target, float deltaTime)
        {
            remainingTime -= deltaTime;

            if (tickInterval > 0f)
            {
                tickTimer += deltaTime;
                if (tickTimer >= tickInterval)
                {
                    tickTimer -= tickInterval;
                    OnTick(target);
                }
            }
        }
    }
}
