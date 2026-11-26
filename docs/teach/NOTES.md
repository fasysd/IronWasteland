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
- Đang ở nhánh `Fix/HUD-ThanhMau-BossBar` (trước đó là `SuaLoi_Coop`): code có `Save/` mới, `Stages/` dùng `StageDirector` + `EnemySpawner` (không phải `StageManager`), wave nằm trên `EnemySpawner` trong scene. Tài liệu `docs/PROJECT_STRUCTURE.md` viết cho trạng thái cũ — **kiểm chứng trong code trước khi dạy**.
- `AGENTS.md` ở gốc repo bị `.gitignore` bỏ qua (dòng 84) nên chỉ có tác dụng trên máy này.

## Cách dạy hiệu quả với người này

- Đi từ **một việc cụ thể** (“sửa cái này ở đâu?”) rồi mới mở ra khái niệm, không dạy khái niệm trước.
- Luôn chỉ **đúng file:dòng** để họ mở được ngay, đừng mô tả chung chung.
- Câu hỏi kiểm tra phải buộc **nhớ lại** (không nhìn tài liệu), phản hồi tức thì.
- Tránh nhồi: mỗi bài một ý lớn, tối đa ~10 phút đọc.

## Đã rà soát tài liệu cũ (26/11/2026)
- Bài 01, `reference/ban-do-code.html`, `reference/thuat-ngu.html`, `learning-records/0001` đã sửa: nhánh `Fix/HUD-ThanhMau-BossBar`, 226 file C#, `Stages/` 15 file, wave nằm trên `EnemySpawner` trong scene (`EnemyWave` + `WaveTimeline`), `StageDefinition` chỉ còn mục tiêu/thưởng/mã màn.
- `docs/CONG_VIEC_DA_LAM.md` để nguyên vì là nhật ký lịch sử (dòng 135 mô tả đúng trạng thái trước 26/11).

## Bài đã soạn

- `lessons/0001-ban-do-iron-wasteland.html` — bản đồ dự án 10 phút + quiz 6 câu.
- `lessons/0002-thiet-ke-dot-quai-va-vat-can.html` — update 26/11/2026: wave chuyển vào `EnemySpawner`, `WaveTimeline`, vật cản sinh theo seed, màn 1-4 sửa cân bằng, cách validate 9 scene + quiz 5 câu.
- `lessons/0003-mot-con-quai-di-qua-nhung-file-nao.html` — trace 8 bước runtime (TickWaves → SpawnWave → Spawn → TakeDamage → Die → OnSpawnerEnemyDied → EvaluateOutcome), quy tắc đọc file `.cs`, quiz 5 câu + bài tập Find All References.
- `reference/thuat-ngu.html`, `reference/ban-do-code.html` — hai tờ tra cứu.
- `reference/unity-co-ban.html`, `reference/ngu-canh-du-an.html` — bản HTML của `docs/UNITY_CO_BAN.md` và `docs/PROJECT_CONTEXT.md`, sinh bằng `tools/md2html.py` (đừng sửa tay: sửa `.md` rồi chạy lại lệnh trong đầu file script).
