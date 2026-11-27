using System;

namespace IronWasteland.Net.Models
{
    /// <summary>Thông tin một phòng LAN nhận được từ gói quảng bá UDP.</summary>
    public readonly struct LanRoomInfo
    {
        public LanRoomInfo(string address, int port, string name, string stageCode, int players, int maxPlayers, bool inMatch)
        {
            Address = address;
            Port = port;
            Name = name;
            StageCode = stageCode;
            Players = players;
            MaxPlayers = maxPlayers;
            InMatch = inMatch;
        }

        /// <summary>Địa chỉ IPv4 của máy host, do phía nhận gói tin điền vào.</summary>
        public string Address { get; }

        /// <summary>Cổng game UDP của host (mặc định 7777).</summary>
        public int Port { get; }

        /// <summary>Tên phòng, lấy từ tên máy host.</summary>
        public string Name { get; }

        /// <summary>Mã ải mà host đang chọn, ví dụ "1-2".</summary>
        public string StageCode { get; }

        public int Players { get; }
        public int MaxPlayers { get; }

        /// <summary>True khi host đã vào trận nhưng vẫn còn phát phòng.</summary>
        public bool InMatch { get; }

        /// <summary>Chuỗi địa chỉ đầy đủ để người chơi nhập hoặc sao chép.</summary>
        public string DisplayAddress => Address + ":" + Port;

        /// <summary>Trả về bản sao đã gắn địa chỉ IP của máy gửi gói tin.</summary>
        public LanRoomInfo WithAddress(string address) => new LanRoomInfo(address, Port, Name, StageCode, Players, MaxPlayers, InMatch);
    }
}
