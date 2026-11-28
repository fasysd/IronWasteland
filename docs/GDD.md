# Iron Wasteland — Game Design Document (GDD)

> **Trạng thái tài liệu:** Chuẩn hóa từ `docs/Tài liệu thiết kế Game.md` kết hợp đối chiếu codebase/scene thực tế và các quyết định chốt thiết kế.
> **Lưu ý:** Tài liệu này là nguồn chuẩn cho gameplay, thông số và luồng trải nghiệm. Các sai lệch đang tồn tại trong code/asset được ghi nhận ở mục **9. Điểm lệch chuẩn cần xử lý**.

---

## 1. Tổng quan trò chơi
- **Tên game:** Iron Wasteland
- **Thể loại:** Top-down 2D Action Shooter (Sci-fi + Ma pháp hậu tận thế)
- **Nền tảng:** PC / Desktop
- **Chế độ chơi:**
  - **Chơi đơn (Single-player):** 1 người chơi điều khiển 1 xe tăng.
  - **Co-op LAN (2–4 người):** 2–4 người chơi phối hợp qua mạng LAN; toàn bộ kẻ địch thường và Tinh anh được **nhân đôi máu (×2 HP)**.
- **Vòng lặp cốt lõi (Core Loop):**
  `Chọn ải` → `Chọn chế độ (Đơn / Co-op)` → `Chuẩn bị Tank & Kỹ năng tùy chọn & Phụ kiện` → `Chiến đấu (WASD + Chuột + 4 phím kỹ năng)` → `Thắng: Nhận Ngọc ma pháp / Phụ kiện / Mở khóa Tank` → `Nâng cấp & Trang bị trong Garage` → `Tiếp tục ải mới`.

---

## 2. Luồng màn hình (Game Flow)

> **[Quyết định chuẩn hóa theo build hiện tại]:**
> - Khởi động vào **MainMenu** gồm 4 mục: **Chọn màn chơi** (`StageSelect`), **Nhà xe** (`Garage`), **Cài đặt** (`Setting`), **Thoát**.
> - Từ **StageSelect**, chọn ải + chế độ:
>   - **Chơi đơn:** Vào thẳng scene màn chơi (`Stage_X_Y`).
>   - **Co-op:** Vào **CoopLobby** (tạo phòng / tìm phòng LAN / nhập IP:Port). Từ phòng chờ có thể mở **Garage** dạng overlay (additive) để đổi xe, nâng cấp, đổi kỹ năng, đổi phụ kiện mà không rớt kết nối.
> - **Kết thúc màn chơi:**
>   - **Thắng:** Cộng Ngọc ma pháp, quay Phụ kiện, mở khóa ải kế tiếp (và mở khóa Tank nếu là mốc lần đầu). Cho phép Chơi lại, Qua ải tiếp, hoặc Về Sảnh/Lobby.
>   - **Thua:** Không nhận thưởng. Chơi đơn có thể Chơi lại hoặc Về chọn ải; Co-op quay lại phòng `CoopLobby` (Host bấm Replay) hoặc rời phòng.

---

## 3. Hệ thống Xe Tăng (Tanks)

### 3.1. Danh sách 6 Xe tăng & Điều kiện mở khóa
| Index | Tên xe | Vai trò chính / Mở rộng | Điều kiện sở hữu |
| :--- | :--- | :--- | :--- |
| 0 | **Wall Titan** | Tanker / Support | Mở khóa mặc định (Cấp 1) |
| 1 | **Red Nova** | DPS / DPS | Mở khóa mặc định (Cấp 1) |
| 2 | **Black Shadow** | DPS / Support | Vượt ải **1-2** lần đầu |
| 3 | **Leviathan** | Tanker / Tanker | Vượt ải **1-3** lần đầu |
| 4 | **Water B** | Support / DPS | Vượt ải **2-1** lần đầu |
| 5 | **Green Fox** | DPS / DPS | Vượt ải **2-3** lần đầu |

### 3.2. Chỉ số & Công thức tăng trưởng (Cấp 1 → 90)
Mỗi xe có **8 chỉ số**: `Tấn công (ATK)`, `Giới hạn máu (MaxHP)`, `Giới hạn năng lượng (MaxEnergy)`, `Phòng thủ (DEF)`, `Tốc độ di chuyển (SPD)`, `Tỉ lệ giảm hồi chiêu (CDR)`, `Tỉ lệ sát thương (DamageRate)`, `Tỉ lệ xuyên giáp (ArmorPen)`.

