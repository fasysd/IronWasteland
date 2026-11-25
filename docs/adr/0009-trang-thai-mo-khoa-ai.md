# 0009 — Trạng thái mở khoá ải = thiết kế ∪ tiến trình

## Bối cảnh

- Thẻ 1-1 và 1-2 được thiết kế mở sẵn (`StageSpec.Unlocked = true`), 1-2 còn là ải mặc định
  (`m_DefaultStageIndex = 1`) và màn hình vẽ sẵn dải trạng thái "ĐÃ MỞ KHÓA".
- `StageSelectController.Awake` ghi đè `m_Stages[i].Unlocked = StageProgress.IsUnlocked(code)`.
  Người chơi chưa phá 1-1 thì 1-2 thành `false`, nhưng hình ảnh thẻ vẫn trông mở khoá.
  `HandleClick` bỏ qua màn có `Unlocked == false` nên bấm thẻ 1-2 không có phản ứng.

## Quyết định

`m_Stages[i].Unlocked = m_Stages[i].Unlocked || StageProgress.IsUnlocked(code)`.

- Cờ lưu trong scene là "màn mở sẵn theo thiết kế".
- Tiến trình người chơi chỉ **mở thêm**, không bao giờ khoá lại màn đã cho mở.
- Dải trạng thái 2 thẻ đầu tính từ dữ liệu thay vì hằng số: ĐANG CHỌN (cam) → ĐÃ HOÀN THÀNH
  (teal, `StageProgress.IsCleared`) → ĐÃ MỞ KHÓA (xám) → CHƯA MỞ KHÓA (xám đậm).

## Hệ quả

- 1-2 bấm chọn được ngay từ đầu, khớp với hình ảnh thẻ.
- 1-1 không còn hiện sai "ĐÃ HOÀN THÀNH" khi chưa phá ải.
- Các ải chưa có `StageDefinition` (1-3..2-2) vẫn khoá; bấm vào không đổi lựa chọn.
- Kiểm chứng play mode: click (444,454) → `SelectedStageIndex = 1`, `CoopEntry.StageCode = "1-2"`;
  `StartSingle` nạp scene `Stage_1_2`; click (668,454) (1-3) không đổi lựa chọn.
- Test EditMode 21/21 xanh.
