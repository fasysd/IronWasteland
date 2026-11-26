# Hướng dẫn dùng Stage Framework (Phase 1)

> Dành cho người mới với Unity. Mọi thao tác đều ghi rõ **bấm vào đâu**, và giải thích **vì sao**.
> Code tương ứng nằm trong `Assets/Scripts/Stages/` (15 file). 6 file cốt lõi: `StageDefinition`, `StageObjective`, `SpawnZone`, `EnemySpawner`, `StageDirector`, `StageReward`; từ 26/11/2026 có thêm `EnemyWave`, `WaveTimeline`, `ObstacleLayout`, `StageObstacleField`.

---

## 0. Bức tranh tổng thể: 3 khối

```
┌────────────────────┐      đọc mục tiêu      ┌────────────────────┐
│  StageDefinition   │ ─────────────────────► │   StageDirector    │
│  (file .asset)     │                        │  (MonoBehaviour)   │
│  - objectives      │                        │  - đếm giờ         │
│  - reward          │                        │  - xét thắng/thua  │
│  - stageCode       │                        │  - đẩy giờ xuống   │
└────────────────────┘                        └─────────┬──────────┘
                                                        │ TickWaves(dt)
                                            ┌───────────▼────────────┐
                                            │     EnemySpawner       │
                                            │  - waves (đợt quái)    │
                                            │  - spawnZones          │
                                            │  - pool + Clone()      │
                                            └───────────┬────────────┘
                                                        │ vị trí lấy từ
                                            ┌───────────▼────────────┐
                                            │      SpawnZone         │
                                            │  (vùng trên map)       │
                                            └────────────────────────┘
```

- **`StageDefinition`** = *dữ liệu tĩnh*: điều kiện thắng, thưởng, mã màn. Designer sửa trong Inspector, không cần code.
- **`StageDirector`** = *trọng tài*: đếm giờ, xét điều kiện thắng/thua, phát event. Nó **không còn biết** màn có những đợt quái nào.
- **`EnemySpawner`** = *nhà thiết kế wave + cơ bắp*: giữ danh sách đợt quái (`waves`), vùng spawn (`spawnZones`), lịch thời gian (`WaveTimeline`), pool và `Clone()` chỉ số quái.
- **`SpawnZone`** = *địa điểm*: quyết định quái rơi ở đâu trên map.

> **Từ 26/11/2026:** thiết kế đợt quái chuyển từ asset `StageDefinition` sang component `EnemySpawner` trong scene. Lý do: wave và vùng spawn là một việc, sửa cùng một chỗ. Asset chỉ còn mục tiêu/thưởng.

---

## 1. Chuẩn bị scene lần đầu (làm 1 lần cho mỗi scene màn)

Mở scene, kiểm tra đủ các thứ sau. Thiếu cái nào thì quái sẽ không chạy / không đánh / không tìm thấy tank.

| # | Object | Component bắt buộc | Ghi chú |
|---|---|---|---|
| 1 | `Main Camera` | `Camera` (tag `MainCamera`) | Không có thì không thấy gì |
| 2 | `Terrain` | `Collider2D` (Box/Composite…) + **Layer = Terrain (8)** | Đây là thứ để `PathfindingGrid2D` biết chỗ nào đi được |
| 3 | `GameController` | `GameController` (kéo `TankDefinition`, `Player Level`) | Tank được tạo ra ở `Awake` |
| 4 | `[PathfindingGrid2D]` | `PathfindingGrid2D` | Nếu chưa có, hệ thống tự tạo; muốn bake lại: chuột phải component → **Bake / Rebuild Grid** |
| 5 | Tank của người chơi | Layer **Tank (6)**, có `Rigidbody2D` + `Collider2D` + `ColliderOwnerRef` | Prefab tank đã đúng sẵn |
| 6 | Prefab quái | Layer **Enemy (7)**, `EnemyMovementAgent` + `ColliderOwnerRef` | Dùng bộ `Assets/Prefabs/Enemies/Normal/` |

> **Vì sao quan trọng layer?** Quái tìm tank bằng `LayerMask.GetMask("Tank")`, và tìm đường bằng `LayerMask.GetMask("Terrain")`. Sai layer ⇒ quái đứng im hoặc không bao giờ đánh.

