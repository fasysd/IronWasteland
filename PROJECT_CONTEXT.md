# Iron Wasteland — Tài Liệu Ngữ Cảnh Dự Án (Project Context Guide)

> **Dành cho AI & Developers**: Đây là tài liệu duy nhất chứa toàn bộ ngữ cảnh kỹ thuật, kiến trúc, quy tắc thiết kế và bảng công thức của dự án **Iron Wasteland**. Bất kỳ AI Agent hoặc lập trình viên nào khi tham gia phát triển dự án cần đọc kỹ tài liệu này trước khi tạo mới hoặc sửa đổi code.

---

## 1. Tổng Quan Dự Án & Tech Stack

### 1.1 Thông tin chung
- **Tên dự án**: Iron Wasteland
- **Thể loại**: Bắn súng hành động 2D góc nhìn từ trên xuống (Top-down shooter), phong cách khoa học viễn tưởng kết hợp ma thuật (Sci-Fi & Magic).
- **Nền tảng**: PC / Desktop (Bàn phím + Chuột).
- **Chế độ chơi**: Chơi đơn (Single-player) và Co-op (2–4 người qua mạng nội bộ **LAN**, mô hình Host / Local).
- **Cốt truyện & Bối cảnh**: Trái Đất thế kỷ 30 bị biến thành nhà tù thí nghiệm của Đế chế Hira. Người chơi điều khiển các Siêu xe tăng ma pháp (kết hợp công nghệ quân sự và tinh thể năng lượng ngoài hành tinh) để chiến đấu qua 9 màn chơi (2 Chương: Lục Địa Đen và Vùng Đất Nguyên Tố).

### 1.2 Tech Stack & Công nghệ
- **Engine**: Unity 6 (`6000.5.6f1`).
- **Render Pipeline**: Universal Render Pipeline (URP 2D) với 2D Lights, Sprite Lit/Unlit.
- **Input System**: Unity New Input System (`UnityEngine.InputSystem`).
- **Giao diện người dùng (UI)**: **Canvas (uGUI)** truyền thống kết hợp TextMeshPro. *Không sử dụng UI Toolkit*.
- **Text Sát thương**: TextMeshPro World Space (quản lý qua `TextDamageManager` pooling).
- **Vật lý**: Physics 2D (`Rigidbody2D`, `Collider2D`).
- **Kiến trúc Màn chơi**: Mỗi màn chơi là một **Unity Scene độc lập** trong `Assets/Scenes/`.
- **Cơ chế Sinh quái**: Kết hợp cả sinh quái theo đợt (**Wave Spawner**) và vùng kích hoạt (**Trigger Zones**).

---

## 2. Triết Lý Kiến Trúc 3 Lớp (Core Architecture)

Nguyên tắc bất biến của dự án là **phân tách triệt để Dữ liệu – Logic – Hiển thị / Vật lý**:

