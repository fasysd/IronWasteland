# Thiết kế Server/Client — Online Co-op 4 người (IronWasteland)

> Tài liệu tổng hợp khi so sánh nhánh **feature/online-coop-4p** với **main**.
> Phạm vi: kiến trúc mạng server/client và toàn bộ thay đổi code phục vụ mạng.
> **Không** bao gồm phần UI (màn hình chính, HUD, panel lobby IMGUI, text hiển thị).

| Mốc | Commit |
|---|---|
| main | `e82ee94` — Cập nhật: WaterB và GreenFox chuyển sang dùng ColliderOwnerRef |
| nhánh hiện tại | `07ed265` — feat(mp): host phai bam bat dau tran, khong tu dong vao khi moi nguoi san sang |

- **63 commit**, **161 file**, **+19.212 / −2.488** dòng (`git diff main...HEAD`).
- Thêm mới trọn bộ module mạng `Assets/Scripts/Net/` (23 script) + `Assets/DefaultNetworkPrefabs.asset`.
- Tài liệu kế hoạch gốc đã có trên nhánh: `docs/plans/ONLINE_COOP_4P_PLAN.md` (802 dòng).
- Package thêm mới (`Packages/manifest.json`): `com.unity.netcode.gameobjects 2.14.0`, `com.unity.services.lobby 1.3.0`,
  `com.unity.services.relay 1.2.0`, `com.unity.pipeline 0.9.0-exp.1`; nâng `com.ivanmurzak.unity.mcp 0.93.3 → 1.4.0`.

---

## 1. Kiến trúc tổng thể

### 1.1 Topology: host-authoritative, client chỉ điều khiển xe của mình

```text
        ┌──────────────────────── Host (client 0 = server) ──────────────────────────┐
        │  NetworkManager · Relay · Lobby                                            │
        │  SERVER QUYẾT: spawn · AI · skill · damage · energy · cooldown · CC · stage │
        │  CLIENT QUYẾT: vị trí tank của chính nó (host validate)                     │
        └──────────────▲──────────────────────────────▲──────────────────────────────┘
                       │  Unity Transport / Relay (UTP)                             │
        ┌──────────────┴─────────┐   ┌────────────────┴────────┐   ┌────────────────┐
        │ Client 1               │   │ Client 2                │   │ Client 3       │
        │ tank mình: owner       │   │ ...                     │   │ ...            │
        │ còn lại: proxy         │   │                         │   │                │
        └────────────────────────┘   └─────────────────────────┘   └────────────────┘
```

- `NetBootstrap` đặt `NetworkConfig.TickRate = 20` (4 người, nhẹ hơn mặc định 30 Hz).
- Offline (không có `NetworkManager.Singleton`) vẫn chạy như single-player: mọi cổng quyền được mở.
- Client **không mô phỏng** skill/AI/damage: chỉ hiển thị và gửi yêu cầu.

### 1.2 Ba nhóm trạng thái

| Nhóm | Nội dung | Cơ chế |
|---|---|---|
| Derived (không truyền) | cooldown còn lại, effect còn lại, animation, VFX, damage text, camera, input | tự tính tại chỗ |
| Replicated | HP, energy, loadout, cooldown end, shield, max HP/move speed, CC nhẹ, HP quái | `NetworkVariable<T>` |
| Event | cast FX, damage/heal text, effect add/remove, wave/kill, kết thúc, snapshot phòng | named message / RPC |

### 1.3 Bảng phân quyền

| Việc | Ai quyết | Hiện thực |
|---|---|---|
| Di chuyển tank của mình | Client sở hữu | `NetworkTransform` authority Owner |
| Cast skill | Client gửi yêu cầu, host xử lý | `RequestCastServerRpc` → host `SetLookTarget` + `UseSkill` |
| Damage / heal / energy / cooldown / CC / shield | Host | cổng `HasGameplayAuthority` trong `TankBrain` |
| AI, spawn quái, pathfinding, wave | Host | `EnemyBrain.IsServerSimulated`, `StageManager` |
| VFX, damage text, hướng nòng proxy | Client | replay từ named message + `AimAngle`/`SlowSpeedFactor` |
| Vòng đời phòng (sẵn sàng, vào trận, chơi lại) | Host | `MatchFlow` + snapshot `IW.Match` |

### 1.4 Vật lý & proxy

