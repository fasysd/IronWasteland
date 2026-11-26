#!/usr/bin/env python3
"""Chuyển một tài liệu Markdown trong repo thành trang HTML dùng ../assets/course.css.

Chỉ hỗ trợ tập con Markdown mà thư mục docs/ đang dùng, cố tình không cài thêm
thư viện: heading 1-4, đoạn văn, danh sách lồng nhau, bảng, khối mã có nhãn
ngôn ngữ, trích dẫn, đường kẻ ngang, và inline: `code`, **đậm**, *nghiêng*, [link](url).

Ví dụ:
    python docs/teach/tools/md2html.py ^
        --input docs/UNITY_CO_BAN.md ^
        --output docs/teach/reference/unity-co-ban.html ^
        --title "Unity cơ bản cho Iron Wasteland" ^
        --eyebrow "Tài liệu · Unity cơ bản" ^
        --meta "Nguồn: docs/UNITY_CO_BAN.md · bản đầy đủ" ^
        --link-prefix ../../../ ^
        --back-href ../lessons/0001-ban-do-iron-wasteland.html ^
        --back-label "Bài 01 — Iron Wasteland trong 10 phút"
"""

import argparse
import html
import re
import unicodedata
import urllib.parse

LIST_MARKER = re.compile(r"^(\s*)([-*+]|\d+[.)])\s+(.*)$")
HEADING = re.compile(r"^(#{1,6})\s+(.*)$")
HRULE = re.compile(r"^(-{3,}|\*{3,}|_{3,})$")
TABLE_SEP = re.compile(r"^\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?$")


# --------------------------------------------------------------------------- #
# tiện ích
# --------------------------------------------------------------------------- #

def slugify(text, seen):
    plain = re.sub(r"<[^>]+>", "", text)
    plain = re.sub(r"`", "", plain)
    ascii_text = unicodedata.normalize("NFD", plain)
    ascii_text = "".join(c for c in ascii_text if unicodedata.category(c) != "Mn")
    ascii_text = ascii_text.replace("đ", "d").replace("Đ", "D")
    slug = re.sub(r"[^a-z0-9]+", "-", ascii_text.lower()).strip("-")
    slug = slug or "muc"
    count = seen.get(slug, 0) + 1
    seen[slug] = count
    return slug if count == 1 else "%s-%d" % (slug, count)


def split_row(line):
    text = line.strip()
    if text.startswith("|"):
        text = text[1:]
    if text.endswith("|"):
        text = text[:-1]
    return [cell.strip() for cell in text.split("|")]


def row_aligns(sep_line):
    aligns = []
    for cell in split_row(sep_line):
        left = cell.startswith(":")
        right = cell.endswith(":")
        if left and right:
            aligns.append("center")
        elif right:
            aligns.append("right")
        else:
            aligns.append("left")
    return aligns


# --------------------------------------------------------------------------- #
# inline
# --------------------------------------------------------------------------- #

def make_inline(link_prefix):
    def rewrite_url(url):
        match = re.match(r"^file:///[a-zA-Z]:/.*?IronWasteland/(.*)$", url, re.IGNORECASE)
        if match:
            rest = match.group(1).replace("\\", "/")
            return link_prefix + urllib.parse.quote(rest), ""
        if url.startswith("#"):
            # mục lục nội bộ của tài liệu: id trong trang là dạng bỏ dấu
            return "#" + slugify(urllib.parse.unquote(url[1:]), {}), ""
        if url.startswith("http://") or url.startswith("https://"):
            return url, ' target="_blank" rel="noopener"'
        return url, ""

    def inline(text):
        codes = []

        def stash(match):
            codes.append(match.group(1))
            return "\x00%d\x00" % (len(codes) - 1)

        text = re.sub(r"`([^`]+)`", stash, text)
        text = html.escape(text, quote=False)

        def link(match):
            label, url = match.group(1), match.group(2)
            href, extra = rewrite_url(url)
            return '<a href="%s"%s>%s</a>' % (html.escape(href, quote=True), extra, label)

        text = re.sub(r"\[([^\]]+)\]\(([^)\s]+)\)", link, text)
        text = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", text)
        text = re.sub(r"(?<![*\w])\*([^*\n]+)\*(?!\*)", r"<em>\1</em>", text)
        # công thức nằm giữa dòng (thường trong một gạch đầu dòng) -> span
        text = re.sub(MATH_MARK + "BLOCK" + MATH_MARK + r"([^\x01]*)",
                      lambda m: '<span class="math">%s</span>' % m.group(1).strip(), text)
        text = text.replace(MATH_MARK, "").replace(MATH_INLINE_MARK, "")

        def restore(match):
            return "<code>%s</code>" % html.escape(codes[int(match.group(1))], quote=False)

        return re.sub("\x00(\\d+)\x00", restore, text)

    return inline


