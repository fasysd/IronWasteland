# Iron Wasteland — Technical Design Document (TDD)

> **Trạng thái tài liệu:** Tổng hợp từ kiến trúc thực tế của codebase (`Assets/Scripts/`, `Assets/Editor/`, `Assets/Tests/`, `Packages/manifest.json`) nhằm chuẩn hóa quy ước kỹ thuật và khoanh vùng nợ kỹ thuật trước khi chỉnh sửa code.

---

## 1. Tech Stack & Tổ chức Assembly

### 1.1. Packages (`Packages/manifest.json`)
- **Networking:** `com.unity.netcode.gameobjects` (`2.14.0`), `com.unity.multiplayer.playmode` (`2.0.2`), `com.unity.multiplayer.center` (`1.0.1`).
  - *Lưu ý:* `com.unity.services.lobby` (`1.3.0`) và `com.unity.services.relay` (`1.2.0`) đang cài trong `manifest.json` nhưng **không sử dụng** (game dùng LAN UDP + Direct IP).
- **Rendering / Input / UI / Test:** `com.unity.render-pipelines.universal` (`17.6.0`), `com.unity.inputsystem` (`1.20.0`), `com.unity.ugui` (`2.5.0`), `com.unity.test-framework` (`1.7.0`), `com.ivanmurzak.unity.mcp` (`1.4.0`).

### 1.2. Phân tách Assembly (`.asmdef`)
| Assembly | Đường dẫn | Mục đích |
| :--- | :--- | :--- |
| `IronWasteland.UiModels` | `Assets/Scripts/UI/Models/` | Pure C# ViewModels (`noEngineReferences: true`), test độc lập không cần Unity runtime. |
| `IronWasteland.NetModels` | `Assets/Scripts/Net/Models/` | Pure C# LAN room/protocol models (`noEngineReferences: true`). |
| `IronWasteland.Tests.EditMode` | `Assets/Tests/EditMode/` | 14 bộ EditMode tests cho Models, Contracts và Scene/UI wiring. |
| `Assembly-CSharp` | `Assets/Scripts/` | Toàn bộ gameplay runtime (Core, Tanks, Enemies, Stages, Net, Save, Audio, UI). |
| `Assembly-CSharp-Editor` | `Assets/Editor/`, `Assets/Scripts/Editor/` | Các `*Builder.cs`, `UITheme.cs`, `StageDataValidator.cs`, `NetBuildRunner.cs`, `TuThanNguyenToSetupEditor.cs`. |

---

## 2. Scene Architecture & Build Settings

`ProjectSettings/EditorBuildSettings.asset` hiện bật **17 scenes**:
- **Menu & Meta (7):** `MainMenu`, `StageSelect`, `Garage`, `GarageAccessory`, `SkillPicker`, `Setting`, `CoopLobby`, `AccessorySalvage` *(tổng 8 meta scenes)*.
- **Campaign (9):** `Stage_1_1` → `Stage_1_4`, `Stage_2_1` → `Stage_2_5`.
- **Scene ngoài Build Settings (chỉ dev/test):** `Stage_Test`, `SkillArena`, `MainScene`.

**Quy ước chuyển scene:**
- **Single-player (từ `StageSelectController`):** Load trực tiếp scene `Stage_X_Y` (`SaveSystem.SceneNameFor(code)`). `GameController.Awake()` tự spawn Tank theo `SaveSystem.Instance.Settings.LastSelectedTankIndex`.
- **Co-op (từ `CoopLobbyController`):** Dùng custom scene sync của `MatchFlow` (tắt `NetworkManager.EnableSceneManagement`).
- **Garage từ `CoopLobby`:** Mở `Garage` theo mode **Additive** (`GarageFlow.OpenedFromLobby = true`), tắt tạm Camera/AudioListener của Lobby để không ngắt kết nối NGO, và đồng bộ lại `NetLoadout` khi đóng.

---

## 3. Core Combat, Stats & StatusEffect Pipeline

