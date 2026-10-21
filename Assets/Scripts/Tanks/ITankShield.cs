namespace IronWasteland.Tanks
{
    /// <summary>
    /// Giao diện đại diện cho mọi loại khiên bảo vệ trên xe tank trong game.
    /// Cho phép các kỹ năng, bùa lợi (buff) hoặc trang bị gắn khiên vào TankBrain
    /// mà không bị phụ thuộc cứng vào bất kỳ lớp cụ thể nào.
    /// </summary>
    public interface ITankShield
    {
        /// <summary>
        /// Trạng thái khiên có đang hoạt động hay không.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Xử lý hấp thụ hoặc triệt tiêu sát thương nhận vào.
        /// </summary>
        /// <param name="incomingDamage">Lượng sát thương nhận vào. Khiên có thể giảm trừ giá trị này (ref float).</param>
        /// <returns>True nếu đòn đánh đã bị triệt tiêu hoàn toàn (không trừ máu tank); False nếu vẫn còn sát thương xuyên qua.</returns>
        bool TryAbsorbDamage(ref float incomingDamage);

        /// <summary>
        /// Gỡ bỏ hoặc phá vỡ khiên bảo vệ.
        /// </summary>
        void BreakShield();
    }
}