- **Nội suy chỉ số cơ bản theo cấp $L \in [1, 90]$:**
  $$\text{Stat}(L) = \text{Stat}_1 + (\text{Stat}_{90} - \text{Stat}_1) \times \frac{L - 1}{89}$$
- **Tính chỉ số cuối cùng (Final Stats):**
  - Với `ATK`, `MaxHP`, `MaxEnergy`, `DEF`, `SPD`:
    $$\text{Final} = \max\left(0,\; \text{Base} \times (1 + \sum \text{PercentBonus}) + \sum \text{FlatBonus}\right)$$
    *(Riêng `MaxHP` tối thiểu là `1`)*
  - Với `DamageRate`, `ArmorPen`, `CDR` (cộng dồn tuyến tính theo tỉ lệ):
    $$\text{Final} = \text{Base} + \sum \text{PercentBonus} + \sum \text{FlatBonus}$$
- **Giới hạn cứng (Hard Caps):**
  - `CDR` tối đa: **60%** (`0.60`).
  - `ArmorPen` tối đa: **100%** (`1.00`).
  - `Tốc độ di chuyển thực tế (units/s)` = $\text{Final SPD} / 100$.

### 3.3. Chi phí nâng cấp (Tài nguyên: Ngọc ma pháp)
| Khoảng cấp độ đích | Chi phí / 1 cấp |
| :--- | :--- |
| Cấp 2 → 20 | **5** Ngọc ma pháp |
| Cấp 21 → 40 | **10** Ngọc ma pháp |
| Cấp 41 → 60 | **15** Ngọc ma pháp |
| Cấp 61 → 80 | **20** Ngọc ma pháp |
| Cấp 81 → 90 | **25** Ngọc ma pháp |

### 3.4. Bộ kỹ năng của mỗi Xe
Mỗi xe có **4 ô kỹ năng**:
1. **Kỹ năng thường:** Không tốn năng lượng, hồi năng lượng khi đánh trúng (trừ trạng thái đặc biệt như *Laser Cannon* của Red Nova).
2. **Kỹ năng chuyên biệt:** Kỹ năng đặc trưng cố định của xe, tiêu hao năng lượng.
3. **Kỹ năng tùy chọn 1:** Chọn **1 trong 3** nhánh kỹ năng trong `SkillPicker`.
4. **Kỹ năng tùy chọn 2:** Chọn **1 trong 3** nhánh kỹ năng trong `SkillPicker`.

---

## 4. Hệ thống Phụ kiện (Accessories) — Quy tắc đã chốt

- **Tổng số loại:** **20** phụ kiện (Index `0..19`, tương ứng STT `1..20` trong `Tài liệu thiết kế Game.md §4.4`).
- **Số ô trang bị:** **Mỗi xe chỉ có tối đa 1 ô phụ kiện.**
- **Trạng thái khởi tạo (`GameData` mới):** Kho phụ kiện **rỗng** (không có phụ kiện nào).
- **Quy tắc Kho khi Trang bị / Tháo:**
  - **Trang bị:** Phụ kiện được lắp trong kho **-1**. Nếu xe đang lắp sẵn phụ kiện cũ, phụ kiện cũ được trả lại kho **+1**.
  - **Tháo:** Phụ kiện tháo ra được trả lại kho **+1**.
- **Tỉ lệ rơi khi qua ải:** **Cố định 50%** cho mỗi phụ kiện trong pool phần thưởng của ải (quay độc lập theo danh sách pool của từng ải).
- **Phân rã phụ kiện:** Phân rã mỗi món nhận ngẫu nhiên **8–12 Ngọc ma pháp**.

---

## 5. Hệ thống Chiến đấu (Combat Rules)

### 5.1. Công thức sát thương & Giảm sát thương theo Phòng thủ
- **Sát thương gốc (Raw Damage):**
  - **Tank:** $\text{Raw} = \text{ATK} \times \text{SkillScale} \times (1 + \text{DamageRate})$
  - **Kẻ địch:** $\text{Raw} = \text{ATK} \times \text{SkillScale}$
- **Phòng thủ hiệu dụng (Effective Defense):**
  - **Tank chịu đòn:** $\text{Def} = \text{DEF}$
  - **Kẻ địch chịu đòn:** $\text{Def} = \text{DEF} \times (1 - \text{ArmorPen}_{\text{attacker}})$
