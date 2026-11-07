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

        /// <summary>
        /// Số đòn tối đa mà lớp khiên này có thể hấp thụ. <see cref="int.MaxValue"/> nghĩa là không giới hạn
        /// (hành vi mặc định của mọi khiên cũ). Dùng cho loại khiên "đỡ đúng N đòn rồi vỡ".
        /// </summary>
        public int MaxAbsorbHits { get; private set; }

        /// <summary>Số đòn lớp khiên này đã hấp thụ kể từ lần làm mới gần nhất.</summary>
        public int AbsorbHits { get; private set; }

        /// <summary>True khi lớp khiên đã cạn lượng khiên hoặc đã chạm số đòn tối đa.</summary>
        public bool IsExhausted => RemainingShield <= 0f || AbsorbHits >= MaxAbsorbHits;

        public ShieldEffect(
            string effectId,
            string displayName,
            float duration,
            float shieldAmount,
            UnityEngine.Object source = null,
            int maxAbsorbHits = int.MaxValue)
            : base(effectId, displayName, duration, 1, EffectStackPolicy.RefreshDuration)
        {
            Source = source;
            ShieldAmount = Mathf.Max(0f, shieldAmount);
            RemainingShield = ShieldAmount;
            MaxAbsorbHits = maxAbsorbHits <= 0 ? int.MaxValue : maxAbsorbHits;
            AbsorbHits = 0;
        }

        public override void OnStackAdded(UnityEngine.Object target, int newStacks)
        {
            base.OnStackAdded(target, newStacks);
            // Khi duoc lam moi hoac cong them stack, hoi lai luong khien
            RemainingShield = ShieldAmount;
            AbsorbHits = 0;
        }

        /// <summary>
        /// Hap thu mot phan hoac toan bo luong sat thuong yeu cau.
        /// Tra ve luong sat thuong thuc te da duoc khien nay hap thu.
        /// Neu luong khien ve 0, hieu ung se het han (RemainingTime = 0).
        /// </summary>
        public float Absorb(float damageAmount)
        {
            if (damageAmount <= 0f || IsExhausted) return 0f;

            float absorbed = Mathf.Min(RemainingShield, damageAmount);
            RemainingShield -= absorbed;
            AbsorbHits++;

            // Cạn lượng khiên, hoặc đã đỡ đủ số đòn tối đa → vỡ khiên và hết hạn ngay.
            if (RemainingShield <= 0f || AbsorbHits >= MaxAbsorbHits)
            {
                // Lượng khiên còn dư cũng bị huỷ vì hiệu ứng đã cạn.
                RemainingShield = 0f;
                remainingTime = 0f; // Danh dau het han de he thong Effect tu don dep
            }

            return absorbed;
        }
    }
}