---

## 2. Tạo asset màn chơi (`StageDefinition`)

1. Trong cửa sổ **Project**, chuột phải vào `Assets/Settings` (hoặc thư mục bạn muốn) → **Create > Iron Wasteland > Stage > Stage Definition**.
2. Đặt tên file theo quy ước đã chốt: `Stage_1_1`, `Stage_1_2`, … (dấu gạch dưới, không dấu cách).
3. Bấm vào file, xem Inspector. Các nhóm field:

> **Asset này không còn chứa đợt quái.** Từ 26/11/2026, `Waves` nằm trên component `EnemySpawner` trong scene — xem mục 2.4.

### 2.1. `Thong tin ai`

| Field | Ý nghĩa | Ví dụ 1-1 |
|---|---|---|
| **Stage Code** | Mã màn, dùng để so sánh / lưu tiến độ | `1-1` |
| **Stage Name** | Tên hiển thị | `Suong Mu Tim` |

### 2.2. `Dieu kien thang (kieu cu …)`

Chỉ dùng khi bạn **để trống** danh sách `Objectives` (bên dưới). Khi `Objectives` có phần tử, hai field này bị bỏ qua.

| Field | Ý nghĩa |
|---|---|
| **Kill Target** | Diệt đủ bao nhiêu quái thì thắng |
| **Time Limit Seconds** | Thời lượng dự kiến — **chỉ để hiển thị**, không phải điều kiện thua |

### 2.3. `Dieu kien thang (moi)` → `Objectives`

Đây là chỗ khai báo điều kiện thắng thật. Bấm dấu **+** để thêm 1 phần tử, chọn **Type**:

| Type | Field dùng | Nghĩa |
|---|---|---|
| `Eliminate` | `Count` | Diệt đủ `Count` quái |
| `Survive` | `Duration` | Sống đủ `Duration` giây |
| `DestroyTargets` | `Count`, `TargetId` | Phá đủ `Count` vật thể có id `TargetId` |
| `Escort` | `Distance`, `TargetId` | Hộ tống xe id `TargetId` đi đủ `Distance` mét |
| `Radar` | `Count` | Kích hoạt đủ `Count` trạm radar |

> **Nhiều phần tử = phải thỏa TẤT CẢ.** Ví dụ 2-2: 1 dòng `DestroyTargets` (4 cụm nấm) + 1 dòng `Survive` (120 s) ⇒ phải phá đủ 4 cụm **và** sống đủ 120 giây.
>
> Một điều kiện `Escort` bị phá xe ⇒ thua ngay, không cần chờ điều kiện khác.

### 2.4. `Cac dot quai` → nằm trên `EnemySpawner` (không còn trong asset)

Asset `StageDefinition` **không còn** trường `Waves`. Để sửa đợt quái:

1. Mở scene của màn (ví dụ `Assets/Scenes/Stage_1_1.unity`).
2. Chọn object `EnemySpawner`.
3. Trong component `EnemySpawner`, mở danh sách **Waves**.

Mỗi phần tử = 1 đợt. Bấm **+** rồi mở rộng:

| Field | Ý nghĩa |
|---|---|
| **Label** | Tên đợt (chỉ để debug/log) |
| **Start Time** | Giây thứ mấy thì đợt này bắt đầu (tính từ lúc `Begin()`) |
| **Spawns** | Danh sách loại quái trong đợt |

Trong mỗi `Spawns`:

| Field | Ý nghĩa |
|---|---|
| **Enemy** | Kéo prefab quái vào (phải có component kế thừa `EnemyBrain`) |
| **Count** | Sinh bao nhiêu con |
| **Speed Multiplier** | Nhân vào tốc độ chạy. `1` = giữ nguyên, `1.5` = nhanh 1,5 lần (dùng cho "quái cường hóa tốc độ") |
| **Zone Id** | Sinh ở vùng nào. Để trống = vùng bất kỳ |

> **Thứ tự list không quan trọng.** `WaveTimeline` sắp xếp theo `StartTime`, nên W3 (120 s) khai báo trước W1 (0 s) vẫn bắn sau. Hai đợt cùng `StartTime` thì đợt khai báo trước bắn trước.

### 2.5. `Thuong`