- Tank của mình: Rigidbody2D động, `NetworkTransform` không interpolate.
- Tank người khác: `NetworkTransform` authority Owner + interpolate; tắt nhánh input/movement trong brain,
  giữ collider (đổi `bodyType` chứ **không** `simulated = false` — host vẫn phải quét trúng).
- Quái trên client: `IsServerSimulated = false` ⇒ tắt AI/movement, chỉ chạy `NetworkTransform` của server.
- Đạn/VFX/ảo ảnh thuần hình: **không** là `NetworkObject`; host phát "ghost" qua `NetVisualSpawn`.

---

## 2. Mô hình dữ liệu đồng bộ

### 2.1 `PlayerNetState` (`Assets/Scripts/Net/PlayerNetState.cs`) — NetworkVariable

| Biến | Kiểu | Ghi chú |
|---|---|---|
| `Health` | float | host ghi khi damage/heal |
| `Energy` | float | host ghi khi cast/hồi |
| `Cooldowns` | `CooldownState` | 4 mốc `ServerTime`, 0 = sẵn sàng |
| `Loadout` | `NetLoadout` | tank/level/equipment/option skills |
| `Shield` | float | tổng khiên host tính |
| `MaxHealth`, `MoveSpeed` | float | snapshot chỉ số để HUD/movement client dùng |
| `SlowSpeedFactor`, `MovementBlocked` | float / bool | CC nhẹ, client tự áp vào di chuyển |
| `AimAngle` | float | **owner ghi** (nòng là con của `WeaponPivot`, không nằm trong `NetworkTransform`) |

API: `SetServerLoadout(NetLoadout)`, `GetRemainingCooldown(int)`, `Brain`, `Health`, `Energy`,
`ShieldAmount`, `AimAngle`, `LoadoutApplied`; spawn/despawn xử lý ở `OnNetworkSpawn`/`OnNetworkDespawn`.

### 2.2 `EnemyNetState` (`Assets/Scripts/Net/EnemyNetState.cs`)

- `Health`, `MaxHealth`: `NetworkVariable<float>` — read Everyone, write Server.
- Client đọc `Health`/`MaxHealth` để vẽ thanh máu; AI và damage chỉ chạy trên host.

### 2.3 Payload struct

| Struct | Field | Dùng ở |
|---|---|---|
| `NetLoadout` | TankIndex, Level, EquipmentId, OptionSkill1, OptionSkill2 | connection approval + snapshot |
| `CooldownState` | `double end0..end3` | `PlayerNetState.Cooldowns` |
| `LobbyRoom` (nested) | Id, Name, LobbyCode, Players, MaxPlayers, AvailableSlots | `NetRelayService` |

### 2.4 Named message (host → client, trừ khi ghi khác)

| Kênh | Hằng khai báo | Kích thước | Nội dung |
|---|---|---|---|
| `IW.SkillFx` | `NetSkillFx.MessageName` | 40 B | phát lại cast: owner, skill index, aim, origin, option1/2 |
| `IW.SkillFxMark` | `MarkExplosionMessageName` | 32 B | nổ Fox Mark: owner + enemy netId + vị trí |
| `IW.SkillFxKillStream` | `KillStreamMessageName` | 24 B | vệt sáng hạ gục 1A: owner + vị trí |
| `IW.SkillFxTank` | `TankFxMessageName` | 24 B | hiệu ứng riêng tank: owner + `fxCode` + vị trí |
| `IW.Effect`, `IW.EnemyEffect` | `NetEffectSync` | — | add/remove effect (`OpApply = 0`, `OpRemove = 1`) |
| `IW.CombatFeedback` | `NetCombatFeedback.MessageName` | — | damage/heal text, hit feedback |
| `IW.StageEvent` | `NetStageEvents.MessageName` | — | `Started 0`, `Cleared 1`, `Failed 2`, `Running 3`, `Idle 4` |
| `IW.Visual`, `IW.VisualEnd`, `IW.VisualPrimitive` | `NetVisualSpawn` | — | spawn ghost, thu hồi theo handle, vòng tròn primitive |
| `IW.Match` | `MatchFlow.SnapshotMessage` | 96 B writer | snapshot phòng: phase, mode, stage, roster, ready, option |
| `IW.MatchRequest` | `MatchFlow.RequestMessage` | 2 B | **client → host**: `op` + `value` |

`fxCode` của `IW.SkillFxTank` do từng tank quy ước: WallTitan `FxCodeEarthShards = 1`;
RedNova `FxCodeExecutionShockwave = 1`; WaterB `FxCodeSharedHealFromOwner = 1`, `FxCodeSharedHealFromAlly = 2`.

