# ADR 0001 — Garage động hoá bằng dữ liệu, không sửa code khi thêm xe

- **Trạng thái:** Đã chấp nhận (2026-11-24)
- **Bối cảnh:** Ba panel của màn Garage (danh sách xe, chỉ số chiến xa, phụ kiện/kỹ năng) ban đầu là ảnh PNG có chữ nướng sẵn. Muốn thêm xe, đổi vai trò, đổi cấp hay khoá theo ải đều phải sửa ảnh hoặc sửa code và dựng lại scene.

## Quyết định

1. **Dữ liệu là nguồn sự thật.** Danh sách xe đọc từ `TankRoster` (thứ tự các `TankDefinition`). Thông tin hiển thị thêm nằm ngay trên `TankDefinition`: `rolePrimary`, `roleSecondary`, `unlockStageCode`, `rosterIcon`, `startingLevel`. Thêm xe = thêm asset + kéo vào roster, không sửa code, không chạy lại builder.
2. **Tách phần tính toán thành model thuần.** `TankListModel`, `StatBarModel`, `AccessoryCardModel`, `SkillSlotModel` nằm trong assembly `IronWasteland.UiModels` (không tham chiếu UnityEngine) và có test EditMode. Controller chỉ map dữ liệu game sang nguồn của model rồi đổ kết quả vào UI.
3. **Builder lo bố cục, controller lo nội dung.** Builder dựng khung, hàng mẫu, thanh chỉ số, badge phím; chạy một lần. Controller đổ chữ/số/độ dài thanh lúc chạy.
4. **Khoá theo tiến trình ải.** Xe khoá khi `unlockStageCode` khác rỗng và `StageProgress.IsUnlocked` trả false; nhãn `ẢI x-y`, hàng bị làm mờ và hiện ổ khoá.
5. **Thanh chỉ số chuẩn hoá theo roster.** Mẫu số là giá trị lớn nhất của từng chỉ số trong roster tại cấp 90; thanh luôn nằm trong 0..1.

## Hệ quả

- Thêm/bớt xe, đổi vai trò, đổi ải mở khoá chỉ cần sửa asset trong Inspector.
- Phần logic hiển thị có test tự động, tránh hồi quy khi chỉnh format chữ.
- Danh sách dài hơn 6 xe tự cuộn dọc, hàng giữ nguyên kích thước.
- Ảnh nền vẫn chứa một số nhãn tĩnh (tiêu đề panel, nhãn ATK/HP…); muốn đổi các nhãn đó vẫn phải xuất lại ảnh từ thiết kế.
