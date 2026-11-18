#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
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

    private static readonly Color TextMain = new Color32(0xEA, 0xF2, 0xFF, 0xFF);
    private static readonly Color TextMuted = new Color32(0x8F, 0xA6, 0xBF, 0xFF);
    private static readonly Color TextDim = new Color32(0x5C, 0x6E, 0x82, 0xFF);
    private static readonly Color Accent = new Color32(0x4C, 0x82, 0xFF, 0xFF);
    private static readonly Color Green = new Color32(0x00, 0xE6, 0x76, 0xFF);
    private static readonly Color Amber = new Color32(0xFF, 0xD5, 0x4F, 0xFF);
    private static readonly Color Red = new Color32(0xFF, 0x52, 0x52, 0xFF);
    private static readonly Color Cyan = new Color32(0x3B, 0xD6, 0xFF, 0xFF);
    private static readonly Color PanelBg = new Color32(0x0B, 0x11, 0x18, 0xFF);

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
            out Image[] skillGlyphs, out Image[] skillDims);

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
        RectTransform panel = Panel("TopLeft", parent, A_TopLeft, new Vector2(26f, -26f), new Vector2(852f, 282f), 0.5f);
        Img("Plate", panel, Deco + "/02_Panel_Frames_And_Bars/frame_player_vitals_empty.png",
            Vector2.zero, Vector2.zero, new Vector2(852f, 282f));

        portrait = Node("Avatar", panel, 34f, -34f, 176f, 216f);
        Image portraitImg = portrait.gameObject.AddComponent<Image>();
        portraitImg.sprite = Load(Deco + "/02_Panel_Frames_And_Bars/avatar_player_black_shadow.png");
        portraitImg.preserveAspect = true;
        portraitImg.raycastTarget = false;

        name = Txt("TankName", panel, 215f, -36f, 430f, 48f, "BLACK SHADOW", 38f, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        level = Txt("LevelText", panel, 560f, -44f, 260f, 40f, "CẤP 1", 22f, TextMuted, TextAlignmentOptions.Right);

        Txt("HpLabel", panel, 215f, -92f, 240f, 26f, "MÁU", 17f, TextMuted, TextAlignmentOptions.Left);
        hpText = Txt("HpText", panel, 560f, -84f, 260f, 38f, "0 / 0", 26f, TextMain, TextAlignmentOptions.Right, FontStyles.Bold);

        Img("HpTrack", panel, Deco + "/02_Panel_Frames_And_Bars/bar_track_health_empty.png",
            Vector2.zero, Vector2.zero, new Vector2(595f, 30f), offset: new Vector2(215f, -124f));
        hpFill = Img("HpFill", panel, Deco + "/02_Panel_Frames_And_Bars/bar_fill_player_health_red.png",
            Vector2.zero, Vector2.zero, new Vector2(595f, 30f), offset: new Vector2(215f, -124f));
        MakeFilled(hpFill);

        Txt("EnLabel", panel, 215f, -172f, 300f, 26f, "NĂNG LƯỢNG", 17f, TextMuted, TextAlignmentOptions.Left);
        enText = Txt("EnText", panel, 560f, -164f, 260f, 36f, "0 / 0", 24f, Cyan, TextAlignmentOptions.Right, FontStyles.Bold);

        Img("EnTrack", panel, Deco + "/02_Panel_Frames_And_Bars/bar_track_health_empty.png",
            Vector2.zero, Vector2.zero, new Vector2(595f, 26f), offset: new Vector2(215f, -204f));
        enFill = Img("EnFill", panel, Deco + "/02_Panel_Frames_And_Bars/bar_fill_player_energy_cyan.png",
            Vector2.zero, Vector2.zero, new Vector2(595f, 26f), offset: new Vector2(215f, -204f));
        MakeFilled(enFill);

        stats = Txt("StatsText", panel, 36f, -244f, 780f, 28f, "0 ATK   /   0 DEF   /   TỐC ĐỘ 0", 17f,
            TextMuted, TextAlignmentOptions.Left);
        return panel;
    }

    /// <summary>Dải thông tin màn chơi (ải, đợt, mục tiêu, ngọc, giờ, diệt) đặt dưới panel người chơi.</summary>
    private static void BuildStatusStrip(Transform parent, out TMP_Text stageTitle, out TMP_Text waveText,
        out TMP_Text objectiveText, out TMP_Text killsText, out TMP_Text timerText, out TMP_Text gemsText)
    {
        RectTransform strip = Panel("TopLeftInfo", parent, A_TopLeft, new Vector2(26f, -176f), new Vector2(560f, 116f), 0.5f);
        Image stripBg = strip.gameObject.AddComponent<Image>();
        stripBg.sprite = UISprites.Rounded(12);
        stripBg.type = Image.Type.Sliced;
        stripBg.color = new Color(0.04f, 0.06f, 0.09f, 0.88f);
        stripBg.raycastTarget = false;

        stageTitle = Txt("StageTitle", strip, 22f, -8f, 520f, 36f, "1-1 - Tên ải", 30f, Amber, TextAlignmentOptions.Left, FontStyles.Bold);
        waveText = Txt("WaveText", strip, 22f, -46f, 240f, 28f, "Đợt 1 / 3", 21f, TextMuted, TextAlignmentOptions.Left);
        objectiveText = Txt("ObjectiveText", strip, 262f, -46f, 540f, 28f, "Mục tiêu", 21f, TextMain, TextAlignmentOptions.Left);
        killsText = Txt("KillsText", strip, 22f, -78f, 240f, 28f, "Đã diệt: 0", 20f, TextMuted, TextAlignmentOptions.Left);
        timerText = Txt("TimerText", strip, 262f, -78f, 160f, 28f, "00:00", 22f, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        gemsText = Txt("GemsText", strip, 430f, -78f, 140f, 28f, "Ngọc: 0", 20f, Amber, TextAlignmentOptions.Right);
    }

    private static RectTransform BuildBoss(Transform parent, out TMP_Text bossName, out TMP_Text bossHp, out Image bossFill)
    {
        RectTransform panel = Panel("BossPanel", parent, A_TopCenter, new Vector2(0f, -18f), new Vector2(1364f, 156f), 0.5f);
        Img("Plate", panel, Deco + "/02_Panel_Frames_And_Bars/frame_boss_health_empty.png",
            Vector2.zero, Vector2.zero, new Vector2(1364f, 156f));

        Txt("BossTag", panel, 24f, -20f, 150f, 44f, "BOSS:", 30f, Red, TextAlignmentOptions.Left, FontStyles.Bold);
        bossName = Txt("BossName", panel, 170f, -22f, 830f, 44f, "Tên boss", 30f, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        bossHp = Txt("BossHpText", panel, 1010f, -20f, 330f, 42f, "0 / 0 HP", 26f, TextMain, TextAlignmentOptions.Right, FontStyles.Bold);

        bossFill = Img("BossBar", panel, Deco + "/02_Panel_Frames_And_Bars/bar_fill_boss_health_red.png",
            Vector2.zero, Vector2.zero, new Vector2(1312f, 44f), offset: new Vector2(26f, -92f));
        MakeFilled(bossFill);
        return panel;
    }

    private static RectTransform BuildMinimap(Transform parent, MinimapUI minimap,
        out Image mapBackground, out Image mapGrid, out Image viewFrame)
    {
        RectTransform panel = Panel("TopRight", parent, A_TopRight, new Vector2(-26f, -26f), new Vector2(488f, 408f), 0.5f);
        Img("Plate", panel, Deco + "/03_Minimap_Elements/frame_tactical_minimap_empty.png",
            Vector2.zero, Vector2.zero, new Vector2(488f, 408f));

        Txt("MapTitle", panel, 24f, -16f, 330f, 34f, "BÀN ĐỒ CHIẾN THUẬT", 22f, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        Img("NorthMark", panel, Deco + "/03_Minimap_Elements/minimap_north_indicator.png",
            Vector2.zero, Vector2.zero, new Vector2(64f, 44f), offset: new Vector2(400f, -12f));

        RectTransform mapView = Node("MapView", panel, 30f, -80f, 428f, 312f);
        mapView.gameObject.AddComponent<RectMask2D>();

        // Nền: lúc dựng tạm dùng ảnh terrain của bộ art, khi chạy MinimapUI thay bằng nền thật của màn.
        mapBackground = Img("StageBackground", mapView, Deco + "/03_Minimap_Elements/minimap_cave_terrain_map.png",
            Vector2.zero, Vector2.zero, new Vector2(428f, 312f));

        // Lưới riêng (bản gốc minimap_radar_grid có vẽ sẵn một khung xanh đứng yên nên không dùng được).
        mapGrid = Img("Grid", mapView, EnsureMinimapGridSprite(),
            Vector2.zero, Vector2.zero, new Vector2(428f, 312f));
        mapGrid.color = new Color(1f, 1f, 1f, 0.55f);

        // Khung xanh = vùng camera đang nhìn, MinimapUI cập nhật theo camera mỗi frame.
        RectTransform frameRt = Node("ViewFrame", mapView, 0f, 0f, 220f, 124f);
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
        RectTransform rt = Node("PauseButton", parent, -26f, 178f, 346f, 94f, A_BottomRight, 0.5f);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = Load(Deco + "/01_HUD_Panels/panel_pause_esc_button.png");
        img.preserveAspect = true;
        img.raycastTarget = true;

        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;

        Txt("Label", rt, 0f, -26f, 346f, 44f, "ESC", 26f, TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        Txt("Sub", rt, 0f, -58f, 346f, 30f, "TẠM DỪNG", 16f, TextMuted, TextAlignmentOptions.Center);
        return rt;
    }

    private static void BuildSkills(Transform parent, out TMP_Text[] names, out TMP_Text[] states,
        out Image[] glyphs, out Image[] dims)
    {
        RectTransform panel = Panel("BottomRight", parent, A_BottomRight, new Vector2(-26f, 24f), new Vector2(1050f, 282f), 0.5f);

        TMP_Text title = Txt("Title", panel, 4f, -2f, 500f, 34f, "KỸ NĂNG TRANG BỊ", 24f, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        title.characterSpacing = 4f;

        string[] frames =
        {
            ArtRoot + "/Frames/frame_slot_beveled_orange_ready.png",
            ArtRoot + "/Frames/frame_slot_beveled_cyan_cooldown.png",
            ArtRoot + "/Frames/frame_slot_beveled_purple_active.png",
            ArtRoot + "/Frames/frame_slot_beveled_gold_ultimate.png"
        };
        string[] badge =
        {
            ArtRoot + "/Badges/badge_key_j.png",
            ArtRoot + "/Badges/badge_key_k.png",
            ArtRoot + "/Badges/badge_key_l.png",
            ArtRoot + "/Badges/badge_key_i.png"
        };
        Color[] stateColors = { Red, Cyan, new Color(0.72f, 0.55f, 1f), Amber };
        string[] keys = { "J", "K", "L", "I" };

        names = new TMP_Text[4];
        states = new TMP_Text[4];
        glyphs = new Image[4];
        dims = new Image[4];

        for (int i = 0; i < 4; i++)
        {
            float x = 2f + i * 262f;
            RectTransform card = Node("Slot" + i, panel, x, -40f, 250f, 231f);

            Image frame = Img("Frame", card, frames[i], Vector2.zero, Vector2.zero, new Vector2(250f, 231f));
            frame.raycastTarget = false;

            RectTransform glyphRt = Node("Icon" + i, card, 60f, -44f, 130f, 130f);
            glyphs[i] = glyphRt.gameObject.AddComponent<Image>();
            glyphs[i].preserveAspect = true;
            glyphs[i].raycastTarget = false;

            Image badgeImg = Img("KeyBadge" + i, card, badge[i], Vector2.zero, Vector2.zero, new Vector2(48f, 48f), offset: new Vector2(8f, -6f));
            badgeImg.raycastTarget = false;

            names[i] = Txt("Name" + i, card, 0f, -168f, 250f, 30f, "Tên kỹ năng", 22f, TextMain,
                TextAlignmentOptions.Center, FontStyles.Bold);
            states[i] = Txt("State" + i, card, 0f, -196f, 250f, 26f, "SẴN SÀNG", 14f, stateColors[i],
                TextAlignmentOptions.Center, FontStyles.Bold);
            states[i].characterSpacing = 2f;

            Image dim = Img("Dim" + i, card, ArtRoot + "/Overlays/overlay_cooldown_dark_dim.png",
                Vector2.zero, Vector2.zero, new Vector2(250f, 231f));
            dim.raycastTarget = false;
            dim.gameObject.SetActive(false);
            dims[i] = dim;
        }
    }

    private static void BuildCoop(Transform parent)
    {
        RectTransform panel = Panel("BottomLeft", parent, A_BottomLeft, new Vector2(26f, 24f), new Vector2(852f, 303f), 0.5f);
        Img("Plate", panel, Deco + "/02_Panel_Frames_And_Bars/frame_coop_teammates_empty.png",
            Vector2.zero, Vector2.zero, new Vector2(852f, 303f));

        Txt("Title", panel, 22f, -12f, 520f, 34f, "ĐỒNG ĐỘI CO-OP (PHÒNG 1/4)", 22f, TextMain,
            TextAlignmentOptions.Left, FontStyles.Bold);

        RectTransform row = Node("TeammateRow", panel, 22f, -62f, 808f, 74f);

        TMP_Text mateName = Txt("MateName", row, 8f, -18f, 300f, 32f, "Chưa có đồng đội", 18f, TextMuted,
            TextAlignmentOptions.Left, FontStyles.Bold);
        mateName.name = "MateName";

        Img("MateTrack", row, Deco + "/02_Panel_Frames_And_Bars/bar_track_health_empty.png",
            Vector2.zero, Vector2.zero, new Vector2(330f, 26f), offset: new Vector2(318f, -20f));
        Image fill = Img("MateFill", row, Deco + "/02_Panel_Frames_And_Bars/bar_fill_teammate_health_green.png",
            Vector2.zero, Vector2.zero, new Vector2(330f, 26f), offset: new Vector2(318f, -20f));
        MakeFilled(fill);
        fill.fillAmount = 0f;

        Txt("MateHp", row, 660f, -16f, 150f, 30f, "0 / 0", 16f, TextMain, TextAlignmentOptions.Right, FontStyles.Bold);

        Txt("EmptySlots", panel, 120f, -196f, 610f, 40f, "[ Slot 2 - 4 Trống ]", 20f, TextDim,
            TextAlignmentOptions.Center, FontStyles.Bold);
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

        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
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