### 2.5 RPC (`Assets/Scripts/Net/NetPlayerController.cs`)

| RPC | Hướng | Tác dụng |
|---|---|---|
| `RequestCastServerRpc(int skillIndex, Vector2 aimWorld)` | client → host | validate `CanUseSkill`/cooldown/energy/CC rồi `UseSkill` |
| `CastRejectedClientRpc(byte result, int skillIndex)` | host → owner | trả `CastResult` (Invalid, NoEnergy, OnCooldown, CrowdControlled, RateLimited) |
| `CorrectPositionClientRpc(Vector3)` | host → owner | kéo vị trí owner về đúng khi lệch |
| `TeleportOwnerClientRpc(Vector3)` + `ServerTeleport(Vector3)` | hai chiều | dịch chuyển hợp lệ (ShadowStep, teleport skill) |

### 2.6 `MatchFlow` ops trong `IW.MatchRequest`

| Op | Hằng | Ý nghĩa |
|---|---|---|
| 0 | `OpSetTank` | đổi tank |
| 1 | `OpSetReady` | bật/tắt sẵn sàng |
| 2 | `OpStart` | hằng số còn khai báo nhưng `HandleRequest` **không** xử lý; host luôn vào trận bằng cách bấm nút → `StartMatch()` |
| 3 | `OpReplay` | chơi lại |
| 4/5 | `OpSetOption1/2` | đổi biến thể optional skill |

> Từ commit `07ed265`: **không còn tự vào trận**. `ApplyReadyRequest` chỉ ghi cờ + `Broadcast()`;
> host phải bấm "Bat dau tran" (nút chỉ bật khi mọi người ready) → `MatchFlow.StartMatch()`.

---

## 3. Module mạng (`Assets/Scripts/Net/`)

| File | Trách nhiệm | API chính |
|---|---|---|
| `NetBootstrap.cs` | vòng đời phiên, transport, Relay/Lobby, loadout | `StartHost()`, `StartClient()`, `StartHostWithRelay()`, `StartClientWithRelay()`, `Shutdown()`, `Rehost(bool)`, `SpawnTankFor(...)`, `DespawnAllTanks()`, `GetLoadout()`, `SetLocalOptionSkills()` |
| `NetSpawnService.cs` | dựng xe/quái qua `NetworkObject` | `SpawnPlayerTank(clientId, loadout)`, `SpawnEnemy(prefab, pos, stats)`, `TryGetTank`, `TryResolveTank`, `DespawnPlayerTank`, `DespawnAll` |
| `MatchFlow.cs` | lobby: roster, ready, chọn tank/option, vào trận, thắng/thua, chơi lại | `OnHostStarted`, `RequestReady`, `RequestTank`, `RequestOptionSkills`, `StartMatch`, `Replay`, `TryGetPlayer`, `Broadcast` |
| `PlayerNetState.cs` | NetworkVariable của tank + áp loadout | `SetServerLoadout`, `GetRemainingCooldown`, `Health`, `Energy`, `ShieldAmount`, `AimAngle` |
| `EnemyNetState.cs` | máu quái qua mạng | `Health`, `MaxHealth` |
| `NetPlayerController.cs` | input owner, cast RPC, camera, proxy body | `RequestCast`, `ServerTeleport`, `CorrectPositionClientRpc` |
| `NetSkillFx.cs` | phát lại 4 nhóm hiệu ứng kỹ năng | `Broadcast`, `BroadcastMarkExplosion`, `BroadcastKillStream`, `BroadcastTankFx` |
| `NetVisual.cs` | helper spawn ghost theo template | `Spawn`, `Spawn<T>`, `SpawnHoming<T>`, `SpawnPrimitive`, `CreateSpriteMaterial`, `EndGhost` |
| `NetVisualSpawn.cs` | protocol ghost: spawn/follow/end | `Broadcast`, `BroadcastByName`, `BroadcastDespawn`, `Follow`, `Remember`, `End` |
| `NetEffectSync.cs` | add/remove effect theo mục tiêu | `Broadcast*Effect`, `Broadcast*Remove` |
| `NetStatusVisual.cs` | aura khiên/buff phía client | client đọc `PlayerNetState.ShieldAmount` + `NetEffectSync`; host/offline đọc `TankBrain.CurrentShield`/`ActiveEffects` |
| `NetCombatFeedback.cs` | damage/heal text | `Broadcast*`, handler `IW.CombatFeedback` |
| `NetStageEvents.cs` | trạng thái màn chơi | `ResetState`, `Broadcast(stage, state)`, `BroadcastStatus`, `BroadcastIdle` |
| `NetEntity.cs` | despawn thống nhất host/client | `Despawn(GameObject)` |
| `NetTeleport.cs` | dịch chuyển tank an toàn | `Move(TankBrain, Vector3)` |
| `NetProxyVisual.cs` | suy hướng thân proxy từ delta vị trí | — |
| `NetCameraFollow.cs` | camera bám xe của chính máy | `SetTarget(Transform)` |
| `NetRelayService.cs` | UGS Relay + Lobby, tìm phòng, heartbeat | `CreateHostAllocationAsync`, `JoinAllocationAsync`, `CreateLobbyAsync`, `QueryEligibleRoomsAsync`, `JoinLobbyByIdAndGetRelayCodeAsync`, `JoinLobbyAndGetRelayCodeAsync` |
| `NetLoadout.cs`, `CooldownState.cs` | payload serialize | `INetworkSerializable` |
| `NetAutoConnect.cs` | harness CLI `-iw-*` (host/join/ready/autocast/…) | `-iw-host`, `-iw-join`, `-iw-flow coop`, `-iw-ready` |
| `NetLobbyUI.cs`, `NetDebugHud.cs` | **UI/harness — ngoài phạm vi tài liệu** | — |

