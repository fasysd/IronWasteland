# 0012 — Đồng bộ visual kỹ năng quái bằng ghost theo catalog

- Ngày: 2026-11-27
- Trạng thái: Đã chấp nhận
- Liên quan: m07682; phần animation và cơ chế cho phép script VFX thuần chạy trên ghost được bổ sung ở `docs/adr/0013-dong-bo-animation-quai.md`.

> Ghi chú: mục 4 (Sanitize tắt toàn bộ MonoBehaviour) và mục 9 (child Skill MatTroiDo) đã được cập nhật bởi ADR 0013. Các mục còn lại vẫn hiệu lực.

## Bối cảnh

Trong chơi mạng, client tắt mô phỏng AI của quái (`EnemyBrain.IsServerSimulated = false`) để host giữ quyền
quyết định sát thương và vị trí. Các kỹ năng quái thường được gắn sẵn dưới dạng child `Skill` đang tắt trong
prefab; chỉ brain trên host bật child đó lên khi cast. Vì vậy client không bao giờ thấy child được bật, dù
con quái vẫn được đồng bộ bằng `EnemyNetState`/`NetworkTransform`.

Bốn prefab quái thường có child `Skill` đang tắt: `KIenLuaKhongLo`, `KySinhThienThach`, `MatTrangDen`,
`MatTroiDo`. Ba prefab đầu dùng child này làm visual kỹ năng thật. `MatTroiDo` không dùng child `Skill`:
brain của nó tự spawn `ExplosionZone_MatTroiDo` và triệu hồi Ký Sinh Thiên Thạch; child `Skill` trong prefab
là asset thừa, không có call site.

Ngoài ra 5 boss dùng các VFX standalone (`VFX_*`) do controller `Instantiate` trực tiếp. Các VFX này không
có `NetworkObject` nên kênh ghost cũ (`IW.Visual`) không gửi được; client mất toàn bộ hiệu ứng kỹ năng boss.

Yêu cầu: client thấy đúng visual kỹ năng quái và boss, nhưng không mô phỏng lại gameplay, không gây sát thương
lần hai, không cần thêm `NetworkObject` cho từng VFX.

## Quyết định

1. **Host giữ quyền gameplay, client chỉ dựng ghost visual-only.** Không chạy lại AI, skill logic hoặc damage
   trên client. Mọi bản sao đều đi qua `NetVisualGhost.Sanitize()`.
2. **Thêm named message `IW.SkillVisual`** trong `NetVisualSpawn`. Gói tin gồm: handle thu hồi,
   `NetworkObjectId` của quái gốc, Id catalog (VFX standalone) hoặc tên child (skill gắn prefab), offset/góc
   local so với root, thời gian sống, cờ attach và cờ animate.
3. **Hai đường gửi:**
   - `PlaySkillVisual(template, lifetime, animate)` cho child `Skill` nằm trong prefab quái. Host tính offset
     local từ root rồi gửi; client parent bản sao vào root để aura/cone bám quái và tự chết khi quái despawn.
   - `PlayVfx(prefab, instance, root, lifetime)` cho VFX standalone của boss. Host gửi Id trong catalog; client
     tra catalog rồi đặt bản sao tại vị trí cast, không parent.
4. **Marker `NetVisualGhost`** đánh dấu prefab được phép làm ghost. Sanitize tắt toàn bộ `Collider2D` và
   `MonoBehaviour` (trừ marker), giữ `Animator`, `ParticleSystem`, `SpriteRenderer` để visual vẫn chạy.
5. **Catalog dùng chung `NetSkillVisualCatalog`** ở `Assets/Resources/NetSkillVisualCatalog.asset`: Id int
   ổn định (FNV-1a từ đường dẫn prefab, tăng khi trùng) ánh xạ sang prefab VFX. Host chỉ gửi Id; VFX không cần
   `NetworkObject`. Công cụ `Assets/Editor/NetSkillVisualSetup.cs` quét ba thư mục VFX boss, gắn marker và
   dựng lại catalog.
