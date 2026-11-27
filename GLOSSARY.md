# Bảng thuật ngữ IronWasteland (GLOSSARY)

Thuật ngữ dùng thống nhất trong code, tài liệu và hội thoại. Khi đặt tên class, field hay viết tài liệu mới, dùng đúng các từ dưới đây.

## Xe và dữ liệu xe

- **Tank (Xe tăng)** — một đơn vị người chơi điều khiển. Dữ liệu tĩnh nằm trong `TankDefinition` (ScriptableObject tại `Assets/Prefabs/Tanks/<Ten>/<Ten>.asset`).
- **TankRoster** — ScriptableObject danh sách có thứ tự các `TankDefinition` hiển thị trong Garage (`Assets/Data/Tanks/TankRoster.asset`). Thứ tự trong roster là thứ tự hàng trong panel danh sách.
- **RosterIcon** — sprite đại diện của xe trong danh sách (khác ảnh chân dung lớn ở giữa màn hình).
- **Vai trò (Role)** — chuỗi mô tả lối chơi, ví dụ `TANKER`, `DPS`, `HỖ TRỢ`, `SÁT THỦ`. Mỗi xe có **rolePrimary** và **roleSecondary**; secondary rỗng thì chỉ hiển thị primary. Hiển thị dạng `PRIMARY / SECONDARY`.
- **Cấp (Level)** — cấp hiện tại của xe, 1..90 (`TankDefinition.MinLevel`/`MaxLevel`). Cấp lưu theo từng xe trong `PlayerProgress` (PlayerPrefs khoá `tank.level.<tankKey>`), chưa có cấp thì dùng `StartingLevel`.
- **Khoá theo ải (UnlockStageCode)** — mã ải dạng `x-y` (ví dụ `2-5`). Xe bị khoá khi ải đó chưa mở theo `StageProgress.IsUnlocked`. Chuỗi rỗng = mở sẵn. Nhãn hiển thị: `ẢI x-y`.
- **Chỉ số chiến xa (Stats)** — 8 chỉ số theo thứ tự cố định: Tấn công (ATK), Máu tối đa (HP), Giới hạn năng lượng (EN), Phòng thủ (DEF), Tốc độ di chuyển (SPD), Tỉ lệ giảm hồi chiêu (CDR), Tỉ lệ sát thương (DMG), Tỉ lệ xuyên giáp (PEN). Ba chỉ số cuối hiển thị dạng %.
- **MenuPortrait (Ảnh trưng bày)** — sprite lớn hiển thị ở khu trưng bày Main Menu, trường `menuPortrait` trong `TankDefinition`; builder seed từ `Assets/Art/UI/MainMenu/tank_hero_<snake_case>.png`.
- **Giá trị chuẩn hoá (Maxima)** — giá trị lớn nhất của từng chỉ số trong roster tại cấp 90, dùng làm mẫu số để vẽ thanh chỉ số.

## Phụ kiện và kỹ năng

