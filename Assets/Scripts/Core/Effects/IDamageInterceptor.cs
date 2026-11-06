namespace IronWasteland.Core.Effects
{
    /// <summary>
    /// Giao diện cho các hiệu ứng có khả năng chặn sạch một đòn đánh (né đòn, đỡ đòn, bất tử tạm thời...).
    /// <para>
    /// Khác với <see cref="ShieldEffect"/> làm việc theo TRỮ LƯỢNG (mỗi đòn bị trừ vào lượng khiên),
    /// hiệu ứng chặn đòn làm việc theo SỐ ĐÒN: đòn bị chặn hoàn toàn không trừ máu, không đi qua bước
    /// tính giáp và cũng không bị lớp khiên nào hấp thụ.
    /// </para>
    /// <para>
    /// Hiệu ứng implement giao diện này chỉ cần được thêm vào Brain qua <c>ApplyEffect</c>. Brain tự
    /// phát hiện trong danh sách hiệu ứng đang hoạt động và hỏi ý nó ở <b>bước đầu tiên</b> của đường ống
    /// sát thương — trước cả chặng chuyển hướng gánh hộ và bước tính giáp. Đứng trước chuyển hướng để
    /// đòn bị chặn sạch không bị chia phần nào sang người gánh hộ.
    /// </para>
    /// </summary>
    public interface IDamageInterceptor
    {
        /// <summary>
        /// Quyết định có chặn đòn này hay không.
        /// Trả về true nghĩa là đòn bị chặn SẠCH: Brain dừng toàn bộ xử lý, không trừ máu và
        /// không phát sự kiện sát thương.
        /// </summary>
        /// <param name="info">Thông tin đòn đánh đang được xử lý (chỉ đọc, không được sửa).</param>
        bool TryIntercept(DamageInfo info);
    }
}