```
┌────────────────────────────────────────────────────────────────────────┐
│  LAYER 1: DATA (ScriptableObjects & Assets)                            │
│  - TankDefinition: Mốc stats Cấp 1 & Cấp 90, prefabs, view, equip DB  │
│  - EquipmentDatabase: Danh sách 20 loại Phụ kiện                      │
│  - EnemyStats: Lưu trực tiếp trên prefab của quái                      │
└──────────────────────────┬─────────────────────────────────────────────┘
                           │ Initialize(context, stats)
┌──────────────────────────▼─────────────────────────────────────────────┐
│  LAYER 2: LOGIC & RUNTIME STATE (Brain MonoBehaviours)                 │
│  - TankBrain: HP, Energy, Shield, Cooldowns, 1 Accessory, CC, Effects   │
│  - EnemyBrain / EliteEnemyBrain: HP, Stats, Bùng nổ năng lượng, CC     │
│  - KHÔNG chứa SpriteRenderer, Animator hay đọc Input trực tiếp         │
└──────────────────────────┬─────────────────────────────────────────────┘
                           │ Điều khiển (Move/Look/UseSkill) + Events va chạm
┌──────────────────────────▼─────────────────────────────────────────────┐
│  LAYER 3: VISUAL & PHYSICAL (View / Model)                             │
│  - TankView: Prefab riêng, spawn con của TankBrain lúc runtime         │
│  - EnemyModel: Child GameObject trong cùng prefab của Enemy            │
│  - Giữ SpriteRenderer, Animator, pivots, Collider2D, ColliderOwnerRef │
│  - KHÔNG chứa logic game; chỉ forward sự kiện va chạm lên Brain       │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Quy Tắc Thép Dành Cho AI (Iron Rules - Inviolable)

Mọi dòng code do AI tạo ra **bắt buộc** phải tuân thủ nghiêm ngặt các quy tắc sau:

1. **Rule 1 — Tách biệt Input**: `TankBrain` và `EnemyBrain` tuyệt đối **không** được đọc trực tiếp từ `Keyboard`, `Mouse` hay `InputSystem` trong `Update()`. Brain chỉ nhận lệnh điều khiển thông qua các API: `SetMoveInput(Vector2)`, `SetLookTarget(Vector2)`, `ClearLookTarget()`, `UseSkill(int)`. Input chỉ được xử lý ở lớp ngoài (như `GameController` hoặc `PlayerInputHandler`).
2. **Rule 2 — Tách biệt Visual & Vật lý**: `TankView` và `EnemyModel` tuyệt đối **không** được xử lý tính toán sát thương, trừ máu hay trạng thái game. Chúng chỉ làm 2 nhiệm vụ: hiển thị hình ảnh (animator/pivots) và chuyển tiếp sự kiện va chạm lên Brain qua `ColliderOwnerRef`.
3. **Rule 3 — Quản lý Chỉ số (Immutable Base Stats)**: Không bao giờ sửa trực tiếp `baseStats` lúc runtime. Mọi buff, debuff, hiệu ứng trang bị phải đi qua `AddBonusStats(key, flatDelta, percentDelta)` và `RemoveBonusStats(key)`. Chỉ số cuối cùng luôn được tính toán tự động: $\text{Final} = \text{Base} \times (1 + \text{Percent}) + \text{Flat}$.
4. **Rule 4 — Không Instantiate / Destroy khi đang chiến đấu (Pooling)**: Đạn đạo, hiệu ứng vùng (AoE), số sát thương (Text Damage) phải được tái sử dụng qua cơ chế Pool cục bộ (`Stack<T>` hoặc `List<T>`). Tuyệt đối không gọi `Instantiate()` hay `Destroy()` liên tục mỗi khi bắn. Kết thúc đạn/vùng chỉ gọi `SetActive(false)` và trả về pool.
5. **Rule 5 — Chuẩn hóa Sát thương**: Mọi đòn tấn công gây sát thương phải đi qua `CombatUtility.ResolveDamage` hoặc truyền qua cấu trúc `DamageInfo`. Không trừ máu tùy tiện bằng số thô.
6. **Rule 6 — Đúng 1 Ô Phụ kiện (Accessory)**: Mỗi xe Tank chỉ có đúng 1 ô phụ kiện duy nhất (`EquipmentBrain`), loại bỏ hoàn toàn khái niệm 3 lõi cũ (`Firepower`, `Defense`, `Mobility`).
7. **Rule 7 — UI dùng Canvas (uGUI)**: Mọi thành phần giao diện mới (HUD máu, mana, cooldown, bảng nâng cấp) bắt buộc viết bằng **uGUI Canvas** + TextMeshPro. Tuyệt đối không dùng UI Toolkit.
8. **Rule 8 — Scene riêng biệt**: Mỗi màn chơi trong 9 màn chiến dịch là một Scene độc lập trong `Assets/Scenes/`.

---

## 4. Bảng Công Thức Toán Học Chuẩn (Combat & Formula Matrix)

### 4.1 Sát thương gốc (Raw Damage)
- **Tank**:
  $$\text{Sát thương gốc} = \text{Tấn công} \times \text{Scale Kỹ năng} \times (1 + \text{Tỉ lệ sát thương})$$
- **Kẻ địch**:
  $$\text{Sát thương gốc} = \text{Tấn công} \times \text{Scale Kỹ năng}$$

### 4.2 Phòng thủ hiệu dụng (Effective Def)
- **Tank**: $\text{Def} = \text{Phòng thủ}$
- **Kẻ địch**: $\text{Def} = \text{Phòng thủ} \times (1 - \text{Tỉ lệ xuyên giáp của Tank})$

> ⚠️ **Đã chốt (06/11/2026)** — bản Word của GDD ghi thiếu phần $(1 - \dots)$ ở công thức Def kẻ địch: chỉ còn $\text{Phòng thủ} \times \text{Tỉ lệ xuyên giáp}$, nghĩa là xuyên giáp 0% sẽ vô hiệu hoá giáp hoàn toàn. Công thức đúng là bản trong mục này và trong `CombatUtility.CalculateEffectiveDefense` (`baseDefense * (1f - pen)`); câu mô tả cùng tài liệu ("xuyên giáp: cho phép đòn tấn công **bỏ qua** chỉ số phòng thủ") cũng xác nhận điều đó. **Code là chuẩn**, cần sửa lại dòng công thức trong Word.

### 4.3 Tỉ lệ giảm sát thương (%) — 4 Phân đoạn Logarit liên tục
Hàm giảm sát thương là một hàm liên tục $C^0$ với đồ thị logarit tiệm cận 90%:
- **Phân đoạn 1 ($\text{Def} \in [0, 40]$)**:
  $$\text{Tỉ lệ giảm} = 50 \times \frac{\ln(1 + \text{Def})}{\ln(41)}$$
- **Phân đoạn 2 ($\text{Def} \in (40, 70]$)**:
  $$\text{Tỉ lệ giảm} = 50 + 25 \times \frac{\ln(1 + \text{Def} - 40)}{\ln(31)}$$
- **Phân đoạn 3 ($\text{Def} \in (70, 100]$)**:
  $$\text{Tỉ lệ giảm} = 75 + 10 \times \frac{\ln(1 + \text{Def} - 70)}{\ln(31)}$$
- **Phân đoạn 4 ($\text{Def} > 100$)**:
  $$\text{Tỉ lệ giảm} = 85 + 5 \times \left(1 - \frac{1}{1 + \ln(\text{Def} - 99)}\right)$$

### 4.4 Sát thương thực tế (Final Damage)
$$\text{Sát thương thực tế} = \text{Sát thương gốc} \times \left(1 - \frac{\text{Tỉ lệ giảm}}{100}\right)$$

### 4.5 Mức trần chỉ số (Hard Caps)
- **Tỉ lệ giảm hồi chiêu (Cooldown Reduction)**: Tối đa $60\%$ (`MaxCooldownReduction = 0.60f`).
- **Tỉ lệ xuyên giáp (Armor Penetration)**: Tối đa $100\%$ (`MaxArmorPenetration = 1.0f`).

### 4.6 Nội suy tuyến tính Cấp 1 & Cấp 90
Mỗi xe chỉ lưu 2 mốc chỉ số cơ bản ở Cấp 1 (`baseStatsLevel1`) và Cấp 90 (`baseStatsLevel90`). Chỉ số ở cấp $L \in [1, 90]$:
$$t = \frac{L - 1}{89}$$
$$\text{Stat}(L) = \text{Stat}(1) + (\text{Stat}(90) - \text{Stat}(1)) \times t$$

### 4.7 Tiêu hao "Ngọc ma pháp" nâng cấp xe (`TankUpgradeUtility`)
- Cấp 2 đến 20: **5** Ngọc / cấp.
- Cấp 21 đến 40: **10** Ngọc / cấp.
- Cấp 41 đến 60: **15** Ngọc / cấp.
- Cấp 61 đến 80: **20** Ngọc / cấp.
- Cấp 81 đến 90: **25** Ngọc / cấp.

---

## 5. Chi Tiết Các Hệ Thống Lõi (Core Systems Breakdown)

### 5.1 Hệ thống Chỉ số (`TankStats`)
Các thuộc tính:
- `Attack` (float): Sát thương cơ bản.
- `Defense` (float - flat): Điểm phòng thủ (tính giảm trừ qua `CombatUtility`).
- `DamageRate` (float): Tỉ lệ sát thương thêm ($0 = 0\%, 0.15 = +15\%$).
- `ArmorPenetration` (float): Tỉ lệ xuyên giáp ($0..1$).
- `MaxHealth` (float): Giới hạn máu tối đa.
- `MaxEnergy` (float): Giới hạn năng lượng tối đa.
- `MoveSpeed` (float): Tốc độ di chuyển đơn vị/giây.
- `CooldownReduction` (float): Giảm hồi chiêu ($0..0.6$).

### 5.2 Hệ thống 4 Kỹ Năng & Năng Lượng
Mỗi Tank có đúng 4 slot kỹ năng (`SkillCount = 4`):
- **Slot 0 — Kỹ năng thường**: Không tiêu hao năng lượng. Đánh trúng hoặc dùng kỹ năng sẽ hồi năng lượng cho xe (`GetSkillEnergyGain(0)`). Cooldown ngắn.
- **Slot 1 — Kỹ năng chuyên biệt**: Tiêu hao năng lượng (`GetSkillEnergyCost(1)`). Đòi hỏi đủ năng lượng mới được thi triển.
- **Slot 2 — Kỹ năng tùy chọn 1**: Người chơi chọn 1 trong các kỹ năng Option 1 của xe, tiêu hao năng lượng.
- **Slot 3 — Kỹ năng tùy chọn 2**: Người chơi chọn 1 trong các kỹ năng Option 2 của xe, tiêu hao năng lượng.

### 5.3 Hệ thống Khống Chế (`CrowdControlType`)
Quản lý trạng thái thực thể:
- `Root`: Trói chân $\rightarrow$ `CanMove = false`, `CanLook = true`, `CanUseSkill = true`.
- `Silence`: Câm lặng $\rightarrow$ `CanMove = true`, `CanLook = true`, `CanUseSkill = false`.
- `Stun`: Làm choáng $\rightarrow$ `CanMove = false`, `CanLook = false`, `CanUseSkill = false`.
- `Slow`: Làm chậm $\rightarrow$ Giảm % tốc độ theo `strength`.
- `Knockback`: Đẩy lùi.
- **Miễn nhiễm khống chế (`IsCrowdControlImmune`)**: Tank trong trạng thái đặc biệt (như Wall Titan cố định) hoặc Boss Tinh anh sẽ miễn nhiễm các CC cứng. Khi Boss kháng trói chân, gọi hook `OnCrowdControlResisted` (để kích hoạt sát thương tăng thêm thay cho trói chân theo GDD).

### 5.4 Hệ thống Hiệu Ứng Kéo Dài (`StatusEffect`)
Hỗ trợ Buff, Debuff, DoT, HoT, Shield:
- Quản lý thời gian tồn tại `duration`, `remainingTime`.
- Chu kỳ kích hoạt `tickInterval` (dành cho DoT như Thiêu đốt).
- Số tầng `currentStacks`, `maxStacks`.
- Chính sách stack `EffectStackPolicy`:
  - `RefreshDuration`: Đặt lại thời gian khi thêm tầng mới.
  - `CapOnly`: Tăng tầng nhưng không đặt lại thời gian (áp dụng cho Thiêu đốt theo GDD).
- Các lớp có sẵn: `BurnEffect`, `ShieldEffect`, `StatModifierEffect`.
- Quản lý trên Brain qua `ApplyEffect`, `RemoveEffect`, `HasEffect`, `ClearAllEffects`.

### 5.5 Hệ thống Khiên Giáp (Shield System)
Hệ thống khiên được quản lý đa tầng thông qua các `ShieldEffect` độc lập được nạp vào Brain (`TankBrain` và `EnemyBrain`):
- **Nguồn gốc & Thời gian**: Mỗi khiên có nguồn cụ thể (`Source` từ skill, trang bị), giá trị ban đầu (`ShieldAmount`), giá trị còn lại (`RemainingShield`), và thời gian hiệu lực (`RemainingTime`).
- **Thuộc tính `CurrentShield`**: Được tính động bằng tổng `RemainingShield` của tất cả các `ShieldEffect` còn hiệu lực. Kèm event `OnShieldChangedEvent(float currentShield)` để cập nhật HUD/UI.
- **Quy tắc trừ sát thương (Damage Absorption)**:
  - Khi đối tượng nhận sát thương (`TakeDamage`), sau khi tính toán giảm trừ qua Giáp/Xuyên giáp thu được `FinalDamage`.
  - Nếu đòn đánh có cờ `IsDirectToHealth = true` (sát thương theo thời gian, phần sát thương đã được gánh hộ), sát thương trừ thẳng vào máu, bỏ qua khiên.
  - Ba cờ của `DamageInfo` được tách nghĩa rõ ràng: `IsDirectToHealth` (bỏ qua khâu khiên), `IsRedirected` (đòn đã được chuyển hướng, không chuyển hướng tiếp), `IsDamageOverTime` (tick DoT, không phải một đòn đánh thật). Cơ chế chặn đòn dựa vào `IsDamageOverTime` để một tick cháy không tiêu hao lượt chặn dành cho đòn thật.
  - Ngược lại, gọi `ProcessShieldAbsorption(finalDamage)`:
    $$\text{Sát thương trừ vào máu} = \text{Final Damage} - \text{Giá trị khiên xử lý}$$
    Trong đó: $\text{Giá trị khiên xử lý} \le \text{Final Damage}$.
  - **Thuật toán ưu tiên khiên (Earliest Expiring First)**: Danh sách `ShieldEffect` được sắp xếp theo thời gian còn lại tăng dần (`RemainingTime` nhỏ nhất lên trước).
    1. Khiên nào sắp hết hạn nhất sẽ hấp thụ trước.
    2. Nếu khiên hấp thụ hết giá trị của nó mà vẫn chưa đủ `FinalDamage`: khiên đó cạn về 0 (`RemainingShield = 0`), hết hạn và bị gỡ bỏ, phần sát thương còn thiếu được chuyển tiếp sang `ShieldEffect` tiếp theo.
    3. Nếu giá trị khiên lớn hơn lượng sát thương còn thiếu: `RemainingShield` của khiên đó bị trừ đúng bằng $(\text{FinalDamage} - \text{Giá trị khiên đã xử lý})$, giữ nguyên thời gian tồn tại, và dừng vòng lặp hấp thụ.

### 5.5.1 Chuyển Hướng Sát Thương (Damage Redirection)
Chặng gánh hộ sát thương cho đồng minh, chạy **sau** chặng chặn đòn và **trước** bước tính giáp trong `TakeDamage`:
1. `ProcessDamageRedirection(info)` hỏi lần lượt các `IDamageRedirector` đã đăng ký qua `AddDamageRedirector`.
2. Mỗi cơ chế trả về `(protector, sharedAmount)`; Brain trừ `sharedAmount` khỏi `info.RawDamage` rồi gọi `protector.TakeDamage` với `DamageInfo` mới mang `IsDirectToHealth = true` và `IsRedirected = true`.
3. Phần còn lại đi tiếp pipeline cũ: tính giáp → hấp thụ khiên → trừ máu.

Quy ước bắt buộc:
- Sát thương chia sẻ tính trên `RawDamage` (trước giáp), nên mỗi bên chỉ chịu giáp của mình đúng một lần.
- Đòn đã mang `IsRedirected = true` **không** bị chuyển hướng thêm lần nữa → chặn đệ quy vô hạn.
- Sát thương theo thời gian (`IsDamageOverTime = true`) không bị chuyển hướng.
- Chặng chặn đòn (`5.5.3`) đứng **trước** chuyển hướng: đòn bị chặn sạch là đòn không tồn tại, nên không có sát thương nào để chia cho người gánh hộ. Nếu đảo lại (chia trước, chặn sau) thì người gánh hộ vẫn mất máu dù đòn đã bị phủ định.
- Chuyển hướng chạy **trước** bước hấp thụ khiên, nên mục tiêu đang có khiên vẫn được gánh hộ (nếu đặt sau, phần còn lại có thể bằng 0 và cơ chế mất tác dụng).
- Bên gánh nhận sát thương trực tiếp vào máu, bỏ qua khiên của chính họ (đúng ngữ nghĩa cờ `IsDirectToHealth`); tuy nhiên chặng chặn đòn của chính họ vẫn chạy bình thường, nên trạng thái bất tử/đỡ đòn vẫn che được phần gánh hộ.

### 5.5.2 Bổ Sung Cho Hệ Khiên
- `ShieldEffect.MaxAbsorbHits` / `AbsorbHits`: giới hạn số đòn một lớp khiên chịu được (mặc định `int.MaxValue` = không giới hạn). Chạm giới hạn thì lớp khiên vỡ ngay và phần khiên còn dư bị huỷ.
- `TankBrain.AddShield(...)` trả về `ShieldEffect` đang hoạt động thật (nếu trùng `EffectId` thì trả về lớp khiên cũ đã được làm mới), để nơi gọi giữ được tham chiếu.
- `OnShieldAbsorbedEvent(ShieldEffect, float, bool)`: phát cho từng lớp khiên sau khi hấp thụ xong; cờ `bool` cho biết đòn có bị chặn sạch hay không. Event phát **sau** bước dọn dẹp, nên lớp khiên vừa vỡ đã ở trạng thái `IsExpired = true`, `RemainingShield = 0`.
- `DamageInfo.AbsorbedByShield` và `DamageInfo.HealthDamage`: giữ nguyên ngữ nghĩa cũ của `FinalDamage` (sau giáp, trước khiên); sát thương thực tế trừ vào máu đọc qua `HealthDamage`.

### 5.5.3 Chặn Sát Thương (Damage Interception)
Cơ chế "đỡ đúng N đòn", "bất tử tạm thời", "né đòn" **không** dùng `ShieldEffect`, vì chúng không có trữ lượng nào để hấp thụ. Chúng dùng một chặng riêng:
1. Hiệu ứng implement `IDamageInterceptor` (`TryIntercept(DamageInfo)`) và được nạp vào Brain bằng `ApplyEffect` như mọi hiệu ứng khác — không cần đăng ký riêng.
2. Trong `TakeDamage`, Brain gọi `ProcessDamageInterception(info)` ở **bước đầu tiên** — trước cả chặng chuyển hướng và bước tính giáp.
3. Hiệu ứng đầu tiên trả `true` chặn sạch đòn: Brain dừng ngay, không trừ máu, không trừ khiên, không phát `OnTakeDamageEvent` (vì đây không phải một lần nhận sát thương).

Quy ước:
- `DamageInterceptEffect` là hiệu ứng chặn đòn dựng sẵn: `MaxNegateHits` / `NegatedHits` (số đòn), `BlockDamageOverTime`, `BlockRedirected`, và event `OnNegated(DamageInfo)` để skill bật burst / né.
- Mặc định `BlockDamageOverTime = false` (một tick cháy không ăn mất lượt chặn dành cho đòn chí mạng) và `BlockRedirected = true` (trạng thái bất tử phải đỡ cả phần gánh hộ).
- Vì chặng chặn đứng trước chặng chuyển hướng, một hiệu ứng "đỡ đúng 1 đòn" trên mục tiêu sẽ phủ định **toàn bộ** đòn: không phần nào bị chia sang người gánh hộ và lượt chặn bị tiêu đúng một lần.
- Thứ tự ưu tiên: hiệu ứng có `RemainingTime` nhỏ nhất được hỏi trước — cùng nguyên tắc Earliest Expiring First với hệ khiên, tránh lãng phí lượt chặn của hiệu ứng sắp hết hạn.
- Khi chạm `MaxNegateHits`, hiệu ứng tự đặt `RemainingTime = 0` để hệ thống hiệu ứng dọn dẹp ở tick kế tiếp.
- Phần hình ảnh (bóng ma xoay quanh đồng minh, burst, tia né) vẫn nằm ở skill MonoBehaviour: skill subscribe `OnNegated` rồi bật/tắt các GameObject con đã dựng sẵn trên prefab.
- HUD chỉ đọc `CurrentShield` cho khiên thật; hiệu ứng chặn đòn không làm sai số liệu khiên.

### 5.6 Kẻ Địch & Cơ Chế Bùng Nổ Năng Lượng (`EliteEnemyBrain`)
- **Quái thường (`NormalEnemyBrain`)**: Có các chỉ số `EnemyStats`, AI di chuyển, tấn công.
- **Boss Tinh anh (`EliteEnemyBrain`)**:
  - Sở hữu các Mạch Năng Lượng (`EnergyCore`).
  - Khi máu xuống dưới ngưỡng `energyBurstHpThreshold` (mặc định $\le 50\%$), tự động kích hoạt **Bùng nổ năng lượng** (`OpenCores()`).
  - Người chơi có khoảng thời gian `breakWindowDuration` để phá hủy toàn bộ mạch.
  - Phá thành công: Boss bị giảm phòng thủ trong `defenseBreakDuration` giây.
  - Hết giờ không phá hết: Boss tung chiêu suy yếu diện rộng.
- **Chế độ Co-op**: Gọi `SetCoopMode(true)` khi chơi Co-op để tự động nhân đôi `MaxHealth` cho toàn bộ quái vật theo GDD mục 5.1.

---

## 6. Danh Mục Các Lớp Base & Hướng Dẫn Sử Dụng (Base Classes Reference)

Dưới đây là chi tiết toàn bộ các lớp nền tảng (Base / Abstract Classes) trong dự án, bao gồm trách nhiệm, các hook cần override và cách sử dụng đúng chuẩn.

### 6.1 Nhóm Thực Thể Xe Tăng (Tank System)

#### 1. `TankBrain` (Abstract Class — `Assets/Scripts/Tanks/TankBrain.cs`)
- **Vai trò**: Quản lý toàn bộ Runtime State và Logic của một chiếc xe tăng (Máu, Năng lượng, Khiên, 4 Cooldowns, Khống chế CC, Buff/Debuff Status Effects, 1 Phụ kiện).
- **Trách nhiệm**:
  - Nhận lệnh điều khiển (`SetMoveInput`, `SetLookTarget`, `ClearLookTarget`, `UseSkill`).
  - Xử lý va chạm qua `TakeDamage`, `Heal`, `AddShield`, `ProcessShieldAbsorption`.
  - Quản lý và tính toán lại chỉ số: `AddBonusStats`, `RemoveBonusStats`, `RecalculateStats`.
- **Các hàm / Hook bắt buộc hoặc cho phép Override**:
  - `GetBaseCooldown(int index)` *(abstract)*: Trả về thời gian hồi chiêu gốc của kỹ năng `0..3`.
  - `GetSkillEnergyCost(int index)` *(virtual)*: Trả về năng lượng tiêu hao (Skill 0 mặc định 0, Skill 1..3 mặc định 30).
  - `GetSkillEnergyGain(int index)` *(virtual)*: Trả về năng lượng hồi khi dùng chiêu (Skill 0 mặc định hồi 10).
  - `OnUseSkill(int index)` *(virtual)*: Kích hoạt logic cụ thể của chiêu (bắn đạn qua pool, tạo tường, buff khiên...).
  - `OnBrainTick(float deltaTime)` *(virtual)*: Chạy logic bổ sung mỗi frame (hồi phục thụ động, đếm timer nội tại).
  - `OnDeath()` *(virtual)*: Dọn dẹp hoặc kích hoạt nội tại khi xe bị tiêu diệt.
- **Cách dùng**:
  - Tạo class kế thừa: `public class TankBrain_TenXe : TankBrain`.
  - Gắn component này lên root GameObject của Tank Prefab.

#### 2. `TankView` (MonoBehaviour — `Assets/Scripts/Tanks/TankView.cs`)
- **Vai trò**: Đại diện cho tầng Visual & Vật lý của Tank (Sprite, Animator, Pivots xoay, Collider, Rigidbody).
- **Trách nhiệm**:
  - Chạy animation và xoay tháp pháo (`firepowerPivot`), thân xe (`bodyPivot`).
  - Cung cấp vị trí nòng súng (`Muzzle`) cho đạn đạo xuất phát.
  - Tuyệt đối không chứa logic tính toán sát thương hay đọc Input.
- **Cách dùng**:
  - Tạo prefab riêng chứa `SpriteRenderer`, `Animator`, `TankView` và `ColliderOwnerRef`.
  - Đăng ký danh sách các prefab View vào `TankDefinition.viewPrefabs`.
  - `TankBrain` tự động instantiate View tương ứng thành con của nó lúc `Initialize()`.

#### 3. `TankDefinition` (ScriptableObject — `Assets/Scripts/Tanks/TankDefinition.cs`)
- **Vai trò**: Asset cấu hình định nghĩa tĩnh cho một dòng xe tăng.
- **Trách nhiệm**:
  - Chứa thông tin mô tả, 2 mốc chỉ số cơ bản (`baseStatsLevel1` và `baseStatsLevel90`).
  - Tham chiếu tới `tankPrefab` (chứa `TankBrain`), mảng `viewPrefabs` (chứa các skin/model `TankView`), và `equipmentDatabase`.
  - Cung cấp hàm Factory: `CreateTank(Transform parent = null, Vector3 position = default) -> TankBrain` để sinh xe hoàn chỉnh trong 1 dòng lệnh.
- **Cách dùng**:
  - Tạo asset qua Unity Menu: `Create -> IronWasteland -> Tank -> Tank Definition`.
  - Kéo thả vào `GameController` hoặc Level Spawner để sinh xe tăng cho người chơi.

#### 4. `EquipmentBrain` (MonoBehaviour — `Assets/Scripts/Tanks/Equipment/EquipmentBrain.cs`)
- **Vai trò**: Lớp cơ sở của Phụ kiện (Accessory) cho xe tăng (mỗi xe có đúng 1 ô).
- **Trách nhiệm**:
  - Cung cấp chỉ số cộng phẳng (`flatBonus`) và chỉ số cộng % (`percentBonus`).
  - Cung cấp các hook để kích hoạt nội tại kỹ năng bị động (Passive Skill).
- **Các Hook cho phép Override**:
  - `OnEquipped(TankBrain owner)`: Gọi khi phụ kiện vừa được gắn vào xe.
  - `OnUnequipped(TankBrain owner)`: Gọi khi phụ kiện bị tháo ra (dọn dẹp buff).
  - `OnOwnerUpdate(float deltaTime)`: Chạy cập nhật mỗi frame theo xe.
  - `OnOwnerTookDamage(DamageInfo info)`: Kích hoạt khi xe của người chơi nhận sát thương (phản đòn, tạo khiên khẩn cấp...).
  - `OnOwnerDealtDamage(DamageInfo info)`: Kích hoạt khi xe gây sát thương (hút máu, đốt máu kẻ địch...).
  - `OnOwnerUsedSkill(int skillIndex)`: Kích hoạt khi xe vừa dùng 1 chiêu (cường hóa đòn đánh kế tiếp...).
- **Cách dùng**:
  - Tạo class kế thừa: `public class Accessory_BioticCrystal : EquipmentBrain`.
  - Override các hook cần thiết, tạo prefab và đăng ký vào `EquipmentDatabase`.

---

### 6.2 Nhóm Kẻ Địch (Enemy System)

#### 1. `EnemyBrain` (Abstract Class — `Assets/Scripts/Enemies/EnemyBrain.cs`)
- **Vai trò**: Lớp cha trừu tượng của toàn bộ kẻ địch trong game (chứa HP, EnemyStats, CC, Status Effects, Hệ thống khiên, chế độ Co-op).
- **Trách nhiệm**:
  - Quản lý máu `currentHealth`, áp dụng công thức giảm trừ phòng thủ và xuyên giáp qua `TakeDamage(DamageInfo)`.
  - Quản lý khống chế (`CrowdControlType`) và các lớp hiệu ứng `StatusEffect`.
  - Quản lý hấp thụ khiên `ProcessShieldAbsorption` và bắn `OnShieldChangedEvent`.
  - Tự động nhân đôi HP khi gọi `SetCoopMode(true)`.
- **Các hàm / Hook bắt buộc hoặc cho phép Override**:
  - `OnBrainTick(float deltaTime)` *(abstract)*: Vòng lặp AI của quái (truy đuổi, kiểm tra tầm bắn, đếm cooldown kỹ năng quái).
  - `OnDeath()` *(abstract)*: Logic khi quái chết (bắn hiệu ứng vỡ vụn, rơi ngọc, cộng điểm...).
  - `OnDamaged(float amount)` *(virtual)*: Phản ứng khi nhận sát thương (kêu la, chuyển mục tiêu aggro).
  - `OnContact(ColliderOwnerRef other)` *(virtual)*: Xử lý va chạm vật lý trực tiếp với xe hoặc đạn.
  - `OnCrowdControlResisted(CrowdControlType type, float strength)` *(virtual)*: Kích hoạt khi quái miễn nhiễm CC (ví dụ nhận thêm sát thương thay vì bị trói).
- **Cách dùng**:
  - Thường kế thừa thông qua `NormalEnemyBrain` hoặc `EliteEnemyBrain`.

#### 2. `NormalEnemyBrain` (Abstract Class — `Assets/Scripts/Enemies/NormalEnemyBrain.cs`)
- **Vai trò**: Khung cho các loại quái thường (chạy bộ, bắn tỉa, cảm tử...).
- **Cách dùng**:
  - Kế thừa: `public class NormalEnemyBrain_MeleeCrawler : NormalEnemyBrain`.
  - Cài đặt AI di chuyển và tấn công trong `OnBrainTick`.

#### 3. `EliteEnemyBrain` (Abstract Class — `Assets/Scripts/Enemies/EliteEnemyBrain.cs`)
- **Vai trò**: Khung dành riêng cho Boss Tinh Anh có cơ chế **Bùng nổ năng lượng** (`EnergyCore`).
- **Trách nhiệm**:
  - Miễn nhiễm khống chế cứng (`IsCrowdControlImmune = true`).
  - Tự động mở các Mạch Năng Lượng (`OpenCores()`) khi máu xuống dưới ngưỡng `energyBurstHpThreshold` (mặc định $\le 50\%$).
  - Theo dõi cửa sổ thời gian phá mạch (`breakWindowDuration`).
  - Giảm phòng thủ Boss (`defenseBreakBonus`) nếu người chơi phá hết mạch trong thời gian cho phép.
  - Tung chiêu trừng phạt cực mạnh nếu người chơi không phá kịp.
- **Các Hook cho phép Override**:
  - `OnCoreOpened(int coreIndex)`: Gọi khi mạch thứ i bắt đầu mở (bật VFX/Collider điểm yếu).
  - `OnCoreBroken(int coreIndex)`: Gọi khi 1 mạch bị người chơi bắn vỡ.
  - `OnAllCoresBroken()`: Gọi khi toàn bộ mạch bị phá thành công (choáng boss, giảm phòng thủ).
  - `OnBurstWindowExpired(int remainingCores)`: Gọi khi hết giờ mà vẫn còn mạch chưa vỡ (Boss dùng ultimate).

#### 4. `EnemyModel` (MonoBehaviour — `Assets/Scripts/Enemies/EnemyModel.cs`)
- **Vai trò**: Tầng Visual & Collider của quái vật.
- **Khác biệt với TankView**: Nằm trực tiếp làm con trong cùng Prefab với `EnemyBrain` (không tách prefab độc lập).
- **Cách dùng**:
  - Gắn `SpriteRenderer`, `BoxCollider2D`, `ColliderOwnerRef` và `EnemyModel` lên GameObject con `Model` bên dưới GameObject cha chứa `EnemyBrain`.

---

### 6.3 Nhóm Hiệu Ứng Trạng Thái (Status Effect System)

#### 1. `StatusEffect` (Abstract Class — `Assets/Scripts/Core/Effects/StatusEffect.cs`)
- **Vai trò**: Lớp cơ sở cho mọi hiệu ứng kéo dài: Buff chỉ số, Debuff, DoT (Sát thương theo thời gian), HoT (Hồi máu theo thời gian), Khiên ảo...
- **Cơ chế Stacking (`EffectStackPolicy`)**:
  - `RefreshDuration`: Mỗi lần thêm tầng mới thì reset `remainingTime = duration`.
  - `CapOnly`: Tăng số tầng nhưng giữ nguyên thời gian tồn tại (áp dụng cho Thiêu đốt).
- **Các Hook cho phép Override**:
  - `OnApplied(UnityEngine.Object target)`: Kích hoạt khi hiệu ứng vừa gắn vào thực thể.
  - `OnRemoved(UnityEngine.Object target)`: Dọn dẹp khi hiệu ứng kết thúc hoặc bị thanh tẩy.
  - `OnTick(UnityEngine.Object target)`: Chạy định kỳ mỗi khoảng `tickInterval` (dành cho DoT/HoT).
  - `OnStackAdded(UnityEngine.Object target, int newStacks)`: Xử lý khi được cộng thêm tầng.
- **Cách dùng**:
  - Để áp dụng: `brain.ApplyEffect(new TenEffect(...));`.
  - Các lớp có sẵn trong project:
    - [`ShieldEffect`](file:///f:/Leaning/Project/IronWasteland/Assets/Scripts/Core/Effects/ShieldEffect.cs): Khiên ảo có `Source`, `ShieldAmount`, `RemainingShield`, hấp thụ sát thương ưu tiên theo thời gian hết hạn trước.
    - [`BurnEffect`](file:///f:/Leaning/Project/IronWasteland/Assets/Scripts/Core/Effects/BurnEffect.cs): Thiêu đốt gây DoT mỗi giây, stack theo `CapOnly`.
    - [`StatModifierEffect`](file:///f:/Leaning/Project/IronWasteland/Assets/Scripts/Core/Effects/StatModifierEffect.cs): Tăng/giảm chỉ số có thời hạn.

---

### 6.4 Nhóm Định Danh & Tương Tác Vật Lý (Owner & Collision System)

#### 1. `OwnerRef` (MonoBehaviour — `Assets/Scripts/Core/OwnerRef.cs`)
- **Vai trò**: Gắn lên GameObject bất kỳ để xác định đối tượng cấp cao nào đang sở hữu nó (`TankBrain`, `EnemyBrain`...).
- **Cách dùng**:
  ```csharp
  if (OwnerRef.From(hitCollider).TryGetOwner(out TankBrain tank)) {
      // Tương tác trực tiếp với TankBrain
  }
  ```

#### 2. `ColliderOwnerRef` (MonoBehaviour — `Assets/Scripts/Core/ColliderOwnerRef.cs`)
- **Vai trò**: Kế thừa `OwnerRef`, bắt buộc gắn cùng `Collider2D`. Cung cấp toàn bộ sự kiện Trigger/Collision và quản lý danh sách các collider đang tiếp xúc để tránh sót va chạm.
- **Các Sự kiện cung cấp**:
  - Trigger: `TriggerEnter`, `TriggerStay`, `TriggerExit`.
  - Collision: `CollisionEnter`, `CollisionStay`, `CollisionExit`.
  - Event gộp nhanh: `Contact` (bắn ra `Action<ColliderOwnerRef>`).
- **Cách dùng**:
  - Luôn sử dụng `ColliderOwnerRef.FromCollider(col)` thay vì `GetComponent<T>()` trực tiếp khi bắt va chạm trong đạn hoặc hitbox.

---

## 7. Mẫu Code Triển Khai Chuẩn (Implementation Templates)

### 7.1 Mẫu tạo TankBrain cụ thể mới
```csharp
using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks.WallTitan
{
    [DisallowMultipleComponent]
    public class TankBrain_WallTitan : TankBrain
    {
        protected override float GetBaseCooldown(int index)
        {
            switch (index)
            {
                case 0: return 0.6f;  // Earth Break
                case 1: return 10f;   // Earth Wall
                case 2: return 12f;   // Stone Fortress (Option 1)
                case 3: return 10f;   // Immovable Titan (Option 2)
                default: return 5f;
            }
        }

        protected override float GetSkillEnergyCost(int index)
        {
            switch (index)
            {
                case 0: return 0f;    // Thường không tốn
                case 1: return 45f;   // Earth Wall
                case 2: return 50f;   // Option 1
                case 3: return 0f;    // Immovable Titan hồi mana
                default: return 30f;
            }
        }

        protected override float GetSkillEnergyGain(int index)
        {
            if (index == 0) return 10f; // Earth Break hồi 10 mana
            if (index == 3) return 40f; // Immovable Titan hồi 40 mana
            return 0f;
        }

        protected override void OnUseSkill(int index)
        {
            base.OnUseSkill(index);
            // Kích hoạt skill tương ứng qua Pool
        }
    }
}
```

### 7.2 Mẫu tạo Phụ Kiện (Accessory) mới
```csharp
using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks
{
    public class Accessory_BioticCrystal : EquipmentBrain
    {
        [Tooltip("Tỷ lệ chuyển sát thương kỹ năng thành máu (0.12 = 12%).")]
        [SerializeField] private float healConversionRate = 0.12f;

        public override void OnOwnerDealtDamage(DamageInfo info)
        {
            if (Owner != null && info.FinalDamage > 0f)
            {
                float healAmount = info.FinalDamage * healConversionRate;
                Owner.Heal(healAmount);
                // Tìm đồng minh thấp máu nhất xung quanh 5m để hồi máu (nếu có)
            }
        }
    }
}
```

### 7.3 Mẫu tạo Kỹ Năng Projectile (với Local Pooling)
```csharp
public class Skill_Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 12f;
    [SerializeField] private float lifetime = 5f;

    private ColliderOwnerRef m_OwnerRef;
    private Action<Skill_Projectile> m_ReturnToPool;
    private float m_Timer;

    public event Action<Skill_Projectile, EnemyBrain> TargetFound;

    public void Initialize(Vector2 direction, Action<Skill_Projectile> returnCallback)
    {
        Clear();
        m_ReturnToPool = returnCallback;
        m_Timer = 0f;
        transform.right = direction;
        GetComponent<Rigidbody2D>().linearVelocity = direction * speed;
    }

    public void Clear()
    {
        TargetFound = null;
        m_ReturnToPool = null;
    }

    private void Update()
    {
        m_Timer += Time.deltaTime;
        if (m_Timer >= lifetime) Finish();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var targetRef = ColliderOwnerRef.FromCollider(other);
        if (targetRef != null && targetRef.TryGetOwner(out EnemyBrain enemy))
        {
            TargetFound?.Invoke(this, enemy);
            Finish();
        }
    }

    private void Finish()
    {
        var cb = m_ReturnToPool;
        Clear();
        gameObject.SetActive(false);
        cb?.Invoke(this);
    }
}
```

---

## 8. Bản Đồ Thư Mục Mã Nguồn (Codebase Directory Map)

```
Assets/Scripts/
├── Core/                               # Các thành phần cốt lõi dùng chung
│   ├── CombatUtility.cs                # Công thức tính sát thương, Def, Logarit giảm trừ
│   ├── DamageInfo.cs                   # Struct đóng gói thông tin đòn đánh
│   ├── CrowdControlType.cs             # Enum và class quản lý Khống chế (Root, Stun...)
│   ├── OwnerRef.cs                     # Nhận diện chủ sở hữu GameObject
│   ├── ColliderOwnerRef.cs             # Nhận diện chủ sở hữu trên Collider + Event va chạm
│   ├── TextDamageManager.cs            # Singleton pooling hiển thị số sát thương
│   ├── ReadOnlyAttribute.cs            # Custom attribute khóa Inspector
│   └── Effects/                        # Hệ thống Status Effect
│       ├── StatusEffect.cs             # Lớp cơ sở hiệu ứng (DoT, HoT, Stacking)
│       ├── BurnEffect.cs               # Hiệu ứng thiêu đốt
│       ├── ShieldEffect.cs             # Hiệu ứng khiên ảo
│       └── StatModifierEffect.cs       # Hiệu ứng tăng/giảm chỉ số có thời hạn
├── Tanks/                              # Hệ thống xe Tank
│   ├── TankBrain.cs                    # Lớp cha điều khiển Runtime State của Tank
│   ├── TankBrainContext.cs             # Struct dữ liệu khởi tạo Tank
│   ├── TankDefinition.cs               # ScriptableObject định nghĩa xe (Level 1 & 90)
│   ├── TankStats.cs                    # Bảng chỉ số (Base, Flat, Percent, Final)
│   ├── TankUpgradeUtility.cs           # Tiện ích tính Ngọc ma pháp nâng cấp xe
│   ├── TankView.cs                     # Prefab con hiển thị (Sprite, Animator, Pivots)
│   ├── GameController.cs               # Nhận Input từ người chơi và điều khiển Tank
│   ├── Equipment/                      # Hệ thống 1 ô Phụ kiện
│   │   ├── EquipmentBrain.cs           # Lớp cha của 1 Phụ kiện (Accessory)
│   │   ├── EquipmentDatabase.cs        # SO quản lý danh sách phụ kiện
│   │   └── Accessory_Test.cs           # Phụ kiện mẫu kiểm thử
│   ├── BlackShadow/                    # Triển khai xe Black Shadow (Brain + 2 Skill mẫu)
│   ├── WallTitan/                      # Triển khai xe Wall Titan
│   ├── RedNova/                        # Triển khai xe Red Nova
│   ├── Leviathan/                      # Triển khai xe Leviathan
│   ├── WaterB/                         # Triển khai xe Water B
│   └── GreenFox/                       # Triển khai xe Green Fox
├── Enemies/                            # Hệ thống Kẻ địch
│   ├── EnemyBrain.cs                   # Lớp cha runtime state của địch
│   ├── EnemyModel.cs                   # Child GameObject hiển thị và collider của địch
│   ├── EnemyStats.cs                   # Bảng chỉ số của kẻ địch
│   ├── NormalEnemyBrain.cs             # Khung kẻ địch thường
│   ├── EliteEnemyBrain.cs              # Khung Boss Tinh anh (Bùng nổ năng lượng)
│   ├── NormalEnemyBrain_Test.cs        # Địch thường mẫu
│   └── EliteEnemyBrain_Test.cs         # Boss Tinh anh mẫu
├── Utilities/                          # Tiện ích toán học & hình học
│   └── Collider2DUtility.cs            # Tiện ích lấy tâm giao nhau, điểm va chạm Collider
└── Editor/                             # Script hỗ trợ Unity Editor
    └── ReadOnlyDrawer.cs               # PropertyDrawer cho ReadOnlyAttribute
