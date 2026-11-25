# Garage Screen — Spec dựng lại UI bằng code

Nguồn đo: D:\tk\garage\garage.jpg (2560×1440) + D:\tk\garage\New Text Document.svg (Figma export 1920×1080, 330 path + 13 image, toàn bộ chữ đã outline thành path nên không có thẻ text và không có font-size).
Hệ quy chiếu: 1920×1080, gốc (0,0) góc trên-trái, +x sang phải, +y xuống, đơn vị px. Mọi số đã quy về hệ này (jpg chia 1.33333) và làm tròn 0.5 px.

## 0. Phương pháp đo & độ tin cậy

- Hình học (x/y/w/h, bán kính, độ vát góc, độ dày nét): đọc trực tiếp từ path trong SVG — số gốc của Figma, chính xác tuyệt đối.
- Màu: lấy mã fill/stroke trong SVG. Đã đối chiếu mẫu pixel trên jpg; JPEG làm lệch ±2-3 mỗi kênh nên spec dùng mã SVG.
- Cỡ chữ: chữ đã outline nên không có font-size. cap = chiều cao glyph in hoa đo trên jpg bằng histogram đỉnh cột mực (±0.5 px). Cỡ chữ ước lượng = cap chia 0.73 (giả định cap-height = 0.73 em).
- Panel vát 45 độ ở hai góc bên phải (trên-phải + dưới-phải), không bo tròn; trừ nút settings vát cả 4 góc.
- Kiểm chứng hệ trục: mép phải nút CTA trong SVG 1848.0 vs trên jpg 1846.5, sai 0.08%, không có offset.

## 1. Bảng panel & khối (toạ độ góc trên-trái)

| # | Khối | x | y | w | h | Vát phải | Nền | Viền |
|---|------|---|---|---|---|----------|-----|------|
| 1 | Thanh header trên (dải gradient) | 0 | 0 | 1920 | 210 | – | đen 85% đến 0% (dọc) | – |
| 2 | Nút QUAY LẠI | 72 | 100 | 158 | 48 | 14 | #121821 | #26313D, 1 px |
| 3 | Ô tiền tệ | 1596 | 90 | 188 | 62 | 16 | #121821 | #26313D, 1 px |
| 4 | Nút vuông settings | 1804 | 100 | 44 | 44 | 12 (vát cả 4 góc) | #121821 | #26313D, 1 px |
| 5 | Vùng panel danh sách xe (không nền) | 72 | 180.5 | 372 | 771.5 | – | không nền | – |
| 6 | Hàng xe (6 hàng) | 72 | 244 + 120 i | 372 | 108 | 18 | #0E141C | #26313D, 1 px |
| 7 | Hàng ĐANG CHỌN | 71 | 483 | 374 | 110 | 18 | #1A2230 đến #0E141C (dọc) | #8A4BF0, 2 px |
| 8 | Khung hero giữa | 468 | 190 | 800 | 450 | 56 | #121821 đến #080C11 (dọc) | #26313D + #8A4BF0 35% |
| 9 | Panel chỉ số phải | 1292 | 190 | 556 | 464 | 20 | #121821 đến #0A0E13 (dọc) | #26313D, 1 px |
| 10 | Panel phụ kiện | 1292 | 670 | 556 | 206 | 18 | #0E141C | #26313D, 1 px |
| 11 | Thanh cấp độ (dưới hero) | 468 | 656 | 800 | 72 | 16 | #0E141C | #26313D, 1 px |
| 12 | Thẻ kỹ năng (4 thẻ) | 468 / 671 / 874 / 1077 | 782 | 191 | 206 | 16 | #0E141C | #26313D, 1 px |
| 13 | Nút NÂNG CẤP | 1028 | 672 | 226 | 52 | 12 | #FF7A18 đến #E85F00 (ngang) | #FFB347, 1.4 px |
| 14 | Nút ĐỔI PHỤ KIỆN | 1310 | 812 | 250 | 42 | 12 | #121821 | #3A4A5C, 1 px |
| 15 | Nút PHÂN RÃ | 1580 | 812 | 250 | 42 | 12 | #121821 | #3A4A5C, 1 px |
| 16 | Nút CTA (XÁC NHẬN RA TRẬN) | 1292 | 890 | 556 | 86 | 26 | #FF7A18 đến #E85F00 + radial trắng 14% | #FFB347, 1.6 px |
| 17 | Ô CẤP ĐỘ (trong hero) | 1138 | 206 | 106 | 58 | 12 | #0A0F16 | #26313D, 1 px |
| 18 | Khung icon hàng xe | 86 | hàng.y + 14 | 80 | 80 | 0 | #0A0F16 | #26313D, 1 px |
| 19 | Khung phím kỹ năng | 617 / 820 / 1023 / 1226 | 792 | 28 | 28 | 8 | #121821 | #8A4BF0, 1 px |
| 20 | Ngoặc góc HUD (4 góc) | 28,28 / 1828,28 / 28,988 / 1828,988 | – | 64 | 64 | – | – | #FF7A18, 2 px (2 cạnh chữ L) |

Hàng 5 là vùng suy ra (không có path nền), chỉ dùng để biết biên trái/phải cho layout: biên trên 180.5 = đỉnh vạch accent header, biên dưới 952 = đáy hàng cuối.

### 1.1 Path gốc các khối vát góc (dựng mask / 9-slice)

| Khối | Path | Vát |
|---|---|---|
| Hero | M468 190 H1212 L1268 246 V584 L1212 640 H468 Z | 56 |
| Panel chỉ số | M1292 190 H1828 L1848 210 V634 L1828 654 H1292 Z | 20 |
| Panel phụ kiện | M1292 670 H1830 L1848 688 V858 L1830 876 H1292 Z | 18 |
| Hàng xe | M72 244 H426 L444 262 V334 L426 352 H72 Z | 18 |
| Hàng chọn | M71 483 H427 L445 501 V575 L427 593 H71 Z | 18 |
| Thẻ kỹ năng | M468 782 H643 L659 798 V972 L643 988 H468 Z | 16 |
| Thanh cấp độ | M468 656 H1252 L1268 672 V712 L1252 728 H468 Z | 16 |
| CTA | M1292 890 H1822 L1848 916 V950 L1822 976 H1292 Z | 26 |
| Nút phụ | M1310 812 H1548 L1560 824 V842 L1548 854 H1310 Z | 12 |
| Nút QUAY LẠI | M72 100 H216 L230 114 V134 L216 148 H72 Z | 14 |
| Ô CẤP ĐỘ | M1138 206 H1232 L1244 218 V252 L1232 264 H1138 Z | 12 |
| Khung phím | M617 792 H637 L645 800 V812 L637 820 H617 Z | 8 |
| Nút NÂNG CẤP | M1028 672 H1242 L1254 684 V712 L1242 724 H1028 Z | 12 |
| Ô tiền tệ | M1596 90 H1768 L1784 106 V136 L1768 152 H1596 Z | 16 |

### 1.2 Lớp nền & trang trí

