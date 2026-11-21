#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using IronWasteland.Tanks;

/// <summary>
/// Sinh icon ky nang cho 6 xe theo cung mot ngon ngu hinh khoi voi bo art HUD da co
/// (khung bevel toi, vien mau cua xe, huy hieu o giua) va tu gan vao TankDefinition.
///
/// Black Shadow da co art rieng trong Assets/Art/UI/GameplayHUD/Buttons nen duoc chep
/// sang SkillIcons/BlackShadow thay vi ve lai.
///
/// Chay: Tools > Iron Wasteland > Generate Skill Icons.
/// </summary>
public static class SkillIconGenerator
{
    private const string Root = "Assets/Art/UI/GameplayHUD";
    private const string IconRoot = Root + "/SkillIcons";
    private const int W = 250;
    private const int H = 231;
    private const int SS = 4; // supersample de canh tron muot

    private enum Glyph
    {
        Fang, Mark, Flame, Swirl,
        Cross, Bubble, Waves, ReturnArrow,
        Burst, Trio, Beam, Rain,
        Bricks, Cage, Crack, QuakeArcs,
        Pulse, BubbleShield, ArmorPlate, TideArrow
    }

    private class Spec
    {
        public string Tank;      // ten folder + ten asset
        public string Asset;     // duong dan TankDefinition
        public Color Rim;        // mau vien + huy hieu
        public Glyph[] Glyphs;   // 4 ky nang
        public string[] Slugs;
        public string[] Names;   // ten hien tren the icon
    }

    private static readonly Spec[] Specs =
    {
        new Spec
        {
            Tank = "BlackShadow", Asset = "Assets/Prefabs/Tanks/BlackShadow/BlackShadow.asset",
            Rim = new Color(0.76f, 0.55f, 1f),
            Glyphs = new[] { Glyph.Burst, Glyph.ReturnArrow, Glyph.Burst, Glyph.Rain },
            Slugs = new[] { "shadow_claw", "shadow_step", "shadow_predator", "shadow_ambush" },
            Names = new[] { "SHADOW CLAW", "SHADOW STEP", "SHADOW PREDATOR", "SHADOW AMBUSH" }
        },
        new Spec
        {
            Tank = "GreenFox", Asset = "Assets/Prefabs/Tanks/GreenFox/GreenFox.asset",
            Rim = new Color(0.35f, 0.95f, 0.45f),
            Glyphs = new[] { Glyph.Fang, Glyph.Mark, Glyph.Flame, Glyph.Swirl },
            Slugs = new[] { "fox_fang", "fox_mark", "fox_fire", "fox_storm" },
            Names = new[] { "FOX FANG", "FOX MARK", "FOX FIRE", "FOX STORM" }
        },
        new Spec
        {
            Tank = "WaterB", Asset = "Assets/Prefabs/Tanks/WaterB/WaterB.asset",
            Rim = new Color(0.32f, 0.78f, 1f),
            Glyphs = new[] { Glyph.Cross, Glyph.BubbleShield, Glyph.Waves, Glyph.ReturnArrow },
            Slugs = new[] { "healing_water", "bubble_shield", "water_wave", "water_return" },
            Names = new[] { "HEALING WATER", "BUBBLE SHIELD", "WATER WAVE", "WATER RETURN" }
        },
        new Spec
        {
            Tank = "RedNova", Asset = "Assets/Prefabs/Tanks/RedNova/RedNova.asset",
            Rim = new Color(1f, 0.42f, 0.36f),
            Glyphs = new[] { Glyph.Burst, Glyph.Trio, Glyph.Beam, Glyph.Rain },
            Slugs = new[] { "nova_shot", "triple_nova", "laser_cannon", "nova_barrage" },
            Names = new[] { "NOVA SHOT", "TRIPLE NOVA", "LASER CANNON", "NOVA BARRAGE" }
        },
        new Spec
        {
            Tank = "WallTitan", Asset = "Assets/Prefabs/Tanks/WallTitan/WallTitan.asset",
            Rim = new Color(1f, 0.72f, 0.30f),
            Glyphs = new[] { Glyph.Crack, Glyph.Bricks, Glyph.Cage, Glyph.QuakeArcs },
            Slugs = new[] { "earth_break", "earth_wall", "earth_prison", "earthquake" },
            Names = new[] { "EARTH BREAK", "EARTH WALL", "EARTH PRISON", "EARTHQUAKE" }
        },
        new Spec
        {
            Tank = "Leviathan", Asset = "Assets/Prefabs/Tanks/Leviathan/Leviathan.asset",
            Rim = new Color(0.30f, 0.92f, 0.90f),
            Glyphs = new[] { Glyph.Pulse, Glyph.Bubble, Glyph.ArmorPlate, Glyph.TideArrow },
            Slugs = new[] { "water_pulse", "water_bubble", "water_armor", "tidal_return" },
            Names = new[] { "WATER PULSE", "WATER BUBBLE", "WATER ARMOR", "TIDAL RETURN" }
        }
    };

    private static readonly string[] Keys = { "j", "k", "l", "i" };

    /// <summary>
    /// Xe da co art bieu tuong rieng (trong suot) trong Assets/Art/Tank/Sprites/skill.
    /// Xe khong co trong bang nay dung bieu tuong vector sinh ra.
    /// </summary>
    private static readonly Dictionary<string, string[]> ArtIcons = new Dictionary<string, string[]>
    {
        ["BlackShadow"] = new[]
        {
            "Assets/Art/Tank/Sprites/skill/blackShadow/icon_shadow_claw.png",
            "Assets/Art/Tank/Sprites/skill/blackShadow/icon_shadow_step.png",
            "Assets/Art/Tank/Sprites/skill/blackShadow/icon_shadow_predator.png",
            "Assets/Art/Tank/Sprites/skill/blackShadow/icon_shadow_ambush.png"
        },
        ["Leviathan"] = new[]
        {
            "Assets/Art/Tank/Sprites/skill/leviathan/icon_water_pulse.png",
            "Assets/Art/Tank/Sprites/skill/leviathan/icon_water_bubble.png",
            "Assets/Art/Tank/Sprites/skill/leviathan/icon_water_armor.png",
            "Assets/Art/Tank/Sprites/skill/leviathan/icon_tidal_return.png"
        }
    };

