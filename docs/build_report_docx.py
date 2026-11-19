"""Tao bao cao .docx tong ket cong viec IronWasteland (9 man + Spawner + SFX + HUD)."""
import os
from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor
from PIL import Image

OUT = r"D:\GameUnity\IronWasteland\docs\BAO_CAO_IronWasteland_CongViec.docx"
SHOTS = r"D:\GameUnity\IronWasteland\docs\screenshots"
TMP = r"D:\GameUnity\IronWasteland\docs\_img_tmp"
os.makedirs(TMP, exist_ok=True)

ACCENT = RGBColor(0x1F, 0x4E, 0x79)
ACCENT2 = RGBColor(0x2E, 0x74, 0xB5)
MUTED = RGBColor(0x59, 0x59, 0x59)


def shade(cell, hex_fill):
    tcPr = cell._tc.get_or_add_tcPr()
    el = OxmlElement("w:shd")
    el.set(qn("w:val"), "clear")
    el.set(qn("w:color"), "auto")
    el.set(qn("w:fill"), hex_fill)
    tcPr.append(el)


def set_col_widths(table, widths_cm):
    for row in table.rows:
        for idx, w in enumerate(widths_cm):
            row.cells[idx].width = Cm(w)


def add_table(doc, headers, rows, widths=None, font=9.0, header_fill="1F4E79"):
    table = doc.add_table(rows=1, cols=len(headers))
    table.style = "Table Grid"
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.autofit = False

    hdr = table.rows[0].cells
    for i, text in enumerate(headers):
        hdr[i].text = ""
        p = hdr[i].paragraphs[0]
        run = p.add_run(text)
        run.bold = True
        run.font.size = Pt(font)
        run.font.color.rgb = RGBColor(0xFF, 0xFF, 0xFF)
        shade(hdr[i], header_fill)

    for r in rows:
        cells = table.add_row().cells
        for i, text in enumerate(r):
            cells[i].text = ""
            p = cells[i].paragraphs[0]
            run = p.add_run(str(text))
            run.font.size = Pt(font)

    if widths:
        set_col_widths(table, widths)
    return table


def para(doc, text, size=10.5, bold=False, italic=False, color=None, align=None, space_after=4, style=None):
    p = doc.add_paragraph(style=style)
    run = p.add_run(text)
    run.bold = bold
    run.italic = italic
    run.font.size = Pt(size)
    if color is not None:
        run.font.color.rgb = color
    if align is not None:
        p.alignment = align
    p.paragraph_format.space_after = Pt(space_after)
    return p


def bullets(doc, items, size=10.5):
    for it in items:
        p = doc.add_paragraph(style="List Bullet")
        run = p.add_run(it)
        run.font.size = Pt(size)
        p.paragraph_format.space_after = Pt(2)


def numbered(doc, items, size=10.5):
    for it in items:
        p = doc.add_paragraph(style="List Number")
        run = p.add_run(it)
        run.font.size = Pt(size)
        p.paragraph_format.space_after = Pt(2)


def h1(doc, text):
    h = doc.add_heading(text, level=1)
    for r in h.runs:
        r.font.color.rgb = ACCENT
        r.font.size = Pt(15)
    h.paragraph_format.space_before = Pt(14)
    h.paragraph_format.space_after = Pt(6)
    return h


def h2(doc, text):
    h = doc.add_heading(text, level=2)
    for r in h.runs:
        r.font.color.rgb = ACCENT2
        r.font.size = Pt(12)
    h.paragraph_format.space_before = Pt(10)
    h.paragraph_format.space_after = Pt(4)
    return h


def code(doc, text):
    p = doc.add_paragraph()
    p.paragraph_format.left_indent = Cm(0.6)
    p.paragraph_format.space_after = Pt(6)
    run = p.add_run(text)
    run.font.name = "Consolas"
    run.font.size = Pt(9)
    run.font.color.rgb = RGBColor(0x33, 0x33, 0x33)
    pPr = p._p.get_or_add_pPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")
    shd.set(qn("w:fill"), "F2F4F7")
    pPr.append(shd)
    return p


def picture(doc, filename, caption, width_cm=15.5):
    src = os.path.join(SHOTS, filename)
    if not os.path.exists(src):
        return
    spacer = doc.add_paragraph()
    spacer.paragraph_format.space_after = Pt(4)
    small = os.path.join(TMP, "scaled_" + filename)
    with Image.open(src) as im:
        im = im.convert("RGB")
        w, h = im.size
        target_w = 1400
        if w > target_w:
            im = im.resize((target_w, int(h * target_w / w)), Image.LANCZOS)
        im.save(small, "JPEG", quality=82)
    doc.add_picture(small, width=Cm(width_cm))
    doc.paragraphs[-1].alignment = WD_ALIGN_PARAGRAPH.CENTER
    cap = doc.add_paragraph()
    cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = cap.add_run(caption)
    run.italic = True
    run.font.size = Pt(8.5)
    run.font.color.rgb = MUTED
    cap.paragraph_format.space_after = Pt(10)


# ------------------------------------------------------------------ document
doc = Document()

style = doc.styles["Normal"]
style.font.name = "Calibri"
style.font.size = Pt(10.5)
style.element.rPr.rFonts.set(qn("w:eastAsia"), "Calibri")

sec = doc.sections[0]
sec.top_margin = Cm(2.0)
sec.bottom_margin = Cm(2.0)
sec.left_margin = Cm(2.2)
sec.right_margin = Cm(1.8)

# footer page number
footer_p = sec.footer.paragraphs[0]
footer_p.alignment = WD_ALIGN_PARAGRAPH.CENTER
run = footer_p.add_run("IronWasteland — Báo cáo công việc  |  trang ")
run.font.size = Pt(8)
run.font.color.rgb = MUTED
fld = OxmlElement("w:fldSimple")
fld.set(qn("w:instr"), "PAGE")
footer_p._p.append(fld)

# ------------------------------------------------------------------ title
title = doc.add_paragraph()
title.alignment = WD_ALIGN_PARAGRAPH.CENTER
r = title.add_run("BÁO CÁO CÔNG VIỆC")
r.bold = True
r.font.size = Pt(26)
r.font.color.rgb = ACCENT