- Nền màn: gradient dọc #0B0F14 (y=0) đến #05070A (y=1080).
- 3 quầng radial: tím #8A4BF0 18% tâm (1200,200) bán kính 900; cam #FF7A18 13% tâm (100,1180) bán kính 800; teal #2FE8E0 10% tâm (1650,810) bán kính 750.
- Lưới: 23 đường dọc 1 px #3A4A5C opacity 0.07 cách 86 px (x = 40 den 1846); 30 đường ngang 1 px trắng opacity 0.012 cách 36 px.
- Dải tối trên 0..210 (đen 85% đến 0) và dải tối dưới 860..1080 (0 đến 88%).
- Dải 3 px trên cùng (y 0..3): gradient ngang #FF7A18 90% den #2FE8E0 70% tại 45% den #8A4BF0 55%.
- Kẻ chân trang: 72,1014, 1776 x 1, gradient #26313D 90% den #3A4A5C 50% den #26313D 90%.
- Gạch chân tiêu đề: 248,166, 640 x 1, gradient #FF7A18 85% den 0%.
- 6 đoạn cam 16.3 x 23.3 ở y 1044, x 1811 den 1881, opacity 0.3.

## 2. Panel danh sách xe (trái)

### 2.1 Header

| Thành phần | x | y | w | h | cap | font ≈ | Màu |
|---|---|---|---|---|---|---|---|
| Vạch accent dọc | 72 | 194 | 3 | 22 | – | – | #FF7A18 (op 0.9) |
| DANH SÁCH XE TĂNG | 88 | 186.5 | 159.5 | 20 | 15 | 20.5 | #E6EDF5 |
| 06 UNITS | 88.5 | 213.5 | 59.5 | 10 | 9.5 | 13 | #5A6675 |
| 06 | 421 | 195.5 | 22.5 | 21 | 20.5 | 28 | #3D4A59 |

- So 06 right-aligned, mép phải 443.5 (panel kết thúc 444, cách 0.5 px).
- Không có đường kẻ dưới header. Header đáy khoảng 224, hàng 1 đỉnh 244 (khe 20).

### 2.2 Nhịp hàng

- Cao hàng 108, khe 12, **pitch 120**. y: 244, 364, 484, 604, 724, 844; đáy hàng cuối 952.
- Hàng đang chọn (index 2) vẽ ở 71,483,374 x 110, nới 1 px mỗi phía so với hàng thường (72, x, 372 x 108) để nét viền 2 px nằm ngoài card.
- Vì vậy hàng chọn chồng 1 px vào khe 12 px, còn 10 px so với hàng trên và hàng dưới. Đây là chủ ý, không phải lỗi.

### 2.3 Bố cục trong một hàng (offset so với hàng.y)

| Thành phần | x | y offset | w | h | Ghi chú |
|---|---|---|---|---|---|
| Khung icon | 86 | +14 | 80 | 80 | nền #0A0F16, viền #26313D 1 px |
| Tên xe | 180.5 | +18 | theo chữ | cap 20.2 | #E6EDF5, font ≈ 27.5 |
| Dòng role | 180.5 | +41.5 | theo chữ | cap 9.7 | font ≈ 13, màu theo hệ |
| Track tiến trình | 180 | +68 | 180 | 5 | #1A2230, không bo |
| Fill tiến trình | 180 | +68 | 6 / 20 / 10 | 5 | màu theo hệ |
| CẤP xx / 90 | 180.5 | +76.5 | theo chữ | cap 10.1 | #8A99AB, font ≈ 14 |
| Tag ĐANG CHỌN | 372 | +81 | 57.5 | 13.5 | #FF7A18, font ≈ 11.5, mép phải 429.5 (chỉ hàng chọn, không có nền chip) |
| Tag ẢI 2-5 | 398 | +77 | 31.5 | 14.5 | #FFB347, font ≈ 13.5 (chỉ hàng khoá) |

### 2.4 Sáu hàng dữ liệu

| # | Tên | Role | Màu role | CẤP | Fill thanh | Icon | Trạng thái |
|---|-----|------|----------|-----|-----------|------|-----------|
| 1 | WALL TITAN | TANKER / HỖ TRỢ | #E0A94B | 01 / 90 | 6 px | 80 x 80 @86,258 | thường |
| 2 | RED NOVA | DPS / HỎA LỰC XA | #FF5A3C | 10 / 90 | 20 px | 93.75 x 80 @80,378 | thường |
| 3 | BLACK SHADOW | DPS / SÁT THỦ | #8A4BF0 | 05 / 90 | 10 px | 80 x 80 @86,498 | ĐANG CHỌN |
| 4 | LEVIATHAN | TANKER / TANKER | #2FE8E0 | 01 / 90 | 6 px | 80 x 80 @86,618 | thường |
| 5 | WATER B | HỖ TRỢ / DPS | #7FB3FF | 01 / 90 | 6 px | 80 x 80 @86,738 | thường |
| 6 | GREEN FOX | DPS / DPS | #3D4A59 | 01 / 90 | 6 px | 80 x 80 @86,858 + overlay #05070A 72 + o khoa | khoá, tag ẢI 2-5 |

- Hàng khoá: tên #5A6675, role + CẤP #3D4A59, fill #3D4A59; icon ổ khoá nét #8A99AB 2 px ở 117..135 x 885..911.
- Fill thanh tiến trình dùng đúng màu role của hàng đó; độ rộng fill 6 / 20 / 10 / 6 / 6 / 6 px trên track 180 px.

### 2.5 Hàng đang chọn

- Rect 71,483,374 x 110, vát 18, nền gradient dọc #1A2230 den #0E141C, viền #8A4BF0 2 px.
- Vạch nhấn trái: 71,483, **4 x 110**, #8A4BF0 (đè lên viền trái).
- Halo tím: ellipse tâm (67,538) bán kính 35 x 48, #8A4BF0 22% den 0, lan ra ngoài panel.
- Tên hàng chọn: 181.5, 502, 164, 21.5 (x, y, w, h), cap 20, font ≈ 27.5.
- Tag ĐANG CHỌN: 372, 564.5, 57.5, 13.5, cap 8.5, font ≈ 11.5.

## 3. Panel chỉ số chiến xa (phải)

Panel 1292,190,556 x 464. Padding trong: trái 34.4 (1292 đến 1326.4), phải 16.5 (1848 đến 1831.5), trên 6.1.

### 3.1 Header

| Thành phần | x | y | w | h | cap | font ≈ | Màu / ghi chú |
|---|---|---|---|---|---|---|---|
| Vạch accent dọc | 1292 | 190 | 3 | 34 | – | – | #2FE8E0 (op 0.9), đè lên mép trái panel |
| CHỈ SỐ CHIẾN XA | 1310.5 | 196 | 113.5 | 19.5 | 13 | 18 | #E6EDF5 |
| 08 STATS | 1769 | 201.5 | 59.5 | 10 | 10.5 | 14.5 | #3D4A59 |

- 08 STATS right-aligned, mép phải 1828.5 (panel kết thúc 1848, cách khoảng 19.5 px).
- Không có đường kẻ dưới header; dòng đầu bắt đầu ở y 252.8 (cách header 42 px).

### 3.2 Cấu trúc chung một dòng