- **Phụ kiện (Accessory)** — trang bị gắn cho xe, dữ liệu trong `AccessoryCatalog` (20 mục): tên hiển thị, các dòng chỉ số cộng thêm, mô tả hiệu ứng bị động, icon.
- **Trang bị đang dùng (Loadout)** — lựa chọn phụ kiện đã lưu cho từng xe, đọc qua `AccessoryLoadout`.
- **Kỹ năng (Skill)** — 4 ô kỹ năng của xe, thứ tự cố định: Kỹ năng thường, Kỹ năng chuyên biệt, Tùy chọn 1, Tùy chọn 2. Phím kích hoạt hiển thị là **1 2 3 4**.
- **Ô kỹ năng (Skill slot)** — một thẻ trong 4 thẻ; thiếu kỹ năng thì thẻ vẫn giữ phím và hiện `CHƯA MỞ`.
- **Tồn kho phụ kiện (AccessoryInventory)** — số bản sở hữu của từng món, lưu PlayerPrefs khoá `iw.acc.<Key>`, mặc định **1** để mọi món dùng được ngay. Đây là nguồn duy nhất để thêm bớt: hệ thống thưởng sau này chỉ cần gọi `Add(key, n)`.
- **Phân rã (Salvage)** — đổi phụ kiện lấy **Ngọc** (`StageProgress.AddGems`). Mỗi lần phân rã một loại, số lượng chọn trong ô stepper; màn `AccessorySalvage` nạp chồng từ Garage và không tự đóng sau khi xác nhận.
- **Giá trị phân rã (SalvageValue)** — số Ngọc nhận được cho **một** bản của món, khai trên `AccessoryCatalog.Entry.SalvageValue` (mặc định 5). Tổng nhận = SalvageValue × số lượng.
- **Đang trang bị (Equipped)** — món đang là lựa chọn của ít nhất một xe theo `AccessoryLoadout`; món này bị chặn phân rã (badge `ĐANG TRANG BỊ`, khoá ô số lượng, nút xác nhận mờ). Chỉ món còn tồn kho mới được tính là đang trang bị.
- **Chưa sở hữu (NotOwned)** — món có tồn kho 0 theo `AccessoryInventory`. Thẻ hiện mờ 0,35 + nhãn `CHƯA SỞ HỮU`, không chọn/không trang bị được ở cả màn CHỌN PHỤ KIỆN lẫn màn PHÂN RÃ; loadout còn trỏ tới món này bị coi là chưa trang bị và tự xoá khi mở Garage/picker.
- **Chốt sở hữu (Ownership gate)** — điều kiện `IsOwned(index)` (có trong catalog **và** `AccessoryInventory.Get(key) > 0`) dùng chung ở picker, panel Garage và màn phân rã để tồn kho luôn khớp với thứ đang hiển thị/đang trang bị.
- **Stepper số lượng** — cụm `−  [N]  +  MAX` vẽ bằng Image + TMP; mỗi nút là `UnityEngine.UI.Button` gắn trên Image sẵn có (không dùng `TMP_InputField`), bấm một lần đổi một bước; phím trái/phải vẫn đổi được.
- **Chưa trang bị / Đã tháo (None)** — `AccessoryLoadout.None = -1`: giá trị lưu cho xe không có phụ kiện nào. Mặc định của xe chưa từng chọn cũng là `None`; `AccessoryLoadout.Has(tank)` trả `false` khi không có phụ kiện.
- **Tháo phụ kiện (Unequip)** — thao tác bấm hai lần vào thẻ đang trang bị ở màn **CHỌN PHỤ KIỆN TRANG BỊ**: lần một chỉ chốt focus, lần hai gọi `AccessoryLoadout.Clear` (ghi `None`) và bỏ focus. Sau khi tháo, bấm XÁC NHẬN chỉ đóng màn, không trang bị lại; badge `ĐANG DÙNG` chỉ hiện trên thẻ thực sự đang được trang bị.

## Giao diện

