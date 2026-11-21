using IronWasteland.Net;
using IronWasteland.Stages;

namespace IronWasteland.UI
{
    /// <summary>
    /// Trạng thái chuyển từ màn CHỌN ẢI sang scene chiến đấu: chế độ, ải đang chọn
    /// và có mở thẳng lobby co-op hay không. Chỉ là dữ liệu bắc cầu giữa hai scene.
    /// </summary>
    public static class CoopEntry
    {
        /// <summary>Chế độ người chơi đã chọn ở màn chọn ải.</summary>
        public static MatchMode Mode = MatchMode.Coop;

        /// <summary>Mã ải đang chọn (ví dụ 1-2).</summary>
        public static string StageCode = "1-2";

        /// <summary>Tên ải đang chọn để lobby hiển thị ngay khi vào scene.</summary>
        public static string StageName = "HANG ĐÁ GAI";

        /// <summary>Dữ liệu chiến đấu của ải đang chọn.</summary>
        public static StageDefinition SelectedDefinition;

        /// <summary>True khi cần mở lobby co-op thay vì vào trận đơn ngay.</summary>
        public static bool OpenLobby;

        /// <summary>Kiểm tra nhanh xem có phải đang ở chế độ Co-op hay không.</summary>
        public static bool IsCoop => Mode == MatchMode.Coop;
    }
}
