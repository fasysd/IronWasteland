# ADR 0002 — Nhân đôi cỡ chữ nhỏ trong Garage

- **Trạng thái:** Đã chấp nhận (2026-11-24)
- **Bối cảnh:** Trên màn 1920×1080, các dòng chữ phụ (vai trò, cấp, ải khoá, mô tả kỹ năng, hiệu ứng phụ kiện) quá nhỏ so với chữ tiêu đề và tên xe.

## Quyết định

- **Chữ nhỏ nhân đôi**, chữ lớn giữ nguyên: tên xe trong danh sách (22), tên xe lớn ở giữa (39.6), số cấp lớn (34.8), số chỉ số (23.76) không đổi.
- Các cỡ đã tăng: vai trò trong hàng 13→20, cấp/ải trong hàng 12→18, phụ đề xe 13.6→18, vai trò dưới tên 17→22, "+ CẤP n" 17→22, mô tả kỹ năng 14.44→20, tên phụ kiện 19→22, dòng chỉ số phụ kiện 10.2→16, mô tả phụ kiện 8.8→14.
- Ô chữ phải cao tối thiểu ~1.25× cỡ chữ; nếu thấp hơn, TMP cắt thành 0 ký tự (đã gặp với tên xe và dòng cấp/ải).
- Panel phụ kiện phải nén lại: tên ở y690, hai dòng chỉ số ở y712, mô tả ở y754 (2 dòng, cắt bằng dấu …) để không đè lên hàng nút ĐỔI PHỤ KIỆN / PHÂN RÃ.

## Hệ quả

- Chữ phụ đọc được ở khoảng cách xem bình thường mà không phải dựng lại ảnh nền.
- Nhãn nướng trong PNG (tiêu đề panel, ATK/HP/EN/DEF/SPD, "1 Ô DUY NHẤT", "04 SLOTS") vẫn giữ cỡ cũ; muốn đổi phải xuất lại ảnh thiết kế.
- Một số mô tả dài bị cắt bằng dấu … thay vì hiện toàn bộ.
