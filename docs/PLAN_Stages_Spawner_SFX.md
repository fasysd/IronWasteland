# KẾ HOẠCH v2 — 9 MÀN CHƠI + SPAWNER QUÁI + HỆ THỐNG SFX

> **Phiên bản:** v2 — viết lại ngày **15/11/2026** sau khi repo được đồng bộ về `origin/main`
> **Baseline code:** commit `e82ee94` — *"Cập nhật: WaterB và GreenFox chuyển sang dùng ColliderOwnerRef"* (09/11)
> **Trạng thái build:** ✅ compile được — `Library/ScriptAssemblies/Assembly-CSharp.dll` 587.264 B, 0 lỗi `error CS`
> **Người thực hiện:** (điền tên) · **Review:** nhóm trưởng Hùng
> **Thay thế hoàn toàn:** `PLAN_Stages_Spawner_SFX.md` bản v1 (dựa trên code local cũ đã bị reset)

---

## 0. Tóm tắt nhanh (TL;DR)

Cần làm **3 khối việc**, mỗi màn là **1 scene riêng** trong `Assets/Scenes/`:

1. **Khung màn chơi** — `StageDefinition` v2 (điều kiện thắng/thua dạng module) + `StageDirector` + `StageObjective` (5 loại) + `EnemySpawner` sinh quái theo `SpawnZone` (thay vì đặt sẵn trên map).
2. **Tank từ dữ liệu** — **GIỮ NGUYÊN `GameController`** (Q1 đã chốt: chưa refactor, để người khác làm). Mỗi scene màn đặt 1 `GameController`, cấu hình 6 field (tank, cấp, phụ kiện, 2 kỹ năng tuỳ chọn) → nó tự gọi `TankDefinition.CreateTank(context)`. `StageDirector` chỉ đọc `GameController.PlayerTank` (đã có sẵn — `GameController.cs:43`).
3. **SFX dùng chung** — `SfxLibrary` (ScriptableObject) + `AudioManager` (pool AudioSource) + `SfxEmitter` (gắn lên tank/quái, nghe event của brain). **15 nhóm âm thanh × 2–3 biến thể**, phát ngẫu nhiên, chống lặp.

> **Màn chọn ải `StageSelect` — ✅ dựng lại theo wireframe 02 (17/11/2026):**
> `Assets/Scripts/UI/StageSelectUI.cs` viết lại (~380 dòng): tiêu đề `CHỌN MÀN CHƠI • CHIẾN DỊCH GIẢI CỨU ĐỊA CẦU`, nút `< VỀ SẢNH` góc trên-trái, chia **2 chương** (`CHƯƠNG 1: LỤC ĐỊA ĐEN`, `CHƯƠNG 2: VÙNG ĐẤT NGUYÊN TỐ`), thẻ màn **3 cột** hiện `ẢI 1-1` + tên + `N đợt quái • Độ khó: …` + nhãn trạng thái (`ĐÃ VƯỢT QUA` xanh lá / `SẴN SÀNG` vàng / `KHÓA` xám), thẻ boss nền đỏ (`ẢI 1-4`, `ẢI 2-4`, `ĐẠI BOSS ẢI 2-5`). Thẻ sinh bằng code từ danh sách `stages` nên thêm màn chỉ cần kéo asset vào Inspector; bấm thẻ → `StageProgress.SceneNameFor` + `LoadScene`.
> `StageProgress.IsUnlocked` sửa để **màn đã thắng luôn được coi là đã mở** (trước đây màn đã thắng vẫn hiện `KHÓA` nếu cờ unlock bị thiếu).
> Ảnh kiểm chứng: `docs/screenshots/stage_select.png`.

**Khối lượng:** ~**78–104 giờ** cho phần làm được ngay (P0→P5 + P7 + StageSelect). **Phần boss (P6: màn 1-4, 2-4, 2-5) tạm hoãn** vì chưa có người viết boss brain (Q4) ⇒ cộng thêm ~24–30 h khi có người.

**Tin tốt so với bản v1:** dữ liệu tank đã đúng, quy ước tốc độ đã có trong code ⇒ **Phase 0 co lại từ ~12 h xuống ~5 h**, không phải migrate lại 6 asset.

### 0.1 Đã thay đổi gì so với bản plan v1

| # | Kết luận ở bản v1 | Thực tế sau khi đồng bộ `origin/main` | Ảnh hưởng |
|---|---|---|---|
| 1 | 3 asset `TankDefinition` (BlackShadow, GreenFox, WaterB) còn schema cũ `statsTable`, thiếu `baseStatsLevel1/90` | **SAI.** Cả 6 asset đã migrate đúng, `statsTable` không còn tồn tại | **Bỏ** hạng mục migrate dữ liệu ở Phase 0 |
| 2 | `Leviathan.MoveSpeed = 100` sai đơn vị, phải đổi thành 5 (÷20) | **SAI.** `TankBrain.cs:918` có `speed = m_FinalStats.MoveSpeed / 100f` ⇒ **100 là đúng**, ÷20 sẽ làm xe chạy chậm 5 lần | **Bỏ** hạng mục sửa tốc độ; đổi lại là **cập nhật tài liệu** §10.7 |
| 3 | Chưa rõ bộ prefab quái nào chuẩn | Vẫn còn 2 bộ song song, nhưng `Normal/` (12 prefab) là bộ **có đủ brain + skill + A\***; `ChapterOne/` (7 prefab) chỉ còn cho boss cũ | Giữ nguyên blocker B4 |
| 4 | 5 prefab boss gắn `EliteEnemyBrain_Test` "rỗng" | Chính xác hơn: `EliteEnemyBrain_Test : EliteEnemyBrain` (đúng lớp cha, **có** cơ chế Mạch Năng Lượng) nhưng **không có AI, không có `EnemyMovementAgent`** ⇒ boss đứng im, không đánh | Đổi mô tả blocker B2, khối lượng P6 tăng nhẹ |
| 5 | (chưa biết) | **Mới phát hiện:** 3 prefab tank (`BlackShadow`, `RedNova`, `Leviathan`) **lỗi import**: `Transform child has another parent` | Thêm blocker B3 — chặn spawn 3/6 tank |
| 6 | (chưa biết) | `EquipmentDatabase.asset` **vẫn** schema cũ ⇒ `accessoryPrefabs` rỗng | Giữ blocker B1, thêm phase sửa |

---

### 0.2 Quyết định đã chốt (16/11/2026)

| # | Câu hỏi | **Quyết định** | Hệ quả lên kế hoạch |
|---|---|---|---|
| **Q1** | Có thay `GameController` bằng `PlayerLoadout`/`PlayerTankSpawner` không? | ❌ **CHƯA THAY** — để người khác làm sau | Không làm `PlayerLoadout`, `PlayerTankSpawner`, `PlayerInputController` đợt này. Mỗi scene đặt 1 `GameController`. `StageDirector` đọc `GameController.PlayerTank` |
| **Q2** | Vật thể hộ tống/phá được dùng interface hay kế thừa `EnemyBrain`? | ✅ **KẾ THỪA `EnemyBrain`** | 0 file skill phải sửa; `DestructibleObjective : EnemyBrain` (§3.5) |
| **Q3** | Prefab **The2Face** (chưa có) | ⏸ **CHƯA LÀM** | Màn 2-4 chưa hoàn thiện được ⇒ nằm trong P6 (tạm hoãn) |
| **Q4** | Ai viết 5 boss brain? | ⏸ **CHƯA CÓ NGƯỜI** | **P6 tạm hoãn**; 1-4 / 2-4 / 2-5 để sau |
| **Q5** | Co-op có làm đợt này? | ❌ **KHÔNG** | Chỉ chừa `bool isCoop` trong `StageDirector`; không bật `SetCoopMode` |
| **Q6** | Tên scene | ✅ **`Stage_X_Y`** | `Stage_1_1` … `Stage_2_5` |
| **Q7** | Có màn chọn ải `StageSelect`? | ✅ **CÓ** | Thêm `StageSelect.unity` + `StageProgress` + `StageSelectUI` (Phase 2) |

**Phạm vi thực tế đợt này:** 6 màn thường (**1-1, 1-2, 1-3, 2-1, 2-2, 2-3**) + framework đầy đủ + SFX + StageSelect. 3 màn boss **để backlog**.

## 1. Hiện trạng code — bằng chứng cụ thể

### 1.1 Đã có, tái sử dụng được

| Thành phần | File | Ghi chú |
|---|---|---|
| Tạo tank từ dữ liệu | `Assets/Scripts/Tanks/TankDefinition.cs:52` `CreateTank(TankBrainContext)` | Đã đúng: instantiate brain → `Initialize(context, GetStatsForLevel(level))` → `SetViewPrefab` → `EquipAccessory` |
| Nội suy chỉ số theo cấp | `TankDefinition.cs:80` `GetStatsForLevel(int)` | Nội suy tuyến tính Level 1 → 90, clamp CDR/PEN |
| Ngữ cảnh tank | `Assets/Scripts/Tanks/TankBrainContext.cs` | `struct { Level, IdView, EquipmentId, OptionSkill1, OptionSkill2 }` |
| Brain tank | `Assets/Scripts/Tanks/TankBrain.cs` (1020 dòng) | `Initialize` L148, `SetMoveInput` L579, `SetLookTarget` L584, `UseSkill` L607 |
| Event tank (hook SFX) | `TankBrain.cs:85-101` | `OnTakeDamageEvent`, `OnDealDamageEvent`, `OnHealEvent`, `OnSkillUsedEvent`, `OnShieldChangedEvent`, `OnShieldAbsorbedEvent`, `OnCrowdControlApplied/Removed`, `OnEffectAppliedEvent/RemovedEvent`, `OnDeathEvent` |
| Brain quái | `Assets/Scripts/Enemies/EnemyBrain.cs` (565 dòng) | `Initialize(EnemyStats)` L99, `SetCoopMode(bool)` L116, `TakeDamage(DamageInfo)` L178 |
| Event quái (hook SFX) | `EnemyBrain.cs:48-56` | `OnTakeDamageEvent`, `OnDealDamageEvent`, `OnHealEvent`, `OnShieldChangedEvent`, CC + effect + `OnDeathEvent` |
| Boss nền (Mạch Năng Lượng) | `Assets/Scripts/Enemies/EliteEnemyBrain.cs` (267 dòng) | `Cores` L77, `EnergyCoreCount` L82, `BreakCore(int)` L129, `AllCoresBroken` L79, `IsElite => true` L188 |
| Di chuyển quái | `Assets/Scripts/Enemies/Navigation/EnemyMovementAgent.cs` (309 dòng) | `SetSpeed`, `SetPath`, `Stop`, `Teleport`, tự tách đàn |
| Tìm đường A\* | `Assets/Scripts/Enemies/Navigation/PathfindingGrid2D.cs` (636 dòng) | `FindPathToRadiusA`, `FindPathToPoint`, `[ContextMenu("Bake / Rebuild Grid")]`, node 0.5 |
| Chủ sở hữu collider | `Assets/Scripts/Core/ColliderOwnerRef.cs` (451 dòng) | `GetTriggerColliders()` L251, `TryGetOtherOwner<T>` L338, `FromCollider` L435 |
| Text sát thương (mẫu pooling) | `Assets/Scripts/Core/TextDamageManager.cs` (346 dòng) | `Show(float, Vector3)`, pool prewarm 20 / max 64 — **dùng làm khuôn cho `EnemySpawner`** |
| Khung màn chơi v1 | `Assets/Scripts/Stages/StageManager.cs` (320 dòng) · `StageDefinition.cs` (96 dòng) | Có `StartStage()` L123, event `WaveStarted/KillsChanged/StageCleared/StageFailed` L51-54, `SpawnEnemy` L224 (đã `.Clone()` stats đúng) |
| Input tank | `Assets/Scripts/Tanks/GameController.cs` (149 dòng) | WASD di chuyển, chuột ngắm, **phím 1–4 chọn skill** (L74-113), `PlayerTank` L43 |

### 1.2 Thiếu hoàn toàn — phải làm mới

| Thiếu | Bằng chứng (grep toàn `Assets/Scripts`) |
|---|---|
| **Spawner / SpawnZone** | 0 kết quả `class *Spawn*` (chỉ có data class `StageDefinition.EnemySpawn`) |
| **Camera follow** | `CameraFollow` = 0 hit, `Cinemachine` = 0 hit (package cũng không cài) |
| **HUD / UI** | `class *HUD` = 0, `Canvas` = 0 (dự án **cấm** UI Toolkit — chỉ uGUI + TMP) |
| **Chuyển scene / flow** | `SceneManager.LoadScene` = 0 hit |
| **Lưu tiến trình** | `PlayerPrefs` = 0, `SaveSystem` = 0 |
| **Âm thanh** | `*.wav/*.mp3/*.ogg` = **0 file**, không có thư mục `Audio/`, không `AudioManager` |
| **Điều kiện thắng phức tạp** | `StageManager.cs:271-280` chỉ có 2 nhánh: `Kills >= KillTarget` hoặc tank chết |
| **Asset màn chơi** | Không có `StageDefinition` asset nào; 0 scene màn (chỉ `SampleScene`, `MainScene`) |

### 1.3 "Thông tin Tank" hiện đang hoạt động thế nào (cực kỳ quan trọng)

Đây chính là cái mà nhiệm vụ yêu cầu tái sử dụng — **nhưng phải tách khỏi scene**:

```
GameController (MonoBehaviour, gắn tay trong scene)
  ├─ [SerializeField] TankDefinition tankDefinition      ← asset dữ liệu tank
  ├─ [SerializeField] int playerLevel  (1..90)
  ├─ [SerializeField] int equipmentId  (-1 = không có)
  ├─ [SerializeField] int optionSkill1, optionSkill2     ← chọn trong pool 3 kỹ năng
  └─ Awake()
       → new TankBrainContext { Level, IdView=0, EquipmentId, OptionSkill1, OptionSkill2 }
       → tankDefinition.CreateTank(context)
       → đặt transform.position = spawnPosition
  └─ Update()   ← ĐỌC INPUT TRỰC TIẾP (đây là chỗ cần tách ra khi làm màn)
       → SetMoveInput / SetLookTarget / chọn skill bằng phím 1–4 / bắn bằng chuột
```

**Vì sao về lâu dài nên tách** (nhưng **đợt này chưa làm — Q1**): nếu mỗi scene màn đặt tay một `GameController` thì dữ liệu tank nằm rải rác 6 nơi, không mang tank từ màn này sang màn khác được. Giải pháp đầy đủ vẫn giữ ở §3.4 để người làm sau dùng, nhưng **đợt này chỉ cần**: mỗi `Stage_X_Y` đặt 1 `GameController` cấu hình đúng, và `StageDirector` đọc `PlayerTank` — việc mở khoá tank do `StageProgress` + `StageSelect` đảm nhiệm.

### 1.4 Quy ước tốc độ — ĐÃ CHỐT trong code (không cần sửa gì)

```csharp
// Assets/Scripts/Tanks/TankBrain.cs:918
float speed = m_FinalStats.MoveSpeed / 100f;
```

- Bảng GDD §10.1 dùng **thang chuẩn 100**; `TankDefinition` asset lưu **số thô GDD** (100, 110, 130…) ⇒ runtime chia 100 ⇒ **1,0 – 1,3 đơn vị/giây**.
- Quái dùng **đúng quy ước này**: `NormalEnemy_BaseChaser.cs:114,142,171` → `movementAgent.SetSpeed(Stats.MoveSpeed / 100f)`.
- ⚠️ **Hệ quả cho người làm màn:** `EnemySpawn.SpeedMultiplier` nhân vào `EnemyStats.MoveSpeed` (thang thô) — đúng chỗ, không cần đổi.
- 📌 **Việc cần làm:** `docs/PROJECT_CONTEXT.md` §10.7 dòng 2 và dòng 7 đang ghi "cần chốt hệ số" / "asset chưa migrate" — **đã lỗi thời**, nên sửa lại (5 phút, xem P0).

---

## 2. Blocker — phải xử lý trước khi dựng màn

### B1. `EquipmentDatabase.asset` sai schema ⇒ không gắn được phụ kiện  *(chặn: phụ kiện, thưởng màn)*

`Assets/Settings/EquipmentDatabase.asset` vẫn serialize theo field cũ:

```yaml
firepowerPrefabs: [{...Firepower_Cannon.prefab}]
defensePrefabs:   [{...Defense_Heavy.prefab}]
mobilityPrefabs:  [{...Mobility_Heavy.prefab}]
```

Trong khi `Assets/Scripts/Tanks/Equipment/EquipmentDatabase.cs` chỉ đọc `List<EquipmentBrain> accessoryPrefabs` ⇒ danh sách **rỗng** ⇒ `GetPrefab(id)` luôn trả `null` ⇒ `TankDefinition.CreateTank` bỏ qua bước gắn phụ kiện, không báo lỗi.

**Cách sửa (5 phút):** mở asset trong Inspector → kéo 3 prefab trong `Assets/Prefabs/Equipment/` vào list `accessoryPrefabs` (đúng thứ tự id 0,1,2) → Ctrl+S. Unity sẽ tự xoá 3 field cũ khỏi YAML.

### B2. Boss chưa có não gameplay  *(chặn: màn 1-4, 2-4, 2-5)*

5 prefab boss (`BongMaTuDia`, `SEAUBEARUS`, `TuThanNguyenTo`, `TruongQuanNgucD1ONK `, `Enemy_Elite`) đều gắn **`EliteEnemyBrain_Test`** — lớp này chỉ có:

```csharp
public class EliteEnemyBrain_Test : EliteEnemyBrain   // Assets/Scripts/Enemies/EliteEnemyBrain_Test.cs
{
    protected override void OnDeath() { Destroy(this.gameObject); }
}
```

Điểm tốt: kế thừa **đúng** `EliteEnemyBrain` ⇒ đã có sẵn Mạch Năng Lượng (`energyCoreCount`, `BreakCore`, `AllCoresBroken`).
Điểm chặn: **không override `OnBrainTick`** và prefab **không có `EnemyMovementAgent`** ⇒ boss đứng yên, không tấn công, không có kỹ năng.

**Cần:** 1 lớp `BossBrainBase` (di chuyển + vòng lặp kỹ năng + 2–3 kỹ năng/boss) và 5 lớp con mỏng. Xem §3.6.

