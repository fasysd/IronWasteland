using UnityEngine;

namespace IronWasteland.UI
{
    /// <summary>
    /// Ghi nhớ Garage đang được mở chồng lên lobby co-op để các màn hình con
    /// quay lại đúng chỗ thay vì tải lại scene và làm mất phiên mạng.
    /// </summary>
    public static class GarageFlow
    {
        /// <summary>True khi Garage được mở additive từ phòng co-op.</summary>
        public static bool OpenedFromLobby { get; set; }
    }
}