sub = doc.add_paragraph()
sub.alignment = WD_ALIGN_PARAGRAPH.CENTER
r = sub.add_run("Dự án IronWasteland (Unity 6000.5.6f1 — URP 2D)")
r.font.size = Pt(13)
r.font.color.rgb = MUTED

sub2 = doc.add_paragraph()
sub2.alignment = WD_ALIGN_PARAGRAPH.CENTER
r = sub2.add_run("Xây dựng 9 màn chơi theo tài liệu thiết kế, Spawner sinh quái, SFX dùng chung và HUD theo Figma")
r.italic = True
r.font.size = Pt(11)
sub2.paragraph_format.space_after = Pt(18)

add_table(
    doc,
    ["Hạng mục", "Thông tin"],
    [
        ["Thư mục dự án", r"D:\GameUnity\IronWasteland"],
        ["Phiên bản Unity", "6000.5.6f1 (URP 2D)"],
        ["Nhánh Git hiện tại", "Stage (tách từ main, commit gần nhất e79776e)"],
        ["Ngày hoàn thành báo cáo", "19/11/2026"],
        ["Số màn đã dựng", "9 / 9 (Stage_1_1 … Stage_2_5)"],
        ["Trạng thái QA", "QA 9/9 PASS · Minimap 9/9 PASS · compile 0 error"],
    ],
    widths=[4.6, 11.4],
    font=9.5,
)

doc.add_paragraph()

# ------------------------------------------------------------------ TOC
h2(doc, "Mục lục")
toc_items = [
    "1. Tóm tắt điều hành",
    "2. Yêu cầu gốc và phạm vi đã chốt",
    "3. Kiến trúc màn chơi",
    "4. Chín màn chơi (bảng màn, mục tiêu đặc biệt, boss)",
    "5. HUD và giao diện (panel, bàn đồ chiến thuật, nameplate, kết quả)",
    "6. Camera và giới hạn biên",
    "7. Âm thanh (SFX)",
    "8. Art và Skill Icon",
    "9. Công cụ Editor",
    "10. Lỗi đã tìm ra và cách sửa",
    "11. Việt hoá chữ hiển thị",
    "12. Kiểm chứng",
    "13. Trạng thái Git",
    "14. Việc còn lại và khuyến nghị",
    "Phụ lục A — Danh mục file chính · Phụ lục B — Cách vận hành nhanh",
]
for item in toc_items:
    p = doc.add_paragraph()
    p.paragraph_format.left_indent = Cm(0.4)
    p.paragraph_format.space_after = Pt(1)
    run = p.add_run(item)
    run.font.size = Pt(10)

doc.add_paragraph()

# ------------------------------------------------------------------ 1
h1(doc, "1. Tóm tắt điều hành")
para(doc, "Báo cáo tổng hợp toàn bộ công việc đã thực hiện cho nhiệm vụ được giao: xây dựng các "
          "màn chơi theo tài liệu thiết kế kèm điều kiện thắng, mỗi màn là một scene riêng; tank được "
          "tạo từ dữ liệu TankDefinition (giống GameController hiện có); quái sinh bằng Spawner thay vì "
          "đặt sẵn trên map; bổ sung SFX dùng chung cho kỹ năng và khi gây sát thương; và viết kế hoạch "
          "chi tiết ra file Markdown trước khi triển khai.")
bullets(doc, [
    "9 scene màn chơi hoàn chỉnh (Stage_1_1 … Stage_2_5) cùng 9 StageDefinition chứa điều kiện thắng, đợt quái và phần thưởng.",
    "Framework màn chơi tách rời dữ liệu: StageObjective (5 loại mục tiêu), EnemySpawner + SpawnZone, StageDirector, StageReward, StageProgress.",
    "Toàn bộ quái sinh qua Spawner (pool + giới hạn số lượng + vùng sinh), không đặt sẵn trên map.",
    "Hệ thống SFX dùng chung: 39 file WAV (13 nhóm × 3 biến thể), SfxLibrary + AudioManager + hook cho tank/quái/màn.",
    "HUD ghép từ bộ art tách sẵn theo mockup: panel người chơi, thanh Boss, bàn đồ chiến thuật, 4 ô kỹ năng, co-op, bảng kết quả, popup Cài đặt.",
    "Nameplate trên đầu tank/quái/boss với tên tiếng Việt; minimap dùng nền thật của màn và khung thể hiện đúng vùng camera đang nhìn.",
    "Việt hoá toàn bộ chữ hiển thị trên màn chơi, tên ải và tên boss.",
    "13 công cụ Editor trong menu Tools ▸ Iron Wasteland để dựng lại HUD, gán icon, kiểm QA và sửa dữ liệu hàng loạt.",
])

# ------------------------------------------------------------------ 2
h1(doc, "2. Yêu cầu gốc và phạm vi đã chốt")
h2(doc, "2.1. Yêu cầu ban đầu")
para(doc, "Nguyên văn nhiệm vụ (rút gọn): “Xây dựng các màn theo tài liệu + thiết kế điều kiện thắng. "
          "Mỗi màn sẽ là một scene riêng. Khi bắt đầu scene thì sẽ dựa trên thông tin Tank (giống "
          "GameController hiện tại) thì cần tạo ra các Tank dựa vào dữ liệu đó. Các quái sẽ cần dùng một "
          "Spawner để sinh quái thay vì đặt sẵn ở trên map. Và kiếm SFX kích hoạt khi thi triển kỹ năng, "
          "gây sát thương trúng mục tiêu… cho tank và enemy. SFX dùng chung cho nên không cần kiếm riêng "
          "cho mỗi đối tượng, nếu được thì kiếm 2–3 cái để phát ngẫu nhiên khi kích event cho đỡ lặp. Đọc "
          "tài liệu dự án cùng các code github và lên plan chi tiết cho các nhiệm vụ này cần làm những gì "
          "vào 1 file .md.”")