6. **Móc gọi đặt tại nơi cast thật của host:** `Skill_KIenLuaKhongLo.Cast`, `Skill_MatTrangDen.CastSkill`,
   `Skill_KySinhThienThach.Cast` gọi `PlaySkillVisual` sau `SetActive(true)`; nhánh huỷ và cuối routine gọi
   `NetVisualSpawn.End`. Ba controller VFX boss thay mọi `Instantiate` bằng helper `SpawnVfx` gọi `PlayVfx`.
7. **Hạ tầng boss:** thêm `EnemyNetState` + `NetworkTransform` cho 5 prefab boss (`BongMaTuDia`,
   `SEAUBEARUS`, `TruongQuanNgucD1ONK `, `TuThanNguyenTo`, `The2Face`). Các prefab đã có `NetworkObject` và
   đã nằm trong `DefaultNetworkPrefabs`; không sửa `StageDefinition`.
8. **Boss probe dev-only:** `BossProbe`, `BossProbeCatalog`, cờ `-iw-boss`/`-iw-spawn` và
   `BossProbe.TrySpawn` nằm trong `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; chỉ dùng để kiểm chứng, không vào
   bản release. Catalog chứa 5 boss + 12 quái thường để test từng quái; `-iw-spawn <tên-prefab>` chọn theo
   tên, `-iw-boss <0..4>` giữ cho 5 boss. Khi có probe, `StageDirector.SuppressEnemyWaves` chặn đợt quái
   gốc để sân chỉ còn quái test; thêm `-iw-keep-waves` nếu muốn giữ đợt quái của ải. Trong trận, host bấm
   **F8** để spawn lại đúng quái đang chọn, **F9** để chuyển sang quái kế tiếp trong catalog và spawn luôn,
   không cần thoát phòng.
9. **`MatTroiDo`:** giữ nguyên gameplay. Child `Skill` thừa không được nối vào brain; kỹ năng thật của nó là
   vùng nổ + Ký Sinh Thiên Thạch. Trước đây KSTT được sinh từ template con không có `NetworkObject`, nên
   host báo lỗi `Prefab quái 'KySinhThienThach_Template' chưa có NetworkObject` và client không thấy quái con.
   Đã thêm field `kySinhThienThachNetworkPrefab` trỏ `Assets/Prefabs/Enemies/Normal/KySinhThienThach.prefab`
   (có `NetworkObject`) và dùng prefab này khi chơi mạng; chế độ offline vẫn dùng template con. Ghi chú để
   lần sau không nhầm child `Skill` là kỹ năng đang hoạt động.

## Hệ quả

- Client thấy được aura, vùng nổ, đòn đánh và VFX boss mà không cần quyền mô phỏng; sát thương và số mạng vẫn
  do host quyết định.
- Ghost có độ trễ một chiều mạng và không dự đoán; chấp nhận được vì đây là visual, không phải hitbox.
- Thêm VFX boss mới bắt buộc chạy lại **Iron Wasteland/Net/Rebuild Skill Visual Catalog**; nếu thiếu Id, host
  ghi cảnh báo và bỏ qua VFX chứ không chặn trận.
- Bản sao gắn root tự mất khi quái despawn; bản standalone có thời gian sống tối đa và bị giới hạn
  `maxGhosts = 120` để không phình bộ nhớ.
- Kiểm chứng trên bản Development Build, host + 1 client: `KIenLuaKhongLo`, `KySinhThienThach`,
  `MatTrangDen` và VFX `BongMaTuDia` (`VFX_DeadlandSlash` id 642896584, `VFX_DeadlandSoulProjectile`
  id 728456789, `SoulSummonPulse` id 705435871) đều có log ghost ở cả hai phía, không có exception.
  `MatTroiDo` chỉ xuất hiện ở đợt 2-3 sau 80 giây; child `Skill` của nó không phải đường cast thật.
