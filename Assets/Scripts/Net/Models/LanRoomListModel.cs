using System;
using System.Collections.Generic;

namespace IronWasteland.Net.Models
{
    /// <summary>Dựng nội dung 6 dòng danh sách phòng LAN từ dữ liệu quảng bá.</summary>
    public static class LanRoomListModel
    {
        public const int MaxRows = 6;

        public static IReadOnlyList<LanRoomRow> Build(IReadOnlyList<LanRoomInfo> rooms)
        {
            List<LanRoomRow> rows = new List<LanRoomRow>();
            if (rooms == null)
            {
                return rows;
            }

            int count = Math.Min(rooms.Count, MaxRows);
            for (int i = 0; i < count; i++)
            {
                LanRoomInfo room = rooms[i];
                string name = string.IsNullOrWhiteSpace(room.Name) ? "PHÒNG LAN" : room.Name;
                string details = room.Players + "/" + room.MaxPlayers + " NGƯỜI · " + room.StageCode;
                if (room.InMatch)
                {
                    details += " · ĐANG CHIẾN ĐẤU";
                }

                rows.Add(new LanRoomRow(name, details));
            }

            return rows;
        }

        public static string Status(int count)
        {
            if (count <= 0)
            {
                return "CHƯA TÌM THẤY PHÒNG LAN NÀO";
            }

            return "TÌM THẤY " + count + " PHÒNG LAN";
        }
    }
}
