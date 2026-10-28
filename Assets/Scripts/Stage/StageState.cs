namespace IronWasteland.Stage
{
    /// <summary>
    /// Các trạng thái vòng đời của một màn chơi trong IronWasteland.
    /// </summary>
    public enum StageState
    {
        /// <summary>Màn chơi chưa bắt đầu, đang chờ tải dữ liệu hoặc người chơi sẵn sàng.</summary>
        NotStarted,

        /// <summary>Màn chơi đang diễn ra, thời gian đang đếm và các điều kiện đang được theo dõi.</summary>
        Playing,

        /// <summary>Màn chơi tạm dừng (khi người chơi mở menu tùy chọn, xem trang bị hoặc tạm hoãn).</summary>
        Paused,

        /// <summary>Người chơi đã hoàn thành xuất sắc các điều kiện thắng của màn chơi.</summary>
        Victory,

        /// <summary>Người chơi thất bại trong màn chơi (hết máu, mục tiêu bị phá hủy hoặc hết thời gian).</summary>
        Defeat
    }
}
