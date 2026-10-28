using System;

namespace IronWasteland.Stage
{
    /// <summary>
    /// Giao diện chuẩn cho tất cả các điều kiện thắng/thua trong hệ thống màn chơi.
    /// Áp dụng mẫu thiết kế Strategy Pattern để dễ dàng mở rộng và cắm ghép các loại mục tiêu mới.
    /// </summary>
    public interface IStageCondition
    {
        /// <summary>Mô tả ngắn gọn mục tiêu của điều kiện (hiển thị trên HUD/UI).</summary>
        string Description { get; }

        /// <summary>Trạng thái điều kiện đã được thỏa mãn hay chưa.</summary>
        bool IsMet { get; }

        /// <summary>
        /// Cờ đánh dấu điều kiện CHỈ có tác dụng gác thất bại, không bao giờ tự hoàn thành.
        /// StageManager sẽ bỏ qua các điều kiện này khi xét điều kiện thắng.
        /// </summary>
        bool IsFailureOnly { get; }

        /// <summary>Tỷ lệ hoàn thành tiến trình từ 0.0f đến 1.0f (dùng cho thanh tiến trình UI).</summary>
        float Progress { get; }

        /// <summary>Chuỗi định dạng văn bản hiển thị tiến trình (ví dụ: "15/20 kẻ địch", "01:30 còn lại").</summary>
        string ProgressText { get; }

        /// <summary>Khởi tạo điều kiện và liên kết với bộ quản lý màn chơi.</summary>
        void Initialize(StageManager manager);

        /// <summary>Cập nhật điều kiện theo khung thời gian (nếu là điều kiện dựa trên thời gian).</summary>
        void UpdateCondition(float deltaTime);

        /// <summary>Đặt lại trạng thái ban đầu của điều kiện khi chơi lại màn chơi.</summary>
        void ResetCondition();

        /// <summary>Dọn dẹp và hủy đăng ký toàn bộ sự kiện khi kết thúc màn chơi.</summary>
        void Cleanup();

        /// <summary>Sự kiện phát ra khi điều kiện này chính thức đạt trạng thái hoàn thành.</summary>
        event Action<IStageCondition> OnConditionMet;

        /// <summary>Sự kiện phát ra khi tiến độ của điều kiện có sự thay đổi.</summary>
        event Action<IStageCondition> OnProgressChanged;
    }
}