h2(doc, "2.2. Các quyết định phạm vi đã chốt với nhóm trưởng")
add_table(
    doc,
    ["Mã", "Vấn đề", "Quyết định"],
    [
        ["Q1", "Có refactor GameController (tách PlayerLoadout, PlayerTankSpawner, PlayerInputController) không?", "KHÔNG — giữ nguyên GameController để tránh ảnh hưởng phần người khác đang làm."],
        ["Q2", "Vật thể hộ tống / phá được (xe, trạm radar) kế thừa gì?", "Kế thừa EnemyBrain để không phải sửa file skill nào."],
        ["Q3", "Có dựng prefab The2Face không?", "Chưa làm trong đợt này."],
        ["Q4", "Có viết boss brain riêng không?", "Không — dùng brain quái có sẵn, phóng to và ghi đè chỉ số."],
        ["Q5", "Có làm co-op không?", "Không — chỉ để khung panel co-op."],
        ["Q6", "Đặt tên scene thế nào?", "Stage_X_Y (ví dụ Stage_1_1)."],
        ["Q7", "Có màn chọn ải không?", "Có — StageSelect."],
    ],
    widths=[1.2, 7.4, 7.4],
    font=9,
)

h2(doc, "2.3. Các yêu cầu bổ sung trong quá trình làm")
add_table(
    doc,
    ["#", "Yêu cầu của người dùng", "Kết quả"],
    [
        ["1", "Chữ trên màn chơi phải có dấu tiếng Việt", "Đã Việt hoá HUD, mục tiêu, bảng kết quả, tên ải, tên boss, nameplate quái."],
        ["2", "Quái đứng yên, chỉ đuổi khi người chơi lại gần", "Tăng bán kính phát hiện của 11 prefab quái từ 10 lên 60 unit."],
        ["3", "UI quá xấu so với thiết kế — nâng cấp", "Dựng lại toàn bộ HUD/StageSelect/Cài đặt/Kết quả theo wireframe, sau đó ghép bộ art tách sẵn."],
        ["4", "Ghép bộ art tách từ ảnh mockup thành UI, chữ phải đúng dữ liệu runtime", "HUD v5 → v7: panel người chơi, Boss, minimap, kỹ năng, co-op, kết quả, cài đặt + nameplate."],
        ["5", "Bàn đồ chiến thuật phải dùng được thật: mũi tên tank/đồng đội, nền là nền của màn", "Minimap dùng sprite Background của từng màn, mũi tên xoay theo nòng pháo, phím M bật/tắt."],
        ["6", "Màn vùng 1 còn nền xanh mặc định; vòng xanh minimap đứng yên", "Đổi camera sang SolidColor #070A0E, ortho 9 → 7; khung nhìn đọc camera thật và bám theo."],
        ["7", "Tạo nhánh git Stage và đẩy lên", "Đã push nhánh Stage (2 commit: 8b4e0fc, e79776e)."],
        ["8", "Cảnh báo “objects were not cleaned up … [PathfindingGrid2D]”", "Chặn singleton tự tạo object khi thoát play; OnDisable dùng HasInstance."],
        ["9", "Ký Sinh Thiên Thạch chết vẫn hiện tên", "Thêm ShowsNameplate; quái đã kích nổ thì ẩn nameplate."],
        ["10", "Tích hợp bộ Skill Icon mới cho các tank", "Gán 24 icon mới (4 xe × 6 tank), thêm lớp Card 250×231 vào ô kỹ năng."],
    ],
    widths=[0.9, 7.4, 7.7],
    font=8.8,
)

# ------------------------------------------------------------------ 3
h1(doc, "3. Kiến trúc màn chơi")
para(doc, "Mỗi scene chỉ chứa: tank người chơi (do GameController tạo từ TankDefinition), object Background, "
          "StageDirector (gắn StageDefinition) và các object đặc biệt của màn (radar, xe hộ tống, mục tiêu phá huỷ, boss). "
          "Quái hoàn toàn do Spawner sinh ra từ các SpawnZone khai báo trong asset.")
add_table(
    doc,
    ["File", "Vai trò"],
    [
        ["Assets/Scripts/Stages/StageDefinition.cs", "ScriptableObject chứa toàn bộ dữ liệu màn: tên ải, điều kiện thắng, các đợt quái, phần thưởng ngọc, màn kế tiếp, cấp tank đề xuất."],
        ["Assets/Scripts/Stages/StageObjective.cs", "Lớp trừu tượng + 5 mục tiêu: Eliminate (diệt X quái), Survive (sống sót N giây), DestroyTargets (phá N mục tiêu theo id), Escort (hộ tống xe đi N mét), Radar (giữ trạm N giây)."],
        ["Assets/Scripts/Stages/EnemySpawner.cs", "Sinh quái theo đợt: pool tái dùng, giới hạn số lượng sống, lọc theo vùng, nhân bản chỉ số (Clone) mỗi lần sinh."],
        ["Assets/Scripts/Stages/SpawnZone.cs", "Vùng sinh quái (hình chữ nhật/bán kính) có id để đợt quái chỉ định."],
        ["Assets/Scripts/Stages/StageDirector.cs", "Điều phối trận: đếm thời gian, chạy các đợt, cộng dồn tiến độ mục tiêu, xử lý thắng/thua, trao thưởng."],
        ["Assets/Scripts/Stages/StageReward.cs", "Kết quả trận: ngọc nhận được, số phụ kiện rơi, thời gian, số quái đã diệt."],
        ["Assets/Scripts/Stages/StageProgress.cs", "Lưu tiến độ bằng PlayerPrefs: tổng ngọc, màn đã mở, màn đã thắng, tên scene theo mã màn."],
        ["Assets/Scripts/Stages/DestructibleTarget.cs", "Mục tiêu phá huỷ (tổ ong, trụ điện, Mạch Năng Lượng) — kế thừa EnemyBrain, báo về director khi chết."],
        ["Assets/Scripts/Stages/EscortVehicle.cs", "Xe hộ tống đi theo waypoint, bị quái đánh, chốt tiến độ bằng độ dài lộ trình khi tới đích."],
        ["Assets/Scripts/Stages/RadarStation.cs", "Trạm radar: yêu cầu tank đứng trong bán kính 3 m, không có quái trong 8 m, giữ 5 giây."],
        ["Assets/Scripts/Stages/BossTag.cs", "Đánh dấu object là boss để HUD hiện thanh máu riêng và nameplate cỡ lớn."],
    ],
    widths=[6.4, 9.6],
    font=8.8,
)