| Field | Ý nghĩa | Ví dụ |
|---|---|---|
| **Reward Gems** | Số ngọc khi thắng | `20` (1-1) |
| **Reward Accessory Count** | Số món phụ kiện tối đa | `0` (màn thường), `3` (boss) |
| **Guarantee Accessories** | Tick = chắc chắn đủ số món (không roll 20%) | tick cho boss |
| **Next Stage Code** | Mã màn kế tiếp | `1-2` — **hiện chưa dùng**, Phase 2 (StageSelect) sẽ đọc |

### 2.6. Dựng thử màn 1-1 (đúng số liệu GDD)

> Tạo các đợt dưới đây **trên component `EnemySpawner` trong scene `Stage_1_1`** (mục 2.4), không phải trong asset.

| Wave | Start Time | Enemy | Count | Speed Multiplier |
|---|---|---|---|---|
| W1 | `0` | `Assets/Prefabs/Enemies/Normal/KySinhThienThach.prefab` | 15 | 1 |
| W2 | `60` | `KySinhThienThach` | 15 | 1 |
| W2 | `60` | `Assets/Prefabs/Enemies/Normal/MatTrangDen.prefab` | 5 | 1 |
| W3 | `120` | `KySinhThienThach` | 10 | **1.5** |

- `Objectives` = 1 dòng: `Eliminate`, `Count = 40`.
- `Reward Gems = 20`, `Reward Accessory Count = 0`.

> Tổng spawn là 45 con nhưng chỉ cần diệt 40 — đúng thiết kế "spawn dư một chút", người chơi không phải săn con cuối cùng.

---

## 3. Tạo vùng sinh quái (`SpawnZone`)

1. GameObject → **Create Empty**, đổi tên `SpawnZone_A`, đặt ở rìa map (ví dụ `x = -12, y = 0`).
2. **Add Component** → gõ `SpawnZone`.
3. Chỉnh trong Inspector:
   - **Zone Id** = `A` (vùng B thì `B`…)
   - **Radius** = `5` (bán kính vùng, tính bằng mét)
   - **Gizmo Color** = màu cam cho dễ thấy
4. Chọn object trong Scene view sẽ thấy **vòng tròn gizmo** — kéo `Radius` để vòng tròn to/nhỏ, đó chính là vùng quái rơi.

> Nếu map chỉ có 1 vùng duy nhất thì cứ để `Zone Id` trống ở `Wave.Spawns` — quái rơi vào vùng đầu tiên trong danh sách **Spawn Zones của spawner**.
>
> Muốn quái ra **hai cánh** như màn 1-2: tạo 2 zone `A` (trái) và `B` (phải), rồi trong wave đặt `Zone Id` = `A` cho một dòng `Spawns`, `B` cho dòng khác.

---

## 4. Tạo spawner (`EnemySpawner`)

1. Tạo GameObject rỗng tên `EnemySpawner` (đặt ở giữa map, `0,0`).
2. **Add Component** → `EnemySpawner`.
3. Inspector:

| Field | Nên đặt | Vì sao |
|---|---|---|
| **Max Alive** | `64` | Chặn trường hợp spawn lỗi làm đầy scene. `0` = không giới hạn |
| **Container** | để trống | Trống = quái là con của chính object này (dễ nhìn trong Hierarchy). Muốn gọn hơn: tạo object con `Enemies` và kéo vào |
| **Coop Mode** | bỏ tick | Đợt này không làm co-op (Q5). Nếu bật, **chỉ bật ở Director**, xem mục 5 |
| **Log Events** | tick khi đang phát triển | In log mỗi lần spawn/thu hồi, rất tiện debug; build release thì bỏ tick |
| **Waves** | danh sách đợt quái | Xem mục 2.4 — đây là nơi thiết kế wave |
| **Spawn Zones** | bấm **+**, kéo `SpawnZone_A`, `SpawnZone_B` vào | Spawner cần biết vùng nào để chọn chỗ rơi quái |

### 4.1. Thêm vật cản cho map (`StageObstacleField`)

Component này dựng 5–7 tảng đá khi bấm Play, để map bớt trống và có chỗ né đạn. Không cần đặt tay từng tảng.