- **Pitch 49 px**. Nhãn dòng i (i = 0..7) đỉnh y = 252.8 + 49 i.
- Chấm màu **7 x 7** tại x = 1308.5, đỉnh y = 258.5 + 49 i (tâm 262 + 49 i).
- Nhãn tiếng Việt x = 1326.5, cap 11, font ≈ 15, #C4D0DC, letter-spacing khoảng 0.4.
- Track: x = 1326, y = 282 + 49 i, **488 x 4**, #1A2230. Không có vạch chia dọc bên trong.
- Số bên phải: mép phải 1795.5, #E6EDF5, cap 19.5, font ≈ 26.5.
- Đơn vị: mép phải 1831.8, #5A6675, cap 10, font ≈ 13.5.
- Baseline nhãn 268.5 đến đỉnh track 282, khe 13.5 px.

### 3.3 Tám dòng

| # | Chấm | Nhãn | x nhãn | y nhãn | y track | Fill w | Màu fill | Số | Đơn vị |
|---|------|------|--------|--------|---------|--------|----------|----|--------|
| 1 | #FF7A18 | TẦN CÔNG | 1326.5 | 253 | 282 | 102.5 | #FF7A18 | 20 | ATK |
| 2 | #FF3B4E | MÁU TỐI ĐA | 1326.5 | 302 | 331 | 45.5 | #FF3B4E | 650 | HP |
| 3 | #7FB3FF | GIỚI HẠN NĂNG LƯỢNG | 1326.5 | 352 | 380 | 167.5 | #7FB3FF | 120 | EN |
| 4 | #2FE8E0 | PHÒNG THỦ | 1326.5 | 400.5 | 429 | 97.5 | #2FE8E0 | 12 | DEF |
| 5 | #4ADE80 | TỐC ĐỘ DI CHUYỂN | 1326.5 | 447.5 | 478 | 413 | #4ADE80 | 110 | SPD |
| 6 | #C9A9FF | TỈ LỆ GIẢM HỒI CHIÊU | 1326.5 | 497 | 527 | 0 | – | 0 | % |
| 7 | #FFB347 | TỈ LỆ SÁT THƯƠNG | 1326.5 | 547.5 | 576 | 0 | – | 0 | % |
| 8 | #8A4BF0 | TỈ LỆ XUYÊN GIÁP | 1326.5 | 596.5 | 625 | 0 | – | 0 | % |

- Fill = (số / max) x 488. Ba dòng cuối (giảm hồi, sát thương, xuyên giáp) đang 0 nên **track trống hoàn toàn**, chỉ còn rãnh #1A2230 và số 0.
- Số và đơn vị canh mép phải độc lập (không canh tâm) vì độ rộng đơn vị khác nhau (ATK/HP/EN/DEF/SPD so với %).
- Không in mốc min/max; thang mỗi dòng riêng. TẦN CÔNG 20 ứng với 102.5 px, suy ra max hiển thị khoảng 95-96 đơn vị cho 488 px.

## 4. Panel kỹ năng xe tăng (giữa dưới)

Không có rect bao ngoài: header + 4 thẻ rời.

### 4.1 Header

| Thành phần | x | y | w | h | cap | font ≈ | Màu / ghi chú |
|---|---|---|---|---|---|---|---|
| Vạch accent dọc | 468 | 746 | 3 | 20 | – | – | #8A4BF0 (op 0.9) |
| KỸ NĂNG XE TĂNG | 484 | 738.5 | 140.5 | 20.5 | 15 | 20.5 | #E6EDF5 |
| TANK SKILLS / 04 SLOTS | 484 | 764 | 169 | 13.5 | 9.5 | 13 | #5A6675 |

### 4.2 Bốn thẻ (pitch 203)

| # | Rect (x, y, w, h) | Vát | Nền | Viền | Accent 3 px trên | Số | Phím | Icon |
|---|------------------|-----|-----|------|------------------|----|------|------|
| 1 | 468, 782, 191 x 206 | 16 | #0E141C | #26313D | #FF7A18 | 01 | J | 96 x 96 @515,832 |
| 2 | 671, 782, 191 x 206 | 16 | #0E141C | #26313D | #2FE8E0 | 02 | K | 96 x 96 @718,832 |
| 3 | 874, 782, 191 x 206 | 16 | #0E141C | #26313D | #8A4BF0 | 03 | L | 96 x 96 @921,832 |
| 4 | 1077, 782, 191 x 206 | 16 | #0E141C | #26313D | #8A4BF0 | 04 | I | 96 x 96 @1124,832 |

Ô phím 28 x 28 tại (617,792) / (820,792) / (1023,792) / (1226,792); tâm lần lượt (631, 806) / (834, 806) / (1037, 806) / (1240, 806).

### 4.3 Bố cục trong thẻ

| Thành phần | x | y | w | h | cap | font ≈ | Màu / ghi chú |
|---|---|---|---|---|---|---|---|
| Accent trên (thẻ 1) | 468 | 782 | 191 | 3 | – | – | #FF7A18 (màu theo thẻ) |
| Số thứ tự 01 | 483.5 | 793.5 | 13 | 10 | 10.5 | 14 | #3D4A59, offset +15.3 so với thẻ.x |
| Khung phím (thẻ 1) | 617 | 792 | 28 | 28 | – | – | #121821, viền #8A4BF0, vát 8 |
| Ký tự phím J | 626.5 | 798.5 | 8 | 14 | 9.5 | 13 | #C9A9FF (cap không tin cậy, xem rủi ro) |
| Nhãn loại thẻ 1 | 482.5 | 811.5 | 88.5 | 13 | 9.5 | 13.5 | #5A6675 |
| Icon (thẻ 1) | 515 | 832 | 96 | 96 | – | – | glow radial 92 x 92 cùng màu op 26%, lệch +2/+2 |
| Tên kỹ năng thẻ 1 | 511 | 938.5 | 105.5 | 16 | 10.5 | 14.5 | #E6EDF5, căn giữa thẻ (tâm thẻ 563.5) |
| Dòng mô tả thẻ 1 | 500.5 | 964.5 | 126 | 17.5 | 9.5 | 13 | #8A99AB, letter-spacing rộng, căn giữa |

Nội dung 4 thẻ:

| # | Loại | Phím | Tên | Mô tả |
|---|------|------|-----|-------|
| 1 | KỸ NĂNG THƯỜNG | J | Shadow Claw | KHÔNG TỐN NĂNG LƯỢNG |
| 2 | KỸ NĂNG CHUYÊN BIỆT | K | Shadow Step | TỐN NĂNG LƯỢNG |
| 3 | TÙY CHỌN 1 | L | Shadow Predator | 3 LỰA CHỌN |
| 4 | TÙY CHỌN 2 | I | Shadow Ambush | 3 LỰA CHỌN |

## 5. Panel phụ kiện trang bị (phải dưới)

Panel 1292,670,556 x 206, vát 18, nền #0E141C, viền #26313D 1 px.

