with open('Assets/Art/Enemies/BongMaTuDia/Animations/BongMaTuDia_Idle.anim', 'r', encoding='utf-8') as f:
    text = f.read()

import re
pos_block = re.search(r'm_PositionCurves:\s*(.*?)(?=m_ScaleCurves:)', text, re.DOTALL)
if pos_block:
    for curve in re.findall(r'- curve:\s*(.*?)(?=- curve:|\Z)', pos_block.group(1), re.DOTALL):
        path_m = re.search(r'path:\s*([^\r\n]+)', curve)
        path = path_m.group(1) if path_m else ""
        print("Path in position curve:", path)
        for val in re.findall(r'time:\s*([^,\r\n]+).*?value:\s*\{x:\s*([^,]+),\s*y:\s*([^,]+),\s*z:\s*([^\}]+)\}', curve, re.DOTALL):
            print(f"  time: {val[0].strip()} -> ({val[1].strip()}, {val[2].strip()}, {val[3].strip()})")
