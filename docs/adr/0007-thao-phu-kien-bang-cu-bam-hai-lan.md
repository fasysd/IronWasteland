# 0007 — Tháo phụ kiện bằng cú bấm thứ hai

- **Ngày:** 2026-11-25
- **Trạng thái:** Đã chốt

## Bối cảnh

Người chơi muốn tháo phụ kiện nhanh để phân rã: mở màn Chọn phụ kiện, chọn món đang trang bị mà không có thao tác "bỏ trang bị" nào, nên phải chọn món khác rồi xác nhận. Ngoài ra trạng thái "không trang bị gì" trước đây không biểu diễn được: `AccessoryLoadout.Get` trả mặc định 0 nên món 01 luôn bị coi là đang trang bị và bị chặn phân rã.

## Quyết định

1. **Thêm giá trị `None = -1`** vào `AccessoryLoadout`. `Get` trả `None` khi xe chưa từng chọn hoặc đã tháo; `Has(tank)` là nguồn duy nhất để biết xe có phụ kiện hay không.
2. **Mặc định không trang bị gì.** Trước đây mọi xe ngầm trang bị món 01 khi chưa chọn; nay mở Garage hiện `CHƯA TRANG BỊ` và món 01 phân rã được như mọi món khác.
3. **Bấm hai lần để tháo.** Ở màn Chọn phụ kiện, cú bấm đầu vào thẻ đang trang bị chỉ chốt focus (`m_OpenedFocus`), cú bấm thứ hai gọi `Unequip()`: `Clear` tồn kho loadout, `m_SavedIndex = None`, `m_PendingIndex = None` (bỏ focus), refresh thẻ.
4. **Xác nhận không trang bị lại.** Cờ `m_Unequipped` chặn `Confirm()` gọi `Set` sau khi vừa tháo; xác nhận chỉ đóng màn.
5. **Badge theo trạng thái trang bị, không theo focus.** `SelectedGroup` (`ĐANG DÙNG`) chỉ bật khi `i == m_SavedIndex`; focus vẫn thể hiện bằng nền/viền thẻ.
6. **Nút PHÂN RÃ đổi sang đỏ** (`#C0392B` → `#8E1F17`, viền `#FF7A6B`, chữ `#FFF1EE`) để tách khỏi các nút trung tính.

## Hệ quả

- `GarageAccessoryPanel`, màn phân rã (`IsEquipped`) và màn chọn đều dựa trên `Has` + `Get`; món không ai trang bị luôn phân rã được.
- Dữ liệu cũ của người chơi chưa từng chọn phụ kiện sẽ hiện `CHƯA TRANG BỊ` (không mất lựa chọn nào vì trước đó chưa có thao tác chọn thật).
- Thao tác tháo lưu ngay vào PlayerPrefs, không cần xác nhận; đóng màn bằng ESC/X vẫn giữ trạng thái đã tháo.
- Phụ kiện vẫn chưa ảnh hưởng chỉ số chiến đấu (ngoài phạm vi ADR này).
