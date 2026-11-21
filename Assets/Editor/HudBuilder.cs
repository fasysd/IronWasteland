#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using IronWasteland.UI;

/// <summary>
/// Dung lai prefab GameplayHub tu bo art da tach san trong
/// Assets/Art/UI/GameplayHUD/Deconstructed_Components (anh goc: docs/figma_wireframes + mockup HUD).
///
/// Moi panel duoc dung o KICH THUOC GOC cua anh roi dat localScale = 0.5,
/// nen toa do ben trong panel dung thang pixel cua anh thiet ke.
///
/// Chay: Tools > Iron Wasteland > Rebuild GameplayHub,
/// sau do Tools > Iron Wasteland > Refresh HUD In Scenes (KHONG o play mode).
/// </summary>
public static class HudBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/StageHUD.prefab";
    private const string ArtRoot = "Assets/Art/UI/GameplayHUD";
    private const string Deco = ArtRoot + "/Deconstructed_Components";
    private const string GeneratedDir = "Assets/Art/UI/Generated";

    // Font cua HUD: Barlow Condensed Bold (OFL, co dau tieng Viet).
    // Do tu file thiet ke: "BLACK SHADOW" rong 118px voi chieu cao chu 15px => ty le 7.87;
    // Barlow Condensed Bold dung ty le 7.87 (Roboto Bold 10.58, Liberation 11.58, Oswald 7.18).
    // Font mac dinh cua TMP la Liberation Sans nen chu rong, tran ra ngoai khung.
    private const string HudFontTtf = "Assets/Art/UI/Fonts/BarlowCondensed-Bold.ttf";
    private const string HudFontAssetPath = "Assets/Art/UI/Fonts/HudFont_BarlowCondensed.asset";

    // Ky tu duoc "nuong" san vao atlas tinh: ASCII + Latin-1 + cac ky tu rieng cua tieng Viet
    // + vai ky hieu HUD dung (mui ten minimap, gach ngang dai).
    private const string HudSymbolChars = "↑↓←→—–…•°×";

    private static TMP_FontAsset s_HudFont;

    /// <summary>Bo ky tu co dinh cua HUD (atlas tinh: khong sinh them glyph khi chay).</summary>
    private static string HudCharacterSet()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(400);
        for (char c = (char)32; c < (char)127; c++) sb.Append(c);
        for (char c = (char)0x00A0; c <= (char)0x00FF; c++) sb.Append(c);
        sb.Append("ĂăĐđĨĩŨũƠơƯư");
        for (char c = (char)0x1EA0; c <= (char)0x1EF9; c++) sb.Append(c);
        sb.Append(HudSymbolChars);
        return sb.ToString();
    }

    /// <summary>Font dung cho toan bo chu tren HUD (tu sinh TMP font asset neu chua co).</summary>
    private static TMP_FontAsset HudFont
    {
        get
        {
            if (s_HudFont != null) return s_HudFont;

            s_HudFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HudFontAssetPath);
            if (s_HudFont != null) return s_HudFont;

            Font source = AssetDatabase.LoadAssetAtPath<Font>(HudFontTtf);
            if (source != null)
            {
                // Atlas TINH (Static): nuong san du ky tu tieng Viet ngay khi tao, nen file .asset
                // khong bi Unity sua lai moi lan chay -> commit len git khong sinh diff rac.
                // Phai tao o che do Dynamic (TMP chi cho them glyph khi con Dynamic) roi
                // chuyen sang Static truoc khi ghi file.
                TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(source, 48, 4, GlyphRenderMode.SDFAA,
                    1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (created != null)
                {
                    string missing;
                    created.TryAddCharacters(HudCharacterSet(), out missing, false);
                    if (!string.IsNullOrEmpty(missing))
                        Debug.LogWarning("[HUD-BUILD] font thieu ky tu: " + missing);

                    created.atlasPopulationMode = AtlasPopulationMode.Static;
                    created.name = "HudFont_BarlowCondensed";

                    // Barlow khong co mui ten (minimap "N ↑") -> cho muon glyph tu font du phong.
                    TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                    if (fallback != null)
                        created.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
                    AssetDatabase.CreateAsset(created, HudFontAssetPath);

                    if (created.atlasTextures != null && created.atlasTextures.Length > 0 && created.atlasTextures[0] != null)
                        AssetDatabase.AddObjectToAsset(created.atlasTextures[0], HudFontAssetPath);
                    if (created.material != null)
                        AssetDatabase.AddObjectToAsset(created.material, HudFontAssetPath);

                    AssetDatabase.SaveAssets();
                    s_HudFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HudFontAssetPath);
                    Debug.Log("[HUD-BUILD] tao font tinh " + HudFontAssetPath + " (" + (created.atlasTextures != null ? created.atlasTextures.Length : 0) + " atlas)");
                }
            }

            if (s_HudFont == null)
            {
                Debug.LogWarning("[HUD-BUILD] khong tao duoc font " + HudFontTtf + " - dung font mac dinh TMP");
                s_HudFont = TMP_Settings.defaultFontAsset;
            }
            return s_HudFont;
        }
    }

    private static readonly Color TextMain = new Color32(0xEA, 0xF2, 0xFF, 0xFF);
    private static readonly Color TextMuted = new Color32(0x8F, 0xA6, 0xBF, 0xFF);
    private static readonly Color TextDim = new Color32(0x5C, 0x6E, 0x82, 0xFF);
    private static readonly Color Accent = new Color32(0x4C, 0x82, 0xFF, 0xFF);
    private static readonly Color Green = new Color32(0x00, 0xE6, 0x76, 0xFF);
    private static readonly Color Amber = new Color32(0xFF, 0xD5, 0x4F, 0xFF);
    private static readonly Color Red = new Color32(0xFF, 0x52, 0x52, 0xFF);
    private static readonly Color Cyan = new Color32(0x3B, 0xD6, 0xFF, 0xFF);
    private static readonly Color PanelBg = new Color32(0x0B, 0x11, 0x18, 0xFF);

    // Bang mau lay tu file thiet ke game_ui_layout.svg (xem docs/_svg_probe.txt).
    private static readonly Color PanelTop = new Color32(0x15, 0x1C, 0x28, 0xFF);
    private static readonly Color PanelBottom = new Color32(0x08, 0x0D, 0x13, 0xFF);
    private static readonly Color Line = new Color32(0x29, 0x35, 0x44, 0xFF);
    private static readonly Color BarTrack = new Color32(0x15, 0x1D, 0x29, 0xFF);
    private static readonly Color HpRed = new Color32(0xFF, 0x3B, 0x4E, 0xFF);
    private static readonly Color Cyan2 = new Color32(0x2F, 0xE8, 0xE0, 0xFF);
    private static readonly Color Orange = new Color32(0xFF, 0x7A, 0x18, 0xFF);
    private static readonly Color Purple = new Color32(0x8A, 0x4B, 0xF0, 0xFF);
    private static readonly Color TextHead = new Color32(0xE6, 0xED, 0xF5, 0xFF);
    private static readonly Color TextSoft = new Color32(0x8A, 0x99, 0xAB, 0xFF);
    private static readonly Color BossFill = new Color32(0x10, 0x15, 0x1E, 0xFF);
    private static readonly Color BossText = new Color32(0xFF, 0x8A, 0x80, 0xFF);

    private static readonly Vector2 A_TopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 A_TopCenter = new Vector2(0.5f, 1f);
    private static readonly Vector2 A_TopRight = new Vector2(1f, 1f);
    private static readonly Vector2 A_BottomLeft = new Vector2(0f, 0f);
    private static readonly Vector2 A_BottomRight = new Vector2(1f, 0f);
    private static readonly Vector2 A_Middle = new Vector2(0.5f, 0.5f);

    // ------------------------------------------------------------------ menu

    [MenuItem("Tools/Iron Wasteland/Rebuild GameplayHub")]
    public static void Rebuild()
    {
        ConfigureArtImporters();

        GameObject root = new GameObject("StageHUD",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        StageHUD hud = root.AddComponent<StageHUD>();
        TankStatusHUD status = root.AddComponent<TankStatusHUD>();
        MinimapUI minimap = root.AddComponent<MinimapUI>();
        UnitNameplates plates = root.AddComponent<UnitNameplates>();

        BuildScreenDecor(root.transform);

        // Nhãn tên nổi trên đầu đơn vị: tạo sớm để nằm DƯỚI các panel HUD và dưới 2 lớp phủ
        // (kết quả / cài đặt), tránh chữ quái đè lên hộp thoại.
        RectTransform plateRoot = Stretch("Nameplates", root.transform);
        Set(plates, "container", plateRoot);

        RectTransform vitals = BuildVitals(root.transform, out RectTransform portrait, out TMP_Text level,
            out TMP_Text name, out Image hpFill, out TMP_Text hpText, out Image enFill, out TMP_Text enText,
            out TMP_Text stats);

        BuildStatusStrip(root.transform, out TMP_Text stageTitle, out TMP_Text waveText,
            out TMP_Text objectiveText, out TMP_Text killsText, out TMP_Text timerText, out TMP_Text gemsText);

        RectTransform boss = BuildBoss(root.transform, out TMP_Text bossName, out TMP_Text bossHp, out Image bossFill);
        RectTransform minimapPanel = BuildMinimap(root.transform, minimap, out Image mapBackground, out Image mapGrid, out Image viewFrame);
        RectTransform pauseButton = BuildPauseButton(root.transform);

        BuildSkills(root.transform, out TMP_Text[] skillNames, out TMP_Text[] skillStates,
            out Image[] skillGlyphs, out Image[] skillDims, out Image[] skillCards);

        BuildCoop(root.transform);

        GameObject result = BuildResult(root.transform, out TMP_Text resultTitle, out TMP_Text resultDetail,
            out Button nextButton, out TMP_Text nextLabel, out Button retryButton, out Button selectButton);

        GameObject settings = BuildSettings(root.transform, out Button settingsButton, out Slider masterSlider,
            out Slider sfxSlider, out Button resumeButton, out Button leaveButton);

        // ------------------------------------------------ gan field cho script
        Assign(hud, "director", null);
        Set(hud, "stageTitle", stageTitle);
        Set(hud, "waveText", waveText);
        Set(hud, "objectiveText", objectiveText);
        Set(hud, "killsText", killsText);
        Set(hud, "timerText", timerText);
        Set(hud, "gemsText", gemsText);
        Set(hud, "bossPanel", boss.gameObject);
        Set(hud, "bossNameText", bossName);
        Set(hud, "bossHpText", bossHp);
        Set(hud, "bossFill", bossFill);
        Set(hud, "settingsPanel", settings);
        Set(hud, "settingsButton", settingsButton);
        Set(hud, "masterSlider", masterSlider);
        Set(hud, "sfxSlider", sfxSlider);
        Set(hud, "resumeButton", resumeButton);
        Set(hud, "leaveButton", leaveButton);
        Set(hud, "resultPanel", result);
        Set(hud, "resultTitle", resultTitle);
        Set(hud, "resultDetail", resultDetail);
        Set(hud, "nextButton", nextButton);
        Set(hud, "nextButtonLabel", nextLabel);
        Set(hud, "retryButton", retryButton);
        Set(hud, "selectButton", selectButton);

        Set(status, "portraitImage", portrait.GetComponent<Image>());
        Set(status, "levelText", level);
        Set(status, "tankNameText", name);
        Set(status, "healthFill", hpFill);
        Set(status, "healthText", hpText);
        Set(status, "energyFill", enFill);
        Set(status, "energyText", enText);
        Set(status, "statsText", stats);
        SetArray(status, "skillNameTexts", skillNames);
        SetArray(status, "skillStateTexts", skillStates);
        SetArray(status, "skillIconImages", skillCards);
        SetArray(status, "skillGlyphImages", skillGlyphs);
        SetArray(status, "skillCooldownOverlays", skillDims);

        Set(minimap, "panelRoot", minimapPanel.gameObject);
        Set(minimap, "mapView", minimapPanel.Find("MapView") as RectTransform);
        Set(minimap, "mapBackground", mapBackground);
        Set(minimap, "mapGrid", mapGrid);
        Set(minimap, "viewFrame", viewFrame);
        SetSprite(minimap, "fallbackBackground", Deco + "/03_Minimap_Elements/minimap_cave_terrain_map.png");
        SetSprite(minimap, "blipPlayer", Deco + "/03_Minimap_Elements/minimap_blip_player_tank.png");
        SetSprite(minimap, "blipEnemy", Deco + "/03_Minimap_Elements/minimap_blip_enemy_minion.png");
        SetSprite(minimap, "blipBoss", Deco + "/03_Minimap_Elements/minimap_blip_boss.png");
        SetSprite(minimap, "blipTeammate", Deco + "/03_Minimap_Elements/minimap_blip_teammate_tank.png");

        if (pauseButton != null)
        {
            Button b = pauseButton.GetComponent<Button>();
            if (b != null) Set(hud, "settingsButton", b);
        }

        result.SetActive(false);
        settings.SetActive(false);
        boss.gameObject.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("[HUD-BUILD] da dung lai " + PrefabPath + " (art Deconstructed_Components)");
    }

    // ------------------------------------------------------------------ panels

    private static void BuildScreenDecor(Transform parent)
    {
        Image border = Img("ScreenBorder", parent, Deco + "/05_Screen_Frame_And_Ornaments/screen_border_scrim_overlay.png",
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
        border.raycastTarget = false;

        string[] corners = { "top_left", "top_right", "bottom_left", "bottom_right" };
        for (int i = 0; i < corners.Length; i++)
        {
            string c = corners[i];
            bool right = c.Contains("right");
            bool top = c.Contains("top");
            Vector2 anchor = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
            Vector2 pos = new Vector2(right ? -6f : 6f, top ? -6f : 6f);
            Image img = Img("Corner_" + c, parent, Deco + "/05_Screen_Frame_And_Ornaments/screen_corner_bracket_" + c + ".png",
                anchor, pos, new Vector2(100f, 100f));
            img.raycastTarget = false;
        }
    }

    private static RectTransform BuildVitals(Transform parent, out RectTransform portrait, out TMP_Text level,
        out TMP_Text name, out Image hpFill, out TMP_Text hpText, out Image enFill, out TMP_Text enText,
        out TMP_Text stats)
    {
        // Toa do lay dung theo SVG: khung 425.6 x 140.8 tai (51, 48), cat cheo CA HAI goc phai 19 px.
        Vector2 size = new Vector2(425.6f, 140.8f);
        RectTransform panel = Panel("TopLeft", parent, A_TopLeft, new Vector2(51f, -48f), size, 1f);
        Shape("panel_player", panel, size, 19, 0, new Color(Purple.r, Purple.g, Purple.b, 0.501961f), 1.6f, 3);

        portrait = Node("Avatar", panel, 4f, -12f, 92.7f, 103.1f);
        Image portraitImg = portrait.gameObject.AddComponent<Image>();
        portraitImg.sprite = Load(Deco + "/02_Panel_Frames_And_Bars/avatar_player_black_shadow.png");
        portraitImg.preserveAspect = false;
        portraitImg.raycastTarget = false;

        name = Txt("TankName", panel, 96f, -14f, 240f, 24f, "BLACK SHADOW", 22f, TextHead,
            TextAlignmentOptions.Left, FontStyles.Bold);
        level = Txt("LevelText", panel, 211f, -14f, 190f, 24f, "CẤP 1", 12f, TextSoft, TextAlignmentOptions.Right);

        Txt("HpLabel", panel, 96f, -44f, 60f, 18f, "MÁU", 11f, TextSoft, TextAlignmentOptions.Left);
        hpText = Txt("HpText", panel, 176f, -40f, 200f, 20f, "0 / 0", 14f, TextHead,
            TextAlignmentOptions.Right, FontStyles.Bold);

        Bar("HpTrack", panel, 96f, -60f, 308.8f, 15.2f, BarTrack);
        hpFill = Bar("HpFill", panel, 96f, -60f, 308.8f, 15.2f, HpRed, true);

        Txt("EnLabel", panel, 96f, -85f, 90f, 18f, "NĂNG LƯỢNG", 11f, TextSoft, TextAlignmentOptions.Left);
        enText = Txt("EnText", panel, 176f, -84f, 200f, 20f, "0 / 0", 13f, Cyan2,
            TextAlignmentOptions.Right, FontStyles.Bold);

        Bar("EnTrack", panel, 96f, -102.4f, 308.8f, 9.6f, BarTrack);
        enFill = Bar("EnFill", panel, 96f, -102.4f, 308.8f, 9.6f, Cyan2, true);

        // Dong chi so nho chay suot duoi panel, bat dau tu mep trai (di duoi avatar) nhu SVG.
        stats = Txt("StatsText", panel, 17f, -117f, 390f, 16f, "0 ATK   /   0 DEF   /   TỐC ĐỘ 0", 9.5f,
            TextDim, TextAlignmentOptions.Left);
        return panel;
    }

    /// <summary>Dải thông tin màn chơi (ải, đợt, mục tiêu, ngọc, giờ, diệt) đặt dưới panel người chơi.</summary>
    private static void BuildStatusStrip(Transform parent, out TMP_Text stageTitle, out TMP_Text waveText,
        out TMP_Text objectiveText, out TMP_Text killsText, out TMP_Text timerText, out TMP_Text gemsText)
    {
        Vector2 size = new Vector2(425.6f, 96f);
        RectTransform strip = Panel("TopLeftInfo", parent, A_TopLeft, new Vector2(51f, -196f), size, 1f);
        Shape("panel_status_strip", strip, size, 0, 6, Line, 1f, 0);

        stageTitle = Txt("StageTitle", strip, 12f, -6f, 300f, 24f, "1-1 - Tên ải", 18f, Amber,
            TextAlignmentOptions.Left, FontStyles.Bold);
        timerText = Txt("TimerText", strip, 300f, -6f, 114f, 24f, "00:00", 18f, TextHead,
            TextAlignmentOptions.Right, FontStyles.Bold);
        waveText = Txt("WaveText", strip, 12f, -32f, 150f, 20f, "Đợt 1 / 3", 13f, Cyan2, TextAlignmentOptions.Left);
        objectiveText = Txt("ObjectiveText", strip, 162f, -32f, 252f, 20f, "Mục tiêu", 13f, TextSoft,
            TextAlignmentOptions.Left);
        killsText = Txt("KillsText", strip, 12f, -58f, 180f, 20f, "Đã diệt: 0", 12f, TextSoft, TextAlignmentOptions.Left);
        gemsText = Txt("GemsText", strip, 200f, -58f, 214f, 20f, "Ngọc: 0", 12f, Amber, TextAlignmentOptions.Right);
    }

    private static RectTransform BuildBoss(Transform parent, out TMP_Text bossName, out TMP_Text bossHp, out Image bossFill)
    {
        // SVG: khung 681.6 x 77.6 tai (620, 73) — tam man hinh nen neo giua tren.
        // Nen PHANG #10151E (khong gradient) + vien do mong 0.8, khong cat goc.
        Vector2 size = new Vector2(681.6f, 77.6f);
        RectTransform panel = Panel("BossPanel", parent, A_TopCenter, new Vector2(0.8f, -73f), size, 1f);
        Shape("panel_boss", panel, size, 0, 0, BossFill, BossFill, HpRed, 0.8f, 0);

        // Ca hai ve deu mau hong nhat #FF8A80 nhu file thiet ke (khong tach "BOSS:" mau do).
        Txt("BossTag", panel, 10f, -6f, 60f, 18f, "BOSS:", 13f, BossText, TextAlignmentOptions.Left, FontStyles.Bold);
        bossName = Txt("BossName", panel, 72f, -6f, 380f, 18f, "Tên boss", 13f, BossText,
            TextAlignmentOptions.Left, FontStyles.Bold);
        bossHp = Txt("BossHpText", panel, 431f, -6f, 200f, 18f, "0 / 0 HP", 13f, TextHead,
            TextAlignmentOptions.Right, FontStyles.Bold);

        Bar("BossTrack", panel, 11.3f, -39.6f, 659f, 22f, new Color32(0x3E, 0x18, 0x21, 0xFF));
        bossFill = Bar("BossBar", panel, 11.3f, -39.6f, 659f, 22f, HpRed, true);
        return panel;
    }

    private static RectTransform BuildMinimap(Transform parent, MinimapUI minimap,
        out Image mapBackground, out Image mapGrid, out Image viewFrame)
    {
        // SVG: khung 252 x 219.6, cat cheo CA HAI goc phai 21.7, cach le phai 43.9 / le tren 30.4.
        Vector2 size = new Vector2(252f, 219.6f);
        RectTransform panel = Panel("TopRight", parent, A_TopRight, new Vector2(-43.9f, -30.4f), size, 1f);
        Shape("panel_minimap", panel, size, 22, 0, Line, 1.8f, 3);

        Txt("MapTitle", panel, 14f, -21f, 220f, 16f, "BÀN ĐỒ CHIẾN THUẬT", 12f, TextHead,
            TextAlignmentOptions.Left, FontStyles.Bold);
        Txt("NorthMark", panel, 195f, -21f, 43f, 16f, "N ↑", 12f, Cyan2, TextAlignmentOptions.Right);

        RectTransform mapView = Node("MapView", panel, 24.3f, -52.2f, 223.2f, 155.7f);
        mapView.gameObject.AddComponent<RectMask2D>();

        // Nền: lúc dựng tạm dùng ảnh terrain của bộ art, khi chạy MinimapUI thay bằng nền thật của màn.
        mapBackground = Img("StageBackground", mapView, Deco + "/03_Minimap_Elements/minimap_cave_terrain_map.png",
            Vector2.zero, Vector2.zero, new Vector2(223.2f, 155.7f));

        // Lưới riêng (bản gốc minimap_radar_grid có vẽ sẵn một khung xanh đứng yên nên không dùng được).
        mapGrid = Img("Grid", mapView, EnsureMinimapGridSprite(),
            Vector2.zero, Vector2.zero, new Vector2(223.2f, 155.7f));
        mapGrid.color = new Color(1f, 1f, 1f, 0.55f);

        // Khung xanh = vùng camera đang nhìn, MinimapUI cập nhật theo camera mỗi frame.
        RectTransform frameRt = Node("ViewFrame", mapView, 0f, 0f, 120f, 70f);
        viewFrame = frameRt.gameObject.AddComponent<Image>();
        viewFrame.raycastTarget = false;
        viewFrame.type = Image.Type.Sliced;
        viewFrame.sprite = UISprites.Frame(6, 2);
        viewFrame.color = new Color(0.36f, 0.86f, 1f, 0.95f);

        return panel;
    }

    /// <summary>
    /// Sinh sprite lưới bản đồ (chỉ lưới, không có khung xanh đứng yên như ảnh gốc).
    /// </summary>
    private static string EnsureMinimapGridSprite()
    {
        const int width = 428;
        const int height = 312;
        const string path = GeneratedDir + "/minimap_grid_only.png";

        if (!System.IO.Directory.Exists(GeneratedDir)) System.IO.Directory.CreateDirectory(GeneratedDir);

        if (!System.IO.File.Exists(path) || new System.IO.FileInfo(path).Length == 0)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            var line = new Color(0.36f, 0.86f, 1f, 0.22f);

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, clear);

            for (int i = 1; i < 8; i++)
            {
                int x = Mathf.RoundToInt(i * (width - 1) / 8f);
                for (int y = 0; y < height; y++) texture.SetPixel(x, y, line);
            }

            for (int i = 1; i < 6; i++)
            {
                int y = Mathf.RoundToInt(i * (height - 1) / 6f);
                for (int x = 0; x < width; x++) texture.SetPixel(x, y, line);
            }

            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return path;
    }

    private static RectTransform BuildPauseButton(Transform parent)
    {
        // SVG: nut 173 x 47 (phan phang 149 + mui nhon 24) o goc duoi-phai, cach le phai 3 / le duoi 218.
        Vector2 size = new Vector2(173f, 47f);
        RectTransform rt = Node("PauseButton", parent, -3f, 218f, size.x, size.y, A_BottomRight);

        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = PanelAsset("panel_esc", 173, 47, 24, 0, PanelTop, PanelBottom, Line, 2f, 3);
        img.raycastTarget = true;

        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;

        // Chu mot dong "ESC / TẠM DỪNG", can giua phan phang cua nut nhu SVG.
        Txt("Label", rt, 0f, -11.5f, 149f, 24f, "ESC / TẠM DỪNG", 14f, TextHead,
            TextAlignmentOptions.Center, FontStyles.Bold);
        return rt;
    }

    private static void BuildSkills(Transform parent, out TMP_Text[] names, out TMP_Text[] states,
        out Image[] glyphs, out Image[] dims, out Image[] cards)
    {
        // SVG: 4 the 124.5 x 115.5, buoc 133.5, hang duoi cach day 88 px, le phai 42 px.
        RectTransform panel = Panel("BottomRight", parent, A_BottomRight, new Vector2(-42f, 88f),
            new Vector2(525f, 115.5f), 1f);

        Color[] borderColors = { Orange, Cyan2, Purple, Purple };
        string[] keys = { "J", "K", "L", "I" };
        float[] cardX = { 0f, 133.5f, 267f, 400.5f };

        // Tieu de nhom nam NGAY TREN hang the (SVG: chu nho mau xam, cach dinh the 10 px).
        // y duong = nam phia TREN dinh panel (panel nay neo goc duoi-phai).
        TMP_Text groupLabel = Txt("GroupLabel", panel, 3f, 25f, 260f, 16f, "KỸ NĂNG TRANG BỊ", 10f, TextSoft,
            TextAlignmentOptions.Left, FontStyles.Bold);
        groupLabel.characterSpacing = 1f;

        names = new TMP_Text[4];
        states = new TMP_Text[4];
        glyphs = new Image[4];
        dims = new Image[4];
        cards = new Image[4];

        for (int i = 0; i < 4; i++)
        {
            RectTransform card = Node("Slot" + i, panel, cardX[i], 0f, 124.5f, 115.5f);

            // Nen the: cat cheo CA HAI goc phai 18 px, vien mau theo phim (cam / cyan / tim / tim).
            RectTransform backingRt = Node("Backing", card, 0f, 0f, 124.5f, 115.5f);
            Image backing = backingRt.gameObject.AddComponent<Image>();
            backing.sprite = PanelAsset("panel_skill_" + i, 125, 116, 18, 0, PanelTop, PanelBottom,
                borderColors[i], 1.5f, 3);
            backing.raycastTarget = false;

            // Art cua ky nang dat trong KHUNG ICON 57x57 (toa do trong SVG), khong phu kin the:
            // the giu nguyen nen toi + vien mau + hai goc phai cat cheo nhu thiet ke.
            RectTransform cardRt = Node("Card" + i, card, 33.8f, -12f, 57f, 57f);
            cards[i] = cardRt.gameObject.AddComponent<Image>();
            cards[i].preserveAspect = true;
            cards[i].raycastTarget = false;

            RectTransform glyphRt = Node("Icon" + i, card, 33.8f, -12f, 57f, 57f);
            glyphs[i] = glyphRt.gameObject.AddComponent<Image>();
            glyphs[i].preserveAspect = true;
            glyphs[i].raycastTarget = false;

            Txt("KeyBadge" + i, card, 6f, -4f, 20f, 18f, keys[i], 12f, borderColors[i],
                TextAlignmentOptions.Left, FontStyles.Bold);

            names[i] = Txt("Name" + i, card, 2f, -78f, 120.5f, 18f, "Tên kỹ năng", 11.5f, TextHead,
                TextAlignmentOptions.Center, FontStyles.Bold);
            states[i] = Txt("State" + i, card, 2f, -94f, 120.5f, 16f, "SẴN SÀNG", 8.5f, borderColors[i],
                TextAlignmentOptions.Center, FontStyles.Bold);
            states[i].characterSpacing = 1f;

            RectTransform dimRt = Node("Dim" + i, card, 2f, -2f, 120.5f, 111.5f);
            dims[i] = dimRt.gameObject.AddComponent<Image>();
            dims[i].sprite = Load(ArtRoot + "/Overlays/overlay_cooldown_dark_dim.png");
            dims[i].raycastTarget = false;
            dims[i].gameObject.SetActive(false);
        }
    }

    private static void BuildCoop(Transform parent)
    {
        // SVG: khung 425 x 150.6, cach le trai 51.3 / le duoi 53.1.
        Vector2 size = new Vector2(425f, 150.6f);
        RectTransform panel = Panel("BottomLeft", parent, A_BottomLeft, new Vector2(51.3f, 53.1f), size, 1f);
        Shape("panel_coop", panel, size, 0, 4, Line, 0.6f, 0);

        Txt("Title", panel, 12f, -10f, 360f, 18f, "ĐỒNG ĐỘI CO-OP (PHÒNG 1/4)", 12f, TextHead,
            TextAlignmentOptions.Left, FontStyles.Bold);

        RectTransform row = Node("TeammateRow", panel, 11.7f, -35.3f, 401.6f, 33.6f);
        Image rowBg = row.gameObject.AddComponent<Image>();
        rowBg.sprite = UISprites.Rounded(4);
        rowBg.type = Image.Type.Sliced;
        rowBg.color = new Color32(0x1B, 0x1E, 0x26, 0xFF);
        rowBg.raycastTarget = false;

        Txt("MateName", row, 10.3f, -8f, 150f, 18f, "Chưa có đồng đội", 11f, TextHead,
            TextAlignmentOptions.Left, FontStyles.Bold);

        Bar("MateTrack", row, 127.2f, -11.2f, 188f, 11.2f, new Color32(0x20, 0x24, 0x2B, 0xFF));
        Image fill = Bar("MateFill", row, 127.2f, -11.2f, 188f, 11.2f, Green, true);
        fill.fillAmount = 0f;

        Txt("MateHp", row, 300f, -8f, 94f, 18f, "0 / 0", 10f, TextHead, TextAlignmentOptions.Right);

        RectTransform row2 = Node("TeammateRow2", panel, 12f, -78.8f, 401f, 33f);
        Image rowBg2 = row2.gameObject.AddComponent<Image>();
        rowBg2.sprite = PanelAsset("panel_coop_row2", 401, 33, 0, 3, new Color32(0x17, 0x19, 0x20, 0xFF),
            new Color32(0x17, 0x19, 0x20, 0xFF), new Color32(0x28, 0x2C, 0x35, 0xFF), 0.6f, 0);
        rowBg2.raycastTarget = false;

        // SVG can giua dong trong "[ Slot 3 & 4 Trống ]" tren ca hang.
        Txt("Slot2", row2, 0f, -8f, 401f, 18f, "[ Slot 3 & 4 Trống ]", 11f, TextDim,
            TextAlignmentOptions.Center);
    }

    private static GameObject BuildResult(Transform parent, out TMP_Text title, out TMP_Text detail,
        out Button next, out TMP_Text nextLabel, out Button retry, out Button select)
    {
        RectTransform panel = Stretch("ResultPanel", parent);
        Image dim = panel.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.82f);

        RectTransform box = Node("ResultBox", panel, 0f, 0f, 960f, 540f, A_Middle);
        Image boxImg = box.gameObject.AddComponent<Image>();
        boxImg.color = new Color32(0x0B, 0x11, 0x18, 0xFF);

        title = Txt("ResultTitle", box, 0f, -30f, 960f, 64f, "CHIẾN THẮNG", 44f, Amber, TextAlignmentOptions.Center, FontStyles.Bold);
        detail = Txt("ResultDetail", box, 50f, -108f, 860f, 300f, "", 22f, TextMain, TextAlignmentOptions.Center);

        next = Btn("NextButton", box, "TIẾP", 200f, -448f, 260f, 64f, new Color32(0x1B, 0x5E, 0x3A, 0xFF));
        nextLabel = next.GetComponentInChildren<TMP_Text>(true);
        retry = Btn("RetryButton", box, "Chơi lại", 490f, -448f, 220f, 64f, new Color32(0x22, 0x2C, 0x3A, 0xFF));
        select = Btn("SelectButton", box, "Về màn chọn", 730f, -448f, 230f, 64f, new Color32(0x22, 0x2C, 0x3A, 0xFF));
        return panel.gameObject;
    }

    private static GameObject BuildSettings(Transform parent, out Button settingsButton, out Slider masterSlider,
        out Slider sfxSlider, out Button resumeButton, out Button leaveButton)
    {
        RectTransform panel = Stretch("SettingsPanel", parent);
        Image dim = panel.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.80f);
        dim.raycastTarget = true;

        RectTransform box = Node("Box", panel, 0f, 0f, 900f, 660f, A_Middle);
        Image boxImg = box.gameObject.AddComponent<Image>();
        boxImg.color = new Color32(0x0B, 0x11, 0x18, 0xFF);

        Txt("Title", box, 0f, -26f, 900f, 56f, "CÀI ĐẶT TRẬN ĐẤU", 36f, TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        Txt("Warning", box, 0f, -82f, 900f, 34f, "CHÚ Ý: GAME KHÔNG TẠM DỪNG!", 18f, Amber, TextAlignmentOptions.Center);

        Txt("MasterLabel", box, 60f, -140f, 780f, 30f, "ÂM LƯỢNG TỔNG", 18f, TextMuted, TextAlignmentOptions.Left);
        masterSlider = Slider("MasterSlider", box, 60f, -176f, 780f, 22f);

        Txt("SfxLabel", box, 60f, -216f, 780f, 30f, "ÂM THANH CHIẾN ĐẤU & MA PHÁP", 18f, TextMuted, TextAlignmentOptions.Left);
        sfxSlider = Slider("SfxSlider", box, 60f, -252f, 780f, 22f);

        Txt("ControlHeader", box, 60f, -298f, 780f, 30f, "HỆ THỐNG ĐIỀU KHIỂN & PHÍM BẤM", 18f, Amber, TextAlignmentOptions.Left);
        Txt("KeyTable", box, 60f, -330f, 780f, 96f,
            "Di chuyển xe: W - A - S - D\nHướng nòng pháo & ngắm bắn: con trỏ chuột\nMở Cài đặt: ESC   |   Bản đồ: M   |   Kỹ năng: J K L I",
            16f, TextMuted, TextAlignmentOptions.TopLeft);

        resumeButton = Btn("ResumeButton", box, "QUAY LẠI TRẬN CHIẾN (ESC)", 220f, -452f, 460f, 58f, new Color32(0x14, 0x5E, 0x5A, 0xFF));
        leaveButton = Btn("LeaveButton", box, "RỜI TRẬN (VỀ SẢNH CHÍNH)", 220f, -520f, 460f, 58f, new Color32(0x5E, 0x1E, 0x1E, 0xFF));
        Txt("CoopNote", box, 60f, -596f, 780f, 28f, "* Nếu chơi Co-op, bạn sẽ thoát khỏi phòng của đồng đội.",
            14f, TextDim, TextAlignmentOptions.Center);

        // Nut mo cai dat nam o panel Pause rieng, tra ve nut tam de gan sau.
        settingsButton = null;
        return panel.gameObject;
    }

    // ------------------------------------------------------------------ helpers

    private static RectTransform Stretch(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, float scale)
    {
        var go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        rt.localScale = new Vector3(scale, scale, 1f);
        return rt;
    }

    /// <summary>
    /// Nen panel theo bo cuc SVG: gradient doc + vien mo + co the cat cheo mot goc.
    /// </summary>
    private static Image Shape(string assetName, RectTransform panel, Vector2 size, int cut, int radius,
        Color border, float borderWidth, int cutCorner)
    {
        return Shape(assetName, panel, size, cut, radius, PanelTop, PanelBottom, border, borderWidth, cutCorner);
    }

    /// <summary>Nhu tren nhung cho phep doi mau nen (vd. panel boss dung mot mau phang).</summary>
    private static Image Shape(string assetName, RectTransform panel, Vector2 size, int cut, int radius,
        Color top, Color bottom, Color border, float borderWidth, int cutCorner)
    {
        RectTransform rt = Node("Backing", panel, 0f, 0f, size.x, size.y);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = PanelAsset(assetName, Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), cut, radius,
            top, bottom, border, borderWidth, cutCorner);
        img.raycastTarget = false;
        return img;
    }

    /// <summary>
    /// Sinh nen panel thanh file .png trong Assets/Art/UI/Generated/Panels roi import kieu Sprite.
    ///
    /// Vi sao phai lam the: sprite tao bang code luc dung prefab la object tam, KHONG luu duoc
    /// vao .prefab — khi mo lai game tham chieu thanh null va Image ve ra o vuong trang.
    /// Chi sprite la asset that moi nam trong prefab.
    /// </summary>
    private static Sprite PanelAsset(string assetName, int width, int height, int cut, int radius,
        Color top, Color bottom, Color border, float borderWidth, int cutCorner)
    {
        const string dir = GeneratedDir + "/Panels";
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

        string path = dir + "/" + assetName + ".png";

        Sprite runtime = UISprites.PanelShape(width, height, cut, radius, top, bottom, border, borderWidth, cutCorner);
        if (runtime != null && runtime.texture != null)
        {
            System.IO.File.WriteAllBytes(path, runtime.texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
        }

        Sprite asset = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (asset == null) Debug.LogWarning("[HUD-BUILD] khong tao duoc sprite panel: " + path);
        return asset;
    }

    /// <summary>Thanh mau phang: sprite vuong de Image.Type.Filled cat dung phan tram.</summary>
    private static Image Bar(string name, Transform parent, float x, float y, float w, float h, Color color, bool filled = false)
    {
        RectTransform rt = Node(name, parent, x, y, w, h);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = UISprites.Square();
        img.type = Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        if (filled) MakeFilled(img);
        return img;
    }

    /// <summary>Node con trong panel: x,y tính từ góc trên-trái của panel (y âm = đi xuống).</summary>
    private static RectTransform Node(string name, Transform parent, float x, float y, float w, float h,
        Vector2? anchorOverride = null, float scale = 1f)
    {
        Vector2 anchor = anchorOverride ?? A_TopLeft;
        var go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        if (anchorOverride.HasValue)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = new Vector2(x, y);
        }
        else
        {
            rt.anchorMin = A_TopLeft;
            rt.anchorMax = A_TopLeft;
            rt.pivot = A_TopLeft;
            rt.anchoredPosition = new Vector2(x, y);
        }

        rt.sizeDelta = new Vector2(w, h);
        if (!Mathf.Approximately(scale, 1f)) rt.localScale = new Vector3(scale, scale, 1f);
        return rt;
    }

    private static Image Img(string name, Transform parent, string path, Vector2 anchor, Vector2 pos, Vector2 size,
        Vector2? offset = null, Color? color = null, bool stretched = false)
    {
        RectTransform rt;
        if (stretched)
        {
            rt = Stretch(name, parent);
        }
        else
        {
            rt = Node(name, parent, offset.HasValue ? offset.Value.x : pos.x, offset.HasValue ? offset.Value.y : pos.y, size.x, size.y);
        }

        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = Load(path);
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.raycastTarget = false;
        if (color.HasValue) img.color = color.Value;
        return img;
    }

    private static TMP_Text Txt(string name, Transform parent, float x, float y, float w, float h,
        string content, float size, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        RectTransform rt = Node(name, parent, x, y, w, h);

        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.fontStyle = style;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;

        text.font = HudFont;
        return text;
    }

    private static Button Btn(string name, Transform parent, string label, float x, float y, float w, float h, Color fill)
    {
        RectTransform rt = Node(name, parent, x, y, w, h);

        Image img = rt.gameObject.AddComponent<Image>();
        img.color = fill;

        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;

        TMP_Text text = Txt("Label", rt, 0f, 0f, w, h, label, 22f, TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static Slider Slider(string name, Transform parent, float x, float y, float w, float h)
    {
        RectTransform rt = Node(name, parent, x, y, w, h);

        Image bg = rt.gameObject.AddComponent<Image>();
        bg.color = new Color32(0x1B, 0x24, 0x33, 0xFF);

        Slider slider = rt.gameObject.AddComponent<Slider>();

        RectTransform fillArea = Node("Fill Area", rt, 0f, 0f, w, h);
        fillArea.anchorMin = Vector2.zero;
        fillArea.anchorMax = Vector2.one;
        fillArea.offsetMin = new Vector2(6f, 6f);
        fillArea.offsetMax = new Vector2(-6f, -6f);

        RectTransform fill = Node("Fill", fillArea, 0f, 0f, w, h);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;

        Image fillImg = fill.gameObject.AddComponent<Image>();
        fillImg.color = Accent;

        RectTransform handleArea = Node("Handle Slide Area", rt, 0f, 0f, w, h);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = Vector2.zero;
        handleArea.offsetMax = Vector2.zero;

        RectTransform handle = Node("Handle", handleArea, 0f, 0f, 18f, h + 8f);
        Image handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.color = TextMain;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImg;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.8f;
        return slider;
    }

    private static void MakeFilled(Image image)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillAmount = 1f;
    }

    private static Sprite Load(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) Debug.LogWarning("[HUD-BUILD] thieu sprite: " + path);
        return sprite;
    }

    private static void Set(Object target, string property, Object value)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null)
        {
            Debug.LogWarning($"[HUD-BUILD] khong co field {property} tren {target.GetType().Name}");
            return;
        }

        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(Object target, string property, Object[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null)
        {
            Debug.LogWarning($"[HUD-BUILD] khong co mang {property} tren {target.GetType().Name}");
            return;
        }

        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSprite(Object target, string property, string path)
    {
        Set(target, property, Load(path));
    }

    private static void Assign(StageHUD hud, string property, Object value)
    {
        Set(hud, property, value);
    }

    /// <summary>Bo anh tach san phai import kieu Sprite (Single), PPU 100, khong mipmap.</summary>
    private static void ConfigureArtImporters()
    {
        string[] roots = { Deco, ArtRoot + "/Frames", ArtRoot + "/Badges", ArtRoot + "/Overlays", ArtRoot + "/SkillIcons", GeneratedDir };
        var seen = new HashSet<string>();

        foreach (string root in roots)
        {
            if (!System.IO.Directory.Exists(root)) continue;

            foreach (string file in System.IO.Directory.GetFiles(root, "*.png", System.IO.SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                if (!seen.Add(path)) continue;

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f)) { importer.spritePixelsPerUnit = 100f; changed = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                if (importer.alphaIsTransparency == false) { importer.alphaIsTransparency = true; changed = true; }

                if (changed) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
#endif