### 3.1. Stats (`Assets/Scripts/Tanks/TankStats.cs`, `TankDefinition.cs`)
- `TankDefinition` nội suy tuyến tính 8 chỉ số từ cấp `1..90`.
- `TankStats.CalculateFinalStats(baseStats, flatBonus, percentBonus)` áp dụng hard cap:
  - `MaxCooldownReduction = 0.60f`
  - `MaxArmorPenetration = 1.0f`
- Tốc độ di chuyển vật lý trong `TankBrain.FixedUpdate()`: `EffectiveMoveSpeed / 100f` (units/s).

### 3.2. Damage Pipeline (`Assets/Scripts/Core/CombatUtility.cs`, `DamageInfo.cs`, `TankBrain.cs`, `EnemyBrain.cs`)
- `DamageInfo` mang cờ: `ArmorPenetration`, `IsDirectToHealth`, `IsRedirected`, `IsDamageOverTime`, `DamageSourceType`.
- Luồng `TakeDamage(DamageInfo)`:
  1. `IDamageInterceptor` (`DamageInterceptEffect`, sắp xếp theo `RemainingTime` tăng dần).
  2. `IDamageRedirector` (bỏ qua nếu `IsRedirected` hoặc `IsDamageOverTime`; đòn chuyển hướng có `IsDirectToHealth = true`, `IsRedirected = true`).
  3. `EquipmentBrain.GetIncomingDamageMultiplier(info)` (bỏ qua nếu `IsDirectToHealth`).
  4. `CombatUtility.ResolveDamage` (4-segment log defense; bỏ qua nếu `IsDirectToHealth`).
  5. `ShieldEffect` (trừ khiên có `RemainingTime` ngắn nhất trước; bỏ qua nếu `IsDirectToHealth` hoặc `IsDamageOverTime`).
  6. Trừ HP còn lại.

### 3.3. Status Effects (`Assets/Scripts/Core/Effects/`)
- Base `StatusEffect` hỗ trợ `EffectStackPolicy` (`RefreshDuration`, `CapOnly`).
- Các implementation chuẩn: `ShieldEffect` (hỗ trợ `MaxAbsorbHits`), `DamageInterceptEffect`, `BurnEffect` (DoT `0.5s/tick`, `IsDirectToHealth = true`), `StatModifierEffect`, `EnemyAttackBuffEffect`.

---

## 4. Save System & Data Persistence (`Assets/Scripts/Save/`)

- **Lưu trữ:** JSON tại `<ProjectRoot|ExeDir>/save/gamedata.json` (hỗ trợ `-iw-save <profile>` cho Multiplayer Playmode và migrate từ `Application.persistentDataPath`).
- **Schema (`GameData`):**
  - `Gems` (`int`, mặc định `0`)
  - `Tanks` (`List<TankSaveData>`, 6 phần tử: `TankId 0..5`, `Level` [`0` = chưa mở khóa, `1..90` = đã mở], `EquippedAccessoryIndex` [`-1` = trống, 1 slot], `OptionSkill1`, `OptionSkill2`)
  - `Accessories` (`List<AccessorySaveData>`, mặc định **rỗng**)
  - `StageProgress` (`ClearedStageCodes`, `UnlockedStageCodes` mặc định `["1-1"]`, `BestTimes`)
  - `Settings` (`MasterVolume`, `SfxVolume`, `BgmVolume`, `AnnouncerVolume`, `LastSelectedTankIndex`)

---

## 5. Equipment / Phụ kiện (`Assets/Scripts/Tanks/Equipment/` & `Assets/Scripts/UI/`)

- **Tách 2 lớp dữ liệu:**
  - `EquipmentDatabase` (`ScriptableObject` gắn trên `TankDefinition`): giữ 20 prefab `EquipmentBrain` (`Accessory_00`..`Accessory_19`) với các lifecycle hooks (`OnEquipped`, `OnBrainTick`, `OnOwnerDealtDamage`, `OnOwnerTookDamage`, `OnOwnerUsedSkill`, `OnOwnerHealed`, `OnOwnerGainedEnergy`, `GetIncomingDamageMultiplier`, `OnOwnerSupportedAlly`).
  - `AccessoryCatalog` (`ScriptableObject` gắn trên UI): giữ metadata hiển thị (`DisplayName`, `Category`, `Role`, `Stats[]`, `Description`, `Icon`).