### B3. 3 prefab tank lỗi import  *(chặn: spawn BlackShadow / RedNova / Leviathan)*

Console Unity (lặp lại mỗi lần import):

```
Problem detected while importing the Prefab file:
'Assets/Prefabs/Tanks/BlackShadow/BlackShadow.prefab'
'Assets/Prefabs/Tanks/RedNova/RedNova.prefab'
'Assets/Prefabs/Tanks/Leviathan/Leviathan.prefab'
Errors:  Transform child has another parent
```

Nguyên nhân: commit *"Loại bỏ TankView Prefabs asset"* đã xoá `*_View.prefab` nhưng prefab brain vẫn còn cấu trúc con trỏ vào instance đã mất ⇒ cây Transform không hợp lệ. Prefab vẫn load được nhưng **hierarchy con bị lệch**, `TankView` không tìm thấy `Muzzle` ⇒ skill bắn ra sai vị trí.

**Cách sửa (30–45 phút):** mở từng prefab → kiểm tra node con trùng parent → xoá node mồ côi hoặc kéo lại vào đúng cha → Apply. *(Nếu chưa sửa được ngay: 3 tank này tạm không dùng trong 9 màn, chỉ test bằng GreenFox / WallTitan / WaterB.)*

### B4. Chưa chốt "bộ prefab quái chuẩn" (2 bộ song song)

| Bộ | Nội dung | Đánh giá |
|---|---|---|
| `Assets/Prefabs/Enemies/Normal/` | 12 prefab, có `NormalEnemy_*` brain + `EnemyMovementAgent` + `ColliderOwnerRef` + skill riêng | ✅ **Dùng bộ này** — khớp đủ 12 loại quái trong GDD |
| `Assets/Prefabs/Enemies/ChapterOne/` | 7 prefab, brain kiểu `ChasingEnemyBrain` cũ | ❌ Không dùng cho 9 màn; chỉ giữ cho scene test cũ |

### B5. Không có audio asset

- 0 file `.wav/.mp3/.ogg` trong toàn project.
- Không có `AudioManager`, `SfxLibrary`, không `AudioSource` nào được cấu hình sẵn.
- `Packages/manifest.json` **có** `com.unity.modules.audio` ⇒ chỉ cần thêm file + code.

**Cần:** tải ~38 clip CC0 (§5.2), import đúng thiết lập (§5.3), rồi code 3 lớp audio (§3.8).

### B6. Thiếu khung tối thiểu để "chơi được" một màn  *(chặn: mọi màn)*

Camera không follow (map rộng hơn màn hình là mất dấu tank), không HUD (không biết máu/còn bao nhiêu quái), không chuyển scene (không nối được 9 màn). Xem §3.7.

### B7. `StageDefinition` v1 quá mỏng  *(chặn: 6/9 màn)*

Hiện chỉ có: `stageCode`, `stageName`, `killTarget`, `timeLimitSeconds`, `List<Wave>` (mỗi wave có `StartTime` + danh sách `EnemySpawn`).

Thiếu: **loại điều kiện thắng**, phase boss, spawn zone, mục tiêu phá được, xe hộ tống, trạm radar, thưởng cuối màn. Xem §3.1 (schema v2).

---

## 3. Kiến trúc đề xuất

### 3.0 Luồng khi vào một scene màn

```
[Scene Stage_1_1 load]
   │
   ├─(1) StageDirector.Awake()
   │      ├─ đọc StageDefinition asset (gán trong Inspector)
   │      └─ build danh sách StageObjective từ definition
   │
   ├─(2) GameController.Awake()          ← GIỮ NGUYÊN (Q1)
   │      ├─ đọc 6 field trong Inspector (tank/cấp/phụ kiện/2 skill/spawnPoint)
   │      └─ tankDefinition.CreateTank(context)  → tank xuất hiện tại spawnPosition
   │
   ├─(3) CameraFollow2D.LateUpdate()  → bám tank
   │
   ├─(4) StageDirector.Start()
   │      ├─ StageEvents.OnStageStarted
   │      ├─ bật StageHUD
   │      └─ StageDefinition.phases[0] → bật các SpawnZone + objective
   │
   ├─(5) EnemySpawner (mỗi SpawnZone một cái)  → Instantiate quái theo sóng
   │
   └─(6) StageDirector.Update()
          ├─ cập nhật objective
          ├─ khi tất cả objective "satisfied" → StageCleared → trao thưởng → (sau 3 s) load màn kế
          └─ khi objective "failed" (tank chết / xe hộ tống bị phá) → StageFailed
```

### 3.1 Data layer — `StageDefinition` v2

Giữ nguyên tên file cũ (`StageDefinition.cs`), **mở rộng** thay vì viết mới, để `StageManager` cũ không chết ngay (có thể xoá `StageManager` sau khi `StageDirector` chạy ổn).

```csharp
[CreateAssetMenu(menuName = "Iron Wasteland/Stage/Stage Definition", fileName = "Stage_1_1")]
public class StageDefinition : ScriptableObject
{
    [Header("Thông tin")]
    public string stageCode = "1-1";
    public string stageName;
    public string chapter;                     // "Ch.1" / "Ch.2"
    public int rewardGems = 20;                // §10.4
    public int guaranteedAccessories = 1;      // 1 / 2 / 3
    public string unlockTankId;                // "BlackShadow" | null

    [Header("Thời lượng")]
    public float timeLimitSeconds;             // 0 = không giới hạn

    [Header("Điều kiện thắng (tất cả phải xong)")]
    public List<StageObjectiveData> objectives = new();

    [Header("Phase (màn boss dùng; màn thường để trống = 1 phase)")]
    public List<StagePhaseData> phases = new();

    [Header("Audio")]
    public AudioClip musicOverride;            // null = dùng nhạc chung
}

[Serializable] public class StageObjectiveData
{
    public ObjectiveType type;                 // Eliminate | Survive | DestroyTargets | Escort | ActivateRadar | KillBoss
    public int targetAmount = 40;              // 40 quái / 100 giây / 4 cụm / 150 mét ...
    public string zoneId;                      // nối với SpawnZone.id
    public string failReason = "Tank bị phá huỷ";
}

[Serializable] public class StagePhaseData
{
    public string label = "Phase 1";
    public List<StageWaveData> waves = new();
    public float durationSeconds;              // 0 = chuyển phase khi wave xong
    public bool waitUntilEnemiesCleared = true;
}

[Serializable] public class StageWaveData
{
    public string label = "Wave 1";
    public float startTime;                    // giây, tính từ đầu phase
    public string spawnZoneId;                 // rỗng = dùng zone mặc định của phase
    public List<EnemySpawnData> spawns = new();
}
```

`EnemySpawnData` = bản mở rộng của `StageDefinition.EnemySpawn` hiện tại:

```csharp
[Serializable] public class EnemySpawnData
{
    public EnemyBrain prefab;                  // Assets/Prefabs/Enemies/Normal/*
    [Min(1)] public int count = 1;
    [Min(0.01f)] public float speedMultiplier = 1f;   // §1.4: nhân vào thang GDD thô
    [Range(0f,1f)] public float coopHealthBonus = 1f; // 1 = dùng SetCoopMode mặc định (×2)
}
```

> **Vì sao `zoneId` là string chứ không phải reference:** 9 scene × nhiều zone; dùng string để copy asset giữa scene không gãy, và để đọc bằng mắt trong Inspector. `SpawnZone` trong scene có `public string id;` trùng tên.

### 3.2 Object layer trong scene (đặt tay trên map)

```
Stage_1_1 (scene)
├── StageDirector            (StageDefinition = Stage_1_1.asset)
├── GameController           (giữ nguyên — Q1: cấu hình 6 field trong Inspector)
├── Main Camera              (+ CameraFollow2D, target = spawnPoint)
├── Canvas (HUD)             (+ StageHUD, + TMP)
├── Environment
│   ├── Terrain (Tilemap / Sprite, layer 8 = Terrain, có Collider2D)   ← bắt buộc để A* bake
│   └── ArenaBounds (BoxCollider2D, layer 8)
├── SpawnZones
│   ├── Zone_North           (SpawnZone id="north")
│   ├── Zone_South           (SpawnZone id="south")
│   └── Zone_East            (SpawnZone id="east")
└── Objectives               (chỉ màn đặc biệt)
    ├── RadarStation_1       (1-3)
    ├── EscortVehicle_Mones  (1-3, 2-3)
    ├── NestMother_1..4      (2-2)
    └── FieldPylon_1..3      (2-3)
```

**Quy tắc:** mọi thứ **có thể chết/đổi trạng thái** phải là prefab được spawner sinh ra hoặc component có event — không hard-code trong `StageDirector`.

### 3.3 `SpawnZone` + `EnemySpawner` — sinh quái thay vì đặt sẵn

```csharp
public class SpawnZone : MonoBehaviour
{
    public string id = "north";
    [Tooltip("Bán kính rải quái quanh tâm zone (m).")]
    public float radius = 3f;
    public Vector2 RandomPoint();          // điểm ngẫu nhiên trong bán kính
    public bool Contains(Vector2 world);
}
```

`EnemySpawner` **một instance / một scene** (không phải mỗi quái một spawner), quản lý pool:

```csharp
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private Transform container;          // "~Enemies"
    [SerializeField] private int prewarmPerPrefab = 4;      // Rule 4: không Instantiate lúc đánh nhau
    [SerializeField] private int maxAlive = 60;             // chặn tràn màn hình

    public EnemyBrain Spawn(EnemyBrain prefab, Vector3 position, EnemySpawnData cfg);
    public void Despawn(EnemyBrain enemy);                  // SetActive(false) + trả về pool
}
```

**Hai bẫy bắt buộc xử lý (đã kiểm trong code):**

1. `EnemyBrain.Initialize(EnemyStats overrideStats)` **gán theo tham chiếu**: `stats = overrideStats`. Phải `Clone()` **mỗi lần spawn** — xem `StageManager.cs:230` làm mẫu đúng:
   ```csharp
   EnemyStats stats = prefab.Stats != null ? prefab.Stats.Clone() : new EnemyStats();
   stats.MoveSpeed *= cfg.speedMultiplier;
   enemy.Initialize(stats);
   ```
2. `SetCoopMode(true)` nhân `stats.MaxHealth *= 2f` **trên instance đang dùng**. Nếu spawn từ pool mà gọi lại mỗi lần respawn thì HP sẽ ×2, ×4, ×8… ⇒ chỉ gọi `SetCoopMode` **một lần khi tạo instance trong pool**, và `Clone()` từ `prefab.Stats` gốc mỗi lần respawn.

**Không dùng `Instantiate/Destroy` trong lúc chiến đấu** (Iron Rule 4 — `docs/PROJECT_CONTEXT.md` §3). `Despawn` = unsubscribe event → `SetActive(false)` → đẩy về pool.

### 3.4 Tank trong màn — **GIỮ `GameController`** (Q1), kèm phần lưu tiến trình cho `StageSelect`

> **Đã chốt (Q1):** đợt này **không** thay `GameController`, không làm `PlayerLoadout`/`PlayerTankSpawner`/`PlayerInputController`. Người khác sẽ làm sau. Đoạn dưới đây là **thiết kế để dành** (backlog) + phần **làm ngay** là `StageProgress`.

**Làm ngay — cách mỗi scene màn lấy tank:**

```
Stage_1_1 (scene)
├── GameController      ← giữ nguyên, chỉ cấu hình 6 field trong Inspector:
│      tankDefinition = Assets/Prefabs/Tanks/BlackShadow/BlackShadow.asset
│      playerLevel    = 1
│      equipmentId    = -1
│      optionSkill1   = 0
│      optionSkill2   = 0
│      spawnPosition  = vị trí xuất phát của màn
└── StageDirector       ← Awake(): playerTank = gameController.PlayerTank   (GameController.cs:43)
```

`GameController` đã tự gọi `TankDefinition.CreateTank(context)` trong `Awake()` (`GameController.cs:46`) và đọc input trong `Update()` (`GameController.cs:74-113`: WASD, chuột ngắm, phím 1–4 chọn skill). **Không cần viết thêm dòng code nào cho việc tạo tank.**

**Làm ngay — `StageProgress` (thay cho save system, vì dự án chưa có):**

```csharp
public static class StageProgress
{
    private const string KeyUnlocked = "iw_unlocked_";     // iw_unlocked_1_1 = 1
    private const string KeyGems     = "iw_gems";
    public static bool IsUnlocked(string stageCode);       // 1-1 luôn true
    public static void MarkCleared(string stageCode);      // mở màn kế + lưu
    public static int Gems { get; }                        // tổng ngọc
    public static void AddGems(int amount);
    public static void ResetAll();                          // nút debug
}
```

Dùng `PlayerPrefs` — đủ cho demo 6 màn, sau này thay bằng save file thật chỉ cần sửa 1 file.

**Để dành cho người làm sau (backlog — không làm đợt này):**

```csharp
// backlog: Assets/Scripts/Tanks/PlayerLoadout.cs
[CreateAssetMenu(menuName = "Iron Wasteland/Player/Loadout")]
public class PlayerLoadout : ScriptableObject
{
    public TankDefinition tank;
    [Range(1,90)] public int level = 1;
    public int equipmentId = -1, optionSkill1, optionSkill2;
    public TankBrainContext ToContext() => new TankBrainContext(level, equipmentId, optionSkill1, optionSkill2);
}
```

Khi có người làm: bỏ `GameController` khỏi scene, thay bằng `PlayerTankSpawner` (2 dòng gọi `loadout.tank.CreateTank(loadout.ToContext())`), và `StageDirector.playerTank` lấy từ `PlayerTankSpawner.Spawned` thay vì `GameController.PlayerTank`. **Không phải sửa gì khác trong framework màn chơi** — đó là lý do `StageDirector` chỉ giữ 1 tham chiếu `TankBrain`.

### 3.5 Vật thể mục tiêu: phá được / hộ tống / radar

Cả 3 đều cần **"nhận sát thương từ tank"** nhưng **không phải `EnemyBrain`**. Hai lựa chọn:

| Cách | Ưu | Nhược |
|---|---|---|
| **A. `DestructibleObjective : MonoBehaviour` + tự xử lý `TakeDamage(DamageInfo)`** | Không đụng code tank; có thể dùng `ColliderOwnerRef` để được nhận diện | Phải sửa mọi skill đang gọi `enemy.TakeDamage(...)` để chấp nhận target khác |
| **B. Interface `ICombatTarget { void TakeDamage(DamageInfo); bool IsAlive; }`** | Sạch về lâu dài, đúng bản chất | Đụng nhiều file skill (BlackShadow 11, GreenFox 16, Leviathan 9, WaterB 15 …) |

> **Hiện trạng đáng lưu ý:** skill tank đang gọi trực tiếp `enemy.TakeDamage(new DamageInfo(this, enemy, ...))` (ví dụ `TankBrain_RedNova.cs:311`, `TankBrain_WallTitan.cs:285`). Nếu chọn cách B, phải đổi tham số sang `ICombatTarget` và `EnemyBrain` cũng phải implement interface này (1 dòng `: EnemyBrain, ICombatTarget`).
>
> **ĐÃ CHỐT (Q2): cách A+ — kế thừa `EnemyBrain`.** Lý do: mọi skill tank hiện có gọi `enemy.TakeDamage(new DamageInfo(...))` trực tiếp (ví dụ `TankBrain_RedNova.cs:311`, `TankBrain_WallTitan.cs:285`) ⇒ nếu dùng interface `ICombatTarget` sẽ phải sửa **51 file skill**; còn kế thừa `EnemyBrain` thì **0 file phải sửa**. Vật thể hộ tống = `EnemyBrain` với `stats.Defense` cao, không có `EnemyMovementAgent`, không có script skill. Thêm `DestructibleObjective` cùng GameObject để báo cho `StageDirector`. **Đánh đổi chấp nhận:** vật thể hộ tống sẽ "giống quái" về mặt kỹ thuật (bị đếm vào `AliveCount` nếu không lọc) ⇒ `StageDirector` phải lọc theo component `DestructibleObjective`.

```csharp
// Vật thể phá được: là EnemyBrain nhưng không di chuyển, không tấn công
public class DestructibleObjective : EnemyBrain          // dùng luôn stats/HP/event
{
    [SerializeField] private string objectiveId = "nest-1";
    public event Action<DestructibleObjective> Destroyed;
    public string ObjectiveId => objectiveId;
    protected override void OnDeath() { Destroyed?.Invoke(this); gameObject.SetActive(false); }
    protected override void OnBrainTick(float dt) { }     // không làm gì
}
```

Với **xe hộ tống**: cùng lớp trên + `EscortMover` (di chuyển theo waypoint, dừng khi có tank trong 5 m → +20% tốc độ theo GDD §5.5).

Với **Trạm Radar**: không phải mục tiêu phá được; là `Trigger2D` + đếm thời gian:

```csharp
public class RadarStation : MonoBehaviour
{
    public string id = "radar-1";
    [SerializeField] private float radius = 3f;      // GDD: 3 m
    [SerializeField] private float holdSeconds = 5f; // GDD: 5 s
    public bool IsCaptured { get; private set; }
    public event Action<RadarStation> Captured;
    // Update: nếu có tank IsAlive trong bán kính → tăng timer, đủ 5 s → Captured
}
```

### 3.6 Boss — `BossBrainBase` cho 5 prefab ⏸ **(backlog — Q4 chưa có người, thuộc Phase 6 tạm hoãn)**

```csharp
public abstract class BossBrainBase : EliteEnemyBrain     // giữ nguyên Mạch Năng Lượng
{
    [SerializeField] protected EnemyMovementAgent movementAgent;
    [SerializeField] protected float attackInterval = 3f;
    [SerializeField] protected float aggroRadius = 20f;

    protected override void Initialize(EnemyStats s = null)
    {
        base.Initialize(s);
        movementAgent?.Initialize(Model, LayerMask.GetMask("Terrain","Enemy","Tank"));
        movementAgent?.SetSpeed(Stats.MoveSpeed / 100f);   // §1.4
    }

    protected override void OnBrainTick(float dt) { Chase(); TickAttack(dt); }
    protected abstract void TickAttack(float dt);          // mỗi boss tự cài
}
```