h2(doc, "3.1. Cơ chế sinh quái")
bullets(doc, [
    "Mỗi lần sinh, Spawner gọi prefab.Stats.Clone() trước khi Initialize — nếu không, EnemyBrain.Initialize sẽ gán tham chiếu vào asset gốc và SetCoopMode nhân máu dồn qua từng lần sinh.",
    "Đợt quái khai báo: thời điểm bắt đầu (giây), danh sách loại quái, số lượng mỗi loại, vùng sinh (ZoneId), hệ số tốc độ.",
    "Giới hạn số quái sống cùng lúc (maxAlive) và tái dùng object đã chết khi brain không tự huỷ.",
    "Quái thường tự Destroy trong OnDeath() nên pool chủ yếu dùng cho brain không tự huỷ (mục tiêu, boss).",
])

# ------------------------------------------------------------------ 4
h1(doc, "4. Chín màn chơi")
para(doc, "Bảng dưới đây lấy trực tiếp từ kết quả QA Audit và các lần chơi thử. Cột “Kết quả test” là thời gian "
          "hoàn thành và số ngọc nhận được trong lần chạy thắng gần nhất.")
add_table(
    doc,
    ["Mã", "Tên ải", "Điều kiện thắng", "Đợt", "Ngọc", "Kết quả test"],
    [
        ["1-1", "Sương Mù Tím", "Diệt 40 quái", "3", "20", "Thắng (41/40 khi chơi thật)"],
        ["1-2", "Hang Đá Gai", "Sống sót 100 giây", "3", "30", "Thắng"],
        ["1-3", "Dấu Vết Tử Binh", "Kích hoạt 2 radar + hộ tống xe 30 m", "4", "40", "Thắng — 46,6 s"],
        ["1-4", "Vòng Xoay Không Gian", "Phá 3 Mạch Năng Lượng (boss Bóng Ma Tử Địa)", "2", "60", "Thắng — 20,7 s"],
        ["2-1", "Vành Đai Bão Lửa", "Diệt 50 quái", "2", "50", "Thắng"],
        ["2-2", "Đầm Lầy Độc Khí", "Phá 4 tổ nấm + sống sót 120 giây", "3", "60", "Thắng — 142,6 s"],
        ["2-3", "Đột Kích Tiền Đồn", "Phá 3 trụ điện + hộ tống xe 30 m", "3", "70", "Thắng — 21,4 s"],
        ["2-4", "Phi Thuyền Hai Mặt", "Phá 2 Mạch Năng Lượng (Tử Thần Nguyên Tố + The2Face)", "2", "90", "Thắng — 21,2 s"],
        ["2-5", "Chiến Hạm D1ONK", "Phá 3 Mạch Năng Lượng (D1ONK)", "2", "120", "Thắng — 23,5 s"],
    ],
    widths=[1.1, 4.1, 5.4, 1.0, 1.1, 3.9],
    font=8.8,
)

h2(doc, "4.1. Ba màn mục tiêu đặc biệt")
bullets(doc, [
    "1-3 (Dấu Vết Tử Binh): hai trạm radar tại (-9, 5) và (10, -5); xe hộ tống “mones” đi từ (-15, -1) tới (15, -1). Radar chỉ tính khi tank trong 3 m, không có quái trong 8 m, giữ 5 giây.",
    "2-2 (Đầm Lầy Độc Khí): bốn tổ nấm (nest) dùng prefab Nấm Độc Gai Thạch đặt ở bốn góc ±10/±5, đồng thời phải sống sót 120 giây.",
    "2-3 (Đột Kích Tiền Đồn): ba trụ điện (tower) dùng prefab Sao Đen, và xe phá thành “ram” hộ tống 30 m.",
])

h2(doc, "4.2. Ba màn boss")
para(doc, "Boss dùng chính prefab Quái Vật Tinh Thạch (có AI sẵn) phóng to, ghi đè chỉ số theo GDD; "
          "“Mạch Năng Lượng” là prefab Sao Đen gắn DestructibleTarget với id “core”.")
add_table(
    doc,
    ["Màn", "Boss", "Máu", "Tấn công", "Giáp", "Tốc độ", "Số Mạch"],
    [
        ["1-4", "Bóng Ma Tử Địa", "2.000", "80", "45", "90", "3"],
        ["2-4", "Tử Thần Nguyên Tố", "9.500", "250", "25", "90", "2"],
        ["2-4", "The2Face", "16.000", "350", "30", "100", "—"],
        ["2-5", "D1ONK", "27.000", "180", "60", "60", "3"],
    ],
    widths=[1.5, 4.6, 2.0, 2.1, 1.5, 1.8, 1.8],
    font=9,
)
para(doc, "Ghi chú cân bằng: Bóng Ma Tử Địa hạ tấn công 120 → 80 vì tank cấp 12 chỉ còn 61/1856 máu; "
          "D1ONK hạ tấn công 500 → 180 và tốc độ 70 → 60; The2Face hạ tấn công 150 → 110 và tốc độ 80 → 60.",
     size=9.5, italic=True)

# ------------------------------------------------------------------ 5
h1(doc, "5. HUD và giao diện")
para(doc, "HUD trải qua 7 vòng phát triển. Ba vòng đầu dựng tay theo wireframe SVG; từ vòng 4 trở đi ghép từ bộ "
          "art tách sẵn trong Assets/Art/UI/GameplayHUD/Deconstructed_Components (38 PNG), mỗi panel dựng đúng "
          "kích thước gốc của ảnh rồi thu nhỏ 0,5 lần để khớp bố cục 1920×1080.")
