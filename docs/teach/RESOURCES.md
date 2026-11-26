# Iron Wasteland — nguồn học

Chỉ nguồn đã đọc và tin được. Mỗi mục ghi rõ **dùng khi nào**, để ba tháng nữa vẫn biết vì sao nó ở đây.

## Kiến thức (Knowledge)

- [Unity Manual — ScriptableObject](https://docs.unity3d.com/6000.0/Documentation/Manual/class-ScriptableObject.html)
  Tài liệu chính thức về lớp Data. Dùng cho: hiểu `TankDefinition`, `StageDefinition`, vì sao sửa asset là toàn game đổi.
- [Unity Manual — Order of execution for event functions](https://docs.unity3d.com/6000.0/Documentation/Manual/execution-order.html)
  Bảng thứ tự `Awake` → `OnEnable` → `Start` → `Update`/`FixedUpdate`. Dùng cho: vì sao `GameController.Awake` tạo tank trước khi UI kịp đọc, và `SaveSystem` phải chạy trước scene.
- [Unity Manual — Object pooling](https://docs.unity3d.com/6000.0/Documentation/Manual/object-pooling.html)
  Vì sao tạo/huỷ liên tục gây giật (GC). Dùng cho: luật “đạn phải lấy từ pool”.
- [Unity — Netcode for GameObjects manual](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.0/manual/index.html)
  Khái niệm NetworkManager, NetworkObject, RPC. Dùng cho: `Assets/Scripts/Net/` (chỉ cần phần Getting started là đủ đọc `NetBootstrap`).
- [Unity Manual — 2D physics (Rigidbody2D, Collider2D)](https://docs.unity3d.com/6000.0/Documentation/Manual/class-Rigidbody2D.html)
  Dùng cho: vì sao di chuyển tank ở `FixedUpdate` và vì sao trigger không đẩy nhau.
- [Game Programming Patterns — miễn phí, Robert Nystrom](https://gameprogrammingpatterns.com/)
  Đọc chương *Object Pool* và *State* (rất ngắn). Dùng cho: hiểu mô hình Brain/View và pool mà không cần sách dày.
- [C# — Events (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/events/)
  Dùng cho: `OnTakeDamageEvent`, `StageCleared`, cách Brain báo cho UI/HUD mà không gọi trực tiếp.
- Trong repo: `docs/UNITY_CO_BAN.md` (thao tác editor), `docs/PROJECT_CONTEXT.md` (công thức, hệ thống lõi — đúng phần lớn, riêng đường dẫn/tên scene đã cũ), `docs/teach/reference/*.html`.

## Khôn ngoan (Wisdom — cộng đồng)

- [Unity Discussions](https://discussions.unity.com/)
  Diễn đàn chính thức, có nhân viên Unity trả lời. Dùng cho: lỗi editor khó hiểu, hành vi package.
- [r/Unity2D](https://www.reddit.com/r/Unity2D/)
  Dùng cho: hỏi cách làm 2D cụ thể (sprite, collider, animation).
- [Unity Discord — kênh #editor và #scripting](https://discord.com/invite/unity)
  Dùng cho: câu hỏi ngắn cần trả lời nhanh.
- Trong đội: hỏi trực tiếp người viết module (Net, Stages, từng xe) — repo có 6 nhánh xe tăng nghĩa là mỗi người giữ một mảng.

## Lỗ hổng đã biết (Gaps)

- **Không có nguồn tiếng Việt chất lượng về Unity 6**: hầu hết video tiếng Việt dạy Unity 2019–2021, API đã khác (`linearVelocity` thay `velocity`, Input System mới). Đành dùng tài liệu tiếng Anh chính thức.
- **Không có sách/tutorial nào nói về kiến trúc Data–Brain–View của repo này**: nguồn duy nhất là chính code + `docs/`. Vì vậy bài học phải dựng từ code thật.
- **Chưa có nguồn tốt về Netcode + Relay/Lobby cho 4 người**: tài liệu Unity có nhưng ví dụ mỏng; ghi nhận để tìm sau.
