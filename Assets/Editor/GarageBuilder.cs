using System.Collections.Generic;
using System.IO;
using IronWasteland.Tanks;
using IronWasteland.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace IronWasteland.EditorTools
{
    /// <summary>
    /// Dựng scene Garage bằng code: các panel (danh sách xe, chỉ số, kỹ năng, phụ kiện,
    /// thanh cấp độ) được vẽ procedural theo docs/design/garage-spec.md, mọi chữ động
    /// do GarageController đổ từ TankRoster.
    /// </summary>
    public static class GarageBuilder
    {
        private const string SpriteDir = "Assets/Art/UI/Garage";
        private const string FontDir = "Assets/Art/Fonts";
        private const string GeneratedFontDir = "Assets/Art/Fonts/Generated";
        private const string ScenePath = "Assets/Scenes/Garage.unity";
        private const string CatalogPath = "Assets/Settings/AccessoryCatalog.asset";
        private const string RosterPath = "Assets/Data/Tanks/TankRoster.asset";
        private const string PickerIconDir = "Assets/Art/UI/Picker/SkillIcons";
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;

        private const string SkillFree = "KHÔNG TỐN NĂNG LƯỢNG";
        private const string SkillCost = "TỐN NĂNG LƯỢNG";
        private const string SkillChoice = "3 LỰA CHỌN  ›";

        /// <summary>Màu dùng chung theo spec.</summary>
        private static readonly Color32 Ink = new Color32(0xE6, 0xED, 0xF5, 0xFF);
        private static readonly Color32 InkDim = new Color32(0x8A, 0x99, 0xAB, 0xFF);
        private static readonly Color32 InkFaint = new Color32(0x5A, 0x66, 0x75, 0xFF);
        private static readonly Color32 InkGhost = new Color32(0x3D, 0x4A, 0x59, 0xFF);
        private static readonly Color32 TextSoft = new Color32(0xC4, 0xD0, 0xDC, 0xFF);
        private static readonly Color32 PanelFill = new Color32(0x0E, 0x14, 0x1C, 0xFF);
        private static readonly Color32 PanelEdge = new Color32(0x26, 0x31, 0x3D, 0xFF);
        private static readonly Color32 TrackColor = new Color32(0x1A, 0x22, 0x30, 0xFF);
        private static readonly Color32 Orange = new Color32(0xFF, 0x7A, 0x18, 0xFF);
        private static readonly Color32 OrangeDark = new Color32(0xE8, 0x5F, 0x00, 0xFF);
        private static readonly Color32 OrangeSoft = new Color32(0xFF, 0xB3, 0x47, 0xFF);
        private static readonly Color32 Purple = new Color32(0x8A, 0x4B, 0xF0, 0xFF);
        private static readonly Color32 PurpleSoft = new Color32(0xC9, 0xA9, 0xFF, 0xFF);
        private static readonly Color32 Teal = new Color32(0x2F, 0xE8, 0xE0, 0xFF);
        private static readonly Color32 NearBlack = new Color32(0x0B, 0x0F, 0x14, 0xFF);

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

            SeedTankAssets();
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

        /// <summary>Dữ liệu tạm để seed vào asset xe: tiền tố sprite và màu chủ đề.</summary>
        private struct TankSeed
        {
            public string Prefix;
            public Color Accent;
            public Vector2 PortraitOffset;
        }

        private static readonly Dictionary<string, TankSeed> Seeds = new Dictionary<string, TankSeed>
        {
            // PortraitOffset = độ lệch khối tâm alpha của ảnh chân dung so với tâm khung (đo từ ảnh 1024², quy về pixel thiết kế).
            { "wall_titan", new TankSeed { Prefix = "wall_titan", Accent = new Color32(0xE0, 0xA9, 0x4B, 0xFF), PortraitOffset = new Vector2(6.1f, 0f) } },
            { "red_nova", new TankSeed { Prefix = "red_nova", Accent = new Color32(0xFF, 0x5A, 0x3C, 0xFF), PortraitOffset = new Vector2(-1.9f, 0f) } },
            { "black_shadow", new TankSeed { Prefix = "black_shadow", Accent = new Color32(0x8A, 0x4B, 0xF0, 0xFF), PortraitOffset = new Vector2(22.9f, 0f) } },
            { "leviathan", new TankSeed { Prefix = "leviathan", Accent = new Color32(0x2F, 0xE8, 0xE0, 0xFF), PortraitOffset = new Vector2(-2.8f, 0f) } },
            { "water_b", new TankSeed { Prefix = "water_b", Accent = new Color32(0x7F, 0xB3, 0xFF, 0xFF), PortraitOffset = new Vector2(0.1f, 0f) } },
            { "green_fox", new TankSeed { Prefix = "green_fox", Accent = new Color32(0x3D, 0x4A, 0x59, 0xFF), PortraitOffset = new Vector2(14.6f, 0f) } }
        };

        /// <summary>Đổi tên asset PascalCase sang tiền tố sprite snake_case (WallTitan -> wall_titan).</summary>
        private static string ToSnakeCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsUpper(character) && i > 0)
                {
                    char previous = value[i - 1];
                    bool nextIsLower = i + 1 < value.Length && char.IsLower(value[i + 1]);
                    if (char.IsLower(previous) || char.IsDigit(previous) || nextIsLower)
                    {
                        builder.Append('_');
                    }
                }

                builder.Append(char.ToLowerInvariant(character));
            }

            return builder.ToString();
        }

        /// <summary>Ghi ảnh chân dung, màu chủ đề, mô tả và icon kỹ năng vào 6 asset TankDefinition.</summary>
        private static void SeedTankAssets()
        {
            TankRoster roster = AssetDatabase.LoadAssetAtPath<TankRoster>(RosterPath);
            if (roster == null)
            {
                Debug.LogError("[GarageBuilder] Không tìm thấy TankRoster: " + RosterPath);
                return;
            }

            string[] descriptions = { SkillFree, SkillCost, SkillChoice, SkillChoice };
            int count = roster.Tanks.Count;
            for (int i = 0; i < count; i++)
            {
                TankDefinition definition = roster.Tanks[i];
                if (definition == null)
                {
                    continue;
                }

                TankSeed seed;
                if (!Seeds.TryGetValue(ToSnakeCase(definition.name), out seed))
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(definition);
                serialized.FindProperty("portrait").objectReferenceValue = LoadSprite("tank_" + seed.Prefix);
                serialized.FindProperty("rosterAccent").colorValue = seed.Accent;
                serialized.FindProperty("portraitOffset").vector2Value = seed.PortraitOffset;

                SerializedProperty skillIcons = serialized.FindProperty("skillIcons");
                skillIcons.arraySize = 4;
                SerializedProperty descriptionsProperty = serialized.FindProperty("skillDescriptions");
                descriptionsProperty.arraySize = 4;
                for (int slot = 0; slot < 4; slot++)
                {
                    skillIcons.GetArrayElementAtIndex(slot).objectReferenceValue = LoadSkillIcon(seed.Prefix + "_" + slot);
                    descriptionsProperty.GetArrayElementAtIndex(slot).stringValue = descriptions[slot];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>Dựng toàn bộ scene: canvas, nền, các panel procedural và chữ động.</summary>
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

            // Scene có Button/ScrollRect bắt buộc phải có EventSystem, thiếu là chuột không tới nút.
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystemObject, scene);

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

            // Số ngọc trong header.png là ảnh nướng: phủ một ô màu nền pill rồi in số động lên trên.
            Image gemPatch = AddImage(root, new LayerRect("GemCountPatch", 1644f, 92f, 70f, 32f), null);
            gemPatch.color = new Color32(0x12, 0x18, 0x21, 0xFF);
            TextMeshProUGUI gemCount = AddRowText(root, "GemCount", 1648f, 92f, 84f, 32f, 26f, new Color32(0xFF, 0xD7, 0x9A, 0xFF), barlow900, TextAlignmentOptions.Left);
            gemCount.text = "0";
            // Ornaments nằm DƯỚI các panel để không đè chữ của panel phụ kiện.
            AddImage(root, new LayerRect("Ornaments", 27f, 27f, 1871f, 1041f), LoadSprite("ornaments"));

            RectTransform rowContent;
            GarageTankRow rowTemplate;
            ScrollRect tankScroll;
            AddTankListPanel(root, barlow600, barlow700, barlow900, out rowContent, out rowTemplate, out tankScroll);

            AddImage(root, new LayerRect("CenterUnit", 467f, 171f, 802f, 470f), LoadSprite("center_unit"));
            // Ảnh chân dung là sprite vuông 1024², Image.preserveAspect căn sprite theo góc trái khung chứ không vào giữa,
            // nên khung phải đúng vuông 330×330 và đặt sao cho tâm khung trùng tâm vòng tròn (868, 405.5).
            Image heroPortrait = AddImage(root, new LayerRect("HeroPortrait", 703f, 240.5f, 330f, 330f), null);
            heroPortrait.preserveAspect = true;
            heroPortrait.color = Color.white;
            heroPortrait.raycastTarget = false;
            TextMeshProUGUI heroCaption = AddText(root, "TextHeroCaption", "CURRENT UNIT", 502f, 238.34f, 18f, InkGhost, mono, TextAlignmentOptions.TopLeft, 0f);
            TextMeshProUGUI heroLevel = AddText(root, "TextHeroLevel", "01", 1152f, 254.73f, 27.28f, Teal, anton, TextAlignmentOptions.TopLeft, 0f);
            TextMeshProUGUI heroName = AddText(root, "TextHeroName", "TANK", 868f, 606.10f, 39.6f, Color.white, anton, TextAlignmentOptions.Top, 1.4f);
            TextMeshProUGUI heroRole = AddText(root, "TextHeroRole", string.Empty, 868f, 627f, 22f, PurpleSoft, barlow700, TextAlignmentOptions.Top, 1.4f);
            heroName.rectTransform.sizeDelta = new Vector2(700f, 50f);
            heroRole.rectTransform.sizeDelta = new Vector2(700f, 30f);

            Image levelFill;
            TextMeshProUGUI levelValue;
            TextMeshProUGUI levelNext;
            TextMeshProUGUI upgradeCost;
            Button upgradeButton = AddLevelBar(root, anton, barlow600, barlow700, barlow900, out levelFill, out levelValue, out levelNext, out upgradeCost);

            TextMeshProUGUI[] statValues;
            Image[] statBars;
            AddStatsPanel(root, anton, barlow600, barlow900, out statValues, out statBars);

            TextMeshProUGUI[] skillNames;
            TextMeshProUGUI[] skillDescriptions;
            Image[] skillIcons;
            Button optionButton1;
            Button optionButton2;
            AddSkillPanel(root, barlow600, barlow700, barlow900, out skillNames, out skillDescriptions, out skillIcons, out optionButton1, out optionButton2);

            GarageAccessoryPanel accessoryPanel;
            Button accessoryButton;
            Button salvageButton;
            AddAccessoryPanel(root, barlow600, barlow700, barlow900, out accessoryPanel, out accessoryButton, out salvageButton);

            AddImage(root, new LayerRect("DeployPanel", 1291f, 889f, 558f, 88f), LoadSprite("cta"));
            Button deployButton = AddTransparentButton(root, "DeployButton", 1292f, 890f, 556f, 86f);
            Button backButton = AddTransparentButton(root, "BackButton", 72f, 100f, 158f, 48f);

            GarageController controller = canvasObject.AddComponent<GarageController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("m_Roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TankRoster>(RosterPath);
            serialized.FindProperty("m_RowContent").objectReferenceValue = rowContent;
            serialized.FindProperty("m_RowTemplate").objectReferenceValue = rowTemplate;
            serialized.FindProperty("m_TankScroll").objectReferenceValue = tankScroll;
            serialized.FindProperty("m_HeroImage").objectReferenceValue = heroPortrait;
            serialized.FindProperty("m_HeroCaption").objectReferenceValue = heroCaption;
            serialized.FindProperty("m_HeroName").objectReferenceValue = heroName;
            serialized.FindProperty("m_HeroRole").objectReferenceValue = heroRole;
            serialized.FindProperty("m_HeroLevel").objectReferenceValue = heroLevel;
            serialized.FindProperty("m_LevelValue").objectReferenceValue = levelValue;
            serialized.FindProperty("m_LevelNext").objectReferenceValue = levelNext;
            serialized.FindProperty("m_UpgradeCost").objectReferenceValue = upgradeCost;
            serialized.FindProperty("m_GemCount").objectReferenceValue = gemCount;
            serialized.FindProperty("m_LevelBarFill").objectReferenceValue = levelFill;
            serialized.FindProperty("m_UpgradeButton").objectReferenceValue = upgradeButton;
            serialized.FindProperty("m_BackButton").objectReferenceValue = backButton;
            serialized.FindProperty("m_DeployButton").objectReferenceValue = deployButton;
            serialized.FindProperty("m_OptionButton1").objectReferenceValue = optionButton1;
            serialized.FindProperty("m_OptionButton2").objectReferenceValue = optionButton2;
            serialized.FindProperty("m_AccessoryButton").objectReferenceValue = accessoryButton;
            serialized.FindProperty("m_SalvageButton").objectReferenceValue = salvageButton;
            serialized.FindProperty("m_AccessoryPanel").objectReferenceValue = accessoryPanel;

            SerializedProperty skillNameProperty = serialized.FindProperty("m_SkillNames");
            skillNameProperty.arraySize = 4;
            SerializedProperty skillDescProperty = serialized.FindProperty("m_SkillDescriptions");
            skillDescProperty.arraySize = 4;
            SerializedProperty skillIconProperty = serialized.FindProperty("m_SkillIcons");
            skillIconProperty.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                skillNameProperty.GetArrayElementAtIndex(i).objectReferenceValue = skillNames[i];
                skillDescProperty.GetArrayElementAtIndex(i).objectReferenceValue = skillDescriptions[i];
                skillIconProperty.GetArrayElementAtIndex(i).objectReferenceValue = skillIcons[i];
            }

            SerializedProperty statProperty = serialized.FindProperty("m_StatValues");
            statProperty.arraySize = 8;
            SerializedProperty statBarProperty = serialized.FindProperty("m_StatBars");
            statBarProperty.arraySize = 8;
            for (int i = 0; i < 8; i++)
            {
                statProperty.GetArrayElementAtIndex(i).objectReferenceValue = statValues[i];
                statBarProperty.GetArrayElementAtIndex(i).objectReferenceValue = statBars[i];
            }

            // 36 icon tùy chọn, xếp theo thứ tự TankRoster x tab x lựa chọn.
            SerializedProperty optionIcons = serialized.FindProperty("m_OptionSkillIcons");
            optionIcons.arraySize = 36;
            for (int tank = 0; tank < 6; tank++)
            {
                for (int tab = 0; tab < SkillCatalog.TabCount; tab++)
                {
                    for (int option = 0; option < SkillCatalog.OptionCount; option++)
                    {
                        int index = tank * (SkillCatalog.TabCount * SkillCatalog.OptionCount)
                            + tab * SkillCatalog.OptionCount + option;
                        optionIcons.GetArrayElementAtIndex(index).objectReferenceValue = LoadPickerIcon(tank, tab, option);
                    }
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[GarageBuilder] Đã dựng xong scene " + ScenePath);
        }

        /// <summary>
        /// Panel danh sách xe: header riêng, vùng cuộn dọc và hàng mẫu ẩn sẵn.
        /// Controller nhân bản hàng mẫu cho từng xe trong TankRoster.
        /// </summary>
        private static void AddTankListPanel(RectTransform root, TMP_FontAsset barlow600, TMP_FontAsset barlow700, TMP_FontAsset barlow900,
            out RectTransform content, out GarageTankRow template, out ScrollRect scroll)
        {
            const float panelX = 72f;
            const float panelY = 180.5f;
            const float panelWidth = 372f;
            const float panelHeight = 771.5f;
            const float headerHeight = 63.5f;
            const float rowHeight = 108f;

            GameObject panelObject = new GameObject("TankListPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.SetParent(root, false);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.zero;
            panelRect.pivot = Vector2.zero;
            panelRect.anchoredPosition = new Vector2(panelX, DesignHeight - (panelY + panelHeight));
            panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

            Image background = panelObject.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0f);
            background.raycastTarget = true;

            AddRowImage(panelRect, "ListAccent", 0f, 13.5f, 3f, 22f, UISprites.Rounded(1), Orange);
            TextMeshProUGUI title = AddRowText(panelRect, "ListTitle", 16f, 6f, 240f, 26f, 20f, Ink, barlow900, TextAlignmentOptions.TopLeft);
            title.text = "DANH SÁCH XE TĂNG";
            TextMeshProUGUI subtitle = AddRowText(panelRect, "ListSubtitle", 16f, 34f, 200f, 20f, 14f, InkFaint, barlow600, TextAlignmentOptions.TopLeft);
            subtitle.text = "06 UNITS";
            TextMeshProUGUI count = AddRowText(panelRect, "ListCount", 292f, 8f, 64f, 26f, 26f, InkGhost, barlow700, TextAlignmentOptions.TopRight);
            count.text = "06";

            GameObject viewportObject = new GameObject("TankViewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.SetParent(panelRect, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0.5f, 1f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = new Vector2(0f, -headerHeight);
            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;

            GameObject contentObject = new GameObject("TankRows", typeof(RectTransform));
            content = contentObject.GetComponent<RectTransform>();
            content.SetParent(viewportRect, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = new Vector2(0f, -headerHeight);
            content.sizeDelta = new Vector2(0f, 0f);

            GameObject rowObject = new GameObject("TankRowTemplate", typeof(RectTransform), typeof(GarageTankRow));
            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.SetParent(content, false);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, rowHeight);
            rowRect.anchoredPosition = Vector2.zero;

            Image rowBackground = AddRowShape(rowRect, "RowBackground", 0f, 0f, panelWidth, rowHeight, 18, PanelFill, PanelFill, PanelEdge, 1f);
            rowBackground.raycastTarget = true;

            Button rowButton = rowObject.AddComponent<Button>();
            rowButton.targetGraphic = rowBackground;
            rowButton.transition = Selectable.Transition.None;

            Image rowAccent = AddRowImage(rowRect, "RowAccent", 0f, 0f, 4f, rowHeight, UISprites.Rounded(2), Orange);

            AddRowShape(rowRect, "RowIconBg", 14f, 14f, 80f, 80f, 8, new Color32(0x0A, 0x0F, 0x16, 0xFF), new Color32(0x0A, 0x0F, 0x16, 0xFF), PanelEdge, 1f);
            Image rowIcon = AddRowImage(rowRect, "RowIcon", 14f, 14f, 80f, 80f, null, Color.white);
            rowIcon.preserveAspect = true;

            Image rowLock = AddRowImage(rowRect, "RowLock", 14f, 14f, 80f, 80f, LoadSprite("lock_overlay"), Color.white);
            rowLock.preserveAspect = true;
            rowLock.gameObject.SetActive(false);

            Image rowSelection = AddRowImage(rowRect, "RowSelection", 0f, 0f, panelWidth, rowHeight, UISprites.Frame(10, 2), Purple);
            rowSelection.rectTransform.anchorMin = Vector2.zero;
            rowSelection.rectTransform.anchorMax = Vector2.one;
            rowSelection.rectTransform.offsetMin = Vector2.zero;
            rowSelection.rectTransform.offsetMax = Vector2.zero;
            rowSelection.type = Image.Type.Sliced;
            rowSelection.gameObject.SetActive(false);

            TextMeshProUGUI rowName = AddRowText(rowRect, "RowName", 108.5f, 8f, 250f, 32f, 24f, Ink, barlow900, TextAlignmentOptions.TopLeft);
            TextMeshProUGUI rowRole = AddRowText(rowRect, "RowRole", 108.5f, 36f, 250f, 26f, 18f, PurpleSoft, barlow700, TextAlignmentOptions.TopLeft);
            TextMeshProUGUI rowLevel = AddRowText(rowRect, "RowLevel", 108.5f, 62f, 170f, 24f, 16f, InkDim, barlow600, TextAlignmentOptions.TopLeft);
            TextMeshProUGUI rowUnlock = AddRowText(rowRect, "RowUnlock", 280f, 62f, 80f, 24f, 16f, OrangeSoft, barlow700, TextAlignmentOptions.TopRight);

            AddRowImage(rowRect, "RowBarBg", 108.5f, 92f, 180f, 5f, UISprites.Rounded(2), TrackColor);
            Image rowBarFill = AddRowImage(rowRect, "RowBarFill", 108.5f, 92f, 180f, 5f, UISprites.Rounded(2), Teal);
            rowBarFill.type = Image.Type.Filled;
            rowBarFill.fillMethod = Image.FillMethod.Horizontal;
            rowBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            rowBarFill.fillAmount = 0f;

            SerializedObject rowSerialized = new SerializedObject(rowObject.GetComponent<GarageTankRow>());
            rowSerialized.FindProperty("m_NameText").objectReferenceValue = rowName;
            rowSerialized.FindProperty("m_Role").objectReferenceValue = rowRole;
            rowSerialized.FindProperty("m_Level").objectReferenceValue = rowLevel;
            rowSerialized.FindProperty("m_Unlock").objectReferenceValue = rowUnlock;
            rowSerialized.FindProperty("m_Icon").objectReferenceValue = rowIcon;
            rowSerialized.FindProperty("m_BarFill").objectReferenceValue = rowBarFill;
            rowSerialized.FindProperty("m_LockOverlay").objectReferenceValue = rowLock;
            rowSerialized.FindProperty("m_Selection").objectReferenceValue = rowSelection;
            rowSerialized.FindProperty("m_Accent").objectReferenceValue = rowAccent;
            rowSerialized.FindProperty("m_Button").objectReferenceValue = rowButton;
            rowSerialized.ApplyModifiedPropertiesWithoutUndo();

            template = rowObject.GetComponent<GarageTankRow>();
            rowObject.SetActive(false);

            scroll = panelObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.elasticity = 0.1f;
            scroll.scrollSensitivity = 40f;
            scroll.viewport = viewportRect;
            scroll.content = content;
        }

        /// <summary>Thanh cấp độ dưới khung hero: số cấp, track 300 px, nút NÂNG CẤP có giá.</summary>
        private static Button AddLevelBar(RectTransform root, TMP_FontAsset anton, TMP_FontAsset barlow600, TMP_FontAsset barlow700, TMP_FontAsset barlow900,
            out Image fill, out TextMeshProUGUI levelValue, out TextMeshProUGUI levelNext, out TextMeshProUGUI upgradeCost)
        {
            const float barWidth = 800f;
            const float barHeight = 72f;
            GameObject container = new GameObject("LevelBar", typeof(RectTransform));
            RectTransform rect = container.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(468f, DesignHeight - (656f + barHeight));
            rect.sizeDelta = new Vector2(barWidth, barHeight);

            AddRowShape(rect, "LevelBarBg", 0f, 0f, barWidth, barHeight, 16, PanelFill, PanelFill, PanelEdge, 1f);
            AddRowImage(rect, "LevelBarAccent", 0f, 0f, 3f, barHeight, UISprites.Rounded(1), Orange);

            TextMeshProUGUI label = AddRowText(rect, "LevelLabel", 18f, 6f, 150f, 20f, 14f, InkFaint, barlow600, TextAlignmentOptions.TopLeft);
            label.text = "CẤP ĐỘ HIỆN TẠI";
            levelValue = AddRowText(rect, "LevelValue", 18f, 24f, 60f, 42f, 34f, Ink, anton, TextAlignmentOptions.TopLeft);
            levelValue.text = "01";
            TextMeshProUGUI maxLabel = AddRowText(rect, "LevelMax", 62f, 42f, 44f, 22f, 16f, InkFaint, barlow600, TextAlignmentOptions.TopLeft);
            maxLabel.text = "/ 90";
            levelNext = AddRowText(rect, "LevelNext", 132f, 32f, 96f, 22f, 16f, OrangeSoft, barlow700, TextAlignmentOptions.TopLeft);
            levelNext.text = "+ CẤP 2";

            AddRowImage(rect, "LevelTrack", 236f, 40f, 300f, 10f, UISprites.Square(), TrackColor);
            fill = AddRowImage(rect, "LevelFill", 236f, 40f, 300f, 10f, UISprites.Square(), Orange);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            for (int tick = 1; tick < 10; tick++)
            {
                AddRowImage(rect, "LevelTick" + tick, 236f + tick * 30f, 36f, 1f, 18f, UISprites.Square(), new Color32(0x26, 0x31, 0x3D, 0xE6));
            }

            GameObject buttonObject = new GameObject("UpgradeButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(rect, false);
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(560f, -16f);
            buttonRect.sizeDelta = new Vector2(228f, 54f);
            Image buttonImage = buttonObject.GetComponent<Image>();
            // Dùng chính art crop của nút cam trong upgrade.png để viên ngọc ma pháp hiện đúng như thiết kế.
            buttonImage.sprite = LoadSprite("upgrade_button_clean");
            buttonImage.type = Image.Type.Simple;
            Button upgradeButton = buttonObject.GetComponent<Button>();
            upgradeButton.targetGraphic = buttonImage;
            upgradeButton.transition = Selectable.Transition.ColorTint;
            ColorBlock upgradeColors = upgradeButton.colors;
            upgradeColors.disabledColor = new Color(0.62f, 0.62f, 0.62f, 0.92f);
            upgradeButton.colors = upgradeColors;

            // Viên ngọc tách riêng khỏi art để canh giữa nút; chữ "NÂNG CẤP" vẫn nướng trong art.
            GameObject gemObject = new GameObject("UpgradeGem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform gemRect = gemObject.GetComponent<RectTransform>();
            gemRect.SetParent(buttonRect, false);
            gemRect.anchorMin = new Vector2(0f, 1f);
            gemRect.anchorMax = new Vector2(0f, 1f);
            gemRect.pivot = new Vector2(0.5f, 0.5f);
            gemRect.sizeDelta = new Vector2(62f, 44f);
            gemRect.anchoredPosition = new Vector2(188f, -26f);
            Image gemImage = gemObject.GetComponent<Image>();
            gemImage.sprite = LoadSprite("upgrade_gem");
            gemImage.raycastTarget = false;

            // Nút art có dải tối phía trên nên tâm thị giác của khối cam nằm ở y ≈ 32,5 chứ không phải 27.
            upgradeCost = AddRowText(buttonRect, "UpgradeCost", 72f, 2f, 92f, 62f, 36f, NearBlack, barlow900, TextAlignmentOptions.Right);
            upgradeCost.text = "5";

            return upgradeButton;
        }

        /// <summary>Panel 8 chỉ số bên phải: chấm màu, nhãn, track 488 px, số và đơn vị canh mép phải.</summary>
        private static void AddStatsPanel(RectTransform root, TMP_FontAsset anton, TMP_FontAsset barlow600, TMP_FontAsset barlow900,
            out TextMeshProUGUI[] values, out Image[] bars)
        {
            const float panelWidth = 556f;
            const float panelHeight = 464f;
            GameObject container = new GameObject("StatsPanel", typeof(RectTransform));
            RectTransform rect = container.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(1292f, DesignHeight - (190f + panelHeight));
            rect.sizeDelta = new Vector2(panelWidth, panelHeight);

            AddRowShape(rect, "StatsBg", 0f, 0f, panelWidth, panelHeight, 20, new Color32(0x12, 0x18, 0x21, 0xFF), new Color32(0x0A, 0x0E, 0x13, 0xFF), PanelEdge, 1f);
            AddRowImage(rect, "StatsAccent", 0f, 0f, 3f, 34f, UISprites.Rounded(1), Teal);
            TextMeshProUGUI title = AddRowText(rect, "StatsTitle", 18f, 5f, 240f, 24f, 18f, Ink, barlow900, TextAlignmentOptions.TopLeft);
            title.text = "CHỈ SỐ CHIẾN XA";
            TextMeshProUGUI caption = AddRowText(rect, "StatsCaption", 440f, 8f, 96f, 20f, 15f, InkGhost, barlow600, TextAlignmentOptions.TopRight);
            caption.text = "08 STATS";

            string[] labels = { "TẦN CÔNG", "MÁU TỐI ĐA", "GIỚI HẠN NĂNG LƯỢNG", "PHÒNG THỦ", "TỐC ĐỘ DI CHUYỂN", "TỈ LỆ GIẢM HỒI CHIÊU", "TỈ LỆ SÁT THƯƠNG", "TỈ LỆ XUYÊN GIÁP" };
            string[] units = { "ATK", "HP", "EN", "DEF", "SPD", "%", "%", "%" };
            Color32[] colors =
            {
                new Color32(0xFF, 0x7A, 0x18, 0xFF),
                new Color32(0xFF, 0x3B, 0x4E, 0xFF),
                new Color32(0x7F, 0xB3, 0xFF, 0xFF),
                new Color32(0x2F, 0xE8, 0xE0, 0xFF),
                new Color32(0x4A, 0xDE, 0x80, 0xFF),
                new Color32(0xC9, 0xA9, 0xFF, 0xFF),
                new Color32(0xFF, 0xB3, 0x47, 0xFF),
                new Color32(0x8A, 0x4B, 0xF0, 0xFF)
            };

            values = new TextMeshProUGUI[8];
            bars = new Image[8];
            for (int i = 0; i < 8; i++)
            {
                float rowY = 49f * i;
                AddRowImage(rect, "StatDot" + i, 16.5f, 68.5f + rowY, 7f, 7f, UISprites.Rounded(3), colors[i]);
                TextMeshProUGUI label = AddRowText(rect, "StatLabel" + i, 34.5f, 62f + rowY, 260f, 22f, 15f, TextSoft, barlow600, TextAlignmentOptions.TopLeft);
                label.text = labels[i];

                values[i] = AddRowText(rect, "StatValue" + i, 356f, 60f + rowY, 148f, 32f, 26f, Ink, anton, TextAlignmentOptions.TopRight);
                values[i].text = "0";

                TextMeshProUGUI unit = AddRowText(rect, "StatUnit" + i, 456f, 66f + rowY, 76f, 22f, 14f, InkFaint, barlow600, TextAlignmentOptions.TopRight);
                unit.text = units[i];

                AddRowImage(rect, "StatTrack" + i, 34f, 92f + rowY, 488f, 4f, UISprites.Square(), TrackColor);
                Image bar = AddRowImage(rect, "StatFill" + i, 34f, 92f + rowY, 488f, 4f, UISprites.Square(), colors[i]);
                bar.type = Image.Type.Filled;
                bar.fillMethod = Image.FillMethod.Horizontal;
                bar.fillOrigin = (int)Image.OriginHorizontal.Left;
                bar.fillAmount = 0f;
                bars[i] = bar;
            }
        }

        /// <summary>Bốn thẻ kỹ năng: số thứ tự, ô phím 1..4, nhãn loại, icon, tên và mô tả.</summary>
        private static void AddSkillPanel(RectTransform root, TMP_FontAsset barlow600, TMP_FontAsset barlow700, TMP_FontAsset barlow900,
            out TextMeshProUGUI[] names, out TextMeshProUGUI[] descriptions, out Image[] icons, out Button optionButton1, out Button optionButton2)
        {
            GameObject container = new GameObject("SkillPanel", typeof(RectTransform));
            RectTransform rect = container.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(468f, DesignHeight - 994f);
            rect.sizeDelta = new Vector2(802f, 262f);

            AddRowImage(rect, "SkillAccent", 0f, 16f, 3f, 20f, UISprites.Rounded(1), Purple);
            TextMeshProUGUI title = AddRowText(rect, "SkillTitle", 16f, 6f, 260f, 26f, 20f, Ink, barlow900, TextAlignmentOptions.TopLeft);
            title.text = "KỸ NĂNG XE TĂNG";
            TextMeshProUGUI subtitle = AddRowText(rect, "SkillSubtitle", 16f, 34f, 280f, 20f, 14f, InkFaint, barlow600, TextAlignmentOptions.TopLeft);
            subtitle.text = "TANK SKILLS / 04 SLOTS";

            string[] types = { "KỸ NĂNG THƯỜNG", "KỸ NĂNG CHUYÊN BIỆT", "TÙY CHỌN 1", "TÙY CHỌN 2" };
            Color32[] accents = { Orange, Teal, Purple, Purple };
            names = new TextMeshProUGUI[4];
            descriptions = new TextMeshProUGUI[4];
            icons = new Image[4];
            Button[] buttons = new Button[4];

            for (int i = 0; i < 4; i++)
            {
                GameObject card = new GameObject("SkillCard" + i, typeof(RectTransform));
                RectTransform cardRect = card.GetComponent<RectTransform>();
                cardRect.SetParent(rect, false);
                cardRect.anchorMin = new Vector2(0f, 1f);
                cardRect.anchorMax = new Vector2(0f, 1f);
                cardRect.pivot = new Vector2(0f, 1f);
                cardRect.anchoredPosition = new Vector2(203f * i, -52f);
                cardRect.sizeDelta = new Vector2(191f, 206f);

                AddRowShape(cardRect, "CardBg", 0f, 0f, 191f, 206f, 16, PanelFill, PanelFill, PanelEdge, 1f);
                AddRowImage(cardRect, "CardAccent", 0f, 0f, 191f, 3f, UISprites.Square(), accents[i]);

                TextMeshProUGUI number = AddRowText(cardRect, "CardNumber", 15.5f, 9f, 34f, 20f, 14f, InkGhost, barlow700, TextAlignmentOptions.TopLeft);
                number.text = "0" + (i + 1);

                AddRowShape(cardRect, "CardKeyBg", 149f, 10f, 28f, 28f, 8, new Color32(0x12, 0x18, 0x21, 0xFF), new Color32(0x12, 0x18, 0x21, 0xFF), Purple, 1f);
                TextMeshProUGUI key = AddRowText(cardRect, "CardKey", 149f, 10f, 28f, 28f, 15f, PurpleSoft, barlow700, TextAlignmentOptions.Center);
                key.text = (i + 1).ToString();

                TextMeshProUGUI type = AddRowText(cardRect, "CardType", 14.5f, 38f, 130f, 18f, 13f, InkFaint, barlow600, TextAlignmentOptions.TopLeft);
                type.text = types[i];

                icons[i] = AddRowImage(cardRect, "CardIcon", 47.5f, 64f, 96f, 96f, null, Color.white);
                icons[i].preserveAspect = true;

                names[i] = AddRowText(cardRect, "CardName", 3f, 162f, 185f, 26f, 20f, Ink, barlow900, TextAlignmentOptions.Top);
                names[i].text = "SKILL";
                descriptions[i] = AddRowText(cardRect, "CardDesc", 3f, 184f, 185f, 22f, 14f, InkDim, barlow600, TextAlignmentOptions.Top);
                descriptions[i].text = string.Empty;

                GameObject buttonObject = new GameObject("CardButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
                buttonRect.SetParent(cardRect, false);
                buttonRect.anchorMin = Vector2.zero;
                buttonRect.anchorMax = Vector2.one;
                buttonRect.offsetMin = Vector2.zero;
                buttonRect.offsetMax = Vector2.zero;
                Image buttonImage = buttonObject.GetComponent<Image>();
                buttonImage.color = new Color(1f, 1f, 1f, 0f);
                buttonImage.raycastTarget = true;
                Button button = buttonObject.GetComponent<Button>();
                button.targetGraphic = buttonImage;
                button.transition = Selectable.Transition.None;
                buttons[i] = button;
            }

            optionButton1 = buttons[2];
            optionButton2 = buttons[3];
        }

        /// <summary>Panel phụ kiện: header riêng, tên dòng dưới, chỉ số, mô tả và nút ĐỔI PHỤ KIỆN.</summary>
        private static void AddAccessoryPanel(RectTransform root, TMP_FontAsset barlow600, TMP_FontAsset barlow700, TMP_FontAsset barlow900,
            out GarageAccessoryPanel panel, out Button changeButton, out Button salvageButton)
        {
            const float panelWidth = 556f;
            const float panelHeight = 206f;
            GameObject container = new GameObject("AccessoryInfo", typeof(RectTransform));
            RectTransform rect = container.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(1292f, DesignHeight - (670f + panelHeight));
            rect.sizeDelta = new Vector2(panelWidth, panelHeight);

            AddRowShape(rect, "AccessoryBg", 0f, 0f, panelWidth, panelHeight, 18, PanelFill, PanelFill, PanelEdge, 1f);
            TextMeshProUGUI header = AddRowText(rect, "AccessoryHeader", 19f, 5f, 260f, 24f, 18f, Ink, barlow900, TextAlignmentOptions.TopLeft);
            header.text = "PHỤ KIỆN TRANG BỊ";
            TextMeshProUGUI caption = AddRowText(rect, "AccessoryCaption", 430f, 7f, 107f, 22f, 14f, InkGhost, barlow600, TextAlignmentOptions.TopRight);
            caption.text = "1 Ô DUY NHẤT";

            AddRowShape(rect, "AccessoryIconBg", 23f, 41f, 80f, 80f, 8, new Color32(0x0A, 0x0F, 0x16, 0xFF), new Color32(0x0A, 0x0F, 0x16, 0xFF), PanelEdge, 1f);
            Image icon = AddRowImage(rect, "AccessoryIcon", 23f, 41f, 80f, 80f, null, Color.white);
            icon.preserveAspect = true;
            icon.enabled = false;

            TextMeshProUGUI name = AddRowText(rect, "AccessoryName", 121f, 26f, 400f, 26f, 20f, Ink, barlow700, TextAlignmentOptions.TopLeft);
            name.enableAutoSizing = true;
            name.fontSizeMin = 13f;
            name.fontSizeMax = 20f;
            name.overflowMode = TextOverflowModes.Ellipsis;

            TextMeshProUGUI stats = AddRowText(rect, "AccessoryStats", 120.5f, 54f, 410f, 44f, 17f, new Color32(0x4A, 0xDE, 0x80, 0xFF), barlow600, TextAlignmentOptions.TopLeft);
            stats.textWrappingMode = TextWrappingModes.Normal;
            stats.overflowMode = TextOverflowModes.Ellipsis;

            TextMeshProUGUI description = AddRowText(rect, "AccessoryDescription", 120.5f, 102f, 410f, 42f, 15.5f, InkDim, barlow600, TextAlignmentOptions.TopLeft);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.overflowMode = TextOverflowModes.Ellipsis;

            GameObject buttonObject = new GameObject("ChangeAccessoryButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(rect, false);
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(18f, -148f);
            buttonRect.sizeDelta = new Vector2(250f, 54f);
            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.sprite = UISprites.PanelShape(250, 54, 12, 12, new Color32(0x12, 0x18, 0x21, 0xFF), new Color32(0x12, 0x18, 0x21, 0xFF), new Color32(0x3A, 0x4A, 0x5C, 0xFF), 1f, 3);
            buttonImage.type = Image.Type.Simple;
            changeButton = buttonObject.GetComponent<Button>();
            changeButton.targetGraphic = buttonImage;
            changeButton.transition = Selectable.Transition.ColorTint;

            TextMeshProUGUI changeLabel = AddRowText(buttonRect, "ChangeAccessoryLabel", 0f, 0f, 250f, 54f, 20f, TextSoft, barlow700, TextAlignmentOptions.Center);
            changeLabel.text = "ĐỔI PHỤ KIỆN";

            // PHÂN RÃ: mở màn phân rã phụ kiện dạng nạp chồng (xem AccessorySalvageBuilder).
            GameObject salvageObject = new GameObject("SalvageAccessoryButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform salvageRect = salvageObject.GetComponent<RectTransform>();
            salvageRect.SetParent(rect, false);
            salvageRect.anchorMin = new Vector2(0f, 1f);
            salvageRect.anchorMax = new Vector2(0f, 1f);
            salvageRect.pivot = new Vector2(0f, 1f);
            salvageRect.anchoredPosition = new Vector2(288f, -148f);
            salvageRect.sizeDelta = new Vector2(250f, 54f);
            Image salvageImage = salvageObject.GetComponent<Image>();
            // Nút phá huỷ: nền đỏ đậm + viền đỏ sáng để tách hẳn khỏi các nút trung tính.
            salvageImage.sprite = UISprites.PanelShape(250, 54, 12, 12, new Color32(0xC0, 0x39, 0x2B, 0xFF), new Color32(0x8E, 0x1F, 0x17, 0xFF), new Color32(0xFF, 0x7A, 0x6B, 0xFF), 1f, 3);
            salvageImage.type = Image.Type.Simple;
            salvageButton = salvageObject.GetComponent<Button>();
            salvageButton.targetGraphic = salvageImage;
            salvageButton.transition = Selectable.Transition.ColorTint;

            TextMeshProUGUI salvageLabel = AddRowText(salvageRect, "SalvageAccessoryLabel", 0f, 0f, 250f, 54f, 20f, new Color32(0xFF, 0xF1, 0xEE, 0xFF), barlow700, TextAlignmentOptions.Center);
            salvageLabel.text = "PHÂN RÃ";

            panel = container.AddComponent<GarageAccessoryPanel>();
            SerializedObject serialized = new SerializedObject(panel);
            serialized.FindProperty("m_Catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AccessoryCatalog>(CatalogPath);
            serialized.FindProperty("m_Icon").objectReferenceValue = icon;
            serialized.FindProperty("m_AccessoryName").objectReferenceValue = name;
            serialized.FindProperty("m_Category").objectReferenceValue = null;
            serialized.FindProperty("m_Stats").objectReferenceValue = stats;
            serialized.FindProperty("m_Description").objectReferenceValue = description;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Nút trong suốt phủ lên ảnh đã vẽ sẵn (CTA, QUAY LẠI).</summary>
        private static Button AddTransparentButton(RectTransform root, string name, float x, float y, float width, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, DesignHeight - (y + height));
            rect.sizeDelta = new Vector2(width, height);

            Image image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }

        /// <summary>Thêm một khung panel vẽ procedural (bo góc + vát góc phải) canh góc trên-trái.</summary>
        private static Image AddRowShape(RectTransform parent, string name, float x, float y, float width, float height,
            int cut, Color top, Color bottom, Color border, float borderWidth)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);

            Image image = go.GetComponent<Image>();
            image.sprite = UISprites.PanelShape(Mathf.RoundToInt(width), Mathf.RoundToInt(height), cut, 0, top, bottom, border, borderWidth, 3);
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            return image;
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

        /// <summary>Thêm một lớp ảnh con canh theo góc trên-trái của khung cha.</summary>
        private static Image AddRowImage(RectTransform parent, string name, float x, float y, float width, float height, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.localScale = Vector3.one;

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null)
            {
                image.type = Image.Type.Simple;
            }

            return image;
        }

        /// <summary>Thêm một vùng chữ con canh theo góc trên-trái của khung cha.</summary>
        private static TextMeshProUGUI AddRowText(RectTransform parent, string name, float x, float y, float width, float height, float fontSize,
            Color color, TMP_FontAsset font, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            // TMP cắt sạch thành 0 ký tự khi hộp chữ thấp hơn ~1.25 lần cỡ chữ, nên nới tối thiểu 1.45 lần.
            float minHeight = fontSize * 1.6f;
            if (height < minHeight)
            {
                height = minHeight;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = string.Empty;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
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
            string path = PickerIconDir + "/" + SkillCatalog.GetIconKey(tankIndex, tabIndex, optionIndex) + ".png";
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