add_table(
    doc,
    ["Panel", "Kích thước gốc", "Dữ liệu động"],
    [
        ["TopLeft — chân dung người chơi", "852×282", "Tên xe, CẤP, MÁU x/y, NĂNG LƯỢNG x/y, ATK/DEF/TỐC ĐỘ"],
        ["TopLeftInfo — dải thông tin màn", "560×116", "Tên ải, Đợt i/n, mục tiêu, số quái đã diệt, đồng hồ, ngọc"],
        ["BossPanel — thanh máu boss", "1364×156", "Tên boss, HP hiện tại / tối đa (định dạng 20.250 / 27.000 HP)"],
        ["TopRight — bàn đồ chiến thuật", "488×408", "Nền thật của màn, lưới, kim Bắc, khung vùng camera nhìn, chấm quái/boss/tank"],
        ["BottomRight — kỹ năng trang bị", "1050×282", "4 thẻ 250×231: art kỹ năng, phím J/K/L/I, tên, trạng thái SẴN SÀNG / HỒI: x GIÂY"],
        ["BottomLeft — đồng đội co-op", "852×303", "Khung phòng 1/4 và các slot trống (chưa có co-op thật)"],
        ["ResultPanel — kết quả trận", "960×540", "THẮNG/THẤT BẠI, thời gian, số quái, % máu còn, ngọc nhận, phụ kiện rơi"],
        ["SettingsPanel — cài đặt (ESC)", "900×660", "Hai thanh âm lượng, bảng phím, nút quay lại / rời trận"],
    ],
    widths=[5.6, 3.0, 7.4],
    font=8.8,
)

picture(doc, "hud_v5_normal.png", "Hình 1 — HUD trong màn 1-1: panel người chơi, dải thông tin màn, bàn đồ chiến thuật, 4 ô kỹ năng, khung co-op và nameplate trên đầu đơn vị.")

h2(doc, "5.1. Bàn đồ chiến thuật")
bullets(doc, [
    "Gộp các mảnh art rời (khung, kim Bắc, blip) thành một panel hoàn chỉnh thay vì hiển thị từng mảnh.",
    "Nền bản đồ lấy đúng sprite của object “Background” trong màn đang chơi, nên 1-1…1-4 hiện Map 1, 2-1…2-4 hiện Map 2, 2-5 hiện Map 3.",
    "Mũi tên cyan là tank người chơi, xoay theo hướng nòng pháo; ô vuông tím là quái, ô đỏ là boss; mỗi chấm có bóng đổ để đọc được trên nền sáng.",
    "Khung chữ nhật cyan thể hiện đúng vùng camera đang nhìn: đọc orthographicSize × 2 và tỉ lệ khung hình, quy đổi sang toạ độ bản đồ; tank đi tới đâu khung chạy theo tới đó.",
    "Phím M bật/tắt bảng bản đồ.",
])
picture(doc, "hud_v7_viewframe_corner.png", "Hình 2 — Khung nhìn trên bản đồ lệch lên phải khi tank di chuyển tới góc map; nền màn không còn lộ màu xanh skybox.", width_cm=15.0)

h2(doc, "5.2. Nameplate và bảng kết quả")
bullets(doc, [
    "Nameplate vẽ trên một canvas phủ toàn màn hình: tên + thanh máu nổi trên đầu tank/quái/boss, tối đa 6 nhãn trong bán kính 16 m, nhãn trùng chỗ được đẩy lên 26 px.",
    "Tên quái lấy từ bảng Việt hoá 12 loại (Ký Sinh Thiên Thạch, Mặt Trăng Đen, Nấm Độc Gai Thạch…), tank hiện tên xe kèm nhãn “BẠN”, boss kèm nhãn “BOSS”.",
    "Bảng kết quả hiện tiêu đề THẮNG/THẤT BẠI, lý do thua, thời gian, số quái đã diệt, phần trăm máu còn lại, ngọc nhận được và số phụ kiện rơi.",
])
picture(doc, "hud_v5_result.png", "Hình 3 — Bảng kết quả trận sau khi thắng, có nút Màn tiếp theo / Chơi lại / Về màn chọn.", width_cm=14.5)

# ------------------------------------------------------------------ 6
h1(doc, "6. Camera và giới hạn biên")
bullets(doc, [
    "Thêm StageBounds: đọc kích thước object Background (37,63 × 21 unit) rồi kẹp vị trí tank trong biên, đồng thời đẩy tank ra khỏi vùng bị HUD che ở góc dưới-phải.",
    "CameraFollow2D có thêm SetBounds/ClearBounds để camera không chạy ra ngoài nền.",
    "Đổi Clear Flags của cả 9 scene sang SolidColor #070A0E để phần ngoài bản đồ không còn lộ màu xanh mặc định của Unity.",
    "Giảm orthographic size từ 9 xuống 7: khung hình 24,9 × 14 nhỏ hơn bản đồ nên camera có không gian di chuyển thật (trước đó camera gần như đứng yên).",
])

# ------------------------------------------------------------------ 7
h1(doc, "7. Âm thanh (SFX)")
para(doc, "Bộ âm thanh dùng chung, không gắn riêng cho từng đối tượng: 39 file WAV chia 13 nhóm, mỗi nhóm 3 biến thể "
          "để phát ngẫu nhiên cho đỡ lặp, kèm asset SfxLibrary liệt kê toàn bộ clip.")
add_table(
    doc,
    ["Thành phần", "Vai trò"],
    [
        ["Assets/Scripts/Audio/SfxCategory.cs", "Khai báo 13 nhóm âm thanh (bắn thường, kỹ năng, trúng đích, nổ, chết, hồi máu, khiên, radar, thắng, thua…)."],
        ["Assets/Scripts/Audio/SfxLibrary.cs + SfxLibrary.asset", "Bảng tra clip theo nhóm, mỗi nhóm nhiều biến thể."],
        ["Assets/Scripts/Audio/AudioManager.cs", "Phát âm thanh, âm lượng tổng và âm lượng SFX riêng, chống phát trùng trong thời gian ngắn."],
        ["Assets/Scripts/Audio/TankSfxHooks.cs", "Gắn vào tank: bắn, thi triển kỹ năng, trúng đích, chết."],
        ["Assets/Scripts/Audio/EnemySfxHooks.cs", "Gắn vào quái: bắn, trúng đích (nghe qua EnemyBrain.OnTakeDamageEvent), chết, nổ."],
        ["Assets/Scripts/Audio/StageSfxHooks.cs", "Âm thanh màn chơi: bắt đầu, hoàn thành mục tiêu, thắng, thua."],
    ],
    widths=[6.6, 9.4],
    font=8.8,
)
para(doc, "Lưu ý kỹ thuật: phần lớn kỹ năng tank gọi enemy.TakeDamage(float) nên sự kiện “gây sát thương” của tank không bắn; "
          "vì vậy âm thanh trúng đích được gắn vào EnemyBrain.OnTakeDamageEvent để luôn có tiếng khi đánh trúng.",
     size=9.5, italic=True)

