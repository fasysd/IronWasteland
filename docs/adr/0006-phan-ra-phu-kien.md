# ADR 0006 — Màn Phân rã phụ kiện: tồn kho, giá trị phân rã và luồng xác nhận

- **Ngày:** 2026-11-25
- **Trạng thái:** Đã chấp nhận

## Bối cảnh

Garage có nút PHÂN RÃ nhưng chỉ là placeholder (không có `onClick`). Cần màn phân rã phụ kiện lấy Ngọc, dùng lại bố cục màn CHỌN PHỤ KIỆN TRANG BỊ và thêm ô chọn số lượng. Catalog hiện chỉ có 7 field mô tả (Key, DisplayName, Category, Role, Stats, Description, Icon) — không có giá, độ hiếm hay số lượng; tiền tệ duy nhất trong game là Ngọc (`StageProgress`).

## Quyết định

1. **Có tồn kho thật**: thêm `AccessoryInventory` (static, PlayerPrefs khoá `iw.acc.<Key>`) với `Get`/`Set`/`Add`/`TrySpend`/`ResetAll`. `Get` mặc định **1** để cả 20 món dùng được ngay; nguồn thưởng sau này chỉ gọi `Add` nên không phải sửa UI.
2. **Thưởng bằng Ngọc**, không thêm tiền tệ mới: cộng qua `StageProgress.AddGems`.
3. **Giá trị phân rã là dữ liệu**: `AccessoryCatalog.Entry.SalvageValue` (mặc định 5), seed theo nhóm trong `GarageAccessoryBuilder.DeriveSalvageValue` (index <8 → 5, <14 → 8, còn lại 12). Tổng nhận = giá trị × số lượng.
4. **Scene riêng, nạp chồng**: `Assets/Scenes/AccessorySalvage.unity` dựng bằng `Assets/Editor/AccessorySalvageBuilder.cs`, mở từ `GarageController.OpenSalvage` (dùng chung helper `OpenOverlay`/`SyncOverlay` với màn chọn phụ kiện).
5. **Một loại mỗi lần**, không multi-select, không giỏ hàng; giữ đủ 20 thẻ cố định để so sánh.
6. **Trang bị không tiêu hao tồn kho**, nhưng chặn phân rã món đang được bất kỳ xe nào trang bị (badge + khoá stepper + nút mờ).
7. **Món ×0 vẫn hiện thẻ** nhưng mờ + `CHƯA SỞ HỮU`, không chọn được để phân rã.
8. **Stepper tự vẽ** (`−  [N]  +  MAX`), clamp 1..min(tồn kho, 99), giữ nhấn để tăng nhanh; không dùng `TMP_InputField`/`EventSystem` vì màn này bắt chuột bằng `OnGUI` + toạ độ thiết kế.
9. **Xác nhận xong ở lại màn**: trừ tồn kho, cộng ngọc, refresh thẻ + tổng, hiện toast `ĐÃ PHÂN RÃ ×N · +X NGỌC` 2,5 s; đóng bằng ESC hoặc nút X.
10. **Tách toast khỏi phụ đề**: thêm TMP `ToastText` riêng (trước đó fallback ghi đè dòng phụ đề, trông như lỗi).
11. **Nút xác nhận mờ theo trạng thái**: `m_ConfirmButtonImage.color = alpha 0,42` khi không phân rã được, vì sprite nút là art cam nướng nên chỉ đổi màu chữ không đủ rõ.

## Hệ quả

- Toàn bộ logic tính toán nằm trong `IronWasteland.UiModels.SalvageQuoteModel` (thuần C#, 4 test EditMode) nên đổi luật (trần 99, điều kiện chặn) chỉ sửa một chỗ.
- `AccessoryCatalog` được dựng lại từ `GarageAccessoryBuilder`, nên `SalvageValue` **phải** seed trong builder, sửa tay trên asset sẽ mất khi rebuild.
- Màn phân rã và màn chọn phụ kiện dùng chung art (`card_normal`, `card_selected`, `button_confirm`…) nhưng khác builder; sửa bố cục thẻ phải sửa cả hai (thẻ phân rã lệch lên so với khung mặc định nhờ `CardContentShiftY = -18`).
- Chưa có nguồn cộng tồn kho và chưa có nguồn tiêu Ngọc trong gameplay, nên hiện Ngọc chỉ tăng từ phân rã.
- Phụ kiện vẫn chưa ảnh hưởng chỉ số chiến đấu (không file combat nào đọc `AccessoryLoadout`), nên phân rã hiện là nguồn Ngọc thuần tuý.

## Cập nhật 2026-11-25 — đồng bộ tồn kho với trang bị

Người chơi báo Garage hiện "đang trang bị HẮC NGUYỆT" nhưng màn phân rã lại báo "CHƯA SỞ HỮU". Truy ra hai lỗ hổng, **không đổi luật chặn phân rã món đang trang bị** (quyết định 6 vẫn giữ):

1. **Chỉ được trang bị món còn tồn kho.** Màn CHỌN PHỤ KIỆN trước đây không kiểm `AccessoryInventory` nên vẫn trang bị được món ×0. Nay thẻ ×0 mờ 0,35, nhãn `CHƯA SỞ HỮU`, không chọn/không xác nhận được (`GarageAccessoryController.IsOwned`); `Awake` còn dọn loadout cũ trỏ tới món ×0 hoặc ngoài danh mục.
2. **Không sở hữu được ưu tiên báo trước đang trang bị.** `SalvageQuoteModel.Build` nay xét `owned <= 0` trước `isEquipped`, nên món ×0 luôn báo `CHƯA SỞ HỮU`; `AccessorySalvageController` chỉ coi là đang trang bị khi `owned > 0`.
3. **Gợi ý nói rõ cách xử lý.** `EquippedHint` đổi thành `ĐANG TRANG BỊ · THÁO Ở MÀN CHỌN PHỤ KIỆN ĐỂ PHÂN RÃ`; panel Garage cũng dọn loadout trỏ món không sở hữu và hiện `CHƯA TRANG BỊ`.

Kiểm chứng: picker với món 06 ×0 → nhãn `CHƯA SỞ HỮU`, alpha 0,35, `Select(5)`/`Confirm()` không đổi loadout; Garage với loadout trỏ món ×0 → panel trống và khoá loadout bị xoá; màn phân rã với món ×1 đang trang bị → hint mới, với món ×0 → `CHƯA SỞ HỮU`. Test EditMode 22/22 xanh.
