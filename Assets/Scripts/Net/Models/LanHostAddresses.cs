using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace IronWasteland.Net.Models
{
    /// <summary>Liệt kê IPv4 nội bộ của máy host để hiển thị và sao chép cho đồng đội.</summary>
    public static class LanHostAddresses
    {
        /// <summary>IPv4 đang dùng được, ưu tiên card mạng thật; 127.0.0.1 luôn nằm cuối danh sách.</summary>
        public static IReadOnlyList<string> GetLocalIPv4()
        {
            List<string> result = new List<string>();
            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < interfaces.Length; i++)
                {
                    NetworkInterface nic = interfaces[i];
                    if (nic == null || nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (UnicastIPAddressInformation info in nic.GetIPProperties().UnicastAddresses)
                    {
                        IPAddress address = info.Address;
                        if (address == null || address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string text = address.ToString();
                        if (!result.Contains(text)) result.Add(text);
                    }
                }
            }
            catch (Exception)
            {
                // Máy không cho đọc card mạng: vẫn còn 127.0.0.1 cho bài test cùng máy.
            }

            if (!result.Contains("127.0.0.1")) result.Add("127.0.0.1");
            return result;
        }

        /// <summary>Địa chỉ đầu tiên để hiển thị/sao chép; luôn có giá trị.</summary>
        public static string Primary()
        {
            IReadOnlyList<string> all = GetLocalIPv4();
            return all.Count > 0 ? all[0] : "127.0.0.1";
        }
    }
}
