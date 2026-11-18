# PLAN: Chơi online 4 người (co-op PvE) — IronWasteland

> **Trạng thái:** Bản 4 — đã qua 3 vòng review đối kháng (kiểm chứng code + phản biện kiến trúc + audit vòng 3). **Chưa sửa mã nguồn nào.**
> **Phiên bản:** 4.1 (thay bản 4 ngày 2026-11-16; chốt 5 câu hỏi ở §6.2: host mô phỏng 100%, reconnect ngoài MVP, lobby overlay, 30 quái/wave mặc định, không scale HP quái). Bản 4 sửa 5 lỗi chặn của vòng 3 (aim khi host cast, hai cờ proxy, collider tank proxy, guard `Awake`, cổng quyền phá offline), sửa số liệu sai (35 call site damage, 42 chỗ `Show`, 14 file `Destroy`, 6 prefab tank) và cắt phần chưa cần cho MVP (bỏ scene Lobby riêng, đưa reconnect ra backlog, gộp state khiên/effect).
> **Ngày lập:** 2026-11-15
> **Phạm vi:** Unity 6000.5.6f1, 2D URP, NGO 2.x + Relay/Lobby + UTP.

---

## Tiến độ thực hiện (cập nhật 2026-11-16)

> Nhánh `feature/online-coop-4p`. `.git` đã được cấp quyền ghi; **chỉ commit local, không push** (theo yêu cầu người dùng).