- **Builder** (`Assets/Editor/GarageBuilder.cs`) — script Editor dựng scene Garage: bố cục, khung, placeholder, gắn tham chiếu SerializeField. Chạy lại bằng `GarageBuilder.Rebuild()`.
- **Model UI** (namespace `IronWasteland.UiModels`) — lớp thuần C# (không tham chiếu UnityEngine) biến dữ liệu game thành nội dung hiển thị đã format: `TankListModel`, `StatBarModel`, `AccessoryCardModel`, `SkillSlotModel`. Được test ở `Assets/Tests/EditMode`.
- **Controller** (`GarageController`, `GarageAccessoryPanel`) — đổ dữ liệu từ model vào các thành phần UI lúc `Awake`/`OnEnable`, xử lý chọn xe và tương tác.
- **Portrait (Chân dung)** — sprite lớn hiển thị ở khung giữa màn hình Garage, trường `portrait` trong `TankDefinition`, seed từ `Assets/Art/UI/Garage/tank_<snake_case>.png`.
- **Mô tả kỹ năng (SkillDescription)** — chuỗi phụ đề dưới tên kỹ năng, mảng `skillDescriptions` (4 phần tử) trong `TankDefinition`, đọc qua `GetSkillDescription(slot)`.
- **Màu chủ đề (RosterAccent)** — màu nhấn của từng xe (`rosterAccent`), dùng cho icon, viền chọn, thanh cấp và dải màu trong hàng danh sách.
- **Ngọc ma pháp (Gems)** — tiền tệ nâng cấp cấp xe, đọc/ghi qua `StageProgress.Gems` và `StageProgress.SpendGems`. Giá một cấp lấy từ `TankUpgradeUtility.GetCostForNextLevel(level)`.
- **Nâng cấp (Upgrade)** — tăng cấp xe thêm 1, trừ ngọc; nút chỉ bấm được khi số ngọc hiện có đủ giá cấp kế tiếp và cấp chưa đạt 90.
- **Xe bị khoá (Locked row)** — hàng danh sách có `ẢI x-y` chưa mở: hiển thị icon khoá, chữ xám và **không phản hồi khi bấm**; khi mở màn, Garage tự chọn xe mở khoá đầu tiên.
- **Nút chuẩn (Button)** — toàn bộ nút trong Garage và bảy màn UI còn lại là `UnityEngine.UI.Button` thật (có `targetGraphic`, `raycastTarget`, `Transition.None`), nối listener bằng persistent listener lúc build hoặc lúc chạy; không còn hit-test bằng toạ độ trong `OnGUI`.
- **UIHit** — helper Editor `Assets/Editor/UIHit.cs`: `EnsureEventSystem(scene)` bảo đảm scene có EventSystem + `InputSystemUIInputModule`, `Attach(Graphic)` gắn Button thẳng lên ảnh sẵn có, `AddZone(parent, name, x, y, w, h)` tạo Image trong suốt làm vùng bấm theo toạ độ thiết kế.
- **Vùng bấm trong suốt (Hit zone)** — `Image` alpha 0 do `UIHit.AddZone` tạo, phủ đúng rect thiết kế để Button nhận chuột ở khoảng trống không có ảnh nền. Chi tiết: `docs/adr/0010-vung-bam-bang-button.md`.
- **Cờ chặn click sau khi kéo (m_SuppressCardClick)** — đặt `true` khi người chơi kéo danh sách thẻ quá 6 px và chỉ reset ở `MouseDown` kế tiếp, để cú nhả chuột cuối thao tác kéo không kích hoạt nhầm thẻ.
- **Panel procedural** — panel danh sách xe, 8 thanh chỉ số, 4 thẻ kỹ năng và panel phụ kiện được dựng bằng `Image` + `TextMeshProUGUI` lúc build scene, thay cho ảnh nướng chữ.
- **Số ngọc động (GemCount)** — số ngọc ma pháp ở pill header không còn là ảnh nướng: builder phủ ô màu nền pill lên cụm số cũ rồi đặt TextMeshProUGUI "GemCount", controller đọc StageProgress.Gems và in lại khi giá trị đổi (GarageController.RefreshGemCount). Chỉ cần hệ thống cộng/trừ ngọc chạy là số tự nhảy, không phải sửa scene.
- **Viên ngọc tách khỏi art (UpgradeGem)** — nút NÂNG CẤP dùng upgrade_button_clean.png (art không còn viên ngọc) + Image "UpgradeGem" riêng (sprite upgrade_gem.png) gắn giữa nút theo trục dọc; nhờ vậy canh lại viên ngọc mà không phải xuất lại cả nút.
- **Pill vai trò (Role tag)** — viên bo góc dưới tên xe ở Main Menu, sinh lúc chạy từ `RoleTagModel.Build(rolePrimary, roleSecondary, rosterAccent)`: tối đa 2 pill, màu theo từ khoá vai trò (DPS tím, TANKER cam, HỖ TRỢ teal, SÁT THỦ đỏ), không khớp thì dùng `rosterAccent`; trùng vai trò thì chỉ một pill.
- **RoleTagView** — component của một pill vai trò (`Assets/Scripts/UI/RoleTagView.cs`): `Bind(text, color)` đổ chữ/màu, nới bề rộng theo `preferredWidth` và trả bề rộng; `SetLeft(x)` dời pill; `Hide()` tắt pill dư. Builder dựng sẵn nền + viền, controller chỉ đổ dữ liệu.
- **Khung CẤP ĐỘ động (Level frame)** — ô CẤP ĐỘ ở Main Menu do `LevelFrameModel.Build(level, maxLevel)` quyết định: số hai chữ số (`ToString("00")`) + hậu tố `/ max`; `/ 90` nướng trong `hero.png` đã bị xoá nên mẫu số đổi được bằng dữ liệu.
- **Portrait offset (portraitOffset)** — độ dời ảnh chân dung trong khung tròn Garage, đơn vị pixel thiết kế, dương = dịch sang phải / lên trên. Dùng để bù khối tâm thị giác của từng ảnh tank; khung chân dung là hình vuông 330×330 tại (703, 240.5), tâm trùng tâm vòng tròn (868, 405.5). Chi tiết: `docs/adr/0004-can-chan-dung-garage.md`.
- **Màn mở sẵn theo thiết kế (Unlocked)** — cờ `StageEntry.Unlocked` lưu trong `StageSelect.unity`; 1-1 và 1-2 mở sẵn. Lúc `Awake`, `StageSelectController` lấy hợp (`||`) với `StageProgress.IsUnlocked`: tiến trình chỉ mở thêm, không khoá lại màn đã cho mở. Chi tiết: `docs/adr/0009-trang-thai-mo-khoa-ai.md`.
- **Dải trạng thái thẻ ải (Status strip)** — 30 px đầu thẻ 1-1/1-2, chữ tính từ dữ liệu: ĐANG CHỌN (cam) → ĐÃ HOÀN THÀNH (teal, `StageProgress.IsCleared`) → ĐÃ MỞ KHÓA (xám) → CHƯA MỞ KHÓA (xám đậm).
