namespace IronWasteland.Stage
{
    /// <summary>
    /// Các nguyên nhân dẫn đến thất bại trong màn chơi.
    /// </summary>
    public enum StageDefeatReason
    {
        /// <summary>Không có hoặc chưa xác định.</summary>
        None,

        /// <summary>Xe tăng của người chơi bị tiêu diệt (hết máu).</summary>
        PlayerDied,

        /// <summary>Mục tiêu cần bảo vệ (xe tăng VIP, công trình, nhà chính) bị phá hủy.</summary>
        DefendTargetDestroyed,

        /// <summary>Hết thời gian giới hạn của màn chơi mà chưa hoàn thành mục tiêu.</summary>
        TimeExpired,

        /// <summary>Người chơi chủ động đầu hàng hoặc thoát trận giữa chừng.</summary>
        Surrendered
    }
}