# --------------------------------------------------------------------------- #
# LaTeX đơn giản trong tài liệu (GitHub render bằng MathJax, HTML thường thì không)
# --------------------------------------------------------------------------- #

LATEX_SYMBOLS = {
    "times": "×", "cdot": "·", "pm": "±", "infty": "∞",
    "rightarrow": "→", "Rightarrow": "⇒", "leftarrow": "←", "to": "→",
    "le": "≤", "leq": "≤", "ge": "≥", "geq": "≥",
    "dots": "…", "ldots": "…", "in": "∈", "approx": "≈", "neq": "≠", "neq": "≠",
    "ln": "ln", "left": "", "right": "",
}

MATH_MARK = "\x01"
MATH_INLINE_MARK = "\x02"


def _match_brace(text, start):
    """start trỏ vào dấu '{'; trả về (nội dung, vị trí sau dấu '}')."""
    depth = 0
    for index in range(start, len(text)):
        if text[index] == "{":
            depth += 1
        elif text[index] == "}":
            depth -= 1
            if depth == 0:
                return text[start + 1:index], index + 1
    return text[start + 1:], len(text)


def replace_frac(text):
    """\\frac{A}{B} -> A / B, thêm ngoặc khi tử/mẫu không phải một hạng tử đơn."""
    out = []
    index = 0
    while index < len(text):
        if text.startswith(r"\frac", index):
            cursor = index + 5
            while cursor < len(text) and text[cursor] == " ":
                cursor += 1
            if cursor < len(text) and text[cursor] == "{":
                numerator, after_num = _match_brace(text, cursor)
                cursor = after_num
                while cursor < len(text) and text[cursor] == " ":
                    cursor += 1
                if cursor < len(text) and text[cursor] == "{":
                    denominator, after_den = _match_brace(text, cursor)
                    numerator = latex_to_text(numerator)
                    denominator = latex_to_text(denominator)
                    simple = re.compile(r"[\w.%]+")
                    left = numerator if simple.fullmatch(numerator) else "(%s)" % numerator
                    right = denominator if simple.fullmatch(denominator) else "(%s)" % denominator
                    out.append("%s / %s" % (left, right))
                    index = after_den
                    continue
        out.append(text[index])
        index += 1
    return "".join(out)


def latex_to_text(formula):
    text = re.sub(r"\\(?:text|mathrm|mathbf|textit)\s*\{([^{}]*)\}", r"\1", formula)
    text = replace_frac(text)

    def command(match):
        name = match.group(1)
        return LATEX_SYMBOLS.get(name, name)

    text = re.sub(r"\\([a-zA-Z]+)", command, text)
    text = text.replace(r"\%", "%").replace(r"\,", " ").replace(r"\;", " ").replace(r"\!", "")
    text = re.sub(r"[{}]", "", text)
    return re.sub(r"\s+", " ", text).strip()


def preprocess_math(lines):
    text = "\n".join(lines)

    def block(match):
        return MATH_MARK + "BLOCK" + MATH_MARK + latex_to_text(match.group(1)) + "\n"

    text = re.sub(r"\$\$(.+?)\$\$", block, text, flags=re.DOTALL)
    text = re.sub(r"(?<!\$)\$([^$\n]+)\$(?!\$)", lambda m: MATH_INLINE_MARK + latex_to_text(m.group(1)) + MATH_INLINE_MARK, text)
    return text.split("\n")


# --------------------------------------------------------------------------- #
# block parsing
# --------------------------------------------------------------------------- #

