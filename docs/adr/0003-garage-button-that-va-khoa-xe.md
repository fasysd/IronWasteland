# 0003 — Garage dùng Button thật, khoá xe không chọn được, panel dựng procedural

- **Ngày:** 2026-11-24
- **Trạng thái:** Đã chốt
- **Liên quan:** [0001](0001-garage-dong-hoa-bang-du-lieu.md), [0002](0002-nhan-doi-ca-chu-nho.md), `docs/design/garage-spec.md`

## Bối cảnh

Scene Garage cũ dựng từ ảnh nướng chữ, `GarageController` tự hit-test bằng `Input.mousePosition` trong `OnGUI`; chỉ hàng danh sách có `Button`, còn QUAY LẠI / NÂNG CẤP / ĐỔI PHỤ KIỆN / XÁC NHẬN RA TRẬN là vùng toạ độ. Hệ quả: không có `EventSystem` thì bấm hàng không ăn, nút NÂNG CẤP không bấm được, PHÂN RÃ rơi vào vùng phụ kiện, chữ trong ảnh không đổi cỡ được.

## Quyết định

1. **Toàn bộ nút là `Button` chuẩn** — mỗi nút có `targetGraphic`, `interactable` và listener gắn lúc chạy; xoá toàn bộ hit-test toạ độ khỏi `GarageController`. Scene bắt buộc có `EventSystem` + `InputSystemUIInputModule` (project dùng Input System mới).
2. **Xe bị khoá không phản hồi** — hàng khoá vẫn hiển thị (icon khoá, chữ xám, nhãn `ẢI x-y`) nhưng bấm không đổi xe; lúc mở màn, Garage nhảy về xe mở khoá đầu tiên.
3. **Panel dựng procedural** theo `docs/design/garage-spec.md` — danh sách xe, 8 thanh chỉ số, 4 thẻ kỹ năng, panel phụ kiện đều là `Image` + `TextMeshProUGUI`; ảnh chỉ còn vai trò nền/khung/trang trí.
4. **Chữ nhỏ tăng một lần** theo ADR 0002, không chỉnh tiếp; chữ lớn giữ nguyên.
5. **Hiện đúng dữ liệu thật** — cấp lấy từ `PlayerProgress`, chân dung/màu chủ đề/mô tả kỹ năng lấy từ `TankDefinition`, phụ kiện lấy từ `AccessoryLoadout`. Bỏ bảng `TankEntry`/`m_Tanks` placeholder.
6. **Nâng cấp trừ ngọc** — `StageProgress.SpendGems` + `TankUpgradeUtility.GetCostForNextLevel`; nút tắt khi thiếu ngọc hoặc đã cấp 90.
7. **Bố cục panel phụ kiện** — header một hàng riêng, tên phụ kiện dòng riêng dưới icon, giữ caption `1 Ô DUY NHẤT`.

## Hệ quả

- Bấm được bằng chuột ở mọi nút, trạng thái nút phản ánh dữ liệu thật (thiếu ngọc thì NÂNG CẤP tắt).
- Xe khoá không thể chọn — muốn xem trước phải mở ải tương ứng.
- Ngọc hiện chưa có nguồn cấp trong game (`StageProgress.Gems` mặc định 0), nên trên bản chạy thật nút NÂNG CẤP luôn tắt cho tới khi có hệ thống thưởng ngọc; số `125` ở header vẫn là ảnh nướng.
- Sửa bố cục phải chạy lại `GarageBuilder.Rebuild()`; mọi text phải có hộp cao tối thiểu ~1,6 lần cỡ chữ nếu không TMP cắt còn 0 ký tự.