```

---

## 9. Lộ Trình Phát Triển Kế Tiếp (Next Steps Roadmap)

1. **Giao diện người dùng (uGUI Canvas)**:
   - Xây dựng HUD Screen Space: Thanh Máu (`Slider`), Thanh Năng lượng (`Slider`), Lớp Khiên chắn (`Slider` đè), 4 Icon kỹ năng với Cooldown Overlay xoay tròn (`Image` type Filled).
2. **Triển khai kỹ năng đầy đủ cho 6 xe Tank**:
   - Hoàn thiện bộ skill theo mô tả GDD cho `WallTitan`, `RedNova`, `BlackShadow`, `Leviathan`, `WaterB`, `GreenFox`.
3. **Triển khai 20 loại Phụ kiện**:
   - Viết các lớp kế thừa `EquipmentBrain` cho 20 phụ kiện trong Mục 4.4 của GDD.
4. **Hệ thống Quái vật & Boss theo Chương**:
   - Chương 1 (Lục Địa Đen): 5 loại quái thường (Ký sinh thiên thạch, Xạ thủ tinh thể, Quái vật thạch anh, Mặt trăng đen, Tinh linh nghĩa địa) + 2 Boss (Bóng Ma Tử Địa, SeauBearus).
   - Chương 2 (Vùng Đất Nguyên Tố): 7 loại quái thường (Kiến lửa, Quái đầm lầy, Nấm độc, Sao đen, Mặt trời đỏ, Xén nước, Tinh linh sấm sét) + 3 Boss (Tử Thần Nguyên Tố, The2Face, D1onk).
5. **Stage & Spawner**:
   - Thiết lập các Scene cho từng màn chơi, gắn `WaveSpawner` và `TriggerZone` điều phối đợt quái.
6. **Mạng LAN Co-op**:
   - Tích hợp giải pháp mạng LAN đồng bộ chuyển động và combat giữa 2–4 người chơi theo mô hình Host/Client.

---

## 10. Dữ Liệu Thiết Kế Tham Chiếu (trích GDD)

> **Nguồn:** `Tài liệu thiết kế Game.docx` (22.6 MB, cập nhật 06/11/2026) — file này **không** nằm trong git. Mục này là bản rút gọn để code và AI tra cứu nhanh; **khi lệch số liệu, bảng trong Word là chuẩn**, trừ các điểm đã chốt ở §10.7.

### 10.1 Chỉ số 6 xe tăng (Cấp 1 → Cấp 90)

| Xe | Vai trò | Tấn công | Máu | Năng lượng | Phòng thủ | Tốc độ | Giảm hồi chiêu | Tỉ lệ sát thương | Xuyên giáp |
|---|---|---|---|---|---|---|---|---|---|
| Wall Titan | Tanker / Support | 15 → 50 | 1200 → 6500 | 100 → 300 | 25 → 55 | 100 → 100 | 0% → 20% | 0% → 0% | 0% → 0% |
| Red Nova | DPS | 25 → 95 | 600 → 3200 | 100 → 300 | 10 → 15 | 100 → 100 | 0% → 0% | 0% → 15% | 0% → 25% |
| Black Shadow | DPS / Support | 20 → 90 | 650 → 3400 | 120 → 300 | 12 → 18 | 110 → 130 | 0% → 15% | 0% → 15% | 0% → 0% |
| Leviathan | Tanker | 5 → 45 | 1300 → 7000 | 120 → 350 | 28 → 60 | 100 → 100 | 0% → 10% | 0% → 0% | 0% → 0% |
| Water B | Support / DPS | 15 → 75 | 650 → 3500 | 100 → 300 | 12 → 18 | 100 → 110 | 0% → 30% | 0% → 0% | 0% → 0% |
| Green Fox | DPS | 25 → 85 | 700 → 3600 | 100 → 300 | 12 → 20 | 100 → 100 | 0% → 0% | 0% → 10% | 0% → 5% |

⚠️ Tốc độ trong GDD dùng thang "chuẩn 100" (quái 70–140). `TankStats.MoveSpeed` ở runtime là đơn vị/giây (3–7.5) → phải chốt một hệ số quy đổi trước khi nhập vào `TankDefinition`, nếu không xe sẽ chạy sai tốc độ.

### 10.2 Bộ kỹ năng: 4 slot, hai slot sau là **pool lựa chọn**

| Xe | Kỹ năng thường | Kỹ năng chuyên biệt | Tuỳ chọn 1 (chọn 1 trong 3) | Tuỳ chọn 2 (chọn 1 trong 3) |
|---|---|---|---|---|
| Wall Titan | Earth Break | Earth Wall | Stone Fortress · Guardian Wall · Earth Prison | Immovable Titan · Earth Guardian · Earthquake |
| Red Nova | Nova Shot | Triple Nova | Overdrive · Laser Cannon · Execution Core | Nova Barrage · Nova Pierce · Nova Rush |
| Black Shadow | Shadow Claw | Shadow Step | Shadow Predator · Shadow Link · Shadow Guard | Shadow Ambush · Shadow Possession · Shadow Exchange |
| Leviathan | Water Pulse | Water Bubble | Water Armor · Guardian Current · Regenerative Shell | Tidal Return · Leviathan Form · Tide Commander |
| Water B | Healing Water | Bubble Shield | Shared Healing · Life Drain · Bubble Protector | Mobile Altar · Water Turrets · Aqua Assault |
| Green Fox | Fox Fang | Fox Mark | Mark Hunter · Burning Fox · Fox Trap | Mark Explosion · Fox Territory · Nine-Tailed Assault |

Tổng cộng **8 kỹ năng mỗi xe** (2 cố định + 6 trong pool). `SkillCount = 4` là 4 **slot đang gắn**, không phải 4 kỹ năng tồn tại.

### 10.3 Kẻ địch

**Chương 1 — Lục Địa Đen**

| Tên | HP | HP Co-op | ATK | Giáp | Tốc độ | Hồi đòn |
|---|---|---|---|---|---|---|
| Ký sinh thiên thạch | 100 | 200 | 150 | 0 | 125 | — (nổ khi chạm) |
| Xạ thủ tinh thể | 200 | 400 | 75 | 25 | 80 | 1.5s |
| Quái vật thạch anh | 220 | 440 | 50 | 15 | 85 | 1.5s |
| Mặt trăng đen | 260 | 520 | 15 | 50 | 95 | 2.0s |
| Tinh linh nghĩa địa | 250 | 500 | 35 | 10 | 135 | — (để lại vệt lửa) |
| **Bóng Ma Tử Địa** (boss) | 2000 | 4000 | 120 | 45 | 90 | — |
| **SeauBearus** (boss) | 1800 | 3600 | 180 | 40 | 85 | — |

**Chương 2 — Vùng Đất Nguyên Tố**

| Tên | HP | HP Co-op | ATK | Giáp | Tốc độ | Hồi đòn |
|---|---|---|---|---|---|---|
| Kiến lửa khổng lồ | 420 | 840 | 130 | 30 | 105 | 2.0s |
| Quái vật đầm lầy | 380 | 760 | 180 | 35 | 75 | 3.5s |
| Nấm độc gai thạch | 380 | 760 | 110 | 25 | 0 | 6.0s |
| Sao đen | 450 | 900 | 100 | 18 | 0 | 3.0s |
| Mặt trời đỏ | 720 | 1400 | 240 | 40 | 0 | 8.0s |
| Xén nước | 360 | 720 | 150 | 24 | 140 | 2.5s |
| Tinh linh sấm sét | 260 | 520 | 170 | 10 | 95 | 2.0s |
| **Tử Thần Nguyên Tố** (boss 2-4) | 9500 | 18000 | 250 | 25 | 90 | — |
| **The2Face** (boss 2-4) | 16000 | 32000 | 350 | 30 | 100 | — |
| **D1onk** (final 2-5) | 27000 | 54000 | 500 | 60 | 70 | — |

Co-op nhân đôi HP — khớp `EnemyBrain.SetCoopMode(true)`.

### 10.4 Chín màn chơi

| Màn | Tên ải | Điều kiện thắng | Thời lượng | Ngọc | Phụ kiện rơi | Mở khoá |
|---|---|---|---|---|---|---|
| 1-1 | Sương Mù Tím | Diệt 40 quái | ~90s | 20 | 1 món | — |
| 1-2 | Hang Đá Gai | Sinh tồn 100 giây | 100s | 30 | 1 món | Black Shadow |
| 1-3 | Dấu Vết Tù Binh | Kích hoạt 2 Trạm Radar + hộ tống xe Mones 150m | ~120s | 40 | 1 món | Leviathan |
| 1-4 | Vòng Xoáy Không Gian | Diệt Bóng Ma Tử Địa **và** SeauBearus (4 phase) | ~180s | 60 | 3 món | — |
| 2-1 | Vành Đai Bão Lửa | Diệt 50 quái | ~90s | 50 | 1 món | Water B |
| 2-2 | Đầm Lầy Độc Khí & Bão Điện | Phá 4 Cụm Nấm Mẹ + sinh tồn 120s | 120s | 60 | 1 món | — |
| 2-3 | Đột Kích Tiền Đồn | Bảo vệ Xe phá thành, phá 3 Trụ Điện Trường | ~130s | 70 | 1 món | Green Fox |
| 2-4 | Phi Thuyền Hai Mặt & Pháo Đài Hira | Diệt Tử Thần Nguyên Tố **và** The2Face (4 phase) | ~200s | 90 | 2 món | — |
| 2-5 | Chiến Hạm D1ONK | Diệt D1onk (3 lõi năng lượng hộ thể) | ~240s | 120 | 3 món | — |

Màn 1-4 và 2-4 là **boss kép 4 giai đoạn**: quái mở đầu → boss 1 → quái tiếp viện → boss 2.

### 10.5 Cơ chế môi trường

| Màn | Cơ chế |
|---|---|
| 1-3 | 2 Trạm Radar — 1 xe đứng trong bán kính 3m suốt 5s (sau khi dọn quái canh gác); hộ tống xe Mones 150m qua 3 đợt phục kích |
| 2-2 | 4 Cụm Nấm Mẹ, mỗi cụm có 3 Nấm độc + 1 Sao đen canh gác; quấy nhiễu liên tục bằng 12 Tinh linh sấm + 12 Quái đầm lầy |
| 2-3 | Xe phá thành phải sống sót phá 3 Trụ Điện Trường; trụ 1–2 có 2 Sao đen + 8 Kiến lửa + 5 Xén nước; trụ 3 (cổng thành) có 4 Mặt trời đỏ + 10 Tinh linh sét |
| 2-5 | Kỳ hạm Hira: 4 trụ plasma 4 góc, 3 lõi năng lượng hộ thể phải phá để mở đường vào D1onk |

### 10.6 Hai mươi phụ kiện

| # | Tên | Chỉ số nền | Hiệu ứng đặc biệt |
|---|---|---|---|
| 1 | Lõi Động Cơ Trọng Lực Địa Mạch | +35% DEF | Đứng yên 1.5s: miễn đẩy lùi, +35% DEF |
| 2 | Lõi Huyết Tinh Sinh Học | +15% ATK, +15% HP | 12% sát thương kỹ năng hồi máu cho bản thân + 1 đồng minh thấp máu nhất trong 5m |
| 3 | Lõi Năng Lượng Thiên Thạch Nổ | +20% HP, +20% DEF | Mất 15% HP → nổ 100% DEF bán kính 1.5m, đẩy lùi quái thường |
| 4 | Lõi Ma Pháp Hội Tụ Quang Tử | +18% DMG, +15% PEN | Trúng mục tiêu xa hơn 7m → +25% DMG trong 1s |
| 5 | Động Cơ Xung Kích Tinh Thể Thạch Anh | +15% ATK, +25% PEN | Bắn liên tiếp 1 mục tiêu trong 2s: +5%/lần, tối đa +20% |
| 6 | Lõi Ma Pháp Hắc Nguyệt | +25% HP | Trường 2.5m: địch −20% SPD, −15% ATK |
| 7 | Động Cơ Tua-Bin Linh Hỏa | +25% SPD, +15% HP | Mỗi 3m để lại vùng lửa 1.2m: đồng minh +20% SPD, địch −20% SPD |
| 8 | Lõi Ma Trận Cộng Hưởng Mones | +80 EN, +15% CDR | Kỹ năng thường chia 50% năng lượng hồi cho đồng minh gần nhất trong 7m (+10% CDR cho họ) |
| 9 | Động Cơ Phân Pha Hư Không | +25% ATK, +15% SPD | Đi 3m → +30% PEN, +40% DMG; mất khi gây sát thương |
| 10 | Lõi Nhiệt Hạch Ba Lõi | +20% ATK, +10% DMG | Kỹ năng thường gây Thiêu đốt 3 cộng dồn (3s, 25% ATK/s mỗi cộng dồn); đủ 3 → +25% DMG 3s |
| 11 | Động Cơ Thủy Lực Ma Pháp Đầm Lầy | +30% DEF, +10% CDR | Mỗi lần nhận sát thương +3% giảm sát thương (tối đa 18%); 6 tầng → miễn làm chậm + 15% CDR trong 5s |
| 12 | Động Cơ Tua-Bin Xoáy Nước | +15% DMG, +15% SPD | Hạ gục địch trong 10m → +25 EN, +20% SPD 2s, +25% ATK (2 tầng) |
| 13 | Lõi Ma Pháp Bào Tử Độc | +50 EN, +15% CDR | Kỹ năng gây sát thương → khói độc 2.5m, địch −20% DEF |
| 14 | Lõi Tụ Điện Ma Pháp Sấm Sét | +20% ATK | Mỗi kỹ năng thường thứ 3 → sét chuỗi 4 mục tiêu, 65% ATK, −20% SPD 5s |
| 15 | Lõi Ma Trận Khiên Nhật Thực | +20% HP, +60 EN | Dùng kỹ năng tốn năng lượng → khiên 12% HP tối đa trong 6s |
| 16 | Lõi Động Cơ Tái Sinh Chữ Thập | +30% HP | Hồi 1.5% HP/s; dưới 40% HP → 3%/s và +25% DEF |
| 17 | Lõi Thủy Tinh Trị Liệu | +15% HP, +20% CDR | Hồi máu → khiên bằng 50% lượng hồi (5s), tối đa 30% HP |
| 18 | Lõi Liên Kết Song Tinh Nguyên Tố | +15% HP, +25% DEF | Nối cáp đồng minh thấp máu nhất, nhận 20% sát thương của họ (trực tiếp vào máu); không có đồng minh → +50% DEF |
| 19 | Lõi Động Cơ Lưỡng Cực THE2FACE | +15% ATK, +15% CDR | Gây sát thương → địch −10% DEF 4s; hồi máu/tạo khiên → đồng minh +15% ATK 4s (2 tầng) |
| 20 | Lõi Siêu Cơ Giáp Thống Lĩnh D1ONK | +50% ATK, +50% HP, +50% DEF, +15% DMG, +30% PEN | Nhận thưởng từ địch Tinh anh → +50% EN, +30 CDR, +30% DMG trong 10s |

Các phụ kiện số 2, 18 dùng đúng chặng **chuyển hướng sát thương** (§5.5.1): phần chuyển sang là `IsDirectToHealth = true`, `IsRedirected = true`.

### 10.7 Cảnh báo lệch giữa GDD và code (đã chốt)

| # | Vấn đề | Kết luận |
|---|---|---|
| 1 | Công thức Def kẻ địch trong Word thiếu `(1 - …)` | **Code đúng**; sửa lại dòng công thức trong Word (xem ghi chú §4.2) |
| 2 | Tốc độ: GDD thang 100, runtime đơn vị/giây | Cần chốt hệ số quy đổi trước khi nhập liệu |
| 3 | `DamageMultiplier` (×1.89) trong asset cũ vs `DamageRate` (+15%) | Quy đổi `DamageRate = Multiplier − 1`; copy thô sẽ sai 10× |
| 4 | §5.6 GDD ghi ngọc Ch1 10–30 / Ch2 35–80, lệch bảng ải | **Bảng ải là chuẩn** (20/30/40/60 · 50/60/70/90/120) |
| 5 | §5.6 ghi rơi phụ kiện 25–50%, bảng ải ghi 20% | **Bảng ải là chuẩn** (20%/món, đảm bảo 1 món) |
| 6 | Final boss ghi "ẢI 2-15" | Sai chính tả, phải là **2-5** |
| 7 | 6 asset `Assets/Prefabs/Tanks/*/*.asset` | Chưa migrate sang `baseStatsLevel1/90`, vẫn là số placeholder chung (10/100/50/10/3 → 180/1400/680/35.6/7.45), **khác hoàn toàn** §10.1 |