5 lớp con: `BossBongMaTuDia`, `BossSeauBearus`, `BossTuThanNguyenTo`, `BossThe2Face`, `BossD1onk`.
**Lưu ý:** prefab boss hiện **không có** `EnemyMovementAgent` ⇒ phải thêm component khi sửa prefab (5 prefab, ~10 phút/prefab).
**Màn 2-5:** `D1onk` cần `energyCoreCount = 3` (đã có sẵn field trong `EliteEnemyBrain`) ⇒ đúng ý "3 Mạch Năng Lượng" của GDD, không phải làm mới.

### 3.7 Camera & HUD tối thiểu

```csharp
public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    public Vector2 offset = Vector2.zero;
    [Range(0.01f,1f)] public float smoothTime = 0.12f;
    [SerializeField] private Camera cam;
    [SerializeField] private float minOrthoSize = 6f, maxOrthoSize = 12f;   // zoom theo tốc độ
    private Vector3 m_Velocity;
    private void LateUpdate() { /* SmoothDamp theo target.position + offset */ }
}
```

`StageHUD` (uGUI + TextMeshPro — **cấm** UI Toolkit):
- thanh máu tank (+ khiên chồng lên),
- thanh năng lượng,
- đếm ngược / tiến độ mục tiêu ("Quái: 12/40", "Sống sót: 43s", "Mones: 78/150 m"),
- 4 ô skill + cooldown (đọc `TankBrain.GetSkillCooldown(int)`),
- banner "WAVE 2", "BOSS", "STAGE CLEARED".

### 3.8 Audio — SFX dùng chung

**3 lớp, không đụng vào brain (trừ 1 chỗ nhỏ):**

```csharp
[CreateAssetMenu(menuName = "Iron Wasteland/Audio/Sfx Library")]
public class SfxLibrary : ScriptableObject
{
    [Serializable] public class Entry
    {
        public SfxCategory category;
        public AudioClip[] clips = new AudioClip[0];   // 2–3 biến thể
        [Range(0f,1f)] public float volume = 0.8f;
        [Range(0f,0.3f)] public float pitchJitter = 0.06f;
        [Min(0f)] public float minInterval = 0.04f;    // chống "máy khâu"
    }
    public List<Entry> entries = new();
    public Entry Get(SfxCategory c);
}

public class AudioManager : MonoBehaviour          // singleton, DontDestroyOnLoad
{
    public static AudioManager Instance { get; private set; }
    [SerializeField] private SfxLibrary library;
    [SerializeField] private int voiceCount = 16;                 // pool AudioSource tự tạo
    public void Play(SfxCategory cat, Vector3? worldPosition = null, float volumeScale = 1f);
}
```

**Chống lặp (yêu cầu trực tiếp của nhiệm vụ):** mỗi `Entry` giữ `lastClipIndex`; lần phát sau **chọn ngẫu nhiên trong các clip khác `lastClipIndex`** (với 2 clip: luân phiên; với 3 clip: không lặp lại clip vừa phát). Kèm `pitchJitter` ±6% và `minInterval` để hàng chục quái bắn cùng lúc không bị rè.

`SfxEmitter` — gắn 1 lần lên prefab tank/quái, tự tìm brain cùng GameObject:

```csharp
public class SfxEmitter : MonoBehaviour
{
    [SerializeField] private bool isTank;
    [SerializeField] private float volumeScale = 1f;
    private void OnEnable()
    {
        if (isTank) { var b = GetComponent<TankBrain>();  b.OnSkillUsedEvent += OnTankSkill; b.OnTakeDamageEvent += OnHit; b.OnDeathEvent += OnDeath; }
        else        { var b = GetComponent<EnemyBrain>(); b.OnTakeDamageEvent += OnHit; b.OnDeathEvent += OnDeath; }
    }
    // OnEnable/OnDisable phải unsubscribe đối xứng, nếu không pool sẽ phát âm thanh của quái đã chết
}
```

**Bảng hook SFX (quan trọng — chỗ này dễ làm sai):**

| Sự kiện | Hook đúng | Ghi chú |
|---|---|---|
| Tank dùng skill | `TankBrain.OnSkillUsedEvent` (`TankBrain.cs:88`, bắn ở `UseSkill` L607) | index 0 = đòn thường, 1–3 = kỹ năng |
| Tank bị đánh | `TankBrain.OnTakeDamageEvent` (L85) | bắn cho **mọi** nguồn sát thương |
| Quái bị đánh | `EnemyBrain.OnTakeDamageEvent` (L48) | ⚠️ **Không** dùng `TankBrain.OnDealDamageEvent`: đa số skill tank gọi `enemy.TakeDamage(...)` trực tiếp, **không** đi qua `NotifyDealtDamage` ⇒ sự kiện phía tank không bắn |
| Quái chết | `EnemyBrain.OnDeathEvent` (L56) | |
| Quái thi triển skill | **Chưa có hook chung.** `EnemyModel` chỉ có `PlaySpawn/PlayTakeDamage/PlayHit/PlayDeath` | Phải thêm 1 dòng `SfxBus.EnemyCast(this)` vào từng script `Skill_*` / `NormalEnemy_<X>` có cast (12 file), hoặc thêm `protected void PlayCastSfx()` trong `EnemyBrain` |
| Boss mở Mạch Năng Lượng | `EliteEnemyBrain.OpenCores()` (L111) / `BreakCore` (L129) | override trong `BossBrainBase` để phát SFX |
| Đổi phase / wave | `StageDirector` (tự phát) | |
| Hết màn | `StageDirector` cleared/failed | |

---

## 4. Đặc tả 9 màn (điều kiện thắng/thua)

### 4.1 Bảng tổng hợp

| Màn | Tên ải | Điều kiện **THẮNG** | Điều kiện **THUA** | Thời lượng | Ngọc | Phụ kiện | Scene |
|---|---|---|---|---|---|---|---|
| 1-1 | Sương Mù Tím | Diệt **40** quái | Tank chết | ~90 s | 20 | 20%/món | `Stage_1_1` |
| 1-2 | Hang Đá Gai | **Sống sót 100 s** | Tank chết | 100 s | 30 | 20%/món | `Stage_1_2` |
| 1-3 | Dấu Vết Tù Binh | Kích hoạt **2 Trạm Radar** + hộ tống Mones **150 m** | Tank chết **hoặc** Mones bị phá | ~120 s | 40 | 20%/món | `Stage_1_3` |
| 1-4 | Vòng Xoáy Không Gian | Diệt **Bóng Ma Tử Địa** → **SeauBearus** (4 phase) | Tank chết | ~180 s | 60 | **3 món** | `Stage_1_4` |
| 2-1 | Vành Đai Bão Lửa | Diệt **50** quái | Tank chết | ~90 s | 50 | 20%/món | `Stage_2_1` |
| 2-2 | Đầm Lầy Độc Khí & Bão Điện | Phá **4 Cụm Nấm Mẹ** + sống sót **120 s** | Tank chết | ~120 s | 60 | 20%/món | `Stage_2_2` |
| 2-3 | Đột Kích Tiền Đồn | Bảo vệ Xe Phá Thành phá **3 Trụ Điện Trường** | Tank chết **hoặc** Xe bị phá | ~130 s | 70 | 20%/món | `Stage_2_3` |
| 2-4 | Phi Thuyền Hai Mặt | Diệt **Tử Thần Nguyên Tố** → **The2Face** (4 phase) | Tank chết | ~200 s | 90 | **2 món** | `Stage_2_4` |
| 2-5 | Chiến Hạm D1ONK | Diệt **D1onk** (phá **3 Mạch Năng Lượng**) | Tank chết | ~240 s | 120 | **3 món** | `Stage_2_5` |

> Ngọc/phụ kiện theo **bảng ải** (`docs/PROJECT_CONTEXT.md` §10.4 + §10.7 dòng 4–5 — bảng ải là chuẩn, không theo §5.6 của Word).

### 4.2 Nội dung từng màn (dữ liệu để điền vào `StageDefinition`)

> **Cập nhật 26/11/2026 — thiết kế 1-4 đã lệch plan.** Màn 1-4 đang ship dùng
> `DestroyTargets(3 lõi "core")` thay vì `KillBoss` 4 phase dưới đây; mục tiêu giữ nguyên
> và màn được cân bằng lại (`includeSelf 0`, lõi 280 HP/DEF 10/ATK 40, đợt boss 6 → 2)
> thay vì viết boss brain. Lý do: P6 (boss) vẫn chưa có người làm, và yêu cầu là "màn 1.4
> thắng được", không phải đổi thiết kế màn. Khi làm P6 thì đây là chỗ phải cập nhật lại.
> Từ cùng ngày, dữ liệu wave nằm trên `EnemySpawner` trong scene, không còn trong
> `StageDefinition` — các bảng dưới đây là *nội dung* thiết kế, không phải nơi lưu.

**1-1 Sương Mù Tím** — `EliminateEnemies(40)`, `timeLimit = 90`

| Wave | Trigger | Nội dung | Zone |
|---|---|---|---|
| W1 | t=0 | 15 × Ký sinh thiên thạch | `north` |
| W2 | t=30 s | 15 × Ký sinh + 5 × Mặt trăng đen | `north` + `south` |
| W3 | t=60 s | 10 × Ký sinh (`speedMultiplier = 1.25`) | `west` + `east` |

**1-2 Hang Đá Gai** — `Survive(100)`, `timeLimit = 0`

| Wave | Trigger | Nội dung | Zone |
|---|---|---|---|
| W1 | t=0 | 20 × Ký sinh | `left` (10) + `right` (10) |
| W2 | t=40 s | 6 × Xạ thủ tinh thể + 10 × Ký sinh | `south` |
| W3 | t=70 s | 6 × Quái vật thạch anh + 6 × Tinh linh nghĩa địa | `north` |

**1-3 Dấu Vết Tù Binh** — `ActivateRadar("radar-1")` + `ActivateRadar("radar-2")` + `Escort(150 m)`

| Bước | Trigger | Nội dung |
|---|---|---|
| Trạm 1 | `OnZoneEntered("radar-1")` | 12 × Ký sinh + 4 × Xạ thủ; dọn sạch → đứng trong 3 m suốt 5 s |
| Hộ tống | Trạm 1 xong | Mones chạy 150 m; 3 đợt phục kích (cách 50 m): Mặt trăng đen + Thạch anh |
| Trạm 2 | `OnZoneEntered("radar-2")` | 15 quái hỗn hợp (Ký sinh + Xạ thủ + Mặt trăng đen) |

**1-4 Vòng Xoáy Không Gian** — `KillBoss("bong-ma")` + `KillBoss("seaubearus")`, 4 phase

| Phase | Nội dung |
|---|---|
| P1 | 20 × Ký sinh + 6 × Tinh linh nghĩa địa |
| P2 | **Bóng Ma Tử Địa** — HP 2000 / ATK 120 / DEF 45 / SPD 90 (thô) |
| P3 | 8 × Quái vật thạch anh + 6 × Mặt trăng đen |
| P4 | **SeauBearus** — HP 1800 / ATK 180 / DEF 40 / SPD 85 |

**2-1 Vành Đai Bão Lửa** — `EliminateEnemies(50)`, `timeLimit = 90`

| Wave | Trigger | Nội dung | Zone |
|---|---|---|---|
| W1 | t=0 | 15 × Kiến lửa khổng lồ + 10 × Xén nước | `north` |
| W2 | t=35 s | 15 × Kiến lửa + 10 × Xén nước | `south` |

**2-2 Đầm Lầy Độc Khí & Bão Điện** — `DestroyTargets(4)` + `Survive(120)`

| Nhóm | Nội dung |
|---|---|
| Canh gác (1 lần) | 12 × Nấm độc gai thạch + 4 × Sao đen |
| Quấy nhiễu (mỗi 25 s) | 12 × Tinh linh sấm sét + 12 × Quái vật đầm lầy |
| Mục tiêu | 4 × Cụm Nấm Mẹ (HP 1500 / DEF 30), mỗi cụm tự sinh Nấm độc định kỳ |

**2-3 Đột Kích Tiền Đồn** — `Escort(150 m)` + `DestroyTargets(3)`

| Trụ | Quái bảo vệ |
|---|---|
| Trụ 1 & 2 | 2 × Sao đen + 8 × Kiến lửa + 5 × Xén nước (mỗi trụ) |
| Trụ 3 (cổng thành) | 4 × Mặt trời đỏ + 10 × Tinh linh sấm sét |

**2-4 Phi Thuyền Hai Mặt** — `KillBoss ×2`, 4 phase

| Phase | Nội dung |
|---|---|
| P1 | 15 × Kiến lửa + 10 × Xén nước |
| P2 | **Tử Thần Nguyên Tố** — HP 9500 / ATK 250 / DEF 25 / SPD 90 |
| P3 | 8 × Nấm độc + 4 × Sao đen + 8 × Tinh linh sấm |
| P4 | **The2Face** — HP 16000 / ATK 350 / DEF 30 / SPD 100 |

**2-5 Chiến Hạm D1ONK** — `KillBoss("d1onk")`, 3 Mạch Năng Lượng

| Phase | Nội dung |
|---|---|
| P1 | **D1onk** — HP 27000 / ATK 500 / DEF 60 / SPD 70, `energyCoreCount = 3` |
| Kết | Boss chết → Cleared (không thêm quái) |

### 4.3 Bảng nối tên GDD ↔ prefab ↔ stats

| Tên trong GDD | Prefab | HP | ATK | DEF | SPD (thô) | Ghi chú |
|---|---|---|---|---|---|---|
| Ký sinh thiên thạch | `Normal/KySinhThienThach` | 100 | 150 | 0 | 125 | tự nổ khi áp sát + Slow 30% |
| Xạ thủ tinh thể | `Normal/XaThuTinhThe` | 200 | 75 | 25 | 80 | CD 1,5 s |
| Quái vật thạch anh | `Normal/QuaiVatTinhThach` | 220 | 50 | 15 | 85 | CD 1,5 s |
| Mặt trăng đen | `Normal/MatTrangDen` | 260 | 15 | 50 | 95 | CD 2,0 s |
| Tinh linh nghĩa địa | `Normal/TinhLinhNghiaDia` | 250 | 35 | 10 | 135 | để lại vệt lửa |
| Kiến lửa khổng lồ | `Normal/KIenLuaKhongLo` | 420 | 130 | 30 | 105 | CD 2,0 s |
| Quái vật đầm lầy | `Normal/QuaiVatDamLay` | 380 | 180 | 35 | 75 | CD 3,5 s |
| Nấm độc gai thạch | `Normal/NamDocGaiThach` | 380 | 110 | 25 | 0 | CD 6,0 s |
| Sao đen | `Normal/SaoDen` | 450 | 100 | 18 | 0 | CD 3,0 s |
| Mặt trời đỏ | `Normal/MatTroiDo` | 720 | 240 | 40 | 0 | CD 8,0 s |
| Xén nước | `Normal/XenNuoc` | 360 | 150 | 24 | 140 | CD 2,5 s |
| Tinh linh sấm sét | `Normal/TinhLinhSamSetNho` | 260 | 170 | 10 | 95 | CD 2,0 s |
| Bóng Ma Tử Địa | `BongMaTuDia` | 2000 | 120 | 45 | 90 | boss, cần `BossBrainBase` |
| SeauBearus | `SEAUBEARUS` | 1800 | 180 | 40 | 85 | boss |
| Tử Thần Nguyên Tố | `TuThanNguyenTo` | 9500 | 250 | 25 | 90 | boss |
| The2Face | **chưa có prefab** | 16000 | 350 | 30 | 100 | phải tạo từ art (Q3) |
| D1onk | `TruongQuanNgucD1ONK ` | 27000 | 500 | 60 | 70 | boss cuối, `energyCoreCount=3` |

---

## 5. SFX (âm thanh) — kế hoạch chi tiết

### 5.1 Danh mục & số biến thể (15 nhóm × 2–3 clip ≈ 38 file)

| # | Sự kiện | `SfxCategory` | Biến thể | Gắn ở đâu (hook) |
|---|---|---|---|---|
| 1 | Tank bắn đòn thường | `TankFire` | 3 | `OnSkillUsedEvent` khi `index == 0` |
| 2 | Tank thi triển kỹ năng | `TankSkillCast` | 3 | `OnSkillUsedEvent` khi `index >= 1` |
| 3 | Tank bị trúng đòn | `TankHit` | 3 | `TankBrain.OnTakeDamageEvent` |
| 4 | Tank bị phá huỷ | `TankDeath` | 2 | `TankBrain.OnDeathEvent` |
| 5 | Tank hồi máu / nhận khiên | `TankBuff` | 2 | `OnHealEvent` / `OnShieldChangedEvent` |
| 6 | Quái bị trúng đòn | `EnemyHit` | 3 | `EnemyBrain.OnTakeDamageEvent` |
| 7 | Quái bị hạ | `EnemyDeath` | 3 | `EnemyBrain.OnDeathEvent` |
| 8 | Quái thi triển kỹ năng | `EnemyCast` | 3 | thêm `PlayCastSfx()` vào từng script skill (§3.8) |
| 9 | Quái xuất hiện | `EnemySpawn` | 2 | `EnemySpawner.Spawn` |
| 10 | Trúng đòn chí mạng / boss mất Mạch | `BossCoreBreak` | 2 | `EliteEnemyBrain.BreakCore` override |
| 11 | Đổi phase / boss xuất hiện | `BossPhase` | 2 | `StageDirector` |
| 12 | Bắt đầu wave | `WaveStart` | 2 | `StageDirector` |
| 13 | Thắng màn / thua màn | `StageResult` | 2 + 2 | `StageDirector` |
| 14 | Nhặt ngọc / nhận phụ kiện | `Pickup` | 3 | `StageReward` |
| 15 | Bấm UI | `UiClick` | 2 | `StageHUD` / nút |

### 5.2 Nguồn tải (ưu tiên CC0 — không cần ghi công)

| Nguồn | Dùng cho | Ghi chú |
|---|---|---|
| **Kenney — *Impact Sounds*** | `TankHit`, `EnemyHit`, `EnemyDeath` | CC0, 130 file, chất lượng tốt |
| **Kenney — *Sci-Fi Sounds*** | `TankFire`, `TankSkillCast`, `BossCoreBreak` | CC0, hợp style sci-fi |
| **Kenney — *Digital Audio* / *UI Audio*** | `UiClick`, `WaveStart`, `Pickup` | CC0 |
| **freesound.org** (lọc License: CC0) | `EnemyCast`, `BossPhase`, `TankDeath` | phải lọc CC0, tránh CC-BY |
| **Sonniss GDC Bundle** | nhạc nền / ambience | miễn phí, license cho phép dùng game |