# ------------------------------------------------------------------ 8
h1(doc, "8. Art và Skill Icon")
h2(doc, "8.1. Bộ art HUD tách sẵn")
para(doc, "Bộ 38 PNG trong Deconstructed_Components được dùng làm nền tảng dựng HUD: 6 panel, khung và thanh máu, "
          "thành phần bản đồ, hiệu ứng chiến đấu và viền màn hình bốn góc. Các ảnh panel_* là ảnh ghép đã in sẵn chữ "
          "nên chỉ dùng để tham chiếu; HUD dựng lại từ frame_*_empty + bar_fill_* + chữ động.")
h2(doc, "8.2. Bộ Skill Icon mới")
para(doc, "Bộ icon mới nằm trong Assets/Art/Tank/Sprites, mỗi ảnh là một thẻ art hoàn chỉnh (khung đá + rune, 512×512, "
          "riêng Black Shadow 1024×1024):")
add_table(
    doc,
    ["Xe", "Thư mục", "Bốn icon theo slot J / K / L / I"],
    [
        ["BlackShadow", "skill/blackShadow", "shadow_claw · step · predator · ambush"],
        ["GreenFox", "GFSkill", "fox_fang · fox_mark · fox_fire · fox_storm"],
        ["WaterB", "WBSKILL", "healing_water · bubble_shield · water_wave · water_return"],
        ["RedNova", "RedNova", "nova_shot · triple_nova · laser_cannon · nova_barrage"],
        ["WallTitan", "WallTitan", "earth_break · earth_wall · earth_prison · earthquake"],
        ["Leviathan", "skill/leviathan", "water_pulse · water_bubble · water_armor · tidal_return"],
    ],
    widths=[2.8, 4.4, 8.8],
    font=9,
)
para(doc, "Việc gán được tự động bằng menu Tools ▸ Iron Wasteland ▸ Integrate New Skill Icons: ép import thành Sprite, "
          "gán skillIcons[4] cho từng TankDefinition, xoá glyph/cooldown cũ (vì art đã có khung riêng) và tắt cờ "
          "“icon in sẵn nhãn” để HUD tự vẽ phím, tên và trạng thái.", size=9.5)

# ------------------------------------------------------------------ 9
h1(doc, "9. Công cụ Editor (menu Tools ▸ Iron Wasteland)")
add_table(
    doc,
    ["Menu", "Chức năng"],
    [
        ["Generate Skill Icons", "Sinh bộ icon kỹ năng dạng thẻ (250×231) và glyph trong suốt cho 5 xe chưa có art, kèm phím, tên và dòng trạng thái."],
        ["Assign Skill Icons To Tanks", "Gán icon đã sinh vào các TankDefinition."],
        ["Integrate New Skill Icons", "Gán bộ icon art mới (thẻ vuông) cho cả 6 xe, tự cấu hình import."],
        ["Rebuild GameplayHub", "Dựng lại toàn bộ prefab StageHUD từ bộ art tách sẵn (panel, kỹ năng, minimap, kết quả, cài đặt, nameplate)."],
        ["Refresh HUD In Scenes", "Thay HUD trong cả 9 scene bằng bản mới và nối lại StageDirector/GameController. Phải chạy khi KHÔNG ở play mode."],
        ["Build Settings Panel", "Dựng popup Cài đặt (ESC) đúng chữ trong wireframe 05."],
        ["Setup Cameras", "Đặt orthographic size, Clear Flags và màu nền cho camera cả 9 scene."],
        ["Audit Minimap Scenes", "Kiểm tra từng màn có đủ MinimapUI, field, mask và in tên sprite nền."],
        ["QA Audit Stages", "Kiểm tra mỗi màn có mục tiêu, đợt quái, prefab hợp lệ, mã màn kế tiếp và scene có chứa asset."],
        ["Fix Stage QA", "Tự sửa các lỗi QA: bổ sung mục tiêu cho 1-1, nối lại chuỗi màn 1-2 → 1-3, 2-1 → 2-2, 2-3 → 2-4."],
        ["Fix Enemy Aggro", "Tăng radiusC lên 60 và bật autoSetAnchorDFromSpawnPosition cho 11 prefab quái để quái đuổi từ xa."],
        ["Localize Stage Text", "Ghi tên ải (9) và tên boss (4) tiếng Việt có dấu vào asset/scene."],
        ["Reskin UI", "Bản reskin đầu tiên theo bảng màu Figma (đã được HudBuilder thay thế ở các vòng sau)."],
    ],
    widths=[4.4, 11.6],
    font=8.6,
)

