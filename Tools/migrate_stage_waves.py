"""Chuyen thiet ke dot quai tu StageDefinition asset sang component EnemySpawner trong scene.

Vi sao viet script thay vi sua tay 9 scene:
- Khoi YAML cua wave giong het nhau vi EnemyWave/EnemyWaveSpawn giu dung ten field cu
  (Label/StartTime/Spawns/Enemy/Count/SpeedMultiplier/ZoneId), nen chi can copy nguyen khoi.
- SpawnZone cung duoc chuyen tu StageDirector sang EnemySpawner cung luc.
- Them component StageObstacleField (sinh vat can) vao object EnemySpawner.

Chay: python Tools/migrate_stage_waves.py            (ghi file)
      python Tools/migrate_stage_waves.py --dry-run  (chi in)
"""
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

GUID_DIRECTOR = "728b14d6ac882544d856bef5284d8482"
GUID_SPAWNER = "c47b433e2faf30d4c8f2e2934282b609"
GUID_OBSTACLES = "d4a7f0a52e6b4d9f1a8c3e5b0f2d7a46"

WAVE_FIELD = re.compile(r"^  waves:")
SPAWN_ZONES_FIELD = re.compile(r"^  spawnZones:")
TOP_FIELD = re.compile(r"^  [A-Za-z_]")


def read(path):
    with open(path, "r", encoding="utf-8") as handle:
        return handle.read()


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)


def split_blocks(text):
    """Tach scene thanh cac block '--- !u!<class> &<id>' giu nguyen thu tu."""
    lines = text.split("\n")
    blocks = []
    current = []
    for line in lines:
        if line.startswith("--- "):
            if current:
                blocks.append(current)
            current = [line]
        else:
            current.append(line)
    if current:
        blocks.append(current)
    return blocks


def block_text(block):
    return "\n".join(block)


def field_block(lines, pattern):
    """Lay cac dong cua 1 field YAML (tu dong khop toi truoc field cap 2 ke tiep)."""
    for index, line in enumerate(lines):
        if pattern.match(line):
            out = [line]
            for follow in lines[index + 1:]:
                if TOP_FIELD.match(follow) or follow.startswith("--- "):
                    break
                out.append(follow)
            return out
    return []


def replace_field(lines, pattern, replacement):
    """Thay (hoac chen cuoi block) 1 field YAML."""
    for index, line in enumerate(lines):
        if pattern.match(line):
            end = index + 1
            for follow in lines[index + 1:]:
                if TOP_FIELD.match(follow) or follow.startswith("--- "):
                    break
                end += 1
            return lines[:index] + replacement + lines[end:]

    # Chua co field: chen truoc dong cuoi cung cua block.
    insert_at = len(lines) - 1
    while insert_at > 0 and lines[insert_at].strip() == "":
        insert_at -= 1
    return lines[:insert_at + 1] + replacement + lines[insert_at + 1:]


def asset_map():
    mapping = {}
    for meta in glob.glob(os.path.join(ROOT, "Assets/Data/Stages/*.asset.meta")):
        with open(meta, "r", encoding="utf-8") as handle:
            guid = re.search(r"guid: ([a-f0-9]+)", handle.read()).group(1)
        mapping[guid] = meta[:-5]
    return mapping


def cleanup_asset(asset_path):
    """Bo khoi waves khoi asset (wave gio thuoc scene)."""
    text = read(asset_path)
    lines = text.split("\n")
    out = replace_field(lines, WAVE_FIELD, [])
    return "\n".join(out)