---

## 4. Luồng chạy

### 4.1 Tạo / vào phòng

```text
Host: StartHostWithRelay() → CreateHostAllocationAsync → CreateLobbyAsync(private=false, Data[relayJoinCode])
      → NetworkManager.StartHost + MatchFlow.OnHostStarted (Phase = Lobby)
Client: QueryEligibleRoomsAsync → JoinLobbyByIdAndGetRelayCodeAsync → StartClientWithRelay(code)
        hoặc nhập mã trực tiếp (mã phòng hoặc mã Relay) → JoinLobbyAndGetRelayCodeAsync
```

- IP LAN vẫn dùng được: `StartHost()` / `StartClient()` với `joinAddress:7777` (`SetJoinTarget`, `SetHostPort`).
- `NetRelayService` giữ heartbeat lobby, rời phòng dọn thành viên, tạo lại phòng khi host thoát.

### 4.2 Spawn xe khi vào phòng

```text
Host: HandleServerClientConnected → GetLoadout(clientId) → AddOrUpdatePlayer(tank, ready=false, option1/2)
      → nếu Phase = Playing: SpawnTankFor(clientId, tank, option1, option2)
SpawnPlayerTank: netState.SetServerLoadout(loadout) TRƯỚC SpawnAsPlayerObject
                 → new TankBrainContext(level, equipment, option1, option2) → definition.InitializeTank
Client: PlayerNetState.ApplyLoadout (client-only) → InitializeTank cùng loadout
```

⇒ Bản host và bản client của cùng một xe nhận **cùng option skill**, cùng level/equipment.

### 4.3 Một lần cast

```text
Client A: bấm skill → RequestCast → RequestCastServerRpc(skillIndex, aimWorld)
Host: validate alive/CC/energy/cooldown → SetLookTarget(aim) → UseSkill(index)
      ├─ damage/AoE/DoT/ảo ảnh xử lý trên host
      ├─ HP quái đổi → NetworkVariable
      ├─ NetSkillFx.Broadcast(...) khi skill thành công (SkillExecutionSucceeded)
      └─ NetCombatFeedback.Broadcast(...) cho damage text
Client: nhận IW.SkillFx → tank.PlayRemoteSkillFx(...) với option1/2 của host
        (IsRemoteFxReplay = true: chỉ dựng hình, khoá damage/energy/shield/cooldown)
```

### 4.4 Hiệu ứng sinh trong luồng chỉ host chạy

Đây là nhóm lỗi đã sửa trong các commit `73a05f9`, `9c8de38`, `0fe0132`:

| Hiệu ứng | Nguồn host | Kênh bù cho client |
|---|---|---|
| Nổ Fox Mark khi đầy cộng dồn | `TankBrain_GreenFox.TriggerMarkExplosion` | `IW.SkillFxMark` → `PlayRemoteMarkExplosionFx` |
| Vệt sáng hạ gục 1A | `NotifyEnemyKilled` | `IW.SkillFxKillStream` → `PlayRemoteKillStreamFx` |
| Mảnh đất WallTitan | `OnEarthBreakTargetFound` | `IW.SkillFxTank` (fxCode 1) → `PlayRemoteTankFx` |
| Xung kích RedNova | `OnExecutionCoreKill` | `IW.SkillFxTank` (fxCode 1) |
| Hồi máu chia sẻ WaterB | `WaterB_SharedHealingLink.ShareHeal` | `IW.SkillFxTank` (fxCode 1/2) → `PlayRemoteHealVisual` |