| Thành phần | x | y | w | h | cap | font ≈ | Màu / ghi chú |
|---|---|---|---|---|---|---|---|
| PHỤ KIỆN TRANG BỊ | 1311 | 677 | 124 | 21 | 12 | 16.5 | #E6EDF5, baseline 693.5 |
| 1 Ô DUY NHẤT | 1742 | 677.5 | 87 | 13.5 | 9.5 | 13.5 | #3D4A59, right-aligned mép phải 1829 |
| Icon phụ kiện | 1315 | 711 | 80 | 80 | – | – | nền #0A0F16, viền #26313D |
| Tên phụ kiện | 1413 | 703 | 188 | 23 | 15 | 21 | #E6EDF5, baseline 720.5 |
| Dòng chỉ số | 1412.5 | 761.5 | 200 | 16.5 | 11 | 15 | #4ADE80 |
| Dòng mô tả | 1412.5 | 780 | 241 | 18 | 8 | 11 | #8A99AB |
| Nút ĐỔI PHỤ KIỆN (rect) | 1310 | 812 | 250 | 42 | – | – | #121821, viền #3A4A5C, vát 12 |
| Chữ ĐỔI PHỤ KIỆN | 1392.5 | 818 | 85.5 | 24 | 12 | 16.5 | #C4D0DC, tâm chữ 1435 vs tâm nút 1435 |
| Nút PHÂN RÃ (rect) | 1580 | 812 | 250 | 42 | – | – | #121821, viền #3A4A5C, vát 12 |
| Chữ PHÂN RÃ | 1677 | 821 | 56 | 16.5 | 12.5 | 17 | #FF3B4E, tâm chữ 1705.5 vs tâm nút 1705 |

### 5.1 Tên phụ kiện có đè lên header không? — KHÔNG đè, nhưng chỉ cách 5 px

Đo bằng bbox path chữ và baseline, không phải cảm quan:

- Header PHỤ KIỆN TRANG BỊ: bbox y 677 đến 698, baseline 693.5, cap 12. Bbox thấp hơn baseline vì chữ BỊ có dấu chấm dưới.
- Tên phụ kiện: bbox y 703 đến 726, baseline 720.5, cap 15.
- **Khe dọc = 5 px** (đáy bbox header đến đỉnh bbox tên). **Hiệu baseline = 27 px**.
- Kết luận: không chồng lấn ở bản mock, nhưng chỉ 5 px. Tên là tiếng Việt có dấu (dấu mũ, dấu thanh vươn trên cap khoảng 2 px) nên khi đổi font hoặc tên dài hơn sẽ đè. Phải khóa chiều cao vùng tên (khuyến nghị 24 px) hoặc dùng VerticalLayoutGroup, không để text tự do.
- Theo trục x: header kết thúc ở 1435, tên bắt đầu 1413 nên **chồng theo x 22 px**. Bắt buộc vẽ header trước, tên sau (z-order); nếu header đổi chuỗi dài hơn phải chặn width khoảng 124 px.

## 6. Thanh hero giữa

Khung 468,190,800 x 450, vát 56, gradient dọc #121821 đến #080C11, viền #26313D 1 px + viền trong #8A4BF0 opacity 35%.

| Thành phần | x | y | w | h | cap | font ≈ | Màu / ghi chú |
|---|---|---|---|---|---|---|---|
| 3 vòng tròn trang trí | 654 | 172 | 428 | 428 | – | – | tâm (868,386), bán kính 214 / 186 / 158, nét #8A4BF0 op 0.10-0.16 |
| Ảnh tank | 688 | 206 | 360 | 360 | – | – | tâm ảnh (868, 386) trùng tâm vòng |
| Quầng tím sau tank | 668 | 186 | 400 | 400 | – | – | radial #8A4BF0 20% đến 0 |
| Bóng đổ chân tank | 678 | 506 | 380 | 90 | – | – | ellipse đen 55% đến 0 |
| Vạch accent dọc | 492 | 212 | 3 | 16 | – | – | #8A4BF0 |
| XE TĂNG ĐANG CHỌN | 502.5 | 206.5 | 120.5 | 18.5 | 11 | 15 | #C9A9FF |
| CURRENT UNIT / BLACK SHADOW | 503.5 | 225.5 | 196 | 12.5 | 10 | 13.5 | #3D4A59, letter-spacing rộng |
| Ô CẤP ĐỘ (rect) | 1138 | 206 | 106 | 58 | – | – | nền #0A0F16, viền #26313D, vát 12 |
| Chữ CẤP ĐỘ | 1152.5 | 210.5 | 34 | 16.5 | 9 | 12 | #5A6675 |
| Số 05 | 1152.5 | 236 | 26 | 24 | 23 | 31.5 | #2FE8E0 |
| / 90 | 1184.5 | 243 | 24.5 | 12 | 11.5 | 15.5 | #5A6675 |
| Tên lớn BLACK SHADOW | 757 | 579 | 223 | 34.5 | 33.5 | 46 | #FFFFFF, căn giữa khung, tâm 868.5 |
| Dòng role | 815 | 617 | 105.5 | 20 | 11.5 | 16 | #C9A9FF, letter-spacing rộng, căn giữa |
| Ngoặc góc trong hero | 488 | 282 | 30 | 30 | – | – | 4 góc: 488,282 / 1218,282 / 488,526 / 1248,526; nét #8A4BF0 2 px |

Vùng an toàn cho tên lớn khi bind tank khác: y 570 đến 620, rộng tối đa 700 px (tâm 868.5). Tên hiện tại cao 34.6 px và không có dấu; tank có dấu tiếng Việt cần chừa thêm khoảng 6 px phía trên.

### 6.1 Thanh cấp độ (dưới hero)

Rect 468,656,800 x 72, vát 16, nền #0E141C, viền #26313D, vạch accent trái 468,656, **3 x 72** #FF7A18.

| Thành phần | x | y | w | h | cap | font ≈ | Màu / ghi chú |
|---|---|---|---|---|---|---|---|
| CẤP ĐỘ HIỆN TẠI | 486.5 | 662.5 | 82 | 17.5 | 10 | 13.5 | #5A6675 |
| Số cấp 05 | 486.5 | 690.5 | 33 | 30.5 | 29.5 | 40.5 | #E6EDF5 |
| / 90 | 526.5 | 703 | 25 | 12.5 | 11.5 | 15.5 | #5A6675 |
| + CẤP 6 | 600.5 | 688.5 | 45 | 16.5 | 11 | 15 | #FFB347 |
| Track cấp | 704 | 696 | 300 | 10 | – | – | #1A2230, không bo |
| Fill cấp | 704 | 696 | 16.5 | 10 | – | – | #FF7A18 (5/90 x 300 = 16.7, đo 16.5) |
| 9 vạch chia dọc | 704 | 692 | 300 | 18 | – | – | 1 px mỗi vạch tại x = 704 + 33.33 k, #26313D op 0.9 |
| Nút NÂNG CẤP | 1028 | 672 | 226 | 52 | – | – | gradient ngang #FF7A18 đến #E85F00, viền #FFB347 1.4, vát 12 |
| Chữ NÂNG CẤP | 1051 | 681 | 87 | 24 | 17 | 23 | #0B0F14, đậm |
| Icon ngọc | 1192 | 688 | 22 | 22 | – | – | ảnh |
| Giá 5 | 1216.5 | 691 | 9 | 17.5 | 16.5 | 22.5 | #0B0F14 |

Công thức track: fill_w = 300 x level / 90. Vạch chia mỗi 10 cấp, 9 vạch bên trong (không có vạch ở 2 đầu).

## 7. Nút CTA & các nút phụ