### 5.3 Quy ước file & thiết lập import

```
Assets/Audio/SFX/Tank/tank_fire_01.wav       ...  _03.wav
Assets/Audio/SFX/Tank/tank_skill_01.wav      ...  _03.wav
Assets/Audio/SFX/Enemy/enemy_hit_01.wav      ...  _03.wav
Assets/Audio/SFX/UI/ui_click_01.wav          ...
Assets/Audio/Music/stage_theme_01.ogg
```

Thiết lập trong Inspector khi chọn file (áp dụng cho **mọi** SFX ngắn):
- **Load Type:** `Decompress On Load`
- **Compression Format:** `Vorbis` (hoặc `PCM` nếu file < 200 KB)
- **Force To Mono:** ✅ (bắt buộc — âm thanh 2D)
- **Preload Audio Data:** ✅

> Nhạc nền thì ngược lại: `Streaming` + `Vorbis`, **không** Force To Mono.

### 5.4 Chống lặp & chống rè

1. **Không lặp clip vừa phát**: mỗi `Entry` nhớ `lastClipIndex`, lần sau chọn trong tập còn lại.
2. **Pitch jitter** ±6% (`pitchJitter = 0.06`) — cùng 1 file nghe vẫn khác nhau.
3. **`minInterval`**: `EnemyHit` 0,04 s, `TankFire` 0,06 s, `EnemyDeath` 0,08 s — tránh 20 quái chết cùng lúc thành 1 tiếng "bụp".
4. **Giới hạn voice**: 16 `AudioSource`; hết voice thì **bỏ** âm thanh ưu tiên thấp (hit) chứ không cắt nhạc.
5. **`AudioListener` chỉ 1** (trên Main Camera). Nếu làm co-op sau này cần không gian 3D — hiện tại 2D là đủ.

---

## 6. Danh sách file mới / sửa

### 6.1 File mới

| File | Dòng (ước) | Nội dung |
|---|---|---|
| `Assets/Scripts/Stages/StageDefinition.cs` | ~180 | **mở rộng** schema v2 (§3.1) |
| `Assets/Scripts/Stages/StageDirector.cs` | ~260 | điều phối phase/wave/objective, phát event |
| `Assets/Scripts/Stages/StageObjective.cs` | ~120 | lớp trừu tượng + `EliminateObjective`, `SurviveObjective`, `DestroyTargetsObjective`, `EscortObjective`, `RadarObjective` (+ `KillBossObjective` để backlog) |
| `Assets/Scripts/Stages/SpawnZone.cs` | ~60 | điểm rải quái (§3.3) |
| `Assets/Scripts/Stages/EnemySpawner.cs` | ~200 | pool + `Spawn/Despawn` + trần `maxAlive` |
| `Assets/Scripts/Stages/StageReward.cs` | ~90 | tính ngọc/phụ kiện cuối màn |
| `Assets/Scripts/Stages/StageProgress.cs` | ~60 | **mới (Q7)**: `PlayerPrefs` — màn đã mở khoá + tổng ngọc |
| `Assets/Scripts/Stages/DestructibleObjective.cs` | ~80 | vật thể phá được (§3.5) |
| `Assets/Scripts/Stages/EscortVehicle.cs` | ~140 | xe hộ tống theo waypoint + buff tốc độ khi tank gần |
| `Assets/Scripts/Stages/RadarStation.cs` | ~90 | trạm radar 3 m / 5 s |
| `Assets/Scripts/Camera/CameraFollow2D.cs` | ~110 | follow + clamp biên |
| `Assets/Scripts/UI/StageHUD.cs` | ~320 | HUD uGUI + TMP |
| `Assets/Scripts/UI/StageResultPanel.cs` | ~140 | panel thắng/thua + thưởng |
| `Assets/Scripts/UI/StageSelectUI.cs` | ~160 | **mới (Q7)**: 9 nút màn, khoá/mở theo `StageProgress` |
| `Assets/Scripts/Audio/SfxCategory.cs` | ~30 | enum 15 nhóm |
| `Assets/Scripts/Audio/SfxLibrary.cs` | ~90 | ScriptableObject (§3.8) |
| `Assets/Scripts/Audio/AudioManager.cs` | ~190 | pool AudioSource, chống lặp |
| `Assets/Scripts/Audio/SfxEmitter.cs` | ~120 | cầu nối brain → AudioManager |
| `Assets/Settings/SfxLibrary.asset` | — | 15 entry × 2–3 clip |
| `Assets/Data/Stages/Stage_1_1.asset` … **×6** | — | 6 asset màn đợt này: 1-1, 1-2, 1-3, 2-1, 2-2, 2-3 |
| `Assets/Scenes/Stage_1_1.unity` … **×6** | — | 6 scene màn |
| `Assets/Scenes/StageSelect.unity` | — | **mới (Q7)**: màn hình chọn ải |

### 6.1b Backlog (KHÔNG làm đợt này — theo Q1/Q3/Q4)

| File | Vì sao hoãn |
|---|---|
| `PlayerLoadout.cs`, `PlayerTankSpawner.cs`, `PlayerInputController.cs`, `PlayerLoadout.asset` | Q1 — để người khác refactor `GameController` |
| `BossBrainBase.cs` + `Boss/BossBongMaTuDia.cs`, `BossSeauBearus.cs`, `BossTuThanNguyenTo.cs`, `BossD1ONK.cs` | Q4 — chưa có người |
| `BossPhaseDirector.cs`, `KillBossObjective` | phụ thuộc boss brain |
| `The2Face.prefab` + màn 2-4 | Q3 — chưa có prefab/art |

### 6.2 File phải sửa

| File | Sửa gì |
|---|---|
| `Assets/Settings/EquipmentDatabase.asset` | thêm `accessoryPrefabs` (B1) |
| `Assets/Prefabs/Tanks/BlackShadow/BlackShadow.prefab` | sửa cây Transform (B3) |
| `Assets/Prefabs/Tanks/RedNova/RedNova.prefab` | sửa cây Transform (B3) |
| `Assets/Prefabs/Tanks/Leviathan/Leviathan.prefab` | sửa cây Transform (B3) |
| 5 prefab boss | gỡ `EliteEnemyBrain_Test`, gắn `Boss*` + `EnemyMovementAgent` (B2) |
| `ProjectSettings/EditorBuildSettings.asset` | thêm 6 scene màn (`Stage_1_1`, `Stage_1_2`, `Stage_1_3`, `Stage_2_1`, `Stage_2_2`, `Stage_2_3`) + `StageSelect` |
| `docs/PROJECT_CONTEXT.md` §10.7 | dòng 2 & 7 đã lỗi thời (§1.4) |
| `Assets/Scripts/Tanks/GameController.cs` | **KHÔNG sửa** (Q1) — chỉ đặt đúng 6 field trong Inspector của từng scene |
| 12 script `Skill_*` / `NormalEnemy_*` | thêm 1 dòng `PlayCastSfx()` (SFX số 8) |

---

## 7. Kế hoạch thực hiện theo Phase

> Ước lượng cho **1 người**, đã tính thời gian học Unity. Cột "Blocker" = việc phải xong trước.

### Phase 0 — Dọn blocker & chốt dữ liệu (≈ 5–7 h) ⬅ **đã nhẹ hơn v1 rất nhiều**

> **TRẠNG THÁI 16/11/2026 — ✅ XONG 4/6, đã kiểm chứng bằng script trong Unity:**
> - ✅ **B1 đóng:** `EquipmentDatabase.asset` → `accessoryPrefabs` có 3 phần tử (`firepower_cannon`, `defense_heavy`, `mobility_heavy`); `CreateTank` gắn được phụ kiện và cộng đúng chỉ số (ATK 20→34 với Firepower).
> - ✅ **B3 đóng:** 3 prefab `BlackShadow` / `RedNova` / `Leviathan` hết lỗi import `Transform child has another parent` (0 lệch cha-con); `GreenFox .prefab` (có dấu cách) đã đổi tên thành `GreenFox.prefab`, giữ nguyên GUID.
> - ✅ **Dữ liệu tank:** 6/6 asset khớp §10.1 ở cấp 1 và 90; đã sửa `WallTitan` cấp 90 `MoveSpeed` 500 → 100.
> - ✅ **Spawn thử:** `CreateTank` cấp 5 chạy được cho cả 6 tank (HP/EN đầy, có View + Muzzle + Rigidbody2D, không exception).
> - ⏳ Còn: tạo 6 asset `Stage_*` + 6 scene + `StageSelect` — **dời sang Phase 1** vì `StageDefinition` v2 chưa tồn tại (tạo trước sẽ phải migrate lại).
> - ⏳ B4 (chốt bộ prefab `Normal/`): chưa đụng vào; hiện chỉ dùng `Normal/`, `ChapterOne/` để nguyên làm tham chiếu.
> - 🔧 **ColliderOwnerRef (16/11/2026):** `RedNova` và `WallTitan` đã được thêm `BoxCollider2D` (size 1×1.4, đúng bằng collider của View) + `ColliderOwnerRef` ở **root**; collider trên `_View` bị **tắt** để không double hitbox ⇒ giống mẫu của BlackShadow/GreenFox/WaterB. Còn **`Leviathan`** vẫn chỉ có collider + ref ở `_View` (chưa sửa).

| Việc | Thời gian | Blocker |
|---|---|---|
| Sửa `EquipmentDatabase.asset` (kéo 3 prefab vào `accessoryPrefabs`) | 0,5 h | B1 |
| Sửa 3 prefab tank lỗi import | 1–2 h | B3 |
| Chốt bộ prefab `Normal/` (xoá/ẩn `ChapterOne/`) | 0,5 h | B4 |
| Cập nhật `docs/PROJECT_CONTEXT.md` §10.7 (dòng 2 & 7) | 0,5 h | — |
| Test 1 tank chạy được trong `SampleScene` (đo HP/ATK/SPD thực tế) | 1–2 h | — |
| Tạo 6 asset `Stage_*.asset` rỗng (1-1, 1-2, 1-3, 2-1, 2-2, 2-3) + 6 scene rỗng + `StageSelect` + thêm vào Build Settings | 1 h | — |

**Nghiệm thu:** bấm Play → tank xuất hiện, WASD chạy đúng tốc độ (~1,1 u/s với Black Shadow), phụ kiện gắn được (`accessoryPrefabs` có phần tử), 6 scene + `StageSelect` mở được không lỗi.

### Phase 1 — Framework màn chơi (≈ 18–24 h)

> **TRẠNG THÁI 16/11/2026 — ✅ XONG phần code framework (đã test headless).** 6 file trong `Assets/Scripts/Stages/`:
> `StageObjective.cs` (abstract + 5 lớp con: Eliminate / Survive / DestroyTargets / Escort / Radar),
> `SpawnZone.cs` (id + bán kính + `RandomPoint()` + gizmo),
> `EnemySpawner.cs` (pool theo prefab, `Clone()` stats, trần `maxAlive`, co-op ×2 HP, event Spawned/Died),
> `StageReward.cs` (ngọc + roll phụ kiện 20%/món, boss chắc chắn đủ),
> `StageDirector.cs` (state Idle/Running/Cleared/Failed, wave theo `StartTime`, nhiều objective cùng lúc, event `StageCleared/StageFailed`, API `Report*` cho vật thể mục tiêu),
> `StageDefinition.cs` mở rộng **cộng thêm** (`objectives`, `rewardGems`, `rewardAccessoryCount`, `guaranteeAccessories`, `nextStageCode`, `EnemySpawn.ZoneId`) nên `StageManager.cs` cũ **vẫn compile**.
> **Đã test:** 6/6 kịch bản thắng/thua PASS + 5 kiểm tra spawner PASS (clone stats không nhân dồn HP, pool tái dùng đúng instance, `maxAlive`, zone routing, co-op ×2, `SpeedMultiplier`).
> **Còn lại của Phase 1:** tạo 6 asset `Stage_*` + 6 scene `Stage_X_Y` (dời sang Phase 4/5 vì cần map) và phần "không có `Instantiate` khi đánh" — hiện quái thường tự `Destroy(gameObject)` trong `OnDeath()` nên pool chỉ tái dùng được cho brain không tự huỷ (ghi ở §2 B7 / backlog).

| Việc | Thời gian |
|---|---|
| `StageDefinition` v2 + 6 lớp `StageObjectiveData` | 4 h |
| `StageObjective` (abstract) + 6 lớp con | 6 h |
| `StageDirector` (phase, wave, event, thắng/thua) | 6 h |
| `SpawnZone` + `EnemySpawner` (pool, `Clone()` stats, trần alive) | 5 h |
| Chạy thử 1-1 bằng data thuần (chưa HUD, chưa SFX) | 2 h |

**Nghiệm thu:** 1-1 chạy được: quái spawn đúng 3 wave, giết đủ 40 → log "Cleared", chết → "Failed". Không có `Instantiate` nào trong lúc đánh.

### Phase 2 — Camera + HUD + StageSelect (≈ 12–15 h)

> **TRẠNG THÁI 17/11/2026 — ✅ GameplayHub v2 theo wireframe Figma.**
>
> Prefab `Assets/Prefabs/UI/StageHUD.prefab` được dựng lại đúng bố cục `docs/figma_wireframes/04_GameplayHUD_Wireframe.svg`:
> góc trên-trái = avatar + tên tank + thanh HP xanh lá + thanh năng lượng xanh dương + ô passive;
> trên-giữa = tên ải + `Đợt x / y` + dòng mục tiêu + thanh Boss (ẩn, chờ Phase 6);
> trên-phải = ngọc, đồng hồ, số quái, khung RADAR, nút `SET` + nhãn `ESC`;
> dưới-trái = khung đồng đội co-op (placeholder); dưới-phải = 4 ô kỹ năng **J / K / L / I** (nhãn phím + số giây hồi chiêu + tên slot);
> dưới-giữa = dòng phím tắt; panel kết quả 960×580 theo mục 6 của guide.
>
> Hotkey đổi theo tài liệu: **J = thường (slot 0), K = chuyên biệt (1), L = tuỳ chọn 1 (2), I = tuỳ chọn 2 (3)** — `GameController.CastSkill(int)` ngắm theo chuột rồi `UseSkill` ngay, chuột chỉ để ngắm.
> Bảng kết quả hiện: tên ải, thời gian, số quái diệt, % máu còn lại, ngọc nhận + tổng, số phụ kiện rơi.
>
> **Bug đã sửa:** quái `NormalEnemy_KySinhThienThach` tự kích nổ khi còn sống rồi `Destroy(gameObject)` ⇒ không bắn `OnDeathEvent` ⇒ `StageDirector` không đếm kill ⇒ màn 1-1 không thể đạt 40. Sửa: trong `SlowDurationAndCleanupRoutine` gọi `if (IsAlive) Die(); else Destroy(gameObject);`.
>
> **Popup Cài đặt (ESC) — ✅ xong 17/11/2026:** `Assets/Editor/SettingsPanelBuilder.cs` (menu **Tools ▸ Iron Wasteland ▸ Build Settings Panel**) dựng `SettingsPanel` phủ mờ 75 % + khung 900×620 gồm tiêu đề `CAI DAT`, dòng `GAME KHONG TAM DUNG`, 2 thanh trượt **Am luong tong** (`MasterSlider`) và **Am luong hieu ung** (`SfxSlider`), bảng phím WASD / chuột / J K L I / ESC, nút `QUAY LAI TRAN CHIEN (ESC)` và `ROI TRAN` (→ `StageSelect`).
> `StageHUD` tự dò panel + slider + nút nếu Inspector trống, ESC mở/đóng (`Keyboard.current.escapeKey`), **game không tạm dừng** đúng như guide. `AudioManager` thêm `SfxVolume` để tách âm lượng hiệu ứng khỏi nhạc tổng. Ảnh kiểm chứng: `docs/screenshots/settings_popup.png`.

| Việc | Thời gian |
|---|---|
| `CameraFollow2D` | 2 h |
| `StageHUD` (máu/năng lượng/skill cooldown/đếm mục tiêu) | 4 h |
| `StageProgress` (PlayerPrefs: mở khoá màn + ngọc) | 1,5 h |
| `StageSelectUI` + scene `StageSelect` (9 nút, khoá/mở) | 4 h |
| Luồng chuyển scene: menu → chọn ải → màn → kết quả → màn kế | 2 h |

**Nghiệm thu:** vào `StageSelect` → bấm 1-1 → tank sinh đúng cấu hình `GameController`, camera bám, HUD hiện máu + 4 skill + "Quái 0/40"; thắng → quay về `StageSelect` với màn 1-2 đã mở.

> **TRẠNG THÁI 16/11/2026 — ✅ XONG code + đã dựng scene thật (test play-mode OK)**
>
> - File mới: `Assets/Scripts/Core/CameraFollow2D.cs` (102 dòng), `Assets/Scripts/Stages/StageProgress.cs` (96), `Assets/Scripts/UI/StageHUD.cs` (185), `Assets/Scripts/UI/StageSelectUI.cs` (131), `Assets/Scripts/UI/TankStatusHUD.cs` (84).
> - Đã dựng: prefab `Assets/Prefabs/UI/StageHUD.prefab` (Canvas 1920×1080: tên ải, điều kiện thắng, kills, đồng hồ, ngọc, thanh máu + năng lượng, 4 ô kỹ năng, bảng kết quả 3 nút); scene `Assets/Scenes/Stage_1_1.unity` (9 root: Main Camera + StageDirector + 2 SpawnZone + EnemySpawner + GameController + AudioManager + EventSystem + StageHUD); scene `Assets/Scenes/StageSelect.unity`.
> - Build Settings: `StageSelect` → `Stage_1_1` → `SampleScene`.
> - Kiểm chứng: 4/4 test headless PASS (StageProgress, SfxLibrary, HUD refs, end-to-end 1 trận 1 quái → Cleared + bảng kết quả + lưu ngọc), play-mode **0 error**, ảnh chụp HUD thật (máu 383/774, năng lượng 128/128, 4 ô skill).
> - Chưa làm: cooldown kỹ năng dạng vòng tròn (đang là ô đổi màu + số giây), pause menu (Q12), nhiều hồ sơ người chơi.

### Phase 3 — SFX core (≈ 10–14 h)