### 4.5 Quái và màn chơi

- Host chạy AI; client tắt `EnemyBrain.Update` và mọi movement khi `IsServerSimulated == false`.
- Quái chết: host `NetEntity.Despawn` → `NetworkObject.Despawn`; client không tự `Destroy`.
- `StageManager` chỉ chạy trên host; mỗi giây phát `BroadcastStatus` (`StateRunning`) để client đồng bộ màn/kill/wave;
  `StateStarted/Cleared/Failed/Idle` phát theo sự kiện.

### 4.6 Kết thúc / chơi lại

- Kết thúc: host phát `Phase = Cleared/Failed` → client hiện overlay (UI tách riêng).
- `Replay()`: despawn toàn bộ xe, `Stage.ResetToIdle()`, `NetStageEvents.ResetState() + BroadcastIdle()`, về Lobby.
- Mất kết nối: `NetBootstrap` phát hiện disconnect, lưu `LastDisconnectReason`, `ConnectionLost/SessionEnded`.

---

## 5. Thay đổi code gameplay phục vụ mạng (ngoài module Net)

### 5.1 `Assets/Scripts/Tanks/TankBrain.cs` (+333 dòng)

- Cờ quyền: `NetworkSessionActive`, `HasGameplayAuthority => !NetworkSessionActive || IsServer`,
  `IsServerSimulated`, `IsRemoteFxReplay`.
- Cổng host ở: `TakeDamage`, `Heal`, `GainEnergy`, `SpendEnergy`, `ApplyCrowdControl`, `RemoveCrowdControl`,
  `ClearAllCrowdControl`, `ApplyEffect`, `CreateShield`, `ClearShield`, `AddDamageRedirector`, các hàm set cooldown.
- `UseSkill`: `if (!IsServerSimulated && !IsRemoteFxReplay) return;`; chỉ broadcast `NetSkillFx` khi
  `SkillExecutionSucceeded` — tránh client phát lại hiệu ứng mà host đã thất bại.
- Hook phát lại hình ảnh: `PlayRemoteSkillFx`, `PlayRemoteMarkExplosionFx`, `PlayRemoteKillStreamFx`, `PlayRemoteTankFx`.

### 5.2 Quái (`Assets/Scripts/Enemies/**`, 14 file brain)

- `EnemyBrain.IsServerSimulated` (mặc định true): chặn `Die`, DoT/buff, các nhánh AI trên client.
- `NormalEnemy_BaseChaser/BasePatrol/BaseStationary`, `EnemyMovementAgent` tắt vòng movement trên client.
- Chuyển despawn sang `NetEntity.Despawn`; đồng bộ `MaxHealth` ngay lúc spawn.

### 5.3 Tank kỹ năng (6 tank)

- `TankBrain_GreenFox.cs` (+131): variant theo option, `NotifyEnemyKilled`, broadcast 3 nhóm hiệu ứng,
  `IsRemoteFxReplay` cho 1A/1B/1C và 2A/2B/2C.
- `Assets/Scripts/Tanks/BlackShadow/**`, `Leviathan/**`, `RedNova/**`, `WallTitan/**`, `WaterB/**`:
  thêm guard quyền, `NetStatusVisual`/`NetEffectSync`, các `PlayRemote*Fx`.
- `TankDefinition.cs`, `GameController.cs`, `StageManager.cs`: nhánh mạng (không chạy single-player khi đang online).

### 5.4 Asset / prefab / scene

- 8 prefab tank + 24 prefab quái được gắn `NetworkObject` (+ `NetworkTransform`); ví dụ
  `Assets/Prefabs/Enemies/ChapterOne/BlackMoon.prefab:105` (`NetworkObject`) và `:145` (`NetworkTransform`).
- `Assets/DefaultNetworkPrefabs.asset` (đăng ký prefab mạng toàn cục).
- `Assets/Prefabs/Stages/Stage_01.asset`, `Assets/Scenes/SkillArena.unity` mới; `Assets/Scenes/SampleScene.unity` sửa
  (thêm hệ thống Net + UI test).