| Thành phần | x | y | w | h | cap | font ≈ | Màu / ghi chú |
|---|---|---|---|---|---|---|---|
| CTA rect | 1292 | 890 | 556 | 86 | – | – | gradient ngang #FF7A18 đến #E85F00, viền #FFB347 1.6, vát 26; phủ radial trắng 14% tâm (1570,933) bán kính 278 x 43 |
| XÁC NHẬN RA TRẬN | 1320.5 | 902 | 206 | 34 | 20.5 | 28.5 | #0B0F14, đậm |
| DEPLOY / SELECT MISSION | 1322.5 | 942.5 | 133 | 10.5 | 9.5 | 13.5 | #5A3A10, letter-spacing rộng |
| Mũi tên phải | 1784 | 920 | 24 | 26 | – | – | chevron nét #0B0F14 dày 3.4 |
| QUAY LẠI rect | 72 | 100 | 158 | 48 | – | – | #121821, viền #26313D, vát 14 |
| Chevron trái | 86 | 117 | 8 | 14 | – | – | nét #8A99AB 2.2 |
| Chữ QUAY LẠI | 105 | 113.5 | 69.5 | 21.5 | 15 | 20.5 | #C4D0DC |
| Ô tiền tệ | 1596 | 90 | 188 | 62 | – | – | #121821, viền #26313D, vát 16 |
| Icon ngọc | 1606 | 101 | 40 | 40 | – | – | ảnh |
| Số 125 | 1652.5 | 102 | 38.5 | 26 | 24.5 | 34 | #FFD79A |
| NGỌC MA PHÁP | 1654.5 | 129 | 74.5 | 16 | 14 | 19.5 | #5A6675 |
| Settings 44 x 44 | 1804 | 100 | 44 | 44 | – | – | #121821, viền #26313D, 3 vạch ngang + 3 chấm teal 6 x 6 (#2FE8E0) tại 1807,112 / 1814,119 / 1821,126 |
| HANGAR / UNIT CONFIGURATION | 249.5 | 69.5 | 241 | 15.5 | 11.5 | 16 | #5A6675 |
| Tiêu đề QUẢN LÝ XE TĂNG & PHỤ KIỆN | 249 | 106 | 459 | 60 | 37.5 | 51 | #FFFFFF, đậm |
| Footer trái | 73 | 1022 | 519.5 | 13.5 | 10 | 13.5 | #3D4A59 |
| Footer phải | 1726 | 1019.5 | 121 | 19.5 | 11 | 15 | #3D4A59 |

## 8. Danh mục toàn bộ chuỗi chữ trong mock

x/y/w/h là bbox của path chữ (hệ 1920 x 1080). cap = chiều cao glyph in hoa đo trên jpg (±0.5 px). font = cap chia 0.73 (ước lượng). Cột kiểu: tĩnh = nhãn cố định trong prefab; động = bind dữ liệu lúc runtime.