# ------------------------------------------------------------------ 10
h1(doc, "10. Lỗi đã tìm ra và cách sửa")
add_table(
    doc,
    ["Triệu chứng", "Nguyên nhân", "Cách sửa"],
    [
        ["Màn 1-1 kẹt ở 37/40 quái, không thể thắng", "Ký Sinh Thiên Thạch tự nổ rồi Destroy(gameObject) mà không đi qua Die() nên không bắn OnDeathEvent, director không đếm kill", "Đổi thành if (IsAlive) Die(); else Destroy(gameObject);"],
        ["Skill bấm không có tác dụng, không hồi năng lượng", "GameController.targetCamera bị null nên không SetLookTarget, FireNormalSkill thoát sớm", "Gán Main Camera vào GameController"],
        ["Tank sinh ra ở giữa hai boss rồi chết ngay", "spawnPosition của GameController là Vector2 nhưng bị ghi bằng vector3Value nên không lưu", "Ghi bằng vector2Value (0, -7)"],
        ["Xe hộ tống tới đích nhưng mục tiêu không hoàn thành", "Xe dừng cách waypoint cuối 0,15 m nên chỉ báo 29,9/30 m", "Chốt m_Travelled = m_PathLength khi tới đích và báo lại tiến độ"],
        ["Quái đứng yên, chỉ đuổi khi người chơi lại gần", "ScanAndValidateTarget dùng OverlapCircleAll quanh anchorPointD với radiusC mặc định 10", "Fix Enemy Aggro: radiusC = 60, autoSetAnchorDFromSpawnPosition = true cho 11 prefab"],
        ["Cảnh báo “objects were not cleaned up … [PathfindingGrid2D]”", "Singleton Instance tự tạo GameObject khi bị gọi trong lúc Unity đóng scene (từ EnemyMovementAgent.OnDisable)", "Instance trả null khi !Application.isPlaying; OnDisable dùng HasInstance"],
        ["Ký Sinh Thiên Thạch chết vẫn hiện tên", "Sau khi kích nổ, model bị ẩn nhưng GameObject còn sống nên IsAlive vẫn true", "Thêm ShowsNameplate (ảo) và override ở Ký Sinh: IsAlive && !hasDetonated"],
        ["Vùng ngoài bản đồ lộ màu xanh", "Camera để Clear Flags = Skybox, ortho 9 nên khung hình rộng hơn bản đồ", "SolidColor #070A0E và ortho 7"],
        ["Khung xanh trên minimap đứng yên", "Khung được vẽ sẵn trong ảnh lưới", "Sinh lưới riêng không có khung; thêm ViewFrame đọc camera thật"],
        ["Ô kỹ năng trắng trơn sau khi đổi art", "Bộ art gốc để spriteMode Multiple nên LoadAssetAtPath<Sprite> trả null", "ConfigureArtImporters ép Sprite Single + ForceSynchronousImport"],
        ["Chữ trên HUD bị đè hoặc nút rơi ra ngoài hộp", "Nút mặc định neo đáy (0.5, 0) và chữ dùng y dương trong hộp neo giữa", "Thêm tham số anchor cho hàm tạo nút; đổi toàn bộ chữ trong hộp sang y âm"],
        ["Icon kỹ năng bị vẽ hai lần", "GetGlyph fallback sang icon thẻ khi thiếu glyph", "Bỏ fallback và ẩn hẳn lớp glyph khi không có glyph"],
    ],
    widths=[4.4, 6.2, 5.4],
    font=8.3,
)

# ------------------------------------------------------------------ 11
h1(doc, "11. Việt hoá chữ hiển thị")
bullets(doc, [
    "HUD: Đợt i/n, Đã diệt, Ngọc, MÁU, NĂNG LƯỢNG, CẤP, TỐC ĐỘ, KỸ NĂNG TRANG BỊ, SẴN SÀNG / HỒI: x GIÂY.",
    "Mục tiêu: Quái x/y, Sống sót x/y giây, Mục tiêu x/y (id), Hộ tống xm/ym.",
    "Bảng kết quả: CHIẾN THẮNG / THẤT BẠI, Thời gian hoàn thành, Quái đã diệt, Máu còn lại, Ngọc Ma Pháp, Phụ kiện rơi.",
    "Popup cài đặt: CÀI ĐẶT TRẬN ĐẤU, CHÚ Ý: GAME KHÔNG TẠM DỪNG!, ÂM LƯỢNG TỔNG, ÂM THANH CHIẾN ĐẤU & MA PHÁP, HỆ THỐNG ĐIỀU KHIỂN & PHÍM BẤM.",
    "Tên ải và tên boss ghi bằng công cụ Localize Stage Text (9 ải, 4 boss).",
    "Icon kỹ năng dùng font bitmap 5×7 tự viết, đã bổ sung glyph có dấu (À, Ẵ, Ồ, Í, Ế, Đ) để in được “SẴN SÀNG”, “HỒI CHIẾU”.",
])
para(doc, "Chưa làm: 587 tooltip và 351 header trong Inspector của 213 file script vẫn đang viết không dấu — đây là phần "
          "hiển thị trong Unity Editor, không ảnh hưởng người chơi.", size=9.5, italic=True)

# ------------------------------------------------------------------ 12
h1(doc, "12. Kiểm chứng")
add_table(
    doc,
    ["Hạng mục", "Cách kiểm", "Kết quả"],
    [
        ["Biên dịch", "Đếm “error CS” trong Library/Bee/tundra.log.json sau mỗi lần sửa", "0 error ở tất cả các bước"],
        ["QA 9 màn", "Tools ▸ QA Audit Stages (mục tiêu, đợt quái, prefab, mã màn kế, scene chứa asset)", "PASS 9/9, chuỗi 1-1 → 2-5 liên tục"],
        ["Bàn đồ chiến thuật", "Tools ▸ Audit Minimap Scenes", "PASS 9/9, nền đúng cho từng vùng"],
        ["Khung nhìn minimap", "Đọc anchor của ViewFrame khi tank ở tâm và ở góc", "Tâm: (0.50, 0.50) — góc: (0.67, 0.67)"],
        ["Giới hạn biên", "Đẩy tank tới (40, -30)", "Bị kẹp về (17,21, -8,90) đúng biên trừ lề 1,6"],
        ["Quái đuổi từ xa", "Đặt tank cách vùng sinh hơn 20 unit, đọc trạng thái AI", "12/12 quái chuyển sang Chasing, dHome ≈ 9,5"],
        ["Nameplate Ký Sinh", "Sinh quái đầy máu, kích nổ, poll mỗi 0,2 giây", "t = 0,40 s: alive = True, shows = False, entry ẩn"],
        ["Cảnh báo dọn scene", "Vào play rồi thoát, đọc log sau mốc", "Không còn cảnh báo PathfindingGrid2D"],
    ],
    widths=[3.6, 6.4, 6.0],
    font=8.6,
)
para(doc, "Ảnh minh chứng được lưu trong thư mục docs/screenshots (34 ảnh), gồm HUD từng vòng, bảng kết quả, popup cài đặt, "
          "màn chọn ải, bản đồ chiến thuật và các màn boss.", size=9.5)

