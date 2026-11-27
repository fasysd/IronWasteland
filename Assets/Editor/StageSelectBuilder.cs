using System.Collections.Generic;
using System.IO;
using IronWasteland.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronWasteland.EditorTools
{
    /// <summary>
    /// Dựng scene CHỌN ẢI CHIẾN DỊCH từ ảnh nền thiết kế và gắn các vùng chữ/ảnh động
    /// cho thẻ Siêu cơ sẵn sàng. Chạy lại bằng menu Tools/Iron Wasteland/Rebuild Stage Select.
    /// </summary>
    public static class StageSelectBuilder
    {
        private const string SpriteDir = "Assets/Art/UI/StageSelect";
        private const string FontDir = "Assets/Art/Fonts";
        private const string GeneratedFontDir = "Assets/Art/Fonts/Generated";
        private const string ScenePath = "Assets/Scenes/StageSelect.unity";
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;

        private struct LayerRect
        {
            public string Name;
            public float X;
            public float Y;
            public float Width;
            public float Height;

            public LayerRect(string name, float x, float y, float width, float height)
            {
                Name = name;
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }
        }

        private struct TankSpec
        {
            public string Key;
            public string DisplayName;
            public string Role;
            public int Level;
            public int[] Stats;
            public string[] SkillNames;
        }

        private struct StageSpec
        {
            public string Code;
            public string Name;
            public string Tag;
            public string Objective;
            public string Description;
            public string Reward;
            public string DropRate;
            public bool Unlocked;
            public string DefinitionPath;
        }

        private static readonly StageSpec[] Stages =
        {
            new StageSpec { Code = "1-1", Name = "SƯƠNG MÙ TÍM", Tag = "SINH TỒN", Objective = "SINH TỒN 90 GIÂY", Description = "Sống sót 90 giây trong Sương Mù Tím. Tiêu diệt quái vật Alien và vượt qua các đợt phục kích để hoàn thành nhiệm vụ.", Reward = "+15 Ngọc Ma Pháp", DropRate = "Tỷ lệ rơi Phụ kiện: 35%", Unlocked = true, DefinitionPath = "Assets/Prefabs/Stages/Stage_01.asset" },
            new StageSpec { Code = "1-2", Name = "HANG ĐÁ GAI", Tag = "SINH TỒN", Objective = "SINH TỒN 100 GIÂY", Description = "Sống sót 100 giây trong Hang đá gai. Tiêu diệt quái vật Alien và vượt qua đợt phục kích để hoàn thành nhiệm vụ.", Reward = "+15 Ngọc Ma Pháp", DropRate = "Tỷ lệ rơi Phụ kiện: 35%", Unlocked = true, DefinitionPath = "Assets/Prefabs/Stages/Stage_02.asset" },
            new StageSpec { Code = "1-3", Name = "MỎ ĐÁ SẤM", Tag = "SINH TỒN", Objective = "SINH TỒN 120 GIÂY", Description = "Sống sót trong mỏ đá rung chuyển.", Reward = "+20 Ngọc Ma Pháp", DropRate = "Tỷ lệ rơi Phụ kiện: 40%", Unlocked = false },
            new StageSpec { Code = "1-4", Name = "LÕI TÀN TÍCH", Tag = "BOSS", Objective = "HẠ GỤC BOSS", Description = "Đối đầu với thực thể canh giữ lõi tàn tích.", Reward = "+40 Ngọc Ma Pháp", DropRate = "Tỷ lệ rơi Phụ kiện: 60%", Unlocked = false },
            new StageSpec { Code = "2-1", Name = "XƯỞNG BỤI ĐỘC", Tag = "SINH TỒN", Objective = "SINH TỒN 150 GIÂY", Description = "Sống sót trong xưởng đầy bụi độc.", Reward = "+25 Ngọc Ma Pháp", DropRate = "Tỷ lệ rơi Phụ kiện: 45%", Unlocked = false },
            new StageSpec { Code = "2-2", Name = "THUNG LŨNG TRO", Tag = "SINH TỒN", Objective = "SINH TỒN 150 GIÂY", Description = "Sống sót trong thung lũng tro tàn.", Reward = "+25 Ngọc Ma Pháp", DropRate = "Tỷ lệ rơi Phụ kiện: 45%", Unlocked = false },
            new StageSpec { Code = "2-3", Name = "ĐẦM LẦY XANH", Tag = "SINH TỒN", Objective = "SINH TỒN 160 GIÂY", Description = "Sống sót trong đầm lầy độc hại.", Reward = "+30 Ngọc Ma Pháp", DropRate = "Tỷ lệ rơi Phụ kiện: 50%", Unlocked = false },
            new StageSpec { Code = "2-4", Name = "CỔNG THÉP GỈ", Tag = "SINH TỒN", Objective = "SINH TỒN 180 GIÂY", Description = "Sống sót trước cổng thép gỉ.", Reward = "+30 Ngọc Ma Pháp", DropRate = "Tỷ lệ rơi Phụ kiện: 50%", Unlocked = false },
            new StageSpec { Code = "2-5", Name = "TRÁI TIM LÒ PHẢN ỨNG", Tag = "BOSS", Objective = "HẠ GỤC BOSS", Description = "Đối đầu với thực thể cuối cùng.", Reward = "+60 Ngọc Ma Pháp", DropRate = "Tỷ lệ rơi Phụ kiện: 75%", Unlocked = false }
        };

        private static readonly TankSpec[] Tanks =
        {
            new TankSpec
            {
                Key = "wall_titan",
                DisplayName = "WALL TITAN",
                Role = "TANKER HỖ TRỢ",
                Level = 1,
                Stats = new[] { 14, 900, 100, 22, 85, 0, 0, 0 },
                SkillNames = new[] { "Earth Break", "Earth Wall" }
            },
            new TankSpec
            {
                Key = "red_nova",
                DisplayName = "RED NOVA",
                Role = "XẠ THỦ HỎA LỰC",
                Level = 10,
                Stats = new[] { 26, 520, 130, 8, 120, 0, 0, 0 },
                SkillNames = new[] { "Nova Shot", "Triple Nova" }
            },
            new TankSpec
            {
                Key = "black_shadow",
                DisplayName = "BLACK SHADOW",
                Role = "SÁT THỦ CƠ ĐỘNG",
                Level = 5,
                Stats = new[] { 20, 650, 120, 12, 110, 0, 0, 0 },
                SkillNames = new[] { "Shadow Claw", "Shadow Step" }
            },
            new TankSpec
            {
                Key = "leviathan",
                DisplayName = "LEVIATHAN",
                Role = "TANKER ĐỘI HÌNH",
                Level = 1,
                Stats = new[] { 16, 820, 110, 18, 95, 0, 0, 0 },
                SkillNames = new[] { "Water Pulse", "Water Bubble" }
            },
            new TankSpec
            {
                Key = "water_b",
                DisplayName = "WATER B",
                Role = "HỖ TRỢ HỒI MÁU",
                Level = 1,
                Stats = new[] { 15, 600, 140, 10, 105, 0, 0, 0 },
                SkillNames = new[] { "Healing Water", "Bubble Shield" }
            },
            new TankSpec
            {
                Key = "green_fox",
                DisplayName = "GREEN FOX",
                Role = "SÁT THỦ ĐỘC",
                Level = 1,
                Stats = new[] { 22, 560, 125, 9, 118, 0, 0, 0 },
                SkillNames = new[] { "Fox Fang", "Fox Mark" }
            }
        };

        [MenuItem("Tools/Iron Wasteland/Rebuild Stage Select")]
        public static void Rebuild()
        {
            ConfigureBackground();
            ConfigureUiSprites();
            TMP_FontAsset anton = EnsureFontAsset(FontDir + "/Anton-400.ttf", GeneratedFontDir + "/AntonMainMenu SDF.asset");
            TMP_FontAsset barlow = EnsureFontAsset(FontDir + "/BarlowCondensed-700.ttf", GeneratedFontDir + "/BarlowCondensed700MainMenu SDF.asset");
            if (anton == null || barlow == null)
            {
                Debug.LogError("[StageSelectBuilder] Thiếu font, dừng dựng scene.");
                return;
            }

            BuildScene(anton, barlow);
            EnsureSceneInBuildSettings();
        }

        /// <summary>Đặt lại thiết lập import cho ảnh nền của màn hình.</summary>
        private static void ConfigureBackground()
        {
            string path = SpriteDir + "/bg.png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[StageSelectBuilder] Không tìm thấy ảnh nền: " + path);
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        /// <summary>Đặt lại import cho các sprite nút chế độ, khung chọn và viền thẻ.</summary>
        private static void ConfigureUiSprites()
        {
            string[] names = { "mode_single_off", "mode_single_on", "mode_coop_off", "mode_coop_on", "select_frame", "card_border_grey" };
            for (int i = 0; i < names.Length; i++)
            {
                string path = SpriteDir + "/" + names[i] + ".png";
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError("[StageSelectBuilder] Không tìm thấy sprite: " + path);
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }

        /// <summary>Tạo (hoặc lấy lại) font asset TextMeshPro động từ file TTF.</summary>
        private static TMP_FontAsset EnsureFontAsset(string ttfPath, string assetPath)
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogError("[StageSelectBuilder] Không tìm thấy font: " + ttfPath);
                return null;
            }

            Directory.CreateDirectory(GeneratedFontDir);
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);
            if (asset.atlasTextures != null && asset.atlasTextures.Length > 0)
            {
                asset.atlasTextures[0].name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            }

            if (asset.material != null)
            {
                asset.material.name = asset.name + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>Dựng canvas, ảnh nền, thẻ xe, panel nhiệm vụ và hai nút chế độ.</summary>
        private static void BuildScene(TMP_FontAsset anton, TMP_FontAsset barlow)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.07f, 0.09f, 1f);
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            GameObject canvasObject = new GameObject("StageSelectCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DesignWidth, DesignHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = canvasObject.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            // bg_clean.png = bg.png đã xoá cụm số ngọc nướng sẵn để đổ số động lên trên.
            Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/bg_clean.png");
            if (background == null)
            {
                Debug.LogError("[StageSelectBuilder] Không tải được sprite nền.");
                return;
            }

            AddImage(root, new LayerRect("Background", 0f, 0f, DesignWidth, DesignHeight), background);

            // Số ngọc ma pháp ở header: cụm "125" đã bị xoá khỏi ảnh nền, controller đổ số thật vào đây.
            TextMeshProUGUI gemCount = AddText(root, "GemCount", "125", 1652f, 121f, 25f, new Color32(0xFF, 0xD7, 0x9A, 0xFF), anton, 90f, false);
            gemCount.textWrappingMode = TextWrappingModes.NoWrap;

            Image portrait = AddImage(root, new LayerRect("TankPortrait", 1310.72f, 745.92f, 81.12f, 81.12f), null);
            portrait.preserveAspect = true;

            TextMeshProUGUI name = AddText(root, "TextTankName", "BLACK SHADOW", 1407f, 769f, 21f, new Color32(0xE6, 0xED, 0xF5, 0xFF), anton, 900f, false);
            name.characterSpacing = 0.3f / 21f * 100f;
            TextMeshProUGUI roleLevel = AddText(root, "TextTankRoleLevel", "CẤP 05 / SÁT THỦ CƠ ĐỘNG", 1407f, 787.5f, 10.2f, new Color32(0xC9, 0xA9, 0xFF, 0xFF), barlow, 500f, false);
            TextMeshProUGUI stats = AddText(root, "TextTankStats", "20 ATK   650 HP   120 EN   110 SPD", 1407f, 812f, 11f, new Color32(0x2F, 0xE8, 0xE0, 0xFF), barlow, 600f, false);
            stats.characterSpacing = 0.2f / 11f * 100f;

            float[] iconX = { 1331f, 1423.04f, 1515.08f, 1607.12f };
            float[] nameX = { 1331f, 1423.04f, 1508.08f, 1600.12f };
            Image[] skillIcons = new Image[4];
            TextMeshProUGUI[] skillNames = new TextMeshProUGUI[4];
            for (int i = 0; i < 4; i++)
            {
                skillIcons[i] = AddImage(root, new LayerRect("SkillIcon" + (i + 1), iconX[i] + 3.84f, 843.36f, 36f, 36f), null);
                skillIcons[i].preserveAspect = true;
                skillNames[i] = AddText(root, "SkillName" + (i + 1), "Shadow Claw", nameX[i], 903f, 12f, new Color32(0x8A, 0x99, 0xAB, 0xFF), barlow, 70f, true);
                skillNames[i].characterSpacing = 0.5f / 12f * 100f;
            }

            // Phím kỹ năng bám theo điều khiển hiện tại: 1 = thường, 2 = chuyên biệt, 3-4 = tùy chọn.
            float[] keyX = { 1321f, 1411f, 1503f, 1594f };
            for (int i = 0; i < keyX.Length; i++)
            {
                TextMeshProUGUI key = AddText(root, "SkillKey" + (i + 1), (i + 1).ToString(), keyX[i] - 7f, 849f, 10f, new Color32(0xCF, 0xBF, 0xEA, 0xFF), barlow, 14f, false);
                key.alignment = TextAlignmentOptions.Center;
            }

            // Panel nhiệm vụ động phủ lên nội dung 1-2 đã nướng trong ảnh nền.
            Image missionPanel = AddImage(root, new LayerRect("MissionPanel", 1303f, 203f, 534f, 204f), null);
            missionPanel.color = new Color32(12, 18, 25, 255);

            Image missionAccent = AddImage(root, new LayerRect("MissionAccent", 1319f, 207f, 4f, 24f), null);
            missionAccent.color = new Color32(0x2F, 0xE8, 0xE0, 0xFF);

            TextMeshProUGUI missionTitle = AddText(root, "TextMissionTitle", "THÔNG TIN NHIỆM VỤ", 1335f, 228f, 14f, new Color32(0xE6, 0xED, 0xF5, 0xFF), anton, 300f, false);
            missionTitle.characterSpacing = 0.4f / 14f * 100f;
            TextMeshProUGUI missionCode = AddText(root, "TextMissionCode", "ẢI 1-2 / SINH TỒN", 1335f, 242f, 9.6f, new Color32(0x5C, 0x68, 0x75, 0xFF), barlow, 300f, false);
            missionCode.characterSpacing = 0.2f / 9.6f * 100f;
            TextMeshProUGUI missionName = AddText(root, "TextMissionName", "HANG ĐÁ GAI", 1323f, 304f, 34f, new Color32(0xF2, 0xF6, 0xFA, 0xFF), anton, 440f, false);
            missionName.characterSpacing = 0.4f / 34f * 100f;
            TextMeshProUGUI missionObjective = AddText(root, "TextMissionObjective", "SINH TỒN 100 GIÂY", 1323f, 326f, 13f, new Color32(0xFF, 0x9A, 0x3C, 0xFF), barlow, 440f, false);
            missionObjective.characterSpacing = 0.2f / 13f * 100f;
            TextMeshProUGUI missionDescription = AddText(root, "TextMissionDescription", "Sống sót 100 giây trong Hang đá gai. Tiêu diệt quái vật Alien và vượt qua đợt phục kích để hoàn thành nhiệm vụ.", 1323f, 357f, 12.2f, new Color32(0x9A, 0xA6, 0xB2, 0xFF), barlow, 410f, false);
            missionDescription.textWrappingMode = TextWrappingModes.Normal;
            missionDescription.overflowMode = TextOverflowModes.Overflow;
            missionDescription.rectTransform.sizeDelta = new Vector2(410f, 40f);
            missionDescription.lineSpacing = 1f;

            Color32 rewardPanelColor = new Color32(44, 52, 64, 255);
            AddLine(root, "RewardLineTop", 1510f, 240f, 320f, 1f, rewardPanelColor);
            AddLine(root, "RewardLineBottom", 1510f, 319f, 320f, 1f, rewardPanelColor);
            AddLine(root, "RewardLineLeft", 1510f, 240f, 1f, 80f, rewardPanelColor);
            AddLine(root, "RewardLineRight", 1829f, 240f, 1f, 80f, rewardPanelColor);
            TextMeshProUGUI missionRewardLabel = AddText(root, "TextMissionRewardLabel", "PHẦN THƯỞNG KHI HOÀN THÀNH:", 1527f, 270f, 9.6f, new Color32(0x7E, 0x8A, 0x96, 0xFF), barlow, 300f, false);
            missionRewardLabel.characterSpacing = 0.2f / 9.6f * 100f;
            TextMeshProUGUI missionReward = AddText(root, "TextMissionReward", "+15 Ngọc Ma Pháp", 1527f, 298f, 13.5f, new Color32(0xE6, 0xED, 0xF5, 0xFF), barlow, 300f, false);
            TextMeshProUGUI missionDrop = AddText(root, "TextMissionDrop", "Tỷ lệ rơi Phụ kiện: 35%", 1527f, 319f, 12f, new Color32(0xFF, 0xB3, 0x47, 0xFF), barlow, 300f, false);

            // Hai nút chế độ: ảnh nền đổi giữa vàng/đen, chữ và dấu tích vẽ động.
            Sprite singleOff = LoadUiSprite("mode_single_off");
            Sprite singleOn = LoadUiSprite("mode_single_on");
            Sprite coopOff = LoadUiSprite("mode_coop_off");
            Sprite coopOn = LoadUiSprite("mode_coop_on");
            Image singleImage = AddImage(root, new LayerRect("ModeSingle", 1300f, 467f, 268f, 47f), singleOff);
            Image coopImage = AddImage(root, new LayerRect("ModeCoop", 1579f, 467f, 269f, 47f), coopOn);
            TextMeshProUGUI singleLabel = AddText(root, "TextModeSingle", "CHƠI ĐƠN", 1313f, 491f, 15f, new Color32(0x6F, 0x7D, 0x8C, 0xFF), barlow, 160f, false);
            TextMeshProUGUI singleHint = AddText(root, "TextModeSingleHint", "01 NGƯỜI / CHIẾN DỊCH", 1313f, 501.5f, 7.8f, new Color32(0x4E, 0x5A, 0x69, 0xFF), barlow, 180f, false);
            TextMeshProUGUI coopLabel = AddText(root, "TextModeCoop", "CHƠI CO-OP", 1585f, 491f, 15f, new Color32(0xFF, 0xFF, 0xFF, 0xFF), barlow, 160f, false);
            TextMeshProUGUI coopHint = AddText(root, "TextModeCoopHint", "2-4 NGƯỜI / TẠO PHÒNG", 1585f, 501.5f, 7.8f, new Color32(0xFF, 0xE0, 0xB2, 0xFF), barlow, 180f, false);
            Sprite modeCheckSprite = LoadUiSprite("mode_check");
            Image modeCheck = AddImage(root, new LayerRect("ModeCheck", 1806f, 476f, 20f, 16f), modeCheckSprite);
            modeCheck.preserveAspect = true;

            // Thẻ 1-1 và 1-2 có trạng thái động; 1-2 tối đi khi chọn ải khác.
            // Toạ độ lấy trực tiếp từ các thẻ <rect> của stage_select.svg (không ước lượng).
            Rect[] cardRects =
            {
                new Rect(122.5f, 380.5f, 195f, 147f),   // 1-1
                new Rect(347f, 381f, 194f, 146f),       // 1-2
                new Rect(570.5f, 380.5f, 195f, 147f),   // 1-3
                new Rect(794f, 380f, 196f, 148f),       // 1-4
                new Rect(1018.5f, 380.5f, 195f, 147f),  // 2-1
                new Rect(346f, 672f, 196f, 148f),       // 2-5
                new Rect(570f, 672f, 196f, 148f),       // 2-4
                new Rect(794.5f, 672.5f, 195f, 147f),   // 2-3
                new Rect(1018.5f, 672.5f, 195f, 147f)   // 2-2
            };

            Image card12Border = AddImage(root, new LayerRect("Card12Border", cardRects[1].x, cardRects[1].y, cardRects[1].width, cardRects[1].height), LoadUiSprite("card_border_neutral"));
            card12Border.raycastTarget = false;

            Image[] statusCovers = new Image[2];
            TextMeshProUGUI[] statusTexts = new TextMeshProUGUI[2];
            for (int i = 0; i < 2; i++)
            {
                Rect card = cardRects[i];
                statusCovers[i] = AddImage(root, new LayerRect("StatusCover" + (i + 1), card.x + 3.5f, card.y + 3.5f, card.width - 7f, 30f), null);
                statusCovers[i].color = i == 0 ? new Color32(11, 17, 24, 255) : new Color32(18, 21, 25, 255);
                statusTexts[i] = AddText(root, "StatusText" + (i + 1), i == 0 ? "ĐÃ HOÀN THÀNH" : "ĐANG CHỌN", card.x + 3.5f, 400f, 10.4f, new Color32(0x2F, 0xE8, 0xE0, 0xFF), barlow, card.width - 7f, false);
                statusTexts[i].alignment = TextAlignmentOptions.Center;
                statusTexts[i].characterSpacing = 0.2f / 10.4f * 100f;
            }

            Image selectionFrame = AddImage(root, new LayerRect("SelectionFrame", cardRects[1].x, cardRects[1].y, cardRects[1].width, cardRects[1].height), LoadUiSprite("select_frame"));
            selectionFrame.raycastTarget = false;

            StageSelectController controller = canvasObject.AddComponent<StageSelectController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("m_TankPortrait").objectReferenceValue = portrait;
            serialized.FindProperty("m_TankName").objectReferenceValue = name;
            serialized.FindProperty("m_TankRoleLevel").objectReferenceValue = roleLevel;
            serialized.FindProperty("m_TankStats").objectReferenceValue = stats;
            serialized.FindProperty("m_MissionPanel").objectReferenceValue = missionPanel.gameObject;
            serialized.FindProperty("m_MissionCode").objectReferenceValue = missionCode;
            serialized.FindProperty("m_MissionName").objectReferenceValue = missionName;
            serialized.FindProperty("m_MissionObjective").objectReferenceValue = missionObjective;
            serialized.FindProperty("m_MissionDescription").objectReferenceValue = missionDescription;
            serialized.FindProperty("m_MissionReward").objectReferenceValue = missionReward;
            serialized.FindProperty("m_MissionDrop").objectReferenceValue = missionDrop;
            serialized.FindProperty("m_GemCount").objectReferenceValue = gemCount;
            serialized.FindProperty("m_SelectionFrame").objectReferenceValue = selectionFrame.rectTransform;
            serialized.FindProperty("m_Card12Border").objectReferenceValue = card12Border;
            serialized.FindProperty("m_ModeSingleImage").objectReferenceValue = singleImage;
            serialized.FindProperty("m_ModeCoopImage").objectReferenceValue = coopImage;
            serialized.FindProperty("m_ModeSingleLabel").objectReferenceValue = singleLabel;
            serialized.FindProperty("m_ModeSingleHint").objectReferenceValue = singleHint;
            serialized.FindProperty("m_ModeCoopLabel").objectReferenceValue = coopLabel;
            serialized.FindProperty("m_ModeCoopHint").objectReferenceValue = coopHint;
            serialized.FindProperty("m_ModeCheck").objectReferenceValue = modeCheck;
            serialized.FindProperty("m_ModeOffSprite").objectReferenceValue = singleOff;
            serialized.FindProperty("m_ModeOnSprite").objectReferenceValue = singleOn;
            serialized.FindProperty("m_DefaultStageIndex").intValue = 1;

            SerializedProperty iconsProperty = serialized.FindProperty("m_SkillIcons");
            iconsProperty.arraySize = 4;
            SerializedProperty namesProperty = serialized.FindProperty("m_SkillNames");
            namesProperty.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                iconsProperty.GetArrayElementAtIndex(i).objectReferenceValue = skillIcons[i];
                namesProperty.GetArrayElementAtIndex(i).objectReferenceValue = skillNames[i];
            }

            SerializedProperty coversProperty = serialized.FindProperty("m_StatusCovers");
            coversProperty.arraySize = 2;
            SerializedProperty statusTextsProperty = serialized.FindProperty("m_StatusTexts");
            statusTextsProperty.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                coversProperty.GetArrayElementAtIndex(i).objectReferenceValue = statusCovers[i];
                statusTextsProperty.GetArrayElementAtIndex(i).objectReferenceValue = statusTexts[i];
            }

            SerializedProperty cardRectsProperty = serialized.FindProperty("m_CardRects");
            cardRectsProperty.arraySize = cardRects.Length;
            for (int i = 0; i < cardRects.Length; i++)
            {
                cardRectsProperty.GetArrayElementAtIndex(i).rectValue = cardRects[i];
            }

            SerializedProperty tanksProperty = serialized.FindProperty("m_Tanks");
            tanksProperty.arraySize = Tanks.Length;
            for (int i = 0; i < Tanks.Length; i++)
            {
                TankSpec spec = Tanks[i];
                SerializedProperty entry = tanksProperty.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("DisplayName").stringValue = spec.DisplayName;
                entry.FindPropertyRelative("Role").stringValue = spec.Role;
                entry.FindPropertyRelative("Level").intValue = spec.Level;
                entry.FindPropertyRelative("Portrait").objectReferenceValue = LoadPortrait(spec.Key);

                SerializedProperty statsProperty = entry.FindPropertyRelative("Stats");
                statsProperty.arraySize = spec.Stats.Length;
                for (int s = 0; s < spec.Stats.Length; s++)
                {
                    statsProperty.GetArrayElementAtIndex(s).intValue = spec.Stats[s];
                }

                SerializedProperty names = entry.FindPropertyRelative("SkillNames");
                names.arraySize = 2;
                for (int s = 0; s < 2; s++)
                {
                    names.GetArrayElementAtIndex(s).stringValue = spec.SkillNames[s];
                }

                SerializedProperty baseIcons = entry.FindPropertyRelative("SkillIcons");
                baseIcons.arraySize = 4;
                for (int s = 0; s < 4; s++)
                {
                    baseIcons.GetArrayElementAtIndex(s).objectReferenceValue = LoadSkillIcon(spec.Key + "_" + s);
                }

                SerializedProperty optionIcons = entry.FindPropertyRelative("OptionSkillIcons");
                optionIcons.arraySize = 6;
                for (int tab = 0; tab < 2; tab++)
                {
                    for (int option = 0; option < 3; option++)
                    {
                        int index = tab * 3 + option;
                        optionIcons.GetArrayElementAtIndex(index).objectReferenceValue = LoadPickerIcon(spec.Key, tab, option);
                    }
                }
            }

            SerializedProperty stagesProperty = serialized.FindProperty("m_Stages");
            stagesProperty.arraySize = Stages.Length;
            for (int i = 0; i < Stages.Length; i++)
            {
                StageSpec spec = Stages[i];
                SerializedProperty entry = stagesProperty.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Code").stringValue = spec.Code;
                entry.FindPropertyRelative("Name").stringValue = spec.Name;
                entry.FindPropertyRelative("Tag").stringValue = spec.Tag;
                entry.FindPropertyRelative("Objective").stringValue = spec.Objective;
                entry.FindPropertyRelative("Description").stringValue = spec.Description;
                entry.FindPropertyRelative("Reward").stringValue = spec.Reward;
                entry.FindPropertyRelative("DropRate").stringValue = spec.DropRate;
                entry.FindPropertyRelative("Unlocked").boolValue = spec.Unlocked;
                entry.FindPropertyRelative("Definition").objectReferenceValue = string.IsNullOrEmpty(spec.DefinitionPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<IronWasteland.Stages.StageDefinition>(spec.DefinitionPath);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            UIHit.EnsureEventSystem(scene);
            Button backButton = UIHit.AddZone(root, "HitBack", 72f, 94f, 168f, 50f);
            UnityEventTools.AddPersistentListener(backButton.onClick, controller.PressBack);
            Button startButton = UIHit.AddZone(root, "HitStart", 1293f, 939f, 536f, 136f);
            UnityEventTools.AddPersistentListener(startButton.onClick, controller.PressStart);
            Button singleButton = UIHit.AddZone(root, "HitModeSingle", 1295f, 472f, 253f, 48f);
            UnityEventTools.AddPersistentListener(singleButton.onClick, controller.PressModeSingle);
            Button coopButton = UIHit.AddZone(root, "HitModeCoop", 1565f, 472f, 265f, 48f);
            UnityEventTools.AddPersistentListener(coopButton.onClick, controller.PressModeCoop);
            for (int i = 0; i < cardRects.Length; i++)
            {
                Rect card = cardRects[i];
                Button cardButton = UIHit.AddZone(root, "HitCard" + (i + 1).ToString("00"), card.x, card.y, card.width, card.height);
                UnityEventTools.AddIntPersistentListener(cardButton.onClick, controller.SelectStage, i);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[StageSelectBuilder] Đã dựng xong scene " + ScenePath);
        }

        private static void EnsureSceneInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == ScenePath)
                {
                    scenes[i] = new EditorBuildSettingsScene(ScenePath, true);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Image AddImage(RectTransform parent, LayerRect layer, Sprite sprite)
        {
            GameObject go = new GameObject(layer.Name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(layer.X, DesignHeight - (layer.Y + layer.Height));
            rect.sizeDelta = new Vector2(layer.Width, layer.Height);
            rect.localScale = Vector3.one;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Thêm một dải màu mảnh dùng làm đường kẻ khung.</summary>
        private static Image AddLine(RectTransform parent, string name, float x, float y, float width, float height, Color color)
        {
            Image image = AddImage(parent, new LayerRect(name, x, y, width, height), null);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Thêm một dòng chữ động, canh theo đường baseline của thiết kế.</summary>
        private static TextMeshProUGUI AddText(RectTransform parent, string name, string value, float x, float baselineY, float fontSize, Color color, TMP_FontAsset font, float width, bool autoSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            float ascentPx = font.faceInfo.ascentLine / font.faceInfo.pointSize * fontSize;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, fontSize * 1.8f);
            rect.anchoredPosition = new Vector2(x, DesignHeight - (baselineY - ascentPx));

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            if (autoSize)
            {
                text.enableAutoSizing = true;
                text.fontSizeMin = 8f;
                text.fontSizeMax = fontSize;
            }

            return text;
        }

        private static Sprite LoadUiSprite(string fileName)
        {
            string path = SpriteDir + "/" + fileName + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("[StageSelectBuilder] Không tìm thấy sprite giao diện: " + path);
            }

            return sprite;
        }

        private static Sprite LoadPortrait(string key)
        {
            string path = "Assets/Art/UI/MainMenu/tank_hero_" + key + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("[StageSelectBuilder] Không tìm thấy ảnh xe: " + path);
            }

            return sprite;
        }

        private static Sprite LoadSkillIcon(string iconName)
        {
            string path = "Assets/Art/UI/Garage/Skill/" + iconName + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("[StageSelectBuilder] Không tìm thấy icon kỹ năng: " + path);
            }

            return sprite;
        }

        private static Sprite LoadPickerIcon(string key, int tab, int option)
        {
            string path = "Assets/Art/UI/Picker/SkillIcons/" + key + "_" + tab + "_" + option + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("[StageSelectBuilder] Không tìm thấy icon tùy chọn: " + path);
            }

            return sprite;
        }
    }
}