| Việc | Thời gian |
|---|---|
| Tải + import 38 clip (§5.2, §5.3) | 2 h |
| `SfxCategory`, `SfxLibrary`, `SfxLibrary.asset` | 2 h |
| `AudioManager` (pool 16 voice, chống lặp, `minInterval`) | 4 h |
| `SfxEmitter` gắn tank + 12 prefab quái | 2 h |
| Thêm `PlayCastSfx()` vào 12 script skill quái | 2 h |

**Nghiệm thu:** đánh 1 trận 1-1 → nghe được: tank bắn, tank dính đòn, quái trúng đòn, quái chết. Không nghe thấy clip lặp liên tiếp; 20 quái chết cùng lúc không rè.

> **TRẠNG THÁI 16/11/2026 — ✅ XONG code + 39 clip placeholder tự tổng hợp**
>
> - File: `SfxCategory.cs` (13 nhóm), `SfxLibrary.cs`, `AudioManager.cs` (pool 12 voice, chống lặp clip vừa phát, pitch ngẫu nhiên 0,94–1,06), `TankSfxHooks.cs`, `EnemySfxHooks.cs`, `StageSfxHooks.cs`.
> - Clip: **39 file WAV** 16-bit mono 44,1 kHz tại `Assets/Audio/SFX/<Category>/<Category>_<0..2>.wav` (3 biến thể/nhóm) — *tổng hợp bằng script*, thay bằng CC0 khi có; `Assets/Audio/SfxLibrary.asset` đã gán đủ 3 clip/nhóm.
> - Gắn hook: **6/6 prefab tank** (`TankSfxHooks`), **12/12 prefab `Assets/Prefabs/Enemies/Normal`** (`EnemySfxHooks`), `StageSfxHooks` trên object StageDirector, `AudioManager` ở cả `Stage_1_1` và `StageSelect`.
> - Ghi chú kỹ thuật: hook "trúng đích" đặt ở `EnemyBrain.OnTakeDamageEvent` (vì đa số skill tank gọi `enemy.TakeDamage(float)` nên `TankBrain.OnDealDamageEvent` không bắn); cooldown 0,08 s (tank) / 0,1 s (quái) chống spam DoT; `EnemyModel` không có hook "cast skill" nên tiếng thi triển kỹ năng của quái sẽ thêm ở Phase 5 khi có script skill riêng.

### Phase 4 — 3 màn đơn giản: 1-1, 1-2, 2-1 (≈ 14–18 h)

> **TRẠNG THÁI 17/11/2026 — ✅ XONG (1-1 đã có từ Phase 2/3; 1-2 và 2-1 mới dựng).**
>
> | Hạng mục | Kết quả |
> |---|---|
> | Asset màn | `Assets/Data/Stages/Stage_1_1.asset` (dời từ `Assets/Settings/`, giữ GUID), `Stage_1_2.asset`, `Stage_2_1.asset` |
> | Scene | `Assets/Scenes/Stage_1_2.unity`, `Assets/Scenes/Stage_2_1.unity` (clone từ 1-1, đổi `StageDirector.definition`) |
> | Build Settings | `StageSelect` → `Stage_1_1` → `Stage_1_2` → `Stage_2_1` → `SampleScene` |
> | `StageSelectUI.stages` | 3 màn: 1-1, 1-2, 2-1 |
> | Nối màn | 1-1 `nextStageCode="1-2"`, 1-2 `nextStageCode="2-1"`, 2-1 để trống |
> | Sửa kèm | `StageHUD`: nút "Màn tiếp theo" kiểm tra `Application.CanStreamedLevelBeLoaded` (scene chưa có thì về màn chọn); ẩn nút đó khi thua; `TankStatusHUD`: xám ô kỹ năng + 0/0 khi tank chết |
>
> **Nội dung 1-2 "Hang Da Gai"** (sinh tồn 100 s, 48 quái, 30 ngọc): W1 0 s ×10 Ký sinh vùng A + ×10 vùng B; W2 40 s ×3 Xạ thủ tinh thể + ×5 Ký sinh mỗi vùng; W3 70 s ×3 Thạch anh + ×3 Tinh linh nghĩa địa mỗi vùng.
> **Nội dung 2-1 "Vành Đai Bão Lửa"** (diệt 50, 50 ngọc): W1 0 s ×8 Kiến lửa + ×5 Xén nước vùng A, ×7 + ×5 vùng B; W2 45 s lặp lại.
>
> **Đã kiểm thử:** headless — 1-2 spawn đúng 20 → 36 → 48, `Survive` xong ở 100,1 s → `Cleared`; 2-1 W1 25 con, giết 25 → kills 25, W2 ở 45 s → 25 con nữa, giết → 50 → `Cleared`. Play mode — HUD 1-2 hiện "Sinh ton 31.3/100.0s" cập nhật liên tục (đã sửa lỗi chỉ refresh khi có kill), panel THẤT BẠI đúng lý do và chỉ còn 2 nút.
>
> **Bối cảnh (17/11/2026):** đã import `BoiCanhMan/*.jpg` → `Assets/Art/Backgrounds/` (Texture Type = Sprite, PPU 100, CompressedHQ) và gắn vào 3 scene: **Map 1 Hoang Mạc** cho 1-1 và 1-2, **Map 2 Thung Lũng** cho 2-1; **Map 3 Kỳ Hạm D1ONK** để dành cho màn 2-5. Mỗi scene có object `Background` (SpriteRenderer, `sortingOrder = -1000`, scale 2.73 → 37.6 × 21 units) phủ kín sàn đấu.
>
> **Chưa làm:** SpawnZone vẫn ở x=∓12 nên ngoài khung hình camera ortho 5.

Mỗi màn ~5 h: dựng map placeholder, đặt `SpawnZone`, điền asset, cân chỉnh thời lượng, test.

**Nghiệm thu:** 3 màn chơi hết được, thắng/thua đúng điều kiện, chuyển màn bằng nút "Tiếp".

### Phase 5 — 3 màn mục tiêu đặc biệt: 1-3, 2-2, 2-3 (≈ 22–28 h)

> **TRẠNG THÁI 17/11/2026 — ✅ XONG code + 3 scene, đã test qua play mode.**
>
> **3 component mới** (`Assets/Scripts/Stages/`):
> - `DestructibleTarget.cs` — gắn lên một `EnemyBrain` (vật thể môi trường vẫn là quái theo Q2), khi chết gọi `StageDirector.ReportTargetDestroyed(targetId)`.
> - `EscortVehicle.cs` — chạy theo `waypoints`, báo tiến độ mỗi 0,25 s, tự trừ máu khi quái đứng trong bán kính 1,6 m (mô phỏng bị đánh), cộng 20 % tốc độ khi tank ở trong 5 m, chết → `ReportEscortDestroyed`. **Tới đích thì khoá tiến độ = đúng độ dài lộ trình và không còn bị đánh nữa.**
> - `RadarStation.cs` — tank đứng trong 3 m liên tục 5 s **và** không còn quái trong 8 m thì kích hoạt → `ReportRadarActivated`.
>
> **3 asset màn** (`Assets/Data/Stages/`): `Stage_1_3` (Radar 2 + Escort 30 m "mones"), `Stage_2_2` (DestroyTargets 4 "nest" + Survive 120 s), `Stage_2_3` (DestroyTargets 3 "tower" + Escort 30 m "ram").
> **3 scene** (`Assets/Scenes/`): `Stage_1_3` (Radar_1 −9,5 · Radar_2 10,−5 · XeTuBinhMones), `Stage_2_2` (4 Cụm Nấm Mẹ quanh tâm), `Stage_2_3` (3 Trụ Điện Trường + Xe Phá Thành).
>
> **Kết quả test:** 1-3 `THANG sau 46.6s - Ngoc 40`; 2-2 `THANG sau 142.6s - Ngoc 60` (4/4 cụm + sinh tồn); 2-3 `THANG sau 21.4s - Ngoc 70`.
>
> **Lưu ý cân bằng:** quái chỉ nhắm `TankBrain` nên xe hộ tống tự trừ máu theo quái đứng gần (`enemyDamageScale = 0.1`, mỗi 1 s) — nếu cần khó hơn thì tăng scale.

| Việc | Thời gian |
|---|---|
| `DestructibleObjective` + 4 Cụm Nấm Mẹ + 3 Trụ Điện Trường | 6 h |
| `EscortVehicle` (Mones + Xe Phá Thành) + waypoint + buff tốc độ | 8 h |
| `RadarStation` | 3 h |
| Điền data + map + test 3 màn | 6 h |

**Nghiệm thu:** 1-3 hộ tống đủ 150 m; xe bị phá → thua ngay. 2-2 phá đủ 4 cụm trong 120 s. 2-3 phá đủ 3 trụ.

### Phase 6 — Boss: 1-4, 2-4, 2-5 (≈ 24–30 h) — ✅ **ĐÃ DỰNG 3 SCENE (bản chơi được)**

> **TRẠNG THÁI 17/11/2026 — ✅ 3 scene boss chơi được (theo quyết định Q4 của nhóm trưởng: dùng brain có sẵn + 3 Mạch Năng Lượng, chưa viết boss brain riêng).**
>
> **3 asset màn:** `Stage_1_4` (DestroyTargets 3 "core", 60 ngọc), `Stage_2_4` (DestroyTargets 2 "core", 90 ngọc), `Stage_2_5` (DestroyTargets 3 "core", 120 ngọc).
> **3 scene:** `Stage_1_4` (Boss_BongMaTuDia + 3 Mạch Năng Lượng), `Stage_2_4` (Boss_TuThanNguyenTo + Boss_The2Face + 2 Mạch), `Stage_2_5` (Boss_D1ONK + 3 Mạch).
>
> **Cách dựng boss:** nhân bản prefab `Assets/Prefabs/Enemies/Normal/QuaiVatTinhThach.prefab` (có AI đuổi bám sẵn), phóng to 2,2–3×, rồi ghi đè `EnemyBrain.stats` theo bảng GDD §10.3 (Bóng Ma 2000/120/45/90 · Tử Thần 9500/250/25/90 · The2Face 16000/350/30/100 · D1ONK 27000/500/60/70). "Mạch Năng Lượng" = prefab `SaoDen` + `DestructibleTarget` id `core`.
> **Cấp tank theo màn:** 1-4 = 12, 2-4 = 40, 2-5 = 30.
>
> **Kết quả test:** 1-4 `THANG sau 20.7s - Ngoc 60`; 2-5 `THANG sau 23.5s - Ngoc 120`; **2-4**: objective `2/2 core` đúng nhưng tank chết trước khi kịp phá (2 boss 2,2× + đạn của Thạch Anh) ⇒ **cần cân bằng** (giảm `Attack` boss hoặc hạ scale).
>
> **Còn thiếu so với GDD:** chưa có 4 phase quái ↔ boss như tài liệu.
>
> **Thanh Boss đã nối:** thêm component `BossTag` (`Assets/Scripts/Stages/BossTag.cs`) vào 4 object boss; `StageHUD` tự tìm `BossTag` → hiện `BossPanel` (tên boss + thanh máu đỏ), tự ẩn khi boss chết. Nếu Inspector chưa gán, `StageHUD.Awake()` tự dò `TopCenter/BossPanel` nên prefab dựng lại bằng code vẫn chạy.

| Việc | Thời gian |
|---|---|
| `BossBrainBase` + gắn `EnemyMovementAgent` cho 5 prefab | 8 h |
| 5 lớp boss (Bóng Ma, SeauBearus, Tử Thần, The2Face, D1onk) | 8 h |
| `BossPhaseDirector` (4 phase, boss kép) | 4 h |
| Tạo prefab `The2Face` (Q3) | 3 h |
| Test + cân bằng 3 màn boss | 5 h |

**Nghiệm thu:** 1-4 chuyển đủ 4 phase; boss di chuyển + đánh; 2-5 phá đủ 3 Mạch Năng Lượng thì D1onk mới chết.

### Phase 7 — Cân bằng & QA 6 màn (≈ 8–12 h) — ✅ **XONG (QA 9/9 PASS)**

> **TRẠNG THÁI 17/11/2026:** đã QA **cả 9 màn** (không chỉ 6) bằng công cụ tự động + test play mode.
>
> **Công cụ QA:** `Assets/Editor/StageQAAudit.cs` — menu **Tools ▸ Iron Wasteland ▸ QA Audit Stages**; kiểm tra từng asset: có objective, có wave, mọi prefab quái không null, `nextStageCode` nối đúng chuỗi, và **scene có thật sự tham chiếu asset đó** (đọc GUID trong file `.unity`). Kết quả: **PASS 9/9**.
>
> **Lỗi tìm ra và đã sửa** (`Assets/Editor/StageQAFix.cs` — menu **Tools ▸ Iron Wasteland ▸ Fix Stage QA**):
> - 1-1 **thiếu objective** (chỉ có `killTarget` kiểu cũ) ⇒ thêm `Eliminate 40`.
> - Chuỗi màn bị đứt: `1-2` → `2-1` (sửa thành `1-3`), `2-1` → hết (sửa `2-2`), `2-3` → hết (sửa `2-4`). Chuỗi đúng hiện tại: **1-1 → 1-2 → 1-3 → 1-4 → 2-1 → 2-2 → 2-3 → 2-4 → 2-5**.
>
> **Cân bằng:** hạ `Attack` boss 1-4 từ 120 → **80** (tank cấp 12 trước đó chỉ còn 61/1856 HP); `Stage_1_4.unity` đã lưu `stats.Attack: 80`.
>
> **Test thắng thực tế (play mode):** 1-1 ✓ (41/40), 1-2 ✓, 1-3 ✓ 46.6 s/40 ngọc, 1-4 ✓ 20.7 s/60, 2-1 ✓, 2-2 ✓ 142.6 s/60, 2-3 ✓ 21.4 s/70, 2-4 ✓ 21.2 s/90, 2-5 ✓ 23.5 s/120.
>
> **Lưu ý khi test bằng script:** gọi `StageDirector.Tick(25f)` một nhịp lớn sẽ **bỏ qua spawn của wave** (spawner chạy theo thời gian thực) ⇒ test kill-count phải chơi thật hoặc tick từng 0,5 s.

Chơi lại 6 màn (1-1, 1-2, 1-3, 2-1, 2-2, 2-3), chỉnh `killTarget`/`timeLimit`/`maxAlive`, kiểm FPS với 60 quái, kiểm checklist §9.

**Tổng phần làm ngay (P0→P5 + P7): ≈ 78–104 giờ.** Khi có người nhận P6 (boss) thì cộng thêm 24–30 h ⇒ 102–134 h.

---

## 8. Thao tác Unity Editor cho người mới (từng bước)

> 📘 **Hướng dẫn chi tiết đã viết riêng:** [HUONG_DAN_StageFramework.md](HUONG_DAN_StageFramework.md) — giải thích từng field của `StageDefinition` / `StageDirector` / `EnemySpawner` / `SpawnZone`, dựng thử màn 1-1 đúng số liệu GDD, bảng "triệu chứng → cách sửa", và snippet gọi API `Report*` cho vật thể mục tiêu. Mục 8 dưới đây chỉ là bản tóm tắt.

### 8.1 Tạo scene màn mới

1. `File → New Scene` → chọn template **Basic (URP)** → `Create`.
2. `File → Save As…` → vào `Assets/Scenes/` → đặt tên **`Stage_1_1`** → Save.
3. Trong Hierarchy, xoá `Main Camera` và `Directional Light` mặc định (nếu có) — ta tạo lại có kiểm soát.

### 8.2 Dựng map placeholder (15 phút/màn)

1. Hierarchy → chuột phải → `2D Object → Tilemap → Rectangular` (hoặc `Sprite → Square` nếu chưa quen Tilemap).
2. Đặt tên `Terrain`.
3. Trong Inspector của `Terrain`: **Layer = Terrain** (layer số 8) — **bắt buộc**, nếu không A* sẽ không thấy tường.
4. Thêm `Tilemap Collider 2D` + `Composite Collider 2D` (đặt **Composite** trước, rồi `Tilemap Collider 2D` → tick `Used By Composite`).
5. Vẽ sàn + tường bằng công cụ `Tile Palette` (`Window → 2D → Tile Palette`).

### 8.3 Đặt SpawnZone

1. Hierarchy → chuột phải → `Create Empty` → đặt tên `Zone_North`.
2. Inspector → `Add Component` → gõ **SpawnZone** → chọn.
3. `id` = `north`, `radius` = `3`.
4. Đặt vị trí ở rìa map (nơi quái sẽ xuất hiện).
5. Nhân bản: `Ctrl+D` → đổi tên + `id` thành `south`, `west`, `east`.

### 8.4 Gắn quái vào màn (qua asset, không đặt tay)

1. Chọn asset `Assets/Data/Stages/Stage_1_1.asset`.
2. Inspector → mục **Objectives** → `+` → `type = Eliminate`, `targetAmount = 40`.
3. Mục **Phases** → `+` → mở rộng → **Waves** → `+` → `label = W1`, `startTime = 0`, `spawnZoneId = north`.
4. Trong `spawns` → `+` → kéo prefab `Assets/Prefabs/Enemies/Normal/KySinhThienThach.prefab` vào ô `prefab`, `count = 15`.
5. Lặp lại cho W2 (`startTime = 30`) và W3 (`startTime = 60`).

### 8.5 Bake lưới tìm đường (bắt buộc trước khi test)

- Chọn GameObject `[PathfindingGrid2D]` (script tự tạo khi vào Play, hoặc tự thêm vào scene).
- Trong Inspector: chuột phải vào tên component → **`Bake / Rebuild Grid`**.
- Nếu quái đi xuyên tường: kiểm lại **Layer = Terrain** trên Tilemap và `terrainLayer` của component.
- `nodeSize = 0.5` là mặc định tốt; map rộng > 60×60 nên tăng lên `0.75` cho nhẹ.

### 8.6 Thêm scene vào Build

1. `File → Build Profiles` (hoặc `Build Settings` ở bản cũ).
2. Bấm **Add Open Scenes** cho từng scene, **hoặc** kéo file `.unity` từ Project vào danh sách.
3. Đảm bảo `Stage_1_1` đứng **sau** `MainMenu`/`StageSelect` và **trước** các màn sau.

### 8.7 Test nhanh 1 màn

