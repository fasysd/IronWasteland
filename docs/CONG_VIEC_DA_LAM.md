# IronWasteland — Tổng kết công việc đã làm

> Tài liệu ghi lại toàn bộ những gì đã thực hiện cho nhiệm vụ "9 màn chơi + Spawner quái + SFX + HUD",
> kèm số liệu kiểm chứng và danh sách file. Kế hoạch gốc chi tiết nằm ở
> [`docs/PLAN_Stages_Spawner_SFX.md`](PLAN_Stages_Spawner_SFX.md) (19 mục, ~1.225 dòng).
>
> Cập nhật lần cuối: **19/11/2026** · Nhánh Git: `Stage` · Commit gần nhất: `e79776e`

---

## 0. Mục lục

1. [Nhiệm vụ được giao](#1-nhiệm-vụ-được-giao)
2. [Kết quả tổng quan](#2-kết-quả-tổng-quan)
3. [Chín màn chơi](#3-chín-màn-chơi)
4. [Framework màn chơi](#4-framework-màn-chơi)
5. [HUD và giao diện](#5-hud-và-giao-diện)
6. [Bàn đồ chiến thuật](#6-bàn-đồ-chiến-thuật)
7. [Camera và giới hạn biên](#7-camera-và-giới-hạn-biên)
8. [Âm thanh (SFX)](#8-âm-thanh-sfx)
9. [Art và Skill Icon](#9-art-và-skill-icon)
10. [Công cụ Editor](#10-công-cụ-editor)
11. [Lỗi đã tìm ra và cách sửa](#11-lỗi-đã-tìm-ra-và-cách-sửa)
12. [Việt hoá chữ hiển thị](#12-việt-hoá-chữ-hiển-thị)
13. [Kiểm chứng](#13-kiểm-chứng)
14. [Trạng thái Git](#14-trạng-thái-git)
15. [Việc còn lại](#15-việc-còn-lại)
16. [Cách vận hành nhanh](#16-cách-vận-hành-nhanh)

---

## 1. Nhiệm vụ được giao

Nguyên văn (rút gọn):

> "Xây dựng các màn theo tài liệu + thiết kế điều kiện thắng. Mỗi màn sẽ là một scene riêng. Khi bắt đầu
> scene thì sẽ dựa trên thông tin Tank (giống GameController hiện tại) thì cần tạo ra các Tank dựa vào dữ
> liệu đó. Các quái sẽ cần dùng một Spawner để sinh quái thay vì đặt sẵn ở trên map. Và kiếm SFX kích hoạt
> khi thi triển kỹ năng, gây sát thương trúng mục tiêu,… cho tank và enemy. SFX dùng chung cho nên không
> cần kiếm riêng cho mỗi đối tượng, nếu được thì kiếm 2–3 cái để phát ngẫu nhiên khi kích event cho đỡ lặp.
> Đọc tài liệu dự án cùng các code github và lên plan chi tiết cho các nhiệm vụ này cần làm những gì vào
> 1 file .md."

### 1.1. Phạm vi đã chốt với nhóm trưởng

| Mã | Vấn đề | Quyết định |
|----|--------|-----------|
| Q1 | Có refactor `GameController` (tách PlayerLoadout / PlayerTankSpawner / PlayerInputController)? | **Không** — giữ nguyên để không ảnh hưởng phần người khác đang làm |
| Q2 | Vật thể hộ tống / phá được kế thừa gì? | **Kế thừa `EnemyBrain`** để không phải sửa file skill nào |
| Q3 | Dựng prefab The2Face? | Chưa làm trong đợt này |
| Q4 | Viết boss brain riêng? | **Không** — dùng brain quái có sẵn, phóng to + ghi đè chỉ số |
| Q5 | Làm co-op? | Không — chỉ để khung panel |
| Q6 | Tên scene? | `Stage_X_Y` (ví dụ `Stage_1_1`) |
| Q7 | Có màn chọn ải? | **Có** — `StageSelect` |

### 1.2. Yêu cầu bổ sung phát sinh trong quá trình làm

| # | Yêu cầu | Kết quả |
|---|---------|---------|
| 1 | Chữ trên màn chơi phải có dấu tiếng Việt | Việt hoá HUD, mục tiêu, bảng kết quả, tên ải, tên boss, nameplate quái |
| 2 | Quái đứng yên, chỉ đuổi khi người chơi lại gần | Tăng bán kính phát hiện 11 prefab quái từ 10 → 60 unit |
| 3 | UI quá xấu so với thiết kế | Dựng lại HUD / StageSelect / Cài đặt / Kết quả theo wireframe, sau đó ghép bộ art tách sẵn |
| 4 | Ghép bộ art tách từ mockup thành UI, chữ phải đúng dữ liệu runtime | HUD v5 → v7 + nameplate |
| 5 | Bàn đồ chiến thuật phải dùng được thật | Nền là nền của màn, mũi tên tank/đồng đội, khung thể hiện vùng camera nhìn |
| 6 | Màn vùng 1 còn nền xanh mặc định; vòng xanh minimap đứng yên | Camera SolidColor `#070A0E`, ortho 9 → 7; khung nhìn đọc camera thật |
| 7 | Tạo nhánh git `Stage` và đẩy lên | Đã push 2 commit (`8b4e0fc`, `e79776e`) |
| 8 | Cảnh báo `objects were not cleaned up … [PathfindingGrid2D]` | Chặn singleton tự tạo object khi thoát play |
| 9 | Ký Sinh Thiên Thạch chết vẫn hiện tên | Thêm `ShowsNameplate`, quái đã kích nổ thì ẩn nameplate |
| 10 | Tích hợp bộ Skill Icon mới cho các tank | Gán 24 icon mới, thêm lớp `Card` 250×231 vào ô kỹ năng |

---

## 2. Kết quả tổng quan

| Hạng mục | Kết quả |
|----------|---------|
| Số màn đã dựng | **9 / 9** (`Stage_1_1` … `Stage_2_5`) + `StageSelect` |
| Framework màn chơi | 11 script trong `Assets/Scripts/Stages/` |
| Quái | 100% sinh bằng `EnemySpawner` + `SpawnZone`, không đặt sẵn |
| SFX | 39 file WAV (13 nhóm × 3 biến thể) + `SfxLibrary.asset` |
| HUD | 8 panel ghép từ 38 PNG art tách sẵn + nameplate + minimap |
| Công cụ Editor | 13 menu trong `Tools ▸ Iron Wasteland` |
| Biên dịch | `0 error CS` ở mọi bước |
| QA | `[QA] PASS 9/9`, `[MINIMAP] PASS 9/9` |
| Ảnh minh chứng | 34 PNG trong `docs/screenshots/` |

---

## 3. Chín màn chơi

| Mã | Tên ải | Điều kiện thắng | Đợt | Ngọc | Kết quả test |
|----|--------|-----------------|-----|------|--------------|
| 1-1 | Sương Mù Tím | Diệt 40 quái | 3 | 20 | Thắng (41/40 khi chơi thật) |
| 1-2 | Hang Đá Gai | Sống sót 100 giây | 3 | 30 | Thắng |
| 1-3 | Dấu Vết Tử Binh | Kích hoạt 2 radar + hộ tống xe 30 m | 4 | 40 | Thắng — 46,6 s |
| 1-4 | Vòng Xoay Không Gian | Phá 3 Mạch Năng Lượng (Bóng Ma Tử Địa) | 2 | 60 | Thắng — 20,7 s |
| 2-1 | Vành Đai Bão Lửa | Diệt 50 quái | 2 | 50 | Thắng |
| 2-2 | Đầm Lầy Độc Khí | Phá 4 tổ nấm + sống sót 120 giây | 3 | 60 | Thắng — 142,6 s |
| 2-3 | Đột Kích Tiền Đồn | Phá 3 trụ điện + hộ tống xe 30 m | 3 | 70 | Thắng — 21,4 s |
| 2-4 | Phi Thuyền Hai Mặt | Phá 2 Mạch Năng Lượng (Tử Thần Nguyên Tố + The2Face) | 2 | 90 | Thắng — 21,2 s |
| 2-5 | Chiến Hạm D1ONK | Phá 3 Mạch Năng Lượng (D1ONK) | 2 | 120 | Thắng — 23,5 s |

### 3.1. Ba màn mục tiêu đặc biệt

- **1-3 (Dấu Vết Tử Binh)** — 2 trạm radar tại `(-9, 5)` và `(10, -5)`; xe hộ tống `mones` đi từ `(-15, -1)` → `(15, -1)`.
  Radar chỉ tính khi: tank trong **3 m**, **không có quái trong 8 m**, giữ liên tục **5 giây**.
- **2-2 (Đầm Lầy Độc Khí)** — 4 tổ nấm (`nest`) dùng prefab Nấm Độc Gai Thạch đặt ở bốn góc `±10/±5`, đồng thời phải sống sót 120 giây.
- **2-3 (Đột Kích Tiền Đồn)** — 3 trụ điện (`tower`) dùng prefab Sao Đen + xe phá thành `ram` hộ tống 30 m.

### 3.2. Ba màn boss

Boss dùng chính prefab `QuaiVatTinhThach` (có AI sẵn) phóng to; "Mạch Năng Lượng" là prefab `SaoDen` gắn
`DestructibleTarget` với id `core`.

| Màn | Boss | Máu | Tấn công | Giáp | Tốc độ | Số Mạch |
|-----|------|-----|----------|------|--------|---------|
| 1-4 | Bóng Ma Tử Địa | 2.000 | 80 | 45 | 90 | 3 |
| 2-4 | Tử Thần Nguyên Tố | 9.500 | 250 | 25 | 90 | 2 |
| 2-4 | The2Face | 16.000 | 350 | 30 | 100 | — |
| 2-5 | D1ONK | 27.000 | 180 | 60 | 60 | 3 |

Cân bằng đã chỉnh: Bóng Ma Tử Địa `Attack 120 → 80` (tank cấp 12 chỉ còn 61/1856 máu),
D1ONK `Attack 500 → 180`, `MoveSpeed 70 → 60`, The2Face `Attack 150 → 110`, `MoveSpeed 80 → 60`.

---

## 4. Framework màn chơi

Mỗi scene chỉ chứa: tank người chơi (do `GameController` tạo từ `TankDefinition`), object `Background`,
`StageDirector` (gắn `StageDefinition`) và các object đặc biệt của màn (radar, xe hộ tống, mục tiêu phá huỷ, boss).
Quái hoàn toàn do Spawner sinh từ các `SpawnZone` khai báo trong asset.

| File | Vai trò |
|------|---------|
| `Assets/Scripts/Stages/StageDefinition.cs` | ScriptableObject chứa dữ liệu màn: tên ải, điều kiện thắng, đợt quái, phần thưởng, màn kế, cấp tank đề xuất |
| `Assets/Scripts/Stages/StageObjective.cs` | Lớp trừu tượng + 5 mục tiêu: `Eliminate`, `Survive`, `DestroyTargets`, `Escort`, `Radar` |
| `Assets/Scripts/Stages/EnemySpawner.cs` | Sinh quái theo đợt: pool tái dùng, giới hạn số sống, lọc theo vùng, `Stats.Clone()` mỗi lần sinh |
| `Assets/Scripts/Stages/SpawnZone.cs` | Vùng sinh quái có id để đợt quái chỉ định |
| `Assets/Scripts/Stages/StageDirector.cs` | Điều phối trận: thời gian, các đợt, tiến độ mục tiêu, thắng/thua, trao thưởng |
| `Assets/Scripts/Stages/StageReward.cs` | Kết quả trận: ngọc, phụ kiện rơi, thời gian, số quái diệt |
| `Assets/Scripts/Stages/StageProgress.cs` | Lưu tiến độ bằng PlayerPrefs: `iw_gems`, `iw_stage_unlocked_<code>`, `iw_stage_cleared_<code>` |
| `Assets/Scripts/Stages/DestructibleTarget.cs` | Mục tiêu phá huỷ (tổ ong, trụ điện, Mạch Năng Lượng) — kế thừa `EnemyBrain` |
| `Assets/Scripts/Stages/EscortVehicle.cs` | Xe hộ tống theo waypoint, bị quái đánh, chốt tiến độ khi tới đích |
| `Assets/Scripts/Stages/RadarStation.cs` | Trạm radar giữ 5 giây với điều kiện tank trong 3 m, không quái trong 8 m |
| `Assets/Scripts/Stages/BossTag.cs` | Đánh dấu boss để HUD hiện thanh máu riêng + nameplate cỡ lớn |

### 4.1. Cơ chế sinh quái

- Mỗi lần sinh, Spawner gọi `prefab.Stats.Clone()` **trước** `Initialize()`. Nếu không, `EnemyBrain.Initialize`
  gán tham chiếu vào asset gốc và `SetCoopMode()` nhân máu dồn qua từng lần sinh.
- Đợt quái khai báo: thời điểm bắt đầu (giây), loại quái, số lượng, vùng sinh (`ZoneId`), hệ số tốc độ.
- Có giới hạn số quái sống cùng lúc (`maxAlive`); object được tái dùng khi brain không tự huỷ.
- Quái thường tự `Destroy(gameObject)` trong `OnDeath()`, nên pool chủ yếu dùng cho mục tiêu/boss.

---

## 5. HUD và giao diện

HUD trải qua **7 vòng phát triển**: 3 vòng dựng tay theo wireframe SVG, từ vòng 4 ghép từ bộ art tách sẵn
trong `Assets/Art/UI/GameplayHUD/Deconstructed_Components` (38 PNG). Mỗi panel dựng đúng kích thước gốc của
ảnh rồi thu nhỏ `localScale = 0.5` để khớp bố cục 1920×1080.

| Panel | Kích thước gốc | Dữ liệu động |
|-------|----------------|--------------|
| `TopLeft` — chân dung người chơi | 852×282 | Tên xe, CẤP, MÁU x/y, NĂNG LƯỢNG x/y, ATK / DEF / TỐC ĐỘ |
| `TopLeftInfo` — dải thông tin màn | 560×116 | Tên ải, Đợt i/n, mục tiêu, số quái đã diệt, đồng hồ, ngọc |
| `BossPanel` — thanh máu boss | 1364×156 | Tên boss, `20.250 / 27.000 HP` |
| `TopRight` — bàn đồ chiến thuật | 488×408 | Nền thật của màn, lưới, kim Bắc, khung vùng camera, chấm quái/boss/tank |
| `BottomRight` — kỹ năng trang bị | 1050×282 | 4 thẻ 250×231: art kỹ năng, phím J/K/L/I, tên, `SẴN SÀNG` / `HỒI: x GIÂY` |
| `BottomLeft` — đồng đội co-op | 852×303 | Khung phòng 1/4 và slot trống (chưa có co-op thật) |
| `ResultPanel` — kết quả trận | 960×540 | THẮNG/THẤT BẠI, thời gian, số quái, % máu còn, ngọc nhận, phụ kiện rơi |
| `SettingsPanel` — cài đặt (ESC) | 900×660 | Hai thanh âm lượng, bảng phím, nút quay lại / rời trận |

**Nameplate** (`Assets/Scripts/UI/UnitNameplates.cs`): vẽ trên một canvas phủ toàn màn hình, tên + thanh máu
nổi trên đầu tank/quái/boss; tối đa **6 nhãn** trong bán kính **16 m**; nhãn trùng chỗ đẩy lên **26 px**.
Tank hiện tên xe + nhãn `BẠN`, boss + nhãn `BOSS`, quái dùng bảng Việt hoá 12 loại.

**Bảng kết quả**: tiêu đề THẮNG / THẤT BẠI, lý do thua, thời gian hoàn thành, số quái đã diệt, phần trăm máu
còn lại, ngọc nhận được (tổng), số phụ kiện rơi; nút Màn tiếp theo / Chơi lại / Về màn chọn.

---

## 6. Bàn đồ chiến thuật

`Assets/Scripts/UI/MinimapUI.cs`:

- **Nền**: lấy đúng `Sprite` của `SpriteRenderer` tên `Background` trong màn đang chơi ⇒ 1-1…1-4 hiện Map 1,
  2-1…2-4 hiện Map 2, 2-5 hiện Map 3.
- **Chấm**: mũi tên cyan = tank người chơi (xoay theo hướng nòng pháo `tank.View.Muzzle`), ô vuông tím = quái,
  ô đỏ = boss; mỗi chấm có bóng đổ để đọc được trên nền sáng; tối đa 40 chấm quái.
- **Khung nhìn**: `ViewFrame` là khung cyan đọc `orthographicSize × 2` và tỉ lệ khung hình của camera thật,
  quy đổi sang toạ độ bản đồ bằng `InverseLerp` theo `StageBounds` ⇒ tank đi tới đâu khung chạy theo tới đó.
- **Bật/tắt**: phím **M** gọi `ToggleMap()`.
- **Lưới**: sinh riêng `Assets/Art/UI/Generated/minimap_grid_only.png` (428×312, 8 cột × 6 hàng) vì ảnh lưới
  gốc có khung xanh vẽ sẵn nằm chết.
- **Kiểm tra tự động**: `Tools ▸ Iron Wasteland ▸ Audit Minimap Scenes` → `[MINIMAP] PASS 9/9`.

---

## 7. Camera và giới hạn biên

- `Assets/Scripts/Core/StageBounds.cs` — đọc kích thước object `Background` (37,63 × 21 unit), kẹp vị trí tank
  trong biên với lề 1,6 unit, đồng thời đẩy tank ra khỏi vùng HUD che ở góc dưới-phải. Tự tạo object mỗi scene
  bằng `[RuntimeInitializeOnLoadMethod]`.
- `Assets/Scripts/Core/CameraFollow2D.cs` — thêm `SetBounds()` / `ClearBounds()` để camera không chạy ra ngoài nền.
- `Assets/Editor/StageCameraSetup.cs` (**Setup Cameras**) — đặt cho cả 9 scene:
  - `Orthographic size = 7` (trước là 9): khung hình 24,9 × 14 nhỏ hơn bản đồ nên camera có không gian di chuyển thật.
  - `Clear Flags = SolidColor`, `Background = #070A0E`: hết lộ màu xanh skybox mặc định của Unity.

---

## 8. Âm thanh (SFX)

Bộ âm thanh dùng chung, không gắn riêng từng đối tượng: **39 file WAV** chia **13 nhóm**, mỗi nhóm **3 biến thể**
phát ngẫu nhiên cho đỡ lặp, kèm asset `SfxLibrary`.

| Thành phần | Vai trò |
|-----------|---------|
| `Assets/Scripts/Audio/SfxCategory.cs` | 13 nhóm: bắn thường, kỹ năng, trúng đích, nổ, chết, hồi máu, khiên, radar, thắng, thua… |
| `Assets/Scripts/Audio/SfxLibrary.cs` + `Assets/Audio/SfxLibrary.asset` | Bảng tra clip theo nhóm, mỗi nhóm nhiều biến thể |
| `Assets/Scripts/Audio/AudioManager.cs` | Phát âm thanh, âm lượng tổng + âm lượng SFX riêng, chống phát trùng |
| `Assets/Scripts/Audio/TankSfxHooks.cs` | Tank: bắn, thi triển kỹ năng, trúng đích, chết |
| `Assets/Scripts/Audio/EnemySfxHooks.cs` | Quái: bắn, **trúng đích (qua `EnemyBrain.OnTakeDamageEvent`)**, chết, nổ |
| `Assets/Scripts/Audio/StageSfxHooks.cs` | Màn chơi: bắt đầu, hoàn thành mục tiêu, thắng, thua |

> **Lưu ý kỹ thuật:** phần lớn skill tank gọi `enemy.TakeDamage(float)` nên sự kiện "gây sát thương" của tank
> không bắn; vì vậy âm thanh trúng đích được gắn vào `EnemyBrain.OnTakeDamageEvent` để luôn có tiếng khi đánh trúng.

---

## 9. Art và Skill Icon

### 9.1. Bộ art HUD tách sẵn

38 PNG trong `Assets/Art/UI/GameplayHUD/Deconstructed_Components/` gồm 5 nhóm: panel (6), khung + thanh máu,
thành phần bản đồ, hiệu ứng chiến đấu, viền màn hình bốn góc. Các ảnh `panel_*` là ảnh ghép **đã in sẵn chữ**
nên chỉ dùng tham chiếu; HUD dựng lại từ `frame_*_empty` + `bar_fill_*` + chữ động.

Bộ art gốc để `spriteMode: Multiple` (Sprite) nên `LoadAssetAtPath<Sprite>` trả `null` — `ConfigureArtImporters()`
trong `HudBuilder` ép **Sprite Single + PPU 100 + ForceSynchronousImport** trước khi dùng.

### 9.2. Bộ Skill Icon mới

Mỗi ảnh là một **thẻ art hoàn chỉnh** (khung đá + rune, 512×512; riêng Black Shadow 1024×1024):

| Xe | Thư mục | Bốn icon theo slot J / K / L / I |
|----|---------|----------------------------------|
| BlackShadow | `Assets/Art/Tank/Sprites/skill/blackShadow/` | `icon_shadow_claw` · `step` · `predator` · `ambush` |
| GreenFox | `Assets/Art/Tank/Sprites/GFSkill/` | `icon_fox_fang_master` · `fox_mark` · `fox_fire` · `fox_storm` |
| WaterB | `Assets/Art/Tank/Sprites/WBSKILL/` | `icon_healing_water_master` · `bubble_shield` · `water_wave` · `water_return` |
| RedNova | `Assets/Art/Tank/Sprites/RedNova/` | `icon_nova_shot_master` · `triple_nova` · `laser_cannon` · `nova_barrage` |
| WallTitan | `Assets/Art/Tank/Sprites/WallTitan/` | `icon_earth_break_master` · `earth_wall` · `earth_prison` · `earthquake` |
| Leviathan | `Assets/Art/Tank/Sprites/skill/leviathan/` | `icon_water_pulse_master` · `water_bubble` · `water_armor` · `tidal_return` |

Cách gán: menu **Tools ▸ Iron Wasteland ▸ Integrate New Skill Icons** (script `Assets/Editor/SkillIconIntegrator.cs`) —
ép import, gán `skillIcons[4]` cho từng `TankDefinition`, xoá `skillGlyphs` / `skillIconsCooldown` cũ (art đã có
khung riêng) và tắt cờ `skillIconsIncludeLabels` để HUD tự vẽ phím, tên, trạng thái. Đã chạy: `xong 6/6 xe`.

HUD có thêm node `Card0..3` (250×231) trong mỗi ô kỹ năng để chứa thẻ art này; lớp `Icon{i}` (glyph) tự ẩn khi
xe không khai báo glyph.

---

## 10. Công cụ Editor

Menu **Tools ▸ Iron Wasteland** — 13 công cụ:

| Menu | Chức năng |
|------|-----------|
| `Generate Skill Icons` | Sinh bộ icon dạng thẻ (250×231) + glyph trong suốt cho 5 xe chưa có art, kèm phím/tên/trạng thái |
| `Assign Skill Icons To Tanks` | Gán icon đã sinh vào các `TankDefinition` |
| `Integrate New Skill Icons` | Gán bộ icon art mới (thẻ vuông) cho cả 6 xe, tự cấu hình import |
| `Rebuild GameplayHub` | Dựng lại toàn bộ prefab `StageHUD` từ bộ art tách sẵn |
| `Refresh HUD In Scenes` | Thay HUD trong 9 scene, nối lại `StageDirector` / `GameController` (**phải chạy khi KHÔNG ở play mode**) |
| `Build Settings Panel` | Dựng popup Cài đặt (ESC) đúng chữ trong wireframe 05 |
| `Setup Cameras` | Đặt orthographic size, Clear Flags, màu nền cho camera 9 scene |
| `Audit Minimap Scenes` | Kiểm tra từng màn có đủ `MinimapUI`, field, mask; in tên sprite nền |
| `QA Audit Stages` | Kiểm tra mục tiêu, đợt quái, prefab hợp lệ, mã màn kế, scene chứa asset |
| `Fix Stage QA` | Tự sửa lỗi QA: thêm mục tiêu cho 1-1, nối chuỗi `1-2 → 1-3`, `2-1 → 2-2`, `2-3 → 2-4` |
| `Fix Enemy Aggro` | `radiusC = 60` + `autoSetAnchorDFromSpawnPosition` cho 11 prefab quái |
| `Localize Stage Text` | Ghi tên ải (9) và tên boss (4) tiếng Việt có dấu vào asset/scene |
| `Reskin UI` | Bản reskin đầu tiên theo bảng màu Figma (đã được `HudBuilder` thay thế ở các vòng sau) |

---

## 11. Lỗi đã tìm ra và cách sửa

| Triệu chứng | Nguyên nhân | Cách sửa |
|-------------|-------------|----------|
| Màn 1-1 kẹt 37/40 quái, không thể thắng | Ký Sinh Thiên Thạch tự nổ rồi `Destroy(gameObject)` mà không đi qua `Die()` nên không bắn `OnDeathEvent`, director không đếm kill | `if (IsAlive) Die(); else Destroy(gameObject);` |
| Skill bấm không có tác dụng, không hồi năng lượng | `GameController.targetCamera` null ⇒ không `SetLookTarget` ⇒ `FireNormalSkill` thoát sớm | Gán Main Camera vào `GameController` |
| Tank sinh ra ở giữa hai boss rồi chết ngay | `spawnPosition` là `Vector2` nhưng bị ghi bằng `vector3Value` nên không lưu | Ghi bằng `vector2Value = (0, -7)` |
| Xe hộ tống tới đích nhưng mục tiêu không hoàn thành | Xe dừng cách waypoint cuối 0,15 m nên chỉ báo 29,9/30 m | Chốt `m_Travelled = m_PathLength` khi tới đích và báo lại tiến độ |
| Quái đứng yên, chỉ đuổi khi lại gần | `ScanAndValidateTarget` dùng `OverlapCircleAll(anchorPointD, radiusC, tankLayer)` với `radiusC = 10` | `Fix Enemy Aggro`: `radiusC = 60`, `autoSetAnchorDFromSpawnPosition = true` cho 11 prefab |
| Cảnh báo `objects were not cleaned up … [PathfindingGrid2D]` | Singleton `Instance` tự tạo GameObject khi bị gọi trong lúc Unity đóng scene (từ `EnemyMovementAgent.OnDisable`) | `Instance` trả `null` khi `!Application.isPlaying`; `OnDisable` dùng `HasInstance` |
| Ký Sinh Thiên Thạch chết vẫn hiện tên | Sau khi kích nổ, model bị ẩn nhưng GameObject còn sống nên `IsAlive` vẫn `true` | Thêm `ShowsNameplate` (ảo) + override ở Ký Sinh: `IsAlive && !hasDetonated` |
| Vùng ngoài bản đồ lộ màu xanh | Camera để `Clear Flags = Skybox`, ortho 9 nên khung hình rộng hơn bản đồ | `SolidColor #070A0E` + ortho 7 |
| Khung xanh trên minimap đứng yên | Khung được vẽ sẵn trong ảnh lưới | Sinh lưới riêng không có khung; thêm `ViewFrame` đọc camera thật |
| Ô kỹ năng trắng trơn sau khi đổi art | Bộ art gốc `spriteMode: Multiple` nên `LoadAssetAtPath<Sprite>` trả `null` | `ConfigureArtImporters` ép Sprite Single + `ForceSynchronousImport` |
| Chữ trên HUD bị đè / nút rơi ra ngoài hộp | Nút mặc định neo đáy `(0.5, 0)`; chữ trong hộp neo giữa dùng y dương | Thêm tham số `anchor` cho hàm tạo nút; đổi chữ trong hộp sang y âm |
| Icon kỹ năng bị vẽ hai lần | `GetGlyph` fallback sang icon thẻ khi thiếu glyph | Bỏ fallback và ẩn hẳn lớp glyph khi không có glyph |

### 11.1. Đoạn code then chốt của 3 lỗi gần nhất

```csharp
// PathfindingGrid2D.Instance — không tạo object khi Unity đang đóng scene / thoát play
if (!Application.isPlaying) return null;
GameObject go = new GameObject("[PathfindingGrid2D]");
```

```csharp
// EnemyMovementAgent.OnDisable — dùng HasInstance để không kích hoạt việc tạo mới
if (PathfindingGrid2D.HasInstance)
{
    PathfindingGrid2D.Instance.UnregisterEnemy(m_CurrentGridCoord);
}
m_CurrentGridCoord = new Vector2Int(-1, -1);
```

```csharp
// EnemyBrain — thuộc tính ảo để loại quái tự kích nổ tự ẩn nhãn tên
public virtual bool ShowsNameplate => IsAlive;

// NormalEnemy_KySinhThienThach — đã kích nổ thì không còn hiện tên
public override bool ShowsNameplate => IsAlive && !hasDetonated;
```

---

## 12. Việt hoá chữ hiển thị

- **HUD**: `Đợt i/n`, `Đã diệt`, `Ngọc`, `MÁU`, `NĂNG LƯỢNG`, `CẤP`, `TỐC ĐỘ`, `KỸ NĂNG TRANG BỊ`,
  `SẴN SÀNG` / `HỒI: x GIÂY`.
- **Mục tiêu**: `Quái x/y`, `Sống sót x/y giây`, `Mục tiêu x/y (id)`, `Hộ tống xm/ym`.
- **Kết quả**: `CHIẾN THẮNG` / `THẤT BẠI`, `Thời gian hoàn thành`, `Quái đã diệt`, `Máu còn lại`,
  `Ngọc Ma Pháp`, `Phụ kiện rơi`.
- **Cài đặt**: `CÀI ĐẶT TRẬN ĐẤU`, `CHÚ Ý: GAME KHÔNG TẠM DỪNG!`, `ÂM LƯỢNG TỔNG`,
  `ÂM THANH CHIẾN ĐẤU & MA PHÁP`, `HỆ THỐNG ĐIỀU KHIỂN & PHÍM BẤM`.
- **Tên ải / boss**: ghi bằng `Localize Stage Text` — 9 ải + 4 boss.
- **Icon kỹ năng**: dùng font bitmap 5×7 tự viết; đã bổ sung glyph có dấu (`À`, `Ẵ`, `Ồ`, `Í`, `Ế`, `Đ`)
  để in được `SẴN SÀNG`, `HỒI CHIẾU`.

> **Chưa làm**: 587 `Tooltip` và 351 `Header` trong Inspector của 213 file script vẫn viết không dấu — đây là
> chữ hiển thị trong Unity Editor, không ảnh hưởng người chơi.

---

## 13. Kiểm chứng

| Hạng mục | Cách kiểm | Kết quả |
|----------|-----------|---------|
| Biên dịch | Đếm `error CS` trong `Library/Bee/tundra.log.json` sau mỗi lần sửa | `0 error` ở tất cả các bước |
| QA 9 màn | `QA Audit Stages` (mục tiêu, đợt quái, prefab, mã màn kế, scene chứa asset) | `PASS 9/9`, chuỗi 1-1 → 2-5 liên tục |
| Bàn đồ chiến thuật | `Audit Minimap Scenes` | `PASS 9/9`, nền đúng cho từng vùng |
| Khung nhìn minimap | Đọc anchor của `ViewFrame` khi tank ở tâm và ở góc | Tâm `(0.50, 0.50)` — góc `(0.67, 0.67)` |
| Giới hạn biên | Đẩy tank tới `(40, -30)` | Bị kẹp về `(17,21, -8,90)` đúng biên trừ lề 1,6 |
| Quái đuổi từ xa | Đặt tank cách vùng sinh > 20 unit, đọc trạng thái AI | `12/12` quái chuyển sang `Chasing`, `dHome ≈ 9,5` |
| Nameplate Ký Sinh | Sinh quái đầy máu, kích nổ, poll mỗi 0,2 giây | `t = 0,40 s`: `alive = True`, `shows = False`, entry ẩn |
| Cảnh báo dọn scene | Vào play rồi thoát, đọc log sau mốc | Không còn cảnh báo `PathfindingGrid2D` |
| Camera | Đọc runtime | `size=7`, `clear=SolidColor`, `bg=RGBA(0.027,0.039,0.055,1)` |

Ảnh minh chứng nằm trong `docs/screenshots/` (34 PNG), gồm HUD từng vòng, bảng kết quả, popup cài đặt,
màn chọn ải, bàn đồ chiến thuật và các màn boss.

---

## 14. Trạng thái Git

| Hạng mục | Giá trị |
|----------|---------|
| Nhánh | `Stage` (tách từ `main` tại `e82ee94`) |
| Commit 1 | `8b4e0fc` — *Hoan thanh 9 man choi + Spawner quai + SFX + HUD theo thiet ke* (643 file, +61.904 / −1.655 dòng) |
| Commit 2 | `e79776e` — *Sua canh bao "objects were not cleaned up" ([PathfindingGrid2D])* (3 file) |
| Remote | `origin/Stage` → `8b4e0fc…e79776e` (`https://github.com/fasysd/IronWasteland.git`) |
| Chưa đẩy | Nameplate Ký Sinh, bộ Skill Icon mới, HUD `Card` 250×231, 6 `TankDefinition`, 9 scene, 24 PNG mới, `SkillIconIntegrator.cs` |

> **Ràng buộc hiện tại (từ 18/11/2026):** không commit/push và không chạy kiểm tra gameplay cho tới khi có
> yêu cầu rõ ràng. Mọi thay đổi nêu trên đang nằm trong working tree.

---

## 15. Việc còn lại

1. Đẩy các thay đổi đang chờ lên nhánh `Stage` khi được phép.
2. Cân nhắc `preserveAspect = true` cho ô kỹ năng để giữ đúng tỉ lệ vuông của art mới (hiện bị nén dọc ~8%).
3. Nhãn tên quái vẫn chồng thành cột khi nhiều quái dồn quanh tank — cần thuật toán xếp hàng ngang.
4. Co-op mới là khung giao diện, chưa có logic nhiều người chơi.
5. Damage popup chưa dùng art `vfx_damage_popup_plus40/minus20` vì ảnh có sẵn số in chìm; muốn dùng phải sinh số động.
6. Chân dung người chơi đang dùng chung art Black Shadow cho cả 6 xe.
7. 587 `Tooltip` + 351 `Header` trong Inspector vẫn không dấu.
8. Có thể xoá bộ icon sinh tự động cũ trong `Assets/Art/UI/GameplayHUD/SkillIcons/<Tank>/`.
9. Cân bằng tiếp: tank cấp 12 ở 1-4 vẫn khá mỏng máu (61/1856 khi test).

---

## 16. Cách vận hành nhanh

```text
Dựng lại HUD sau khi sửa:
    Tools ▸ Iron Wasteland ▸ Rebuild GameplayHub
    Tools ▸ Iron Wasteland ▸ Refresh HUD In Scenes   (phải TẮT play mode)

Đổi bộ icon kỹ năng:
    Tools ▸ Iron Wasteland ▸ Integrate New Skill Icons
    → Rebuild GameplayHub → Refresh HUD In Scenes

Kiểm tra trước khi phát hành:
    QA Audit Stages        → phải PASS 9/9
    Audit Minimap Scenes   → phải PASS 9/9

Camera / nền:
    Setup Cameras          → orthographic size 7, SolidColor #070A0E cho 9 scene

Quái không đuổi:
    Fix Enemy Aggro        → radiusC = 60 cho 11 prefab
```

### Danh mục file chính

| Nhóm | Đường dẫn |
|------|-----------|
| Dữ liệu màn | `Assets/Data/Stages/Stage_1_1.asset` … `Stage_2_5.asset` (9 file) |
| Scene | `Assets/Scenes/Stage_1_1.unity` … `Stage_2_5.unity` (9) + `StageSelect.unity` |
| Framework | `Assets/Scripts/Stages/` — 11 file (StageDefinition, StageObjective, EnemySpawner, SpawnZone, StageDirector, StageReward, StageProgress, DestructibleTarget, EscortVehicle, RadarStation, BossTag) |
| Camera / biên | `Assets/Scripts/Core/` — `CameraFollow2D.cs`, `StageBounds.cs` |
| HUD | `Assets/Scripts/UI/` — `StageHUD.cs`, `StageSelectUI.cs`, `TankStatusHUD.cs`, `MinimapUI.cs`, `UnitNameplates.cs`, `UISprites.cs` |
| Âm thanh | `Assets/Scripts/Audio/` (6 file) + `Assets/Audio/SFX/` (39 WAV) + `Assets/Audio/SfxLibrary.asset` |
| Prefab HUD | `Assets/Prefabs/UI/StageHUD.prefab` |
| Art | `Assets/Art/UI/GameplayHUD/`, `Assets/Art/Backgrounds/`, `Assets/Art/Tank/Sprites/`, `Assets/Art/UI/Generated/` |
| Công cụ Editor | `Assets/Editor/` — HudBuilder, HudSceneRefresh, MinimapAudit, StageQAAudit, StageQAFix, StageCameraSetup, SettingsPanelBuilder, EnemyAggroFix, StageTextLocalize, SkillIconGenerator, SkillIconIntegrator, UITheme, UIReskin |
| Tài liệu | `docs/PLAN_Stages_Spawner_SFX.md`, `docs/HUONG_DAN_StageFramework.md`, `docs/BAO_CAO_IronWasteland_CongViec.docx`, chính file này |
| Ảnh minh chứng | `docs/screenshots/` (34 PNG) |