- **Tỉ lệ giảm sát thương (`DR%`) theo 4 đoạn logarit:**
  - $\text{Def} \le 0$: $0\%$
  - $0 < \text{Def} \le 40$: $50 \times \frac{\ln(1 + \text{Def})}{\ln(41)}$ *(0% → 50%)*
  - $40 < \text{Def} \le 70$: $50 + 25 \times \frac{\ln(1 + \text{Def} - 40)}{\ln(31)}$ *(50% → 75%)*
  - $70 < \text{Def} \le 100$: $75 + 10 \times \frac{\ln(1 + \text{Def} - 70)}{\ln(31)}$ *(75% → 85%)*
  - $\text{Def} > 100$: $85 + 5 \times \left(1 - \frac{1}{1 + \ln(\max(1, \text{Def} - 99))}\right)$ *(85% → tiệm cận 90%)*
- **Sát thương sau giáp:** $\text{AfterArmor} = \text{Raw} \times (1 - \text{DR\%} / 100)$.

### 5.2. Thứ tự ưu tiên khi nhận sát thương
1. **Chặn đòn / Né đòn / Bất tử (`IDamageInterceptor`):** Xử lý đầu tiên (theo thời gian còn lại ngắn nhất). Nếu chặn thành công, đòn đánh bị hủy hoàn toàn. Mặc định DoT không kích hoạt chặn đòn.
2. **Gánh hộ / Chuyển hướng sát thương (`IDamageRedirector`):** Phần sát thương chuyển sang người gánh hộ đi thẳng vào Máu (`IsDirectToHealth = true`, bỏ qua giáp và khiên của người gánh hộ; không chuyển hướng tiếp dây chuyền và không áp dụng cho DoT).
3. **Hệ số giảm sát thương từ Phụ kiện** (nếu không phải `IsDirectToHealth`).
4. **Phòng thủ & Xuyên giáp** (bỏ qua nếu `IsDirectToHealth`).
5. **Khiên (`ShieldEffect`):** Trừ vào lớp khiên có `RemainingTime` **ngắn nhất trước**. Khiên có giới hạn số đòn (`MaxAbsorbHits`) sẽ vỡ ngay khi đủ số đòn dù còn trữ lượng. **DoT (Thiêu đốt) và sát thương Gánh hộ bỏ qua Khiên**, trừ thẳng vào Máu.

### 5.3. Hiệu ứng khống chế (Crowd Control)
- Các loại CC chuẩn: `Root` (Trói chân), `Silence` (Câm lặng), `Stun` (Choáng), `Slow` (Làm chậm), `Knockback` (Đẩy lùi).
- **Boss / Kẻ địch Tinh anh:** Miễn nhiễm khống chế cứng (`Root`, `Stun`); một số kỹ năng có quy tắc riêng thay thế trói chân bằng sát thương/làm chậm.

---

## 6. Kẻ địch & Boss Tinh Anh

- **Phân loại:**
  - **Kẻ địch thường:** Chương 1 (5 loại: Ký sinh thiên thạch, Xạ thủ tinh thể, Quái vật thạch anh, Mặt trăng đen, Tinh linh nghĩa địa); Chương 2 (7 loại: Kiến lửa khổng lồ, Quái vật đầm lầy, Nấm độc gai thạch, Sao đen, Mặt trời đỏ, Xén nước, Tinh linh sấm sét).
  - **Boss Tinh anh (5 Boss):**
    - Chương 1 (Ải 1-4): **Bóng Ma Tử Địa**, **SeauBearus**.
    - Chương 2 (Ải 2-4): **Tử Thần Nguyên Tố**, **The2Face**.
    - Chương 2 (Ải 2-5 Final Boss): **Trưởng Quản Ngục D1onk**.
- **Cơ chế Bùng nổ năng lượng (Energy Burst):**
  - Kích hoạt khi Boss xuống dưới ngưỡng HP quy định lần đầu (45%–60% tùy Boss).
  - Nếu người chơi phá giải cơ chế trong thời gian quy định → **Phần thưởng** (giảm DEF mạnh, choáng, buff/hồi cho Tank).
  - Nếu hết giờ hoặc thất bại điều kiện → **Hình phạt** (sát thương lớn / hiệu ứng bất lợi / Instant Kill ở D1onk).

---

## 7. Màn chơi & Phần thưởng (9 Ải — 2 Chương)