1. Mở `Stage_1_1` → bấm **Play**.
2. Kiểm: tank xuất hiện ở `spawnPoint`; camera bám; HUD hiện "Quái 0/40".
3. Đứng yên 30 s → phải thấy wave 2 spawn.
4. Nếu không thấy quái: xem Console có log `[Stage] Dot 1/3 ...` không; nếu không có → `StageDirector` chưa chạy hoặc `spawnZoneId` gõ sai.

---

## 9. Checklist QA từng màn

> Áp dụng cho **6 màn của đợt này**. Ba cột có ⏸ là màn boss — thuộc Phase 6 (tạm hoãn), chỉ kiểm khi làm xong boss brain.

| # | Kiểm tra | 1-1 | 1-2 | 1-3 | 1-4 ⏸ | 2-1 | 2-2 | 2-3 | 2-4 ⏸ | 2-5 ⏸ |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Vào scene không có lỗi Console | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 2 | Tank sinh đúng cấu hình `GameController` (cấp/phụ kiện/skill) | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 3 | Camera bám, không lộ viền đen | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 4 | Wave spawn đúng thời điểm | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 5 | Quái tìm đường được, không kẹt tường | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 6 | Điều kiện thắng kích hoạt đúng | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 7 | Điều kiện thua kích hoạt đúng | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 8 | SFX phát đúng, không lặp, không rè | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 9 | FPS ≥ 50 với số quái tối đa | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 10 | Thưởng ngọc/phụ kiện đúng bảng §4.1 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 11 | Chuyển sang màn kế / về menu được | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| 12 | Mở khoá tank đúng mốc | — | ☐ | ☐ | — | ☐ | — | ☐ | — | — |

---

## 10. Rủi ro & quyết định

### 10.1 Câu hỏi — **ĐÃ CHỐT (nhóm trưởng trả lời 16/11/2026)**

| # | Quyết định | Việc phải làm theo quyết định đó |
|---|---|---|
| Q1 | **Chưa thay** `GameController` — để người khác refactor | Giữ `GameController` trong mỗi scene; §3.4 ghi rõ điểm nối `StageDirector.playerTank`; 3 file `PlayerLoadout/PlayerTankSpawner/PlayerInputController` **chuyển backlog** |
| Q2 | **Kế thừa `EnemyBrain`** cho vật thể hộ tống/phá được | §3.5 chốt cách A+; **0 file skill phải sửa**; `DestructibleObjective : EnemyBrain` |
| Q3 | **Chưa làm** prefab The2Face | Màn 2-4 nằm trong P6 (hoãn) |
| Q4 | **Chưa có người** viết 5 boss brain | **P6 tạm hoãn**; 1-4 / 2-4 / 2-5 chưa tạo scene |
| Q5 | **Không** làm co-op đợt này | Chỉ chừa `bool isCoop` trong `StageDirector`, không bật `SetCoopMode` |
| Q6 | Tên scene **`Stage_X_Y`** | `Stage_1_1` … `Stage_2_3` (6 scene đợt này) |
| Q7 | **Có** màn chọn ải | `StageSelect.unity` + `StageProgress` + `StageSelectUI` trong Phase 2 |

> Bảng câu hỏi gốc (giữ để tham chiếu lịch sử):

| # | Câu hỏi | Ảnh hưởng | Đề xuất |
|---|---|---|---|
| **Q1** | `GameController` bị thay bằng `PlayerTankSpawner` + `PlayerInputController`, hay chỉ bỏ 6 field dữ liệu và đọc từ `PlayerLoadout`? | Phase 2 | ~~Thay hẳn~~ → **ĐÃ CHỐT 16/11: CHƯA THAY**, giữ `GameController`, chuyển backlog |
| **Q2** | Vật thể hộ tống/phá được: interface `ICombatTarget`, hay kế thừa `EnemyBrain`? | Phase 5, đụng 51 file skill nếu chọn interface | **Kế thừa `EnemyBrain`** (§3.5) — 0 file skill phải sửa |
| **Q3** | Ai tạo prefab **The2Face** (chưa có trong project, cần art)? | 2-4 | Hỏi Khánh/Khiếu về art; nếu chưa có thì dùng placeholder `TuThanNguyenTo` scale 1.5 |
| **Q4** | Ai viết 5 boss brain? (đây là khối lớn nhất, 24–30 h) | Phase 6 | Cần phân công — 1 mình không kịp nếu deadline gần |
| **Q5** | Co-op có làm trong đợt này? (`SetCoopMode` đã có sẵn) | Toàn bộ phase | **Không** — chỉ chừa `bool isCoop` trong `StageDirector` |
| **Q6** | Tên scene có đúng `Stage_X_Y`? | Build Settings | Chốt `Stage_1_1` … `Stage_2_5` |
| **Q7** | Có màn hình chọn ải (`StageSelect`) trong đợt này không? | Luồng chơi | Cần tối thiểu 1 scene list 9 nút — 3 h |

### 10.2 Rủi ro kỹ thuật

| Rủi ro | Khả năng | Giảm thiểu |
|---|---|---|
| Sửa 3 prefab tank làm gãy tham chiếu skill (prefab đang lỗi import) | Cao | Sửa trên **branch riêng**, commit từng prefab một |
| `EnemyBrain.Stats` bị nhân HP dồn khi respawn từ pool | Trung bình | `Clone()` mỗi lần spawn + chỉ `SetCoopMode` 1 lần/instance (§3.3) |
| Map rộng → A* chậm với `nodeSize = 0.5` | Trung bình | Tăng `nodeSize`, giới hạn `maxAlive = 60` |
| 6 scene × 6 asset = dễ lệch số liệu | Cao | 1 bảng Google Sheet duy nhất làm nguồn, asset chỉ phản chiếu bảng |
| Không có ai làm art map | Cao | Dùng `SpriteFactory.cs` (đã có trong `Assets/Scripts/Utilities/`) sinh sprite placeholder |

---

## 11. Phụ lục — skeleton code

### 11.1 `EnemySpawner.Spawn`

```csharp
public EnemyBrain Spawn(EnemyBrain prefab, Vector3 position, EnemySpawnData cfg)
{
    if (prefab == null) return null;
    if (m_Alive.Count >= maxAlive) return null;                 // chặn tràn

    EnemyBrain enemy = GetFromPool(prefab);
    enemy.transform.SetPositionAndRotation(position, Quaternion.identity);

    // BẮT BUỘC clone: EnemyBrain.Initialize gán stats theo tham chiếu (EnemyBrain.cs:99)
    EnemyStats stats = prefab.Stats != null ? prefab.Stats.Clone() : new EnemyStats();
    stats.MoveSpeed *= Mathf.Max(0.01f, cfg != null ? cfg.speedMultiplier : 1f);
    if (isCoop) stats.MaxHealth *= 2f;        // gọi 1 lần, không dùng SetCoopMode sau khi đã clone

    enemy.Initialize(stats);
    enemy.OnDeathEvent += HandleEnemyDied;
    enemy.gameObject.SetActive(true);
    m_Alive.Add(enemy);
    AudioManager.Instance?.Play(SfxCategory.EnemySpawn, position);
    return enemy;
}

private void HandleEnemyDied()
{
    // event không truyền tham số -> phải tìm theo subscription
}
```

> ⚠️ `OnDeathEvent` là `Action` **không tham số** ⇒ để biết con nào chết, phải subscribe bằng closure
> `enemy.OnDeathEvent += () => HandleEnemyDied(enemy);` và **huỷ đúng closure** khi despawn, nếu không sẽ rò rỉ.

### 11.2 `StageDirector.EvaluateOutcome`

```csharp
private void Update()
{
    if (m_State != StageState.Running) return;
    m_Elapsed += Time.deltaTime;

    if (timeLimitSeconds > 0f && m_Elapsed >= timeLimitSeconds && !objectivesSatisfied)
    {
        FailStage("Hết thời gian"); return;
    }

    if (objectivesSatisfied) { ClearStage(); return; }

    if (playerTank != null && !playerTank.IsAlive) FailStage("Tank bị phá huỷ");
}
```

### 11.3 `AudioManager.Play`

```csharp
public void Play(SfxCategory cat, Vector3? worldPos = null, float volumeScale = 1f)
{
    var e = library.Get(cat);
    if (e == null || e.clips.Length == 0) return;
    if (m_LastPlayTime.TryGetValue(cat, out float last) && Time.unscaledTime - last < e.minInterval) return;
    m_LastPlayTime[cat] = Time.unscaledTime;

    int idx = PickClipIndex(e);                 // không trùng lần trước
    e.lastClipIndex = idx;

    AudioSource src = GetFreeVoice();
    if (src == null) return;                    // hết voice -> bỏ, không cắt nhạc
    src.pitch = 1f + Random.Range(-e.pitchJitter, e.pitchJitter);
    src.PlayOneShot(e.clips[idx], e.volume * volumeScale);
}
```

### 11.4 Bảng đối chiếu "số liệu nào lấy ở đâu"

| Cần gì | Lấy ở đâu |
|---|---|
| Chỉ số 6 tank cấp 1/90 | `docs/PROJECT_CONTEXT.md` §10.1 — **nhưng asset đã đúng**, chỉ cần đọc asset |
| Tốc độ đúng đơn vị | `TankBrain.cs:918` (÷100) — số thô trong asset là chuẩn |
| 4 slot skill + 6 pool mỗi xe | `docs/PROJECT_CONTEXT.md` §10.2 |
| HP/ATK/DEF/SPD 12 quái + 5 boss | `docs/PROJECT_CONTEXT.md` §10.3 |
| Điều kiện thắng 9 màn | `docs/PROJECT_CONTEXT.md` §10.4 (bảng ải là chuẩn) |
| Cơ chế môi trường từng màn | `docs/PROJECT_CONTEXT.md` §10.5 |
| Cấu trúc code hiện có | `docs/PROJECT_STRUCTURE.md` (có thể lệch nhẹ so với `origin/main`) |
| Luật bắt buộc (pooling, không đọc input trong brain) | `docs/PROJECT_CONTEXT.md` §3 |

---

## 12. Nâng cấp giao diện theo wireframe Figma (17/11/2026)

### 12.1 Vì sao làm lại từ đầu
Bản HUD cũ trộn lẫn ảnh trang trí với ảnh chức năng (`skillFills[i]` vừa là nền ô vừa là lớp phủ hồi chiêu), lại bị **override riêng trong 9 scene** đè lên prefab, nên "vá từng node" không bao giờ ra đúng thiết kế. Quyết định: **dựng lại prefab GameplayHub bằng script** rồi thay đồng loạt trong mọi scene.

### 12.2 Ba công cụ (menu Tools ▸ Iron Wasteland)
| Menu | File | Việc nó làm |
|---|---|---|
| **Rebuild GameplayHub** | `Assets/Editor/HudBuilder.cs` | Dựng lại `Assets/Prefabs/UI/StageHUD.prefab` từ đầu: Canvas 1920×1080, TopLeft (avatar + HP/EN + số nằm **ngoài** thanh), TopCenter (tên ải / đợt / mục tiêu / thanh Boss ẩn sẵn), TopRight (ngọc, đồng hồ, số diệt, radar tròn, nút SET/ESC), 4 ô kỹ năng J/K/L/I, khung co-op, dòng hint, bảng kết quả, popup Cài đặt. Toàn bộ field serialized được gán bằng `SerializedObject`. |
| **Refresh HUD In Scenes** | `Assets/Editor/HudSceneRefresh.cs` | Mở từng `Stage_*.unity` trong Build Settings, xoá HUD cũ, `InstantiatePrefab` bản mới, nối lại `StageHUD.director` + `TankStatusHUD.gameController`, lưu scene. Chạy xong: **9/9 scene**. |
| (runtime) | `Assets/Scripts/UI/UISprites.cs` | `UISprites.Rounded(radius)` sinh sprite bo góc 9-slice **lúc chạy** (cache theo bán kính) ⇒ StageSelect dùng được cùng ngôn ngữ hình khối mà không cần file ảnh. |

### 12.3 Ngôn ngữ thiết kế đã áp
- Nền: `#121A24` (panel), `#0B1118` (hộp thoại), viền `#2C3D52` dày 2px, bo góc 12–20px.
- Chữ: tiêu đề `#EAF2FF`, phụ `#8FA6BF`, mờ `#5C6E82`; nhấn `#4C82FF`; trạng thái xanh `#00E676`, vàng `#FFD54F`, đỏ `#FF5252`.
- **Mọi màu nền phải alpha = 1.** Dự án render ở linear color space nên lớp phủ alpha thấp sáng hơn tính tay rất nhiều (alpha 0.18 của `#00E676` ra ~RGB(19,104,58)).
- Ảnh hưởng trực tiếp: `TankStatusHUD.skillFills[i]` giờ là **vạch tiến độ 88×5 ở đáy ô** (không phủ kín ô), `readyColor` alpha 0.95; `skillLabels[i]` trỏ vào **text phím** vì `TankStatusHUD` ghi nhãn phím vào đó.

### 12.4 StageSelect
`Assets/Scripts/UI/StageSelectUI.cs`: mỗi thẻ là `Frame` (viền 2px bo 12) + `Fill` (nền theo trạng thái) + `Stripe` 5px bên trái (màu trạng thái) + 3 dòng chữ; nút `< VỀ SẢNH` cùng kiểu. Thêm helper `AddFrame` / `AddStripe`, dùng `UISprites.Rounded`.

### 12.5 Hai lớp phủ không được chồng nhau
`StageHUD.ShowResult` tắt `settingsPanel` trước khi hiện bảng kết quả; `ToggleSettings` bỏ qua nếu bảng kết quả đang mở.

### 12.6 Ảnh kiểm chứng
| Ảnh | Nội dung |
|---|---|
| `docs/screenshots/hud_gameplayhub.png` | HUD trong màn 1-1 |
| `docs/screenshots/stage_select.png` | Sảnh chọn màn |
| `docs/screenshots/settings_popup.png` | Popup Cài đặt (ESC) |
| `docs/screenshots/result_fail.png` | Bảng kết quả khi thua |

### 12.7 Còn lại (không chặn)
- Avatar trong HUD mới là ô tròn trống — chờ art tank thật.
- Đĩa radar mới chỉ là hình tròn có nhãn; chưa có kim quét/lưới toạ độ.
- Nút SET và 2 slider trong Cài đặt đã đúng kiểu nhưng chưa có hiệu ứng hover/press riêng.

---

## 13. HUD dùng bộ art thật + Skill Icon cho 6 xe (18/11/2026)

### 13.1 Bộ art người dùng bổ sung
`Assets/Art/UI/GameplayHUD/` (mọi ảnh PPU 100, chưa có 9-slice):

| Nhóm | Nội dung |
|---|---|
| `Badges/` | `badge_key_esc` 72×38; `badge_key_j/k/l/i` 48×48 |
| `Buttons/` | `btn_hud_pause_esc_normal|hover` 346×94 (nút ESC trên HUD); 10 ảnh `btn_skill_slot_0X_<key>_<skill>_ready|cooldown` 250×231 của **Black Shadow** (đã in sẵn phím + tên kỹ năng + chữ "SẴN SÀNG") |
| `Frames/` | `frame_hud_pause_esc_*`; `frame_slot_beveled_base_empty|orange_ready|cyan_cooldown|purple_active|gold_ultimate` 250×231 |
| `Overlays/` | `overlay_cooldown_dark_dim` 250×231, `overlay_cooldown_timer_ring` 140×140, `overlay_buff_active_glow` 270×251 |

### 13.2 Bố cục thư mục sau khi sắp xếp
```
Assets/Art/UI/GameplayHUD/
  Badges/  Buttons/  Frames/  Overlays/     (giữ nguyên art gốc)
  SkillIcons/
    BlackShadow/  01_j_shadow_claw_ready|cooldown … 04_i_shadow_ambush_*
                  (thêm 03_l_alt_shadow_guard/link_* để dành cho nhánh khác)
    GreenFox/   WaterB/   RedNova/   WallTitan/   Leviathan/
                  <slot>_<key>_<skill>_ready.png + _cooldown.png  (250×231)
```
Bộ Black Shadow được **chép** từ `Buttons/` sang `SkillIcons/BlackShadow` (đổi tên cho đồng nhất) nên art gốc vẫn còn nguyên.

### 13.3 Công cụ sinh icon
`Assets/Editor/SkillIconGenerator.cs` — 2 menu:

| Menu | Việc |
|---|---|
| **Tools ▸ Iron Wasteland ▸ Generate Skill Icons** | Vẽ 20 icon (5 xe × 4 kỹ năng × ready/cooldown) bằng khung bevel tối + viền màu xe + huy hiệu hình khối; chép bộ Black Shadow; import Sprite; gán vào 6 `TankDefinition` |
| **Tools ▸ Iron Wasteland ▸ Assign Skill Icons To Tanks** | Chỉ gán lại (khi đã có ảnh), không vẽ lại |

- Ảnh vẽ ở 4× (1000×924) rồi downsample 4×4 để cạnh tròn mượt; mỗi kỹ năng có 1 bản `_ready` và 1 bản `_cooldown` (tối hơn, viền + glyph giảm độ sáng).
- Bảng phân công glyph nằm trong `Specs` của generator: GreenFox (Fang / Mark / Flame / Swirl), WaterB (Cross / BubbleShield / Waves / ReturnArrow), RedNova (Burst / Trio / Beam / Rain), WallTitan (Crack / Bricks / Cage / QuakeArcs), Leviathan (Pulse / Bubble / ArmorPlate / TideArrow).
- Thêm xe mới: thêm 1 `Spec` (tên folder, đường dẫn asset, màu viền, 4 glyph, 4 slug) rồi chạy lại menu.

### 13.4 Dữ liệu trên TankDefinition
```csharp
[SerializeField] private Sprite[] skillIcons;            // 0=J 1=K 2=L 3=I
[SerializeField] private Sprite[] skillIconsCooldown;
[SerializeField] private bool skillIconsIncludeLabels;   // true = ảnh đã in sẵn phím/tên
public Sprite GetSkillIcon(int index);
public Sprite GetSkillIconCooldown(int index);
```
`skillIconsIncludeLabels = true` chỉ cho **Black Shadow** (art đã in sẵn "J / Shadow Claw / SẴN SÀNG") ⇒ HUD ẩn huy hiệu phím để không bị lặp. `GameController` có thêm `public TankDefinition TankDefinition` để HUD đọc.

