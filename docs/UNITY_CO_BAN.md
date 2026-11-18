# Unity Cơ Bản Cho Dự Án Iron Wasteland

> Dành cho người **chưa từng dùng Unity**. Chỉ gồm những thao tác bạn thực sự cần cho dự án này —
> không lan man sang 3D, animation nâng cao hay shader.
>
> Đọc xong tài liệu này + làm mục 8 (bài tập 15 phút) là bạn tự chạy thử, tự chỉnh số và tự tìm lỗi được.

---

## Mục lục

1. [Trước khi mở Unity](#1-trước-khi-mở-unity)
2. [Năm khái niệm, hiểu trong 2 phút](#2-năm-khái-niệm-hiểu-trong-2-phút)
3. [Sáu cửa sổ và cách điều hướng](#3-sáu-cửa-sổ-và-cách-điều-hướng)
4. [Mười thao tác thực tế](#4-mười-thao-tác-thực-tế)
5. [Vòng lặp làm việc và debug](#5-vòng-lặp-làm-việc-và-debug)
6. [Sáu điều đặc thù của dự án này](#6-sáu-điều-đặc-thù-của-dự-án-này)
7. [Phím tắt đáng nhớ](#7-phím-tắt-đáng-nhớ)
8. [Bài tập 15 phút](#8-bài-tập-15-phút)
9. [Năm lỗi tân binh hay mắc](#9-năm-lỗi-tân-binh-hay-mắc)
10. [Khi bị kẹt thì đọc theo thứ tự này](#10-khi-bị-kẹt-thì-đọc-theo-thứ-tự-này)

---

## 1. Trước khi mở Unity

### 1.1 Đúng phiên bản

Dự án chạy trên **Unity 6000.5.6f1** (xem `ProjectSettings/ProjectVersion.txt`).

- Mở **Unity Hub** → **Add ▸ Add project from disk** → chọn `D:\GameUnity\IronWasteland`.
- Nếu Hub hỏi nâng cấp phiên bản → **chọn 6000.5.6f1**, đừng để nó tự nâng. Cả nhóm phải cùng phiên bản, nếu lệch sẽ sinh conflict hàng loạt trong `ProjectSettings/`.

### 1.2 Lần mở đầu tiên rất chậm

Unity sẽ import lại toàn bộ `Library/` — có thể **5–15 phút**. Cứ để yên, đừng ngắt. Những lần sau mở chỉ mất vài giây.

### 1.3 Lấy code mới nhất

Trước khi mở Unity, luôn chạy trong terminal tại thư mục dự án:

```powershell
git pull
```

Nếu đang mở Unity mà pull về nhiều file, Unity sẽ tự import lại. Đang chạy Play thì nên dừng trước.

### 1.4 Mở scene để làm việc

Trong cửa sổ **Project** (góc dưới trái): `Assets ▸ Scenes ▸ SampleScene` → **double-click**.

Đây là scene test chính của dự án. `MainScene` là bản cũ, ít dùng.

---

## 2. Năm khái niệm, hiểu trong 2 phút

| Khái niệm | Nghĩ như | Ví dụ trong dự án |
|---|---|---|
| **GameObject** | Một "cái hộp" vô nghĩa, chỉ có vị trí | `WallTitan`, `Main Camera`, `GameController` |
| **Component** | Bộ phận gắn vào hộp để nó *làm gì đó* | `Rigidbody2D` (vật lý), `TankBrain` (logic), `SpriteRenderer` (hình) |
| **Transform** | Component bắt buộc, giữ vị trí / xoay / scale | Mọi object đều có, không xoá được |
| **Prefab** | Khuôn mẫu: sửa khuôn → mọi bản sao đổi theo | `WallTitan.prefab`, `QuaiVatTinhThach.prefab` |
| **ScriptableObject** | File dữ liệu thuần, **không** nằm trong scene | `WallTitan.asset` (chỉ số), `EquipmentDatabase.asset` |

**Công thức cốt lõi:**

```
Một vật thể trong game  =  GameObject  +  nhiều Component
```

Không có chuyện "viết class kế thừa GameObject" — đây là điều người mới hay nhầm nhất khi chuyển từ lập trình thường sang Unity.

**Ba loại file bạn sẽ gặp trong Project:**

| Đuôi | Là gì | Sửa ở đâu |
|---|---|---|
| `.cs` | Code C# | Mở bằng IDE (double-click) |
| `.prefab` | Khuôn mẫu vật thể | Prefab Mode (double-click) |
| `.asset` | Dữ liệu (ScriptableObject) | Chọn → sửa trong Inspector |

---

## 3. Sáu cửa sổ và cách điều hướng

### 3.1 Bố cục mặc định

```
┌──────────────┬──────────────────────────┬──────────────┐
│  HIERARCHY   │         SCENE            │  INSPECTOR   │
│  (mọi object │  (nhìn, căn vị trí)      │  (thuộc tính │
│   trong scene)│                         │   của object)│
├──────────────┴──────────────────────────┴──────────────┤
│  PROJECT (file trên đĩa)  │  CONSOLE (log, lỗi)        │
└───────────────────────────────────────────────────────┘
```

| Cửa sổ | Dùng để | Mở bằng |
|---|---|---|
| **Hierarchy** | Chọn object trong scene đang mở | `Ctrl+Shift+H` |
| **Inspector** | Xem/sửa component, gán file | `Ctrl+3` |
| **Project** | Duyệt file, tạo asset, tìm prefab | `Ctrl+5` |
| **Console** | Đọc lỗi và log | `Ctrl+Shift+C` |
| **Game** | Xem kết quả khi bấm Play | `Ctrl+2` |
| **Profiler** | Xem hiệu năng (FPS, GC) | `Ctrl+7` |

Nếu bố cục bị rối: menu **Window ▸ Layouts ▸ Default** để về mặc định.

### 3.2 Điều hướng Scene view

| Thao tác | Kết quả |
|---|---|
| Chuột giữa + kéo | Di chuyển khung nhìn (pan) |
| Lăn chuột | Phóng to / thu nhỏ |
| Chuột phải + kéo | Xoay góc nhìn |
| `F` | Zoom thẳng vào object đang chọn |
| `W` / `E` / `R` | Công cụ Di chuyển / Xoay / Scale |
| Bấm nút **2D** trên thanh công cụ | Khoá góc nhìn phẳng — **nên bật** vì game này 2D |

> Nếu bấm `F` mà màn hình trắng trơn: bạn đang ở góc nhìn 3D nghiêng. Bật **2D** rồi bấm `F` lại.

---

## 4. Mười thao tác thực tế

### ① Chạy thử game

1. Mở `Assets/Scenes/SampleScene.unity`.
2. Chọn object `GameController` trong Hierarchy.
3. Xem ô **Tank Definition**: nếu trống, kéo file `Assets/Prefabs/Tanks/WallTitan/WallTitan.asset` từ Project vào.
4. Bấm **▶ Play** (hoặc `Ctrl+P`).

**Điều khiển có sẵn:**

| Phím | Việc |
|---|---|
| `WASD` hoặc phím mũi tên | Di chuyển |
| Chuột | Ngắm (nòng súng luôn quay về con trỏ) |
| `J` `K` `L` `I` | 4 kỹ năng (nhả phím mới kích hoạt) |

> ⚠️ Mọi thay đổi bạn làm **khi đang Play sẽ mất** khi bấm Stop. Đây là điều gây ức chế nhất cho người mới. Nhìn viền cửa sổ Editor đổi màu = đang Play.

### ② Chỉnh một con số

Chọn object → Inspector → sửa ô số → `Enter`. Chưa cần lưu nếu chỉ thử.

### ③ Tạo object mới

Hierarchy chuột phải vùng trống → **Create Empty** → `F2` đổi tên → bấm **Add Component** để gắn thứ cần.

### ④ Gán file vào ô trống (thao tác bạn sẽ làm nhiều nhất)

Mọi ô trống trong Inspector đều nhận **kéo–thả** từ cửa sổ Project:

- Ô `Tank Definition` ← kéo `WallTitan.asset`
- Ô `None (Transform)` ← kéo một object từ Hierarchy
- Ô `Bolt Design` ← kéo một prefab skill

Đây chính là cách dự án "nối dây": `TankDefinition` trỏ tới prefab tank → tank trỏ tới prefab skill → skill trỏ tới sprite.

### ⑤ Sửa prefab

- **Double-click** prefab trong Project → vào **Prefab Mode** (màn hình tối, chỉ thấy prefab đó).
- Sửa xong bấm mũi tên `←` góc trái trên để thoát. Thay đổi **tự động** áp cho mọi bản sao.
- Nếu bạn lỡ sửa một **bản sao trong scene**: Inspector hiện khối **Overrides**
  - **Apply All** = đẩy thay đổi vào prefab
  - **Revert** = bỏ thay đổi cục bộ, quay về như prefab

### ⑥ Tạo ScriptableObject (file dữ liệu)

Chuột phải trong Project → **Create** → tìm menu của dự án:

| Menu | Tạo ra |
|---|---|
| `Iron Wasteland ▸ Tank ▸ Tank Definition` | Chỉ số một xe tăng |
| `Iron Wasteland ▸ Stage ▸ Stage Definition` | Một màn chơi (wave quái, điều kiện thắng) |

Kết quả là file `.asset`. Đây là **dữ liệu**, không phải code — sửa trực tiếp trong Inspector.

### ⑦ Viết code

1. Project chuột phải → **Create ▸ C# Script** → đặt tên.
2. **Tên file phải trùng tên class** (`TankBrain.cs` chứa `class TankBrain`).
3. Double-click mở IDE → sửa → `Ctrl+S` → quay lại Unity, nó tự biên dịch.

Lỗi biên dịch hiện **đỏ** ở Console và góc dưới phải cửa sổ. **Còn lỗi đỏ thì không bấm Play được.**

### ⑧ Nhân bản / tắt / xoá

| Việc | Cách |
|---|---|
| Nhân bản | `Ctrl+D` |
| Tắt tạm (không xoá) | Bỏ tick ở ô cạnh tên component, hoặc untick object trong Hierarchy |
| Xoá | `Delete` |

Tắt object là cách tốt để thử "nếu không có thứ này thì sao" mà không mất công dựng lại.

### ⑨ Đọc Console

| Màu | Nghĩa |
|---|---|
| Trắng | `Debug.Log` — log bình thường |
| Vàng | Cảnh báo — game vẫn chạy |
| Đỏ | Lỗi — thường là `NullReferenceException` |

Bấm vào dòng lỗi → Unity chỉ ra object gây lỗi. Hai nút hữu ích ở góc trái: **Clear** (xoá log cũ) và **Collapse** (gộp dòng trùng).

Muốn lọc log của một hệ thống: gõ vào ô search, ví dụ `[TankBrain]` hoặc `[Stage]`.

### ⑩ Lưu

`Ctrl+S` lưu **scene đang mở**. **Unity không tự lưu** — thoát mà chưa lưu thì nó sẽ hỏi.

Ba thứ lưu riêng nhau, đừng nhầm:

| Đang sửa gì | `Ctrl+S` lưu cái đó |
|---|---|
| Scene | Scene |
| Prefab (trong Prefab Mode) | Prefab |
| ScriptableObject `.asset` | File `.asset` |

Sửa asset hay prefab thì Unity lưu ngay khi bạn đổi giá trị (có biểu tượng đĩa nhỏ ở góc dưới phải khi có thay đổi chưa ghi). Riêng **scene** thì phải `Ctrl+S` mới lưu.

---

## 5. Vòng lặp làm việc và debug

### 5.1 Vòng lặp chuẩn

```
Sửa trong Inspector / Prefab Mode
            ↓
        Ctrl+P chạy thử
            ↓
     sai → Ctrl+P dừng → sửa tiếp
```

### 5.2 Debug không cần viết code (hiệu quả nhất với người mới)

**Bấm Play rồi chọn object** — Inspector hiện số liệu **sống**, cập nhật realtime:

- Chọn tank → thấy `Current Health`, `Current Energy`, `Stats` (giá trị sau khi cộng buff)
- Chọn quái → thấy máu còn lại
- Chọn `StageManager` → thấy số quái đã hạ

Đây là cách nhanh nhất để biết "skill có ăn không", "buff có cộng không" mà không phải in log.

### 5.3 Debug bằng log

Thêm tạm một dòng vào chỗ cần kiểm tra:

```csharp
Debug.Log($"Máu còn: {currentHealth}");
```

Rồi xem Console. **Nhớ xoá trước khi commit** — log rác làm chậm game và rối Console.

### 5.4 Debug bằng breakpoint

Trong IDE (Rider/Visual Studio): bấm vào lề trái dòng code để đặt chấm đỏ → quay lại Unity bấm Play → khi chạy tới dòng đó, game **dừng lại**, bạn xem được mọi biến. Bấm continue (▷) để chạy tiếp.

Đây là công cụ mạnh nhất khi lỗi khó, nhưng hơi chậm — dùng khi log không đủ.

### 5.5 Ba nút Play

| Nút | Việc |
|---|---|
| **▶** | Chạy / dừng |
| **⏸** | Tạm dừng (game đứng yên, vẫn xem được Inspector) |
| **⏭** | Tiến từng frame khi đang tạm dừng |

Nút ⏭ rất hữu ích để bắt lỗi "đạn xuyên qua" — tiến từng frame sẽ thấy đúng frame va chạm.

### 5.6 Nhìn thấy vùng ảnh hưởng

Chọn object rồi xem trong Scene view: các script có vẽ Gizmo sẽ hiện vòng tròn bán kính, đường bắn… Giúp căn bán kính AoE cho khớp thiết kế mà không phải đoán.

---

## 6. Sáu điều đặc thù của dự án này

| # | Điều | Vì sao quan trọng |
|---|---|---|
| 1 | **Input dùng `Keyboard.current`, không phải `Input.GetKey`** | Project bật Input System mới; code kiểu cũ sẽ báo lỗi đỏ |
| 2 | **Mọi collider cần component `ColliderOwnerRef`** | Đây là "chứng minh thư" để biết đạn trúng ai. Thiếu → đạn xuyên qua quái mà không gây sát thương |
| 3 | **Không bao giờ xoá file `.meta`** | Mỗi file có `.meta` chứa GUID. Xoá là mọi tham chiếu đứt, Unity báo "Missing" |
| 4 | **Tên file phải trùng tên class** | `TankBrain.cs` chứa `class TankBrain` |
| 5 | **Logic ở `TankBrain_<Xe>.cs`, hình ở `<Xe>_View.prefab`** | Muốn đổi chỉ số thì sửa asset/prefab, đừng sửa code |
| 6 | **Dự án chưa có UI thật** | HUD hiện là panel vẽ tạm bằng `OnGUI` trong các script test |

### 6.1 Cấu trúc một xe tăng (để biết sửa ở đâu)

```
Assets/Prefabs/Tanks/WallTitan/
├── WallTitan.asset        ← CHỈ SỐ (Attack, HP, Speed, level 1 → 90)
├── WallTitan.prefab       ← BRAIN (logic, cooldown, năng lượng từng chiêu)
└── WallTitan_View.prefab  ← HÌNH (sprite, animator, vị trí nòng súng)
```

**Muốn cân bằng game** → sửa `.asset` (chỉ số gốc) hoặc `.prefab` (hệ số từng chiêu).
**Muốn đổi hình** → sửa `_View.prefab`.
**Muốn đổi hành vi** → sửa code.

### 6.2 Hai công cụ test có sẵn

| Script | Phím | Dùng để |
|---|---|---|
| `SkillArena` | `1` `2` `3` `4` đổi nhánh chiêu · `R` hồi bia · `C` xoá hồi chiêu · `H` hồi đầy HP/EN | Test skill trên bia, xem cooldown realtime |
| `AllyTestSpawner` | `F2` spawn đồng minh tại con trỏ · `F3` xoá | Test skill buff đồng minh |

Gắn component vào một GameObject bất kỳ trong scene là dùng được.

---

## 7. Phím tắt đáng nhớ

| Phím | Việc |
|---|---|
| `Ctrl+P` | Play / Stop |
| `Ctrl+Shift+P` | Pause |
| `Ctrl+S` | Lưu scene |
| `Ctrl+D` | Nhân bản object |
| `Ctrl+Z` | Undo (undo được cả thao tác trong Inspector) |
| `F` | Zoom tới object đang chọn |
| `Ctrl+Shift+C` | Mở Console |
| `Ctrl+Shift+F` | Đặt object trùng vị trí camera |
| `F2` | Đổi tên |
| `Delete` | Xoá |

---

## 8. Bài tập 15 phút

Làm đúng thứ tự, mỗi bước đều an toàn (không phá gì, cuối bài có hướng dẫn trả lại).

**Bước 1 — Chạy thử (3 phút)**
Mở `SampleScene` → Play → di chuyển bằng `WASD`, ngắm bằng chuột, bấm `J K L I`. Dừng.

**Bước 2 — Đổi level (2 phút)**
Chọn `GameController` → kéo thanh `Player Level` từ `1` lên `60` → Play.
→ Máu và sát thương tăng vọt. Đây là cách level ảnh hưởng chỉ số (nội suy từ level 1 đến 90).
Dừng, kéo lại `1`.

**Bước 3 — Đổi chỉ số gốc (3 phút)**
Mở `Assets/Prefabs/Tanks/WallTitan/WallTitan.asset` → trong Inspector, mục `Base Stats Level 1` → đổi `Max Health` từ `1200` thành `5000` → Play.
→ Tank trâu hơn hẳn. Dừng, đổi lại `1200`.

**Bước 4 — Xem nơi cân bằng chiêu (3 phút)**
Double-click `WallTitan.prefab` → chọn object có component `Tank Brain Wall Titan` → xem các ô `Cooldown`, `Energy Cost`, `Damage Multiplier`.
→ Đây chính là nơi bạn sẽ chỉnh khi làm skill. Thoát bằng mũi tên `←`.

**Bước 5 — Đọc log hệ thống (2 phút)**
Console → gõ `[TankBrain]` vào ô lọc → Play → bắn chiêu → thấy log từng lần dùng skill kèm level.

**Bước 6 — Tìm hiểu một prefab quái (2 phút)**
Project → `Assets/Prefabs/Enemies/Normal/` → chọn một prefab → xem cấu trúc: object gốc có `EnemyBrain` + `Rigidbody2D`, object con `Model` có `SpriteRenderer` + `Animator`.

> Nếu có gì đó trông khác mô tả (ô trống, object không có, Console báo đỏ) — đó là bình thường, dự án đang phát triển. Ghi lại và hỏi, đừng tự sửa lung tung.

---

## 9. Năm lỗi tân binh hay mắc

| # | Lỗi | Hậu quả | Cách tránh |
|---|---|---|---|
| 1 | Sửa lúc đang Play | Mất sạch khi Stop | Nhìn viền Editor đổi màu = đang Play |
| 2 | Kéo object từ Hierarchy vào ô Inspector rồi xoá object đó | Ô thành `Missing` | Ưu tiên kéo **prefab/asset** từ Project, không kéo object trong scene |
| 3 | Xoá `.meta` hoặc đổi tên file mà không đổi tên class | Unity báo "script missing" | Đổi tên trong Unity (F2), để Unity tự đổi `.meta` |
| 4 | Quên gán ô `None (...)` | `NullReferenceException` khi Play | Trước khi Play, lướt Inspector tìm ô `None` |
| 5 | Sửa bản sao trong scene nhưng tưởng đang sửa prefab | Sửa không áp dụng cho chỗ khác | Kiểm tra khối **Overrides** trong Inspector |

**Thêm một lỗi hay gặp với dự án này:** bấm Play mà không có tank nào xuất hiện → `GameController` chưa gán `Tank Definition`, hoặc asset chưa có `tankPrefab`. Console sẽ báo `Khong tao duoc Tank`.

---

## 10. Khi bị kẹt thì đọc theo thứ tự này

1. **Console** — lỗi đỏ nói gì? Object nào?
2. **Inspector** — có ô nào đang `None` hoặc `Missing` không?
3. **`PROJECT_CONTEXT.md`** — hệ thống này được thiết kế thế nào, công thức ra sao?
4. **File mẫu gần nhất** — copy từ thứ đang chạy được, đừng viết từ đầu.
5. **`git log --oneline -- <đường-dẫn-file>`** — ai sửa file này gần nhất, sửa gì.

Và nguyên tắc số một: **thay đổi một thứ, chạy thử ngay.** Sửa 5 chỗ rồi mới Play thì không biết chỗ nào gây lỗi.

---

*Tài liệu dành cho người mới, viết ngày 14/11/2026, dựa trên codebase `main` @ `ea49431` (Unity 6000.5.6f1).*
