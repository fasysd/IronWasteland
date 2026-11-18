# Menu Iron Wasteland – hướng dẫn nhanh

Bao gồm: **Main Menu, Nhà xe (quản lý tank & phụ kiện), Chọn ải, Cài đặt**, popup **Chọn kỹ năng**, **Chọn phụ kiện**, **Xác nhận**.
Toàn bộ UI dựng bằng code (uGUI + TextMeshPro) trên khung 1920x1080, không cần tự kéo prefab.

## 1. Cài đặt (5 bước)

1. Copy thư mục `Assets/Scripts/Menu` vào project (giữ nguyên `Editor/` và `Editor/Data/GddMenuData.json`).
2. Ghi đè `GameController.cs` bằng bản kèm theo (đã nối với Menu, xem mục 4).
3. **Hai font** (theo Figma): **Anton Regular** cho tiêu đề (gán vào `MenuBootstrap.displayFont`) và **Barlow Condensed** cho chữ còn lại (`uiFont`).
   Tạo TMP Font Asset bằng *Window > TextMeshPro > Font Asset Creator*: Dynamic, Sampling Point Size 48–64, Atlas 1024–2048, bật **Multi Atlas Textures**
   (hoặc Static với Unicode Range `20-7E,A0-17F,1A0-1B0,1EA0-1EF9,2013-2014,2018-201D,2026,B7`). Thiếu ký tự tiếng Việt thì chữ có dấu thành ô vuông;
   atlas Dynamic hết chỗ thì nhãn dựng sau sẽ mất chữ.
4. Menu: `IronWasteland > Menu > 1. Tạo dữ liệu mẫu từ GDD`, rồi `2. Tạo Scene MainMenu`.
   - Tạo `Assets/GameData/Menu/{TankCatalog, AccessoryCatalog, StageDatabase}.asset` từ GDD (tên ải, thưởng Ngọc,
     phụ kiện có thể rơi, xe mở khóa ở ải nào, tên + thông số kỹ năng 6 xe) và Scene `Assets/Scenes/MainMenu.unity`.
   - Tự gán `TankDefinition` (khớp theo tên) và `EquipmentDatabase` nếu tìm thấy; thiếu gì sẽ in cảnh báo ở Console.
5. Chọn object `MenuBootstrap` trong Scene: gán **UI Font**, (tùy chọn) ảnh nền. Trong `TankCatalog` gán `portrait` / `listIcon`
   cho từng xe và icon kỹ năng. Bấm Play.

## 2. Luồng màn hình

```
MainMenu ──Chọn màn chơi──▶ StageSelect ──Đổi xe──▶ Garage
   │                              ▲                     │
   ├──Nhà xe & nâng cấp──────────────────────────────▶ │ ──Xác nhận ra trận──▶ StageSelect
   └──Cài đặt──▶ Settings                               
```
ESC: đóng popup trên cùng → quay lại → (ở Main Menu thì mở Cài đặt). Đang gán phím thì ESC chỉ hủy việc gán.

## 3. Dữ liệu & lưu trữ

| Thứ | Nơi lưu | Ghi chú |
|---|---|---|
| Ngọc, cấp xe, 2 kỹ năng tùy chọn, phụ kiện, tiến độ ải | `persistentDataPath/ironwasteland_profile.json` | Sửa qua `ProfileService` (tự lưu + phát `Changed`) |
| Âm lượng 4 kênh, phím | PlayerPrefs `IronWasteland.Settings.v1` | `GameSettings` |
| Chỉ số xe theo cấp | đọc từ `TankDefinition` | qua `TankStatsBridge` (xem mục 6) |
| Tên/icon/mô tả/chỉ số phụ kiện | đọc từ `EquipmentBrain` trong `EquipmentDatabase` | ID phụ kiện = index trong `accessoryPrefabs` |

Xóa hồ sơ để test lại: `IronWasteland > Menu > Xóa hồ sơ người chơi`.

## 4. Dành cho người làm Scene ải (Quốc Khánh)

- Thêm Scene ải vào **Build Settings**, tên Scene khớp `sceneName` trong `StageDatabase` (mặc định `Stage_1_1`...).
- `GameController` (bản kèm theo) tự lấy xe / cấp / phụ kiện / kỹ năng tùy chọn từ `GameSession` khi vào từ Menu;
  chạy thẳng Scene thì dùng giá trị Inspector như cũ. Spawner/quái đọc `GameSession.IsCoop` (Co-op: HP quái thường và Tinh Anh x2 theo GDD).
- Khi **thắng**: `StageResult r = GameSession.ReportVictory();` → cộng Ngọc, tính rơi phụ kiện, mở xe mới (lần đầu), mở ải kế.
  `r.Gems`, `r.AccessoryIds[]`, `r.UnlockedTankIds[]` dùng cho màn kết quả. Sau đó `GameSession.ReturnToMenu()` (về sảnh chính).
