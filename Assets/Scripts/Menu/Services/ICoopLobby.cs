using System;
using System.Collections.Generic;

namespace IronWasteland.Menu
{
    public enum CoopSlotState
    {
        Empty = 0,   // Chưa có người
        Host = 1,    // Chủ phòng
        Joined = 2,  // Đã vào phòng, chưa sẵn sàng
        Ready = 3,   // Đã sẵn sàng
    }

    public struct CoopSlot
    {
        public CoopSlotState State;
        public string PlayerName;
    }

    /// <summary>
    /// Hợp đồng giữa màn Chọn ải và hệ thống mạng LAN (hạng mục 6 trong lộ trình).
    /// Khi làm xong mạng, chỉ cần viết một lớp implement interface này rồi gán vào
    /// <see cref="MenuServices.CoopLobby"/>; UI không phải sửa.
    /// </summary>
    public interface ICoopLobby
    {
        /// <summary>Luôn đủ 4 phần tử: P1 (host) ... P4.</summary>
        IReadOnlyList<CoopSlot> Slots { get; }

        /// <summary>True khi được phép bắt đầu ải ở chế độ Co-op.</summary>
        bool CanStart { get; }

        /// <summary>Dòng trạng thái ngắn dưới các ô người chơi.</summary>
        string StatusText { get; }

        /// <summary>Phát khi danh sách người chơi / trạng thái đổi.</summary>
        event Action Changed;

        /// <summary>Màn Chọn ải mở chế độ Co-op.</summary>
        void Open();

        /// <summary>Rời chế độ Co-op / đóng màn.</summary>
        void Close();
    }

    /// <summary>
    /// Lobby giả lập khi chưa có mạng: chỉ có người chơi này làm host.
    /// Co-op chỉ bắt đầu được nếu bật <see cref="AllowSoloStart"/> (để test).
    /// </summary>
    public sealed class OfflineCoopLobby : ICoopLobby
    {
        private readonly CoopSlot[] m_Slots =
        {
            new CoopSlot { State = CoopSlotState.Host, PlayerName = "P1" },
            new CoopSlot { State = CoopSlotState.Empty },
            new CoopSlot { State = CoopSlotState.Empty },
            new CoopSlot { State = CoopSlotState.Empty },
        };

        public bool AllowSoloStart { get; set; }

        public IReadOnlyList<CoopSlot> Slots => m_Slots;

        public bool CanStart => AllowSoloStart;

        public string StatusText => AllowSoloStart
            ? "CHƯA CÓ KẾT NỐI LAN · CHO PHÉP VÀO TRẬN MỘT MÌNH (CHẾ ĐỘ THỬ)"
            : "CHƯA CÓ KẾT NỐI LAN · CẦN ÍT NHẤT 2 NGƯỜI CHƠI";

        public event Action Changed;

        public void Open() => Changed?.Invoke();
        public void Close() { }
    }

    /// <summary>Điểm cắm các dịch vụ dùng chung của Menu.</summary>
    public static class MenuServices
    {
        public static ICoopLobby CoopLobby { get; set; } = new OfflineCoopLobby();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => CoopLobby = new OfflineCoopLobby();
    }
}
