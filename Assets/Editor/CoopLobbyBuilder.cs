using IronWasteland.Net;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronWasteland.EditorTools
{
    /// <summary>
    /// Dựng giao diện lobby co-op trong CoopLobby.unity theo phong cách các màn menu đã có.
    /// Chạy lại bằng menu Tools/Iron Wasteland/Rebuild Coop Lobby.
    /// </summary>
    public static class CoopLobbyBuilder
    {
        private const string ScenePath = "Assets/Scenes/CoopLobby.unity";
        private const string SpriteDir = "Assets/Art/UI/CoopLobby";
        private const string FontDir = "Assets/Art/Fonts";
        private const string GeneratedFontDir = "Assets/Art/Fonts/Generated";
        private const string CanvasName = "CoopLobbyCanvas";
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;

        private static readonly Color PanelFill = new Color32(9, 15, 23, 238);
        private static readonly Color PanelFillAlt = new Color32(12, 19, 29, 242);
        private static readonly Color Border = new Color32(42, 53, 67, 255);
        private static readonly Color Line = new Color32(29, 38, 49, 255);
        private static readonly Color TextMain = new Color32(0xE6, 0xED, 0xF5, 0xFF);
        private static readonly Color TextMuted = new Color32(0x6F, 0x7D, 0x8C, 0xFF);
        private static readonly Color Orange = new Color32(0xFF, 0x7A, 0x18, 0xFF);
        private static readonly Color Teal = new Color32(0x2F, 0xE8, 0xE0, 0xFF);
        private static readonly Color Purple = new Color32(0xC9, 0xA9, 0xFF, 0xFF);

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

        [MenuItem("Tools/Iron Wasteland/Rebuild Coop Lobby")]
        public static void Rebuild()
        {
            ConfigureBackground();
            TMP_FontAsset anton = EnsureFontAsset(FontDir + "/Anton-400.ttf", GeneratedFontDir + "/AntonMainMenu SDF.asset");
            TMP_FontAsset barlow = EnsureFontAsset(FontDir + "/BarlowCondensed-700.ttf", GeneratedFontDir + "/BarlowCondensed700MainMenu SDF.asset");
            if (anton == null || barlow == null)
            {
                Debug.LogError("[CoopLobbyBuilder] Thiếu font, dừng dựng giao diện.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            GameObject old = GameObject.Find(CanvasName);
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            BuildCanvas(anton, barlow, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[CoopLobbyBuilder] Đã dựng lobby co-op trong " + ScenePath);
        }

        /// <summary>Đặt lại import cho ảnh nền lobby.</summary>
        private static void ConfigureBackground()
        {
            string path = SpriteDir + "/bg.png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[CoopLobbyBuilder] Không tìm thấy ảnh nền: " + path);
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
                Debug.LogError("[CoopLobbyBuilder] Không tìm thấy font: " + ttfPath);
                return null;
            }

            System.IO.Directory.CreateDirectory(GeneratedFontDir);
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
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

        private static void BuildCanvas(TMP_FontAsset anton, TMP_FontAsset barlow, Scene scene)
        {
            GameObject canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;

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

            CanvasGroup group = canvasObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/bg.png");
            if (background == null)
            {
                Debug.LogError("[CoopLobbyBuilder] Không tải được sprite nền lobby.");
                return;
            }

            Image bg = AddImage(root, new LayerRect("Background", 0f, 0f, DesignWidth, DesignHeight), background);
            bg.raycastTarget = false;

            // Header dùng chung cho cả hai màn.
            AddText(root, "TextBackChevron", "‹", 78f, 113f, 36f, Orange, anton, 40f, false);
            TextMeshProUGUI back = AddText(root, "TextBack", "QUAY LẠI", 112f, 110f, 19f, TextMain, anton, 160f, false);
            back.characterSpacing = 0.6f / 19f * 100f;

            TextMeshProUGUI title = AddText(root, "TextLobbyTitle", "PHÒNG LAN", 278f, 128f, 44f, TextMain, anton, 700f, false);
            title.characterSpacing = 0.3f / 44f * 100f;

            AddText(root, "TextLobbySub", "CHỌN PHÒNG TRONG MẠNG LAN  /  2-4 NGƯỜI", 282f, 153f, 10f, TextMuted, barlow, 720f, false);

            TextMeshProUGUI stage = AddText(root, "TextLobbyStage", "ẢI 1-2  /  HANG ĐÁ GAI", 1298f, 118f, 26f, Orange, anton, 548f, false);
            stage.alignment = TextAlignmentOptions.TopRight;
            stage.characterSpacing = 0.3f / 26f * 100f;

            AddLine(root, "HeaderRule", 72f, 176f, 1776f, 1f, Line);
            AddLine(root, "HeaderAccent", 72f, 175f, 138f, 3f, Orange);

            GameObject browser = new GameObject("BrowserView", typeof(RectTransform));
            RectTransform browserRect = browser.GetComponent<RectTransform>();
            browserRect.SetParent(root, false);
            browserRect.anchorMin = Vector2.zero;
            browserRect.anchorMax = Vector2.one;
            browserRect.offsetMin = Vector2.zero;
            browserRect.offsetMax = Vector2.zero;

            GameObject room = new GameObject("RoomView", typeof(RectTransform));
            RectTransform roomRect = room.GetComponent<RectTransform>();
            roomRect.SetParent(root, false);
            roomRect.anchorMin = Vector2.zero;
            roomRect.anchorMax = Vector2.one;
            roomRect.offsetMin = Vector2.zero;
            roomRect.offsetMax = Vector2.zero;
            room.SetActive(false);

            TextMeshProUGUI[] roomNames = new TextMeshProUGUI[6];
            TextMeshProUGUI[] roomDetails = new TextMeshProUGUI[6];
            GameObject emptyHint;
            TextMeshProUGUI addressText;
            GameObject[] roomRows;
            BuildBrowser(browserRect, anton, barlow, roomNames, roomDetails, out emptyHint, out addressText, out roomRows);

            TextMeshProUGUI[] slotTitles = new TextMeshProUGUI[4];
            TextMeshProUGUI[] slotDetails = new TextMeshProUGUI[4];
            TextMeshProUGUI roomAddress;
            TextMeshProUGUI roomHint;
            TextMeshProUGUI roomNote;
            TextMeshProUGUI readyLabel;
            TextMeshProUGUI startLabel;
            BuildRoom(roomRect, anton, barlow, slotTitles, slotDetails, out roomAddress, out roomHint, out roomNote, out readyLabel, out startLabel);

            TextMeshProUGUI status = AddText(root, "TextLobbyStatus", string.Empty, 74f, 1008f, 11.5f, TextMuted, barlow, 1200f, false);
            AddFooter(root, barlow);

            NetBootstrap bootstrap = Object.FindAnyObjectByType<NetBootstrap>();
            LanDiscoveryService discovery = Object.FindAnyObjectByType<LanDiscoveryService>();
            if (discovery == null && bootstrap != null)
            {
                discovery = bootstrap.GetComponent<LanDiscoveryService>();
                if (discovery == null) discovery = bootstrap.gameObject.AddComponent<LanDiscoveryService>();
            }

            if (bootstrap != null)
            {
                SerializedObject bootstrapSerialized = new SerializedObject(bootstrap);
                bootstrapSerialized.FindProperty("discovery").objectReferenceValue = discovery;
                bootstrapSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bootstrap);
            }

            CoopLobbyController controller = canvasObject.AddComponent<CoopLobbyController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("bootstrap").objectReferenceValue = bootstrap;
            serialized.FindProperty("flow").objectReferenceValue = Object.FindAnyObjectByType<MatchFlow>();
            serialized.FindProperty("discovery").objectReferenceValue = discovery;
            serialized.FindProperty("m_Root").objectReferenceValue = group;
            serialized.FindProperty("m_BrowserView").objectReferenceValue = browser;
            serialized.FindProperty("m_RoomView").objectReferenceValue = room;
            serialized.FindProperty("m_StageCaption").objectReferenceValue = stage;
            serialized.FindProperty("m_Status").objectReferenceValue = status;
            serialized.FindProperty("m_RoomAddress").objectReferenceValue = roomAddress;
            serialized.FindProperty("m_RoomHint").objectReferenceValue = roomHint;
            serialized.FindProperty("m_RoomNote").objectReferenceValue = roomNote;
            serialized.FindProperty("m_ReadyLabel").objectReferenceValue = readyLabel;
            serialized.FindProperty("m_StartLabel").objectReferenceValue = startLabel;
            serialized.FindProperty("m_EmptyHint").objectReferenceValue = emptyHint;
            serialized.FindProperty("m_AddressText").objectReferenceValue = addressText;

            SerializedProperty names = serialized.FindProperty("m_RoomNames");
            names.arraySize = roomNames.Length;
            SerializedProperty details = serialized.FindProperty("m_RoomDetails");
            details.arraySize = roomDetails.Length;
            SerializedProperty rowRoots = serialized.FindProperty("m_RoomRows");
            rowRoots.arraySize = roomRows.Length;
            for (int i = 0; i < roomNames.Length; i++)
            {
                names.GetArrayElementAtIndex(i).objectReferenceValue = roomNames[i];
                details.GetArrayElementAtIndex(i).objectReferenceValue = roomDetails[i];
                rowRoots.GetArrayElementAtIndex(i).objectReferenceValue = roomRows[i];
            }

            SerializedProperty titles = serialized.FindProperty("m_SlotTitles");
            titles.arraySize = slotTitles.Length;
            SerializedProperty slotDetail = serialized.FindProperty("m_SlotDetails");
            slotDetail.arraySize = slotDetails.Length;
            for (int i = 0; i < slotTitles.Length; i++)
            {
                titles.GetArrayElementAtIndex(i).objectReferenceValue = slotTitles[i];
                slotDetail.GetArrayElementAtIndex(i).objectReferenceValue = slotDetails[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            IronWasteland.EditorTools.UIHit.EnsureEventSystem(scene);

            Button backZone = IronWasteland.EditorTools.UIHit.AddZone(root, "HitBack", 72f, 72f, 168f, 50f);
            UnityEventTools.AddPersistentListener(backZone.onClick, controller.PressBack);

            BindButton(root, "ButtonCreate", controller.PressCreate);
            BindButton(root, "ButtonRefresh", controller.PressRefresh);
            BindButton(root, "JoinCodeBox", controller.PressAddressField);
            BindButton(root, "ButtonJoinCode", controller.PressJoinAddress);
            BindButton(root, "ButtonCopyAddress", controller.PressCopyAddress);
            BindButton(root, "ButtonReady", controller.PressReady);
            BindButton(root, "ButtonGarage", controller.PressGarage);
            BindButton(root, "ButtonStart", controller.PressStart);
            BindButton(root, "ButtonLeave", controller.PressLeave);

            for (int i = 0; i < 6; i++)
            {
                Button row = BindButton(root, "RoomRow" + (i + 1), null);
                UnityEventTools.AddIntPersistentListener(row.onClick, controller.PressRoom, i);
            }
        }

        /// <summary>Gắn Button lên Image sẵn có và nối một hành động không tham số.</summary>
        private static Button BindButton(RectTransform root, string name, UnityAction action)
        {
            Image image = FindImage(root, name);
            if (image == null)
            {
                Debug.LogError("[CoopLobbyBuilder] Không tìm thấy Image để gắn Button: " + name);
                return null;
            }

            Button button = IronWasteland.EditorTools.UIHit.Attach(image);
            if (action != null)
            {
                UnityEventTools.AddPersistentListener(button.onClick, action);
            }

            return button;
        }

        /// <summary>Tìm Image con theo tên, kể cả object đang ẩn.</summary>
        private static Image FindImage(RectTransform root, string name)
        {
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].name == name)
                {
                    return images[i];
                }
            }

            return null;
        }

        private static void BuildBrowser(RectTransform parent, TMP_FontAsset anton, TMP_FontAsset barlow, TextMeshProUGUI[] names, TextMeshProUGUI[] details, out GameObject emptyHint, out TextMeshProUGUI addressText, out GameObject[] roomRows)
        {
            roomRows = new GameObject[6];
            AddBox(parent, "BrowserListPanel", 72f, 208f, 1188f, 632f, PanelFill, Border, 2f);
            AddText(parent, "TextListTitle", "DANH SÁCH PHÒNG LAN", 100f, 252f, 22f, TextMain, anton, 500f, false);
            AddText(parent, "TextListSub", "TỰ DÒ CÁC PHÒNG ĐANG MỞ TRONG MẠNG LAN", 102f, 278f, 9.5f, TextMuted, barlow, 760f, false);

            GameObject hint = new GameObject("EmptyHint", typeof(RectTransform));
            RectTransform hintRect = hint.GetComponent<RectTransform>();
            hintRect.SetParent(parent, false);
            hintRect.anchorMin = Vector2.zero;
            hintRect.anchorMax = Vector2.one;
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;
            emptyHint = hint;

            TextMeshProUGUI hintTitle = AddText(hintRect, "TextEmptyTitle", "CHƯA TÌM THẤY PHÒNG LAN NÀO", 96f, 486f, 25f, TextMuted, anton, 1120f, false);
            hintTitle.alignment = TextAlignmentOptions.Center;
            hintTitle.characterSpacing = 0.3f / 25f * 100f;
            TextMeshProUGUI hintBody = AddText(hintRect, "TextEmptyBody", "BẤM TẠO PHÒNG ĐỂ MỞ PHÒNG, HOẶC NHẬP IP HOST Ở DƯỚI.", 96f, 520f, 11f, new Color32(0x4E, 0x5A, 0x69, 0xFF), barlow, 1120f, false);
            hintBody.alignment = TextAlignmentOptions.Center;

            for (int i = 0; i < 6; i++)
            {
                float y = 304f + i * 88f;
                GameObject row = new GameObject("RoomRowGroup" + (i + 1), typeof(RectTransform));
                RectTransform rowRect = row.GetComponent<RectTransform>();
                rowRect.SetParent(parent, false);
                rowRect.anchorMin = Vector2.zero;
                rowRect.anchorMax = Vector2.one;
                rowRect.offsetMin = Vector2.zero;
                rowRect.offsetMax = Vector2.zero;
                row.SetActive(false);
                roomRows[i] = row;

                AddBox(rowRect, "RoomRow" + (i + 1), 96f, y, 1140f, 74f, new Color32(13, 21, 32, 235), new Color32(33, 44, 57, 255), 1f);
                AddLine(rowRect, "RoomRowAccent" + (i + 1), 96f, y + 16f, 3f, 42f, Orange);
                names[i] = AddText(rowRect, "TextRoomName" + (i + 1), string.Empty, 122f, y + 32f, 15f, TextMain, barlow, 880f, false);
                details[i] = AddText(rowRect, "TextRoomDetail" + (i + 1), string.Empty, 122f, y + 55f, 9.5f, TextMuted, barlow, 900f, false);
                TextMeshProUGUI enter = AddText(rowRect, "TextRoomEnter" + (i + 1), "VÀO  ›", 1048f, y + 45f, 13f, Orange, barlow, 160f, false);
                enter.alignment = TextAlignmentOptions.TopRight;
            }

            AddBox(parent, "BrowserInfoPanel", 1288f, 208f, 560f, 632f, PanelFill, Border, 2f);
            AddLine(parent, "BrowserInfoAccent", 1288f, 208f, 4f, 72f, Orange);
            AddText(parent, "TextInfoLabel", "ẢI ĐANG CHỌN", 1316f, 252f, 10f, TextMuted, barlow, 480f, false);
            AddText(parent, "TextInfoBig", "LAN", 1316f, 310f, 58f, new Color32(0x35, 0x42, 0x52, 0xFF), anton, 480f, false);
            TextMeshProUGUI info = AddText(parent, "TextInfoBody", "CHƠI LAN: CÁC MÁY PHẢI Ở CÙNG\nMỘT MẠNG, KHÔNG CẦN INTERNET.\n\nTỰ DÒ PHÒNG HOẶC NHẬP IP HOST.\nBẤM VÀO MỘT PHÒNG ĐỂ VÀO NGAY.", 1316f, 370f, 11f, TextMain, barlow, 490f, false);
            info.textWrappingMode = TextWrappingModes.Normal;
            info.lineSpacing = 12f;

            AddLine(parent, "InfoFlowRule", 1316f, 536f, 500f, 1f, Line);
            AddText(parent, "TextFlowLabel", "QUY TRÌNH MỞ PHÒNG", 1316f, 566f, 9.5f, TextMuted, barlow, 480f, false);
            string[] steps =
            {
                "CHỌN ẢI Ở MÀN CHỌN ẢI",
                "TẠO PHÒNG TRÊN MÁY HOST",
                "CHIA SẺ ĐỊA CHỈ HOST CHO ĐỒNG ĐỘI",
                "HOST BẤM BẮT ĐẦU KHI ĐỦ NGƯỜI"
            };
            for (int i = 0; i < steps.Length; i++)
            {
                float y = 602f + i * 42f;
                AddText(parent, "TextFlowNum" + (i + 1), "0" + (i + 1), 1316f, y, 10f, Orange, barlow, 40f, false);
                TextMeshProUGUI step = AddText(parent, "TextFlowStep" + (i + 1), steps[i], 1352f, y, 9.8f, TextMain, barlow, 460f, false);
                step.characterSpacing = 0.2f / 9.8f * 100f;
            }

            AddText(parent, "TextInfoHint", "CÙNG MẠNG LAN  /  KHÔNG CẦN INTERNET", 1316f, 780f, 8.6f, Teal, barlow, 490f, false);

            AddButton(parent, "ButtonCreate", 140f, 866f, 320f, 68f, Orange, new Color32(0x1A, 0x12, 0x06, 0xFF), "TẠO PHÒNG", 21f, anton);
            AddButton(parent, "ButtonRefresh", 482f, 866f, 240f, 68f, new Color32(15, 22, 32, 255), TextMain, "TẢI LẠI", 16f, barlow);
            AddBox(parent, "JoinCodeBox", 740f, 866f, 332f, 68f, new Color32(15, 22, 32, 255), new Color32(80, 90, 104, 255), 1f);
            addressText = AddText(parent, "TextJoinCode", "NHẬP IP HOST", 762f, 907f, 17f, TextMuted, barlow, 296f, false);
            AddButton(parent, "ButtonJoinCode", 1092f, 866f, 180f, 68f, Orange, new Color32(0x1A, 0x12, 0x06, 0xFF), "VÀO PHÒNG", 15f, barlow);
            AddLine(parent, "BrowserFooterRule", 72f, 960f, 1776f, 1f, Line);
        }

        private static void BuildRoom(RectTransform parent, TMP_FontAsset anton, TMP_FontAsset barlow, TextMeshProUGUI[] titles, TextMeshProUGUI[] details, out TextMeshProUGUI roomAddress, out TextMeshProUGUI roomHint, out TextMeshProUGUI roomNote, out TextMeshProUGUI readyLabel, out TextMeshProUGUI startLabel)
        {
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? 72f : 640f;
                float y = i < 2 ? 216f : 420f;
                AddBox(parent, "Slot" + (i + 1), x, y, 520f, 184f, i == 0 ? new Color32(12, 24, 20, 240) : PanelFill, i == 0 ? new Color32(47, 232, 224, 180) : Border, 2f);
                AddLine(parent, "SlotAccent" + (i + 1), x, y, 4f, 48f, i == 0 ? Teal : Orange);
                titles[i] = AddText(parent, "TextSlotTitle" + (i + 1), "P" + (i + 1) + " / TRỐNG", x + 26f, y + 46f, 22f, TextMuted, anton, 460f, false);
                details[i] = AddText(parent, "TextSlotDetail" + (i + 1), "CHƯA THAM GIA", x + 26f, y + 82f, 11f, TextMuted, barlow, 460f, false);
                AddLine(parent, "SlotRule" + (i + 1), x + 26f, y + 104f, 468f, 1f, Line);
                AddText(parent, "TextSlotFoot" + (i + 1), "ĐỘI HÌNH CO-OP 2-4", x + 26f, y + 138f, 8.4f, new Color32(0x4E, 0x5A, 0x69, 0xFF), barlow, 460f, false);
            }

            AddBox(parent, "RoomInfoPanel", 1288f, 216f, 560f, 388f, PanelFill, Border, 2f);
            AddLine(parent, "RoomInfoAccent", 1288f, 216f, 4f, 58f, Teal);
            AddText(parent, "TextRoomCodeLabel", "PHÒNG LAN / IP HOST", 1316f, 260f, 10f, TextMuted, barlow, 480f, false);
            roomAddress = AddText(parent, "TextRoomCode", "—", 1316f, 306f, 27f, TextMain, anton, 500f, false);
            roomHint = AddText(parent, "TextRoomHint", "ĐANG CHỜ ĐỒNG ĐỘI…", 1316f, 356f, 11.5f, Purple, barlow, 500f, false);
            roomHint.textWrappingMode = TextWrappingModes.Normal;
            roomHint.lineSpacing = 10f;

            AddLine(parent, "RoomInfoRule", 1316f, 438f, 500f, 1f, Line);
            roomNote = AddText(parent, "TextRoomNote", "ĐỊA CHỈ SẼ HIỆN KHI MỞ PHÒNG.", 1316f, 470f, 9.6f, TextMuted, barlow, 490f, false);
            roomNote.textWrappingMode = TextWrappingModes.Normal;
            roomNote.lineSpacing = 8f;
            AddButton(parent, "ButtonCopyAddress", 1316f, 540f, 300f, 44f, new Color32(15, 22, 32, 255), Orange, "SAO CHÉP ĐỊA CHỈ", 13f, barlow);

            AddButton(parent, "ButtonReady", 140f, 690f, 300f, 62f, new Color32(15, 22, 32, 255), TextMain, "SẴN SÀNG", 17f, barlow);
            AddButton(parent, "ButtonGarage", 520f, 690f, 300f, 62f, new Color32(15, 22, 32, 255), Orange, "NHÀ XE", 17f, barlow);
            AddButton(parent, "ButtonStart", 140f, 770f, 420f, 76f, Orange, new Color32(0x1A, 0x12, 0x06, 0xFF), "BẮT ĐẦU CHIẾN ĐẤU", 21f, anton);
            AddButton(parent, "ButtonLeave", 140f, 862f, 260f, 54f, new Color32(15, 22, 32, 255), TextMain, "RỜI PHÒNG", 14f, barlow);

            readyLabel = FindText(parent, "TextButtonReadyLabel");
            startLabel = FindText(parent, "TextButtonStartLabel");
        }

        private static void AddButton(RectTransform parent, string name, float x, float y, float width, float height, Color fill, Color textColor, string label, float fontSize, TMP_FontAsset font)
        {
            AddBox(parent, name, x, y, width, height, fill, new Color32(80, 90, 104, 255), 1f);
            TextMeshProUGUI text = AddText(parent, "Text" + name + "Label", label, x, y + height * 0.5f + fontSize * 0.36f, fontSize, textColor, font, width, false);
            text.alignment = TextAlignmentOptions.Center;
            text.characterSpacing = 0.2f / fontSize * 100f;
        }

        private static TextMeshProUGUI FindText(RectTransform parent, string name)
        {
            Transform[] children = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == name)
                {
                    return children[i].GetComponent<TextMeshProUGUI>();
                }
            }

            return null;
        }

        private static void AddFooter(RectTransform root, TMP_FontAsset barlow)
        {
            AddText(root, "TextLobbyHeaderMini", "CHIẾN DỊCH  /  PHÒNG LAN", 74f, 60f, 8.4f, new Color32(0x4E, 0x5A, 0x69, 0xFF), barlow, 600f, false);
            AddText(root, "TextLobbyFooter", "IRON WASTELAND  /  BUILD v1.0-alpha", 74f, 1040f, 8.4f, new Color32(0x4E, 0x5A, 0x69, 0xFF), barlow, 900f, false);
            TextMeshProUGUI esc = AddText(root, "TextLobbyEsc", "ESC  ĐỂ QUAY LẠI", 1590f, 1040f, 8.4f, new Color32(0x4E, 0x5A, 0x69, 0xFF), barlow, 260f, false);
            esc.alignment = TextAlignmentOptions.TopRight;
        }

        private static void AddBox(RectTransform parent, string name, float x, float y, float width, float height, Color fill, Color border, float borderWidth)
        {
            Image image = AddImage(parent, new LayerRect(name, x, y, width, height), null);
            image.color = fill;
            AddLine(parent, name + "Top", x, y, width, borderWidth, border);
            AddLine(parent, name + "Bottom", x, y + height - borderWidth, width, borderWidth, border);
            AddLine(parent, name + "Left", x, y, borderWidth, height, border);
            AddLine(parent, name + "Right", x + width - borderWidth, y, borderWidth, height, border);
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

        private static Image AddLine(RectTransform parent, string name, float x, float y, float width, float height, Color color)
        {
            Image image = AddImage(parent, new LayerRect(name, x, y, width, height), null);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

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
    }
}
