#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using IronWasteland.Tanks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gan bo Skill Icon MOI (anh the art day du 512x512 / 1024x1024) vao 4 slot ky nang cua tung xe.
///
/// Bo art moi nam o Assets/Art/Tank/Sprites/{GFSkill,WBSKILL,WallTitan,RedNova,skill/leviathan,skill/blackShadow}.
/// Moi anh da la mot the hoan chinh (khung + bieu tuong) nen:
///   - skillIcons[i]      = anh the (Icon trong o ky nang)
///   - skillGlyphs        = rong  -> HUD an lop glyph de khong ve trung hinh
///   - skillIconsCooldown = rong  -> HUD dung anh ready + lop phu toi khi hoi chieu
///   - skillIconsIncludeLabels = false (art moi khong in san nhan phim / ten ky nang)
///
/// Chay: Tools > Iron Wasteland > Integrate New Skill Icons.
/// </summary>
public static class SkillIconIntegrator
{
    private const string ArtRoot = "Assets/Art/Tank/Sprites";

    private sealed class TankIcons
    {
        public string Tank;
        public string Asset;
        public string[] Icons;   // dung thu tu slot J, K, L, I
    }

    private static readonly TankIcons[] Tanks =
    {
        new TankIcons
        {
            Tank = "BlackShadow",
            Asset = "Assets/Prefabs/Tanks/BlackShadow/BlackShadow.asset",
            Icons = new[]
            {
                ArtRoot + "/skill/blackShadow/icon_shadow_claw.png",
                ArtRoot + "/skill/blackShadow/icon_shadow_step.png",
                ArtRoot + "/skill/blackShadow/icon_shadow_predator.png",
                ArtRoot + "/skill/blackShadow/icon_shadow_ambush.png"
            }
        },
        new TankIcons
        {
            Tank = "GreenFox",
            Asset = "Assets/Prefabs/Tanks/GreenFox/GreenFox.asset",
            Icons = new[]
            {
                ArtRoot + "/GFSkill/icon_fox_fang_master.png",
                ArtRoot + "/GFSkill/icon_fox_mark_master.png",
                ArtRoot + "/GFSkill/icon_fox_fire_master.png",
                ArtRoot + "/GFSkill/icon_fox_storm_master.png"
            }
        },
        new TankIcons
        {
            Tank = "WaterB",
            Asset = "Assets/Prefabs/Tanks/WaterB/WaterB.asset",
            Icons = new[]
            {
                ArtRoot + "/WBSKILL/icon_healing_water_master.png",
                ArtRoot + "/WBSKILL/icon_bubble_shield_master.png",
                ArtRoot + "/WBSKILL/icon_water_wave_master.png",
                ArtRoot + "/WBSKILL/icon_water_return_master.png"
            }
        },
        new TankIcons
        {
            Tank = "RedNova",
            Asset = "Assets/Prefabs/Tanks/RedNova/RedNova.asset",
            Icons = new[]
            {
                ArtRoot + "/RedNova/icon_nova_shot_master.png",
                ArtRoot + "/RedNova/icon_triple_nova_master.png",
                ArtRoot + "/RedNova/icon_laser_cannon_master.png",
                ArtRoot + "/RedNova/icon_nova_barrage_master.png"
            }
        },
        new TankIcons
        {
            Tank = "WallTitan",
            Asset = "Assets/Prefabs/Tanks/WallTitan/WallTitan.asset",
            Icons = new[]
            {
                ArtRoot + "/WallTitan/icon_earth_break_master.png",
                ArtRoot + "/WallTitan/icon_earth_wall_master.png",
                ArtRoot + "/WallTitan/icon_earth_prison_master.png",
                ArtRoot + "/WallTitan/icon_earthquake_master.png"
            }
        },
        new TankIcons
        {
            Tank = "Leviathan",
            Asset = "Assets/Prefabs/Tanks/Leviathan/Leviathan.asset",
            Icons = new[]
            {
                ArtRoot + "/skill/leviathan/icon_water_pulse_master.png",
                ArtRoot + "/skill/leviathan/icon_water_bubble_master.png",
                ArtRoot + "/skill/leviathan/icon_water_armor_master.png",
                ArtRoot + "/skill/leviathan/icon_tidal_return_master.png"
            }
        }
    };

    [MenuItem("Tools/Iron Wasteland/Integrate New Skill Icons")]
    public static void Integrate()
    {
        var sb = new StringBuilder("[SKILLICON]\n");
        int ok = 0;

        foreach (TankIcons tank in Tanks)
        {
            var sprites = new List<Sprite>(tank.Icons.Length);
            bool missing = false;

            foreach (string path in tank.Icons)
            {
                ConfigureImporter(path);

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    sb.AppendLine("  THIEU: " + path);
                    missing = true;
                    continue;
                }

                sprites.Add(sprite);
            }

            if (missing || sprites.Count != 4)
            {
                sb.AppendLine(tank.Tank + ": BO QUA (khong du 4 icon)");
                continue;
            }

            TankDefinition definition = AssetDatabase.LoadAssetAtPath<TankDefinition>(tank.Asset);
            if (definition == null)
            {
                sb.AppendLine(tank.Tank + ": KHONG thay TankDefinition " + tank.Asset);
                continue;
            }

            var so = new SerializedObject(definition);

            SerializedProperty icons = so.FindProperty("skillIcons");
            icons.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
            {
                icons.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }

            // Art moi la the day du: khong dung anh cooldown rieng, cung khong ve them glyph.
            so.FindProperty("skillIconsCooldown").arraySize = 0;
            so.FindProperty("skillGlyphs").arraySize = 0;
            so.FindProperty("skillIconsIncludeLabels").boolValue = false;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            ok++;
            sb.AppendLine(tank.Tank + ": gan 4 icon (" + sprites[0].name + " ... " + sprites[3].name + ")");
        }

        AssetDatabase.SaveAssets();
        sb.AppendLine("xong " + ok + "/" + Tanks.Length + " xe");
        Debug.Log(sb.ToString());
    }

    private static void ConfigureImporter(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            sb_LogMissing(path);
            return;
        }

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f)) { importer.spritePixelsPerUnit = 100f; changed = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
        if (importer.maxTextureSize < 1024) { importer.maxTextureSize = 1024; changed = true; }

        if (changed) importer.SaveAndReimport();
    }

    private static void sb_LogMissing(string path)
    {
        Debug.LogWarning("[SKILLICON] khong co file: " + path);
    }
}
#endif
