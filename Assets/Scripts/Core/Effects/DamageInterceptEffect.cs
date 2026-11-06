using System;
using UnityEngine;

namespace IronWasteland.Core.Effects
{
    /// <summary>
    /// Hiệu ứng CHẶN ĐÒN: né đòn, đỡ đòn, bất tử tạm thời... làm việc theo SỐ ĐÒN thay vì theo trữ lượng.
    /// <para>
    /// Vì sao không dùng <see cref="ShieldEffect"/> cho các cơ chế này: khiên có trữ lượng (Tide Water),
    /// còn "đỡ đúng 1 đòn" hoặc "bất tử trong 1.2 giây" không có trữ lượng nào cả. Nhét chúng vào hệ khiên
    /// sẽ phải bịa một lượng khiên vô hạn, làm sai <c>CurrentShield</c> và biến chúng thành mục tiêu của
    /// các hiệu ứng "phá khiên" — trong khi bản chất chúng không phải khiên.
    /// </para>
    /// <para>
    /// Hiệu ứng này được Brain phát hiện tự động trong danh sách hiệu ứng đang hoạt động (không cần
    /// đăng ký riêng). Mỗi khi chặn được một đòn, nó phát <see cref="OnNegated"/> để skill bật hiệu ứng
    /// hình ảnh (burst, tia chớp, phản đòn...), rồi tự hết hạn khi đã dùng hết số lượt.
    /// </para>
    /// </summary>
    public class DamageInterceptEffect : StatusEffect, IDamageInterceptor
    {
        /// <summary>Nguồn gốc hiệu ứng (TankBrain của đồng minh đặt bảo vệ, hoặc chính bản thân).</summary>
        public UnityEngine.Object Source { get; private set; }

        /// <summary>Số đòn tối đa có thể chặn. <see cref="int.MaxValue"/> nghĩa là không giới hạn số đòn
        /// (chỉ giới hạn theo thời gian, ví dụ trạng thái bất tử 1.2 giây của Leviathan).</summary>
        public int MaxNegateHits { get; private set; }

        /// <summary>Số đòn đã chặn được kể từ lần làm mới gần nhất.</summary>
        public int NegatedHits { get; private set; }

        /// <summary>True khi đã dùng hết số lượt chặn.</summary>
        public bool IsExhausted => NegatedHits >= MaxNegateHits;

        /// <summary>
        /// Có chặn cả sát thương theo thời gian (DoT: Thiêu Đốt...) hay không.
        /// Mặc định false: một tick cháy nhỏ không được phép ăn mất lượt chặn dành cho đòn chí mạng.
        /// Trạng thái bất tử nên đặt true.
        /// </summary>
        public bool BlockDamageOverTime { get; set; }

        /// <summary>
        /// Có chặn phần sát thương do đồng minh chuyển hướng (gánh hộ) sang hay không.
        /// Mặc định true: trạng thái "bất tử" phải bảo vệ cả phần gánh hộ.
        /// </summary>
        public bool BlockRedirected { get; set; }

        /// <summary>
        /// Phát ngay khi một đòn bị chặn sạch, kèm thông tin đòn đánh gốc.
        /// Skill dùng sự kiện này để bật hiệu ứng burst/né và biết kẻ tấn công là ai.
        /// </summary>
        public event Action<DamageInfo> OnNegated;

        public DamageInterceptEffect(
            string effectId,
            string displayName,
            float duration,
            UnityEngine.Object source = null,
            int maxNegateHits = 1,
            bool blockDamageOverTime = false,
            bool blockRedirected = true)
            : base(effectId, displayName, duration, 1, EffectStackPolicy.RefreshDuration)
        {
            Source = source;
            MaxNegateHits = maxNegateHits <= 0 ? int.MaxValue : maxNegateHits;
            NegatedHits = 0;
            BlockDamageOverTime = blockDamageOverTime;
            BlockRedirected = blockRedirected;
        }

        /// <summary>
        /// Được làm mới (ví dụ đồng minh niệm lại kỹ năng bảo vệ): đặt lại thời gian và số lượt chặn.
        /// </summary>
        public override void OnStackAdded(UnityEngine.Object target, int newStacks)
        {
            base.OnStackAdded(target, newStacks);
            NegatedHits = 0;
        }

        /// <summary>
        /// Chặn sạch một đòn nếu còn lượt và loại đòn nằm trong chính sách cho phép.
        /// </summary>
        public bool TryIntercept(DamageInfo info)
        {
            if (IsExpired) return false;
            if (IsExhausted) return false;
            if (info.IsDamageOverTime && !BlockDamageOverTime) return false;
            if (info.IsRedirected && !BlockRedirected) return false;

            NegatedHits++;
            OnNegated?.Invoke(info);

            // Hết lượt chặn → hết hạn ngay; hệ thống hiệu ứng sẽ tự dọn ở tick kế tiếp.
            if (NegatedHits >= MaxNegateHits)
            {
                remainingTime = 0f;
            }

            return true;
        }
    }
}
