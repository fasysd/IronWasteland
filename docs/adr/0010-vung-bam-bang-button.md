# 0010 — Vùng bấm là Button thật, giữ nguyên toạ độ thiết kế

- Ngày: 2026-11-27
- Trạng thái: Đã chấp nhận
- Liên quan: m03492, m03524, m03525, m03557

## Bối cảnh

Bảy màn UI (MainMenu, CoopLobby, GarageAccessory, SkillPicker, Setting, StageSelect, AccessorySalvage)
trước đây tự bắt chuột bằng `OnGUI` + `Rect(...).Contains(designPosition)` trong controller. Cách này:

- không có phản hồi hover/press chuẩn của uGUI, không hỗ trợ bàn phím/gamepad qua EventSystem;
- khó kiểm thử vì vùng bấm là số nằm rải trong controller, dễ lệch khi đổi layout;
- trùng lặp logic bấm ở từng màn.

Garage đã chuyển sang Button thật từ ADR 0003 nhưng bảy màn còn lại thì chưa.

## Quyết định

1. Mỗi vùng bấm là một `UnityEngine.UI.Button` thật. Nếu vùng đã có `Image` hiển thị thì gắn Button
   thẳng lên Image đó; nếu vùng là khoảng trống thì tạo `Image` trong suốt làm vùng bấm.
   Rect giữ **nguyên 100%** so với thiết kế 1920×1080.
2. Button dùng `Transition = None` để hình thức không đổi so với trước.
3. Thẻ trong danh sách dùng **một** Button trên nền thẻ, truyền chỉ số bằng persistent listener
   (`UnityEventTools.AddIntPersistentListener`) tới `controller.SelectCard(i)`, không tạo Button cho từng chữ con.
4. Controller bỏ `HandleClick` và các field `Rect` vùng bấm; chỉ còn các method public `PressX` / `SelectCard`.
   Kéo danh sách, lăn chuột, phím tắt (Esc/Enter/mũi tên) giữ nguyên.
5. Mỗi scene có `EventSystem` + `InputSystemUIInputModule` và Canvas có `GraphicRaycaster`. Builder gọi
   `UIHit.EnsureEventSystem(scene)`: tìm EventSystem sẵn có trong scene, chỉ tạo mới khi thiếu.
6. `Assets/Editor/UIHit.cs` là helper dùng chung: `EnsureEventSystem`, `Attach(Graphic)`, `AddZone(parent, ...)`.
7. Kiểm chứng bằng hai seam test EditMode:
   - `UILayoutModel` (thuần C#) quy đổi rect gốc trên-trái sang `anchoredPosition` gốc dưới-trái;
   - `UIClickZoneSceneTests` mở từng scene và khẳng định EventSystem, GraphicRaycaster, đúng số Button,
     đúng rect, `raycastTarget`, persistent listener đúng tên hàm.

## Hệ quả

- Vùng bấm hoạt động với chuột, bàn phím/gamepad UI và có trạng thái `Selectable` chuẩn.
- Muốn đổi vùng bấm phải sửa builder và test scene-contract (cố ý, để hình và vùng bấm không lệch nhau).
- Kéo thẻ vẫn dùng `OnGUI` `MouseDrag`; cờ `m_SuppressCardClick` chặn cú click phát sinh sau khi vừa kéo.
- Danh sách màn đã chuyển (số Button): MainMenu 4, CoopLobby 16, GarageAccessory 23, StageSelect 13,
  AccessorySalvage 26, SkillPicker 7, Setting 3. Garage đã dùng Button từ ADR 0003.
- Test suite EditMode: 32/32 xanh; `unity recompile` 0 lỗi.