- **Quy tắc kho:**
  - Trang bị: `ConsumeAccessory(newId, 1)` (`-1`), trả cũ về kho `AddAccessory(oldId, 1)` (`+1`).
  - Tháo: `AddAccessory(oldId, 1)` (`+1`), đặt `EquippedAccessoryIndex = -1`.
  - Phân rã (`SalvageQuoteModel` + `AccessorySalvageController`): `Random.Range(8, 13)` Ngọc ma pháp mỗi món.

---

## 6. Stage, Wave & Enemy/Boss Architecture

### 6.1. Stage Coordination (`Assets/Scripts/Stages/`)
- `StageDefinition` (`ScriptableObject`): Metadata màn chơi, `rewardGems`, `rewardAccessoryCount`, `guaranteeAccessories`, `rewardAccessoryPool`, `nextStageCode`.
- `StageDirector`: Điều phối runtime chính của 9 ải (`StageObjectiveSpec`: `Eliminate`, `Survive`, `DestroyTargets`, `Escort`, `Radar`; đồng bộ `CoopMode` xuống `EnemySpawner`).
- `EnemySpawner` + `WaveTimeline`: Chạy wave theo thời gian, `RadarStation` (`EnemyWave.StartRadar`) hoặc `StageWaveGate`.
- `StageBounds`: Auto-bootstrap qua `RuntimeInitializeOnLoadMethod`, clamp Tank & Camera theo `Background` `SpriteRenderer` (hỗ trợ `SetOverrideBounds` cho sub-arena).
- *Lưu ý:* `StageManager.cs` là controller đời cũ vẫn còn tồn tại song song với `StageDirector.cs`.

### 6.2. Enemies & Bosses (`Assets/Scripts/Enemies/`)
- **Normal Enemies:** Đang có 2 thế hệ song song — script cũ ở `Assets/Scripts/Enemies/*.cs` và bộ refactored dùng `EnemyMovementAgent` + A* `PathfindingGrid2D` ở `Assets/Scripts/Enemies/Normal/*.cs`.
- **Elite Bosses (`EliteEnemyBrain`):** State machine `EliteBurstState` (`Inactive` → `Charging` → `Active` → `RewardPhase`/`PenaltyPhase` → `Finished`), miễn nhiễm `Root`/`Stun`.
- **Sub-Arenas & VFX:** `DeadlandPhantom` và `D1onk` dùng SubArena + VFXController riêng; `ElementalReaper` dùng SubArena + VFXController + `TuThanNguyenToSetupEditor` (1704 dòng); `SeauBearus` và `The2Face` vẽ VFX inline. `DeadlandPhantom`, `SeauBearus`, `The2Face` implement `INetVfxReceiver`; `ElementalReaper` và `D1onk` chưa implement.

---

## 7. Networking & LAN Co-op (`Assets/Scripts/Net/`)

- **`NetBootstrap` (`DontDestroyOnLoad`):** `TickRate = 20`, `EnableSceneManagement = false`, `ConnectionApproval = true` (giữ chỗ `m_ReservedClients` chống race condition đủ 4 người), bind port `7777` (fallback tối đa `10` port).
- **`LanDiscoveryService`:** UDP broadcast port `47777` mỗi `1s`, TTL `4.5s`.
- **`MatchFlow` (`MonoBehaviour` trên `NetworkManager` GO):** Dùng `CustomMessagingManager` (`"IW.Match"`, `"IW.MatchRequest"`, `"IW.Scene"`) để sync trạng thái phòng và load scene đồng bộ (chờ `OpSceneLoaded` tối đa `10s`).
- **`NetLoadout`:** Struct 17-byte (`TankIndex`, `Level`, `EquipmentId`, `OptionSkill1`, `OptionSkill2`) gửi qua `ConnectionData` và `NetworkVariable`.
- **`PlayerNetState` + `NetPlayerController`:**
  - *Owner-auth:* Movement (`NetworkTransform`) + Aim (15 Hz), có server anti-cheat validation (`speedTolerance = 1.6f`, teleport grace).
  - *Server-auth:* HP, Energy, Cooldowns (`CooldownState`), Stats, CC (`m_SlowSpeedFactor`, `m_MovementBlocked`, `m_LockedSlotMask`), Shield (10 Hz).
