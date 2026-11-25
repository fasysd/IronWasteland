# 0008 — Main Menu đọc dữ liệu roster thay cho dữ liệu seed

- **Ngày:** 2026-11-25
- **Trạng thái:** Đã chốt

## Bối cảnh

Khu trưng bày xe ở Main Menu trước đây dùng mảng seed `MainMenuBuilder.TankPreviews`: tên, vai trò, cấp, bốn chỉ số và ảnh chân dung đều chép tay trong builder. Hệ quả là cấp/chỉ số ở menu không bao giờ khớp với Garage — nơi đã đọc `TankRoster` + `TankDefinition` + `PlayerProgress`. Ngoài ra pill vai trò (DPS / SUPPORT / BÓNG TÓI), dòng `TIẾN ĐỘ CẤP` và `22%` đều là pixel nướng trong `hero.png`, nên không thể đổi theo xe.

## Quyết định

1. **Bỏ hoàn toàn dữ liệu seed.** Xoá class `TankPreview`, mảng `TankPreviews` và struct `TankPreviewSpec`; `MainMenuController` nhận `m_Roster` (cùng asset `Assets/Data/Tanks/TankRoster.asset` với Garage) và đọc `TankDefinition` theo `SkillLoadout.LastTankIndex`.
2. **Cấp lấy từ PlayerProgress.** `level = PlayerProgress.HasTankLevel(tank.name) ? GetTankLevel : StartingLevel`, format qua `LevelFrameModel.Build` (số hai chữ số, mẫu số động `/ max`).
3. **Khung CẤP ĐỘ dựng mới, không vẽ đè art.** Builder đặt TMP `TextLevel` + `TextLevelMax` vào ô trống của khung nướng; `/ 90` cũ trong `hero.png` đã được xoá để mẫu số do dữ liệu quyết định.
4. **Pill vai trò dựng lúc chạy.** `RoleTagModel.Build(rolePrimary, roleSecondary, rosterAccent)` trả tối đa hai pill, màu theo từ khoá vai trò (DPS tím, TANKER cam, HỖ TRỢ teal, SÁT THỦ đỏ), không khớp từ khoá thì dùng `rosterAccent`. `RoleTagView.Bind` nới bề rộng theo `preferredWidth`, `SetLeft` xếp nối tiếp từ mốc 1087 px, pill dư thì ẩn. Nhờ vậy thêm vai trò mới chỉ cần sửa `TankDefinition`.
5. **Bỏ tag BÓNG TÓI và bỏ dòng tiến độ.** Chỉ còn hai pill vai trò; dòng `TIẾN ĐỘ CẤP` cùng `22%` bị xoá khỏi scene và khỏi `hero.png` vì tiến độ cấp đã có thanh riêng trong Garage.
6. **Bốn ô chỉ số đọc từ `GetStatsForLevel`.** ATK, HP, EN, SPD lấy đúng cấp đang hiển thị, làm tròn bằng `Mathf.RoundToInt`.
7. **Ảnh trưng bày là dữ liệu của xe.** Thêm `TankDefinition.menuPortrait`; `MainMenuBuilder.SeedMenuPortraits` tự ghép `tank_hero_<snake_case>.png` theo tên asset xe nên thêm xe mới chỉ cần đặt ảnh đúng tên.
8. **Dọn art một lần.** `hero.png` được vá nội suy ngang để xoá cụm pill, khối tiến độ và `/ 90` (giữ nguyên khung CẤP ĐỘ và nhãn). Bản gốc lưu trong `.buildtemp`.

## Hệ quả

- Main Menu và Garage dùng chung một nguồn dữ liệu; đổi cấp/chỉ số/vai trò ở asset là cả hai màn đổi theo, không phải chạy lại builder.
- Ảnh tank giữ nguyên (kích thước hiển thị lệch giữa các xe vẫn còn) — đã thoả thuận hoãn, xử lý bằng crop lại ảnh sau.
- `RoleTagModel` và `LevelFrameModel` nằm trong assembly `IronWasteland.UiModels` nên test được bằng EditMode, không cần mở scene.
- Thêm vai trò mới ngoài bốn từ khoá sẽ hiện màu `rosterAccent` cho tới khi bổ sung ánh xạ màu.
