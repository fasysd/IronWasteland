# ADR 0005 — Số ngọc và giá nâng cấp đều là dữ liệu động

- **Ngày:** 2026-11-24
- **Trạng thái:** Đã chấp nhận

## Bối cảnh

Pill ngọc ở header Garage in cứng số 125 trong header.png; nút NÂNG CẤP dùng upgrade.png có viên ngọc nướng ở góc dưới-phải nên không canh giữa được. Giá nâng cấp đã lấy từ TankUpgradeUtility.GetCostForNextLevel nhưng vẫn có ký tự 5 mẫu trong scene.

## Quyết định

1. Xoá số nướng bằng cách phủ một ô màu nền pill #121821 (65×34 tại 1644,92) rồi đặt TMP GemCount canh trái trong pill. GarageController.RefreshGemCount() đọc StageProgress.Gems mỗi khung hình và chỉ in lại khi giá trị đổi; gọi thêm trong Awake và Select.
2. Tách viên ngọc khỏi art nút: tạo upgrade_button_clean.png (art đã xoá ngọc) và upgrade_gem.png (ngọc tách bằng alpha theo khoảng cách màu so với nền cam từng dòng). Builder gắn ngọc thành Image con canh giữa nút; số giá in trong hộp 70×54 canh phải nên tự căn giữa dọc.
3. Giá nâng cấp vẫn đi qua TankUpgradeUtility (nguồn duy nhất) và được in lại mỗi lần Select/nâng cấp, nên đổi công thức giá ở một chỗ là UI theo ngay.

## Hệ quả

- Sau này chỉ cần hệ thống thưởng ngọc gọi StageProgress.AddGems là số ở header tự cập nhật, không sửa scene.
- Art gốc upgrade.png vẫn giữ nguyên (dùng làm nền thanh cấp); muốn đổi vị trí ngọc chỉ sửa anchoredPosition của UpgradeGem trong GarageBuilder.
- Nếu sau này đổi nền pill khác #121821 thì phải đổi màu GemCountPatch, nếu không sẽ thấy ô chữ nhật lệch màu.
- Cỡ số giá (2026-11-25): hộp 92×62 tại x 72, font 36 px, canh phải + y = 2 — số 1 chữ số cao ~30 px, xấp xỉ chiều cao nhãn "NÂNG CẤP" nướng trong art; số 2 chữ số vẫn gọn trong nút. Đây là cỡ tối đa thực tế vì mép phải hộp số đã sát viên ngọc; muốn to hơn phải dời ngọc hoặc làm lại art nút không kèm nhãn.
