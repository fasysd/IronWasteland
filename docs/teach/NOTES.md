# Ghi chú giảng dạy

Ghi lại sở thích, bối cảnh và những điều cần nhớ khi soạn bài cho người dùng này.

## Về người học

- Mới với Unity (tự nhận), đang làm repo `IronWasteland`. Không giả định biết Editor, Prefab, ScriptableObject.
- Dùng tiếng Việt; bài học viết tiếng Việt, thuật ngữ Unity giữ nguyên tiếng Anh kèm giải thích (vì phải tra cứu bằng tiếng Anh).
- Học xen trong lúc làm việc: 10–15 phút một buổi, cần một thắng lợi cụ thể mỗi bài.
- Đã quen Git ở mức cơ bản (tự fetch/checkout nhánh). Chưa chứng minh được khả năng đọc kiến trúc.

## Quy ước của phiên làm việc

- **Không push, không commit** trừ khi người dùng yêu cầu rõ.
- **Không tự chạy build/verify/Play** để “kiểm tra” — chỉ hướng dẫn người dùng tự làm trong Unity.
- Đang ở nhánh `SuaLoi_Coop`: code có `Save/` mới, `Stages/` dùng `StageDirector` (không phải `StageManager`), UI garage đã đổi. Tài liệu `docs/PROJECT_STRUCTURE.md` viết cho trạng thái cũ — **kiểm chứng trong code trước khi dạy**.
- `AGENTS.md` ở gốc repo bị `.gitignore` bỏ qua (dòng 84) nên chỉ có tác dụng trên máy này.

## Cách dạy hiệu quả với người này

- Đi từ **một việc cụ thể** (“sửa cái này ở đâu?”) rồi mới mở ra khái niệm, không dạy khái niệm trước.
- Luôn chỉ **đúng file:dòng** để họ mở được ngay, đừng mô tả chung chung.
- Câu hỏi kiểm tra phải buộc **nhớ lại** (không nhìn tài liệu), phản hồi tức thì.
- Tránh nhồi: mỗi bài một ý lớn, tối đa ~10 phút đọc.

## Bài đã soạn

- `lessons/0001-ban-do-iron-wasteland.html` — bản đồ dự án 10 phút + quiz 6 câu.
- `lessons/0002-thiet-ke-dot-quai-va-vat-can.html` — update 26/11/2026: wave chuyển vào `EnemySpawner`, `WaveTimeline`, vật cản sinh theo seed, màn 1-4 sửa cân bằng, cách validate 9 scene + quiz 5 câu.
- `reference/thuat-ngu.html`, `reference/ban-do-code.html` — hai tờ tra cứu.
- `reference/unity-co-ban.html`, `reference/ngu-canh-du-an.html` — bản HTML của `docs/UNITY_CO_BAN.md` và `docs/PROJECT_CONTEXT.md`, sinh bằng `tools/md2html.py` (đừng sửa tay: sửa `.md` rồi chạy lại lệnh trong đầu file script).