1. Chọn object `EnemySpawner` (hoặc object ở giữa map).
2. **Add Component** → `StageObstacleField`.
3. Inspector:

| Field | Ý nghĩa |
|---|---|
| **Stage Code** | Mã màn (`1-1`, `2-3`…). Đổi mã ⇒ đổi seed ⇒ bố cục đá khác. Để trống sẽ lấy tên scene |
| **Obstacle Count** | Số tảng đá. `0` = tắt vật cản |
| **Obstacle Color** | Màu nhuộm cho sprite đá |
| **Log Build** | In số đá đã dựng khi vào màn |

Đá tự động: nằm trong vành cách tâm 4–10 m, cách nhau ≥ 2.2 m, chừa tâm 4 m cho tank xuất phát, và **không lấn vào `SpawnZone`** (quái không sinh ra trong đá). Mỗi tảng ở layer **Terrain** (chặn tank) và được đăng ký với `PathfindingGrid2D` (quái A* né).

> Muốn đá có thể phá: thêm `DestructibleTarget` + `EnemyBrain` cho từng tảng — hiện chưa làm, đây là bài tập mở rộng.

---

## 5. Tạo bộ não màn chơi (`StageDirector`)

1. Tạo GameObject rỗng tên `StageDirector`.
2. **Add Component** → `StageDirector`.
3. Kéo thả vào các ô:

| Ô | Kéo gì vào |
|---|---|
| **Definition** | file `Stage_1_1.asset` vừa tạo |
| **Game Controller** | GameObject `GameController` (có component cùng tên) |
| **Spawner** | GameObject `EnemySpawner` |

**Không còn ô `Spawn Zones` trên Director** — danh sách vùng spawn giờ nằm trên `EnemySpawner` (mục 4).

4. Tuỳ chọn:
   - **Auto Start**: tick (mặc định) = bấm Play là màn tự chạy. Bỏ tick nếu muốn tự gọi `Begin()` từ script khác.
   - **Coop Mode**: bỏ tick. Nếu tick ở đây, Director tự bật co-op cho spawner ⇒ HP quái ×2 (đúng GDD). **Đừng tick cả hai nơi**, sẽ không bị nhân đôi nhưng dễ nhầm khi đọc code.
   - **Fail On Player Death**: tick = tank chết là thua ngay.
   - **Log Events**: tick khi phát triển.

> Director tự tìm `EnemySpawner` / `GameController` nếu bạn quên gán, nhưng **nên gán tay** để tránh nhầm khi scene có nhiều object cùng loại.

---

## 6. Bấm Play và đọc kết quả

Khi chạy, Console sẽ in (nếu bật `Log Events`):

```
[StageDirector] Bat dau ai 1-1 - Suong Mu Tim | 1 dieu kien, 3 dot quai
[EnemySpawner] +KySinhThienThach tai (-11.4, 2.1) (song 1/64)
[StageDirector] Dot 1/3 'W1' - 15 quai tai 0.0s
...
[StageDirector] THANG ai 1-1 sau 94.3s - Ngoc 20, phu kien 0
```

- Thắng: `THANG ai … sau …s - Ngoc …, phu kien …`
- Thua: `THUA ai …: Xe ho tong bi pha huy` hoặc `…: Tank cua nguoi choi bi pha huy`

Trong lúc Play, chọn `StageDirector` để xem các property chỉ đọc: `State`, `Elapsed`, `Kills`, `AliveCount`, `Objectives` (mỗi objective có `Label` + `Progress01`).

---

## 7. Điều kiện thắng đặc biệt: DestroyTargets / Escort / Radar

Ba loại này cần một **vật thể trên map báo về** Director. Phase 1 chỉ có API, chưa có component gắn sẵn (Phase 5 sẽ làm `DestructibleObjective`, `EscortVehicle`, `RadarStation`).

Cách gọi từ script bất kỳ:

```csharp
using IronWasteland.Stages;
using UnityEngine;

public class ViDuBaoVe : MonoBehaviour
{
    [SerializeField] private StageDirector director;   // kéo StageDirector vào
    [SerializeField] private string targetId = "nest";

    // Gọi khi vật thể này bị phá huỷ
    public void BaoDaBiPha() => director?.ReportTargetDestroyed(targetId);

    // Xe hộ tống: gọi mỗi frame hoặc mỗi 1 mét đi được
    public void BaoTienDo(float metDaDi) => director?.ReportEscortProgress("mones", metDaDi);

    // Xe hộ tống bị phá
    public void BaoXeBiPha() => director?.ReportEscortDestroyed("mones");

    // Trạm radar kích hoạt xong
    public void BaoRadarXong() => director?.ReportRadarActivated("radar_1");
}
```

> `TargetId` trong `ObjectiveSpec` phải **khớp đúng chuỗi** với id bạn truyền vào. Sai một ký tự là không bao giờ hoàn thành.

---

## 8. Gắn HUD / âm thanh (chuẩn bị cho Phase 2)

Director phát event, UI/audio chỉ việc nghe — không cần sửa Director:

```csharp
using IronWasteland.Stages;
using UnityEngine;

public class StageEventListener : MonoBehaviour
{
    [SerializeField] private StageDirector director;

    private void OnEnable()
    {
        director.StageStarted   += OnStarted;
        director.KillCountChanged += OnKills;
        director.StageCleared   += OnCleared;
        director.StageFailed    += OnFailed;
    }

    private void OnDisable()
    {
        director.StageStarted   -= OnStarted;
        director.KillCountChanged -= OnKills;
        director.StageCleared   -= OnCleared;
        director.StageFailed    -= OnFailed;
    }

    private void OnStarted(StageDirector d) => Debug.Log($"Man {d.Definition.StageCode} bat dau");
    private void OnKills(int kills) => Debug.Log($"Da diet {kills}");
    private void OnCleared(StageDirector d, StageRewardResult r) => Debug.Log($"Thang, thuong {r}");
    private void OnFailed(StageDirector d, string reason) => Debug.Log($"Thua: {reason}");
}
```

---

## 9. Bảng tra nhanh (reference)

### `StageDirector`

| Thành phần | Dùng để |
|---|---|
| `Begin()` / `Stop()` | Bắt đầu / dừng màn (dùng khi tự làm flow chuyển scene) |
| `Tick(float dt)` | Đẩy thời gian bằng tay — chỉ dùng cho test, gameplay đã tự gọi trong `Update` |
| `State` | `Idle`, `Running`, `Cleared`, `Failed` |
| `Kills`, `Elapsed`, `AliveCount` | Số liệu để vẽ HUD |
| `Objectives` | Danh sách điều kiện + `Label`, `Progress01` |
| Event `StageStarted`, `WaveStarted`, `KillCountChanged`, `StageCleared`, `StageFailed` | HUD, âm thanh, chuyển màn. `WaveStarted` phát `(int index, EnemyWave wave)` — Director chỉ chuyển tiếp từ spawner |
| `ReportTargetDestroyed`, `ReportRadarActivated`, `ReportEscortProgress`, `ReportEscortDestroyed` | Vật thể mục tiêu báo về |

### `EnemySpawner`

| Thành phần | Dùng để |
|---|---|
| `Waves`, `SpawnZones`, `TotalWaves`, `TotalEnemyCount`, `WaveIndex` | Thiết kế màn: đợt quái + vùng rơi quái |
| `BeginWaves()` / `TickWaves(dt)` / `StopWaves()` | Chạy lịch wave; Director gọi 3 hàm này, không tự bắn |
| `ConfigureDesign(waves, zones)` | Gán dữ liệu wave/zone bằng code (dùng khi dựng scene tự động) |
| `Spawn(prefab, position, speedMultiplier)` | Sinh 1 quái (tự `Clone()` stats + áp co-op) |
| `Prewarm(prefab, count)` | Tạo sẵn object cho đỡ giật lúc mới vào màn |
| `Despawn(enemy)` / `DespawnAll()` | Thu hồi về pool |
| `AliveCount`, `IsFull` | Kiểm tra trần quái |

### `StageObstacleField`

| Thành phần | Dùng để |
|---|---|
| `Build()` | Dựng vật cản (tự gọi trong `Awake`) |
| `Stage Code`, `Obstacle Count`, `Obstacle Color` | Thiết kế vật cản trong Inspector |

### `WaveTimeline` (lớp thuần C#, không phải MonoBehaviour)