def parse_list(lines, start):
    total = len(lines)
    nodes = []
    index = start

    while index < total:
        raw = lines[index]
        stripped = raw.strip()

        if not stripped:
            look = index + 1
            while look < total and not lines[look].strip():
                look += 1
            if look < total and LIST_MARKER.match(lines[look]):
                index = look
                continue
            break

        marker = LIST_MARKER.match(raw)
        if marker:
            indent = len(marker.group(1).expandtabs(4))
            ordered = marker.group(2)[0].isdigit()
            text = marker.group(3)

            while len(nodes) > 1 and indent < nodes[-1]["indent"]:
                nodes.pop()

            if not nodes:
                nodes.append({"indent": indent, "ordered": ordered, "items": []})

            top = nodes[-1]
            if indent > top["indent"] and top["items"]:
                child = {"indent": indent, "ordered": ordered, "items": []}
                top["items"][-1][1].append(child)
                nodes.append(child)
            elif indent == top["indent"] and ordered != top["ordered"]:
                node = {"indent": indent, "ordered": ordered, "items": []}
                if len(nodes) > 1:
                    nodes.pop()
                    nodes[-1]["items"][-1][1].append(node)
                else:
                    nodes[0] = node
                nodes.append(node)
            nodes[-1]["items"].append([text, []])
        else:
            if nodes and nodes[-1]["items"]:
                nodes[-1]["items"][-1][0] += " " + stripped
            else:
                break

        index += 1

    return nodes[0], index


def parse_blocks(lines):
    blocks = []
    index = 0
    total = len(lines)

    while index < total:
        raw = lines[index]
        stripped = raw.strip()

        if not stripped:
            index += 1
            continue

        if stripped.startswith(MATH_MARK + "BLOCK" + MATH_MARK):
            blocks.append(("math", stripped[len(MATH_MARK + "BLOCK" + MATH_MARK):]))
            index += 1
            continue

        if stripped.startswith("```"):
            language = stripped[3:].strip()
            index += 1
            code = []
            while index < total and not lines[index].strip().startswith("```"):
                code.append(lines[index])
                index += 1
            index += 1
            blocks.append(("code", language, code))
            continue

        heading = HEADING.match(stripped)
        if heading:
            blocks.append(("heading", len(heading.group(1)), heading.group(2).strip()))
            index += 1
            continue

        if HRULE.match(stripped):
            blocks.append(("hr",))
            index += 1
            continue

        if stripped.startswith("|") and index + 1 < total and TABLE_SEP.match(lines[index + 1].strip()):
            header = split_row(stripped)
            aligns = row_aligns(lines[index + 1].strip())
            index += 2
            rows = []
            while index < total and lines[index].strip().startswith("|"):
                rows.append(split_row(lines[index].strip()))
                index += 1
            blocks.append(("table", header, aligns, rows))
            continue

        if stripped.startswith(">"):
            quoted = []
            while index < total and lines[index].strip().startswith(">"):
                quoted.append(re.sub(r"^>\s?", "", lines[index].strip()))
                index += 1
            blocks.append(("quote", quoted))
            continue

        if LIST_MARKER.match(raw):
            node, index = parse_list(lines, index)
            blocks.append(("list", node))
            continue

        paragraph = []
        while index < total:
            current = lines[index]
            content = current.strip()
            if not content:
                break
            if (content.startswith("```") or HEADING.match(content) or content.startswith("|")
                    or content.startswith(">") or LIST_MARKER.match(current) or HRULE.match(content)
                    or content.startswith(MATH_MARK)):
                break
            paragraph.append(content)
            index += 1
        blocks.append(("para", " ".join(paragraph)))

    return blocks


# --------------------------------------------------------------------------- #
# render
# --------------------------------------------------------------------------- #

def render_list(node, inline):
    tag = "ol" if node["ordered"] else "ul"
    parts = ["<%s>" % tag]
    for text, children in node["items"]:
        parts.append("<li>%s%s</li>" % (inline(text), "".join(render_list(c, inline) for c in children)))
    parts.append("</%s>" % tag)
    return "".join(parts)


def render_blocks(blocks, inline, seen, toc):
    out = []
    for block in blocks:
        kind = block[0]
        if kind == "heading":
            level = block[1]
            text = inline(block[2])
            anchor = slugify(block[2], seen)
            out.append('<h%d id="%s">%s</h%d>' % (level, anchor, text, level))
            if level == 2:
                toc.append((anchor, re.sub(r"<[^>]+>", "", text)))
        elif kind == "para":
            out.append("<p>%s</p>" % inline(block[1]))
        elif kind == "code":
            language = block[1]
            code = html.escape("\n".join(block[2]), quote=False)
            attr = ' class="language-%s"' % language if language else ""
            out.append("<pre><code%s>%s</code></pre>" % (attr, code))
        elif kind == "quote":
            inner = "".join("<p>%s</p>" % inline(line) for line in block[1])
            out.append("<blockquote>%s</blockquote>" % inner)
        elif kind == "hr":
            out.append("<hr>")
        elif kind == "math":
            out.append('<div class="math-block">%s</div>' % html.escape(block[1], quote=False))
        elif kind == "list":
            out.append(render_list(block[1], inline))
        elif kind == "table":
            header, aligns, rows = block[1], block[2], block[3]
            head = "".join(
                '<th%s>%s</th>' % (' style="text-align:%s"' % aligns[i] if i < len(aligns) else "", inline(cell))
                for i, cell in enumerate(header)
            )
            body = []
            for row in rows:
                cells = "".join(
                    "<td%s>%s</td>" % (' style="text-align:%s"' % aligns[i] if i < len(aligns) else "", inline(cell))
                    for i, cell in enumerate(row)
                )
                body.append("<tr>%s</tr>" % cells)
            out.append('<div class="table-wrap"><table><thead><tr>%s</tr></thead><tbody>%s</tbody></table></div>'
                       % (head, "".join(body)))
    return "\n".join(out)