| #path | Chuỗi | x | y | w | h | cap | baseline | font ≈ | Màu | Kiểu |
|---|---|---|---|---|---|---|---|---|---|---|
| 81 | HANGAR / UNIT CONFIGURATION | 249.5 | 69.5 | 241 | 15.5 | 11.5 | 83.5 | 16 | #5A6675 | tĩnh |
| 85 | 125 | 1652.5 | 102 | 38.5 | 26 | 24.5 | 127.5 | 34 | #FFD79A | động |
| 82 | QUẢN LÝ XE TĂNG & PHỤ KIỆN | 249 | 106 | 459 | 60 | 37.5 | 158 | 51 | #FFFFFF | tĩnh |
| 80 | QUAY LẠI | 105 | 113.5 | 69.5 | 21.5 | 15 | 129.5 | 20.5 | #C4D0DC | tĩnh |
| 86 | NGỌC MA PHÁP | 1654.5 | 129 | 74.5 | 16 | 14 | 142 | 19.5 | #5A6675 | tĩnh |
| 95 | DANH SÁCH XE TĂNG | 88 | 186.5 | 159.5 | 20 | 15 | 207 | 20.5 | #E6EDF5 | tĩnh |
| 97 | 06 | 421 | 195.5 | 22.5 | 21 | 20.5 | 217 | 28 | #3D4A59 | động |
| 254 | CHỈ SỐ CHIẾN XA | 1310.5 | 196 | 113.5 | 19.5 | 13 | 215.5 | 18 | #E6EDF5 | tĩnh |
| 255 | 08 STATS | 1769 | 201.5 | 59.5 | 10 | 10.5 | 212 | 14.5 | #3D4A59 | động |
| 150 | XE TĂNG ĐANG CHỌN | 502.5 | 206.5 | 120.5 | 18.5 | 11 | 221 | 15 | #C9A9FF | tĩnh |
| 153 | CẤP ĐỘ | 1152.5 | 210.5 | 34 | 16.5 | 9 | 224 | 12 | #5A6675 | động |
| 96 | 06 UNITS | 88.5 | 213.5 | 59.5 | 10 | 9.5 | 224 | 13 | #5A6675 | động |
| 151 | CURRENT UNIT / BLACK SHADOW | 503.5 | 225.5 | 196 | 12.5 | 10 | 237.5 | 13.5 | #3D4A59 | động |
| 154 | 05 | 1152.5 | 236 | 26 | 24 | 23 | 259.5 | 31.5 | #2FE8E0 | động |
| 155 | / 90 | 1184.5 | 243 | 24.5 | 12 | 11.5 | 255 | 15.5 | #5A6675 | động |
| 257 | TẦN CÔNG | 1326.5 | 253 | 58 | 16.5 | 11 | 268.5 | 15 | #C4D0DC | tĩnh |
| 258 | 20 | 1773 | 253.5 | 22.5 | 21 | 19.5 | 274 | 26.5 | #E6EDF5 | động |
| 259 | ATK | 1812.5 | 258.5 | 19.5 | 10 | 10 | 269 | 13.5 | #5A6675 | tĩnh |
| 100 | WALL TITAN | 180.5 | 262 | 125 | 21 | 20 | 283 | 27.5 | #E6EDF5 | động |
| 101 | TANKER / HỖ TRỢ | 180.5 | 285.5 | 93 | 19.5 | 9.5 | 301 | 13 | #E0A94B | động |
| 263 | MÁU TỐI ĐA | 1326.5 | 302 | 66.5 | 16.5 | 11 | 318 | 15 | #C4D0DC | tĩnh |
| 264 | 650 | 1761.5 | 302.5 | 34 | 21 | 20 | 323.5 | 27 | #E6EDF5 | động |
| 265 | HP | 1819 | 307.5 | 12.5 | 10 | 9.5 | 318 | 13.5 | #5A6675 | tĩnh |
| 104 | CẤP 01 / 90 | 180.5 | 320.5 | 60.5 | 15 | 10 | 335.5 | 14 | #8A99AB | động |
| 269 | GIỚI HẠN NĂNG LƯỢNG | 1326.5 | 352 | 129.5 | 19 | 11 | 367 | 15 | #C4D0DC | tĩnh |
| 270 | 120 | 1765 | 351.5 | 30.5 | 21 | 19.5 | 372.5 | 26.5 | #E6EDF5 | động |
| 271 | EN | 1819 | 356.5 | 12.5 | 10 | 10.5 | 367 | 14 | #5A6675 | tĩnh |
| 107 | RED NOVA | 181.5 | 382 | 101.5 | 21.5 | 20 | 402.5 | 27.5 | #E6EDF5 | động |
| 275 | PHÒNG THỦ | 1326.5 | 400.5 | 66 | 16 | 11 | 416 | 15 | #C4D0DC | tĩnh |
| 276 | 12 | 1776.5 | 401 | 19 | 20.5 | 19.5 | 421 | 26.5 | #E6EDF5 | động |
| 277 | DEF | 1813.5 | 405.5 | 18.5 | 10 | 10 | 416 | 13.5 | #5A6675 | tĩnh |
| 108 | DPS / HỎA LỰC XA | 180.5 | 407 | 95.5 | 18 | 10 | 422 | 14 | #FF5A3C | động |
| 111 | CẤP 10 / 90 | 180.5 | 440.5 | 60.5 | 15 | 10 | 455.5 | 14 | #8A99AB | động |
| 281 | TỐC ĐỘ DI CHUYỂN | 1326.5 | 447.5 | 108 | 21.5 | 11 | 465.5 | 15 | #C4D0DC | tĩnh |
| 282 | 110 | 1769 | 449.5 | 26.5 | 21 | 20 | 470.5 | 27.5 | #E6EDF5 | động |
| 283 | SPD | 1812.5 | 454.5 | 19 | 10.5 | 9.5 | 465 | 13 | #5A6675 | tĩnh |
| 287 | TỈ LỆ GIẢM HỒI CHIÊU | 1326.5 | 497 | 122 | 21 | 11 | 514.5 | 15 | #C4D0DC | tĩnh |
| 288 | 0 | 1784.5 | 498.5 | 11 | 21 | 19.5 | 519.5 | 26.5 | #E6EDF5 | động |
| 116 | BLACK SHADOW | 181.5 | 502 | 164 | 21.5 | 20 | 522.5 | 27.5 | #E6EDF5 | động |
| 289 | % | 1821.5 | 503.5 | 10 | 10.5 | 10 | 513.5 | 13.5 | #5A6675 | tĩnh |
| 117 | DPS / SÁT THỦ | 180.5 | 527 | 77 | 14.5 | 9.5 | 541.5 | 13 | #8A4BF0 | động |
| 292 | TỈ LỆ SÁT THƯƠNG | 1326.5 | 547.5 | 105.5 | 19.5 | 11 | 563 | 15 | #C4D0DC | tĩnh |
| 293 | 0 | 1784.5 | 547.5 | 11 | 21 | 19.5 | 568 | 26.5 | #E6EDF5 | động |
| 294 | % | 1821.5 | 552.5 | 10 | 10.5 | 9.5 | 562.5 | 13 | #5A6675 | tĩnh |
| 120 | CẤP 05 / 90 | 180.5 | 560.5 | 63 | 15 | 10 | 575.5 | 14 | #8A99AB | động |
| 121 | ĐANG CHỌN | 372 | 564.5 | 57.5 | 13.5 | 8.5 | 574 | 11.5 | #FF7A18 | tĩnh |
| 190 | BLACK SHADOW | 757 | 579 | 223 | 34.5 | 33.5 | 613.5 | 46 | #FFFFFF | động |
| 297 | TỈ LỆ XUYÊN GIÁP | 1326.5 | 596.5 | 102.5 | 19.5 | 11 | 612 | 15 | #C4D0DC | tĩnh |
| 298 | 0 | 1784.5 | 596.5 | 11 | 21 | 19.5 | 617.5 | 27 | #E6EDF5 | động |
| 299 | % | 1821.5 | 601.5 | 10 | 10.5 | 9.5 | 612 | 13 | #5A6675 | tĩnh |
| 191 | SÁT THỦ CƠ ĐỘNG | 815 | 617 | 105.5 | 20 | 11.5 | 633 | 16 | #C9A9FF | động |
| 124 | LEVIATHAN | 181.5 | 622 | 115 | 21 | 20 | 643 | 27.5 | #E6EDF5 | động |
| 125 | TANKER / TANKER | 180.5 | 650.5 | 97 | 10.5 | 9.5 | 661 | 13 | #2FE8E0 | động |
| 194 | CẤP ĐỘ HIỆN TẠI | 486.5 | 662.5 | 82 | 17.5 | 10 | 677 | 13.5 | #5A6675 | động |
| 303 | PHỤ KIỆN TRANG BỊ | 1311 | 677 | 124 | 21 | 12 | 693.5 | 16.5 | #E6EDF5 | tĩnh |
| 304 | 1 Ô DUY NHẤT | 1742 | 677.5 | 87 | 13.5 | 9.5 | 691 | 13.5 | #3D4A59 | động |
| 128 | CẤP 01 / 90 | 180.5 | 680.5 | 60.5 | 15 | 10 | 695.5 | 14 | #8A99AB | động |
| 211 | NÂNG CẤP | 1051 | 681 | 87 | 24 | 17 | 705 | 23 | #0B0F14 | tĩnh |
| 197 | + CẤP 6 | 600.5 | 688.5 | 45 | 16.5 | 11 | 705 | 15 | #FFB347 | động |
| 195 | 05 | 486.5 | 690.5 | 33 | 30.5 | 29.5 | 721 | 40.5 | #E6EDF5 | động |
| 212 | 5 | 1216.5 | 691 | 9 | 17.5 | 16.5 | 708.5 | 22.5 | #0B0F14 | động |
| 196 | / 90 | 526.5 | 703 | 25 | 12.5 | 11.5 | 714.5 | 15.5 | #5A6675 | động |
| 305 | Lỗi Huyết Tinh Sinh Học | 1413 | 703 | 188 | 23 | 15 | 720.5 | 21 | #E6EDF5 | động |
| 214 | KỸ NĂNG XE TĂNG | 484 | 738.5 | 140.5 | 20.5 | 15 | 758.5 | 20.5 | #E6EDF5 | tĩnh |
| 131 | WATER B | 180.5 | 742 | 90.5 | 21 | 20 | 763 | 27.5 | #E6EDF5 | động |
| 306 | +15% Tấn công, +15% Giới hạn máu | 1412.5 | 761.5 | 200 | 16.5 | 11 | 774.5 | 15 | #4ADE80 | động |
| 215 | TANK SKILLS / 04 SLOTS | 484 | 764 | 169 | 13.5 | 9.5 | 775.5 | 13 | #5A6675 | động |
| 132 | HỖ TRỢ / DPS | 180.5 | 765.5 | 71.5 | 19.5 | 10 | 781.5 | 13.5 | #7FB3FF | động |
| 307 | BỊ ĐỘNG: Miễn nhiễm sát thương chí mạng. | 1412.5 | 780 | 241 | 18 | 8 | 794 | 11 | #8A99AB | tĩnh |
| 218 | 01 | 483.5 | 793.5 | 13 | 10 | 10.5 | 804.5 | 14 | #3D4A59 | động |
| 227 | 02 | 686.5 | 793.5 | 13 | 10 | 10 | 804.5 | 14 | #3D4A59 | động |
| 236 | 03 | 889.5 | 793.5 | 12.5 | 10 | 10.5 | 804.5 | 14 | #3D4A59 | động |
| 245 | 04 | 1092.5 | 793.5 | 13 | 10 | 10 | 804 | 14 | #3D4A59 | động |
| 220 | J | 627 | 798.5 | 8 | 14 | 5 | 812 | 6.5 | #C9A9FF | tĩnh |
| 229 | K | 830 | 798.5 | 8.5 | 14 | 13.5 | 812.5 | 18.5 | #C9A9FF | tĩnh |
| 238 | L | 1033.5 | 798.5 | 7.5 | 14 | 2 | 812.5 | 3 | #C9A9FF | tĩnh |
| 247 | I | 1238.5 | 798.5 | 3 | 14 | 0 | 0 | 0 | #C9A9FF | tĩnh |
| 135 | CẤP 01 / 90 | 180.5 | 800.5 | 60.5 | 15 | 10 | 815.5 | 14 | #8A99AB | động |
| 221 | KỸ NĂNG THƯỜNG | 482.5 | 811.5 | 88.5 | 13 | 9.5 | 824.5 | 13.5 | #5A6675 | tĩnh |
| 230 | KỸ NĂNG CHUYÊN BIỆT | 685.5 | 811.5 | 114.5 | 16.5 | 9.5 | 824.5 | 13.5 | #5A6675 | tĩnh |
| 239 | TÙY CHỌN 1 | 888.5 | 811.5 | 57.5 | 16.5 | 9.5 | 824.5 | 13 | #5A6675 | động |
| 248 | TÙY CHỌN 2 | 1091.5 | 811.5 | 60 | 16.5 | 9.5 | 824.5 | 13 | #5A6675 | động |
| 309 | ĐỔI PHỤ KIỆN | 1392.5 | 818 | 85.5 | 24 | 12 | 838 | 16.5 | #C4D0DC | tĩnh |
| 311 | PHÂN RÃ | 1677 | 821 | 56 | 16.5 | 12.5 | 838 | 17 | #FF3B4E | tĩnh |
| 141 | GREEN FOX | 181 | 862 | 114 | 21.5 | 21 | 882.5 | 28.5 | #5A6675 | động |
| 142 | DPS / DPS | 180.5 | 890.5 | 53.5 | 11 | 11 | 902 | 15 | #3D4A59 | động |
| 314 | XÁC NHẬN RA TRẬN | 1320.5 | 902 | 206 | 34 | 20.5 | 929 | 28.5 | #0B0F14 | tĩnh |
| 145 | CẤP 01 / 90 | 180.5 | 920.5 | 60.5 | 15 | 11 | 936 | 15 | #3D4A59 | động |
| 146 | ẢI 2-5 | 398 | 921 | 31.5 | 14.5 | 10 | 935 | 13.5 | #FFB347 | động |
| 223 | Shadow Claw | 511 | 938.5 | 105.5 | 16 | 10.5 | 954.5 | 14.5 | #E6EDF5 | động |
| 232 | Shadow Step | 715 | 938.5 | 103 | 20 | 10.5 | 954.5 | 14.5 | #E6EDF5 | động |
| 241 | Shadow Predator | 900.5 | 938.5 | 138 | 16 | 10.5 | 954.5 | 14.5 | #E6EDF5 | động |
| 250 | Shadow Ambush | 1106.5 | 938.5 | 132 | 16 | 10.5 | 954.5 | 14.5 | #E6EDF5 | động |
| 315 | DEPLOY / SELECT MISSION | 1322.5 | 942.5 | 133 | 10.5 | 9.5 | 953 | 13.5 | #5A3A10 | tĩnh |
| 224 | KHÔNG TỐN NĂNG LƯỢNG | 500.5 | 964.5 | 126 | 17.5 | 9.5 | 978 | 13 | #8A99AB | tĩnh |
| 233 | TỐN NĂNG LƯỢNG | 722.5 | 964.5 | 88 | 17.5 | 9.5 | 978.5 | 13 | #8A99AB | tĩnh |
| 242 | 3 LỰA CHỌN | 940 | 967 | 59 | 15 | 9.5 | 978.5 | 13 | #8A99AB | động |
| 251 | 3 LỰA CHỌN | 1143 | 967 | 59 | 15 | 9.5 | 978.5 | 13 | #8A99AB | động |
| 323 | NHẤN ESC ĐỂ QUAY LẠI | 1726 | 1019.5 | 121 | 19.5 | 11 | 1035.5 | 15 | #3D4A59 | tĩnh |
| 322 | UNITY 6 / TOP-DOWN 2D SHOOTER / CO-OP 1-4 / POST-APOCALYPTIC SCI-FI | 73 | 1022 | 519.5 | 13.5 | 10 | 1033.5 | 13.5 | #3D4A59 | động |