| Giai đoạn | Trạng thái | Đã làm |
|---|---|---|
| GĐ 0 — Cài NGO + cờ phân quyền | ✅ Xong | Cài `com.unity.netcode.gameobjects` 2.14.0, `com.unity.services.relay` 1.2.0, `com.unity.services.lobby` 1.3.0; `TankBrain.IsLocallyControlled`/`IsServerSimulated`; `EnemyBrain.IsServerSimulated`; `ChasingEnemyBrain.FindPlayerTank()` chọn người chơi còn sống gần nhất; 3 script test bọc `#if UNITY_EDITOR` |
| GĐ 1 — Host spawn xe | ✅ Xong | Root `Network` trong `SampleScene` (`NetworkManager` + `UnityTransport` + `NetBootstrap` + `NetSpawnService` + `NetLobbyUI`), roster 6 `TankDefinition`; `NetworkObject` + `PlayerNetState` + `NetPlayerController` + `NetworkTransform` (Authority = Owner) trên đúng 6 prefab tank; test host spawn `BlackShadow(Clone)` 650/650 |
| GĐ 2 — Chia quyền owner/proxy + validate | ✅ Xong phần chính | `GameController.BindTank/DestroyLocalTank/createLocalTankOnAwake`; `NetPlayerController` proxy kinematic (giữ collider), camera `NetCameraFollow`, server kiểm tra quãng đường (`speedTolerance` 1.6 + `positionSlack` 0.15) — test teleport 5 unit bị kéo về |
| GĐ 3 — Đồng bộ state | ✅ Xong | `PlayerNetState` có `NetworkVariable` Health/Energy/`CooldownState` + `NetLoadout`; host poll 10 Hz, chỉ ghi khi đổi ≥ 0.1 (máu/năng lượng) hoặc khi hồi chiêu tăng; client nhận loadout rồi `InitializeTank` đúng chỉ số của chủ xe, sau đó tự đếm ngược hồi chiêu |
| GĐ 4 — Combat về host | ✅ Xong phần MVP | `TankBrain`/`EnemyBrain` chặn `TakeDamage`/`Heal`/`GainEnergy`/`SpendEnergy`/`ApplyCrowdControl` khi đang chạy mạng mà không phải host; client bấm kỹ năng gọi `NetPlayerController.RequestCastServerRpc` (host `SetLookTarget` + `UseSkill`); **42/42 chỗ `TextDamageManager.Show` đã chuyển sang façade `CombatFeedback.Show`** — offline hiện cục bộ, online chỉ host hiện + gửi named message `IW.CombatFeedback` (gộp 0,5 s theo vị trí cho DoT). Chưa làm `kind` heal/damage (xem chỗ khác plan #5) và VFX kỹ năng thuộc GĐ 6 |
| GĐ 5 — Quái là NetworkObject | 🟡 Xong phần host | Thêm `NetworkObject` + `EnemyNetState` + `NetworkTransform` (Authority = Server, Interpolate) cho **24 prefab quái**; `NetEntity.Despawn` thay `Destroy(gameObject)` ở 16 chỗ/15 file; `NetSpawnService.SpawnEnemy(prefab, position, stats)` spawn qua NGO rồi mới `Initialize`; `StageManager.SpawnEnemy` rẽ nhánh online/offline và bỏ qua trên client; `NetBootstrap.ClearSceneEnemies` dọn 11 quái đặt sẵn trong scene khi vào phòng. **Vá 6 lỗi [BLOCKING] từ audit**: gate `EnemyMovementAgent.FixedUpdate`/`OnEnable` bằng `SetServerSimulated`, quái con `MatTroiDo` spawn qua `NetSpawnService` khi online, gate `MeteorParasiteBrain.OnContact/Detonate`, `EliteEnemyBrain_Test` dùng `NetEntity.Despawn`, `EnemyBrain.Die` chỉ chạy hook gameplay trên host (client vẫn phát visual chết), `BasePatrol.Start` không set path trên client; đặt `NetworkObject.SynchronizeTransform = false` cho cả 24 prefab để tránh nội suy kép. Test host: `BlackMoon` 260 máu, đánh 100 → 160, net var khớp, despawn sạch; `XenNuoc` 360 máu có movement `moveSim=True`, đánh 9999 → chết và despawn sau 1 frame. **Chưa test client thật**; còn hở: projectile/hazard/zone của quái chỉ host mô phỏng nên client không thấy visual, `MaxHealth` chưa sync (chỉ ảnh hưởng nếu bật `SetCoopMode`) |
| GĐ 6 — VFX, aim, proxy visual | 🟡 Xong phần chính | `TankBrain.PlayRemoteSkillFx` + named message `IW.SkillFx` (`NetSkillFx`) phát lại visual đạn/kỹ năng bằng pool cục bộ; `PlayerNetState.AimAngle` (NetworkVariable, host ghi 10 Hz, lệch ≥ 1°) cho proxy xoay nòng; `NetProxyVisual` suy hướng thân từ delta vị trí; `WaterB_Utils.GetAimWorldPosition` để WaterB bắn theo LookTarget thay vì chuột của máy nhận; `Skill_EarthWall.VisualOnly` để tường phát lại không tự chặn đường client. Test host: replay slot 0 sinh đúng 1 đạn, năng lượng/hồi chiêu không đổi, LookTarget được trả lại; slot 2 bị chặn bởi whitelist; `AimAngle` khớp `Atan2(LookTarget - position)` chính xác. **Đã bổ sung**: cơ chế ghost `NetVisualSpawn` (named message `IW.Visual`, client tự dựng bản sao visual tắt collider, tự huỷ theo lifetime, trần 120 ghost) — decoy `ShadowExchangeClone` có ghost (test `-iw-selftest-visual`: client `ghosts=1` suốt 6 s rồi về 0), đạn quái `XaThuTinhThe` + `QuaiVatTinhThach` đi qua `NetVisual.Spawn` (test `-iw-selftest-enemy`: client `ghosts=2→3` khi quái bắn, HP host 650→454 khớp client). **Vùng hazard**: thêm kênh thu hồi ghost `IW.VisualEnd` (handle do host cấp), `NetVisualSpawn.Follow/End` dùng `ConditionalWeakTable`; hook 5 vùng pool `MatTroiDo`/`NamDocGaiThach`/`QuaiVatDamLay`/`SaoDen`/`TinhLinhNghiaDia` (test `MatTroiDo`: client `ghosts=1` khi vùng hoạt động rồi về 0). **Còn lại**: decoy vẫn không phải `NetworkObject` (ghost chỉ để nhìn, host quyết định hấp thụ đòn) |
| GĐ 7 — Relay/Lobby thật | ✅ Xong | `NetRelayService` bọc Unity Services: `EnsureServicesAsync` (init + đăng nhập ẩn danh), `CreateHostAllocationAsync` (allocation 3 chỗ + join code), `JoinAllocationAsync`, `CreateLobbyAsync` (lobby công khai, `Data["relayJoinCode"]`), `JoinLobbyAndGetRelayCodeAsync`, `ResetSession`; `NetBootstrap.StartHostWithRelay()`/`StartClientWithRelay(code)` gắn `UnityTransport.SetRelayServerData`; `NetLobbyUI` thêm ô mã Relay + mã phòng và 3 nút Relay (giữ nút IP trực tiếp làm fallback); `NetAutoConnect` thêm `-iw-relay-host`/`-iw-relay-join <code>`. **Kiểm chứng thật qua internet**: host log `Relay code=FBQJJ6 lobby=True lobbyCode=RD69BQ` (region asia-southeast1); client `-iw-relay-join FBQJJ6` → hai bên `clients=2 tanks=2`, host thấy `owner=1 hp=650 local=False`, client sở hữu `owner=1`; test 4 người (host + 3 client, join code `WLMD7F`) → host `clients=4 tanks=4` owner 0/1/2/3, mỗi client thấy đủ 4 xe và chỉ `local=True` ở xe mình, tất cả 650/120. **Chưa test**: join bằng mã phòng lobby qua UI (đường code đã có), relay với 4 máy khác mạng thật (mới test 4 tiến trình cùng máy) |
| GĐ 0.5 — Màn chơi cho co-op | ✅ Xong | Gắn `StageManager` vào `SampleScene` (GameObject `Stage`, autoStart tắt), tạo asset `Assets/Prefabs/Stages/Stage_01.asset` (3 đợt × 10 quái: `XenNuoc` → `QuaiVatTinhThach` → `XaThuTinhThe`, `killTarget = 30`, `timeLimitSeconds = 90` chỉ hiển thị). `NetBootstrap` thêm field `stageManager`, host tự `StartStage()` ngay sau `StartHost()` (cả đường IP lẫn Relay). `StageManager` giờ lấy **roster toàn đội** (`FindObjectsByType<TankBrain>(Include)`, làm mới mỗi 0,5 s) thay vì 1 tank → chỉ thua khi tất cả người chơi chết; quái chết vẫn đếm về `Kills` khi ải đang chạy. Test host trong Play: `StartHost()` → `State=Running`, đợt 1 spawn **10 XenNuoc qua NGO**, giết 2 quái → `Kills=2`, cả đội chết → `State=Failed`. **Test player build 2 người**: host log `[Stage] Bat dau ai 1-1 - Sương Mù Tím … 3 dot / 30 quai`, `Đợt 1/3 - Nước Độc (10 quái) t=0.0s`, `Đợt 2/3 - Đá Vụn (10 quái) t=20.0s`; client thấy `enemies=20` đúng bằng số quái 2 đợt đã spawn (kèm hp từng con), `ghosts` tăng 40 rồi về 0 khi quái bắn, **0 exception**; cả hai xe chết → host `[Stage] THUA ai 1-1 … (0/30 mạng)`. **Kiểm chứng nhánh THẮNG + 4 người (player build)**: host `-iw-host -iw-selftest-win` tự giữ xe sống và dọn quái → log `[Stage] Ha guc 1/30 … 30/30`, `[Stage] THANG ai 1-1 - Sương Mù Tím sau 40.0s (30/30 mang)`, `[NetSelfTest] win -> state=Cleared kills=30/30`; client nhận `[Stage][Client] BẮT ĐẦU - 0/30 mạng sau 0.3s` rồi `[Stage][Client] THẮNG - 30/30 mạng sau 40.0s`. **4 người (host + 3 client IP)**: host `clients=4 tanks=4` owner 0/1/2/3, `[NetBootstrap] Duyệt client 1/2/3`, mỗi client nhận `[Stage][Client] BẮT ĐẦU` đúng lúc vào (18.3s/19.6s/21.4s) và `THUA` chung lúc 26.8s khi cả đội chết. **Còn thiếu**: `StageManager` phía client vẫn `Idle` (không tự chạy ải, chỉ nhận sự kiện qua `NetStageEvents` — HUD sẽ đọc sự kiện này), chưa kiểm chứng đếm mạng khi người chơi thật bắn giết quái (batchmode không bắn được); thắng/thua mới ở mức **HUD debug** (`NetDebugHud` gắn trong root `Network`: dòng đầu có `ải=… kills/target`, banner `THẮNG`/`THUA` giữa màn hình, F3 bật/tắt; UI chính thức do nhóm UI làm). **Bổ sung 2026-11-17 (vòng đời trận)**: thêm `MatchFlow` trên root `Network` điều phối đúng luồng *chọn tank → chọn màn → chọn chơi đơn/đa → lobby (đổi tank, sẵn sàng) → vào trận → thắng/thua → chơi lại/thoát*; `NetBootstrap` **không còn tự `StartStage()`** khi mở phòng và **không spawn xe lúc client kết nối** (chờ vào trận), tank đã chọn trong lobby do host lưu theo từng `clientId`; lobby hiển thị mã Relay/mã phòng, danh sách người chơi + tank + trạng thái sẵn sàng, host đổi màn và bắt đầu khi mọi người sẵn sàng (auto-start chỉ khi co-op có ≥ 2 người); thắng/thua lấy từ `StageCleared`/`StageFailed` của `StageManager`; "chơi lại" despawn toàn bộ xe + `StageManager.ResetToIdle()` rồi về lobby. Chế độ chơi đơn = host 1 người vào trận ngay. |
| GĐ 0.6 — MaxHealth quái, CastResult, khiên/effect, va chạm xe | ✅ Xong (2026-11-17) | `EnemyNetState.m_MaxHealth` sync 1 lần lúc spawn (client áp trước khi áp HP); `NetPlayerController.CastResult` + `CastRejectedClientRpc` chỉ gửi owner, HUD hiện `cast=`; `NetEffectSync` (named message `IW.Effect`) gửi (loại, stacks, duration, `ServerTime`) khi áp/gỡ và `PlayerNetState.ShieldAmount` sync 10 Hz; `TankCollisionSetup` tắt va chạm Tank–Tank, toàn bộ collider của 6 prefab tank chuyển sang layer `Tank` (6) — kiểm chứng Play: `ignore66=True`, `ignore67=False`. |
| Test biến thể Optional Skill | ✅ Xong (2026-11-18) | `NetBootstrap.SetLocalOptionSkills` + `MatchFlow.RequestOptionSkills` (op 4/5, snapshot kèm 2 byte/người) + `NetLobbyUI` có khu "Test ky nang (bien the Optional)" trước khi vào phòng và 2 nút đổi biến thể trong lobby; host dựng xe bằng `SpawnTankFor(clientId, tankIndex, option1, option2)`. Kiểm chứng Play: chọn (2,1) trước khi host → xe spawn `OptionSkill1=2, OptionSkill2=1`; Replay + đổi (0,2) → trận mới spawn đúng (0,2). Sửa kèm 2026-11-18: `PlayerNetState.SetServerLoadout` bỏ guard `IsServer` (chỉ đúng sau spawn) → dùng `NetworkManager.Singleton.IsServer`, nhờ vậy `m_Loadout` được ghi TRƯỚC `SpawnAsPlayerObject` và client nhận đúng biến thể ngay trong gói spawn (trước đó netVar rỗng 0/0 nên client dùng biến thể mặc định trong khi host dùng biến thể đã chọn). |
| Aura khiên theo nguồn + số liệu trận định kỳ | ✅ Xong (2026-11-18) | User báo: khiên Tide Commander (Leviathan ban cho BlackShadow) hiện **hào quang bóng đêm** sai ở cả host lẫn client vì `NetStatusVisual` chọn sprite theo prefab tank chứ không theo nguồn khiên. Sửa `NetStatusVisual`: thêm `ShieldAuraKind { None, Default, Water, External }`, `ClassifyShieldId(id)` — id chứa `Bubble` → External (skill tự vẽ bong bóng), chứa `TideCommander`/`Altar` → **Water** (sprite `asset_water_bubble`, đường kính 5,8 unit, alpha tối đa 0,38), còn lại → Default; cùng một luật chạy trên dữ liệu đã sync (`NetEffectSync` cho client, `ActiveEffects` cho host) nên hai bên luôn chọn cùng sprite. Sửa luôn visual gốc Optional Skill 1 biến thể 1 (Water Armor) trên `Leviathan.prefab`: `VisualArmor.localScale` 0,5 → **0,85**, alpha 0,7255 → **0,45** (to và mờ hơn để nhìn rõ xe). Đồng bộ số liệu trận: thêm `NetStageEvents.StateRunning = 3` + `BroadcastStatus(StageManager)`; `StageManager.Update` gửi nhịp **1 giây/lần** (chỉ host, khi `StageState.Running`) kèm `Kills/KillTarget/Elapsed`; client `HandleMessage` cập nhật cache nhưng **không log** gói nhịp; `NetLobbyUI.DrawResultPanel` đọc số liệu từ cache `NetStageEvents` khi là client (trước đó đọc `StageManager` local luôn `Idle` nên hiện 0/30, 0 s). Kiểm chứng bằng **dev build** (host `-iw-host -iw-port 7815`, client `-iw-join 127.0.0.1 -iw-port 7815`): client log `[MatchFlow] Client nhận phase mạng: Lobby -> Playing` rồi `Playing -> Failed` và `[Stage][Client] THUA - 0/30 mạng sau 26.9s` ⇒ bản thân phase đã sync đúng, lỗi còn lại chỉ là số liệu hiển thị (đã sửa). Release build lại: `Builds/Windows/IronWasteland_Data/Managed/Assembly-CSharp.dll` 687 616 byte lúc 10:15:33, `Build Finished, Result: Success.` **Sửa tiếp cùng ngày (banner THUA còn sót ở client sau trận đầu):** user báo vào lobby lần sau vẫn thấy chữ THUA giữa màn hình dù đang ở lobby. Nguyên nhân: `NetStageEvents.ResetState()` chỉ xoá cache **trên host**, client giữ `StageState = StateFailed` của trận trước và `NetDebugHud` vẽ banner từ cache đó. Sửa: thêm `NetStageEvents.StateIdle = 4` + `BroadcastIdle()` (gửi state 0 kèm kills/target/elapsed 0, client `ApplyState` đặt `HasStageState = false`); `MatchFlow` thay 3 chỗ `ResetState()` bằng helper `ResetStageEventsState()` = `ResetState()` + `BroadcastIdle()` (gọi trong `OnHostStarted`, `Replay`, `StartMatchInternal`); `NetDebugHud` đổi banner THẮNG/THUA sang đọc `MatchFlow.Phase` (dữ liệu đã sync) thay vì cache sự kiện ải — hai lớp bảo vệ để trạng thái cũ không hiện lại. **Sửa tiếp cùng ngày (vào trận mới là thua ngay):** user báo sau khi thua một trận, vào trận mới là hiện `KET QUA: THUA` với `0/30`, `0s` (cả host lẫn client). Nguyên nhân: `StageManager.m_Players` giữ tham chiếu tới xe của **trận trước** đã bị despawn; `RefreshRoster` còn trong thời gian chờ 0,5 s nên chưa quét lại, `EvaluateOutcome` thấy roster "có người" nhưng mọi phần tử đã bị huỷ ⇒ `AnyPlayerAlive()` false ⇒ `FailStage()` ngay frame đầu. Sửa: `StartStage()` và `ResetToIdle()` đều `m_Players.Clear(); m_RosterTimer = 0f;` (buộc quét lại); `EvaluateOutcome()` chỉ xét các phần tử **khác null**, bỏ qua khi roster chỉ còn tham chiếu đã huỷ. Kèm sửa thông báo lỗi Relay: `NetRelayService.DescribeRelayError()` dịch "join code not found"/"expired" thành câu hướng dẫn người chơi xin mã mới thay vì hiện `Not Found: join code not found`. Release build lại: `Assembly-CSharp.dll` 688 640 byte lúc 10:44:25, `Build Finished, Result: Success.` |
| Kiểm chứng nhiều client (build Windows thật) | ✅ Pass 4 người + đồng bộ xe/quái | `NetAutoConnect` đọc tham số dòng lệnh (`-iw-host`, `-iw-join <ip>`, `-iw-port`, `-iw-selftest`, `-iw-selftest-cast`, `-iw-selftest-enemy`, `-iw-selftest-stage`, `-iw-godmode`, `-iw-clean`) và log `[NetState]` mỗi 2 s; build player tại `Builds/Windows/IronWasteland.exe`. **2 client**: host `clients=2 tick=20 tanks=2`, client thấy đúng 2 xe và chỉ sở hữu xe mình; host tự đánh 120 → hp **650 → 571**, client nhận đúng **571**; client `RequestCast(1)` → host trừ năng lượng **120 → 80**, client cũng thấy **80**. **4 người (1 host + 3 client trên một máy)**: host `clients=4 tanks=4` với owner 0/1/2/3, mỗi client thấy đủ 4 xe và chỉ `local=True` ở xe mình, tất cả 650/120. **Quái**: host spawn `XenNuoc` hp 360 → client thấy `id=16 hp=360`; host đánh 100 → host 303, client 303; host despawn → cả hai `enemies=0`. Tank bị quái đánh 650→552→454 cũng khớp ở client. Sau khi bỏ đăng ký prefab runtime, không còn log `duplicate GlobalObjectIdHash`. **Relay/Lobby qua internet**: host `Relay code=FBQJJ6 lobby=True lobbyCode=RD69BQ`; client join bằng mã → `clients=2 tanks=2`; test **4 người qua Relay** (mã `WLMD7F`, host + 3 client cùng lúc) → host `clients=4 tanks=4` owner 0/1/2/3, mỗi client thấy đủ 4 xe, chỉ `local=True` ở xe mình, máu/năng lượng khớp. Sau khi bật `ConnectionApproval`: host log `[NetBootstrap] Duyệt client 1: tank=0 level=1 equipment=-1 opt1=0 opt2=0`, `clients=2 tanks=2`, không còn `Incomplete connection request` lẫn spam NullReferenceException. **Sự kiện ải (`NetStageEvents`) trên player build 2 người**: host `-iw-host -iw-selftest-stage` log `[Stage] Bat dau ai 1-1`, đợt 1/2, `Ha guc 1/30`/`2/30`, `[NetSelfTest] giet 2 quai -> Kills=2/30 state=Running`; client vào giữa trận nhận `[Stage][Client] BẮT ĐẦU - 2/30 mạng sau 15.2s` (snapshot kills hiện tại) rồi `[Stage][Client] THUA - 2/30 mạng sau 26.5s` khi cả đội chết; 0 exception hai bên. **Lobby + disconnect (player build 2 người)**: host `-iw-relay-host` tạo phòng `8RCGCW`; client dùng `-iw-lobby-query 8RCGCW` tìm phòng công khai → đọc mã Relay trong `Data` → kết nối, host `clients=2 tanks=2`, client chỉ `local=True` ở xe mình; khi client thoát host về `clients=1 tanks=1` (xe được `NetSpawnService.DespawnPlayerTank` dọn, không còn xe ma). Test join thật bằng `-iw-lobby-join` trên cùng một máy báo `player is already a member of the lobby` vì 2 tiến trình dùng chung danh tính Unity Services ⇒ thêm đường `-iw-lobby-query` và đổi dữ liệu phòng sang `DataObject.VisibilityOptions.Public` (MVP chưa có tài khoản). **Host thoát giữa trận**: `UnityTransport.DisconnectTimeoutMS` hạ từ 30 s (mặc định) xuống **10 s**; test player 2 người, `taskkill` host → sau ~10 s client log `[NetBootstrap] Mất kết nối tới host (clientId=1).`, HUD hiện banner `MẤT KẾT NỐI` và dòng debug có `kết nối=MẤT`. **Join lại sau khi rời phòng (player build)**: client rời êm bằng `-iw-exit-after 8` (`NetBootstrap.Shutdown()` → `Application.Quit`) → host về `clients=1` trong ~2 s, client mới `-iw-join` vào được ngay (host log `Duyệt client 2`, client thấy đủ xe). **Kiểm chứng ảnh hưởng tới client còn lại (host + 3 client)**: khi client C **rời êm**, host đi `clients=4 → 3`, hai client còn lại **không** mất kết nối (`lost=True` = 0) và vẫn đồng bộ HP/vị trí. Ngược lại, khi client bị **kill tiến trình**, host thấy `Disconnect ... [ProtocolTimeout]`, rồi **toàn bộ client còn lại cũng bị ngắt theo**: host đi `clients=4 → 3 → 1`, cả hai client còn lại log `[NetBootstrap] Mất kết nối tới host`; sau đó mọi client mới đều không vào được (driver UTP vẫn `created=True listening=True` nhưng không còn sự kiện `Connect`). Đã thử trên cả host Editor lẫn host player, cổng 7799/7802/7803 và **7811** (cổng không bị Editor chiếm) — kết quả vẫn wedge ⇒ loại trừ nguyên nhân cổng. **Bẫy môi trường khi test trên một máy**: nếu Editor từng vào Play rồi thoát không sạch, tiến trình Editor có thể vẫn giữ socket `UDP 127.0.0.1:7777`; player host bind `0.0.0.0:7777` vẫn báo `listening=True` nhưng client nối `127.0.0.1:7777` sẽ chạm socket chết của Editor và không bao giờ vào được. Vì vậy khi test player phải dùng `-iw-port` khác 7777 (hoặc nối bằng IP LAN). **4 người qua LAN IP thật** (`-iw-join 192.168.1.7 -iw-port 7811`, không dùng loopback): host `clients=4 tanks=4`, vị trí từng xe ổn định qua 14 s (`5.4,-2.1`, `6.6,2.1`, `-0.6,0.0`, `2.5,0.0`), máu/năng lượng 650/120. Một lần chạy trước đó thấy vị trí trôi bất thường (y tăng dần tới 143 rồi 263, host và client lệch nhau) nhưng **không tái hiện** ở 2 lần chạy lại — nghi do tiến trình cũ còn sót; cần theo dõi nếu gặp lại. Sự kiện ải phát cho cả 4 người: host log `[Stage] THUA ai 1-1 - tank nguoi choi bi ha tai t=26.8s (0/30 mang).` và **cả 3 client** đều nhận `[Stage][Client] THUA - 0/30 mạng sau 26.8s.` cùng mốc thời gian. ⇒ Ghi nhận là hạn chế transport (xem mục 6.1), MVP phải tạo lại phòng khi có người crash. **Đường khôi phục "tạo lại phòng" đã kiểm chứng (player build, `-iw-rehost-after 25`)**: host có client vào rồi client bị kill, sau 25 s host `Shutdown()` → chờ 2 s → `StartHost()` → log `[NetAutoConnect] rehost StartHost ok=True listening=True`, client mới join được (`Duyệt client 2`, `clients=2 tanks=2`, mỗi bên thấy đúng 2 xe và vị trí khớp). **Chưa test**: bấm nút Lobby trong UI (đã bấm được nút mở phòng Relay và nút join bằng mã Relay), va chạm 4 xe, 4 máy khác mạng. **Input bàn phím/chuột thật**: chưa kiểm chứng được bằng công cụ giả lập (SendInput/keybd_event của Windows không tới được Unity player chạy trong phiên sandbox); đã kiểm chứng gián tiếp đường client→host bằng `-iw-selftest-move` (client tự dịch chuyển `2.9 → 5.9`, host thấy đúng `5.9`) và bằng `RequestCast` qua RPC (năng lượng 120 → 80 khớp hai bên). **Đồng bộ đạn/visual (player build, cổng 7825)**: client cast Normal (Shadow Claw) → log `[NetSkillFx] phát lại kỹ năng 0 cho xe BlackShadow(Clone) (owner=1)`; chụp burst 200 ảnh cách 80 ms thấy đạn **bay khỏi nòng sang phải** (tâm x ≈ 732 → 772 → 806 → 835 → 869 px) ⇒ client thấy đạn tank. Ghost đạn quái (`XaThuTinhThe`) được host gửi kèm `velocity` nên cũng bay trên client (montage 0,3 s/khung: đạn dịch sang phải rồi biến mất) — sửa 2 lỗi gốc: `NetSkillFx` tra xe bằng `TryGetTank` (dictionary chỉ có dữ liệu trên host) và ghost thiếu vận tốc. |

**Vòng audit đối kháng 2026-11-17 (3 reviewer: tầng Net / đồng bộ gameplay / plan vs code) — đã sửa:**

| # | Lỗi [BLOCKING] | Cách sửa |
|---|---|---|
| 1 | Loadout tới sau khi spawn, `TankBrain.Initialize` return sớm ⇒ client sai chỉ số | `SetServerLoadout` gọi **trước** `SpawnAsPlayerObject`; `ApplyLoadout` retry 0,1 s + log khi thiếu definition |
| 2 | Client không tick CC/hiệu ứng/hồi chiêu (Update bị khoá bởi `IsServerSimulated`) | Client vẫn tick CC/effects; `PlayerNetState` sync slow/blocked + hồi chiêu (drift cả tăng lẫn giảm) |
| 3 | Root/Stun không chặn xe client; validation cộng dồn `positionSlack` | `RemoteMovementBlocked`/`RemoteSlowSpeedFactor` + ngân sách di chuyển có trần + `CorrectPositionClientRpc` chỉ gửi owner |
| 4 | AimAngle sai quyền và chỉ đúng lúc cast | Owner-write, cập nhật mỗi 0,1 s; `AimAngle => IsOwner ? ReadAimAngle() : netVar` |
| 5 | `Show(36)` debug còn trong `OnUseSkill` | Đã xoá |
| 6 | Named message không kiểm sender | 4 handler: `senderClientId != ServerClientId → return` |
| 7 | Ghost trần 120 thì drop gói mới; ghost lifetime 0 sống mãi | Evict ghost cũ nhất; lifetime mặc định 30 s |
| 8 | `RequestCast` không rate-limit | Giới hạn 0,08 s/lần |
| 9 | Client tự `StartStage()` trong `Start()` | Gate `!IsServer` |
| 10 | `EnemyBrain.ApplyEffect` chạy trên client | Gate `!IsServerSimulated` |
| 11 | `NetLoadout` không validate payload | Clamp Level 1..90, Equipment ≥ -1, Option 0..2 |

**Chưa sửa (đã ghi nhận, ưu tiên sau):** visual khiên/buff trên client (giá trị khiên/effect đã sync nhưng chưa vẽ icon/aura); snapshot `ActiveEffects` cho người vào giữa trận; AimAngle 10 Hz; damage text gộp ô 0,75 unit; GreenFox dùng `Random`/`Time.time` nên replay client lệch chút; `NetAutoConnect` nên tách `Net/Dev/` + asmdef; bổ sung test tự động; sửa ~10 tham chiếu `file:dòng` còn lệch; gộp 3 cờ boolean thành `enum EntityRole`.

**Đã xong sau bảng trên (2026-11-17/18):** `MatchFlow`/`MatchPhase` + ready gate + chơi lại; `CastResult` gửi riêng owner; `EnemyStatsSnapshot` (MaxHealth quái); sync khiên/`ActiveEffects` (`NetEffectSync`); tắt va chạm xe–xe bằng layer `Tank`; mở replay mọi slot 0–3 cho mọi tank; visual thủ tục cho 4 loại quái; HUD chọn biến thể Optional Skill; **lobby lifecycle** (heartbeat 15 s, `DeleteLobby` khi host đóng, `RemovePlayer` khi thành viên rời, `VisibilityOptions.Member` — kiểm chứng Play: lobby tạo lúc 07:4x, heartbeat > 15 s không lỗi, `Shutdown()` xoá phòng, `LobbyId=null/IsLobbyHost=False/heartbeat=False/LastError=null`). **A2 — màn kết thúc trận + tạo lại phòng trong build release (2026-11-18):** `NetLobbyUI` có overlay toàn màn hình khi `SessionEnded`/`ConnectionLost`/Phase `Cleared|Failed`; host có nút **"Tạo phòng mới"** gọi `NetBootstrap.Rehost(useRelay)` (coroutine `Shutdown()` → chờ 0,6 s → `StartHostWithRelay()`/`StartHost()`, nhớ loại phòng qua `NetLobbyUI.HostMode`), client "Mất kết nối" có "Về menu"; panel kết quả hiện `Kills/KillTarget` + thời gian + tên màn. Kiểm chứng Play: host Relay tạo lobby `GBTFLF` → `StartMatch()` → giết xe → `phase=Failed/stage=Failed` → `Rehost(true)` → sau ~15 s lobby mới `8KJ9DG` + Relay `L8CPQG`, `phase=Lobby`, `players=1`, `IsLobbyHost=True`, heartbeat chạy, `LastError=null`. **Chưa kiểm chứng**: nút "Tạo phòng mới" sau `SessionEnded` thật (cần client thứ hai rời phòng) và overlay trên player build 2 máy. **B5 — visual khiên/buff trên client (2026-11-18):** thêm `Assets/Scripts/Net/NetStatusVisual.cs` gắn trên root 6 prefab tank, mỗi prefab có 2 object con dựng sẵn `ShieldAura` (order 6, scale theo sprite để phủ ~4,6 unit) và `BuffAura` (order 5, ~3,6 unit). Client đọc `PlayerNetState.ShieldAmount` (đã sync từ host) để bật hào quang khiên nhấp nháy; buff đọc `NetEffectSync.GetEffects(ownerClientId)` (host/offline đọc `TankBrain.ActiveEffects`), bỏ các id dạng khiên/guard/fortress/guardian, màu suy ra từ hash tên hiệu ứng. Kiểm chứng Play trên host: `AddShield(200, 20)` → `ShieldAura.enabled=True`, alpha nhấp nháy 0,45; trước khi có khiên `enabled=False`. **Sửa 2026-11-18 (sau khi user test 2 cửa sổ):** overlay kết thúc giờ hiện **cả khi NGO đã ngắt kết nối** (`OnGUI` chỉ cần `IsEndState()`, không cần `IsRunning`) nên host bấm thoát phòng thì client thấy "MẤT KẾT NỐI TỚI HOST" + nút "Về menu" thay vì bị đẩy thẳng ra menu; thêm đổi profile đăng nhập ẩn danh (`NetRelayService.CurrentProfile`/`SwitchProfileAsync` + nút "Doi profile test" trong `NetLobbyUI`) để test join bằng mã phòng trên cùng một máy — kiểm chứng Play: profile `client1` → playerId `TfOgacQEWKpAb7Ei0yQRLBpiEasU`, profile `client2` → `abByeYSdLZ0I5Jm36mfSAUnE1AAm`, cả hai `signedIn=True`, `LastError=null` (mỗi profile có player id riêng nên không còn lỗi "already a member").

**Bổ sung visual thủ tục (2026-11-17):** thêm kênh named message `IW.VisualPrimitive` trong `NetVisualSpawn` cho các object host tạo bằng code (không có template trong prefab): vệt lửa `FlamePatch` (fade + order −1) và gai đá `QuartzSpike` (bay theo hướng mục tiêu, order 4); scanner + tia sét của `TinhLinhSamSetNho` dùng kênh template sẵn có (`Follow`/`End`). Kiểm chứng bằng player build: client nhận đủ **4 ghost** khi host phát 2 primitive + 1 FlamePatch + 1 QuartzSpike, sau đó `ghosts=0` khi hết vòng đời, không exception.

**Quyết định kiến trúc (ADR):** giữ `RelayService` + `LobbyService` trực tiếp cho MVP thay vì `Unity.Services.Multiplayer` — đã kiểm chứng 4 người qua internet; đổi SDK tốn ~150–200 dòng và không sửa được hạn chế transport.

**Mười một chỗ làm khác plan (đã cân nhắc, cần biết):**
1. **Bỏ guard `Awake` bằng `m_NetworkManaged`** như bảng vòng 3: NGO instantiate prefab nên `Awake` chạy trước khi bất kỳ code mạng nào kịp set cờ, và `TankBrain.Initialize` (`TankBrain.cs:168-169`) ghi đè máu/năng lượng. Thay bằng `SetHealth`/`SetEnergy` + `PlayerNetState` đồng bộ sau spawn.
2. **Xe offline vẫn được tạo trong `Awake`** (`createLocalTankOnAwake = true`) và chỉ bị huỷ khi bấm Host/Join trong `NetLobbyUI`, để chế độ một người vẫn chơi được trong cùng scene.
3. **Hồi chiêu đồng bộ bằng "số giây còn lại tại lúc ghi" + client tự đếm ngược**, không dùng end-time tuyệt đối — tránh phải sửa 5 hàm cooldown trong `TankBrain` (rủi ro cao, lợi ích thấp cho MVP).
4. **Loadout đi qua `NetworkVariable<NetLoadout>`** thay vì chỉ `ConnectionData`: client cần biết cấp/biến thể kỹ năng để dựng đúng chỉ số; `ConnectionData` chỉ dùng để host biết chọn xe gì lúc spawn.
5. **Số sát thương đi bằng named message `IW.CombatFeedback`, không phải `NetworkBehaviour` + `CombatEventRpc`**: NGO cấm gắn `NetworkBehaviour` lên GameObject của `NetworkManager` (và con của nó) — gắn vào sẽ bật modal chặn Editor. Named message không cần `NetworkObject`, không cần spawn, và cho phép gộp 0,5 s theo ô vị trí để DoT không bắn một gói mỗi tick.
6. **Chưa có trường `kind` (heal/damage) trong gói feedback**: `TextDamageManager` hiện chỉ có `Show(amount, position)`, chưa phân biệt màu/nội dung heal. Khi UI thật được làm (nhóm khác) thì thêm `kind` vào gói 16 byte là đủ — hiện chưa cần cho MVP.
7. **Quái đặt sẵn trong scene bị dọn khi vào phòng** (`NetBootstrap.ClearSceneEnemies`): 11 quái của `SampleScene` thừa hưởng `NetworkObject` từ prefab nhưng không do NGO spawn, nên mỗi máy sẽ mô phỏng một bản riêng. Chỉ giữ quái do host spawn qua `NetSpawnService`; chế độ offline vẫn giữ nguyên quái trong scene.
8. **Replay VFX chỉ mở cho slot có visual thuần** (`TankBrain.CanReplayRemoteFx`, mặc định slot 0; Leviathan/RedNova/WallTitan mở thêm slot 1). Audit `fx-mapper` chỉ ra nếu replay nguyên `OnUseSkill` thì client sẽ tự cộng buff, tạo khiên, đăng ký gánh hộ, xoá hồi chiêu và teleport xe — nên đã chặn ở gốc: `AddBonusStats`/`RemoveBonusStats`/`ApplyEffect`/`AddShield`/`AddDamageRedirector`/`ResetSkillCooldown`/`ReduceSkillCooldown`/`SetSkillCooldown` đều `return` khi `IsRemoteFxReplay`. Slot 2–3 (buff/khiên/trạng thái nội bộ như ảo ảnh BlackShadow, ấn GreenFox, laser RedNova) chưa phát lại — visual của chúng sẽ do UI/VFX riêng xử lý sau.
9. **Template visual của quái không phải prefab rời** — `ShadowExchangeClone`, `zoneTemplate`, `projectile`, `scannerTemplate`, `beamTemplate`… đều là GameObject con (local fileID) nằm trong prefab quái/tank, nên không thể gắn `NetworkObject` cho từng cái. Cách làm: named message `IW.Visual` (`NetVisualSpawn`) gửi `NetworkObjectId` của gốc + **tên** template con + vị trí/góc/lifetime; client tìm child theo tên dưới object gốc rồi tự `Instantiate` bản ghost, tắt `Collider2D` (và toàn bộ `MonoBehaviour` nếu `animate=false`), tự huỷ khi hết lifetime. Host không tạo ghost cho chính nó. Giới hạn 120 ghost để chống spam. Kèm kênh thu hồi `IW.VisualEnd` (handle host cấp) để vùng hazard biến mất đúng lúc trên client.
10. **Relay dựng `RelayServerData` bằng constructor 7 tham số**, không dùng `new RelayServerData(allocation, "udp")` như sample: bản `com.unity.transport` trong project chưa có constructor nhận thẳng `Allocation` (build lỗi `CS1729`). `NetRelayService` tự truyền `RelayServer.IpV4`/`Port` + `AllocationIdBytes`/`ConnectionData`/`HostConnectionData`/`Key`, `isSecure: false`.
11. **Bật `NetworkConfig.ConnectionApproval = true`** (trước đó cờ này tắt nên NGO không hề gọi `HandleConnectionApproval`, loadout client gửi qua `ConnectionData` bị bỏ qua và mọi người vào phòng đều nhận xe mặc định). Kèm 2 chi tiết: (a) host tự duyệt chính nó trong `HandleConnectionApproval` (NGO gọi callback cho cả host với payload rỗng — nếu kiểm tra payload như client sẽ log "từ chối" vô nghĩa, dù NGO vẫn tự duyệt host); (b) client **phải được build cùng trạng thái cờ** — nếu host bật mà client tắt, `ConnectionRequestMessage` thiếu `ConfigHash`/payload, NGO log `Incomplete connection request message given config` và ngắt kết nối. Test lại sau khi bật: host log `[NetBootstrap] Duyệt client 1: tank=0 level=1 …`, `clients=2 tanks=2`, không còn lỗi mismatch. Nhân dịp này thêm guard null cho `CustomMessagingManager` trong `NetVisualSpawn.Update` và `NetSkillFx.Broadcast` (player build từng ngập 870k `NullReferenceException` mỗi frame, log phình 156 MB).

**Ghi chú vận hành:** `.git` đã được cấp quyền ghi (set integrity level `(OI)(CI)L` cho toàn bộ `.git`) nên commit được ngay trong shell này; nhánh `feature/online-coop-4p` giữ commit **local**, **không push** theo yêu cầu. Build kiểm tra: `dotnet build Assembly-CSharp.csproj` với `TEMP` trỏ `.buildtemp` → 0 error. Chạy thử 2 client trên một máy:

```
Builds/Windows/IronWasteland.exe -batchmode -nographics -iw-host -logFile "<repo>\.netlogs\host.log"
Builds/Windows/IronWasteland.exe -batchmode -nographics -iw-join 127.0.0.1 -iw-selftest-cast -logFile "<repo>\.netlogs\client.log"
```

Dùng thư mục `.netlogs/` (không dùng `Logs/` vì thư mục cũ thiếu quyền kế thừa nên player không ghi được log). `-iw-selftest` cho host tự đánh vào xe mình để kiểm tra đồng bộ máu; `-iw-selftest-cast` cho client tự gọi `RequestCast` để kiểm tra RPC.

---

## 0. Nhật ký review — những gì bản 3 sửa so với bản 2

| # | Lỗi chặn ở bản 2 | Sửa ở bản 3 |
|---|---|---|
| 1 | Coi `StageManager` đang chạy | **Nó chưa được gắn vào scene nào**, guid `6708fec496af4cd44981f0a9b5921eef` = 0 tham chiếu; **chưa có asset `StageDefinition`**; `StageManager.cs:127` sẽ `LogError`. Thêm GĐ 0.5 bắt buộc |
| 2 | Thua khi "cả nhóm chết" nhưng code chỉ xét 1 người | `StageManager.cs:264-268` chỉ lấy 1 `m_Player`; phải đổi sang danh sách + tạo roster |
| 3 | Nói `timeLimit` là điều kiện kết thúc | `StageDefinition.cs:69` ghi rõ chỉ để hiển thị; muốn dùng phải định nghĩa mới |
| 4 | Client `ReportHit` từng va chạm | Bỏ hẳn: **host mô phỏng toàn bộ combat**, tự quét hình học. Xoá `ReportHitServerRpc` |
| 5 | Host tính damage thiếu dữ liệu (buff runtime, `attackScale` nằm trong skill) | Vì host chạy chính skill đó nên dùng đúng instance/field của nó — không cần bảng damage |
| 6 | DoT/decoy bị coi là VFX | `BurnEffect` gây damage thật; `ShadowExchangeClone` có HP + hấp thụ đòn → phải là `NetworkObject` server-spawn |
| 7 | Proxy tank vẫn ghi `linearVelocity` đè NetworkTransform | Thêm luật proxy: tắt `FixedUpdate` movement + kinematic hoá Rigidbody2D |
| 8 | Spawn tank ngoài NGO (`TankDefinition.CreateTank` → `Instantiate`) | Đảo luồng: NGO spawn prefab → `Initialize(NetLoadout)`; đăng ký NetworkPrefabs |
| 9 | Cooldown "end time" phá 5 hàm hiện có | Liệt kê đích danh các hàm phải sửa + giữ `skillCooldowns` cho hiển thị |

Ngoài ra sửa: `TakeDamage` thật có **35 call site gameplay** (16 tank→enemy, 18 enemy→tank, 1 tank→tank qua redirect) chứ không phải "vài skill"; `TankBrain.cs:640` (không phải `:641`); `StageManager.Elapsed` là property `:58`, tăng ở `:99`; `TankBrain.Awake` tự bơm đầy máu (`:901`); AI của `ChasingEnemyBrain` chỉ đuổi **1** player; prefab tank nhúng nhiều `Rigidbody2D` con.

**Vòng 3 (audit lại toàn bộ code) sửa thêm:**

| # | Lỗi chặn | Sửa ở bản 4 |
|---|---|---|
| 1 | Host gọi `UseSkill` nhưng **không set aim** → `m_HasLookTarget` mặc định `false`, skill tự `return` im lặng (`TankBrain_WallTitan.cs:396`, `TankBrain_RedNova.cs:430`…) | Handler `RequestCastServerRpc` phải `SetLookTarget(aimWorld)` **trước** `UseSkill`; `UseSkill` trả kết quả để client biết bị từ chối |
| 2 | Một `IsProxy` vừa "tắt skill" vừa là đối tượng host cast — tự mâu thuẫn; tank của chính client vẫn gọi `UseSkill` local (`GameController.cs:146`) → cast đôi | Tách `IsLocallyControlled` / `IsServerSimulated`; `UseSkill` chỉ chạy khi `IsServerSimulated` |
| 3 | `body.simulated = false` cho tank proxy **xoá collider khỏi host** → quái melee/AoE không bao giờ trúng tank client | Bắt buộc `bodyType = Kinematic`, giữ collider; không dùng `simulated = false` |
| 4 | Guard `Awake` bằng `IsSpawned` vô tác dụng (`IsSpawned` luôn `false` trong `Awake`) | Dùng cờ thường `m_NetworkManaged` do `NetSpawnService` set trước `Initialize` |
| 5 | Cổng `if (!IsSpawned \|\| !IsServer) return;` giết chế độ offline (Editor/test GĐ 0–3) | `if (NetworkManager.Singleton != null && !IsServer) return;` — offline là fallback hợp lệ |

---

## TL;DR

1. **Host mô phỏng 100% combat**: skill, đạn, DoT, ảo ảnh, damage, năng lượng, cooldown, CC. Client chỉ gửi input (di chuyển, ngắm, cast) và phát lại VFX nhận từ RPC.
2. **Dùng primitive NGO**: `NetworkObject`, `NetworkVariable`, `NetworkList`, `NetworkTransform`, RPC. Không tự dựng tầng vận chuyển.
3. **Vị trí tank của mình do client sở hữu** (owner `NetworkTransform`); host validate bằng raycast + token bucket.
4. **Không sync thứ suy ra được**: cooldown gửi mốc kết thúc, thời gian màn tính từ `matchStart`, damage text/VFX sinh từ RPC.
5. **Hai cờ thay vì một `IsProxy`**: `IsLocallyControlled` (input/camera) và `IsServerSimulated` (skill/energy/damage). Proxy giữ Rigidbody2D **Kinematic**, không dùng `simulated = false` — collider phải còn trong physics world để host quét trúng.
6. **Điều kiện tiên quyết GĐ 0.5**: gắn `StageManager` vào scene + tạo `StageDefinition` + roster nhiều người — hiện tại màn chơi chưa chạy.
7. **Reconnect nằm ngoài MVP**: MVP rút mạng = despawn + spawn lại; snapshot đầy đủ để backlog (GĐ 7*).
8. **Lobby là overlay trong scene chơi**, không tạo scene riêng — tránh `NetworkSceneManager`, bỏ `ReportClientLoaded`.
9. **Quy mô MVP**: 30 quái/wave (stage design chưa quyết, đây là mặc định), **không** scale HP quái theo số người.

> **Không tối ưu vi mô:** không quantize tay, không gộp gói tay, không đếm byte từng message. Nhưng **có ngưỡng thực tế** ở §5, và ngưỡng đó được đặt lại cho đúng với 30 quái/wave.

---

## Mục lục

- [0. Nhật ký review](#0-nhật-ký-review--những-gì-bản-3-sửa-so-với-bản-2)
- [1. Hiện trạng](#1-hiện-trạng)
- [2. Kiến trúc đồng bộ (NGO-native)](#2-kiến-trúc-đồng-bộ-ngo-native)
- [3. Thiết kế object / state / RPC](#3-thiết-kế-object--state--rpc)
- [4. Các bước triển khai](#4-các-bước-triển-khai)
- [5. Đo đạc và kiểm thử](#5-đo-đạc-và-kiểm-thử)
- [6. Rủi ro và câu hỏi](#6-rủi-ro-và-câu-hỏi)
- [Phụ lục A — File/asset sẽ chạm](#phụ-lục-a--fileasset-sẽ-chạm)
- [Phụ lục B — Thuật ngữ](#phụ-lục-b--thuật-ngữ)

---

## 1. Hiện trạng

### 1.1 Cấu trúc và luồng dữ liệu

| Tầng | Thành phần | Ghi chú cho online |
|---|---|---|
| Data | `TankDefinition` (6 asset trong `Assets/Prefabs/Tanks/*/*.asset`), `EnemyStats`, `StageDefinition`, `EquipmentDatabase` | **Chưa có asset `StageDefinition`**; `TankDefinition` chỉ chứa prefab + 2 mốc stats |
| Logic | `TankBrain` + **7 class con** (6 gameplay + `TankBrain_Test`), `EnemyBrain` + **19 class quái** (21 kể cả 2 test), `StageManager`, skill MonoBehaviour | Nơi phải phân quyền host/proxy |
| Visual | `TankView`, `EnemyModel`, `TextDamageManager`, VFX | Local, không sync |

**Điểm hiện trạng quan trọng (đã kiểm chứng)**

- **`StageManager` chưa chạy**: guid `6708fec496af4cd44981f0a9b5921eef` không xuất hiện trong scene/prefab/asset nào. `StageManager.cs:127` sẽ `Debug.LogError("Chua gan StageDefinition")`. `SampleScene` chỉ có `GameController` và `[PathfindingGrid2D]`.
- **`StageManager` chỉ theo 1 người**: `m_Player` (`StageManager.cs:82`), `TrackPlayer` (`:264`) lấy `gameController.PlayerTank` hoặc `FindAnyObjectByType<TankBrain>()`; `EvaluateOutcome` (`:271`) thua ngay khi người đó chết.
- **`timeLimitSeconds` chỉ để hiển thị**: `StageDefinition.cs:69` ghi rõ "dung de hien thi, khong phai dieu kien thua"; `EvaluateOutcome` không dùng.
- **Không có roster**: 6 `TankDefinition` rời; `GameController` chỉ giữ **một** `tankDefinition` (`GameController.cs:16`).
- **`TankBrain.Awake` tự bơm đầy máu/năng lượng** nếu `<= 0` (`TankBrain.cs:901-902`) → spawn mạng sẽ "hồi sinh" tank đã chết.
- **`TankBrain` chỉ có setter hẹp**: `SetMoveInput` (`:579`), `SetLookTarget` (`:584`), `SetSkillCooldown` (`:1012`). Không có `SetHealth/SetEnergy/SetShield`.
- **AI đuổi mục tiêu**: `NormalEnemy_BaseChaser` quét layer `Tank` (nhiều mục tiêu), nhưng `ChasingEnemyBrain.FindPlayerTank` (`:163-167`) chỉ trả **một** tank → 7 loại quái kế thừa (`BlackMoonBrain`, `CrystalShooterBrain`, `DeadlandPhantomBrain`, `GraveyardWispBrain`, `MeteorParasiteBrain`, `QuartzBruteBrain`, `SeauBearusBrain`) chỉ đuổi player #1.
- **Damage có 35 call site gameplay**: 16 tank→enemy, 18 enemy→tank, 1 tank→tank qua chuyển hướng (chi tiết Phụ lục A.2), cộng 4 chỗ nội bộ/debug.
- **Damage text có 42 call site** `TextDamageManager.Instance?.Show(...)` (39 gameplay + 3 debug).
- **Quái tự `Destroy(gameObject)`** khi chết (14 file quái/hazard, 16 chỗ; ví dụ `BlackMoonBrain.cs:50`, `CrystalShooterBrain.cs:177`) → online phải đổi sang NGO despawn trên host.
- **Tank prefab nhúng nhiều `Rigidbody2D`** (BlackShadow có 4: `BlackShadow.prefab:40/:477/:1277/:1893`) và template skill inactive; `NetworkTransform` trên root không phủ các body con.

**Luồng single-player hiện tại**

```text
GameController.Update()                      [Assets/Scripts/Tanks/GameController.cs:74]
   ├─ SetMoveInput                              → TankBrain.m_MoveInput
   ├─ SetLookTarget(worldPos)                   → TankBrain.m_LookTarget (mỗi frame)
   └─ UseSkill(index)                           → TankBrain.UseSkill [TankBrain.cs:607]
                                                    ├─ CanUseSkill / skillCooldowns / currentEnergy
                                                    ├─ GainEnergy(GetSkillEnergyGain) hoặc SpendEnergy
                                                    ├─ skillCooldowns[index] = GetCooldown(index)
                                                    └─ OnUseSkill → skill (pool) → tự gọi TakeDamage

TankBrain.Update()                              [TankBrain.cs:905]  tick cooldown/CC/effect
TankBrain.FixedUpdate()                         [TankBrain.cs:915]
   └─ m_Body.linearVelocity = m_MoveInput * speed   [TankBrain.cs:930]

EnemyBrain.Update() / EnemyMovementAgent.FixedUpdate()    AI + pathfinding + linearVelocity
StageManager.Update()                            [StageManager.cs:95]  (chưa được gắn vào scene)
   └─ Elapsed (:58/:99) · TrackPlayer · EvaluateOutcome · SpawnDueWaves → Instantiate [StageManager.cs:227]
```

### 1.2 State nào cần đồng bộ

| State | Nơi giữ | Cơ chế NGO | Ghi chú |
|---|---|---|---|
| Loadout | `TankDefinition` + `TankBrainContext` (`Level`, `EquipmentId`, `OptionSkill1/2`) | `ConnectionData` | Server tính stats; client không tính |
| Vị trí tank | `Rigidbody2D` (root) | `NetworkTransform` owner | Rotation root không đổi — thân/nòng xoay bằng pivot con, xem §3.1 |
| Máu / năng lượng | `currentHealth` / `currentEnergy` | `NetworkVariable<float>` server-write | Cần thêm setter |
| Final stats | `m_FinalStats` | `NetworkVariable<TankStatsSnapshot>` server-write | MVP chỉ cần `maxHealth/maxEnergy/moveSpeed` cho HUD + movement; Attack/Defense/DamageRate host-only |
| 4 cooldown | `skillCooldowns[4]` (đếm ngược) | `NetworkVariable<CooldownState>` (mốc kết thúc) | Phải sửa 5 hàm + caller BlackShadow |
| Khiên nhiều lớp | `activeShields` cục bộ trong `ProcessShieldAbsorption` (`TankBrain.cs:503`) | `NetworkVariable<float> ShieldTotal` + `byte ShieldLayerCount` server-write | Client chỉ vẽ tổng; thứ tự lớp/hấp thụ chỉ host cần |
| CC | `m_ActiveCC` | `NetworkVariable<CCState>` server-write | Slow ảnh hưởng movement của client |
| Effect/Burn | `ActiveEffects` (host) | `ApplyStatusRpc` add/remove để hiện icon | Không replicate danh sách; burn tick chỉ host, text gộp 0,5 s |
| Trạng thái skill đang chạy (ShadowStep pha 2, Form, Tide) | `activeSpecializedSkill`, `optionalSkill2_2/2_3` | **Host-only** + cờ `NetworkVariable<byte>` cho UI | Client không chạy skill |
| Ảo ảnh `ShadowExchangeClone` (có HP, hấp thụ đòn) | `currentHealth`, `maxHits` | **`NetworkObject` server** + HP var | Không phải VFX |
| Quái: vị trí, HP, CC | `EnemyBrain` | `NetworkTransform` server + `NetworkVariable<float>` HP + `EnemyStatsSnapshot{maxHealth}` | Client không chạy AI nên không cần attack/defense/moveSpeed |
| Spawn/despawn quái | `StageManager` | NGO spawn/despawn host | Thay `Destroy` |
| Stage (wave/kill/thời gian) | `StageManager` | RPC + `matchStartServerTime` | Chỉ host chạy |
| Kết quả trận | events `StageCleared/StageFailed` | `MatchEndRpc` | |
| Input, aim, skill đang chọn | `GameController` | Local; aim gửi kèm cast | |
| Damage text / VFX | `TextDamageManager`, skill visual | RPC | 42 call site phải chuyển |
| Acccessory (`EquipmentBrain`) | `m_EquippedAccessory` + `Instantiate` runtime (`TankBrain.cs:255`) | Prefab con cố định theo `EquipmentId` | Không `Instantiate` runtime |
| Pool đạn/ảo ảnh | skill MonoBehaviour | Local; host prewarm | Không phải NetworkObject |

### 1.3 Những chỗ sẽ gửi thừa nếu làm sai

| Vị trí | Nếu sai | Đúng |
|---|---|---|
| `TankBrain.cs:905-913` | Ghi `cooldownRemaining` mỗi frame | Ghi 1 lần lúc cast: `endTime` |
| `StageManager.cs:99` | Sync `Elapsed` mỗi frame | Chỉ gửi `matchStartServerTime` |
| `TankBrain.cs:930` | Bật NetworkTransform cho mọi object, tick 60 | Tick 20, threshold, kinematic proxy |
| `TextDamageManager` (40 chỗ) | Đồng bộ text bay | Sinh local từ `CombatEventRpc` |
| `GameController.cs:60-62` | Mỗi client tự `Instantiate` tank | Host `SpawnAsPlayerObject` |
| `EnemyBrain.cs:534` | Client chạy AI | `IsProxy` |
| `TankBrain.ProcessDamageRedirection` | In số gánh hộ (raw share) trên host rồi client in lại | Đo `CurrentHealth` trước/sau khi gánh → in đúng lượng máu đã mất; chỉ host phát qua `CombatFeedback` |
| `TankBrain.cs:640` | Debug `Show(36)` làm text sai | Xoá |

### 1.4 Rào cản single-player trong code

| Vấn đề | Vị trí | Hướng xử lý |
|---|---|---|
| Stage chưa gắn + thiếu asset | scene `SampleScene`, `StageManager.cs:127` | GĐ 0.5: gắn + tạo `StageDefinition` asset |
| Chỉ theo 1 player | `StageManager.cs:264-271` | Roster `List<TankBrain>`, thua khi tất cả chết |
| Tự tạo tank | `GameController.cs:60-71` | Online: chờ host spawn |
| Input poll trực tiếp | `GameController.cs:74-147` | `IPlayerInput` + `NetPlayerController` |
| AI chạy vô điều kiện | `EnemyBrain.cs:534`, `EnemyMovementAgent.cs:170` | `IsProxy` + tắt component trong prefab |
| AI chỉ đuổi 1 người | `ChasingEnemyBrain.cs:163-167` | Chọn tank gần nhất trong roster |
| Damage local | 35 call site (Phụ lục A.2) | Chỉ host gọi |
| `Awake` bơm đầy máu | `TankBrain.cs:901` | Guard khi spawn mạng; `Initialize` sau spawn |
| Không có setter state | `TankBrain.cs:579/584/1012` | Thêm `SetHealth/SetEnergy/SetShieldSnapshot/SetCooldownState` |
| Spawn ngoài NGO | `TankDefinition.cs:52-60` | Spawn qua NGO rồi `Initialize` |

---

## 2. Kiến trúc đồng bộ (NGO-native)

### 2.1 Topology

```text
        ┌──────────────────────── Host (client 0 = server) ──────────────────────────┐
        │  NetworkManager · Relay · Lobby                                            │
        │  SERVER QUYẾT: spawn · AI · skill · damage · energy · cooldown · CC · stage │
        │  CLIENT QUYẾT: vị trí tank của chính nó (host validate)                     │
        └──────────────▲──────────────────────────────▲──────────────────────────────┘
                       │  UTP/Relay                                                  │
        ┌──────────────┴─────────┐   ┌────────────────┴────────┐   ┌────────────────┐
        │ Client 1               │   │ Client 2                │   │ Client 3       │
        │ tank mình: owner       │   │ ...                     │   │ ...            │
        │ còn lại: proxy         │   │                         │   │                │
        └────────────────────────┘   └─────────────────────────┘   └────────────────┘
```

### 2.2 Nhu cầu → primitive NGO

| Nhu cầu | Primitive | Ghi chú |
|---|---|---|
| Định danh, spawn/despawn | `NetworkObject` | Không tự đặt ID |
| Vị trí/góc xoay | `NetworkTransform` | Tank: Owner; quái/ảo ảnh: Server; bật interpolate |
| State rời rạc khi đổi | `NetworkVariable<T>` / `NetworkList<T>` | Chỉ ghi khi dirty |
| Sự kiện | RPC `[Rpc(SendTo...)]` | Không tự định nghĩa message |
| Loadout | `ConnectionData` + Connection Approval | |
| Gộp gói | NGO theo `TickRate` | |
| Interest management | observer/`NetworkShow`/`NetworkHide` | MVP chưa cần |
| Đo băng thông | `NetworkMetrics`/Profiler | |

### 2.3 Ba nhóm state

1. **Derived (không truyền):** cooldown còn lại, effect còn lại, animation, VFX, damage text, camera, input, thời gian màn.
2. **Replicated:** HP, energy, stats tối thiểu (`maxHealth/maxEnergy/moveSpeed`), cooldown end, **tổng khiên + số lớp**, CC, HP quái.
3. **Event (RPC):** cast FX, damage text, effect/CC add-remove (nếu không dùng list), wave, kill, kết thúc, snapshot.

### 2.4 Phân quyền

| Việc | Ai quyết | Ghi chú |
|---|---|---|
| Di chuyển tank của mình | Client sở hữu | Owner `NetworkTransform`; host validate raycast + token bucket |
| Cast | Client gửi `RequestCastServerRpc(skillIndex, aimWorld)` | **Host `SetLookTarget(aimWorld)` rồi `UseSkill` trên tank đó** — không có aim thì skill tự `return` (`m_HasLookTarget` mặc định `false`) |
| Mọi damage (skill, đạn, DoT, ảo ảnh, redirect) | Host | Client chỉ hiển thị |
| HP/energy/cooldown/shield/stats | Host | `NetworkVariable`/`NetworkList` |
| CC/effect | Host | Replicate state; client áp slow vào movement của mình |
| AI, spawn, pathfinding, stage | Host | Client `IsProxy` |
| VFX, damage text, nòng xoay | Client | Replay từ RPC; aim gửi kèm cast + NetworkVariable |

**Luật vàng:** mọi chỗ gọi `TakeDamage`/`Heal`/`GainEnergy`/`SpendEnergy`/`ApplyCrowdControl` phải nằm sau cổng `if (NetworkManager.Singleton != null && !IsServer) return;`. Offline (không có `NetworkManager`) vẫn chạy bình thường — đây là fallback hợp lệ cho Editor và test GĐ 0–3. Client tuyệt đối không gọi các hàm này cho gameplay.

### 2.5 Tick, vật lý, proxy

| Thành phần | Thiết lập |
|---|---|
| `NetworkConfig.TickRate` | 20 |
| Tank của mình | `NetworkTransform` authority Owner; Rigidbody2D động; không interpolate; `IsLocallyControlled = true` |
| Tank người khác (proxy) | `IsLocallyControlled = false`; **tắt** nhánh movement trong `TankBrain.FixedUpdate`; `bodyType = Kinematic` (**giữ collider**) |
| Quái trên client (proxy) | `IsServerSimulated = false`; tắt `EnemyBrain.Update`, `ChasingEnemyBrain.FixedUpdate` (vòng `MovePosition`/`linearVelocity` riêng), `NormalEnemy_BaseChaser/BasePatrol/BaseStationary`, `EnemyMovementAgent`; `bodyType = Kinematic`; `NetworkTransform` server + interpolate |
| Collider trên proxy | **Không** dùng `body.simulated = false` — nó gỡ collider khỏi physics world, host sẽ không quét trúng tank client (`ChasingEnemyBrain.cs:142` `OverlapCircleAll`, `TankBrain_WallTitan.cs:583`, skill Leviathan). Chỉ đổi `bodyType` |
| Quái trên host | Động như hiện tại |
| Đạn/VFX | Không là NetworkObject; spawn local từ RPC; pool prewarm |
| Ảo ảnh có HP | Là `NetworkObject` server (ngoại lệ của luật "VFX local") |

### 2.6 Prediction và nội suy

- **Tank của mình:** không prediction — client sở hữu, phản hồi 0 ms.
- **Tank khác + quái:** `NetworkTransform.Interpolate`.
- **Skill:** client **không** tự mô phỏng. Bấm cast → gửi RPC → chờ host xác nhận → VFX phát khi nhận `PlaySkillFxRpc`. Trễ ~RTT/2 nhưng đúng.
- **Damage text:** chỉ khi nhận `CombatEventRpc`.
- **Không lag compensation** (PvE).

### 2.7 Interest management

MVP không dùng. Arena nhỏ, 4 người. Khi cần: bật observer / `NetworkShow`–`NetworkHide`; không tự viết `relevanceMask`.

### 2.8 Reconnect

NGO không có sẵn, và **client reconnect sẽ có `clientId` mới**. Cách làm:

1. Mỗi người có `playerId` ổn định (sinh ở lobby, lưu trong `ConnectionData`).
2. Host giữ player object (`DontDestroyWithOwner = true` — **cần xác minh trên bản NGO cụ thể**), đánh dấu `IsDisconnected`, grace 60 s.
3. Client reconnect → host map `playerId` → object cũ → `ChangeOwnership` (cần xác minh) hoặc despawn + spawn lại.
4. *(Backlog — không làm ở MVP)* Gửi `SyncSnapshotRpc(SendTo.SpecificClients)` gồm: HP, energy, stats tối thiểu, **4 cooldown end**, **tổng khiên + CC**, trạng thái skill đang chạy, vị trí, stage (elapsed, kills, wave). MVP: rút mạng → despawn, vào lại → spawn lại từ đầu.
5. Hết grace: despawn, trận tiếp tục với 3 người; chống spam bằng cooldown 10 s/player.

### 2.9 Khi nào được tự serialize

- **Được:** `INetworkSerializable` cho payload `NetworkVariable`/`NetworkList`/RPC (định nghĩa ở §3.1).
- **Không:** tự ID, tự spawn/despawn, tự reliability, tự observer, tự gộp gói.
- Nếu băng thông vượt ngưỡng: giảm tần suất `NetworkTransform` của quái (nếu API cho phép — cần xác minh), giảm `TickRate`, hoặc giảm số quái. Không dựng protocol riêng.

---

## 3. Thiết kế object / state / RPC

### 3.1 Object model và payload

**Payload `INetworkSerializable`**

| Struct | Field | Ghi chú |
|---|---|---|
| `NetLoadout` | `byte tankId`, `byte level`, `short equipmentId`, `byte optionSkill1`, `byte optionSkill2` | Gửi qua `ConnectionData` |
| `CooldownState` | `double end0, end1, end2, end3` | Mốc theo `NetworkManager.ServerTime`; `0` = sẵn sàng |
| `CastResult` | `byte code` (0 ok, 1 thiếu năng lượng, 2 hồi chiêu, 3 bị CC, 4 sai mục tiêu) | Trả về cho client qua RPC |
| `TankStatsSnapshot` | `float maxHealth, maxEnergy, moveSpeed` | Client không tính damage → chỉ cần cho HUD + movement |
| `EnemyStatsSnapshot` | `float maxHealth` | Client không chạy AI nên không cần attack/defense/moveSpeed |
| `CCState` | `byte flags`, `float slowStrength`, `double endTime` | Client áp slow vào movement của mình |

> Khiên **không dùng struct list**: `NetworkVariable<float> ShieldTotal` + `byte ShieldLayerCount` (client chỉ vẽ tổng). Effect **không replicate**: `ApplyStatusRpc` add/remove icon; `EffectState` bị bỏ khỏi bản 4.

**PlayerTank** (`NetworkObject`, owner = client)

| Thành phần | Vai trò |
|---|---|
| `NetworkTransform` | Authority Owner |
| `PlayerNetState : NetworkBehaviour` | State + RPC |
| `TankBrain` (+ subclass) | Host: logic đầy đủ. Client: proxy trừ tank mình (input + vị trí) |
| `NetPlayerController` | Chỉ bật cho local player |

**NetworkVariable/NetworkList của PlayerTank**

| Tên | Kiểu | Quyền ghi | Khi ghi |
|---|---|---|---|
| `Health` | `float` | Server | damage/heal |
| `Energy` | `float` | Server | cast/hồi |
| `Cooldowns` | `CooldownState` | Server | cast/reset |
| `Stats` | `TankStatsSnapshot` | Server | buff/level đổi |
| `ShieldTotal` | `float` | Server | thêm/hấp thụ/vỡ (client vẽ thanh khiên) |
| `ShieldLayerCount` | `byte` | Server | số lớp còn lại (HUD tuỳ chọn) |
| `CC` | `CCState` | Server | CC đổi. `Flags` **bị bỏ**: "chết" suy từ `Health <= 0`, silence/root nằm trong `CC.flags` |
| `AimAngle` | `float` | Owner | đổi > 2°, tối đa 10 Hz — **cần**, vì nòng là `firepowerPivot` con (`TankView.cs:200-219`) xoay từ `m_LookTarget`, không nằm trong `NetworkTransform` root |
| `SkillPhase` | `byte` | Server | tuỳ chọn MVP — chỉ cần nếu HUD hiện ShadowStep pha 2 |

> **Hướng thân xe proxy:** root không bao giờ xoay (`bodyPivot` mới xoay, `TankView.cs:143/:154`). Proxy suy hướng thân từ delta vị trí `NetworkTransform` (dừng thì giữ hướng cũ) trong `NetProxyVisual` — không thêm biến mạng. Nòng dùng `AimAngle`.

**Enemy** (`NetworkObject`, server)

| Thành phần | Vai trò |
|---|---|
| `NetworkTransform` | Authority Server |
| `EnemyNetState : NetworkBehaviour` | `NetworkVariable<float> Health`, `EnemyStatsSnapshot Stats`, `CCState CC` |
| `EnemyBrain` | Host chạy AI; client `IsProxy`, tắt Update |

**Decoy** (`ShadowExchangeClone`) — `NetworkObject` server, có `Health` + `NetworkTransform`; client chỉ hiển thị.

**Không phải NetworkObject:** đạn, VFX, damage text, vùng hiệu ứng thuần hình ảnh.

### 3.2 RPC

**Client → Server**

| RPC | Tham số | Khi gọi | Server làm gì |
|---|---|---|---|
| `RequestCastServerRpc` | `byte skillIndex`, `Vector2 aimWorld` | Bấm cast | Validate `CanUseSkill` + cooldown + energy → `SetLookTarget(aimWorld)` → `UseSkill` → trả `CastResult` + broadcast FX |
| `RequestMoveDeltaServerRpc` | (không cần — dùng NetworkTransform) | — | Validate vị trí trong `NetworkTransform`/validator |
| `SetReadyServerRpc` | `bool` | Lobby overlay | Cập nhật roster |

> Bỏ `ReportClientLoadedServerRpc`: lobby là overlay trong cùng scene, host bắt đầu khi `ConnectedClientsIds.Count` đủ roster hoặc khi host bấm Start. Không dùng `NetworkSceneManager` ở MVP.

**Server → Client**

| RPC | Tham số | Khi gọi |
|---|---|---|
| `PlaySkillFxRpc` | `int casterNetId`, `byte skillIndex`, `Vector2 origin`, `Vector2 dir`, `byte variant` | Sau cast hợp lệ |
| `CombatEventRpc` | `int targetNetId`, `float raw`, `float final`, `float absorbed`, `byte kind` (0 damage, 1 heal, 2 absorb), `Vector2 hitPos` | Sau damage/heal; DoT gộp 0,5 s/target |
| `ApplyStatusRpc` | `int netId`, `int effectIdHash`, `byte stacks`, `double endServerTime` | Effect/CC add-remove (nếu không dùng list) |
| `StageEventRpc` | `byte state`, `int wave`, `int kills`, `double remainSeconds` | Đổi wave/kill |
| `MatchEndRpc` | `byte result`, `int kills`, `float duration` | Kết thúc |
| `SyncSnapshotRpc` | `PlayerSnapshot` + `StageSnapshot` | Chỉ client reconnect |

### 3.3 Sơ đồ luồng

**Vào phòng**

```text
Client                        Lobby/Relay                 Host
  create/join lobby ─────────▶│ join code ───────────────▶│
  connect + NetLoadout ──────────────────────────────────▶│ approval: validate + lưu
  │                                                       │ SpawnAsPlayerObject(clientId)
  │◀──────────── NetworkObject + NetworkVariables ────────│
  (không RPC riêng) ─────────────────────────────────────▶│ đủ roster/ready → StageEventRpc(start)
```

**Một lần cast (host mô phỏng)**

```text
Client A                      Host                              Client B/C
  bấm skill
  RequestCastServerRpc(idx, aim) ─▶ validate energy/cooldown/CC
                                    ├─ tankA.SetLookTarget(aim) → tankA.UseSkill(idx)
                                    │  (thiếu aim = m_HasLookTarget false = skill return im lặng)
                                    ├─ đạn/AoE/DoT/ảo ảnh xử lý trên host
                                    ├─ trúng quái → TakeDamage → HP NetworkVariable
                                    ├─ CombatEventRpc ──────────────▶ damage text
                                    └─ PlaySkillFxRpc ──────────────▶ VFX
  ◀────────────── Cooldowns + Energy NetworkVariable ──────
```

**Chuỗi damage phức tạp (đều trên host)**

```text
Water Pulse xuyên nhiều quái  → host quét collider, damage từng con
Water Bubble nổ diện rộng     → host quét OverlapCircleAll
BurnEffect DoT                → host tick, client chỉ hiện effect
Guardian Current redirect     → host chuyển RawDamage sang protector trên host
Shadow Guard / Form intercept → host chặn, phát CombatEvent(absorbed)
ShadowExchangeClone           → NetworkObject, host nhận đòn thay
```

**Kết thúc**

```text
Host: kills >= target | tất cả player chết | (nếu định nghĩa mới) hết giờ
  → StageEventRpc + MatchEndRpc → client hiện kết quả → shutdown về lobby
```

### 3.4 Quy tắc bắt buộc

1. Chỉ ghi `NetworkVariable` khi giá trị đổi; không ghi mỗi `Update`.
2. Không lưu số đếm ngược trong state mạng — lưu mốc `ServerTime`.
3. Cổng host ở **mọi** chỗ damage/heal/energy/CC (35 call site — Phụ lục A.2).
4. `TankBrain.Awake` guard bằng cờ `m_NetworkManaged` (do `NetSpawnService` set **trước** `Initialize`), không dùng `IsSpawned` — `IsSpawned` luôn `false` trong `Awake`.
5. Quái chết: host `NetworkObject.Despawn`; client **không** `Destroy` (14 file).
6. Damage text chỉ từ `CombatEventRpc` (42 chỗ `Show`); xoá debug `Show(36)` (`TankBrain.cs:640`).
7. Proxy: hai cờ `IsLocallyControlled`/`IsServerSimulated`; tắt movement/AI/skill theo cờ; `bodyType = Kinematic` — **không** `simulated = false` (mất collider, host không quét trúng).
8. `ShadowExchangeClone` là NetworkObject; mọi thứ còn lại (đạn/VFX) không.
9. `UseSkill` chỉ chạy khi `IsServerSimulated`; tank của chính client chỉ gửi `RequestCastServerRpc`, không gọi `UseSkill` local (`GameController.cs:146` phải đổi).
10. DoT (Burn/Poison/GreenFox burn): chỉ host tick; text gộp mỗi 0,5 s cho mỗi target, không 1 RPC mỗi tick.
11. Offline: `NetworkManager.Singleton == null` → chạy như single-player, không chặn damage/energy/CC.

---

## 4. Các bước triển khai

> Độ khó: S (1–2 ngày), M (3–5), L (1–2 tuần), XL (>2 tuần) cho 1 dev.

| GĐ | Nội dung | Độ khó | Rủi ro | Test độc lập |
|---|---|---|---|---|
| 0 | Refactor nền: hai cờ proxy, roster, Awake guard, gating debug, cài NGO (chưa dùng API) | M | Thấp | 1 tank + stage chạy được, proxy không tự chạy |
| 0.5 | **Gắn stage**: tạo `StageDefinition` asset + đặt `StageManager` vào scene | S | Thấp | Chạy 1 wave quái, thắng/thua đúng |
| 1 | Bootstrap NGO + Relay + **lobby overlay** + NetworkPrefabs | L | Trung bình | 2 Editor thấy nhau, mỗi người 1 tank |
| 2 | Di chuyển owner-authority + kinematic proxy + validate | L | Trung bình | 4 client chạy vòng, lệch < 0.3 unit |
| 3 | `PlayerNetState` + setter state + HUD | M | Trung bình | 4 client khớp HP/energy/cooldown/shield |
| 4 | **Chuyển combat về host** (35 call site) | XL | Cao | Client không gây damage; host gây đúng 1 lần |
| 5 | Quái + stage: NetworkObject, stats, despawn, roster | L | Cao | 4 client thấy cùng quái/HP/vị trí/kill |
| 6 | VFX/aim + decoy NetworkObject | M | Trung bình | 4 client cùng bắn, VFX khớp |
| 7* | **Backlog** — Reconnect + grace + snapshot | M | Trung bình | Ngoài MVP |
| 8 | Đo + chỉnh tick/threshold | S | Thấp | Số liệu đạt ngưỡng §5 |

### GĐ 0 — Refactor nền (chưa có mạng)

- Thêm hai cờ `IsLocallyControlled`, `IsServerSimulated` (bool thường, không phụ thuộc NGO) vào `TankBrain`/`EnemyBrain`; gate `FixedUpdate` movement (`TankBrain.cs:915-936`) và `Update` cooldown/effect theo cờ.
- Cài trước `com.unity.netcode.gameobjects` + services (chưa dùng API) để GĐ 1 không phải chờ.
- `TankBrain.Awake` (`:901-902`): chỉ bơm máu khi `!m_NetworkManaged`; thêm `SetHealth/SetEnergy/SetShieldSnapshot/SetCooldownState/SetStats` (hoặc `PlayerNetState` ghi trực tiếp qua API nội bộ).
- `ChasingEnemyBrain.FindPlayerTank` → chọn tank gần nhất trong roster.
- Gate `DebugDame.cs`, `DamageTestTrigger.cs`, `Tests/SkillArena.cs` bằng `#if UNITY_EDITOR` hoặc cờ.
- Tách `IPlayerInput` khỏi `GameController`.
- Tạo roster asset/list (mới) thay vì 6 `TankDefinition` rời.

**Test:** 1 tank chơi offline; proxy (giả lập) không tự chạy movement/AI.

### GĐ 0.5 — Gắn stage

- Tạo `Assets/Prefabs/Stages/Stage_01.asset` (`StageDefinition`): `killTarget`, `timeLimitSeconds` (**chỉ hiển thị** — đã chốt), 2–3 wave, **30 quái/wave** (mặc định tạm).
- Đặt `StageManager` + `StageDefinition` vào scene chơi; gắn `m_Container` (hoặc để tự tạo).
- Stage design (thành phần wave, `killTarget`, số wave) **chưa quyết** — dùng số mặc định của `StageDefinition` (`killTarget = 40`, `timeLimitSeconds = 90`) làm placeholder, chỉnh sau khi có thiết kế màn.

**Test:** vào scene, quái spawn theo wave, giết đủ thì `StageCleared`.

### GĐ 1 — Bootstrap NGO

- Cài `com.unity.netcode.gameobjects`, `com.unity.services.core`, `com.unity.services.relay`, `com.unity.services.lobby`, `com.unity.services.authentication` (anonymous) nếu GĐ 0 chưa cài. Kiểm tra API thực tế.
- `NetworkManager` + `UnityTransport`, `TickRate = 20`, Connection Approval đọc `NetLoadout`.
- Thêm `NetworkObject` cho **đúng 6 prefab tank**: `Assets/Prefabs/Tanks/BlackShadow/BlackShadow.prefab`, `Assets/Prefabs/Tanks/GreenFox/GreenFox .prefab`, `Assets/Prefabs/Tanks/Leviathan/Leviathan.prefab`, `Assets/Prefabs/Tanks/RedNova/RedNova.prefab`, `Assets/Prefabs/Tanks/WallTitan/WallTitan.prefab`, `Assets/Prefabs/Tanks/WaterB/WaterB.prefab` (+ `Assets/Prefabs/Tanks/Tank.prefab` nếu còn dùng), và prefab quái trong `Assets/Prefabs/Enemies/**`. **Tuyệt đối không** gắn `NetworkObject` vào 22 prefab skill/turret/aura nằm cùng thư mục.
- Lobby = **UI overlay trong `SampleScene`**, không tạo scene riêng: host tạo Relay + join code, client join, host bấm Start khi đủ roster.
- Viết `NetSpawnService.SpawnPlayer(clientId, NetLoadout)` gọi NGO spawn, sau đó `TankBrain.Initialize(context, stats)`. Đăng ký `NetworkPrefabs` chỉ gồm tank/quái/decoy.

**Test:** host tạo phòng + client join bằng code; thấy 2 tank; tank chỉ chạy khi host spawn (không còn `CreateTank` trong `Awake`).

### GĐ 2 — Di chuyển

- `NetPlayerController` (chỉ local player); `GameController` online bị tắt phần tạo tank.
- Owner `NetworkTransform`; validate host: `Physics2D.CapsuleCast`/`Linecast` đoạn cũ→mới + token bucket (không chỉ so khoảng cách).
- Proxy: kinematic Rigidbody2D + tắt movement branch.
- Camera follow local player.

**Test:** 4 client chạy vòng 60 s; thử teleport bằng cách sửa transform → host chặn/log.

### GĐ 3 — State người chơi

- `PlayerNetState` + `TankStatsSnapshot`, `CooldownState`, `ShieldTotal`/`ShieldLayerCount`, `CCState` (`Flags` bỏ).
- Sửa 5 hàm cooldown: `TickCooldowns`, `GetSkillCooldown`, `ResetSkillCooldown`, `ReduceSkillCooldown`, `SetSkillCooldown` (`TankBrain.cs`) + caller `TankBrain_BlackShadow.cs:184`.
- HUD đọc state; cooldown hiển thị `end - ServerTime`.

**Test:** cast → cooldown đúng trên 4 client; ăn CC → slow hiển thị + ảnh hưởng movement client đó.

### GĐ 4 — Combat về host

- Thêm cổng host ở đầu `TankBrain.TakeDamage`/`EnemyBrain.TakeDamage`/`Heal`/`GainEnergy`/`SpendEnergy`/`ApplyCrowdControl`: `if (NetworkManager.Singleton != null && !IsServer) return;`. Offline vẫn chạy bình thường; client online không có code nào cần damage local (chỉ đọc NetworkVariable).
- Rà **35 call site** theo Phụ lục A.2 để chắc chắn chúng chạy trên host: site tank→enemy vì skill chạy trên host; site enemy→tank vì AI chạy trên host.
- Bỏ `ReportHitServerRpc`; client không tự chạy skill. Handler `RequestCastServerRpc`: validate → `SetLookTarget(aimWorld)` → `UseSkill` → trả `CastResult`.
- Chuyển **42 chỗ** `TextDamageManager.Show` (39 gameplay + 3 debug) sang `CombatEventRpc` có `kind`; DoT gộp 0,5 s/target.
- Năng lượng theo đòn trúng (Leviathan `energyGainPerHit`) do host cộng.

**Test:** tắt host → không damage; 100 lần cast → HP khớp tuyệt đối; pierce/AoE/DoT chỉ trừ 1 lần.

### GĐ 5 — Quái và stage

- Chỉ host spawn quái (NGO); despawn thay `Destroy` (**14 file**, danh sách ở A.1).
- `EnemyNetState`: HP + `EnemyStatsSnapshot{maxHealth}` + CC.
- Client: tắt `EnemyBrain.Update`, `ChasingEnemyBrain.FixedUpdate`, `NormalEnemy_BaseChaser/BasePatrol/BaseStationary`, `EnemyMovementAgent`; `bodyType = Kinematic` (giữ collider).
- `StageManager`: roster, `EvaluateOutcome` theo cả nhóm, RPC event; `m_Container` không cần parent khi NGO spawn (ghi rõ).
- Sửa AI chọn mục tiêu gần nhất.

**Test:** 1 wave 20 quái; 4 client cùng số lượng/HP/vị trí/kill; 1 người chết không làm fail.

### GĐ 6 — VFX, aim, decoy

- ✅ `PlaySkillFxRpc` → thực tế dùng named message `IW.SkillFx` (`NetSkillFx`), replay bằng pool; host gọi trong `TankBrain.UseSkill`, client gọi `PlayRemoteSkillFx(index, aim)`.
- ✅ `AimAngle` host ghi cho proxy xoay nòng (`NetworkVariable<float>`, ghi 10 Hz, lệch ≥ 1°). Thân xe proxy: `NetProxyVisual` suy hướng từ delta vị trí `NetworkTransform` (root không xoay) và gọi `TankView.Move/Stop/Look`; dừng thì `Stop()`.
- ✅ Whitelist `CanReplayRemoteFx` + khoá side effect khi replay (xem "chỗ khác plan #8"), tránh client tự buff/khiên/hồi chiêu.
- ⬜ `ShadowExchangeClone` → `NetworkObject` server; HP + despawn.
- ⬜ Visual projectile/hazard/zone của quái phát lại cho client.
- ⬜ Template skill con trong prefab: chỉ bật trên đúng client sở hữu skill visual.

**Test:** 4 client bắn cùng lúc, VFX khớp ±100 ms; ảo ảnh hấp thụ đòn đúng trên host.

### GĐ 7* — Reconnect (backlog, ngoài MVP)

- MVP: rút mạng → despawn; vào lại → spawn lại từ đầu, không snapshot.
- Backlog: `playerId` ổn định, grace 60 s, `ChangeOwnership`/spawn lại, `SyncSnapshotRpc` (HP, energy, 4 cooldown end, `ShieldTotal`, CC, `SkillPhase`, stage).

**Test (khi làm):** rút mạng 5 s → reconnect → state đúng; spam reconnect bị chặn.

### GĐ 8 — Đo

- `NetworkMetrics` + log 1 dòng/giây; chỉnh `TickRate`/threshold/số quái.

---

## 5. Đo đạc và kiểm thử

### 5.1 Cách đo

| Chỉ số | Cách lấy |
|---|---|
| Byte in/out mỗi client | `NetworkMetrics`/`UnityTransport`, log 1 s |
| Số object đồng bộ | `NetworkManager.SpawnManager.SpawnedObjects.Count` |
| RTT | `NetworkTime`/ping RPC |
| CPU host | Profiler quanh AI + pathfinding + skill |
| FPS host | Profiler |

Log 1 dòng/giây: `t, downKBps, upKBps, objects, rttMs, fps`.

### 5.2 Ngưỡng thực tế (PC, 4 người, 30 quái/wave)

| Hạng mục | Ngưỡng |
|---|---|
| Down mỗi client | **< 60 KB/s** (30 quái × NetworkTransform 20 Hz ước ~18–25 KB/s + player + RPC) |
| Up mỗi client | < 5 KB/s |
| FPS host | ≥ 50 |
| RTT nội địa | < 80 ms |

Nếu vượt: giảm `TickRate` xuống 15, giảm số quái/wave, hoặc giảm tần suất NetworkTransform của quái (nếu API cho phép). Không đặt ngưỡng 30 KB/s như bản 2: 30 quái đã ước 18–25 KB/s, cộng player + RPC sẽ sát trần.

### 5.3 Test matrix

| Tình huống | Cách làm | Kỳ vọng |
|---|---|---|
| Mất kết nối 5 s | Rút mạng | Grace + reconnect đúng |
| Rớt vĩnh viễn | Tắt client | 3 người chơi tiếp |
| Lag 200 ms + 5% loss | Simulator | Interpolation mượt |
| 2 người cast cùng lúc | Cùng frame | Host xử lý tuần tự, không nhân đôi |
| Thiếu năng lượng / đang CD | Spam cast | Host từ chối; client thấy cooldown |
| Client khác cast (host không có aim) | Client 2 bấm cast | Host `SetLookTarget(aimWorld)` từ RPC → đạn/VFX đúng hướng, không im lặng |
| 4 người đứng cạnh quái melee | Quái lao vào | Collider tank proxy vẫn nằm trong physics world → quái đánh trúng |
| Offline sau khi thêm cổng host | Editor, không `NetworkManager` | Damage/heal/CC vẫn chạy |
| Pierce/AoE | Water Pulse, Nova Barrage | Mỗi quái trừ đúng 1 lần |
| DoT | Burn | Chỉ host tick; client hiện effect |
| Redirect + khiên | Guardian Current + Shadow Guard | Chia trước, chặn sau, không âm |
| Player A buff/khiên player B | Tide Commander | B thấy khiên đúng |
| Decoy | Shadow Exchange | Ảo ảnh hấp thụ đúng trên host |
| Player chết | Chết 1 người | Không fail; cả nhóm chết mới fail |
| Quái đuổi người #2–#4 | Đứng xa | Quái chọn đúng người gần nhất |
| Host pause/alt-tab | Mất focus | Trận không lệch giờ (dùng ServerTime, `runInBackground`) |
| 4 client 1 máy | Multiplayer Play Mode | FPS host ≥ 50 |
| 30 quái | Wave lớn | Down < 60 KB/s |

---

## 6. Rủi ro và câu hỏi

### 6.1 Rủi ro

| Rủi ro | Mức | Giảm thiểu |
|---|---|---|
| Hai nguồn damage (35 call site) | Cao | GĐ 0 gate + GĐ 4 checklist Phụ lục A.2 + test "tắt host" |
| Stage chưa có dữ liệu | Cao | GĐ 0.5 trước khi làm mạng |
| Host thoát giữa trận | Cao | MVP: kết thúc trận; sau: dedicated |
| Client sửa vị trí | Trung bình | CapsuleCast + token bucket |
| Proxy vật lý đè NetworkTransform | Cao | Kinematic + tắt movement (GĐ 0/2) |
| Stats/buff lệch | Trung bình | `TankStatsSnapshot` + rebuild từ bonus key |
| Ảo ảnh/DoT bị coi là VFX | Cao | NetworkObject cho decoy; host tick DoT |
| Bandwidth 30 quái/wave | Trung bình | Ngưỡng 60 KB/s + đo trước |
| NGO API khác tài liệu | Trung bình | Cài qua Multiplayer Center, xác minh từng API |
| Reconnect không có sẵn | Trung bình | MVP: respawn từ đầu; GĐ 7* là backlog |
| **Transport sập cả phiên khi một client bị timeout** | Trung bình | Đã kiểm chứng: client rời êm (`-iw-exit-after`, `Shutdown()` rồi `Application.Quit`) → host `4 → 3`, các client còn lại **không** mất kết nối và vẫn đồng bộ HP/vị trí/RPC; client bị **kill** (host thấy `ProtocolTimeout`) → driver UTP vẫn `created=True listening=True` nhưng **toàn bộ client còn lại bị ngắt theo** (host `4 → 3 → 1`, client log `Mất kết nối tới host`) và **không nhận client mới nào nữa** (NGO 2.14 + UTP 6.5.0). MVP: chấp nhận hạn chế transport, host tạo lại phòng khi có người crash (user test lại 2026-11-17 trên máy riêng: thoát client → phiên vẫn chạy, client mới join được ⇒ chỉ còn khi kill cứng). Cờ test `-iw-rehost-after <giây>` (host `Shutdown()` → chờ 2 s → `StartHost()`) nằm **chỉ trong `NetAutoConnect`** và đã bọc `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; bản build phát hành không có logic tự rehost. Dài hạn: nâng NGO/UTP hoặc tự quản lý transport.. **User test lại 2026-11-17 trên máy riêng (2 client + 1 host): thoát 1 client → client còn lại và host vẫn chơi tiếp, client mới join được ⇒ không tái hiện; hạ mức rủi ro xuống Trung bình, hiện chỉ gặp khi client bị kill cứng (ProtocolTimeout) hoặc khi test nhiều tiến trình trên cùng máy có socket cũ.** |
| Awake tự hồi máu | Cao | Guard `m_NetworkManaged` (không dùng `IsSpawned` trong `Awake`) |
| Proxy mất collider (`simulated = false`) | Cao | Bắt buộc `bodyType = Kinematic`, giữ collider |
| Text heal bị đẩy qua `CombatEventRpc` thiếu `kind` | Trung bình | `CombatEventRpc` có `kind` damage/heal/absorb |
| Quái tự Destroy | Cao | Đổi sang Despawn host |

### 6.2 Câu hỏi — trạng thái

| # | Câu hỏi | Trạng thái | Ghi chú |
|---|---|---|---|
| 1 | Host mô phỏng toàn bộ combat (bỏ client ReportHit) | **ĐÃ CHỐT: 100%** (2026-11-16) | 35 call site damage/energy/CC chỉ host chạy; client chỉ input + VFX |
| 2 | `timeLimit` là hiển thị hay điều kiện thua | Đề xuất: chỉ hiển thị | Giữ `StageDefinition.cs:69`, `EvaluateOutcome` không đổi |
| 3 | Host thoát giữa trận | Đề xuất: kết thúc trận | `MatchEndRpc` lý do `HostLeft` |
| 4 | Reconnect trong MVP | **ĐÃ CHỐT: không** (2026-11-16) | Snapshot + grace là backlog GĐ 7*; rớt = despawn + respawn từ đầu |
| 5 | Va chạm giữa 4 tank | Đề xuất: tắt đẩy khi online | `Physics2D.IgnoreCollision` giữa tank cùng phe (host set) |
| 6 | Lobby | **ĐÃ CHỐT: UI overlay trong `SampleScene`** (2026-11-16) | Không tạo `Lobby.unity`, không `NetworkSceneManager` |
| 7 | Số quái tối đa 1 wave | **ĐÃ CHỐT: 30 (mặc định)** (2026-11-16) | Stage design chưa quyết — dùng 30 khi làm `Stage_01.asset` ở GĐ 0.5 |
| 8 | Steamworks | Đề xuất: không cần ở MVP | Relay join code thủ công |
| 9 | Scale HP quái theo số người (`EnemyBrain.SetCoopMode` có sẵn, 0 caller) | **ĐÃ CHỐT: không** (2026-11-16) | Để nguyên `SetCoopMode`, MVP không gọi |

> Ghi chú: 5 câu **đã chốt** đã được phản ánh vào TL;DR, GĐ 0–1, §5.2 và Phụ lục A. Các câu còn "Đề xuất" không chặn triển khai — cứ làm theo đề xuất, chỉ cần xác nhận lại trước GĐ 5 (Q2/Q3) và GĐ 2 (Q5).

---

## Phụ lục A — File/asset sẽ chạm

### A.1 File/asset

**Sửa:**

- [ ] `Assets/Prefabs/Tanks/BlackShadow/BlackShadow.prefab`, `GreenFox/GreenFox .prefab`, `Leviathan/Leviathan.prefab`, `RedNova/RedNova.prefab`, `WallTitan/WallTitan.prefab`, `WaterB/WaterB.prefab` — thêm `NetworkObject`, `PlayerNetState`. **Không** thêm vào 22 prefab skill/turret/aura cùng thư mục
- [ ] `Assets/Prefabs/Enemies/**` (20+) — thêm `NetworkObject`, `EnemyNetState`; tắt `EnemyMovementAgent` cho proxy
- [ ] `Assets/Scripts/Core/Effects/BurnEffect.cs` — chỉ host tick damage
- [ ] `Assets/Scripts/Tanks/GameController.cs` — không tạo tank khi online; tách input
- [ ] `Assets/Scripts/Tanks/TankBrain.cs` — hai cờ `IsLocallyControlled`/`IsServerSimulated`, setter state, guard `m_NetworkManaged`, bỏ debug `:640`
- [ ] `Assets/Scripts/Tanks/TankDefinition.cs` — bỏ đường `Instantiate` khi online (hoặc thêm `SpawnViaNetwork`)
- [ ] `Assets/Scripts/Tanks/BlackShadow/TankBrain_BlackShadow.cs` — caller `:184`, skill state
- [ ] `Assets/Scripts/Enemies/EnemyBrain.cs` — `IsServerSimulated`, `TakeDamage` host gate, despawn
- [ ] `Assets/Scripts/Enemies/ChasingEnemyBrain.cs` — chọn mục tiêu gần nhất + gate `FixedUpdate` cho proxy
- [ ] `Assets/Scripts/Enemies/Navigation/EnemyMovementAgent.cs` — guard `OnEnable`
- [ ] 14 file bỏ `Destroy(gameObject)` (16 chỗ): `BlackMoonBrain.cs:50`, `CrystalShooterBrain.cs:177`, `DeadlandPhantomBrain.cs:151`, `FlamePatch.cs:81`, `GraveyardWispBrain.cs:104`, `MeteorParasiteBrain.cs:43/:105`, `QuartzBruteBrain.cs:104`, `QuartzSpike.cs:66/:107`, `SeauBearusBrain.cs:163`, `Normal/EnemyProjectileBase.cs:279`, `Normal/NormalEnemy_BaseChaser.cs:409`, `Normal/NormalEnemy_BasePatrol.cs:230`, `Normal/NormalEnemy_BaseStationary.cs:134`, `Normal/NormalEnemy_KySinhThienThach.cs:259`
- [ ] `Assets/Scripts/Stages/StageManager.cs` — roster, chỉ host, RPC
- [ ] `Assets/Scripts/Core/TextDamageManager.cs` — chỉ từ RPC
- [ ] 35 call site `TakeDamage` + 42 call site `Show` (A.2/A.3)

**Tạo mới:**

- [ ] `Assets/Scripts/Net/NetBootstrap.cs`, `NetPlayerController.cs`, `NetProxyVisual.cs`, `PlayerNetState.cs`, `EnemyNetState.cs`, `MatchFlow.cs`, `NetSpawnService.cs`
- [ ] `Assets/Scripts/Net/Payloads.cs` (`NetLoadout`, `CooldownState`, `CastResult`, `TankStatsSnapshot`, `EnemyStatsSnapshot`, `CCState`)
- [ ] `Assets/Prefabs/Stages/Stage_01.asset` (`StageDefinition`)
- [ ] UI lobby overlay trong `SampleScene` (không tạo `Lobby.unity`)

### A.2 35 call site `TakeDamage` (gameplay)

**Tank → Enemy (16):** `OptionalSkill2.cs:132`, `TankBrain_BlackShadow.cs:518`, `:654`, `GreenFox_BurnEffect.cs:113`, `GreenFox_MagicTrap.cs:124`, `TankBrain_GreenFox.cs:212`, `OptionalSkill1_Leviathan.cs:222`, `OptionalSkill2_Leviathan.cs:205`, `TankBrain_Leviathan.cs:634`, `:710`, `TankBrain_RedNova.cs:312`, `TankBrain_WallTitan.cs:286`, `TankBrain_WaterB.cs:252`, `WaterB_HealingProjectile.cs:120`, `WaterB_TurretProjectile.cs:111`, `Core/Effects/BurnEffect.cs:39`

**Enemy → Tank (18):** `Core/Effects/BurnEffect.cs:46`, `ChasingEnemyBrain.cs:155`, `CrystalShooterBrain.cs:157`, `FlamePatch.cs:100`, `QuartzSpike.cs:103`, `SeauBearusBrain.cs:97`, `:127`, `NormalEnemy_KIenLuaKhongLo.cs:159`, `NormalEnemy_KySinhThienThach.cs:235`, `NormalEnemy_MatTrangDen.cs:137`, `NormalEnemy_MatTroiDo.cs:211`, `NormalEnemy_NamDocGaiThach.cs:261`, `NormalEnemy_QuaiVatDamLay.cs:301`, `NormalEnemy_QuaiVatTinhThach.cs:188`, `NormalEnemy_TinhLinhNghiaDia.cs:168`, `NormalEnemy_TinhLinhSamSetNho.cs:516`, `NormalEnemy_XaThuTinhThe.cs:140`, `NormalEnemy_XenNuoc.cs:337`

**Tank → Tank (1):** `TankBrain.cs:726` (chuyển hướng sát thương Guardian Current)

**Nội bộ/debug (4):** `TankBrain_Leviathan.cs:840` (base override), `NormalEnemy_KySinhThienThach.cs:159` (base override), `DebugDame.cs:50`, `DamageTestTrigger.cs:108`

### A.3 42 call site `TextDamageManager.Show`

39 chỗ gameplay (trong đó ≥5 chỗ in **lượng hồi máu**, không phải sát thương: `TankBrain_WaterB.cs:179`, `NormalEnemy_SaoDen.cs:180`, `WaterB_HealingProjectile.cs:121`, `WaterB_SharedHealingLink.cs:169`, `OptionalSkill1_2_Leviathan.cs:343`) + 3 chỗ debug (`TankBrain.cs:640`, `DebugDame.cs:80/:85`). Chuyển sang `CombatEventRpc` có `kind`; các file chính: `TankBrain_WaterB.cs:253`, `WaterB_TurretProjectile.cs:112`, `TankBrain_WallTitan.cs:287`, `TankBrain_RedNova.cs:314`, `OptionalSkill1_Leviathan.cs:223`, `OptionalSkill2_Leviathan.cs:206`, `TankBrain_Leviathan.cs:635`, `BurnEffect.cs`, `GreenFox_*`, enemy scripts.

---

## Phụ lục B — Thuật ngữ

| Thuật ngữ | Nghĩa |
|---|---|
| Host | Người chơi thứ 0 đóng vai server |
| Proxy | Bản sao entity trên client, không tự mô phỏng gameplay |
| Host-simulated combat | Toàn bộ skill/damage chạy trên host; client chỉ input + VFX |
| Owner-authoritative | Client sở hữu object được quyết định (vị trí tank của mình) |
| `NetworkObject` | Đơn vị NGO đồng bộ |
| `NetworkVariable` / `NetworkList` | State replicate, chỉ gửi khi dirty |
| `NetworkTransform` | Đồng bộ transform |
| RPC | Lệnh gọi từ xa |
| Observer | Cơ chế NGO chọn client nào thấy object |
| Grace | Thời gian giữ slot người vừa rớt |

---

*Hết tài liệu. Bản 4.1 — 2026-11-16.*
