using System.IO;
using IronWasteland.Tanks;
using IronWasteland.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace IronWasteland.EditorTools
{
    /// <summary>
    /// Dựng scene Garage từ bộ sprite đã xuất ra từ file thiết kế 02_GARAGE.svg.
    /// Mỗi lớp hình ảnh nằm đúng toạ độ pixel trong hệ 1920x1080, các vùng chữ động
    /// dùng TextMeshPro và được GarageController cập nhật khi người chơi chọn xe.
    /// </summary>
    public static class GarageBuilder
    {
        private const string SpriteDir = "Assets/Art/UI/Garage";
        private const string FontDir = "Assets/Art/Fonts";
        private const string GeneratedFontDir = "Assets/Art/Fonts/Generated";
        private const string ScenePath = "Assets/Scenes/Garage.unity";
        private const string CatalogPath = "Assets/Settings/AccessoryCatalog.asset";
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;

        /// <summary>Toạ độ một lớp ảnh trong hệ thiết kế (gốc trên-trái).</summary>
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

        /// <summary>Dữ liệu tạm của một xe, chỉ dùng trong lúc dựng scene.</summary>
        private struct TankSpec
        {
            public string SpriteName;
            public string DisplayName;
            public string ListRole;
            public string HeroRole;
            public int Level;
            public int NextLevel;
            public int UpgradeCost;
            public int[] Stats;
            public string[] SkillNames;
            public string[] SkillDescriptions;
        }

        [MenuItem("Tools/Iron Wasteland/Rebuild Garage")]
        public static void Rebuild()
        {
            ConfigureSprites();
            TMP_FontAsset anton = EnsureFontAsset(FontDir + "/Anton-400.ttf", GeneratedFontDir + "/AntonMainMenu SDF.asset");
            TMP_FontAsset barlow600 = EnsureFontAsset(FontDir + "/BarlowCondensed-600.ttf", GeneratedFontDir + "/GarageBarlow600 SDF.asset");
            TMP_FontAsset barlow700 = EnsureFontAsset(FontDir + "/BarlowCondensed-700.ttf", GeneratedFontDir + "/BarlowCondensed700MainMenu SDF.asset");
            TMP_FontAsset barlow900 = EnsureFontAsset(FontDir + "/BarlowCondensed-900.ttf", GeneratedFontDir + "/GarageBarlow900 SDF.asset");
            TMP_FontAsset mono = EnsureFontAsset(FontDir + "/ShareTechMono-400.ttf", GeneratedFontDir + "/GarageShareTechMono SDF.asset");
            if (anton == null || barlow600 == null || barlow700 == null || barlow900 == null || mono == null)
            {
                Debug.LogError("[GarageBuilder] Thiếu font, dừng dựng scene.");
                return;
            }

            BuildScene(anton, barlow600, barlow700, barlow900, mono);
        }

        /// <summary>Đặt lại thiết lập import cho toàn bộ sprite của màn hình Garage.</summary>
        private static void ConfigureSprites()
        {
            ConfigureSpriteFolder(SpriteDir);
            string skillDir = SpriteDir + "/Skill";
            if (Directory.Exists(skillDir))
            {
                ConfigureSpriteFolder(skillDir);
            }
        }

        /// <summary>Đặt lại thiết lập import cho toàn bộ ảnh PNG trong một thư mục.</summary>
        private static void ConfigureSpriteFolder(string folder)
        {
            string[] files = Directory.GetFiles(folder, "*.png");
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
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
                importer.sRGBTexture = true;
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
                Debug.LogError("[GarageBuilder] Không tìm thấy font: " + ttfPath);
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

        /// <summary>Dựng toàn bộ scene: canvas, các lớp ảnh, khung chọn, ảnh xe và các vùng chữ động.</summary>
        private static void BuildScene(TMP_FontAsset anton, TMP_FontAsset barlow600, TMP_FontAsset barlow700, TMP_FontAsset barlow900, TMP_FontAsset mono)
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

            GameObject canvasObject = new GameObject("GarageCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

            AddImage(root, new LayerRect("Background", 0f, 0f, DesignWidth, DesignHeight), LoadSprite("bg"));
            AddImage(root, new LayerRect("Header", 0f, 0f, 1920f, 167f), LoadSprite("header"));
            AddImage(root, new LayerRect("TankList", 71f, 186f, 374f, 767f), LoadSprite("left_tank_list"));
            AddImage(root, new LayerRect("CenterUnit", 467f, 171f, 802f, 470f), LoadSprite("center_unit"));
            AddImage(root, new LayerRect("UpgradeBar", 467f, 655f, 802f, 74f), LoadSprite("upgrade"));
            AddImage(root, new LayerRect("SkillBar", 467f, 739f, 802f, 250f), LoadSprite("skills"));
            AddImage(root, new LayerRect("RightStats", 1291f, 189f, 558f, 688f), LoadSprite("right_stats_dynamic"));
            GarageAccessoryPanel accessoryPanel = AddAccessoryPanel(root, barlow600, barlow700);
            AddImage(root, new LayerRect("DeployPanel", 1291f, 889f, 558f, 88f), LoadSprite("cta"));
            AddImage(root, new LayerRect("Ornaments", 27f, 27f, 1871f, 1041f), LoadSprite("ornaments"));

            Sprite[] heroSprites = new Sprite[6];
            for (int i = 0; i < heroSprites.Length; i++)
            {
                heroSprites[i] = LoadSprite("tank_" + TankSprites[i].SpriteName);
            }

            Image heroImage = AddImage(root, new LayerRect("TankPortrait", 688f, 206f, 360f, 360f), heroSprites[2]);
            heroImage.rectTransform.sizeDelta = new Vector2(360f, 360f);
            heroImage.preserveAspect = true;

            Image selectionFrame = AddImage(root, new LayerRect("SelectionFrame", 32f, 482f, 414f, 112f), LoadSprite("sel_frame"));

            // Ảnh thu nhỏ của chính xe đang trưng bày (đủ thân + nòng) cho danh sách bên trái.
            for (int i = 0; i < TankSprites.Length; i++)
            {
                Image icon = AddImage(root, new LayerRect("TankIcon" + i, 86f, 258f + 120f * i, 80f, 80f), LoadSprite("icon_" + TankSprites[i].SpriteName));
                icon.rectTransform.sizeDelta = new Vector2(80f, 80f);
                icon.preserveAspect = true;
            }

            // Lớp phủ khoá của xe chưa mở (Green Fox) nằm trên ảnh thu nhỏ.
            AddImage(root, new LayerRect("LockOverlay", 86f, 858f, 80f, 80f), LoadSprite("lock_overlay"));

            TextMeshProUGUI heroCaption = AddText(root, "TextHeroCaption", "CURRENT UNIT  /  BLACK SHADOW", 502f, 238.34f, 13.6f, new Color32(0x3D, 0x4A, 0x59, 0xFF), mono, TextAlignmentOptions.TopLeft, 0f);
            TextMeshProUGUI heroLevel = AddText(root, "TextHeroLevel", "05", 1152f, 254.73f, 27.28f, new Color32(0x2F, 0xE8, 0xE0, 0xFF), anton, TextAlignmentOptions.TopLeft, 0f);
            TextMeshProUGUI heroName = AddText(root, "TextHeroName", "BLACK SHADOW", 868f, 606.10f, 39.6f, Color.white, anton, TextAlignmentOptions.Top, 1.4f);
            TextMeshProUGUI heroRole = AddText(root, "TextHeroRole", "SÁT THỦ CƠ ĐỘNG", 868f, 632.95f, 17.04f, new Color32(0xC9, 0xA9, 0xFF, 0xFF), barlow700, TextAlignmentOptions.Top, 1.4f);
            TextMeshProUGUI levelValue = AddText(root, "TextLevelValue", "05", 486f, 714.45f, 34.8f, new Color32(0xE6, 0xED, 0xF5, 0xFF), anton, TextAlignmentOptions.TopLeft, 0f);
            TextMeshProUGUI levelNext = AddText(root, "TextLevelNext", "+ CẤP 6", 600f, 704.95f, 17.04f, new Color32(0xFF, 0xB3, 0x47, 0xFF), barlow700, TextAlignmentOptions.TopLeft, 1.4f);
            TextMeshProUGUI upgradeCost = AddText(root, "TextUpgradeCost", "5", 1226f, 705.05f, 19.8f, new Color32(0x0B, 0x0F, 0x14, 0xFF), anton, TextAlignmentOptions.TopRight, 0f);

            float[] skillX = { 563.5f, 766.5f, 969.5f, 1172.5f };
            TextMeshProUGUI[] skillNames = new TextMeshProUGUI[4];
            TextMeshProUGUI[] skillDescriptions = new TextMeshProUGUI[4];
            for (int i = 0; i < 4; i++)
            {
                skillNames[i] = AddText(root, "TextSkillName" + i, TankSprites[2].SkillNames[i], skillX[i], 955.05f, 22.44f, new Color32(0xE6, 0xED, 0xF5, 0xFF), barlow900, TextAlignmentOptions.Top, 0.6f);
                skillDescriptions[i] = AddText(root, "TextSkillDesc" + i, TankSprites[2].SkillDescriptions[i], skillX[i], 978.97f, 14.44f, new Color32(0x8A, 0x99, 0xAB, 0xFF), barlow600, TextAlignmentOptions.Top, 0.6f);
            }

            // 4 ô ảnh kỹ năng: ảnh thật của xe đang chọn, cập nhật khi đổi xe.
            float[] skillIconX = { 515.5f, 718.5f, 921.5f, 1124.5f };
            Image[] skillIcons = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                Image icon = AddImage(root, new LayerRect("SkillIcon" + i, skillIconX[i], 832f, 96f, 96f), LoadSkillIcon(TankSprites[2].SpriteName + "_" + i));
                icon.rectTransform.sizeDelta = new Vector2(96f, 96f);
                icon.preserveAspect = true;
                skillIcons[i] = icon;
            }

            int[] defaultStats = TankSprites[2].Stats;
            TextMeshProUGUI[] statValues = new TextMeshProUGUI[8];
            for (int i = 0; i < 8; i++)
            {
                statValues[i] = AddText(root, "TextStat" + i, defaultStats[i].ToString(), 1796f, 270.06f + i * 49f, 23.76f, new Color32(0xE6, 0xED, 0xF5, 0xFF), anton, TextAlignmentOptions.TopRight, 0f);
            }

            GarageController controller = canvasObject.AddComponent<GarageController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("m_HeroImage").objectReferenceValue = heroImage;
            serialized.FindProperty("m_SelectionFrame").objectReferenceValue = selectionFrame.rectTransform;
            serialized.FindProperty("m_HeroName").objectReferenceValue = heroName;
            serialized.FindProperty("m_HeroRole").objectReferenceValue = heroRole;
            serialized.FindProperty("m_HeroCaption").objectReferenceValue = heroCaption;
            serialized.FindProperty("m_HeroLevel").objectReferenceValue = heroLevel;
            serialized.FindProperty("m_LevelValue").objectReferenceValue = levelValue;
            serialized.FindProperty("m_LevelNext").objectReferenceValue = levelNext;
            serialized.FindProperty("m_UpgradeCost").objectReferenceValue = upgradeCost;
            serialized.FindProperty("m_DefaultIndex").intValue = 2;
            serialized.FindProperty("m_AccessoryPanel").objectReferenceValue = accessoryPanel;

            SerializedProperty skillNameProperty = serialized.FindProperty("m_SkillNames");
            skillNameProperty.arraySize = 4;
            SerializedProperty skillDescProperty = serialized.FindProperty("m_SkillDescriptions");
            skillDescProperty.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                skillNameProperty.GetArrayElementAtIndex(i).objectReferenceValue = skillNames[i];
                skillDescProperty.GetArrayElementAtIndex(i).objectReferenceValue = skillDescriptions[i];
            }

            SerializedProperty skillIconProperty = serialized.FindProperty("m_SkillIcons");
            skillIconProperty.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                skillIconProperty.GetArrayElementAtIndex(i).objectReferenceValue = skillIcons[i];
            }

            SerializedProperty statProperty = serialized.FindProperty("m_StatValues");
            statProperty.arraySize = 8;
            for (int i = 0; i < 8; i++)
            {
                statProperty.GetArrayElementAtIndex(i).objectReferenceValue = statValues[i];
            }

            SerializedProperty tanksProperty = serialized.FindProperty("m_Tanks");
            tanksProperty.arraySize = TankSprites.Length;
            for (int i = 0; i < TankSprites.Length; i++)
            {
                SerializedProperty element = tanksProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("DisplayName").stringValue = TankSprites[i].DisplayName;
                element.FindPropertyRelative("ListRole").stringValue = TankSprites[i].ListRole;
                element.FindPropertyRelative("HeroRole").stringValue = TankSprites[i].HeroRole;
                element.FindPropertyRelative("Level").intValue = TankSprites[i].Level;
                element.FindPropertyRelative("NextLevel").intValue = TankSprites[i].NextLevel;
                element.FindPropertyRelative("UpgradeCost").intValue = TankSprites[i].UpgradeCost;

                SerializedProperty stats = element.FindPropertyRelative("Stats");
                stats.arraySize = 8;
                for (int s = 0; s < 8; s++)
                {
                    stats.GetArrayElementAtIndex(s).intValue = TankSprites[i].Stats[s];
                }

                SerializedProperty names = element.FindPropertyRelative("SkillNames");
                names.arraySize = 4;
                SerializedProperty descs = element.FindPropertyRelative("SkillDescriptions");
                descs.arraySize = 4;
                for (int s = 0; s < 4; s++)
                {
                    names.GetArrayElementAtIndex(s).stringValue = TankSprites[i].SkillNames[s];
                    descs.GetArrayElementAtIndex(s).stringValue = TankSprites[i].SkillDescriptions[s];
                }

                SerializedProperty icons = element.FindPropertyRelative("SkillIcons");
                icons.arraySize = 4;
                for (int s = 0; s < 4; s++)
                {
                    icons.GetArrayElementAtIndex(s).objectReferenceValue = LoadSkillIcon(TankSprites[i].SpriteName + "_" + s);
                }

                // 6 icon kỹ năng tùy chọn dùng chung bộ ảnh với màn Picker.
                SerializedProperty optionIcons = element.FindPropertyRelative("OptionSkillIcons");
                optionIcons.arraySize = SkillCatalog.TabCount * SkillCatalog.OptionCount;
                for (int tab = 0; tab < SkillCatalog.TabCount; tab++)
                {
                    for (int option = 0; option < SkillCatalog.OptionCount; option++)
                    {
                        optionIcons.GetArrayElementAtIndex(tab * SkillCatalog.OptionCount + option).objectReferenceValue =
                            LoadPickerIcon(i, tab, option);
                    }
                }

                element.FindPropertyRelative("Hero").objectReferenceValue = heroSprites[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[GarageBuilder] Đã dựng xong scene " + ScenePath);
        }

        private const string SkillFree = "KHÔNG TỐN NĂNG LƯỢNG";
        private const string SkillCost = "TỐN NĂNG LƯỢNG";
        private const string SkillChoice = "3 LỰA CHỌN  ›";

        /// <summary>Dữ liệu tạm của 6 xe, đúng thứ tự thẻ trong danh sách bên trái.</summary>
        private static readonly TankSpec[] TankSprites =
        {
            new TankSpec
            {
                SpriteName = "wall_titan",
                DisplayName = "WALL TITAN",
                ListRole = "TANKER  /  HỖ TRỢ",
                HeroRole = "TANKER HỖ TRỢ",
                Level = 1,
                NextLevel = 2,
                UpgradeCost = 5,
                Stats = new[] { 14, 900, 100, 22, 85, 0, 0, 0 },
                SkillNames = new[] { "Earth Break", "Earth Wall", "Stone Fortress", "Immovable Titan" },
                SkillDescriptions = new[] { SkillFree, SkillCost, SkillChoice, SkillChoice }
            },
            new TankSpec
            {
                SpriteName = "red_nova",
                DisplayName = "RED NOVA",
                ListRole = "DPS  /  HỎA LỰC XA",
                HeroRole = "XẠ THỦ HỎA LỰC",
                Level = 10,
                NextLevel = 11,
                UpgradeCost = 5,
                Stats = new[] { 26, 520, 130, 8, 120, 0, 0, 0 },
                SkillNames = new[] { "Nova Shot", "Triple Nova", "Overdrive", "Nova Barrage" },
                SkillDescriptions = new[] { SkillFree, SkillCost, SkillChoice, SkillChoice }
            },
            new TankSpec
            {
                SpriteName = "black_shadow",
                DisplayName = "BLACK SHADOW",
                ListRole = "DPS  /  SÁT THỦ",
                HeroRole = "SÁT THỦ CƠ ĐỘNG",
                Level = 5,
                NextLevel = 6,
                UpgradeCost = 5,
                Stats = new[] { 20, 650, 120, 12, 110, 0, 0, 0 },
                SkillNames = new[] { "Shadow Claw", "Shadow Step", "Shadow Predator", "Shadow Ambush" },
                SkillDescriptions = new[] { SkillFree, SkillCost, SkillChoice, SkillChoice }
            },
            new TankSpec
            {
                SpriteName = "leviathan",
                DisplayName = "LEVIATHAN",
                ListRole = "TANKER  /  TANKER",
                HeroRole = "TANKER ĐỘI HÌNH",
                Level = 1,
                NextLevel = 2,
                UpgradeCost = 5,
                Stats = new[] { 16, 820, 110, 18, 95, 0, 0, 0 },
                SkillNames = new[] { "Water Pulse", "Water Bubble", "Water Armor", "Tidal Return" },
                SkillDescriptions = new[] { SkillFree, SkillCost, SkillChoice, SkillChoice }
            },
            new TankSpec
            {
                SpriteName = "water_b",
                DisplayName = "WATER B",
                ListRole = "HỖ TRỢ  /  DPS",
                HeroRole = "HỖ TRỢ HỒI MÁU",
                Level = 1,
                NextLevel = 2,
                UpgradeCost = 5,
                Stats = new[] { 15, 600, 140, 10, 105, 0, 0, 0 },
                SkillNames = new[] { "Healing Water", "Bubble Shield", "Shared Healing", "Mobile Altar" },
                SkillDescriptions = new[] { SkillFree, SkillCost, SkillChoice, SkillChoice }
            },
            new TankSpec
            {
                SpriteName = "green_fox",
                DisplayName = "GREEN FOX",
                ListRole = "DPS  /  DPS",
                HeroRole = "SÁT THỦ ĐỘC",
                Level = 1,
                NextLevel = 2,
                UpgradeCost = 5,
                Stats = new[] { 22, 560, 125, 9, 118, 0, 0, 0 },
                SkillNames = new[] { "Fox Fang", "Fox Mark", "Mark Hunter", "Mark Explosion" },
                SkillDescriptions = new[] { SkillFree, SkillCost, SkillChoice, SkillChoice }
            }
        };

        /// <summary>
        /// Tạo cụm nội dung động cho panel phụ kiện: icon, tên, nhóm, chỉ số và hiệu ứng.
        /// Các lớp nền và nút ĐỔI PHỤ KIỆN / PHÂN RÃ vẫn nằm trong ảnh right_stats_dynamic.
        /// </summary>
        private static GarageAccessoryPanel AddAccessoryPanel(RectTransform root, TMP_FontAsset barlow600, TMP_FontAsset barlow700)
        {
            GameObject container = new GameObject("AccessoryInfo", typeof(RectTransform));
            RectTransform containerRect = container.GetComponent<RectTransform>();
            containerRect.SetParent(root, false);
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            Image icon = AddImage(containerRect, new LayerRect("AccessoryIcon", 1313f, 711f, 82f, 82f), null);
            icon.preserveAspect = true;
            icon.enabled = false;

            TextMeshProUGUI name = AddText(containerRect, "TextAccessoryName", string.Empty, 1412f, 695f, 19f, new Color32(0xE6, 0xED, 0xF5, 0xFF), barlow700, TextAlignmentOptions.TopLeft, 0.4f);
            PlaceTopLeft(name, 1412f, 695f, 414f, 24f);
            name.enableAutoSizing = true;
            name.fontSizeMin = 10f;
            name.fontSizeMax = 19f;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;

            TextMeshProUGUI category = AddText(containerRect, "TextAccessoryCategory", string.Empty, 1412f, 722f, 9.2f, new Color32(0xC9, 0xA9, 0xFF, 0xFF), barlow700, TextAlignmentOptions.TopLeft, 0.8f);
            PlaceTopLeft(category, 1412f, 722f, 414f, 12f);
            category.textWrappingMode = TextWrappingModes.NoWrap;
            category.overflowMode = TextOverflowModes.Ellipsis;

            TextMeshProUGUI stats = AddText(containerRect, "TextAccessoryStats", string.Empty, 1412f, 739f, 10.2f, new Color32(0x7F, 0xE0, 0x8A, 0xFF), barlow600, TextAlignmentOptions.TopLeft, 0.3f);
            PlaceTopLeft(stats, 1412f, 739f, 414f, 27f);
            stats.textWrappingMode = TextWrappingModes.Normal;
            stats.overflowMode = TextOverflowModes.Ellipsis;

            TextMeshProUGUI description = AddText(containerRect, "TextAccessoryDescription", string.Empty, 1412f, 768f, 8.8f, new Color32(0x9F, 0xAD, 0xBC, 0xFF), barlow600, TextAlignmentOptions.TopLeft, 0.2f);
            PlaceTopLeft(description, 1412f, 768f, 414f, 56f);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.overflowMode = TextOverflowModes.Ellipsis;

            GarageAccessoryPanel panel = container.AddComponent<GarageAccessoryPanel>();
            SerializedObject serialized = new SerializedObject(panel);
            serialized.FindProperty("m_Catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AccessoryCatalog>(CatalogPath);
            serialized.FindProperty("m_Icon").objectReferenceValue = icon;
            serialized.FindProperty("m_AccessoryName").objectReferenceValue = name;
            serialized.FindProperty("m_Category").objectReferenceValue = category;
            serialized.FindProperty("m_Stats").objectReferenceValue = stats;
            serialized.FindProperty("m_Description").objectReferenceValue = description;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        /// <summary>Đặt vùng chữ theo toạ độ gốc trên-trái thay vì theo baseline.</summary>
        private static void PlaceTopLeft(TextMeshProUGUI text, float x, float y, float width, float height)
        {
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, DesignHeight - y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>Thêm một lớp ảnh với toạ độ pixel đúng theo thiết kế.</summary>
        private static Image AddImage(RectTransform parent, LayerRect layer, Sprite sprite)
        {
            GameObject go = new GameObject(layer.Name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(layer.X, DesignHeight - (layer.Y + layer.Height));
            rect.sizeDelta = sprite != null ? new Vector2(sprite.rect.width, sprite.rect.height) : new Vector2(layer.Width, layer.Height);
            rect.localScale = Vector3.one;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Thêm một vùng chữ động, canh theo đường baseline của thiết kế.</summary>
        private static TextMeshProUGUI AddText(RectTransform parent, string name, string value, float x, float baselineY, float fontSize, Color color, TMP_FontAsset font, TextAlignmentOptions alignment, float letterSpacing)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            float ascentPx = font.faceInfo.ascentLine / font.faceInfo.pointSize * fontSize;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.sizeDelta = new Vector2(900f, fontSize * 1.6f);
            rect.anchoredPosition = new Vector2(x, DesignHeight - (baselineY - ascentPx));

            if (alignment == TextAlignmentOptions.TopRight)
            {
                rect.pivot = new Vector2(1f, 1f);
            }
            else if (alignment == TextAlignmentOptions.Top)
            {
                rect.pivot = new Vector2(0.5f, 1f);
            }
            else
            {
                rect.pivot = new Vector2(0f, 1f);
            }

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.characterSpacing = letterSpacing / fontSize * 100f;
            return text;
        }

        private static Sprite LoadSkillIcon(string iconName)
        {
            string path = SpriteDir + "/Skill/" + iconName + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("[GarageBuilder] Không tìm thấy icon kỹ năng: " + path);
            }

            return sprite;
        }

        /// <summary>Lấy icon kỹ năng tùy chọn từ bộ ảnh dùng chung với màn Picker.</summary>
        private static Sprite LoadPickerIcon(int tankIndex, int tabIndex, int optionIndex)
        {
            string path = "Assets/Art/UI/Picker/SkillIcons/" + SkillCatalog.GetIconKey(tankIndex, tabIndex, optionIndex) + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("[GarageBuilder] Không tìm thấy icon tùy chọn: " + path);
            }

            return sprite;
        }

        private static Sprite LoadSprite(string spriteName)
        {
            string path = SpriteDir + "/" + spriteName + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("[GarageBuilder] Không tìm thấy sprite: " + path);
            }

            return sprite;
        }
    }
}
