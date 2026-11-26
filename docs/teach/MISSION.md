# Mission: Hiểu dự án Iron Wasteland

## Why

Người dùng là lập trình viên mới với Unity, đang làm việc trên repo game `IronWasteland` (Unity 6, URP 2D, co-op online). Mục tiêu thật: hiểu đủ dự án để **tự tin mở Unity, chạy thử, đọc code và nhận một việc nhỏ mà không làm hỏng thứ khác** — chứ không phải học Unity cho bằng hết.

## Success looks like

- Mở Unity, bấm Play một màn bất kỳ và nói được màn đó gồm những thành phần nào.
- Đọc một file `.cs` bất kỳ trong `Assets/Scripts/` và xếp được nó vào lớp Data / Logic / View.
- Nghe “sửa cân bằng xe”, “thêm một đợt quái”, “đổi HUD” là biết mở đúng file/asset, không cần grep mò.
- Tự làm được một việc nhỏ (sửa bug UI, chỉnh số liệu màn, thêm một prefab quái) và giải thích được mình đã đụng vào lớp nào.
- Đọc hiểu một luồng runtime: đạn đi từ input → skill → va chạm → sát thương → chết.

## Constraints

- Người mới với Unity: cần giải thích khái niệm editor, không giả định kiến thức sẵn có.
- Thời gian học xen trong lúc làm việc thật: mỗi buổi 10–15 phút, một thắng lợi cụ thể.
- Tài liệu trong repo bằng tiếng Việt; bài học cũng viết tiếng Việt.
- Không đẩy code lên remote và không chạy kiểm tra/verify trừ khi người dùng yêu cầu rõ.
- Đang ở nhánh `Fix/HUD-ThanhMau-BossBar`; nội dung bài học phải khớp code nhánh này, không tin tài liệu cũ trong `docs/`.

## Out of scope

- Shader/HLSL, tối ưu render, build pipeline, CI.
- Thiết kế màn chơi và cân bằng số liệu theo GDD (chỉ cần biết sửa ở đâu).
- Unity multiplayer chuyên sâu (đồng bộ tick, dự đoán phía client, host migration).
- Học Unity tổng quát kiểu “làm game 2D từ đầu” — chỉ học phần dự án này dùng.
