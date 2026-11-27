using System.IO;
using IronWasteland.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace IronWasteland.EditorTools
{
    /// <summary>
    /// Dựng scene Cài đặt hệ thống từ bộ sprite đã xuất ra từ file thiết kế setting.svg.
    /// Các lớp khung, panel và chữ tĩnh nằm trong ảnh; bốn giá trị âm lượng, bảng phím
    /// và vòng âm lượng tổng được dựng bằng TextMeshPro / Image để SettingController cập nhật.
    /// </summary>
    public static class SettingBuilder
    {
        private const string SpriteDir = "Assets/Art/UI/Setting";
        private const string FontDir = "Assets/Art/Fonts";
        private const string GeneratedFontDir = "Assets/Art/Fonts/Generated";
        private const string ScenePath = "Assets/Scenes/Setting.unity";
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;

        private static readonly string[] KeyLabels =
        {
            "DI CHUYỂN",
            "NGẮM / XOAY NÒNG",
            "BẮN THƯỜNG",
            "KỸ NĂNG THƯỜNG",
            "KỸ NĂNG CHUYÊN BIỆT",
            "TÙY CHỌN 1",
            "TÙY CHỌN 2"
        };

        private static readonly float[] SliderDefaults = { 65f, 45f, 80f };

        private static readonly Color32[] SliderColors =
        {
            new Color32(0xFF, 0x7A, 0x18, 0xFF),
            new Color32(0x2F, 0xE8, 0xE0, 0xFF),
            new Color32(0x8A, 0x4B, 0xF0, 0xFF)
        };

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

        [MenuItem("Tools/Iron Wasteland/Rebuild Setting")]
        public static void Rebuild()
        {
            ConfigureSprites();
            TMP_FontAsset anton = EnsureFontAsset(FontDir + "/Anton-400.ttf", GeneratedFontDir + "/AntonMainMenu SDF.asset");
            TMP_FontAsset barlow700 = EnsureFontAsset(FontDir + "/BarlowCondensed-700.ttf", GeneratedFontDir + "/BarlowCondensed700MainMenu SDF.asset");
            TMP_FontAsset mono = EnsureFontAsset(FontDir + "/ShareTechMono-400.ttf", GeneratedFontDir + "/GarageShareTechMono SDF.asset");
            if (anton == null || barlow700 == null || mono == null)
            {
                Debug.LogError("[SettingBuilder] Thiếu font, dừng dựng scene.");
                return;
            }

            BuildScene(anton, barlow700, mono);
        }

        /// <summary>Đặt lại thiết lập import cho toàn bộ sprite của màn hình Cài đặt.</summary>
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
                Debug.LogError("[SettingBuilder] Không tìm thấy font: " + ttfPath);
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

        /// <summary>Dựng canvas, các lớp ảnh, bốn giá trị âm lượng và bảng phím.</summary>
        private static void BuildScene(TMP_FontAsset anton, TMP_FontAsset barlow700, TMP_FontAsset mono)
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

            GameObject canvasObject = new GameObject("SettingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            AddImage(root, new LayerRect("Header", 240f, 68f, 608f, 98f), LoadSprite("header"));
            Image backButton = AddImage(root, new LayerRect("BackButton", 72f, 100f, 158f, 48f), LoadSprite("back"));
            AddImage(root, new LayerRect("LeftPanel", 72f, 190f, 1188f, 700f), LoadSprite("left_panel"));
            AddImage(root, new LayerRect("RightPanel", 1292f, 190f, 556f, 700f), LoadSprite("right_panel"));
            Image resetButton = AddImage(root, new LayerRect("ResetButton", 1292f, 890f, 200f, 86f), LoadSprite("reset_btn"));
            Image applyButton = AddImage(root, new LayerRect("ApplyButton", 1504f, 890f, 344f, 86f), LoadSprite("apply_btn"));

            Image ring = AddImage(root, new LayerRect("MasterRing", 236f, 372f, 240f, 240f), null);
            TextMeshProUGUI masterValue = AddText(root, "TextMasterValue", "78", 394f, 526f, 77f, Color.white, anton, TextAlignmentOptions.TopRight, 0f);
            TextMeshProUGUI masterPercent = AddText(root, "TextMasterPercent", "%", 407f, 511f, 22f, new Color32(0xFF, 0xB3, 0x47, 0xFF), anton, TextAlignmentOptions.TopLeft, 0f);

            Image[] fills = new Image[3];
            RectTransform[] knobs = new RectTransform[3];
            TextMeshProUGUI[] values = new TextMeshProUGUI[3];
            Sprite fillSprite = LoadSprite("fill_base");
            Sprite[] knobSprites = { LoadSprite("knob_orange"), LoadSprite("knob_cyan"), LoadSprite("knob_purple") };
            for (int i = 0; i < 3; i++)
            {
                float top = 411f + 122f * i;
                Image fill = AddImage(root, new LayerRect("SliderFill" + i, 700f, top, 530f, 8f), fillSprite);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = SliderDefaults[i] / 100f;
                fill.color = SliderColors[i];
                fills[i] = fill;

                GameObject knobObject = new GameObject("SliderKnob" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform knobRect = knobObject.GetComponent<RectTransform>();
                knobRect.SetParent(root, false);
                knobRect.anchorMin = Vector2.zero;
                knobRect.anchorMax = Vector2.zero;
                knobRect.pivot = new Vector2(0.5f, 0.5f);
                knobRect.sizeDelta = knobSprites[i] != null ? new Vector2(knobSprites[i].rect.width, knobSprites[i].rect.height) : new Vector2(12f, 22f);
                knobRect.anchoredPosition = new Vector2(700f + 530f * SliderDefaults[i] / 100f, DesignHeight - (415f + 122f * i));
                Image knob = knobObject.GetComponent<Image>();
                knob.sprite = knobSprites[i];
                knob.raycastTarget = false;
                knobs[i] = knobRect;

                values[i] = AddText(root, "TextSliderValue" + i, ((int)SliderDefaults[i]).ToString(), 1212f, 390.5f + 122f * i, 20.7f, new Color32(0xE6, 0xED, 0xF5, 0xFF), anton, TextAlignmentOptions.TopRight, 0f);
            }

            Sprite smallCap = LoadSprite("keycap_small");
            Sprite wideCap = LoadSprite("keycap_wide");
            for (int i = 0; i < KeyLabels.Length; i++)
            {
                float centerY = 288f + 78f * i;
                AddText(root, "TextKeyLabel" + i, KeyLabels[i], 1316f, centerY + 8f, 19f, new Color32(0x8A, 0x99, 0xAB, 0xFF), barlow700, TextAlignmentOptions.TopLeft, 0.4f);
            }

            string[] movementKeys = { "W", "A", "S", "D" };
            for (int k = 0; k < movementKeys.Length; k++)
            {
                float left = 1664f + 42f * k;
                AddImage(root, new LayerRect("KeyCapMove" + k, left, 272f, 34f, 32f), smallCap);
                AddText(root, "TextKeyMove" + k, movementKeys[k], left + 17f, 272f + 21.4f, 15f, new Color32(0xC4, 0xD0, 0xDC, 0xFF), barlow700, TextAlignmentOptions.Top, 0f);
            }

            AddImage(root, new LayerRect("KeyCapAim", 1760f, 350f, 64f, 32f), wideCap);
            AddText(root, "TextKeyAim", "CHUỘT", 1792f, 350f + 21.4f, 15f, new Color32(0xC4, 0xD0, 0xDC, 0xFF), barlow700, TextAlignmentOptions.Top, 0.6f);

            AddImage(root, new LayerRect("KeyCapFire", 1760f, 428f, 64f, 32f), wideCap);
            AddText(root, "TextKeyFire", "CHUỘT TRÁI", 1792f, 428f + 20.4f, 12.5f, new Color32(0xC4, 0xD0, 0xDC, 0xFF), barlow700, TextAlignmentOptions.Top, 0.2f);

            string[] skillKeys = { "1", "2", "3", "4" };
            for (int k = 0; k < skillKeys.Length; k++)
            {
                float top = 506f + 78f * k;
                AddImage(root, new LayerRect("KeyCapSkill" + k, 1790f, top, 34f, 32f), smallCap);
                AddText(root, "TextKeySkill" + k, skillKeys[k], 1807f, top + 21.4f, 15f, new Color32(0xC4, 0xD0, 0xDC, 0xFF), barlow700, TextAlignmentOptions.Top, 0f);
            }

            AddText(root, "TextKeyHint", "CHUỘT PHẢI: VỀ KỸ NĂNG THƯỜNG", 1316f, 840f, 11.5f, new Color32(0x3D, 0x4A, 0x59, 0xFF), mono, TextAlignmentOptions.TopLeft, 1.2f);

            SettingController controller = canvasObject.AddComponent<SettingController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("m_Canvas").objectReferenceValue = canvas;
            serialized.FindProperty("m_MasterValue").objectReferenceValue = masterValue;
            serialized.FindProperty("m_MasterPercent").objectReferenceValue = masterPercent;
            serialized.FindProperty("m_RingArc").objectReferenceValue = ring;
            SerializedProperty sliders = serialized.FindProperty("m_Sliders");
            sliders.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                SerializedProperty element = sliders.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Fill").objectReferenceValue = fills[i];
                element.FindPropertyRelative("Knob").objectReferenceValue = knobs[i];
                element.FindPropertyRelative("Value").objectReferenceValue = values[i];
            }

            serialized.FindProperty("m_BackSceneName").stringValue = "MainMenu";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            UIHit.EnsureEventSystem(scene);
            UnityEventTools.AddPersistentListener(UIHit.Attach(backButton).onClick, controller.PressBack);
            UnityEventTools.AddPersistentListener(UIHit.Attach(resetButton).onClick, controller.PressReset);
            UnityEventTools.AddPersistentListener(UIHit.Attach(applyButton).onClick, controller.PressApply);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[SettingBuilder] Đã dựng xong " + ScenePath);
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

        private static Sprite LoadSprite(string spriteName)
        {
            string path = SpriteDir + "/" + spriteName + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("[SettingBuilder] Không tìm thấy sprite: " + path);
            }

            return sprite;
        }
    }
}