| Ải | Tên ải | Điều kiện thắng chuẩn | Ngọc | Mở khóa Tank (lần đầu) | Số món tối thiểu (pool) |
| :--- | :--- | :--- | :---: | :--- | :--- |
| **1-1** | Sương Mù Tím | Tiêu diệt 40 quái vật | 20 | — | Pool 3 món (1 món nếu đảm bảo) |
| **1-2** | Hang Đá Gai | Sinh tồn 100 giây | 30 | **Black Shadow** | Pool 3 món |
| **1-3** | Dấu Vết Tù Binh | Kích hoạt 2 Trạm Radar & Hộ tống xe Mones 150m | 40 | **Leviathan** | Pool 3 món |
| **1-4** | Vòng Xoáy Không Gian | Hạ gục liên tiếp Bóng Ma Tử Địa & SeauBearus (4 Phase) | 60 | — | Pool 10 món |
| **2-1** | Vành Đai Bão Lửa | Tiêu diệt 50 quái vật | 50 | **Water B** | Pool 3 món |
| **2-2** | Đầm Lầy Độc Khí & Bão Điện | Phá hủy 4 Cụm Nấm Mẹ & Sinh tồn 120s | 60 | — | Pool 3 món |
| **2-3** | Đột Kích Tiền Đồn | Bảo vệ Xe phá thành phá 3 Trụ Điện Trường | 70 | **Green Fox** | Pool 3 món |
| **2-4** | Phi Thuyền Hai Mặt & Pháo Đài Hira | Hạ gục liên tiếp Tử Thần Nguyên Tố & The2Face (4 Phase) | 90 | — | Pool 9 món |
| **2-5** | Chiến Hạm D1ONK | Hạ gục Final Boss D1onk | 120 | — | Pool 10 món |

---

## 8. Cơ chế Môi trường & Vật thể tương tác
- **Trạm Radar (1-3):** Đứng trong bán kính `3m` suốt `5s` (sau khi dọn quái quanh vùng `8m`) để kích hoạt.
- **Xe Hộ tống / Xe Phá thành (1-3, 2-3):** `HP = 2000`; có Tank đi kèm trong bán kính `5m` tăng `20%` tốc độ di chuyển. Xe nổ → Thua ngay.
- **Cụm Nấm Mẹ (2-2) & Trụ Điện Trường (2-3):** Công trình địch cố định (`HP = 1500`, `Giáp = 30`).

---

## 9. Điểm lệch chuẩn cần xử lý (Sau khi xác nhận)
1. **Mở khóa Tank theo ải (`1-2`, `1-3`, `2-1`, `2-3`) chưa được nối:** `SaveSystem.UnlockTank()` tồn tại nhưng không có ai gọi; `StageDefinition` chưa có field `unlockTankIndex`; `GarageController.IsTankLocked()` đang kiểm tra `IsStageCleared` thay vì `IsTankOwned`.
2. **Logic `StageReward.Roll()` vs `guaranteeAccessories`:** `StageReward.cs` đang có `AccessoryDropChance = 0.5f` (đúng ý định chốt 50%), nhưng comment vẫn ghi 20% và phần code luôn pad đủ `rewardAccessoryCount` bất chấp `guaranteeAccessories = false` (trong khi cả 9 asset `Stage_1_1`..`Stage_2_5` đều đặt `guaranteeAccessories: 0`). Cần chốt rõ: luôn đảm bảo số món tối thiểu hay chỉ quay 50% thuần túy.
3. **Phân rã phụ kiện đang trang bị (`AccessorySalvageController.cs`):** `SalvageQuoteModel` không chặn phân rã khi đang trang bị (vì bản đang lắp đã trừ `-1` khỏi kho), nhưng `AccessorySalvageController.ChangeQuantity()` và `SelectMaxQuantity()` vẫn chặn nếu `IsEquipped` đúng (trong khi `ConfirmSalvage` ở số lượng 1 lại không chặn).
4. **Thông số `Stage_*.asset` lệch mô tả thắng:** `winConditionDescription` ở `Stage_1_2`, `Stage_1_3`, `Stage_1_4`, `Stage_2_2`, `Stage_2_4` chưa khớp GDD; `The2Face` trong GDD gốc ghi nhầm `HP: 8.000 (Co-op: 6.000)` (đáng lẽ Co-op 16.000).
5. **Thông số Boss trong code lệch GDD:** Header & stats của `SeauBearusBrain` (HP 1800 vs 3000), `ElementalReaperBrain` (HP 9500 vs 5000), `D1onkBrain` (HP 27000 vs 10000), và đơn vị `SPD` (70–90 vs 450–650) đang lệch giữa các Boss.
