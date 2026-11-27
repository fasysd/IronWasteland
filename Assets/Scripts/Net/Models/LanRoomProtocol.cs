using System;

namespace IronWasteland.Net.Models
{
    /// <summary>Mã hoá và giải mã gói quảng bá phòng LAN trên UDP.</summary>
    public static class LanRoomProtocol
    {
        public const int Version = 1;
        private const string Prefix = "IWLAN";
        private const int FieldCount = 8;

        /// <summary>Đóng gói thông tin phòng thành một dòng ASCII an toàn cho UDP.</summary>
        public static string Encode(LanRoomInfo room)
        {
            if (string.IsNullOrWhiteSpace(room.Name) || string.IsNullOrWhiteSpace(room.StageCode))
            {
                return string.Empty;
            }

            return string.Join("|",
                Prefix,
                Version.ToString(),
                Uri.EscapeDataString(room.Name),
                Uri.EscapeDataString(room.StageCode),
                room.Players.ToString(),
                room.MaxPlayers.ToString(),
                room.Port.ToString(),
                room.InMatch ? "1" : "0");
        }

        /// <summary>Giải mã gói quảng bá; trả false với dữ liệu hỏng hoặc sai phiên bản.</summary>
        public static bool TryDecode(string payload, out LanRoomInfo room)
        {
            room = default;
            if (string.IsNullOrEmpty(payload))
            {
                return false;
            }

            string[] parts = payload.Split('|');
            if (parts.Length != FieldCount || parts[0] != Prefix)
            {
                return false;
            }

            if (!int.TryParse(parts[1], out int version) || version != Version)
            {
                return false;
            }

            string name;
            string stageCode;
            try
            {
                name = Uri.UnescapeDataString(parts[2]);
                stageCode = Uri.UnescapeDataString(parts[3]);
            }
            catch (UriFormatException)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(stageCode))
            {
                return false;
            }

            if (!int.TryParse(parts[4], out int players) || !int.TryParse(parts[5], out int maxPlayers))
            {
                return false;
            }

            if (!int.TryParse(parts[6], out int port) || port <= 0 || port > 65535)
            {
                return false;
            }

            if (maxPlayers < 2 || maxPlayers > 4 || players < 0 || players > maxPlayers)
            {
                return false;
            }

            bool inMatch;
            if (parts[7] == "1")
            {
                inMatch = true;
            }
            else if (parts[7] == "0")
            {
                inMatch = false;
            }
            else
            {
                return false;
            }

            room = new LanRoomInfo(string.Empty, port, name, stageCode, players, maxPlayers, inMatch);
            return true;
        }
    }
}
