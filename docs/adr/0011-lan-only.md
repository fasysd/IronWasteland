# 0011 — Chơi online chỉ qua mạng LAN, bỏ Relay/Lobby

- Ngày: 2026-11-27
- Trạng thái: Đã chấp nhận
- Liên quan: m03943, m03975

## Bối cảnh

Chế độ chơi chung trước đây dựa hoàn toàn vào Unity Gaming Services: Relay để xuyên NAT và
Lobby để phát hiện phòng theo mã. Cách này cần Internet, cần tài khoản/dự án UGS và thêm độ trễ
qua máy chủ trung gian, trong khi nhu cầu hiện tại chỉ là hai tới bốn máy trong cùng mạng LAN.

Người dùng chốt: **chỉ chơi LAN, không triển khai Relay**. Mã phòng, đăng nhập dịch vụ và phòng
theo ải đều không còn cần thiết.

## Quyết định

1. Bỏ hoàn toàn Relay/Lobby: xoá `NetRelayService`, `NetLobbyUI`, các nhánh
   `StartHostWithRelay`/`StartClientWithRelay`, cờ test Relay và hai package
   `com.unity.services.lobby`, `com.unity.services.relay`.
2. Host mở phòng bằng kết nối trực tiếp: `NetBootstrap.StartHost()` gọi
   `SetConnectionData("127.0.0.1", <cổng>, "0.0.0.0")` để lắng nghe mọi card mạng.
3. Cổng game ưu tiên **7777**. Nếu cổng đang bị ứng dụng khác chiếm, host tự thử các cổng
   kế tiếp trong dải **7777–7786**, cập nhật `HostPort` theo cổng thực tế và quảng bá đúng
   cổng đó; nhờ vậy vẫn tạo được phòng thay vì báo lỗi. Chỉ đổi cổng gốc khi test bằng
   `-iw-port <cổng>` (hoặc `NetBootstrap.SetHostPort`). Không thêm ô nhập cổng trên UI.
4. Tự dò phòng bằng UDP broadcast: `LanDiscoveryService` lắng nghe cổng **47777**, host phát
   gói `IWLAN|1|...` mỗi **1 giây**, phòng hết hạn sau **4,5 giây** không thấy gói mới.
   Danh sách hiện tối đa **6 phòng**, sắp xếp theo tên.
5. Ô nhập tay là phương án dự phòng khi broadcast bị chặn: nhận IP, `IP:port` hoặc tên máy,
   mặc định cổng 7777 (`LanAddress.TryParse`). Vẫn cho `127.0.0.1` để test hai tiến trình
   trên cùng máy.
6. Phòng được định danh bằng **IP:port của host**, không còn mã phòng. Nút SAO CHÉP ĐỊA CHỈ
   copy địa chỉ host đang dùng; host hiện **tất cả IPv4 nội bộ** kèm cổng để chọn đúng card mạng.
7. Hiện **tất cả phòng LAN** tìm thấy, không lọc theo ải đang chọn; dòng phòng ghi mã ải.
   Vào phòng thì đổi sang ải của host (`ApplyStageCode`).
8. Host đang trong trận vẫn phát phòng, dòng ghi `ĐANG CHIẾN ĐẤU`; người vào muộn được vào
   thẳng trận nếu còn chỗ (`MatchFlow.MaxPlayers = 4`).
9. Tên phòng lấy từ tên máy: `"PHÒNG CỦA " + Environment.MachineName`; không có ô nhập tên.
10. Nhãn giao diện đổi theo nghĩa LAN: `PHÒNG LAN`, `NHẬP IP HOST`, `PHÒNG LAN / IP HOST`,
    `CHIA SẺ ĐỊA CHỈ HOST CHO ĐỒNG ĐỘI`.

## Hệ quả

- Chơi được khi các máy cùng mạng LAN và firewall của host cho phép UDP 47777 + UDP 7777–7786
(dải cổng game có thể dùng khi 7777 bận).
  Broadcast có thể bị chặn ở một số mạng doanh nghiệp; khi đó nhập IP host thủ công.
- Không cần Internet, không cần tài khoản UGS, không còn độ trễ trung gian Relay.
- Không có xác thực/anti-cheat/ mã hoá của dịch vụ: tin tưởng các máy trong LAN, phù hợp phạm vi
  hiện tại. Nếu sau này cần chơi qua Internet phải làm lại tầng vận chuyển/NAT.
- `CoopLobbyController` chỉ còn phụ thuộc `LanDiscoveryService`, `NetBootstrap` và `MatchFlow`;
  scene `CoopLobby` dựng lại với 16 Button (thêm `ButtonCopyAddress`) và không còn component
  `NetLobbyUI`.
- Kiểm chứng: 6 test model (`LanRoomModelTests`) + test scene-contract CoopLobby (16 Button,
  nhãn LAN), tổng suite EditMode 38/38 xanh; verify tay hai tiến trình trên cùng máy.