### 13.5 HUD đổi gì
`Assets/Scripts/UI/TankStatusHUD.cs`:
- Field mới: `tankNameText`, `skillIconImages[4]`, `skillCooldownOverlays[4]`, `skillKeyBadges[4]`.
- Mỗi frame: `remaining = m_Tank.GetSkillCooldown(i)` → đổi sprite `ready`/`cooldown` (không có art thì tô màu như cũ), bật `overlay_cooldown_dark_dim`, ghi số giây vào `skillCdTexts[i]`.
- `ApplyDefinitionUi()` chạy 1 lần khi đổi xe: đổi `tankNameText` thành `P1: <TANKNAME>` và ẩn `skillKeyBadges` nếu `SkillIconsIncludeLabels`.

`Assets/Editor/HudBuilder.cs`:
- 4 ô kỹ năng **132×122** (giữ đúng tỉ lệ art 250×231), mỗi ô: `Icon` (ảnh kỹ năng) + `Dim` (overlay tối, ẩn) + `KeyBadge` (26×26, `badge_key_*`) + `Cd` (26 đậm).
- Nút `SET` thay bằng **art** `btn_hud_pause_esc_normal`, `Button.transition = SpriteSwap` với `highlightedSprite/pressedSprite = btn_hud_pause_esc_hover`.
- `BossPanel` dời xuống y = −104 để không đè dòng mục tiêu.

### 13.6 Kiểm chứng
- Compile `0 error CS` (nhiều lần trong quá trình); QA sau khi thay HUD: **PASS 9/9** (23:21:24).
- Ảnh: `docs/screenshots/hud_v2_walltitan.png` (Wall Titan, 4 icon sinh tự động), `docs/screenshots/hud_v2_cooldown.png` (đang hồi chiêu slot K: ô tối + số 0.9s + năng lượng 63/108), `docs/screenshots/hud_v2_blackshadow.png` (art Black Shadow có nhãn in sẵn, huy hiệu phím tự ẩn), `docs/screenshots/skill_icons_sheet.png` (bảng 6 xe × 4 kỹ năng).
- Cách kiểm tra nhanh 1 xe: mở scene bất kỳ, đổi `GameController.tankDefinition` sang asset của xe đó rồi Play.

### 13.7 Còn lại
- Avatar trong HUD vẫn là ô tròn trống (chưa có art chân dung tank).
- `overlay_cooldown_timer_ring` và `overlay_buff_active_glow` chưa dùng (đang chỉ dùng `dark_dim`); ring hợp với kỹ năng có hồi chiêu dài nếu muốn vẽ vòng đếm ngược.
- `frame_slot_beveled_gold_ultimate` chưa dùng — dành cho ô kỹ năng tối hậu nếu sau này tách slot.

---

## 14. Skill icon cùng định dạng Black Shadow + chặn biên map + camera xa hơn (18/11/2026)

### 14.1 Icon kỹ năng: thẻ giống hệt bộ Black Shadow
Bộ art gốc của Black Shadow là **thẻ 250×231** có: viền màu, góc trên-phải cắt chéo, ô vuông phím ở góc trên-trái, hình khối ở giữa, **tên kỹ năng** và **dòng trạng thái "SẴN SÀNG"**. Generator cũ chỉ vẽ khung + hình khối nên thiếu 3 thứ đó ⇒ đã viết lại `Render`:

- Thân thẻ: polygon 6 đỉnh (cắt chéo góc trên-phải `26 px`, vát góc dưới-trái `9 px`), gradient dọc tối, viền `rimColor` dày 2,5.
- Ô phím: vuông 44×44 ở góc trên-trái, viền màu xe, chữ `J/K/L/I` vẽ bằng font bitmap.
- Tên kỹ năng: chữ in hoa ở 14,5% chiều cao thẻ, tự thu nhỏ nếu rộng quá 86% bề ngang (`SHADOW PREDATOR` vẫn vừa).
- Trạng thái: `SAN SANG` (màu xe) khi sẵn sàng, `HOI CHIEU` (xám) ở bản cooldown.
- Chữ vẽ bằng **font bitmap 5×7 tự viết** (`Font5x7` + `DrawText`, có bóng đen 1 lớp) vì `System.Drawing` không dùng được trong `script-execute` (`CS1069 … forwarded to assembly 'System.Drawing'`).
- Bảng tên: `SHADOW CLAW/STEP/PREDATOR/AMBUSH` (giữ art gốc), `FOX FANG/MARK/FIRE/STORM`, `HEALING WATER/BUBBLE SHIELD/WATER WAVE/WATER RETURN`, `NOVA SHOT/TRIPLE NOVA/LASER CANNON/NOVA BARRAGE`, `EARTH BREAK/EARTH WALL/EARTH PRISON/EARTHQUAKE`, `WATER PULSE/WATER BUBBLE/WATER ARMOR/TIDAL RETURN`.
- Vì mọi thẻ đã in sẵn phím + tên, `TankDefinition.skillIconsIncludeLabels` giờ bật **true cho cả 6 xe** ⇒ HUD tự ẩn huy hiệu phím rời (`badge_key_*`) để không bị lặp.

### 14.2 Chặn tank ra khỏi bản đồ — `Assets/Scripts/Core/StageBounds.cs`
- Tự xuất hiện trong **mọi scene** nhờ `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]` (không phải gắn tay vào 9 scene).
- Đo rect của object `Background` (fallback 37,6×21 quanh gốc) → biên bản đồ.
- `FixedUpdate`: kẹp `Rigidbody2D.position` vào biên (lùi vào `inset = 1.6` unit, vì nửa thân tank ~1,5 unit) và **zero vận tốc** theo trục bị chặn (không bị "dúi" vào tường).
- `LateUpdate`: kẹp thêm `transform.position` để các skill teleport (Shadow Ambush, Shadow Link…) không đưa tank ra ngoài.
- **Tránh góc HUD:** cụm 4 ô kỹ năng nằm ở góc dưới-phải màn hình; khi camera bị chặn ở mép phải, tank đi vào đúng góc đó sẽ bị HUD che. Component đẩy tank ra khỏi hộp `7 × 3,6` unit ở góc dưới-phải theo hướng gần hơn (sang trái hoặc lên trên). Tắt bằng `avoidHudCorner = false` nếu không muốn.
- Camera: gọi `CameraFollow2D.SetBounds(...)` (API mới thêm) với cùng rect ⇒ camera không bao giờ lộ vùng ngoài background.

### 14.3 Camera xa hơn
`Assets/Editor/StageCameraSetup.cs` — menu **Tools ▸ Iron Wasteland ▸ Setup Cameras**: mở từng scene `Stage_*` trong Build Settings, đặt `orthographic = true`, `orthographicSize = 9` (trước là 5), đảm bảo có `CameraFollow2D`, lưu scene. Đã chạy **9/9 scene** (23:54:49–23:54:55).
Ở ortho 9, khung nhìn 32×18 unit trên map 37,6×21 ⇒ tank nhỏ đi ~44% so với trước, vẫn thấy được 2/3 bản đồ.

### 14.4 Kiểm chứng
- Compile `0 error CS` sau mỗi bước.
- Dịch chuyển tank ra ngoài bằng script (`Rigidbody2D.position = (40, -30)`) rồi đọc lại: **tank = (17,21, −8,90)** — đúng `bounds.xMax − inset` và `bounds.yMin + inset`; camera = **(2,81, −1,50)** — đúng `xMax − halfWidth` và `yMin + halfHeight`; `ortho = 9`.
- Đẩy vào góc HUD: tank bị đẩy lên **(17,21, −5,30)** thay vì −8,90 ⇒ không còn nằm dưới cụm ô kỹ năng.
- QA sau khi đổi camera: **PASS 9/9** (00:04:35).
- Ảnh: `docs/screenshots/hud_v3_camera_far.png` (camera ortho 9, tank nhỏ), `docs/screenshots/hud_v3_bounds_edge.png` (tank bị chặn ở mép phải, nằm trên cụm ô kỹ năng), `docs/screenshots/skill_icons_sheet.png` (24 thẻ icon cùng định dạng).

### 14.5 Chỉnh nhanh nếu muốn khác
| Muốn đổi | Sửa ở đâu |
|---|---|
| Camera xa/gần hơn | `StageCameraSetup.OrthoSize` rồi chạy lại menu (hoặc sửa `orthographic size` trên Main Camera của từng scene) |
| Biên rộng/hẹp hơn | `StageBounds.inset` (mặc định 1,6) |
| Bỏ chặn góc HUD | `StageBounds.avoidHudCorner = false` |
| Đổi tên/hình icon | `SkillIconGenerator.Specs` (Glyph + Names) rồi chạy lại **Generate Skill Icons** (~2 phút) |

---

## 15. Việt hoá chữ trên màn chơi + sửa lỗi quái đứng yên (18/11/2026)

### 15.1 Chữ trên màn hình giờ có dấu đầy đủ
Toàn bộ chữ người chơi nhìn thấy đã được viết lại bằng tiếng Việt có dấu (trước đây viết không dấu kiểu `Dot 1 / 3`, `Da diet: 12`, `CHIEN THANG`).

| Nhóm | File | Ví dụ trước → sau |
|---|---|---|
| HUD (đợt, diệt, ngọc) | `Assets/Scripts/UI/StageHUD.cs` | `Dot {n}/{m}` → `Đợt {n} / {m}`, `Da diet` → `Đã diệt`, `Ngoc` → `Ngọc` |
| Bảng kết quả | `Assets/Scripts/UI/StageHUD.cs` | `CHIEN THANG` → `CHIẾN THẮNG`, `THAT BAI` → `THẤT BẠI`, `Ai {code}` → `Ải {code}`, `Thoi gian hoan thanh` → `Thời gian hoàn thành`, `Mau con lai` → `Máu còn lại`, `Ngoc Ma Phap` → `Ngọc Ma Pháp`, `Phu kien roi` → `Phụ kiện rơi`, nút `Man tiep theo`/`Ve man chon` → `Màn tiếp theo`/`Về màn chọn` |
| Mục tiêu màn | `Assets/Scripts/Stages/StageObjective.cs` | `Quai 12/40` → `Quái 12/40`, `Sinh ton 30/100s` → `Sống sót 30/100s`, `Muc tieu 2/4` → `Mục tiêu 2/4`, `Ho tong 12m/30m` → `Hộ tống 12m/30m`, `Xe ho tong bi pha huy` → `Xe hộ tống bị phá huỷ` |
| Lý do thua | `Assets/Scripts/Stages/StageDirector.cs` | `Tank cua nguoi choi bi pha huy` → `Tank của người chơi bị phá huỷ`, `Khong dat dieu kien man` → `Không đạt điều kiện màn` |
| Chữ mặc định của prefab HUD | `Assets/Editor/HudBuilder.cs` | `1-1 - Ten ai` → `1-1 - Tên ải`, `Dot 1 / 3` → `Đợt 1 / 3`, `Muc tieu` → `Mục tiêu`, `Ngoc: 0` → `Ngọc: 0`, `Da diet: 0` → `Đã diệt: 0`, `DONG DOI CO-OP (1/4)` → `ĐỒNG ĐỘI CO-OP (1/4)`, `Chua co dong doi` → `Chưa có đồng đội`, dòng gợi ý → `WASD di chuyển \| J / K / L / I = kỹ năng \| ESC = cài đặt`, nút `TIEP`/`Choi lai`/`Ve man chon` → `TIẾP`/`Chơi lại`/`Về màn chọn` |
| Tên ải (asset) | `Assets/Data/Stages/Stage_*.asset` | `Suong Mu Tim` → `Sương Mù Tím`, `Hang Da Gai` → `Hang Đá Gai`, `Dau Vet Tu Binh` → `Dấu Vết Tử Binh`, `Vong Xoay Khong Gian` → `Vòng Xoay Không Gian`, `Vanh Dai Bao Lua` → `Vành Đai Bão Lửa`, `Dam Lay Doc Khi` → `Đầm Lầy Độc Khí`, `Dot Kich Tien Don` → `Đột Kích Tiền Đồn`, `Phi Thuyen Hai Mat` → `Phi Thuyền Hai Mặt`, `Chien Ham D1ONK` → `Chiến Hạm D1ONK` |
| Tên boss (thanh máu) | scene `Stage_1_4`, `Stage_2_4`, `Stage_2_5` (field `displayName` của `EnemyBrain`) | `Boss_BongMaTuDia` → `Bóng Ma Tử Địa`, `Boss_TuThanNguyenTo` → `Tử Thần Nguyên Tố`, `Boss_The2Face` → `The2Face`, `Boss_D1ONK` → `D1ONK` |
| Thẻ icon kỹ năng | `Assets/Editor/SkillIconGenerator.cs` | `SAN SANG` → `SẴN SÀNG`, `HOI CHIEU` → `HỒI CHIẾU` (kèm bảng glyph có dấu) |

Hai công cụ mới (menu **Tools ▸ Iron Wasteland**):
- **Localize Stage Text** (`Assets/Editor/StageTextLocalize.cs`): ghi `stageName` cho 9 asset + `displayName` cho 4 boss. Chạy lại lúc nào cũng được, không tạo trùng.
- Glyph có dấu trong `SkillIconGenerator`: font bitmap 5×7 không đủ chỗ cho dấu nên thêm `HighGlyphs` — bảng glyph **cao 10 hàng** (3 hàng dấu + 7 hàng chữ) cho `À`, `Ẵ`, `Ồ`, `Í`, `Ế` và `Đ` (7 hàng). `DrawText` chọn bảng nào có ký tự đó và canh đáy theo số hàng thực tế, nên chữ có dấu và không dấu vẫn thẳng hàng.

### 15.2 Quái đứng yên — nguyên nhân và cách sửa
**Nguyên nhân:** `NormalEnemy_BaseChaser.ScanAndValidateTarget()` (L346) tìm người chơi bằng
`Physics2D.OverlapCircleAll(anchorPointD, radiusC, tankLayer)` — tức **chỉ thấy tank trong bán kính C quanh điểm gốc của prefab**, mặc định `radiusC = 10`. Ngoài bán kính đó `currentTarget = null` và quái rơi vào nhánh `HandleReturnHomeLogic` ⇒ **đứng yên tại chỗ**, chỉ đuổi khi người chơi tự đi vào vùng 10 unit. Đường đi cũng bị giới hạn bởi `PathfindingGrid2D.FindPathToRadiusA(..., radiusC, ...)` nên không thể vượt quá vùng đó.

**Cách sửa (thuần dữ liệu, không sửa code AI):** `Assets/Editor/EnemyAggroFix.cs` — menu **Tools ▸ Iron Wasteland ▸ Fix Enemy Aggro**:
- `radiusC = 60` cho mọi prefab quái đang đuổi (đường chéo bản đồ 37,6 × 21 ≈ 43) ⇒ quái thấy tank ở mọi nơi;
- `autoSetAnchorDFromSpawnPosition = true` để điểm gốc D là **chỗ quái thực sự xuất hiện** (spawner sinh quái ở nhiều vùng, không phải toạ độ tác giả đặt trong prefab).
- Đã sửa **11 prefab**: KIenLuaKhongLo, KySinhThienThach, MatTrangDen, QuaiVatDamLay, QuaiVatTinhThach, SaoDen, TinhLinhSamSetNho, XaThuTinhThe, XenNuoc (+ MatTroiDo, NamDocGaiThach cũng có field này).
- **Không đổi 3 loại đứng yên theo thiết kế**: `MatTroiDo` (nổ), `NamDocGaiThach` (nhả độc), `SaoDen` (hồi máu đồng đội) kế thừa `NormalEnemy_BaseStationary`; `TinhLinhNghiaDia` kế thừa `NormalEnemy_BasePatrol` (đi tuần theo waypoint).

### 15.3 Kiểm chứng
- Compile `0 error CS` sau mọi bước.
- **Đo hành vi:** đưa tank ra (15, 8) — cách mọi vùng sinh quái hơn 20 unit (gấp đôi bán kính cũ). Đọc `currentState` của quái qua SerializedObject: **12/12 quái ở trạng thái `Chasing`**, `dTank` 11–15, `dHome` ≈ 9,5 (đã rời khỏi điểm sinh). Trước khi sửa, khoảng cách đó nằm ngoài `radiusC = 10` nên quái không tạo path và đứng yên.
- **Đo kết quả cuối:** thả tank đứng yên ở (0,0) ở ải 1-1 — quái tự băng qua bản đồ lao vào tank và kích nổ: `kills = 15`, `tank hp = 0/1438`, `state = Failed`. Trước khi sửa, quái ngoài bán kính 10 không bao giờ tới.
- **QA:** `[QA] PASS 9/9` (10:00:38) với tên ải tiếng Việt.
- **Ảnh:** `docs/screenshots/hud_vi_result.png` (HUD + bảng kết quả), `docs/screenshots/stage_select_vi.png` (màn chọn ải), `docs/screenshots/hud_vi_boss.png` (thanh boss `D1ONK` + tên ải).

### 15.4 Còn lại (không nằm trên màn chơi)
- `[Tooltip("…")]` (587 chỗ) và `[Header("…")]` (351 chỗ) trong 213 file vẫn viết không dấu — chỉ hiện trong **Inspector**, không hiện khi chơi. Muốn Việt hoá nốt thì làm theo từng nhóm file để tránh sai dấu.
- Comment trong code và `Debug.Log` trong Console cũng còn không dấu (ví dụ `[StageDirector] Dot 1/2 'W1'`).

### 15.5 Lưu ý cân bằng
Quái giờ đuổi từ mọi khoảng cách nên màn khó hơn rõ rệt so với trước (trước đây chúng đứng chờ). Nếu thấy quá khó, có hai cách chỉnh: giảm `radiusC` xuống ~30 trong `EnemyAggroFix.AggroRadius` (vẫn phủ nửa bản đồ) hoặc tăng `radiusA` (khoảng cách dừng lại khi tấn công) trên prefab quái.

---

## 16. HUD ghép từ bộ art tách sẵn (Deconstructed_Components) — 18/11/2026

### 16.1 Nguyên tắc
Ảnh mockup HUD được tách thành 38 file trong `Assets/Art/UI/GameplayHUD/Deconstructed_Components/` (panels, frames, bar fills, minimap, ornaments). Những ảnh `panel_*` là **ảnh hoàn chỉnh có chữ và thanh đã tô** nên chỉ dùng làm tham chiếu; HUD thật dựng từ `frame_*_empty` + thanh fill + chữ TMP, để mọi con số/tên đều lấy từ dữ liệu đang chạy.

