# Spec: Wave design on EnemySpawner + vật cản + sửa màn 1-4

Status: implemented (26/11/2026) — chờ Play Mode xác nhận thủ công cho 1-4.

## Vấn đề

1. Thiết kế đợt quái nằm trong `StageDefinition` (ScriptableObject), còn vùng spawn và
   logic bắn wave nằm ở `StageDirector` (scene). Muốn sửa một đợt phải mở asset + scene,
   và `StageDirector` vừa là "trọng tài" vừa là "người bắn quái".
2. Màn 1-4 không thể thắng: 3 lõi `SaoDen` (`targetId = core`, mục tiêu DestroyTargets 3)
   bật `includeSelf` nên tự hồi `Attack 100` mỗi 0.5 s = 200 HP/s, trong khi tank Wall Titan
   level 12 gây ~11.5 sát thương/đòn. Đợt 2 còn gọi 6 boss 2000 HP.
3. Bản đồ 9 màn trống, không có vật cản nào.

## Quyết định

- **Dữ liệu wave chuyển vào scene.** `EnemyWave`/`EnemyWaveSpawn` giữ đúng tên field cũ
  (`Label`, `StartTime`, `Spawns`, `Enemy`, `Count`, `SpeedMultiplier`, `ZoneId`) để khối
  YAML copy nguyên từ asset sang component `EnemySpawner` không phải đổi tên.
- **StageDirector chỉ còn trọng tài.** Nó gọi `spawner.BeginWaves()`, đẩy
  `spawner.TickWaves(dt)`, còn `WaveStarted` do spawner phát và Director chuyển tiếp.
- **Lịch wave là lớp thuần C#** (`WaveTimeline`) để test bằng `dotnet` khi Unity đóng;
  wave bắn theo `StartTime`, không theo thứ tự khai báo (sắp xếp ổn định).
- **Vùng spawn thuộc spawner** (`spawnZones`), không còn trên Director.
- **Vật cản sinh theo seed** (`ObstacleLayout` + `StageObstacleField`): cùng màn ⇒ cùng bố cục,
  vành 4–10 m, cách nhau ≥ 2.2 m, không lấn `SpawnZone`, layer Terrain, đăng ký
  `PathfindingGrid2D` để quái A* né.
- **1-4 sửa bằng dữ liệu, không đổi code AI**: `includeSelf 0`, `Attack 40`, `Defense 10`,
  `MaxHealth 280`, đợt boss 6 → 2. Giữ objective DestroyTargets 3 lõi đang có thay vì
  đổi sang thiết kế KillBoss 4 phase trong `docs/PLAN_Stages_Spawner_SFX.md` §4.2
  (đổi objective là thay đổi thiết kế màn, ngoài phạm vi yêu cầu).

## Không làm (non-goals)

- Không xoá `StageManager.cs`: dù không scene nào gắn, nó vẫn được
  `NetBootstrap`/`NetAutoConnect`/`MatchFlow`/`NetStageEvents`/`NetLobbyUI` tham chiếu.
  Nó giữ một bản wave song song cho đường mạng cũ — nợ kỹ thuật đã ghi nhận.
- Không làm vật cản phá được (`DestructibleTarget` trên đá).
- Không đổi giao diện `StageSelect`/lobby để hiện số đợt (dữ liệu giờ ở scene).
- Không chạy Play Mode tự động; 1-4 cần một lần chơi thử thủ công.

## Kiểm chứng

- `dotnet build Assembly-CSharp.csproj`: Build succeeded.
- `dotnet run --project Tools/WaveTimelineTests`: 20 PASS / 0 FAIL.
- `Unity.exe -batchmode -executeMethod StageDataValidator.ValidateAll`:
  PASS 9/9 màn (log `Logs/stage-validate2.log`), gồm 1-4 "2 đợt / 14 quái, 3 lõi core",
  mỗi lõi `includeSelf = false`, `MaxHealth <= 350`.
- Tổng quái sau migrate khớp asset cũ: 45/48/35/14/50/26/30/24/14 (1-4 giảm 18→14 do boss 6→2).

## Việc còn mở

- Chơi thử 1-4 trong Unity để xác nhận thắng được (ước tính ~22 đòn/lõi, ~13.1 sát thương/đòn).
- Cân nhắc gộp `StageDataValidator` và `StageQAAudit` (đang trùng danh sách màn + kiểm wave).
- Client mạng: `StageDirector.Update` bỏ qua tick wave ở client nên HUD đợt quái đứng ở "Đợt 1"
  như hành vi cũ; nếu muốn HUD chạy trên client thì phải tick `TickWaves` ở nhánh client và
  chấp nhận `WaveStarted` phát trên client.
