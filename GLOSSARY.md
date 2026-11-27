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

## Mạng LAN

- **Phòng LAN (LAN room)** — phiên chơi chung do một máy host mở trực tiếp, không qua Relay. Phòng được định danh bằng **IP:port của host** (mặc định `7777`), không có mã phòng. Tên phòng lấy từ tên máy host: `PHÒNG CỦA <tên máy>`. Chi tiết: `docs/adr/0011-lan-only.md`.
- **Địa chỉ host (Host address)** — địa chỉ để client kết nối, dạng `IP` hoặc `IP:port`; host hiển thị mọi IPv4 nội bộ kèm cổng và nút SAO CHÉP ĐỊA CHỈ. `LanHostAddresses.Primary()` chọn địa chỉ chính, `GetLocalIPv4()` trả toàn bộ (luôn có `127.0.0.1` để test cùng máy).
- **Cổng host (Host port)** — cổng UDP host mở phòng, ưu tiên `7777`; nếu cổng bị ứng dụng khác chiếm, host tự thử các cổng kế tiếp trong dải `7777–7786` và dùng cổng thực tế trong địa chỉ lẫn gói quảng bá LAN.
- **Tự dò phòng LAN (LAN discovery)** — `LanDiscoveryService` phát/nhận gói UDP broadcast `IWLAN|1|...` ở cổng **47777**; host phát mỗi 1 giây, phòng hết hạn sau 4,5 giây. Danh sách hiện tối đa 6 phòng, sắp theo tên.
- **Nhập IP host (Join by address)** — ô nhập tay thay cho mã phòng, nhận IP, `IP:port` hoặc tên máy qua `LanAddress.TryParse`; cổng mặc định 7777. Là phương án dự phòng khi broadcast bị chặn.
- **ĐANG CHIẾN ĐẤU (In match)** — cờ trong gói quảng bá cho biết host đang trong trận; phòng vẫn hiện trong danh sách và người vào muộn vẫn kết nối được nếu còn chỗ (tối đa 4 người).
- **Chỉ LAN (LAN-only)** — chế độ chơi chung duy nhất: không Relay, không Lobby, không cần Internet hay tài khoản UGS. Máy host phải cho phép UDP 47777 (dò phòng) và UDP 7777 (trận đấu).

## Đồng bộ kỹ năng quái

- Chi tiết kênh ghost: `docs/adr/0012-dong-bo-visual-ky-nang-quai.md`; animation, scale và script VFX thuần: `docs/adr/0013-dong-bo-animation-quai.md`.
- **Bóng kỹ năng (Skill ghost)** — bản sao visual-only của child Skill hoặc VFX kỹ năng do host gửi xuống client. Client không mô phỏng AI, không gây sát thương, không quyết định va chạm; chỉ hiển thị đúng thời điểm và vị trí host cast.
- **Marker bóng kỹ năng (NetVisualGhost)** — component đánh dấu prefab được phép làm ghost. `NetVisualGhost.Sanitize(ghost, allowVisualDrivers)` luôn tắt mọi `Collider2D`; giữ marker và các script đánh dấu `INetVisualGhostDriver` khi cho phép, tắt mọi `MonoBehaviour` còn lại. Animator/ParticleSystem/SpriteRenderer vẫn chạy nhưng logic gameplay bị vô hiệu.
- **Script VFX thuần hiển thị (INetVisualGhostDriver)** — interface đánh dấu script chỉ diễn hoạt hình ảnh (mở rộng vòng, xoay, fade, pulse), không gây sát thương/summon trong Update hay trigger. Chỉ 15 script đã kiểm tra được đánh dấu; script có `Physics2D.*` hoặc `TakeDamage`/`Instantiate` quái phải để tắt trên client.
- **Kênh IW.SkillVisual** — named message của NGO: host gửi handle, NetworkObjectId của quái gốc, Id catalog hoặc tên child, offset/góc local, `worldScale`, thời gian sống, cờ attach/animate và velocity/homing. Client dựng bản sao, gắn vào root nếu là aura/cone (scale chia cho `root.lossyScale`), hoặc đặt tại chỗ cast nếu là VFX độc lập; `NetVisualSpawn.End()` gửi lệnh thu hồi.
- **NetworkAnimator quái** — component NGO đặt trên root prefab quái, trỏ tới Animator child `EnemyModel`; mọi tham số được đồng bộ trừ `MoveSpeed`. Host phát trigger qua `NetEnemyAnimation.Trigger`; client nhận đúng state Idle/Move/đòn đánh/chết.
- **Đồng bộ pose client (tốc độ + hướng nhìn)** — `EnemyNetState` gửi `m_ModelYaw` mỗi 0,1 giây và client tự suy tốc độ từ delta vị trí `NetworkTransform`, làm mượt rồi gọi `EnemyModel.SetMoveSpeed`; nhờ vậy animation chạy mà không gửi thêm tham số MoveSpeed.
- **Catalog hình ảnh kỹ năng (NetSkillVisualCatalog)** — ScriptableObject dùng chung ở Assets/Resources/NetSkillVisualCatalog.asset, ánh xạ Id int ổn định sang prefab VFX. Host chỉ gửi Id nên VFX standalone không cần NetworkObject; thêm VFX mới phải chạy menu **Iron Wasteland/Net/Rebuild Skill Visual Catalog**.
- **Boss probe** — hạ tầng dev-only (BossProbe, cờ -iw-boss/-iw-spawn; BossProbeCatalog chứa 5 boss + 12 quái thường) để spawn một quái trong trận thử mà không sửa StageDefinition. Khi có probe, StageDirector.SuppressEnemyWaves tắt đợt quái gốc; thêm -iw-keep-waves để giữ lại; bấm F8 trên host để spawn lại quái đang chọn, F9 để chuyển và spawn quái kế tiếp trong phòng. Phần code runtime nằm trong #if UNITY_EDITOR || DEVELOPMENT_BUILD nên không vào bản release.

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