    [MenuItem("Tools/Iron Wasteland/Generate Skill Icons")]
    public static void Generate()
    {
        Directory.CreateDirectory(IconRoot);

        // 1. Black Shadow: chep art da co (giu nguyen chat luong) thay vi ve lai.
        CopyBlackShadow();

        // 2. Nam xe con lai: ve icon ready + cooldown.
        for (int s = 0; s < Specs.Length; s++)
        {
            Spec spec = Specs[s];
            if (spec.Tank == "BlackShadow") continue;

            string dir = IconRoot + "/" + spec.Tank;
            Directory.CreateDirectory(dir);

            for (int slot = 0; slot < 4; slot++)
            {
                string baseName = $"{slot + 1:00}_{Keys[slot]}_{spec.Slugs[slot]}";
                string key = Keys[slot].ToUpperInvariant();
                Render(dir + "/" + baseName + "_ready.png", spec.Rim, spec.Glyphs[slot], false, key, spec.Names[slot]);
                Render(dir + "/" + baseName + "_cooldown.png", spec.Rim, spec.Glyphs[slot], true, key, spec.Names[slot]);
                RenderGlyph(dir + "/" + baseName + "_glyph.png", spec.Rim, spec.Glyphs[slot]);
            }
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        ConfigureImporters();

        // 3. Gan vao 6 TankDefinition.
        for (int s = 0; s < Specs.Length; s++) Assign(Specs[s]);

        AssetDatabase.SaveAssets();
        Debug.Log("[SKILL-ICON] xong 6 bo icon (24 ky nang, moi ky nang 1 ready + 1 cooldown)");
    }

    // ---------------------------------------------------------------- Black Shadow

    private static void CopyBlackShadow()
    {
        string dir = IconRoot + "/BlackShadow";
        Directory.CreateDirectory(dir);

        string[,] map =
        {
            { "btn_skill_slot_01_j_shadow_claw_ready", "01_j_shadow_claw_ready" },
            { "btn_skill_slot_01_j_shadow_claw_cooldown", "01_j_shadow_claw_cooldown" },
            { "btn_skill_slot_02_k_shadow_step_ready", "02_k_shadow_step_ready" },
            { "btn_skill_slot_02_k_shadow_step_cooldown", "02_k_shadow_step_cooldown" },
            { "btn_skill_slot_03_l_shadow_predator_ready", "03_l_shadow_predator_ready" },
            { "btn_skill_slot_03_l_shadow_predator_active", "03_l_shadow_predator_cooldown" },
            { "btn_skill_slot_04_i_shadow_ambush_ready", "04_i_shadow_ambush_ready" },
            { "btn_skill_slot_04_i_shadow_ambush_cooldown", "04_i_shadow_ambush_cooldown" },
            { "btn_skill_slot_03_l_shadow_guard_ready", "03_l_alt_shadow_guard_ready" },
            { "btn_skill_slot_03_l_shadow_guard_cooldown", "03_l_alt_shadow_guard_cooldown" },
            { "btn_skill_slot_03_l_shadow_link_ready", "03_l_alt_shadow_link_ready" },
            { "btn_skill_slot_03_l_shadow_link_cooldown", "03_l_alt_shadow_link_cooldown" }
        };

        for (int i = 0; i < map.GetLength(0); i++)
        {
            string src = Root + "/Buttons/" + map[i, 0] + ".png";
            string dst = dir + "/" + map[i, 1] + ".png";
            if (!File.Exists(src)) continue;
            File.Copy(src, dst, true);
        }
    }

    // ---------------------------------------------------------------- ve

    private static void Render(string path, Color rim, Glyph glyph, bool cooldown, string key, string name)
    {
        int pw = W * SS;
        int ph = H * SS;
        Color32[] px = new Color32[pw * ph];

        float inset = 8f * SS;
        float cut = 26f * SS;
        float bevel = 9f * SS;

        Color top = cooldown ? new Color(0.055f, 0.065f, 0.085f) : new Color(0.10f, 0.125f, 0.165f);
        Color bottom = cooldown ? new Color(0.015f, 0.02f, 0.03f) : new Color(0.03f, 0.04f, 0.055f);
        Color rimColor = cooldown ? new Color(rim.r * 0.40f, rim.g * 0.40f, rim.b * 0.45f, 1f) : rim;

        float x0 = inset, x1 = pw - inset, y0 = inset, y1 = ph - inset;

        // The: goc tren-phai cat cheo giong cac the ky nang cua Black Shadow.
        Vector2[] plate =
        {
            new Vector2(x0 + bevel, y0),
            new Vector2(x1, y0),
            new Vector2(x1, y1 - cut),
            new Vector2(x1 - cut, y1),
            new Vector2(x0 + bevel, y1),
            new Vector2(x0, y1 - bevel)
        };

        FillPoly(px, pw, ph, plate, (x, y) => Color.Lerp(bottom, top, Mathf.InverseLerp(y0, y1, y)));

        var draw = new Draw(px, pw, ph);
        draw.PolyOutline(plate, 2.5f * SS, rimColor);

        // Huy hieu phim o goc tren-trai (giong art goc: o vuong nho vien mau).
        float badge = 44f * SS;
        float bx = x0 + 12f * SS;
        float by = y1 - 12f * SS - badge;
        FillRoundRect(px, pw, ph, bx, by, bx + badge, by + badge, 6f * SS,
            (x, y, t) => new Color(0.055f, 0.065f, 0.09f, 1f));
        StrokeRoundRect(px, pw, ph, bx, by, bx + badge, by + badge, 6f * SS, 1.6f * SS, rimColor);

        float keyScale = 3.6f * SS;
        DrawText(px, pw, ph, key, bx + badge * 0.5f, by + (badge - 7f * keyScale) * 0.5f, keyScale, rimColor, true);

        // Huy hieu hinh khoi o nua tren.
        Vector2 c = new Vector2(pw * 0.5f, y0 + (y1 - y0) * 0.62f);
        float s = ph * 0.235f;

        Color ink = cooldown
            ? new Color(rim.r * 0.55f, rim.g * 0.55f, rim.b * 0.6f, 0.75f)
            : new Color(Mathf.Lerp(rim.r, 1f, 0.25f), Mathf.Lerp(rim.g, 1f, 0.25f), Mathf.Lerp(rim.b, 1f, 0.25f), 1f);

        Color glow = new Color(rim.r, rim.g, rim.b, cooldown ? 0.10f : 0.20f);
        DrawGlyph(draw, glyph, c, s, glow, 2.2f);
        DrawGlyph(draw, glyph, c, s, ink, 1f);

        // Ten ky nang + trang thai, giong bo art goc.
        float nameScale = 9f * SS * 0.25f;
        float nameWidth = name.Length * 6f * nameScale;
        float maxWidth = (x1 - x0) * 0.86f;
        if (nameWidth > maxWidth) nameScale *= maxWidth / nameWidth;

        Color nameColor = cooldown ? new Color(0.70f, 0.75f, 0.82f, 1f) : new Color(0.94f, 0.96f, 1f, 1f);
        Color stateColor = cooldown ? new Color(0.55f, 0.60f, 0.68f, 1f) : rim;

        float nameBottom = y0 + (y1 - y0) * 0.145f;
        DrawText(px, pw, ph, name.ToUpperInvariant(), pw * 0.5f, nameBottom, nameScale, nameColor, false);

        string state = cooldown ? "HỒI CHIẾU" : "SẴN SÀNG";
        float stateScale = 6f * SS * 0.25f;
        DrawText(px, pw, ph, state, pw * 0.5f, y0 + (y1 - y0) * 0.045f, stateScale, stateColor, false);

        // Downsample 4x4.
        Color32[] outPx = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (int sy = 0; sy < SS; sy++)
                {
                    for (int sx = 0; sx < SS; sx++)
                    {
                        Color32 p = px[(y * SS + sy) * pw + (x * SS + sx)];
                        float pa = p.a / 255f;
                        r += p.r * pa;
                        g += p.g * pa;
                        b += p.b * pa;
                        a += pa;
                    }
                }

                int n = SS * SS;
                if (a <= 0.0001f)
                {
                    outPx[y * W + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                outPx[y * W + x] = new Color32(
                    (byte)Mathf.Clamp(r / a, 0, 255),
                    (byte)Mathf.Clamp(g / a, 0, 255),
                    (byte)Mathf.Clamp(b / a, 0, 255),
                    (byte)Mathf.Clamp(a / n * 255f, 0, 255));
            }
        }

        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.SetPixels32(outPx);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    /// <summary>
    /// Bieu tuong trong suot (chi hinh, khong khung/chu) de HUD tu ve khung + ten + trang thai.
    /// </summary>
    private static void RenderGlyph(string path, Color rim, Glyph glyph)
    {
        const int G = 256;
        const int S = 2;
        int pw = G * S;
        int ph = G * S;
        Color32[] px = new Color32[pw * ph];

        var draw = new Draw(px, pw, ph);
        Vector2 c = new Vector2(pw * 0.5f, ph * 0.5f);
        float s = pw * 0.30f;

        Color glow = new Color(rim.r, rim.g, rim.b, 0.45f);
        Color ink = new Color(Mathf.Lerp(rim.r, 1f, 0.7f), Mathf.Lerp(rim.g, 1f, 0.7f), Mathf.Lerp(rim.b, 1f, 0.7f), 1f);

        DrawGlyph(draw, glyph, c, s, glow, 2.6f);
        DrawGlyph(draw, glyph, c, s * 0.99f, ink, 1.1f);

        Color32[] outPx = new Color32[G * G];
        for (int y = 0; y < G; y++)
        {
            for (int x = 0; x < G; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (int sy = 0; sy < S; sy++)
                {
                    for (int sx = 0; sx < S; sx++)
                    {
                        Color32 p = px[(y * S + sy) * pw + (x * S + sx)];
                        float pa = p.a / 255f;
                        r += p.r * pa;
                        g += p.g * pa;
                        b += p.b * pa;
                        a += pa;
                    }
                }

                int n = S * S;
                if (a <= 0.0001f)
                {
                    outPx[y * G + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                outPx[y * G + x] = new Color32(
                    (byte)Mathf.Clamp(r / a, 0, 255),
                    (byte)Mathf.Clamp(g / a, 0, 255),
                    (byte)Mathf.Clamp(b / a, 0, 255),
                    (byte)Mathf.Clamp(a / n * 255f, 0, 255));
            }
        }

        Texture2D tex = new Texture2D(G, G, TextureFormat.RGBA32, false);
        tex.SetPixels32(outPx);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    /// <summary>To da giac bang quet dong (gradient theo truc y).</summary>
    private static void FillPoly(Color32[] px, int w, int h, Vector2[] pts, System.Func<float, float, Color> shader)
    {
        float minX = pts[0].x, maxX = pts[0].x, minY = pts[0].y, maxY = pts[0].y;
        for (int i = 1; i < pts.Length; i++)
        {
            minX = Mathf.Min(minX, pts[i].x); maxX = Mathf.Max(maxX, pts[i].x);
            minY = Mathf.Min(minY, pts[i].y); maxY = Mathf.Max(maxY, pts[i].y);
        }

        for (int y = Mathf.FloorToInt(minY); y < Mathf.CeilToInt(maxY); y++)
        {
            for (int x = Mathf.FloorToInt(minX); x < Mathf.CeilToInt(maxX); x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) continue;

                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = false;
                for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                {
                    if (((pts[i].y > p.y) != (pts[j].y > p.y)) &&
                        (p.x < (pts[j].x - pts[i].x) * (p.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x))
                    {
                        inside = !inside;
                    }
                }

                if (inside) px[y * w + x] = shader(p.x, p.y);
            }
        }
    }

    // ---------------------------------------------------------------- chu (font 5x7)

    private static readonly Dictionary<char, string[]> Font5x7 = BuildFont();

    private static Dictionary<char, string[]> BuildFont()
    {
        var f = new Dictionary<char, string[]>();
        void Add(char c, params string[] rows) => f[c] = rows;

        Add('A', ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
        Add('B', "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####.");
        Add('C', ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###.");
        Add('D', "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####.");
        Add('E', "#####", "#....", "#....", "####.", "#....", "#....", "#####");
        Add('F', "#####", "#....", "#....", "####.", "#....", "#....", "#....");
        Add('G', ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####");
        Add('H', "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
        Add('I', "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####");
        Add('J', "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##..");
        Add('K', "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#");
        Add('L', "#....", "#....", "#....", "#....", "#....", "#....", "#####");
        Add('M', "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#");
        Add('N', "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#");
        Add('O', ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
        Add('P', "####.", "#...#", "#...#", "####.", "#....", "#....", "#....");
        Add('Q', ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#");
        Add('R', "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#");
        Add('S', ".####", "#....", "#....", ".###.", "....#", "....#", "####.");
        Add('T', "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#..");
        Add('U', "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
        Add('V', "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#..");
        Add('W', "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#");
        Add('X', "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#");
        Add('Y', "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#..");
        Add('Z', "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####");
        Add('0', ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###.");
        Add('1', "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###.");
        Add('2', ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####");
        Add('3', "#####", "...#.", "..#..", "...#.", "....#", "#...#", ".###.");
        Add('4', "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#.");
        Add('5', "#####", "#....", "####.", "....#", "....#", "#...#", ".###.");
        Add('6', "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###.");
        Add('7', "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#...");
        Add('8', ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###.");
        Add('9', ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##..");
        Add('-', ".....", ".....", ".....", "#####", ".....", ".....", ".....");
        Add('.', ".....", ".....", ".....", ".....", ".....", ".##..", ".##..");
        Add(' ', ".....", ".....", ".....", ".....", ".....", ".....", ".....");
        return f;
    }

    /// <summary>
    /// Glyph tiếng Việt có dấu: 10 hàng = 3 hàng dấu + 7 hàng chữ (riêng Đ chỉ 7 hàng).
    /// Chỉ khai báo những chữ thật sự xuất hiện trên thẻ icon ("SẴN SÀNG" / "HỒI CHIẾU"),
    /// vì font 5×7 không đủ chỗ cho dấu.
    /// </summary>
    private static readonly Dictionary<char, string[]> HighGlyphs = new Dictionary<char, string[]>
    {
        ['À'] = new[] { "...#.", "..#..", ".....", ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
        ['Ẵ'] = new[] { "##.##", "..#..", ".#.#.", ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
        ['Ồ'] = new[] { "...#.", "..#..", ".#.#.", ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
        ['Í'] = new[] { "...#.", "..#..", ".....", "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" },
        ['Ế'] = new[] { "...#.", "..#..", ".#.#.", "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
        ['Đ'] = new[] { "####.", "#...#", "#...#", "#####", "#...#", "#...#", "####." },
    };

    /// <summary>Ve chu canh giua theo truc x, day (bottom) tai y.</summary>
    private static void DrawText(Color32[] px, int w, int h, string text, float cx, float bottomY, float scale, Color col, bool fillInsideBox)
    {
        if (string.IsNullOrEmpty(text)) return;

        float charW = 6f * scale;
        float total = text.Length * charW - scale;
        float x = cx - total * 0.5f;

        Color shadow = new Color(0f, 0f, 0f, 0.75f);
        for (int pass = 0; pass < 2; pass++)
        {
            Color c = pass == 0 ? shadow : col;
            float ox = pass == 0 ? scale * 0.5f : 0f;
            float oy = pass == 0 ? -scale * 0.5f : 0f;
            float cursor = x;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = char.ToUpperInvariant(text[i]);
                string[] rows = null;

                // Chữ có dấu (Ẵ, À, Ồ, Í, Ế…) dùng bảng glyph cao 10 hàng: 3 hàng dấu + 7 hàng chữ.
                if (!HighGlyphs.TryGetValue(ch, out rows) && !Font5x7.TryGetValue(ch, out rows))
                {
                    cursor += charW;
                    continue;
                }

                int n = rows.Length;
                for (int r = 0; r < n; r++)
                {
                    string row = rows[r];
                    for (int col2 = 0; col2 < row.Length; col2++)
                    {
                        if (row[col2] != '#') continue;
                        float px0 = cursor + col2 * scale + ox;
                        float py0 = bottomY + (n - 1 - r) * scale + oy;
                        FillRect(px, w, h, px0, py0, scale * 1.02f, scale * 1.02f, c);
                    }
                }

                cursor += charW;
            }
        }
    }

    private static void FillRect(Color32[] px, int w, int h, float x, float y, float sw, float sh, Color col)
    {
        int x0 = Mathf.FloorToInt(x), x1 = Mathf.CeilToInt(x + sw);
        int y0 = Mathf.FloorToInt(y), y1 = Mathf.CeilToInt(y + sh);

        for (int yy = y0; yy < y1; yy++)
        {
            for (int xx = x0; xx < x1; xx++)
            {
                if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;

                int i = yy * w + xx;
                Color32 dst = px[i];
                float a = col.a + (dst.a / 255f) * (1f - col.a);
                px[i] = new Color(
                    (col.r * col.a + dst.r / 255f * (dst.a / 255f) * (1f - col.a)) / Mathf.Max(0.0001f, a),
                    (col.g * col.a + dst.g / 255f * (dst.a / 255f) * (1f - col.a)) / Mathf.Max(0.0001f, a),
                    (col.b * col.a + dst.b / 255f * (dst.a / 255f) * (1f - col.a)) / Mathf.Max(0.0001f, a),
                    a);
            }
        }
    }


    // ---------------------------------------------------------------- huy hieu

    private static void DrawGlyph(Draw d, Glyph g, Vector2 c, float s, Color col, float widthScale)
    {
        float w = 0.12f * s * widthScale;

        switch (g)
        {
            case Glyph.Fang:
                d.Tri(c + new Vector2(-0.52f, 0.10f) * s, c + new Vector2(-0.14f, 0.10f) * s, c + new Vector2(-0.33f, -0.52f) * s, col);
                d.Tri(c + new Vector2(0.14f, 0.10f) * s, c + new Vector2(0.52f, 0.10f) * s, c + new Vector2(0.33f, -0.52f) * s, col);
                d.Line(c + new Vector2(-0.60f, 0.24f) * s, c + new Vector2(0.60f, 0.24f) * s, w * 0.9f, col);
                break;

            case Glyph.Mark:
                d.PolyOutline(new[] { c + new Vector2(0f, -0.6f) * s, c + new Vector2(0.55f, 0f) * s, c + new Vector2(0f, 0.6f) * s, c + new Vector2(-0.55f, 0f) * s }, w, col);
                d.Circle(c, 0.16f * s, col);
                break;

            case Glyph.Flame:
                d.PolyOutline(new[]
                {
                    c + new Vector2(0f, 0.62f) * s,
                    c + new Vector2(0.34f, 0.12f) * s,
                    c + new Vector2(0.24f, -0.16f) * s,
                    c + new Vector2(0f, -0.58f) * s,
                    c + new Vector2(-0.24f, -0.16f) * s,
                    c + new Vector2(-0.34f, 0.12f) * s
                }, w, col);
                d.PolyOutline(new[]
                {
                    c + new Vector2(0f, -0.02f) * s,
                    c + new Vector2(0.14f, -0.20f) * s,
                    c + new Vector2(0f, -0.40f) * s,
                    c + new Vector2(-0.14f, -0.20f) * s
                }, w * 0.7f, col);
                break;

            case Glyph.Swirl:
                d.Arc(c, 0.52f * s, 20f, 250f, w, col);
                d.Arc(c, 0.34f * s, 140f, 380f, w * 0.85f, col);
                d.Circle(c, 0.10f * s, col);
                break;

            case Glyph.Cross:
                d.Ring(c, 0.52f * s, w, col);
                d.Line(c + new Vector2(-0.24f, 0f) * s, c + new Vector2(0.24f, 0f) * s, w * 1.25f, col);
                d.Line(c + new Vector2(0f, -0.24f) * s, c + new Vector2(0f, 0.24f) * s, w * 1.25f, col);
                break;

            case Glyph.Bubble:
                d.Ring(c + new Vector2(-0.10f, -0.06f) * s, 0.36f * s, w, col);
                d.Ring(c + new Vector2(0.34f, 0.30f) * s, 0.17f * s, w * 0.85f, col);
                d.Ring(c + new Vector2(0.30f, -0.38f) * s, 0.10f * s, w * 0.8f, col);
                d.Circle(c + new Vector2(-0.30f, 0.24f) * s, 0.07f * s, col);
                break;

            case Glyph.Waves:
                for (int i = -1; i <= 1; i++)
                {
                    float y = i * 0.40f;
                    float h = 0.10f;
                    d.Line(c + new Vector2(-0.52f, y) * s, c + new Vector2(-0.18f, y + h) * s, w * 0.85f, col);
                    d.Line(c + new Vector2(-0.18f, y + h) * s, c + new Vector2(0.18f, y - h) * s, w * 0.85f, col);
                    d.Line(c + new Vector2(0.18f, y - h) * s, c + new Vector2(0.52f, y) * s, w * 0.85f, col);
                }
                break;

            case Glyph.ReturnArrow:
                d.Arc(c, 0.42f * s, 30f, 320f, w, col);
                d.Tri(c + new Vector2(0.24f, 0.34f) * s, c + new Vector2(0.62f, 0.40f) * s, c + new Vector2(0.44f, 0.02f) * s, col);
                break;

            case Glyph.Burst:
                d.Circle(c, 0.22f * s, col);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 2f / 8f;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    d.Line(c + dir * 0.34f * s, c + dir * 0.62f * s, w * 0.85f, col);
                }
                break;

            case Glyph.Trio:
                d.Circle(c + new Vector2(-0.40f, 0.34f) * s, 0.14f * s, col);
                d.Circle(c + new Vector2(0f, 0f) * s, 0.18f * s, col);
                d.Circle(c + new Vector2(0.40f, -0.34f) * s, 0.22f * s, col);
                break;

            case Glyph.Beam:
                d.Ring(c + new Vector2(-0.40f, 0f) * s, 0.22f * s, w, col);
                d.Circle(c + new Vector2(-0.40f, 0f) * s, 0.08f * s, col);
                d.Line(c + new Vector2(-0.16f, 0f) * s, c + new Vector2(0.62f, 0f) * s, w * 1.6f, col);
                d.Line(c + new Vector2(0.30f, 0.26f) * s, c + new Vector2(0.56f, 0.46f) * s, w * 0.7f, col);
                d.Line(c + new Vector2(0.30f, -0.26f) * s, c + new Vector2(0.56f, -0.46f) * s, w * 0.7f, col);
                break;

            case Glyph.Rain:
                for (int i = -1; i <= 1; i++)
                {
                    float x = i * 0.38f;
                    d.Line(c + new Vector2(x, 0.50f) * s, c + new Vector2(x - 0.10f, 0.10f) * s, w * 0.8f, col);
                    d.Circle(c + new Vector2(x - 0.16f, -0.34f) * s, 0.11f * s, col);
                }
                break;

            case Glyph.Bricks:
                d.PolyOutline(new[] { c + new Vector2(-0.58f, -0.44f) * s, c + new Vector2(0.58f, -0.44f) * s, c + new Vector2(0.58f, 0.44f) * s, c + new Vector2(-0.58f, 0.44f) * s }, w, col);
                d.Line(c + new Vector2(-0.58f, 0f) * s, c + new Vector2(0.58f, 0f) * s, w * 0.55f, col);
                d.Line(c + new Vector2(-0.20f, 0.44f) * s, c + new Vector2(-0.20f, 0f) * s, w * 0.55f, col);
                d.Line(c + new Vector2(0.22f, 0f) * s, c + new Vector2(0.22f, -0.44f) * s, w * 0.55f, col);
                break;

            case Glyph.Cage:
                d.Line(c + new Vector2(-0.56f, 0.46f) * s, c + new Vector2(0.56f, 0.46f) * s, w, col);
                d.Line(c + new Vector2(-0.56f, -0.46f) * s, c + new Vector2(0.56f, -0.46f) * s, w, col);
                for (int i = 0; i < 4; i++)
                {
                    float x = -0.36f + i * 0.24f;
                    d.Line(c + new Vector2(x, 0.46f) * s, c + new Vector2(x, -0.46f) * s, w * 0.85f, col);
                }
                break;

            case Glyph.Crack:
                d.PolyOutline(new[] { c + new Vector2(-0.55f, -0.20f) * s, c + new Vector2(-0.20f, 0.46f) * s, c + new Vector2(0.30f, 0.40f) * s, c + new Vector2(0.56f, -0.16f) * s, c + new Vector2(0.10f, -0.48f) * s, c + new Vector2(-0.36f, -0.44f) * s }, w, col);
                d.Line(c + new Vector2(-0.10f, 0.36f) * s, c + new Vector2(0.06f, 0.10f) * s, w * 0.8f, col);
                d.Line(c + new Vector2(0.06f, 0.10f) * s, c + new Vector2(-0.10f, -0.06f) * s, w * 0.8f, col);
                d.Line(c + new Vector2(-0.10f, -0.06f) * s, c + new Vector2(0.10f, -0.32f) * s, w * 0.8f, col);
                break;

            case Glyph.QuakeArcs:
                d.Arc(c + new Vector2(0f, -0.30f) * s, 0.30f * s, 20f, 160f, w, col);
                d.Arc(c + new Vector2(0f, -0.34f) * s, 0.48f * s, 20f, 160f, w * 0.9f, col);
                d.Arc(c + new Vector2(0f, -0.38f) * s, 0.66f * s, 20f, 160f, w * 0.75f, col);
                d.Line(c + new Vector2(0f, 0.50f) * s, c + new Vector2(-0.08f, 0.18f) * s, w * 0.8f, col);
                break;

            case Glyph.Pulse:
                d.Arc(c + new Vector2(-0.24f, 0f) * s, 0.34f * s, -60f, 60f, w, col);
                d.Arc(c + new Vector2(-0.24f, 0f) * s, 0.52f * s, -60f, 60f, w * 0.9f, col);
                d.Arc(c + new Vector2(-0.24f, 0f) * s, 0.70f * s, -60f, 60f, w * 0.75f, col);
                d.Circle(c + new Vector2(-0.48f, 0f) * s, 0.10f * s, col);
                break;

            case Glyph.BubbleShield:
                d.Ring(c + new Vector2(-0.06f, 0f) * s, 0.44f * s, w, col);
                d.Ring(c + new Vector2(0.36f, 0.34f) * s, 0.18f * s, w * 0.85f, col);
                d.Ring(c + new Vector2(0.34f, -0.34f) * s, 0.12f * s, w * 0.8f, col);
                d.Line(c + new Vector2(-0.30f, -0.20f) * s, c + new Vector2(-0.06f, -0.34f) * s, w * 0.6f, col);
                break;

            case Glyph.ArmorPlate:
                d.PolyOutline(new[] { c + new Vector2(0f, 0.58f) * s, c + new Vector2(0.46f, 0.30f) * s, c + new Vector2(0.46f, -0.18f) * s, c + new Vector2(0f, -0.56f) * s, c + new Vector2(-0.46f, -0.18f) * s, c + new Vector2(-0.46f, 0.30f) * s }, w, col);
                d.Line(c + new Vector2(-0.22f, 0.02f) * s, c + new Vector2(0f, -0.20f) * s, w * 0.8f, col);
                d.Line(c + new Vector2(0f, -0.20f) * s, c + new Vector2(0.24f, 0.10f) * s, w * 0.8f, col);
                break;

            case Glyph.TideArrow:
                d.Arc(c, 0.34f * s, 0f, 180f, w, col);
                d.Line(c + new Vector2(-0.34f, 0f) * s, c + new Vector2(-0.34f, -0.34f) * s, w, col);
                d.Line(c + new Vector2(0.34f, 0f) * s, c + new Vector2(0.34f, -0.16f) * s, w, col);
                d.Tri(c + new Vector2(0.18f, -0.16f) * s, c + new Vector2(0.50f, -0.16f) * s, c + new Vector2(0.34f, -0.52f) * s, col);
                break;
        }
    }

    // ---------------------------------------------------------------- thao tac pixel

    private class Draw
    {
        private readonly Color32[] m_Px;
        private readonly int m_W;
        private readonly int m_H;

        public Draw(Color32[] px, int w, int h)
        {
            m_Px = px;
            m_W = w;
            m_H = h;
        }

        public void Blend(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= m_W || y >= m_H) return;
            if (c.a <= 0.001f) return;

            int i = y * m_W + x;
            Color32 d = m_Px[i];
            float da = d.a / 255f;
            float sa = c.a;
            float oa = sa + da * (1f - sa);

            Color r = new Color(
                (c.r * sa + d.r / 255f * da * (1f - sa)) / Mathf.Max(0.0001f, oa),
                (c.g * sa + d.g / 255f * da * (1f - sa)) / Mathf.Max(0.0001f, oa),
                (c.b * sa + d.b / 255f * da * (1f - sa)) / Mathf.Max(0.0001f, oa),
                oa);

            m_Px[i] = r;
        }

        public void Circle(Vector2 c, float r, Color col)
        {
            int x0 = Mathf.FloorToInt(c.x - r), x1 = Mathf.CeilToInt(c.x + r);
            int y0 = Mathf.FloorToInt(c.y - r), y1 = Mathf.CeilToInt(c.y + r);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) <= r) Blend(x, y, col);
        }

        public void Ring(Vector2 c, float r, float t, Color col)
        {
            float inner = r - t * 0.5f, outer = r + t * 0.5f;
            int x0 = Mathf.FloorToInt(c.x - outer), x1 = Mathf.CeilToInt(c.x + outer);
            int y0 = Mathf.FloorToInt(c.y - outer), y1 = Mathf.CeilToInt(c.y + outer);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    if (d >= inner && d <= outer) Blend(x, y, col);
                }
        }

        public void Line(Vector2 a, Vector2 b, float w, Color col)
        {
            float r = w * 0.5f;
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - r), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + r);
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - r), y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + r);

            Vector2 ab = b - a;
            float abLen2 = Mathf.Max(0.0001f, ab.sqrMagnitude);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLen2);
                    Vector2 proj = a + ab * t;
                    if (Vector2.Distance(p, proj) <= r) Blend(x, y, col);
                }
        }

        public void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            Poly(new[] { a, b, c }, 0f, col);
        }

        /// <summary>Chi ve duong vien cua da giac (khong to dac) — dung cho icon dang line-art.</summary>
        public void PolyOutline(Vector2[] pts, float strokeWidth, Color col)
        {
            if (pts == null || pts.Length < 2) return;
            for (int i = 0; i < pts.Length; i++) Line(pts[i], pts[(i + 1) % pts.Length], strokeWidth, col);
        }

        public void Poly(Vector2[] pts, float strokeWidth, Color col)
        {
            if (pts == null || pts.Length < 3) return;

            float minX = pts[0].x, maxX = pts[0].x, minY = pts[0].y, maxY = pts[0].y;
            for (int i = 1; i < pts.Length; i++)
            {
                minX = Mathf.Min(minX, pts[i].x);
                maxX = Mathf.Max(maxX, pts[i].x);
                minY = Mathf.Min(minY, pts[i].y);
                maxY = Mathf.Max(maxY, pts[i].y);
            }

            float pad = strokeWidth;
            int x0 = Mathf.FloorToInt(minX - pad), x1 = Mathf.CeilToInt(maxX + pad);
            int y0 = Mathf.FloorToInt(minY - pad), y1 = Mathf.CeilToInt(maxY + pad);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    bool inside = Inside(pts, p);
                    if (inside) Blend(x, y, col);
                }

            if (strokeWidth > 0f)
            {
                for (int i = 0; i < pts.Length; i++) Line(pts[i], pts[(i + 1) % pts.Length], strokeWidth, col);
            }
        }

        private static bool Inside(Vector2[] pts, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                if (((pts[i].y > p.y) != (pts[j].y > p.y)) &&
                    (p.x < (pts[j].x - pts[i].x) * (p.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public void Arc(Vector2 c, float r, float fromDeg, float toDeg, float w, Color col)
        {
            int steps = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(toDeg - fromDeg) / 6f));
            Vector2 prev = c + Polar(r, fromDeg);
            for (int i = 1; i <= steps; i++)
            {
                float deg = Mathf.Lerp(fromDeg, toDeg, i / (float)steps);
                Vector2 cur = c + Polar(r, deg);
                Line(prev, cur, w, col);
                prev = cur;
            }
        }

        private static Vector2 Polar(float r, float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }
    }

    private static void FillRoundRect(Color32[] px, int w, int h, float x0, float y0, float x1, float y1,
        float radius, System.Func<float, float, float, Color> shader)
    {
        for (int y = Mathf.FloorToInt(y0); y < Mathf.CeilToInt(y1); y++)
        {
            for (int x = Mathf.FloorToInt(x0); x < Mathf.CeilToInt(x1); x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) continue;

                float px2 = x + 0.5f, py2 = y + 0.5f;
                float cx = Mathf.Clamp(px2, x0 + radius, x1 - radius);
                float cy = Mathf.Clamp(py2, y0 + radius, y1 - radius);
                float d = Vector2.Distance(new Vector2(px2, py2), new Vector2(cx, cy));
                if (d > radius) continue;

                float t = Mathf.InverseLerp(y0, y1, y);
                Color c = shader(x, y, t);
                int i = y * w + x;
                Color32 cur = px[i];
                if (cur.a == 0)
                {
                    px[i] = c;
                }
                else
                {
                    Color dst = cur;
                    float a = c.a + (cur.a / 255f) * (1f - c.a);
                    px[i] = new Color(
                        (c.r * c.a + dst.r * (cur.a / 255f) * (1f - c.a)) / Mathf.Max(0.0001f, a),
                        (c.g * c.a + dst.g * (cur.a / 255f) * (1f - c.a)) / Mathf.Max(0.0001f, a),
                        (c.b * c.a + dst.b * (cur.a / 255f) * (1f - c.a)) / Mathf.Max(0.0001f, a),
                        a);
                }
            }
        }
    }

    private static void StrokeRoundRect(Color32[] px, int w, int h, float x0, float y0, float x1, float y1,
        float radius, float width, Color col)
    {
        float half = width * 0.5f;
        for (int y = Mathf.FloorToInt(y0 - width); y < Mathf.CeilToInt(y1 + width); y++)
        {
            for (int x = Mathf.FloorToInt(x0 - width); x < Mathf.CeilToInt(x1 + width); x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) continue;

                float px2 = x + 0.5f, py2 = y + 0.5f;
                float cx = Mathf.Clamp(px2, x0 + radius, x1 - radius);
                float cy = Mathf.Clamp(py2, y0 + radius, y1 - radius);
                float d = Vector2.Distance(new Vector2(px2, py2), new Vector2(cx, cy));
                if (Mathf.Abs(d - radius) > half) continue;
                if (px2 < x0 - width || px2 > x1 + width || py2 < y0 - width || py2 > y1 + width) continue;

                int i = y * w + x;
                Color32 dst = px[i];
                float a = col.a + (dst.a / 255f) * (1f - col.a);
                px[i] = new Color(
                    (col.r * col.a + dst.r / 255f * (dst.a / 255f) * (1f - col.a)) / Mathf.Max(0.0001f, a),
                    (col.g * col.a + dst.g / 255f * (dst.a / 255f) * (1f - col.a)) / Mathf.Max(0.0001f, a),
                    (col.b * col.a + dst.b / 255f * (dst.a / 255f) * (1f - col.a)) / Mathf.Max(0.0001f, a),
                    a);
            }
        }
    }

    // ---------------------------------------------------------------- import + gan

    private static void ConfigureImporters()
    {
        string[] folders =
        {
            IconRoot + "/BlackShadow", IconRoot + "/GreenFox", IconRoot + "/WaterB",
            IconRoot + "/RedNova", IconRoot + "/WallTitan", IconRoot + "/Leviathan"
        };

        foreach (string folder in folders)
        {
            if (!Directory.Exists(folder)) continue;

            foreach (string file in Directory.GetFiles(folder, "*.png"))
            {
                string assetPath = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }
    }

    [MenuItem("Tools/Iron Wasteland/Assign Skill Icons To Tanks")]
    public static void AssignOnly()
    {
        for (int s = 0; s < Specs.Length; s++) Assign(Specs[s]);
        AssetDatabase.SaveAssets();
        Debug.Log("[SKILL-ICON] da gan lai icon cho 6 TankDefinition");
    }

    private static void Assign(Spec spec)
    {
        TankDefinition def = AssetDatabase.LoadAssetAtPath<TankDefinition>(spec.Asset);
        if (def == null)
        {
            Debug.LogWarning("[SKILL-ICON] khong thay " + spec.Asset);
            return;
        }

        var so = new SerializedObject(def);
        SerializedProperty ready = so.FindProperty("skillIcons");
        SerializedProperty cool = so.FindProperty("skillIconsCooldown");
        SerializedProperty names = so.FindProperty("skillNames");
        SerializedProperty glyphs = so.FindProperty("skillGlyphs");
        ready.arraySize = 4;
        cool.arraySize = 4;
        names.arraySize = 4;
        glyphs.arraySize = 4;

        for (int slot = 0; slot < 4; slot++)
        {
            string baseName = $"{slot + 1:00}_{Keys[slot]}_{spec.Slugs[slot]}";
            string dir = IconRoot + "/" + spec.Tank;

            Sprite r = AssetDatabase.LoadAssetAtPath<Sprite>(dir + "/" + baseName + "_ready.png");
            Sprite c = AssetDatabase.LoadAssetAtPath<Sprite>(dir + "/" + baseName + "_cooldown.png");

            ready.GetArrayElementAtIndex(slot).objectReferenceValue = r;
            cool.GetArrayElementAtIndex(slot).objectReferenceValue = c;
            names.GetArrayElementAtIndex(slot).stringValue = spec.Names[slot];

            // Xe co art bieu tuong rieng (Black Shadow, Leviathan) thi dung anh do cho net;
            // cac xe con lai dung bieu tuong vector trong suot vua sinh.
            Sprite g = ArtIcons.TryGetValue(spec.Tank, out string[] art)
                ? AssetDatabase.LoadAssetAtPath<Sprite>(art[slot])
                : AssetDatabase.LoadAssetAtPath<Sprite>(dir + "/" + baseName + "_glyph.png");

            glyphs.GetArrayElementAtIndex(slot).objectReferenceValue = g;

            if (r == null) Debug.LogWarning($"[SKILL-ICON] thieu ready {spec.Tank}/{baseName}");
            if (g == null) Debug.LogWarning($"[SKILL-ICON] thieu glyph {spec.Tank}/{baseName}");
        }

        // Moi the icon deu co san nhan phim + ten ky nang trong anh.
        so.FindProperty("skillIconsIncludeLabels").boolValue = true;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(def);
    }
}
#endif