Tổng cộng 100 chuỗi nhìn thấy được. Các chuỗi phải vẽ động (bind theo dữ liệu): tên 6 xe, role từng xe, CẤP xx / 90, số đếm 06, số 06 UNITS, số ngọc 125, số cấp trong ô CẤP ĐỘ (05 và / 90), số cấp thanh dưới (05, / 90, + CẤP 6), giá nâng cấp (5), 8 giá trị chỉ số, tên phụ kiện + dòng chỉ số, CURRENT UNIT / (tên), tên lớn (TANK NAME), tag ĐANG CHỌN, tag ẢI 2-5.

## 9. Bảng màu dùng chung

| Tên | Hex | Dùng ở đâu |
|---|---|---|
| Cam chủ đạo | #FF7A18 | CTA, NÂNG CẤP, vạch accent list/level, focus, tag ĐANG CHỌN, dot + bar TẦN CÔNG |
| Cam đậm | #E85F00 | đầu cuối gradient CTA/NÂNG CẤP |
| Cam sáng | #FFB347 | viền nút cam, + CẤP 6, tag ẢI, dot TỈ LỆ SÁT THƯƠNG |
| Vàng ngọc | #FFD79A | số lượng ngọc |
| Vàng đất | #E0A94B | role WALL TITAN |
| Tím chủ đạo | #8A4BF0 | viền hàng chọn, tick KỸ NĂNG, viền ô phím, icon skill 3-4, dot TỈ LỆ XUYÊN GIÁP, role hàng 3 |
| Tím nhạt | #C9A9FF | XE TĂNG ĐANG CHỌN, role hero, phím J/K/L/I, dot TỈ LỆ GIẢM HỒI |
| Teal | #2FE8E0 | số cấp ô CẤP ĐỘ, tick panel chỉ số, dot PHÒNG THỦ, accent thẻ 2, role LEVIATHAN |
| Xanh dương | #7FB3FF | dot/bar GIỚI HẠN NĂNG LƯỢNG, role WATER B |
| Xanh lá | #4ADE80 | dot/bar TỐC ĐỘ, dòng chỉ số phụ kiện |
| Đỏ | #FF3B4E | dot/bar MÁU TỐI ĐA, chữ PHÂN RÃ |
| San hô | #FF5A3C | role RED NOVA |
| Chữ chính | #E6EDF5 | tên xe, tên kỹ năng, số chỉ số, tên phụ kiện |
| Chữ phụ | #C4D0DC | nhãn chỉ số, chữ nút phụ, QUAY LẠI |
| Chữ mờ | #8A99AB | mô tả kỹ năng, CẤP xx/90, mô tả phụ kiện |
| Chữ rất mờ | #5A6675 | header phụ, đơn vị, / 90, role hàng khoá, tên ngọc |
| Chữ mờ nhất | #3D4A59 | 08 STATS, 1 Ô DUY NHẤT, CURRENT UNIT, footer, số thứ tự thẻ, số 06 |
| Viền panel | #26313D | viền mọi panel/hàng/thẻ, vạch chia level |
| Viền sáng | #3A4A5C | viền nút phụ, đường kẻ footer, lưới nền |
| Rãnh | #1A2230 | track chỉ số, track cấp, đỉnh gradient hàng chọn |
| Nền tối | #121821 | QUAY LẠI, ô tiền tệ, ô phím, nút phụ, đỉnh gradient hero/stats |
| Nền panel | #0E141C | hàng xe, thẻ kỹ năng, panel phụ kiện, thanh cấp |
| Nền icon | #0A0F16 | khung icon hàng xe, khung icon phụ kiện, ô CẤP ĐỘ |
| Gần đen | #0B0F14 | chữ trên nền cam, đỉnh nền màn |
| Đen đáy | #05070A | đáy nền màn, overlay hàng khoá |

