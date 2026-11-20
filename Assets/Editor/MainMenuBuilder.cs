using System.IO;
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
    /// Dựng scene Main Menu từ bộ sprite đã xuất ra từ file thiết kế 01_MAINMENU.svg.
    /// Mỗi lớp hình ảnh đặt đúng toạ độ pixel trong hệ 1920x1080, chữ số động dùng TextMeshPro.
    /// </summary>
    public static class MainMenuBuilder
    {
        private const string SpriteDir = "Assets/Art/UI/MainMenu";
        private const string FontDir = "Assets/Art/Fonts";
        private const string GeneratedFontDir = "Assets/Art/Fonts/Generated";
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
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

        /// <summary>Dữ liệu hiển thị của một xe trong menu, khớp thứ tự với Garage.</summary>
        private struct TankPreviewSpec
        {
            public string SpriteName;
            public string DisplayName;
            public string Role;
            public int Level;
            public int[] Stats;
        }

        private static readonly TankPreviewSpec[] TankPreviews =
        {
            new TankPreviewSpec
            {
                SpriteName = "wall_titan",
                DisplayName = "WALL TITAN",
                Role = "TANKER HỖ TRỢ",
                Level = 1,
                Stats = new[] { 14, 900, 100, 22, 85, 0, 0, 0 }
            },
            new TankPreviewSpec
            {
                SpriteName = "red_nova",
                DisplayName = "RED NOVA",
                Role = "XẠ THỦ HỎA LỰC",
                Level = 10,
                Stats = new[] { 26, 520, 130, 8, 120, 0, 0, 0 }
            },
            new TankPreviewSpec
            {
                SpriteName = "black_shadow",
                DisplayName = "BLACK SHADOW",
                Role = "SÁT THỦ CƠ ĐỘNG",
                Level = 5,
                Stats = new[] { 20, 650, 120, 12, 110, 0, 0, 0 }
            },
            new TankPreviewSpec
            {
                SpriteName = "leviathan",
                DisplayName = "LEVIATHAN",
                Role = "TANKER ĐỘI HÌNH",
                Level = 1,
                Stats = new[] { 16, 820, 110, 18, 95, 0, 0, 0 }
            },
            new TankPreviewSpec
            {
                SpriteName = "water_b",
                DisplayName = "WATER B",
                Role = "HỖ TRỢ HỒI MÁU",
                Level = 1,
                Stats = new[] { 15, 600, 140, 10, 105, 0, 0, 0 }
            },
            new TankPreviewSpec
            {
                SpriteName = "green_fox",
                DisplayName = "GREEN FOX",
                Role = "SÁT THỦ ĐỘC",
                Level = 1,
                Stats = new[] { 22, 560, 125, 9, 118, 0, 0, 0 }
            }
        };

        [MenuItem("Tools/Iron Wasteland/Rebuild Main Menu")]
        public static void Rebuild()
        {
            ConfigureSprites();
            TMP_FontAsset anton = EnsureFontAsset(FontDir + "/Anton-400.ttf", GeneratedFontDir + "/AntonMainMenu SDF.asset");
            TMP_FontAsset barlow = EnsureFontAsset(FontDir + "/BarlowCondensed-700.ttf", GeneratedFontDir + "/BarlowCondensed700MainMenu SDF.asset");
            TMP_FontAsset mono = EnsureFontAsset(FontDir + "/ShareTechMono-400.ttf", GeneratedFontDir + "/GarageShareTechMono SDF.asset");
            if (anton == null || barlow == null || mono == null)
            {
                Debug.LogError("[MainMenuBuilder] Thiếu font, dừng dựng scene.");
                return;
            }

            BuildScene(anton, barlow, mono);
        }

        /// <summary>Đặt lại thiết lập import cho toàn bộ sprite của màn hình chính.</summary>
        private static void ConfigureSprites()
        {
            string[] files = Directory.GetFiles(SpriteDir, "*.png");
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
                Debug.LogError("[MainMenuBuilder] Không tìm thấy font: " + ttfPath);
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

        /// <summary>Dựng toàn bộ scene: canvas, các lớp ảnh và các chữ số động.</summary>
        private static void BuildScene(TMP_FontAsset anton, TMP_FontAsset barlow, TMP_FontAsset mono)
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

            GameObject canvasObject = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
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
            AddImage(root, new LayerRect("Topbar", 0f, 0f, 1920f, 84f), LoadSprite("topbar"));
            AddImage(root, new LayerRect("WordmarkBlock", 95f, 147f, 720f, 326f), LoadSprite("navleft"));
            AddImage(root, new LayerRect("HeroPanel", 883f, 127f, 966f, 742f), LoadSprite("hero"));
            Image tankPortrait = CreateTankPortrait(root);
            AddImage(root, new LayerRect("StatStrip", 883f, 883f, 966f, 98f), LoadSprite("stats"));
            AddImage(root, new LayerRect("Footer", 27f, 27f, 1871f, 1041f), LoadSprite("footer"));

            Image[] navItems = new Image[4];
            Sprite[] navNormal = new Sprite[4];
            Sprite[] navSelected = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                float y = 484f + i * 120f;
                navNormal[i] = LoadSprite("nav" + i + "_normal");
                navSelected[i] = LoadSprite("nav" + i + "_sel");
                navItems[i] = AddImage(root, new LayerRect("Nav" + i, 24f, y, 750f, 112f), navNormal[i]);
            }

            TextMeshProUGUI tankLevel = AddText(root, "TextLevel", "05", 1706f, 192.01f, 27.28f, new Color32(0x2F, 0xE8, 0xE0, 0xFF), anton, false, 0f);
            TextMeshProUGUI tankName = AddText(root, "TextUnitName", "BLACK SHADOW", 1085f, 762f, 51.9f, Color.white, anton, false, 0.3f);
            tankName.rectTransform.localScale = new Vector3(1.118f, 1f, 1f);
            TextMeshProUGUI tankRole = AddText(root, "TextTankRole", "SÁT THỦ CƠ ĐỘNG", 1088f, 798f, 17.04f, new Color32(0xC9, 0xA9, 0xFF, 0xFF), barlow, false, 0f);
            TextMeshProUGUI tankCaption = AddText(root, "TextTankCaption", "CURRENT UNIT // BLACK SHADOW", 918f, 177.5f, 13.63f, new Color32(0x3D, 0x4A, 0x59, 0xFF), mono, false, 0f);
            AddText(root, "TextProgress", "22%", 1754f, 770.71f, 16.72f, new Color32(0xFF, 0xB3, 0x47, 0xFF), barlow, true, 1f);
            TextMeshProUGUI[] tankStats = new TextMeshProUGUI[4];
            tankStats[0] = AddText(root, "TextAtk", "20", 900f, 954.71f, 39.44f, new Color32(0xE6, 0xED, 0xF5, 0xFF), anton, false, 0f);
            tankStats[1] = AddText(root, "TextHp", "650", 1144f, 954.71f, 39.44f, new Color32(0xE6, 0xED, 0xF5, 0xFF), anton, false, 0f);
            tankStats[2] = AddText(root, "TextEn", "120", 1388f, 954.71f, 39.44f, new Color32(0xE6, 0xED, 0xF5, 0xFF), anton, false, 0f);
            tankStats[3] = AddText(root, "TextSpd", "110", 1632f, 954.71f, 39.44f, new Color32(0xE6, 0xED, 0xF5, 0xFF), anton, false, 0f);

            MainMenuController controller = canvasObject.AddComponent<MainMenuController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("m_Root").objectReferenceValue = canvasObject.GetComponent<CanvasGroup>();
            serialized.FindProperty("m_Background").objectReferenceValue = root.Find("Background") as RectTransform;
            serialized.FindProperty("m_TankPortrait").objectReferenceValue = tankPortrait;
            serialized.FindProperty("m_TankName").objectReferenceValue = tankName;
            serialized.FindProperty("m_TankRole").objectReferenceValue = tankRole;
            serialized.FindProperty("m_TankCaption").objectReferenceValue = tankCaption;
            serialized.FindProperty("m_TankLevel").objectReferenceValue = tankLevel;

            SerializedProperty statValuesProperty = serialized.FindProperty("m_TankStatValues");
            statValuesProperty.arraySize = tankStats.Length;
            for (int i = 0; i < tankStats.Length; i++)
            {
                statValuesProperty.GetArrayElementAtIndex(i).objectReferenceValue = tankStats[i];
            }

            SerializedProperty tanksProperty = serialized.FindProperty("m_Tanks");
            tanksProperty.arraySize = TankPreviews.Length;
            for (int i = 0; i < TankPreviews.Length; i++)
            {
                TankPreviewSpec spec = TankPreviews[i];
                SerializedProperty entry = tanksProperty.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("DisplayName").stringValue = spec.DisplayName;
                entry.FindPropertyRelative("Role").stringValue = spec.Role;
                entry.FindPropertyRelative("Level").intValue = spec.Level;
                entry.FindPropertyRelative("Portrait").objectReferenceValue = LoadSprite("tank_hero_" + spec.SpriteName);

                SerializedProperty statsProperty = entry.FindPropertyRelative("Stats");
                statsProperty.arraySize = spec.Stats.Length;
                for (int s = 0; s < spec.Stats.Length; s++)
                {
                    statsProperty.GetArrayElementAtIndex(s).intValue = spec.Stats[s];
                }
            }

            SerializedProperty itemsProperty = serialized.FindProperty("m_NavItems");
            itemsProperty.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                itemsProperty.GetArrayElementAtIndex(i).objectReferenceValue = navItems[i];
            }

            SerializedProperty normalProperty = serialized.FindProperty("m_NavSpritesNormal");
            normalProperty.arraySize = 4;
            SerializedProperty selectedProperty = serialized.FindProperty("m_NavSpritesSelected");
            selectedProperty.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                normalProperty.GetArrayElementAtIndex(i).objectReferenceValue = navNormal[i];
                selectedProperty.GetArrayElementAtIndex(i).objectReferenceValue = navSelected[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MainMenuBuilder] Đã dựng xong scene " + ScenePath);
        }

        private const string TankPortraitPath = "Assets/Art/UI/MainMenu/tank_hero.png";

        /// <summary>Đặt ảnh con xe thật (ghép từ Hull + Weapon của prefab) vào khu trưng bày bên phải.</summary>
        private static Image CreateTankPortrait(RectTransform canvasRoot)
        {
            Sprite portrait = AssetDatabase.LoadAssetAtPath<Sprite>(TankPortraitPath);
            if (portrait == null)
            {
                Debug.LogWarning("[MainMenuBuilder] Không tìm thấy ảnh xe trưng bày: " + TankPortraitPath);
                return null;
            }

            Image view = AddImage(canvasRoot, new LayerRect("TankPortrait", 1125f, 221f, 518f, 518f), portrait);
            view.rectTransform.sizeDelta = new Vector2(518f, 518f);
            view.preserveAspect = true;
            return view;
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

        /// <summary>Thêm một dòng chữ động, canh theo đường baseline của thiết kế.</summary>
        private static TextMeshProUGUI AddText(RectTransform parent, string name, string value, float x, float baselineY, float fontSize, Color color, TMP_FontAsset font, bool alignRight, float letterSpacing)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            float ascentPx = font.faceInfo.ascentLine / font.faceInfo.pointSize * fontSize;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = alignRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(900f, fontSize * 1.6f);
            rect.anchoredPosition = new Vector2(x, DesignHeight - (baselineY - ascentPx));

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignRight ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.characterSpacing = letterSpacing / fontSize * 100f;
            return text;
        }

        private static Sprite LoadSprite(string spriteName)
        {
            string path = SpriteDir + "/" + spriteName + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("[MainMenuBuilder] Không tìm thấy sprite: " + path);
            }

            return sprite;
        }
    }
}