- Khi **thua** (chơi đơn hoặc Co-op): không gọi `ReportVictory`; gọi `GameSession.ReturnToMenu(MenuLanding.StageSelect)` (Co-op sẽ ở lại phòng của ải).
- Nếu Scene ải dùng phím: đọc qua `GameSettings.IsHeld / WasReleasedThisFrame(GameAction.X, keyboard)`, đừng hard-code phím.
- Âm lượng: `GameSettings.GetVolume(AudioChannel.Sfx / Music / Voice)` (Master đã áp vào `AudioListener.volume`).

## 5. Dành cho người làm mạng (Văn Khiếu)

Màn Chọn ải đã có sẵn phòng Co-op 4 ô. Chỉ cần viết một lớp implement `ICoopLobby`
(`Slots`, `CanStart`, `StatusText`, `Changed`, `Open()`, `Close()`) rồi gán `MenuServices.CoopLobby = new MyLobby();`
trước khi Menu khởi động. Hiện đang dùng `OfflineCoopLobby` (chỉ một mình; `MenuBootstrap.allowCoopWithoutNetwork` cho phép
bấm Bắt đầu để test).

## 6. Điểm cần kiểm tra / giả định

1. **`TankStatsBridge`**: chưa có `TankDefinition.cs` nên cầu nối tìm bằng reflection: hàm `(int level) → TankStats`
   (tên có chữ "Stat") hoặc hai field `...Level1` / `...Level90` (nội suy tuyến tính). Nếu bảng chỉ số hiện 0 và Console
   cảnh báo, sửa thân `TankStatsBridge.GetStats` thành lời gọi trực tiếp.
2. **Rơi phụ kiện (GDD 5.6)**: hiểu là trúng tỉ lệ 20% thì nhận N phụ kiện **khác nhau** từ danh sách của ải
   (N = 1; ải 1-4 là 3; 2-4 là 2; 2-5 là 3). Chỉnh ở `StageDatabase` (`accessoryDropChance/Count/Pool`).
3. **Phân rã**: ngẫu nhiên 8–12 Ngọc (đổi trong `AccessoryCatalog`).
4. **Xe mở sẵn**: Wall Titan và Red Nova (không có điều kiện); Black Shadow ← 1-2, Leviathan ← 1-3, Water B ← 2-1,
   Green Fox ← 2-3 (theo bảng 5.7 của GDD; mockup ghi nhãn khóa của Green Fox là "ẢI 2-5", chưa khớp GDD).
5. **Cấp đề xuất của ải** là số tạm (1, 5, 10, 20, 30, 40, 50, 60, 75): chỉnh trong `StageDatabase`.
6. Ô **tốc độ di chuyển** hiển thị đúng giá trị `MoveSpeed` (GDD ghi 110 ở cấp 1). Đổi thang ở `StatDisplay.MoveSpeedDisplayScale`.
7. Mockup không có **Chơi mới / Chơi tiếp** (GDD 2.2 có). Hiện chỉ có "Chọn màn chơi"; "Chơi mới" tương đương xóa hồ sơ.
8. Chưa làm: **Gameplay HUD** và **Giao diện kết quả trận đấu** (chưa có mockup). `StageResult` đã đủ dữ liệu cho màn kết quả.

## 6b. Hiển thị xe bằng prefab TankView

Menu dựng prefab `TankView` (vd `Leviathan_View`) thật rồi quay ra RenderTexture cho Main Menu / Nhà xe / Chọn ải. Prefab lấy từ
`TankEntry.viewPrefab` (công cụ tạo dữ liệu mẫu tự gán nếu tên prefab dạng `<Tên xe>_View`) hoặc tự đọc từ `TankDefinition`.
Xe hiện đen → thêm Global Light 2D vào scene MainMenu. Không dựng được → hiện ảnh tĩnh `portrait`.

## 7. Cấu trúc file

```
Menu/
  Data/      TankCatalog, AccessoryCatalog, StageDatabase           (ScriptableObject dữ liệu UI)
  Save/      PlayerProfile, ProfileService, GameSettings, GameSession
  Services/  TankStatsBridge, TankViewBridge, ICoopLobby (+OfflineCoopLobby, MenuServices)
  UI/        MenuBootstrap, ScreenManager, MenuView (+MenuContext), UIKit, UITheme, UIButtonFx, StatDisplay,
             MainMenuScreen, GarageScreen, StageSelectScreen, SettingsScreen, RadialVolumeKnob,
             SkillPickerPopup, AccessoryPickerPopup, ConfirmPopup, TankPreviewStage, TankPreviewSlot
  Editor/    MenuSampleDataCreator, Data/GddMenuData.json
```