Gradient theo defs của SVG:

| id | Từ | Đến | Hướng | Dùng ở |
|---|---|---|---|---|
| paint12 | #121821 | #080C11 | dọc (468,190) den (468,640) | nền hero |
| paint20 | #121821 | #0A0E13 | dọc (1292,190) den (1292,654) | nền panel chỉ số |
| paint10 | #1A2230 | #0E141C | dọc (71,483) den (71,593) | nền hàng đang chọn |
| paint21 | #FF7A18 | #E85F00 | ngang (1292,890) den (1848,890) | nền CTA |
| paint15 | #FF7A18 | #E85F00 | ngang (1028,672) den (1254,672) | nền NÂNG CẤP |
| paint8 | #FF7A18 90% | #8A4BF0 55% | ngang, qua #2FE8E0 70% tại 45% | dải 3 px trên cùng |
| paint23 | #26313D 90% | #26313D 90% | ngang, giữa #3A4A5C 50% | kẻ chân trang y=1014 |
| paint25 | #FFFFFF 100% | #FFFFFF 0% | ngang 248 den 888 | gạch chân tiêu đề (qua mask) |

## 10. Rủi ro / mơ hồ

1. **Font không xác định.** Chữ trong SVG đã outline, không còn metadata font. Dáng rất hẹp: tiêu đề 26 glyph rộng 458.9 px ở cap 37.4, tức advance trung bình khoảng 0.35 em, hẹp hơn Anton/Teko thường dùng. Dựng bằng TMP + font thường sẽ dài hơn 40-60% và tràn panel. Cần font condensed tương đương, hoặc bật horizontal scale khoảng 0.6, hoặc giảm cỡ chữ cho vừa bề rộng đã đo.
2. **Cỡ chữ là ước lượng.** font = cap chia 0.73 theo giả định cap-height = 0.73 em. Font thật cap 0.70 em thì mọi cỡ +4%, cap 0.76 em thì -4%, tức sai ±2 px ở cỡ lớn nhất (46-51).
3. **Cap đo trên jpg nén.** Chữ nhỏ (cap 8-10) chỉ cao 11-13 px trong ảnh 2560 nên sai ±0.5-1 px, tương đương ±1.4 px cỡ chữ. Các dòng letter-spacing rộng (08 STATS, TANK SKILLS / 04 SLOTS, DEPLOY / SELECT MISSION) dễ lệch vì nét mảnh.
4. **Riêng cap của J/K/L/I không dùng được.** Đo được 4.8 / 13.4 / 2.2 / 0 vì glyph một nét mảnh, histogram cột không hội tụ. Ô phím 28 x 28 và cỡ chữ khoảng 13 là số suy ra từ chiều cao ô, không phải từ cap.
5. **Màu lệch ±2-3/kênh nếu so với jpg** do JPEG; spec dùng mã SVG (đúng ý thiết kế). Các lớp opacity (lưới 0.012, quầng radial) không lấy đúng được từ jpg, phải dùng hệ số trong SVG.
6. **Vát góc không dựng được bằng Image thường.** Cần sprite 9-slice có 2 góc vát phải, hoặc mask shader/procedural mesh. Với hero vát 56, viền 1 px trên đường chéo sẽ bị đứt nếu 9-slice — nên vẽ viền bằng mesh riêng hoặc dùng sprite vát sẵn.
7. **Hàng đang chọn nới 1 px mỗi phía** (71,483,374 x 110) so với hàng thường (72, ..., 372 x 108). Nếu dùng VerticalLayoutGroup pitch 120 phải override thủ công rect hàng chọn, không thì lệch 1 px mọi cạnh.
8. **Tên phụ kiện cách header 5.1 px** và chồng theo trục x 21 px (header kết thúc 1434.9, tên bắt đầu 1413). Mock không đè nhưng đây là vùng rất sát: bắt buộc z-order header trước, tên sau, và khóa width header khoảng 124 px.
9. **Số 06 ở header list sát mép phải** (mép 443.7, panel kết thúc 444). Nếu label co giãn theo nội dung sẽ tràn; phải neo right-aligned vào panel.
10. **Không rõ hành vi cuộn** của danh sách: mock hiển thị đủ 6 hàng, không có scrollbar hay mask. Nếu runtime nhiều hơn 6 xe thì vùng 72..444 x 244..952 sẽ tràn, cần ScrollRect + mask; spec này không có số đo cho trạng thái cuộn.
11. **Tag ĐANG CHỌN không có nền chip**, chỉ là text cam canh phải, cùng dòng với CẤP 05 / 90. Vùng an toàn bên trái tag: x nhỏ hơn hoặc bằng 360.
12. **Icon hàng RED NOVA rộng 93.75** tràn khung 80 — có thể ảnh gốc khác tỉ lệ, không phải lỗi đo; nếu dựng bằng Image Preserve Aspect sẽ thấy khác mock.
13. **3 vòng tròn hero** chỉ đọc được bán kính (214/186/158) và opacity thô; kiểu nét đứt (dash pattern) không đo chính xác từ jpg.
14. **Đơn vị ATK/HP/EN/DEF/SPD/%** canh mép phải 1831.8 nhưng rộng khác nhau, nên nếu tách TMP số và TMP đơn vị phải canh theo mép phải, không canh tâm.
15. **Không có số đo cho trạng thái hover/pressed/disabled** của bất kỳ nút nào; mock chỉ có 1 trạng thái tĩnh. Cũng không có số đo cho trạng thái mở panel phụ kiện (ĐỔI PHỤ KIỆN dẫn tới màn khác).
16. **Chữ 1 Ô DUY NHẤT** là caption mô tả số slot; nếu game cho nhiều slot thì phải bind động.

---

Hết spec. Toạ độ dùng hệ 1920 x 1080 gốc trên-trái; khi dựng Canvas 1920 x 1080 dùng CanvasScaler Scale With Screen Size, reference 1920 x 1080, match 0.5.