def build_toc(toc):
    if not toc:
        return ""
    items = "".join('<li><a href="#%s">%s</a></li>' % (anchor, text) for anchor, text in toc)
    return '<nav class="toc" aria-label="Mục lục"><span class="toc__label">Mục lục</span><ol>%s</ol></nav>' % items


def has_own_toc(blocks):
    for block in blocks:
        if block[0] == "heading" and block[1] == 2:
            plain = unicodedata.normalize("NFD", block[2])
            plain = "".join(c for c in plain if unicodedata.category(c) != "Mn").lower()
            if plain.strip(" :-") in ("muc luc", "table of contents"):
                return True
    return False


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--title", required=True)
    parser.add_argument("--eyebrow", default="")
    parser.add_argument("--meta", default="")
    parser.add_argument("--link-prefix", default="../../../")
    parser.add_argument("--back-href", default="")
    parser.add_argument("--back-label", default="")
    parser.add_argument("--related", default="", help='danh sách "href|nhãn" cách nhau bằng ";"')
    parser.add_argument("--toc", default="auto", choices=["auto", "yes", "no"],
                        help="auto: bỏ mục lục tự sinh nếu tài liệu đã có mục 'Mục lục'")
    args = parser.parse_args()

    with open(args.input, encoding="utf-8") as handle:
        lines = handle.read().replace("\r\n", "\n").split("\n")

    lines = preprocess_math(lines)
    blocks = parse_blocks(lines)
    inline = make_inline(args.link_prefix)

    title_html = ""
    if blocks and blocks[0][0] == "heading" and blocks[0][1] == 1:
        title_html = '<h1>%s</h1>' % inline(blocks[0][2])
        blocks = blocks[1:]

    seen = {}
    toc = []
    body = render_blocks(blocks, inline, seen, toc)

    if args.toc == "no" or (args.toc == "auto" and has_own_toc(blocks)):
        toc = []

    crumbs = []
    if args.back_href:
        crumbs.append('<a href="%s">%s</a>' % (args.back_href, args.back_label))
    related = [item for item in args.related.split(";") if item.strip()]
    for item in related:
        href, label = item.split("|", 1)
        crumbs.append('<a href="%s">%s</a>' % (href.strip(), label.strip()))
    crumbs_html = " · ".join(crumbs)

    page = """<!DOCTYPE html>
<html lang="vi">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
<link rel="stylesheet" href="../assets/course.css">
</head>
<body>
<main class="page">
  <p class="crumbs">{crumbs}</p>
  <p class="eyebrow">{eyebrow}</p>
  {h1}
  <p class="meta">{meta}</p>
  {toc}
  <article class="doc">
{body}
  </article>
  <div class="nav">
    <p class="small">Bản gốc Markdown: <span class="file">{source}</span> — trang này chỉ trình bày lại, nội dung giữ nguyên.</p>
    <p class="small">Xem thêm: <a href="../lessons/0001-ban-do-iron-wasteland.html">Bài 01 — Iron Wasteland trong 10 phút</a> · <a href="thuat-ngu.html">Từ điển thuật ngữ</a> · <a href="ban-do-code.html">Bản đồ code</a></p>
  </div>
</main>
</body>
</html>
""".format(
        title=html.escape(args.title),
        crumbs=crumbs_html,
        eyebrow=args.eyebrow,
        h1=title_html,
        meta=args.meta,
        toc=build_toc(toc),
        body=body,
        source=args.input.replace("\\", "/"),
    )

    with open(args.output, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(page)

    print("wrote %s (%d chars, %d toc entries)" % (args.output, len(page), len(toc)))


if __name__ == "__main__":
    main()
