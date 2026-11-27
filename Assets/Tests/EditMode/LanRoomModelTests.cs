using System.Collections.Generic;
using IronWasteland.Net.Models;
using NUnit.Framework;

namespace IronWasteland.Tests
{
    /// <summary>
    /// Hợp đồng model LAN thuần C#: gói tin quảng bá, cách nhập địa chỉ host và nội dung danh sách phòng.
    /// Test ở seam model, không mở scene và không cần Unity runtime.
    /// </summary>
    public class LanRoomModelTests
    {
        [Test]
        public void Goi_tin_LAN_giu_nguyen_ten_phong_co_dau_va_ky_tu_dac_biet()
        {
            LanRoomInfo room = new LanRoomInfo(string.Empty, 7777, "PHÒNG CỦA DESKTOP-ABC | 2", "1-2", 3, 4, true);

            string payload = LanRoomProtocol.Encode(room);

            Assert.That(LanRoomProtocol.TryDecode(payload, out LanRoomInfo decoded), Is.True);
            Assert.That(decoded.Name, Is.EqualTo("PHÒNG CỦA DESKTOP-ABC | 2"));
            Assert.That(decoded.StageCode, Is.EqualTo("1-2"));
            Assert.That(decoded.Players, Is.EqualTo(3));
            Assert.That(decoded.MaxPlayers, Is.EqualTo(4));
            Assert.That(decoded.Port, Is.EqualTo(7777));
            Assert.That(decoded.InMatch, Is.True);
        }

        [Test]
        public void Goi_tin_LAN_tu_choi_du_lieu_hong_hoac_sai_phien_ban()
        {
            Assert.That(LanRoomProtocol.TryDecode(null, out _), Is.False);
            Assert.That(LanRoomProtocol.TryDecode("", out _), Is.False);
            Assert.That(LanRoomProtocol.TryDecode("IWLAN|1|thieu-truong", out _), Is.False);
            Assert.That(LanRoomProtocol.TryDecode("IWLAN|99|Phong|1-1|1|4|7777|0", out _), Is.False);
            Assert.That(LanRoomProtocol.TryDecode("IWLAN|1|Phong|1-1|sai|4|7777|0", out _), Is.False);
            Assert.That(LanRoomProtocol.TryDecode("IWLAN|1|Phong|1-1|1|4|0|0", out _), Is.False);
        }

        [Test]
        public void Nhap_dia_chi_host_chap_nhan_ip_trong_mang_va_ip_co_cong()
        {
            Assert.That(LanAddress.TryParse("192.168.1.5", 7777, out string host1, out ushort port1), Is.True);
            Assert.That(host1, Is.EqualTo("192.168.1.5"));
            Assert.That(port1, Is.EqualTo((ushort)7777));

            Assert.That(LanAddress.TryParse(" 127.0.0.1:8888 ", 7777, out string host2, out ushort port2), Is.True);
            Assert.That(host2, Is.EqualTo("127.0.0.1"));
            Assert.That(port2, Is.EqualTo((ushort)8888));

            Assert.That(LanAddress.TryParse("may-chu.lan", 7777, out string host3, out ushort port3), Is.True);
            Assert.That(host3, Is.EqualTo("may-chu.lan"));
            Assert.That(port3, Is.EqualTo((ushort)7777));
        }

        [Test]
        public void Nhap_dia_chi_host_tu_choi_chuoi_rong_hoac_cong_sai()
        {
            Assert.That(LanAddress.TryParse("", 7777, out _, out _), Is.False);
            Assert.That(LanAddress.TryParse("   ", 7777, out _, out _), Is.False);
            Assert.That(LanAddress.TryParse(":7777", 7777, out _, out _), Is.False);
            Assert.That(LanAddress.TryParse("host:0", 7777, out _, out _), Is.False);
            Assert.That(LanAddress.TryParse("host:abc", 7777, out _, out _), Is.False);
            Assert.That(LanAddress.TryParse("a:b:c", 7777, out _, out _), Is.False);
        }

        [Test]
        public void Danh_sach_phong_LAN_toi_da_sau_dong_va_ghi_trang_thai_dang_chien_dau()
        {
            List<LanRoomInfo> rooms = new List<LanRoomInfo>();
            for (int i = 0; i < 8; i++)
            {
                rooms.Add(new LanRoomInfo("192.168.1." + (10 + i), 7777, "PHÒNG " + i, "1-" + (i + 1), 2, 4, i == 0));
            }

            IReadOnlyList<LanRoomRow> rows = LanRoomListModel.Build(rooms);

            Assert.That(rows.Count, Is.EqualTo(6));
            Assert.That(rows[0].Name, Is.EqualTo("PHÒNG 0"));
            Assert.That(rows[0].Details, Does.Contain("2/4 NGƯỜI"));
            Assert.That(rows[0].Details, Does.Contain("1-1"));
            Assert.That(rows[0].Details, Does.Contain("ĐANG CHIẾN ĐẤU"));
            Assert.That(rows[1].Details, Does.Not.Contain("ĐANG CHIẾN ĐẤU"));
            Assert.That(rows[5].Name, Is.EqualTo("PHÒNG 5"));
        }

        [Test]
        public void Trang_thai_danh_sach_phong_LAN_phan_biet_rong_va_co_phong()
        {
            Assert.That(LanRoomListModel.Status(0), Is.EqualTo("CHƯA TÌM THẤY PHÒNG LAN NÀO"));
            Assert.That(LanRoomListModel.Status(3), Is.EqualTo("TÌM THẤY 3 PHÒNG LAN"));
        }
    }
}
