using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Core.Effects
{
    /// <summary>
    /// Hieu ung Khien bao ve (Shield): co nguon goc (Source), gia tri khien va thoi gian ton tai rieng biet.
    /// Hap thu sat thuong truoc khi tru vao mau that cua doi tuong.
    /// </summary>
    public class ShieldEffect : StatusEffect
    {
        public UnityEngine.Object Source { get; private set; }
        public float ShieldAmount { get; private set; }
        public float RemainingShield { get; private set; }

        public ShieldEffect(
            string effectId,
            string displayName,
            float duration,
            float shieldAmount,
            UnityEngine.Object source = null)
            : base(effectId, displayName, duration, 1, EffectStackPolicy.RefreshDuration)
        {
            Source = source;
            ShieldAmount = Mathf.Max(0f, shieldAmount);
            RemainingShield = ShieldAmount;
        }

        public override void OnStackAdded(UnityEngine.Object target, int newStacks)
        {
            base.OnStackAdded(target, newStacks);
            // Khi duoc lam moi hoac cong them stack, hoi lai luong khien
            RemainingShield = ShieldAmount;
        }

        /// <summary>
        /// Hap thu mot phan hoac toan bo luong sat thuong yeu cau.
        /// Tra ve luong sat thuong thuc te da duoc khien nay hap thu.
        /// Neu luong khien ve 0, hieu ung se het han (RemainingTime = 0).
        /// </summary>
        public float Absorb(float damageAmount)
        {
            if (damageAmount <= 0f || RemainingShield <= 0f) return 0f;

            float absorbed = Mathf.Min(RemainingShield, damageAmount);
            RemainingShield -= absorbed;

            if (RemainingShield <= 0f)
            {
                RemainingShield = 0f;
                remainingTime = 0f; // Danh dau het han de he thong Effect tu don dep
            }

            return absorbed;
        }
    }
}
