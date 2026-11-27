# 0013 — Đồng bộ animation quái và VFX ghost có kiểm soát

- Ngày: 2026-11-27
- Trạng thái: Đã chấp nhận
- Liên quan: m08788, m08937, m09749; mở rộng `docs/adr/0012-dong-bo-visual-ky-nang-quai.md`

## Bối cảnh

Kênh ghost ở ADR 0012 đã đưa visual kỹ năng xuống client, nhưng thân quái vẫn đứng Idle và các đòn đánh
không có pose. Nguyên nhân: client tắt mô phỏng AI (`EnemyBrain.IsServerSimulated = false`) nên
`EnemyBrain.Update` return sớm; mọi `Animator.SetTrigger/SetFloat` chỉ chạy trên host. Ngoài ra:

- `NetworkAnimator` chưa có trên prefab quái, nên kể cả trigger có chạy cũng không qua mạng.
- `NetworkTransform` nằm ở root, còn AI xoay `EnemyModel` con; hướng nhìn của quái không được gửi.
- Client không có tốc độ di chuyển nên Animator kẹt ở Idle dù NetworkTransform vẫn trượt quái.
- Ghost skill giữ `localScale` của prefab thay vì scale thế giới host, nên vết chém KIenLua to gấp 3,63 lần
  và nằm cao hơn; sortingOrder 0 làm nó bị thân quái che.
- Ghost VFX bị tắt toàn bộ `MonoBehaviour` nên các script diễn hoạt thuần hình ảnh (mở rộng vòng, xoay,
  fade) đứng yên; aura không bám quái và đạn không bay.

Yêu cầu: host vẫn giữ toàn quyền gameplay; client thấy đúng pose cơ thể, hướng nhìn, tốc độ, skill và VFX
của cả quái thường lẫn boss, không gây sát thương lần hai.

## Quyết định

1. **Dùng `NetworkAnimator` của NGO trên root prefab quái**, trỏ tới `Animator` ở child `EnemyModel`.
   Component cho phép đặt ở root và tham chiếu Animator con. Toàn bộ tham số được `Synchronize = true`
   **trừ `MoveSpeed`**, vì client tự suy tốc độ từ `NetworkTransform`. Công cụ
   `Assets/Editor/NetEnemySyncSetup.cs` (menu **Iron Wasteland/Net/Rebuild Enemy Animation Sync**) gắn
   component, gán Animator và ghi đè danh sách tham số; chạy lại an toàn (idempotent).
2. **Mọi trigger animation của quái đi qua `NetEnemyAnimation.Trigger(GameObject owner, Animator fallback,
   int hash)`**. Hàm tìm `NetworkAnimator` trên owner hoặc cha; nếu `IsSpawned && IsServer` thì gọi
   `NetworkAnimator.SetTrigger(hash)`, ngược lại gọi thẳng `Animator` (offline/Editor). Đã thay các điểm
   phát trigger trong `D1onkBrain`, `DeadlandPhantomBrain`, `ElementalReaperVFXController`,
   `ElementalReaperClone`, `DeadlandPhantomClone`; `EnemyModel.PlayTakeDamage` cũng dùng đường này.
   `EnemyModel.PlayTakedDamage` (chết) giữ trigger cục bộ vì máu 0 đã được đồng bộ và cả hai phía cùng gọi
   `PlayDeath`.
3. **Đồng bộ pose phụ bằng `EnemyNetState`:** thêm `NetworkVariable<float> m_ModelYaw` (server ghi, client
   đọc). Server gửi góc Z của `EnemyModel` mỗi `PoseInterval = 0.1s` nếu lệch ít nhất 0,5°; client làm mượt
   bằng `Mathf.MoveTowardsAngle(..., 720°/s)`. Client tự đo tốc độ từ delta vị trí root `NetworkTransform`,
   làm mượt với `SpeedSmoothing = 12` rồi gọi `EnemyModel.SetMoveSpeed`. Nhờ vậy chuyển Idle/Move khớp mà
   không gửi thêm tham số `MoveSpeed`.
4. **Ghost VFX được phép chạy script thuần hiển thị có kiểm soát:** thêm interface
   `INetVisualGhostDriver` trong `NetVisualGhost.cs`. `NetVisualGhost.Sanitize(GameObject ghost,
   bool allowVisualDrivers)` luôn tắt mọi `Collider2D`, giữ marker, giữ các script đánh dấu interface khi
   `allowVisualDrivers = true`, và tắt mọi `MonoBehaviour` còn lại. Chỉ 15 script đã kiểm tra là thuần
   hình ảnh được đánh dấu: `DeadlandSlashVFX`, `SoulSummonPulseVFX`, `SpatialRiftVFX`,
   `CardinalShieldVFX`, `BurstChargingAuraVFX` (DeadlandPhantom); `D1onkChargingAuraVFX`,
   `D1onkStunVFX`; `BurstChargingAuraVFX`, `BurstShieldVFX`, `DocQuatVFX`, `ElementalAuraVFX`,
   `FireTornadoVFX`, `LavaBeamVFX`, `ThuyLieuVFX`, `ThuyTrieuVFX` (ElementalReaper). Các script
   gây sát thương hoặc summon trong Update/trigger (`D1onkLazeNguyenToVFX`, `D1onkToaNhietDienRongVFX`,
   `D1onkTrungPhatVFX`, `D1onkVienBinhSummonVFX`) **không** được đánh dấu và vẫn bị tắt trên client.