| Thành phần | Dùng để |
|---|---|
| `Advance(dt)` | Cộng thời gian |
| `TryTakeDue(out int)` / `TakeDue(List<int>)` | Lấy các đợt đã tới giờ |
| `Reset()` | Chạy lại màn |

### `SpawnZone`

| Thành phần | Dùng để |
|---|---|
| `ZoneId`, `Radius`, `Center` | Thông tin vùng |
| `Matches(id)` | Vùng này có phục vụ id không |
| `RandomPoint()` | 1 điểm ngẫu nhiên trong vùng |

---

## 10. Sự cố thường gặp

| Triệu chứng | Nguyên nhân | Cách sửa |
|---|---|---|
| Không có quái nào xuất hiện | Quên gán `Spawner`; hoặc `Start Time` của wave lớn hơn thời gian đã chạy; hoặc `Enemy` trong `Spawns` bị trống | Xem Console có `Dot 1/N ...` chưa; kiểm tra `Enemy` |
| Quái sinh ra nhưng **đứng im** | `Terrain` thiếu `Collider2D` hoặc sai layer 8; `[PathfindingGrid2D]` chưa bake; prefab thiếu `EnemyMovementAgent` | Đặt layer, bấm **Bake / Rebuild Grid**, dùng prefab trong `Normal/` |
| Quái chạy nhưng **không đánh** | Tank không ở layer `Tank (6)`; tank thiếu `Collider2D`/`ColliderOwnerRef` | Kiểm tra prefab tank + layer |
| Bấm Play mà màn không chạy | `Auto Start` bị bỏ tick | Tick lại, hoặc gọi `Begin()` từ script |
| **Thắng ngay lập tức** | `Objectives` rỗng **và** `Kill Target = 0`; hoặc `Count` của objective = 0 | Đặt `Count ≥ 1` |
| HP quái **tăng dần** qua từng đợt | Không dùng `EnemySpawner` mà tự `Instantiate` và truyền chung 1 `EnemyStats` | Luôn spawn qua `EnemySpawner` (nó `Clone()` mỗi lần) |
| Console báo `Transform child has another parent` | Prefab tank hỏng cấu trúc (đã sửa 3 prefab BlackShadow/RedNova/Leviathan ngày 16/11) | Nếu xuất hiện lại: mở Prefab, kéo `_View` về đúng cha, Save |
| Warning `Parameter 'Hash ...' does not exist` | Animator của quái thiếu parameter (lỗi asset có sẵn) | Không ảnh hưởng gameplay; sửa Animator Controller khi rảnh |
| Scene bị dấu `*` (dirty) sau khi test | Script test tạo/xoá object trong scene | Bấm Ctrl+S hoặc File → Revert nếu chưa muốn lưu |

---

## 11. Những thứ **chưa có** (đừng đi tìm)

| Thiếu gì | Khi nào có |
|---|---|
| Camera follow, HUD (máu, năng lượng, tiến độ), màn chọn ải | Phase 2 |
| SFX (kỹ năng, trúng đích, nổ) | Phase 3 |
| 6 asset `Stage_*` + 6 scene `Stage_X_Y` thật | Phase 4 (1-1, 1-2, 2-1) và Phase 5 (1-3, 2-2, 2-3) |
| `DestructibleObjective`, `EscortVehicle`, `RadarStation` (component gắn map) | Phase 5 |
| Boss brain + 3 màn boss 1-4 / 2-4 / 2-5 | Phase 6 (đang tạm hoãn) |
| Lưu ngọc / phụ kiện vào profile | chưa nằm trong đợt này |
| Đọc `Next Stage Code` để chuyển màn | Phase 2 (StageSelect) |

---

## 12. Vòng lặp làm việc gợi ý

1. Tạo asset `Stage_1_1` với **1 wave duy nhất, 3 con quái** → bấm Play cho chắc hệ thống chạy.
2. Tăng dần số wave, thêm zone B, thêm speed multiplier.
3. Đổi `Objectives` sang `Survive` → thử lại (không cần quái cũng thắng được sau N giây).
4. Khi số liệu đã đúng, mới gắn HUD/SFX (Phase 2–3).
5. Trước khi commit: bỏ tick `Log Events` ở cả Director và Spawner.