- **`EnemyNetState`:** Server-auth AI/physics (`EnemyBrain.IsServerSimulated`); client chuyển `Rigidbody2D` sang `Kinematic` và sync `Health`/`MaxHealth`/`Yaw` + `ClientRpc` VFX/Anim.
- **`NetStageEvents` & `NetSkillFx`:** Channel `"IW.StageEvent"` (kèm `m_MatchId` chống gói tin cũ) và `"IW.SkillFx*"`.

---

## 8. Audio & Editor Tooling

- **Audio (`Assets/Scripts/Audio/`):** `AudioManager` (`DontDestroyOnLoad`, pool `12` `AudioSource`) + `SfxLibrary` (`13` `SfxCategory`) + các hook decoupled (`StageSfxHooks`, `EnemySfxHooks`, `TankSfxHooks`).
- **Editor Builders (`Assets/Editor/`):** Toàn bộ UI scenes được dựng bằng script (`*Builder.cs` + `UITheme.cs` chuẩn `1920x1080`) và kiểm tra bằng `StageDataValidator.ValidateAll`.

---

## 9. Nợ kỹ thuật & Rủi ro kiến trúc cần xử lý (Chờ xác nhận)

| Nhóm | Vấn đề cụ thể trong codebase | Mức độ |
| :--- | :--- | :---: |
| **Co-op / Scene** | `StageHUD.OnNextClicked()` gọi `SceneManager.LoadScene` trực tiếp thay vì qua `MatchFlow` → dễ lệch/rớt client khi qua ải trong Co-op. | Cao |
| **Save / Unlock** | `SaveSystem.EnsureTanksInitialized()` gán nhầm `Level = 1` cho tank thiếu (đáng lẽ `Level = 0` với index `2..5`). `SaveSystem.UnlockTank()` chưa có caller. | Cao |
| **Salvage** | `AccessorySalvageController` chặn +/-/MAX khi `IsEquipped` dù `SalvageQuoteModel` cho phép và `ConfirmSalvage` ở qty=1 không chặn; `AccessoryCatalog.Entry.SalvageValue = 5` là field chết. | Trung bình |
| **Garage UI** | `GarageController.TryUpgrade()` gọi `SpendGems`+`SetTankLevel` thay vì `UpgradeTank()`; `ApplyStats()` chưa cộng chỉ số từ phụ kiện đang lắp. | Trung bình |
| **Editor/Runtime** | `Assets/Scripts/Tests/*.cs` (test/probe Editor-only) nằm trong thư mục runtime; nhiều script runtime lẫn `#if UNITY_EDITOR`; `Assets/Editor/` chưa có `.asmdef`. | Trung bình |
| **Legacy / Trùng lặp** | `StageManager.cs` song song `StageDirector.cs`; bộ enemy cũ song song `Enemies/Normal/`; `Stage_Test`/`Stage_01`/`Stage_02` còn sót; `com.unity.services.lobby/relay` cài nhưng không dùng; `ElementalReaper` & `D1onk` thiếu `INetVfxReceiver`. | Thấp–TB |
| **Input & Settings** | Nhiều màn UI vừa dùng UGUI `Button` vừa poll `OnGUI`; `StageHUD` chỉnh `AudioManager` trực tiếp không qua `SaveSystem` như `SettingController`. | Thấp |
