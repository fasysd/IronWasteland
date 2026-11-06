using IronWasteland.Core;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Cơ chế gánh hộ (chuyển hướng) sát thương cho đồng minh.
    /// Được Brain gọi ở chặng ĐẦU TIÊN của <see cref="TankBrain"/>, TRƯỚC bước tính giáp
    /// và TRƯỚC bước hấp thụ khiên, nên phần sát thương chia sẻ được tính trên sát thương gốc.
    /// </summary>
    public interface IDamageRedirector
    {
        /// <summary>
        /// Quyết định có gánh một phần đòn đánh hiện tại hay không.
        /// </summary>
        /// <param name="info">Thông tin đòn đánh gốc (chưa tính giáp, chưa trừ khiên).</param>
        /// <param name="protector">Đối tượng sẽ nhận phần sát thương gánh hộ.</param>
        /// <param name="sharedAmount">Lượng sát thương gốc chuyển sang cho <paramref name="protector"/>.</param>
        /// <returns>True nếu có chuyển hướng; False nếu không tham gia đòn đánh này.</returns>
        bool TryRedirect(DamageInfo info, out TankBrain protector, out float sharedAmount);
    }
}