Mỗi panel được dựng ở **kích thước gốc của ảnh** rồi đặt `localScale = 0.5`, nên toạ độ bên trong dùng thẳng pixel của ảnh thiết kế; trên màn hình 1920×1080 panel chiếm đúng tỉ lệ như mockup.

### 16.2 Các panel và dữ liệu động
| Panel | Art | Dữ liệu động |
|---|---|---|
| Người chơi (trên-trái) | `frame_player_vitals_empty` 852×282 + `avatar_player_black_shadow` + `bar_track_health_empty` + `bar_fill_player_health_red` + `bar_fill_player_energy_cyan` | tên xe (`TankDefinition.TankName`), `CẤP n` (`GameController.PlayerLevel`), `MÁU cur / max`, `NĂNG LƯỢNG cur / max`, dòng `ATK / DEF / TỐC ĐỘ` lấy từ `TankBrain.Stats` |
| Dải thông tin màn (dưới panel người chơi) | nền bo góc bán trong suốt | tên ải, `Đợt x / y`, mục tiêu, `Đã diệt`, đồng hồ, `Ngọc` |
| Boss (trên-giữa) | `frame_boss_health_empty` 1364×156 + `bar_fill_boss_health_red` | `BOSS:` + tên boss (`EnemyBrain.DisplayName`), `20.250 / 27.000 HP` (dấu chấm ngăn nghìn), thanh máu theo % |
| Bản đồ chiến thuật (trên-phải) | `frame_tactical_minimap_empty` 488×408 + `minimap_cave_terrain_map` + `minimap_radar_grid` | chấm tank (cyan), quái (tím), boss (đỏ) tính từ toạ độ thật qua `StageBounds.Bounds`; bấm **M** bật/tắt |
| Kỹ năng (dưới-phải) | 4 khung `frame_slot_beveled_*` 250×231 + `badge_key_j/k/l/i` + `overlay_cooldown_dark_dim` | glyph trong suốt của xe, tên kỹ năng (`TankDefinition.skillNames`), trạng thái `SẴN SÀNG` / `HỒI: 3,2 GIÂY`, lớp tối khi hồi chiêu |
| Đồng đội co-op (dưới-trái) | `frame_coop_teammates_empty` 852×303 + `bar_fill_teammate_health_green` | tiêu đề phòng, dòng đồng đội + thanh máu (chưa nối dữ liệu vì chưa có co-op), `[ Slot 2 - 4 Trống ]` |
| Nút ESC | `panel_pause_esc_button` 346×94 | mở/đóng popup Cài đặt |
| Trang trí | `screen_border_scrim_overlay` 1920×1080 + 4 `screen_corner_bracket_*` | — |
| Kết quả / Cài đặt | dựng tay (dim + hộp + TMP) | giữ nguyên hành vi cũ |

### 16.3 File đã sửa/thêm
- `Assets/Scripts/Tanks/TankDefinition.cs`: thêm `skillNames[]` + `skillGlyphs[]`, `GetSkillName(int)`, `GetSkillGlyph(int)`.
- `Assets/Scripts/Tanks/GameController.cs`: thêm `PlayerLevel`.
- `Assets/Scripts/UI/TankStatusHUD.cs`: thêm `portraitImage`, `levelText`, `statsText`, `skillNameTexts[4]`, `skillStateTexts[4]`, `skillGlyphImages[4]`; `RefreshStatsLine()`; trạng thái kỹ năng dạng `HỒI: x,y GIÂY`.
- `Assets/Scripts/UI/StageHUD.cs`: thêm `bossHpText` + `GroupDigits()`.
- `Assets/Scripts/UI/MinimapUI.cs` (mới): vẽ chấm theo `StageBounds`, phím **M**, giới hạn 40 chấm quái.
- `Assets/Editor/SkillIconGenerator.cs`: sinh thêm `*_glyph.png` (256×256, nền trong suốt) và gán `skillNames`/`skillGlyphs`; xe có art riêng (Black Shadow, Leviathan) dùng luôn `Assets/Art/Tank/Sprites/skill/.../icon_*.png`.
- `Assets/Editor/HudBuilder.cs`: viết lại toàn bộ theo bộ art; thêm `ConfigureArtImporters()` ép mọi PNG liên quan thành Sprite **Single** (bộ art gốc để `spriteMode: Multiple` nên `LoadAssetAtPath<Sprite>` trả null → card trắng).

### 16.4 Kiểm chứng
- Compile `0 error CS` sau từng bước.
- `Rebuild GameplayHub` → `Refresh HUD In Scenes` (9/9 scene `director=True gc=True luu=True`).
- QA `[QA] PASS 9/9` (18/11 10:42).
- Ảnh: `docs/screenshots/hud_v4_normal.png` (ải 1-1, 3,2 MB), `docs/screenshots/hud_v4_boss.png` (ải 2-5, 3,1 MB — thanh boss `D1ONK`, `27.000 / 27.000 HP`).

### 16.5 Còn thiếu so với mockup
1. **Nameplate trên đầu tank/quái** (tên + thanh máu nhỏ) — mockup có, chưa làm.
2. **Damage popup art** (`vfx_damage_popup_plus40/minus20`) — hiện vẫn dùng số text của `TextDamageManager`.
3. **Trạng thái `ĐANG CƯỜNG HOÁ`** — chưa có API buff để phát hiện; hiện chỉ có `SẴN SÀNG` / `HỒI`.
4. **Co-op** — panel mới là khung; phòng 1/4 và đồng đội chưa có dữ liệu.
5. **Chân dung** — dùng chung `avatar_player_black_shadow` cho mọi xe (chưa có art riêng từng xe).
6. **Reticle / buff badge / FPS tag** trong `04_Combat_Entities_And_VFX` chưa dùng.

### 16.6 Bổ sung 18/11/2026 — nameplate trên đầu đơn vị
- `Assets/Scripts/UI/UnitNameplates.cs` (mới): nhãn tên + thanh máu nhỏ nổi trên đầu tank/quái/boss, vẽ trên **một canvas Screen Space Overlay** chung (không tạo canvas World Space cho từng đơn vị), chiếu bằng `Camera.WorldToScreenPoint` + `RectTransformUtility.ScreenPointToLocalPointInRectangle`.
- Tên quái lấy `EnemyBrain.DisplayName`; nếu trống thì tra bảng `EnemyNames` (12 prefab: Ký Sinh Thiên Thạch, Sao Đen, Mặt Trăng Đen, Kiến Lửa Khổng Lồ…) rồi mới tách CamelCase. Tank người chơi hiện tên xe + nhãn `BẠN`, boss hiện nhãn `BOSS`.
- Giới hạn: chỉ 6 nhãn quái gần nhất trong bán kính 16 unit; nhãn trùng chỗ được đẩy lên theo bước 26 px; chữ có viền đen (`ShaderUtilities.ID_OutlineWidth = 0.18`).
- `HudBuilder.cs`: thêm component + container `Nameplates` tạo **ngay sau lớp trang trí** để nằm dưới các panel HUD và dưới 2 lớp phủ Kết quả/Cài đặt (nếu tạo cuối, chữ quái sẽ đè lên hộp thoại).
- Sửa thêm 2 lỗi bố cục của 2 lớp phủ: `ResultBox`/`SettingsPanel.Box` phải neo giữa với `anchoredPosition = (0,0)` (trước đặt lệch nửa kích thước nên hộp nằm ngoài màn hình) và toàn bộ chữ/nút bên trong dùng **y âm** (gốc trên-trái của hộp); nút kết quả đang ở `y = +448` nên bị đẩy ra ngoài, đổi thành `-448`.
- Ảnh: `docs/screenshots/hud_v5_normal.png` (3,23 MB), `hud_v5_settings.png` (1,93 MB), `hud_v5_result.png` (1,94 MB). QA vẫn `PASS 9/9`, `Rebuild GameplayHub` + `Refresh HUD In Scenes` 9/9 scene.

---

## 17. Bàn đồ chiến thuật dùng được (18/11/2026)

### 17.1 Yêu cầu
Bộ art HUD được tách thành nhiều mảnh rời (`03_Minimap_Elements/`), nên bàn đồ phải được **gộp lại thành một khối chạy thật**: khung + kim Bắc + lưới + nền + chấm vị trí. Nền bản đồ phải là **nền của chính màn đang chơi**, mũi tên xanh là vị trí tank (và đồng đội nếu có), áp dụng cho **cả 9 màn**.

### 17.2 Ghép các mảnh art
| Mảnh | Dùng làm gì |
|---|---|
| `frame_tactical_minimap_empty.png` 488×408 | khung panel + ô tiêu đề + ô bản đồ rỗng |
| `minimap_north_indicator.png` 64×44 | kim Bắc `N ↑` đặt ở góc trên-phải (bỏ text "N ↑" cũ) |
| `minimap_radar_grid.png` 428×312 | lưới radar phủ lên nền, alpha 0.35 |
| `minimap_blip_player_tank.png` 48×48 | **mũi tên xanh** cho tank người chơi, xoay theo hướng nòng pháo |
| `minimap_blip_teammate_tank.png` 48×48 | mũi tên cho đồng đội (sẵn sàng cho co-op) |
| `minimap_blip_enemy_minion.png` 28×28 | ô vuông tím cho quái |
| `minimap_blip_boss.png` 32×32 | ô vuông đỏ cho boss |
| `minimap_cave_terrain_map.png` 428×312 | nền dự phòng khi màn không có object `Background` |

Mũi tên tank được đặt trên một **chip tròn tối** (bán trong suốt) để vẫn đọc được khi nền bản đồ sáng; mọi chấm đều có bóng đổ.

### 17.3 Nền bản đồ = nền của màn đang chơi
`MinimapUI.ApplyStageBackground()` tìm `SpriteRenderer` có tên `Background` (hoặc `sortingOrder <= -500`) trong scene rồi gán `.sprite` cho `mapBackground`. Nhờ vậy không cần cấu hình riêng cho từng scene: mở màn nào là bản đồ hiện đúng địa hình màn đó (Map 1 / Map 2 / Map 3).

### 17.4 Vị trí và hướng
- Toạ độ thế giới → chuẩn hoá bằng `Mathf.InverseLerp` trên `StageBounds.Bounds` rồi gán vào `anchorMin/anchorMax` của chấm, nên chấm khớp đúng vị trí thật trong map.
- Hướng mũi tên = `TankView.Muzzle.position - tank.transform.position` (fallback `transform.up`), trừ đi góc gốc 45° của ảnh.
- Thứ tự vẽ: quái → tank người chơi → đồng đội, để mũi tên không bị ô vuông quái che.
- `MapView` có `RectMask2D` nên chấm ngoài biên không tràn ra ngoài khung; tối đa 40 chấm quái.

### 17.5 Điều khiển
Bấm **M** để bật/tắt bàn đồ (`MinimapUI.ToggleMap()`, `toggleKey`).

### 17.6 Kiểm chứng
- Compile `0 error CS` sau từng bước.
- `Assets/Editor/MinimapAudit.cs` — menu **Tools ▸ Iron Wasteland ▸ Audit Minimap Scenes**: mở từng scene, kiểm `StageHUD` + `MinimapUI`, 6 field đã gán, `MapView` có `RectMask2D`, in tên sprite nền ⇒ **PASS 9/9**:
  `Stage_1_1..1_4 → Map_1_Hoang_Mac_Tu_Dia_Phang`, `Stage_2_1..2_4 → Map_2_Thung_Lung_Nguyen_To_Phang`, `Stage_2_5 → Map_3_Ky_Ham_D1ONK_Phang`, tất cả `view=428x312`.
- QA màn chơi vẫn `PASS 9/9`.
- Ảnh: `docs/screenshots/hud_v6_minimap_22.png` (ải 2-2, nền dung nham), `docs/screenshots/hud_v6_minimap_11.png` (ải 1-1, nền xám tím), `docs/screenshots/crop_minimap_22.png` (cắt 3× thấy rõ mũi tên xanh + ô vuông tím).

### 17.7 Còn lại
1. Ô bản đồ 428×312 (tỉ lệ 1,37) trong khi map thật 37,6×21 (tỉ lệ 1,79) ⇒ ảnh nền bị kéo dọc; vị trí chấm vẫn đúng vì cùng một phép chuẩn hoá. Muốn đúng tỉ lệ thì phải đổi kích thước `MapView` (và cắt lại khung).
2. Chưa có bấm-lên-bản-đồ để ping/đánh dấu.
3. Đồng đội đã có nhánh vẽ nhưng game chưa có co-op nên chưa bao giờ xuất hiện.

---

## 18. Sửa nền xanh mặc định + khung nhìn trên bản đồ (18/11/2026)

### 18.1 Hai lỗi người dùng báo
1. Các màn vùng 1 vẫn thấy **nền xanh mặc định** của Unity.
2. **Khung xanh trên minimap** không thể hiện vùng đang nhìn của người chơi, chỉ đứng yên.

### 18.2 Nền xanh
- **Nguyên nhân:** camera của cả 9 scene để `Clear Flags = Skybox` (màu trời mặc định xanh `RGBA(0.192,0.302,0.475)`), nên chỗ nào ảnh nền màn không phủ tới là lộ màu trời. Ngoài ra `orthographic size = 9` làm khung hình rộng 32×18 unit trong khi bản đồ chỉ 37,63×21 ⇒ camera gần như không di chuyển và rất dễ lộ mép.
- **Sửa:** `Assets/Editor/StageCameraSetup.cs` (menu **Tools ▸ Iron Wasteland ▸ Setup Cameras**) giờ đặt thêm `cam.clearFlags = CameraClearFlags.SolidColor` và `cam.backgroundColor = #070A0E`, đồng thời đổi `OrthoSize` 9 → **7** để khung hình (24,9×14) nhỏ hơn bản đồ, camera có không gian chạy thật.
- **Kết quả:** cả 9 scene có `orthographic size: 7` và `m_ClearFlags: 2`; phần ngoài bản đồ (nếu có) là màu tối, không còn xanh.

### 18.3 Khung xanh trên minimap
- **Nguyên nhân:** ảnh `minimap_radar_grid.png` có **khung xanh vẽ sẵn ở giữa**, code cũ dùng nguyên ảnh đó làm lưới ⇒ khung nằm chết, không có gì đọc camera.
- **Sửa:**
  - `UISprites.Frame(radius, thickness)` — sprite rỗng chỉ có viền, 9-slice, để kéo giãn khung mà viền không méo.
  - `MinimapUI.DrawViewFrame()` — lấy `orthographicSize × 2` và `aspect` của camera thật, đổi sang toạ độ bản đồ (`InverseLerp` theo `StageBounds.Bounds`), gán `anchorMin/anchorMax` + `sizeDelta` cho `viewFrame`; không có camera thì ẩn khung.
  - `HudBuilder` sinh lưới riêng `Assets/Art/UI/Generated/minimap_grid_only.png` (8 cột × 6 hàng, không có khung) và tạo node `ViewFrame` trong `MapView`.
- **Kết quả:** tank ở tâm ⇒ camera `(0,0)`, khung `anchors (0.50, 0.50)`; đưa tank tới `(16, 8.5)` ⇒ camera `(6.37, 3.50)`, khung `anchors (0.67, 0.67)` — khung bám đúng vùng đang nhìn.

### 18.4 Kiểm chứng
- `error CS: 0` sau mỗi bước.
- YAML 9/9 scene: `orthographic size: 7`, `m_ClearFlags: 2`.
- `[MINIMAP] PASS 9/9` (audit đã thêm kiểm tra field `viewFrame`) và `[QA] PASS 9/9`.
- Ảnh: `docs/screenshots/hud_v7_viewframe_center.png` (khung giữa), `docs/screenshots/hud_v7_viewframe_corner.png` (tank ở góc — khung lệch lên phải, nền không còn xanh).

### 18.5 Còn lại
1. Nhãn tên quái vẫn chồng thành cột khi nhiều quái dồn quanh tank (nameplate chưa có thuật toán xếp hàng ngang).
2. Bản đồ chỉ 37,63×21 nên camera chỉ chạy được ±6,4 / ±3,5 unit ⇒ khung nhìn di chuyển khoảng 1/3 bản đồ. Muốn khung chạy nhiều hơn phải mở rộng bản đồ (scale nền) hoặc giảm orthographic size nữa.

---

## 19. Sửa cảnh báo "objects were not cleaned up" ([PathfindingGrid2D]) — 18/11/2026

### 19.1 Hiện tượng
Khi thoát play mode / đóng scene, Unity báo:

```
Some objects were not cleaned up when closing the scene.
(Did you spawn new GameObjects from OnDestroy?)
The following scene GameObjects were found:
[PathfindingGrid2D]
```

### 19.2 Nguyên nhân
`PathfindingGrid2D.Instance` là singleton tự tạo: nếu chưa có object nào thì nó `new GameObject("[PathfindingGrid2D]")`. Trong lúc Unity đóng scene, `EnemyMovementAgent.OnDisable()` vẫn gọi `PathfindingGrid2D.Instance?.UnregisterEnemy(...)` — lúc đó object lưới đã bị huỷ nên getter **tạo lại một object mới** ngay trong teardown, và object đó không thuộc scene nào để Unity dọn ⇒ cảnh báo trên.

### 19.3 Đã sửa
- `Assets/Scripts/Enemies/Navigation/PathfindingGrid2D.cs`: trong getter `Instance`, nếu `!Application.isPlaying` thì **trả về null** thay vì tạo mới (không sinh object trong teardown/editor).
- `Assets/Scripts/Enemies/Navigation/EnemyMovementAgent.cs`: `OnDisable()` dùng `PathfindingGrid2D.HasInstance` rồi mới gọi `UnregisterEnemy` — không kích hoạt việc tạo mới.
- Các chỗ khác đã đúng từ trước: `NormalEnemy_BaseChaser.OnDisable` và `Skill_EarthWall.OnDisable` đều kiểm `HasInstance`.

### 19.4 Kiểm chứng
- `error CS: 0`.
- Vào play ở `Stage_1_1` rồi thoát: không còn log cảnh báo nào (`console-get-logs sinceSequence` trả rỗng).
- Vào play ở `Stage_2_5` (màn boss), kiểm runtime: `HasInstance=True`, `instance=[PathfindingGrid2D]`, `enemies=4`, `agents=1` ⇒ lưới vẫn được tạo và quái vẫn chạy; thoát play không còn cảnh báo.