def migrate_scene(scene_path, assets, dry_run):
    file_name = os.path.basename(scene_path)
    stage_code = file_name.replace("Stage_", "").replace(".unity", "").replace("_", "-")

    blocks = split_blocks(read(scene_path))

    director = None
    spawner = None
    director_index = -1
    spawner_index = -1
    for index, block in enumerate(blocks):
        text = block_text(block)
        if GUID_DIRECTOR in text:
            director = block
            director_index = index
        elif GUID_SPAWNER in text:
            spawner = block
            spawner_index = index

    if director is None or spawner is None:
        print(f"  BO QUA {file_name}: thieu director hoac spawner")
        return False

    director_text = block_text(director)
    definition = re.search(r"definition: \{fileID: 11400000, guid: ([a-f0-9]+)", director_text)
    if not definition:
        print(f"  BO QUA {file_name}: director chua gan StageDefinition")
        return False

    asset_path = assets.get(definition.group(1))
    if not asset_path:
        print(f"  BO QUA {file_name}: khong tim thay asset cho guid {definition.group(1)}")
        return False

    asset_lines = read(asset_path).split("\n")
    code_match = re.search(r"^  stageCode: (.+)$", read(asset_path), re.MULTILINE)
    if code_match:
        stage_code = code_match.group(1).strip()

    waves = field_block(asset_lines, WAVE_FIELD)
    if not waves:
        # Asset da duoc don (hoac man moi chua co wave): giu nguyen wave dang co
        # tren spawner thay vi ghi de bang list rong.
        existing = field_block(spawner, WAVE_FIELD)
        waves = existing if existing else ["  waves: []"]

    zones = field_block(director, SPAWN_ZONES_FIELD)
    if not zones:
        zones = ["  spawnZones: []"]

    spawner = replace_field(spawner, WAVE_FIELD, waves)
    spawner = replace_field(spawner, SPAWN_ZONES_FIELD, zones)
    blocks[spawner_index] = spawner

    # StageDirector khong con field spawnZones -> bo khoi YAML cho sach.
    director = replace_field(director, SPAWN_ZONES_FIELD, [])
    blocks[director_index] = director

    # --- Them component StageObstacleField vao cung GameObject voi spawner ---
    # (bo qua neu scene da co component nay, de chay lai script khong bi nhan doi)
    if GUID_OBSTACLES in read(scene_path):
        print(f"  OK {file_name}: {stage_code} | waves+{len(waves) - 1} dong | {len(zones) - 1} zone | da co vat can")
        if not dry_run:
            write(scene_path, "\n".join(block_text(block).rstrip("\n") for block in blocks if block_text(block).strip()) + "\n")
            write(asset_path, cleanup_asset(asset_path))
        return True

    spawner_go = re.search(r"m_GameObject: \{fileID: (\d+)\}", block_text(spawner))
    if not spawner_go:
        print(f"  BO QUA {file_name}: spawner khong co m_GameObject")
        return False
    go_id = spawner_go.group(1)

    all_ids = [int(match) for match in re.findall(r"&(\d+)", read(scene_path))]
    new_id = 880000000
    while new_id in all_ids:
        new_id += 1

    obstacle_count = 5 + (int(stage_code.split("-")[1]) % 3)

    obstacle_component = [
        f"--- !u!114 &{new_id}",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {{fileID: {go_id}}}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {GUID_OBSTACLES}, type: 3}}",
        "  m_Name: ",
        "  m_EditorClassIdentifier: ",
        f"  stageCode: {stage_code}",
        f"  obstacleCount: {obstacle_count}",
        "  obstacleColor: {r: 0.26, g: 0.27, b: 0.3, a: 1}",
        "  logBuild: 1",
        "",
    ]

    for index, block in enumerate(blocks):
        if block[0].startswith(f"--- !u!1 &{go_id}"):
            # Them component vao GameObject nay.
            comp_index = None
            for line_index, line in enumerate(block):
                if line.strip() == "- component:" or line.strip().startswith("- component: {"):
                    comp_index = line_index
            if comp_index is None:
                print(f"  BO QUA {file_name}: GameObject {go_id} khong co m_Component")
                return False
            block.insert(comp_index + 1, f"  - component: {{fileID: {new_id}}}")
            break

    # Chen block component moi truoc SceneRoots neu co.
    insert_at = len(blocks)
    for index, block in enumerate(blocks):
        if block[0].startswith("--- !u!1660057539"):
            insert_at = index
            break
    blocks.insert(insert_at, obstacle_component)

    new_text = "\n".join(block_text(block).rstrip("\n") for block in blocks if block_text(block).strip()) + "\n"

    if not dry_run:
        write(scene_path, new_text)
        write(asset_path, cleanup_asset(asset_path))

    print(f"  OK {file_name}: {stage_code} | {len(waves) - 1} dong wave | {len(zones) - 1} zone | {obstacle_count} vat can")
    return True


def main():
    dry_run = "--dry-run" in sys.argv
    assets = asset_map()
    scenes = sorted(glob.glob(os.path.join(ROOT, "Assets/Scenes/Stage_*.unity")))
    scenes = [scene for scene in scenes if "StageSelect" not in scene]

    print(f"{'DRY-RUN' if dry_run else 'GHI FILE'}: {len(scenes)} scene")
    ok = 0
    for scene in scenes:
        if migrate_scene(scene, assets, dry_run):
            ok += 1

    print(f"Xong: {ok}/{len(scenes)} scene")


if __name__ == "__main__":
    main()