# ------------------------------------------------------------------ 13
h1(doc, "13. Trạng thái Git")
add_table(
    doc,
    ["Hạng mục", "Giá trị"],
    [
        ["Nhánh", "Stage (tách từ main tại e82ee94)"],
        ["Commit 1", "8b4e0fc — Hoan thanh 9 man choi + Spawner quai + SFX + HUD theo thiet ke (643 file, +61.904 / −1.655 dòng)"],
        ["Commit 2", "e79776e — Sua canh bao “objects were not cleaned up” ([PathfindingGrid2D]) (3 file)"],
        ["Remote", "origin/Stage → 8b4e0fc…e79776e (https://github.com/fasysd/IronWasteland.git)"],
        ["Chưa đẩy", "Toàn bộ thay đổi sau thời điểm đó: nameplate Ký Sinh, bộ Skill Icon mới, HUD Card 250×231, 6 TankDefinition, 9 scene, 24 PNG mới"],
    ],
    widths=[3.4, 12.6],
    font=8.8,
)
para(doc, "Theo yêu cầu ngày 18/11/2026, từ thời điểm đó không commit/push và không chạy kiểm tra gameplay cho tới khi có "
          "yêu cầu rõ ràng; các thay đổi nêu trên đang nằm trong working tree.", size=9.5, italic=True)

# ------------------------------------------------------------------ 14
h1(doc, "14. Việc còn lại và khuyến nghị")
numbered(doc, [
    "Đẩy các thay đổi đang chờ lên nhánh Stage khi được phép (nameplate Ký Sinh, bộ icon mới, HUD Card).",
    "Cân nhắc đổi preserveAspect = true cho ô kỹ năng để giữ đúng tỉ lệ vuông của art mới (hiện bị nén dọc khoảng 8%).",
    "Nhãn tên quái vẫn chồng thành cột khi nhiều quái dồn quanh tank — cần thuật toán xếp hàng ngang thay vì chỉ đẩy lên.",
    "Co-op mới là khung giao diện, chưa có logic nhiều người chơi.",
    "Bảng kết quả chưa hiển thị ảnh damage popup vì bộ art có sẵn số in chìm; nếu muốn dùng cần sinh số động.",
    "Chân dung người chơi đang dùng chung art Black Shadow cho cả 6 xe.",
    "587 tooltip và 351 header trong Inspector vẫn không dấu.",
    "Có thể xoá bộ icon sinh tự động cũ trong Assets/Art/UI/GameplayHUD/SkillIcons/<Tank>/ vì đã thay bằng bộ art mới.",
])

# ------------------------------------------------------------------ appendix
doc.add_page_break()
h1(doc, "Phụ lục A — Danh mục file chính")
add_table(
    doc,
    ["Nhóm", "Đường dẫn"],
    [
        ["Dữ liệu màn", "Assets/Data/Stages/Stage_1_1.asset … Stage_2_5.asset (9 file)"],
        ["Scene màn chơi", "Assets/Scenes/Stage_1_1.unity … Stage_2_5.unity (9 file) + StageSelect.unity"],
        ["Framework", "Assets/Scripts/Stages/ — StageDefinition, StageObjective, EnemySpawner, SpawnZone, StageDirector, StageReward, StageProgress, DestructibleTarget, EscortVehicle, RadarStation, BossTag (11 file)"],
        ["Camera / biên", "Assets/Scripts/Core/ — CameraFollow2D, StageBounds"],
        ["HUD", "Assets/Scripts/UI/ — StageHUD, StageSelectUI, TankStatusHUD, MinimapUI, UnitNameplates, UISprites"],
        ["Âm thanh", "Assets/Scripts/Audio/ — SfxCategory, SfxLibrary, AudioManager, TankSfxHooks, EnemySfxHooks, StageSfxHooks; dữ liệu: Assets/Audio/SFX/ (39 WAV) + SfxLibrary.asset"],
        ["Prefab HUD", "Assets/Prefabs/UI/StageHUD.prefab"],
        ["Bộ art", "Assets/Art/UI/GameplayHUD/ · Assets/Art/Backgrounds/ · Assets/Art/Tank/Sprites/ · Assets/Art/UI/Generated/"],
        ["Công cụ Editor", "Assets/Editor/ — HudBuilder, HudSceneRefresh, MinimapAudit, StageQAAudit, StageQAFix, StageCameraSetup, SettingsPanelBuilder, EnemyAggroFix, StageTextLocalize, SkillIconGenerator, SkillIconIntegrator, UITheme, UIReskin"],
        ["Tài liệu", "docs/PLAN_Stages_Spawner_SFX.md (1.225 dòng, 19 mục) · docs/HUONG_DAN_StageFramework.md"],
        ["Ảnh minh chứng", "docs/screenshots/** (34 PNG)"],
    ],
    widths=[3.2, 12.8],
    font=8.4,
)

h1(doc, "Phụ lục B — Cách vận hành nhanh")
bullets(doc, [
    "Dựng lại HUD sau khi sửa: Tools ▸ Iron Wasteland ▸ Rebuild GameplayHub, sau đó Refresh HUD In Scenes (phải tắt play mode).",
    "Đổi bộ icon kỹ năng: Integrate New Skill Icons → Rebuild GameplayHub → Refresh HUD In Scenes.",
    "Kiểm tra trước khi phát hành: QA Audit Stages và Audit Minimap Scenes, cả hai phải PASS 9/9.",
    "Camera và nền: Setup Cameras (ghi orthographic size 7, SolidColor #070A0E cho cả 9 scene).",
    "Quái không đuổi: Fix Enemy Aggro (radiusC = 60).",
])

doc.save(OUT)

size = os.path.getsize(OUT)
print("saved:", OUT)
print("size KB:", int(size / 1024))

check = Document(OUT)
print("paragraphs:", len(check.paragraphs))
print("tables:", len(check.tables))
print("inline shapes:", len(check.inline_shapes))