- `Assets/Scripts/Core/CombatFeedback.cs`, `Assets/Scripts/Core/TankCollisionSetup.cs` mới;
  `TextDamageManager`, `BurnEffect` sửa để không tạo hiệu ứng cục bộ khi đang online.

---

## 6. Lịch sử commit theo chủ đề (63 commit)

| Giai đoạn | Commit tiêu biểu | Nội dung |
|---|---|---|
| Nền tảng NGO | `df6efb0`, `8b1d20f`, `a99e5ab`, `4235c2b`, `c81a433` | aim gửi lên host, teleport, đồng bộ hình ảnh kỹ năng, sửa giật sau teleport |
| Vòng đời trận/lobby | `afa5796`, `b53ef46`, `4088e99`, `1cda51c`, `326708b`, `a77d791` | lobby, sẵn sàng, vào trận, thắng/thua, chơi lại, heartbeat 15 s, dọn khi host thoát |
| Đồng bộ state | `774b369`, `1302ae2`, `3e114c9`, `d70cf1f`, `32d461e`, `e47bd14`, `fdc22a9` | MaxHealth quái, shield/effect, CastResult, chặn va chạm giữa xe, aura khiên, banner theo phase |
| Audit đồng bộ (nhóm 1–3) | `215a317`, `90758ca`, `d8ade3c`, `a77d791`, `06724fa` | chặn race 4 người, snapshot effect đúng phạm vi, giảm băng thông (NetworkTransform 2D, HP quái 4 Hz), khoá di chuyển khi Root/Slow |
| Hiệu ứng chỉ host sinh ra | `ac86749`, `2b5ebd6`, `98a5341`, `46de7a2`, `92eb178`, `5a911a0`, `73a05f9`, `9c8de38`, `0fe0132` | Fox Mark/burn/tail, client dùng đúng prefab, biến thể optional trong replay, nổ ấn, vệt hạ gục, mảnh đất WallTitan, xung kích RedNova, hồi máu WaterB |
| Lobby tìm phòng | `d0ddd78` | `QueryEligibleRoomsAsync` + join theo danh sách/mã phòng/mã Relay |
| Vòng đời phiên & luật vào trận | `7ee0448`, `414b527`, `07ed265` | ghi loadout trước spawn, màn mất kết nối, **host phải bấm bắt đầu trận** |

---

## 7. Công cụ test và giới hạn hiện tại

- **Harness CLI** (`NetAutoConnect`, chỉ build DEVELOPMENT): `-iw-host`, `-iw-join <ip>`, `-iw-port`,
  `-iw-tank`, `-iw-opt1/-iw-opt2`, `-iw-ready`, `-iw-flow coop`, `-iw-relay-host`, `-iw-relay-join`,
  `-iw-lobby-join`, `-iw-lobby-query`, `-iw-godmode`, `-iw-autocast`, `-iw-autocast-interval`,
  `-iw-exit-after`, `-iw-netlog`, `-iw-selftest*`, `-iw-retry`.
- **Giới hạn đã biết**: chưa có reconnect (client nối lại nhận clientId mới), chưa có interest management,
  không lag compensation; `NetVisualSpawn` chỉ tái tạo được template là **con của một `NetworkObject` đã spawn**
  (không tái tạo được `new GameObject()` sinh lúc chạy); số tầng Fox Mark trên client chỉ vẽ 1 tầng.
- Lệnh kiểm tra bắt buộc trước khi bàn giao: `dotnet build Assembly-CSharp.csproj` → 0 error
  (5 warning có sẵn: CS0618 ×4 ở `Assets/Scripts/Enemies/SeauBearusBrain.cs:111` và
  `Assets/Scripts/Enemies/MeteorParasiteBrain.cs:119`, CS0414 ở `Assets/Scripts/Tanks/RedNova/RedNovaVfx.cs:38`).

---

## 8. Phạm vi loại trừ

- Không tính: màn hình chính (`Assets/Scripts/UI/*`, `Assets/Editor/MainMenuBuilder.cs`, `Assets/Art/UI/*`),
  scene `Assets/Scenes/MainMenu.unity`, panel lobby IMGUI (`Assets/Scripts/Net/NetLobbyUI.cs`) và HUD debug
  (`Assets/Scripts/Net/NetDebugHud.cs`) — chỉ nêu vai trò, không mô tả thiết kế giao diện.
- Không tính asset mỹ thuật/font và các file Unity tự sinh (`ProjectSettings/*.asset`, `Packages/packages-lock.json`).
