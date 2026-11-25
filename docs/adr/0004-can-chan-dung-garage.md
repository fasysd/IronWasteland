# 0004 — Căn chân dung tank vào tâm vòng tròn Garage

Ngày: 2026-11-24
Trạng thái: Chấp nhận

## Bối cảnh

Khu trưng bày Garage có ảnh chân dung từng tank (sprite vuông 1024²) nằm trong khung tròn vẽ bởi `center_unit.png`. Ảnh hiển thị lệch trái so với vòng tròn.

Nguyên nhân thật: `Image.preserveAspect = true` không căn sprite vào giữa khung mà căn vào góc trái/trên của khung khi RectTransform có pivot (0,0). Vì vậy chân dung 330×330 luôn bám mép trái khung 500×330, lệch khoảng 85 px thiết kế, không phải do pivot của sprite.

Ngoài ra, ngay khi đã đúng khung, khối tâm thị giác (alpha weighted centroid) của từng ảnh vẫn lệch: ảnh có nòng pháo dài bên phải thì khối tâm nằm trái tâm hình học, nên tank trông "hơi lệch" dù hình học đã cân.

## Quyết định

1. Khung chân dung là hình vuông đúng 330×330 đặt tại (703, 240.5) để tâm khung trùng tâm vòng tròn đo được ở (868, 405.5).
2. `TankDefinition` có thêm `[SerializeField] Vector2 portraitOffset` (pixel thiết kế, dương = phải/lên) để từng tank tự bù độ lệch khối tâm; `GarageController` cộng offset vào vị trí gốc chụp trong `Awake`.
3. Giá trị offset ban đầu lấy từ đo khối tâm alpha của ảnh 1024², quy về pixel thiết kế: `design_shift = (512 - centroid_x) * 330 / 1024`. Wall Titan +6.1, Red Nova −1.9, Black Shadow +22.9, Leviathan −2.8, Water B +0.1, Green Fox +14.6.

## Hệ quả

- Muốn chỉnh căn cho một tank chỉ cần sửa `portraitOffset` trên asset, không phải sửa builder hay controller.
- Nếu bộ ảnh chân dung được vẽ lại, cần đo lại centroid và cập nhật `TankSeed.PortraitOffset` trong `Assets/Editor/GarageBuilder.cs` (seed lại asset khi Rebuild).
- Đừng đổi khung chân dung về tỉ lệ khác 1:1 nếu còn `preserveAspect`.
