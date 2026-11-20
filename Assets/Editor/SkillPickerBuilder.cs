using System.Collections.Generic;
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
    /// Dựng scene SkillPicker (chọn kỹ năng tùy chọn) từ bộ sprite đã nướng từ
    /// file thiết kế 02b_GARAGE_SKILL_PICKER.svg. Ba thẻ lựa chọn, hai tab và toàn bộ
    /// chữ động được SkillPickerController cập nhật theo xe đang chọn trong Garage.
    /// </summary>
    public static class SkillPickerBuilder
    {
        private const string SpriteDir = "Assets/Art/UI/Picker";
        private const string IconDir = SpriteDir + "/SkillIcons";
        private const string FontDir = "Assets/Art/Fonts";
        private const string GeneratedFontDir = "Assets/Art/Fonts/Generated";
        private const string ScenePath = "Assets/Scenes/SkillPicker.unity";
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

        [MenuItem("Tools/Iron Wasteland/Rebuild Skill Picker")]
        public static void Rebuild()
        {
            ConfigureSprites();
            TMP_FontAsset barlow600 = EnsureFontAsset(FontDir + "/BarlowCondensed-600.ttf", GeneratedFontDir + "/GarageBarlow600 SDF.asset");
            TMP_FontAsset barlow700 = EnsureFontAsset(FontDir + "/BarlowCondensed-700.ttf", GeneratedFontDir + "/BarlowCondensed700MainMenu SDF.asset");
            TMP_FontAsset barlow800 = EnsureFontAsset(FontDir + "/BarlowCondensed-800.ttf", GeneratedFontDir + "/GarageBarlow800 SDF.asset");
            TMP_FontAsset barlow900 = EnsureFontAsset(FontDir + "/BarlowCondensed-900.ttf", GeneratedFontDir + "/GarageBarlow900 SDF.asset");
            TMP_FontAsset mono = EnsureFontAsset(FontDir + "/ShareTechMono-400.ttf", GeneratedFontDir + "/GarageShareTechMono SDF.asset");
            if (barlow600 == null || barlow700 == null || barlow800 == null || barlow900 == null || mono == null)
            {
                Debug.LogError("[SkillPickerBuilder] Thiếu font, dừng dựng scene.");
                return;
            }

            BuildScene(barlow600, barlow700, barlow800, barlow900, mono);
        }

        /// <summary>Đặt lại thiết lập import cho toàn bộ sprite của màn chọn kỹ năng.</summary>
        private static void ConfigureSprites()
        {
            ConfigureSpriteFolder(SpriteDir);
            if (Directory.Exists(IconDir))
            {
                ConfigureSpriteFolder(IconDir);
            }
        }

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
                Debug.LogError("[SkillPickerBuilder] Không tìm thấy font: " + ttfPath);
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

        private static void BuildScene(TMP_FontAsset barlow600, TMP_FontAsset barlow700, TMP_FontAsset barlow800, TMP_FontAsset barlow900, TMP_FontAsset mono)
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

            GameObject canvasObject = new GameObject("SkillPickerCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            AddImage(root, new LayerRect("Panel", 396f, 190f, 1128f, 700f), LoadSprite("panel"));
            Image tabsBackground = AddImage(root, new LayerRect("Tabs", 432f, 306f, 482f, 48f), LoadSprite("tabs_left"));
            AddImage(root, new LayerRect("CloseButton", 1444f, 214f, 44f, 44f), LoadSprite("close_btn"));
            AddImage(root, new LayerRect("FooterButton", 1318f, 812f, 170f, 52f), LoadSprite("footer_btn"));

            // Ba thẻ: nền theo đúng vị trí, viền cam và nhãn LỰA CHỌN bật/tắt theo lựa chọn.
            Image[] cardBases = new Image[SkillCatalog.OptionCount];
            Image[] cardHovers = new Image[SkillCatalog.OptionCount];
            Image[] cardLabels = new Image[SkillCatalog.OptionCount];
            Image[] cardSelected = new Image[SkillCatalog.OptionCount];
            for (int i = 0; i < SkillCatalog.OptionCount; i++)
            {
                float x = 432f + 360f * i;
                cardBases[i] = AddImage(root, new LayerRect("CardBase" + i, x, 380f, 336f, 400f), LoadSprite("card_base_" + (i + 1)));
                cardLabels[i] = AddImage(root, new LayerRect("ChoiceLabel" + i, x + 228f, 392f, 92f, 32f), LoadSprite("label_choice"));
            }

            float[] cardX = { 432f, 792f, 1152f };
            float[] iconX = { 540f, 900f, 1260f };
            float[] nameX = { 600f, 960f, 1320f };
            float[] numberX = { 454f, 814f, 1174f };
            float[] energyX = { 582f, 942f, 1302f };
            float[] cooldownX = { 734f, 1094f, 1454f };
            float[] descX = { 454f, 814f, 1174f };
            float[] descY = { 684.41f, 709.41f, 734.41f };

            SkillPickerController.CardView[] views = new SkillPickerController.CardView[SkillCatalog.OptionCount];
            for (int i = 0; i < SkillCatalog.OptionCount; i++)
            {
                SkillCatalog.Option option = SkillCatalog.Get(2, 0, i);
                SkillPickerController.CardView view = new SkillPickerController.CardView();
                view.Icon = AddImage(root, new LayerRect("CardIcon" + i, iconX[i], 434f, 120f, 120f), LoadSkillIcon(SkillCatalog.GetIconKey(2, 0, i)));
                view.Icon.rectTransform.sizeDelta = new Vector2(120f, 120f);
                view.Icon.preserveAspect = true;

                TextMeshProUGUI number = AddText(root, "CardNumber" + i, "0" + (i + 1), numberX[i], 407.55f, 15.2f, new Color32(0x5A, 0x66, 0x75, 0xFF), mono, TextAlignmentOptions.TopLeft, 1.6f);
                view.Name = AddText(root, "CardName" + i, option.Name.ToUpperInvariant(), nameX[i], 585.91f, 23.56f, new Color32(0xE6, 0xED, 0xF5, 0xFF), barlow900, TextAlignmentOptions.Top, 0.8f);
                view.Role = AddText(root, "CardRole" + i, option.Role, nameX[i], 610.13f, 15.96f, new Color32(0x8A, 0x99, 0xAB, 0xFF), barlow600, TextAlignmentOptions.Top, 1.6f);
                view.Energy = AddText(root, "CardEnergy" + i, option.Energy.ToString(), energyX[i], 648.65f, 18.46f, new Color32(0xC4, 0xD0, 0xDC, 0xFF), barlow700, TextAlignmentOptions.TopRight, 0.6f);
                view.Cooldown = AddText(root, "CardCooldown" + i, option.Cooldown.ToString("0") + "s", cooldownX[i], 648.65f, 18.46f, new Color32(0xC4, 0xD0, 0xDC, 0xFF), barlow700, TextAlignmentOptions.TopRight, 0.6f);

                string[] lines = WrapDescription(option.Description, 3);
                view.Description = new TextMeshProUGUI[3];
                for (int line = 0; line < 3; line++)
                {
                    view.Description[line] = AddText(root, "CardDesc" + i + "_" + line, lines[line], descX[i], descY[line], 16.33f, new Color32(0x8A, 0x99, 0xAB, 0xFF), barlow600, TextAlignmentOptions.TopLeft, 0.2f);
                }

                cardHovers[i] = AddImage(root, new LayerRect("CardHover" + i, cardX[i], 380f, 336f, 400f), LoadSprite("hover_outline"));
                cardHovers[i].enabled = false;
                cardSelected[i] = AddImage(root, new LayerRect("CardSelected" + i, cardX[i], 380f, 336f, 400f), LoadSprite("card_selected"));
                cardSelected[i].enabled = i == 0;

                view.Base = cardBases[i];
                view.Selected = cardSelected[i];
                view.Hover = cardHovers[i];
                view.ChoiceLabel = cardLabels[i];
                views[i] = view;
            }

            TextMeshProUGUI breadcrumb = AddText(root, "TextBreadcrumb", "BLACK SHADOW / KỸ NĂNG TÙY CHỌN / NHÀ XE", 447f, 225.55f, 15.2f, new Color32(0x5A, 0x66, 0x75, 0xFF), barlow700, TextAlignmentOptions.TopLeft, 1.6f);
            AddText(root, "TextTitle", "CHỌN KỸ NĂNG TÙY CHỌN", 447f, 264.45f, 34.8f, new Color32(0xE6, 0xED, 0xF5, 0xFF), barlow900, TextAlignmentOptions.TopLeft, 1.2f);
            TextMeshProUGUI tab1 = AddText(root, "TextTab1", "TÙY CHỌN 1   ·   L", 550f, 336.13f, 17.04f, new Color32(0x0B, 0x0F, 0x14, 0xFF), barlow800, TextAlignmentOptions.Top, 1.8f);
            TextMeshProUGUI tab2 = AddText(root, "TextTab2", "TÙY CHỌN 2   ·   I", 796f, 336.13f, 17.04f, new Color32(0x6C, 0x7A, 0x8A, 0xFF), barlow800, TextAlignmentOptions.Top, 1.8f);
            AddText(root, "TextFooter", "CHỌN 1 TRONG 3  ·  BẤM ESC ĐỂ ĐÓNG  ·  ĐỔI ĐƯỢC BẤT CỨ LÚC NÀO TRONG NHÀ XE", 432f, 834.13f, 15.96f, new Color32(0x5A, 0x66, 0x75, 0xFF), barlow600, TextAlignmentOptions.TopLeft, 1.2f);
            AddText(root, "TextClose", "ĐÓNG", 1403f, 845.16f, 19.88f, new Color32(0xC4, 0xD0, 0xDC, 0xFF), barlow800, TextAlignmentOptions.Top, 2.4f);

            Sprite[] icons = new Sprite[SkillCatalog.TankCount * SkillCatalog.TabCount * SkillCatalog.OptionCount];
            for (int tank = 0; tank < SkillCatalog.TankCount; tank++)
            {
                for (int tab = 0; tab < SkillCatalog.TabCount; tab++)
                {
                    for (int option = 0; option < SkillCatalog.OptionCount; option++)
                    {
                        icons[SkillCatalog.GetIconIndex(tank, tab, option)] = LoadSkillIcon(SkillCatalog.GetIconKey(tank, tab, option));
                    }
                }
            }

            SkillPickerController controller = canvasObject.AddComponent<SkillPickerController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("m_Breadcrumb").objectReferenceValue = breadcrumb;
            serialized.FindProperty("m_TabsBackground").objectReferenceValue = tabsBackground;
            serialized.FindProperty("m_TabsLeftSprite").objectReferenceValue = LoadSprite("tabs_left");
            serialized.FindProperty("m_TabsRightSprite").objectReferenceValue = LoadSprite("tabs_right");

            SerializedProperty iconProperty = serialized.FindProperty("m_OptionIcons");
            iconProperty.arraySize = icons.Length;
            for (int i = 0; i < icons.Length; i++)
            {
                iconProperty.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
            }

            SerializedProperty tabLabelProperty = serialized.FindProperty("m_TabLabels");
            tabLabelProperty.arraySize = 2;
            tabLabelProperty.GetArrayElementAtIndex(0).objectReferenceValue = tab1;
            tabLabelProperty.GetArrayElementAtIndex(1).objectReferenceValue = tab2;

            SerializedProperty cardsProperty = serialized.FindProperty("m_Cards");
            cardsProperty.arraySize = views.Length;
            for (int i = 0; i < views.Length; i++)
            {
                SerializedProperty card = cardsProperty.GetArrayElementAtIndex(i);
                card.FindPropertyRelative("Base").objectReferenceValue = views[i].Base;
                card.FindPropertyRelative("Selected").objectReferenceValue = views[i].Selected;
                card.FindPropertyRelative("Hover").objectReferenceValue = views[i].Hover;
                card.FindPropertyRelative("ChoiceLabel").objectReferenceValue = views[i].ChoiceLabel;
                card.FindPropertyRelative("Icon").objectReferenceValue = views[i].Icon;
                card.FindPropertyRelative("Name").objectReferenceValue = views[i].Name;
                card.FindPropertyRelative("Role").objectReferenceValue = views[i].Role;
                card.FindPropertyRelative("Energy").objectReferenceValue = views[i].Energy;
                card.FindPropertyRelative("Cooldown").objectReferenceValue = views[i].Cooldown;

                SerializedProperty description = card.FindPropertyRelative("Description");
                description.arraySize = 3;
                for (int line = 0; line < 3; line++)
                {
                    description.GetArrayElementAtIndex(line).objectReferenceValue = views[i].Description[line];
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[SkillPickerBuilder] Đã dựng xong scene " + ScenePath);
        }

        /// <summary>Chia mô tả thành tối đa 3 dòng, mỗi dòng khoảng 34 ký tự (giống controller).</summary>
        private static string[] WrapDescription(string text, int lineCount)
        {
            string[] lines = new string[lineCount];
            if (string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < lineCount; i++)
                {
                    lines[i] = string.Empty;
                }

                return lines;
            }

            string[] words = text.Split(' ');
            int line = 0;
            string current = string.Empty;
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (candidate.Length > 34 && current.Length > 0 && line < lineCount - 1)
                {
                    lines[line] = current;
                    line++;
                    current = word;
                }
                else
                {
                    current = candidate;
                }
            }

            lines[line] = current;
            for (int i = line + 1; i < lineCount; i++)
            {
                lines[i] = string.Empty;
            }

            return lines;
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
            string path = IconDir + "/" + iconName + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("[SkillPickerBuilder] Không tìm thấy icon kỹ năng: " + path);
            }

            return sprite;
        }

        private static Sprite LoadSprite(string spriteName)
        {
            string path = SpriteDir + "/" + spriteName + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("[SkillPickerBuilder] Không tìm thấy sprite: " + path);
            }

            return sprite;
        }
    }
}
