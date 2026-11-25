import re

with open('Assets/Prefabs/Enemies/BongMaTuDia.prefab', 'r', encoding='utf-8') as f:
    text = f.read()

objects = {}
current_id = None
current_type = None
current_lines = []

for line in text.splitlines():
    m = re.match(r'^--- !u!(\d+) &(-?\d+)', line)
    if m:
        if current_id:
            objects[current_id] = (current_type, current_lines)
        current_type = int(m.group(1))
        current_id = m.group(2)
        current_lines = []
    else:
        current_lines.append(line)
if current_id:
    objects[current_id] = (current_type, current_lines)

gameobjects = {}
transforms = {}

for oid, (otype, lines) in objects.items():
    if otype == 1:
        name = ''
        for l in lines:
            m_name = re.match(r'^\s*m_Name:\s*\"?(.*?)\"?\s*$', l)
            if m_name:
                raw = m_name.group(1)
                try: name = raw.encode().decode('unicode-escape')
                except: name = raw
        gameobjects[oid] = name
    elif otype == 4:
        go = father = None
        pos = rot = scale = rot_hint = ""
        children = []
        in_ch = False
        for l in lines:
            if 'm_GameObject:' in l: go = re.search(r'\{fileID:\s*(-?\d+)\}', l).group(1)
            if 'm_Father:' in l: father = re.search(r'\{fileID:\s*(-?\d+)\}', l).group(1)
            if 'm_LocalPosition:' in l: pos = l.strip()
            if 'm_LocalRotation:' in l: rot = l.strip()
            if 'm_LocalEulerAnglesHint:' in l: rot_hint = l.strip()
            if 'm_LocalScale:' in l: scale = l.strip()
            if 'm_Children:' in l: in_ch = True; continue
            if in_ch:
                m_c = re.search(r'-\s*\{fileID:\s*(-?\d+)\}', l)
                if m_c: children.append(m_c.group(1))
                elif not l.startswith(' '): in_ch = False
        transforms[oid] = {'go': go, 'father': father, 'pos': pos, 'rot_hint': rot_hint, 'scale': scale, 'children': children}

def dump_tree(tid, path=""):
    t = transforms[tid]
    go_id = t['go']
    name = gameobjects.get(go_id, 'Unknown')
    full_path = f"{path}/{name}" if path else name
    print(f"Path: {full_path}")
    print(f"  {t['pos']}")
    print(f"  {t['rot_hint']}")
    print(f"  {t['scale']}")
    for cid in t['children']:
        dump_tree(cid, full_path)

roots = [tid for tid, data in transforms.items() if data['father'] in (None, '0')]
for r in roots:
    dump_tree(r)