5. **Gói `IW.SkillVisual` mang đủ dữ liệu hình ảnh:** thêm `worldScale`, `attachToRoot`, `animate`,
   `velocity`, `homingTargetId`, `turnSpeed`, `homingDelay`, `homingDuration` bên cạnh offset/góc local
   và lifetime. Client gắn ghost vào root thì đặt `localScale = worldScale / root.lossyScale`; ghost standalone
   nhận thẳng `worldScale`. Aura/cone đặt `attachToRoot = true` và local offset nên bám quái; đạn nhận
   velocity/homing; vùng đất giữ nguyên vị trí cast.
6. **Sửa hiển thị vết chém KIenLua:** ghost giữ đúng scale thế giới host (1,375 × 0,6875 thay vì 5 × 2,5) và
   SpriteRenderer của child `Skill` trên `KIenLuaKhongLo`, `KySinhThienThach`, `MatTrangDen` được nâng
   `sortingOrder` lên 10 để không bị thân quái che.
7. **Bỏ child `Skill` chết của `MatTroiDo`:** brain `NormalEnemy_MatTroiDo` chỉ dùng
   `ExplosionZone_MatTroiDo` + Ký Sinh Thiên Thạch, không gọi `Skill_KySinhThienThach`. Child `Skill` và
   marker `NetVisualGhost` trên prefab gốc đã bị xoá; child `Skill` còn lại nằm dưới
   `MatTroiDo/KySinhThienThach_Template/EnemyModel/Skill` (template offline, `active = false`) được giữ
   nguyên.
8. **Phạm vi prefab:** 18 prefab quái đều có `EnemyNetState` + `NetworkTransform`. 16 prefab có Animator
   nên được gắn `NetworkAnimator`; `The2Face` và `Enemy_Elite` không có Animator trong prefab nên không thể
   đồng bộ animation thân cho tới khi có model/Animator.

### Ghi chú hậu review

- `BongMaTuDiaVFXController` không còn tự gọi `Animator.SetTrigger` cho bốn đòn boss; trigger chỉ đi qua
  `NetEnemyAnimation.Trigger` từ brain/clone để tránh gửi trùng và giữ đúng thẩm quyền host.
- Ghost của laser/stun D1onk và các aura gắn thân được parent vào điểm phát **trước** khi broadcast, rồi mới
  đặt `localPosition = Vector3.zero`; nhờ vậy client nhận đúng offset thay vì vị trí cast ban đầu.
- Bước dọn `Skill` chết của `MatTroiDo` chỉ tìm con trực tiếp `root.transform.Find("Skill")`, nên chạy lại
  setup không xoá nhầm `Skill` trong template triệu hồi.

## Hệ quả

- Client thấy pose Idle/Move, hướng nhìn, animation trúng đòn, animation chết, các đòn đánh boss và VFX diễn
  hoạt; sát thương, HP, vị trí và quyết định kỹ năng vẫn do host.
- `MoveSpeed` không đi qua `NetworkAnimator`; client suy từ `NetworkTransform` nên có thể lệch rất nhỏ khi
  mạng giật, nhưng không phát sinh gói tin tham số liên tục.
- Ghost VFX chạy script thuần hình ảnh nên collider vẫn tắt; các hiệu ứng trigger chỉ còn phần nhìn. Bất kỳ
  script nào có khả năng gây sát thương/summon **không** được thêm interface.
- `The2Face` và `Enemy_Elite` chưa có body animation; `The2Face`, `SEAUBEARUS` chưa có prefab VFX trong
  catalog (thư mục chỉ có Animations). Host ghi cảnh báo và bỏ qua VFX thiếu Id, không chặn trận.
- Kiểm chứng Development Build host + 1 client: `BongMaTuDia` có 17 trigger host, client nhận state
  `BongMaTuDia_DeadlandSlash` và ghost `VFX_DeadlandSlash`/`VFX_DeadlandSoulProjectile`/
  `VFX_SoulSummonPulse`; `KIenLuaKhongLo` có 98 ghost host / 97 ghost client, client nhận
  `KIenLuaKhongLo_Idle`/`_Move`, ghost scale `(1.38, 0.69)`, sorting 10, attached; `TruongQuanNgucD1ONK`
  nhận `D1onk_PhaoHamDaNong`; `SEAUBEARUS` nhận Idle/Move; `TuThanNguyenTo` nhận Idle/Move + VFX;
  `The2Face` spawn được nhưng không có Animator. Không có exception/NRE.
- EditMode 44/44 pass; `dotnet build Assembly-CSharp.csproj` 0 lỗi.
