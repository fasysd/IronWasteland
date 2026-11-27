using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using IronWasteland.Tanks;
using IronWasteland.UI;
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
    /// Dựng màn CHỌN PHỤ KIỆN TRANG BỊ từ dữ liệu trong
    /// Assets/Art/UI/GarageAccessory/accessory_data.txt và bộ ảnh phụ kiện.
    /// Chạy lại bằng menu Tools/Iron Wasteland/Rebuild Garage Accessory.
    /// </summary>
    public static class GarageAccessoryBuilder
    {
        private const string ArtDir = "Assets/Art/UI/GarageAccessory";
        private const string ItemDir = ArtDir + "/Items";
        private const string IconDir = ArtDir + "/Icons";
        private const string CatalogPath = "Assets/Settings/AccessoryCatalog.asset";
        private const string ScenePath = "Assets/Scenes/GarageAccessory.unity";
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;
        private const int CardCount = 20;
        private const float CardWidth = 276f;
        private const float CardHeight = 596f;
        /// <summary>Dời cả cụm icon + chữ xuống giữa card; divider trong sprite cũng dời theo cùng khoảng này.</summary>
        private const float CardContentShiftY = 60f;
        private const float CardPitch = 290.4f;
        private const float ViewportX = 96f;
        private const float ViewportY = 276f;
        private const float ViewportWidth = 1728f;
        private const float ViewportHeight = 596f;

        /// <summary>Thứ tự hiển thị mong muốn, lấy theo đúng 6 thẻ đầu của bản thiết kế.</summary>
        private static readonly string[] OrderKeys =
        {
            "loi_dong_co_trong_luc_dia_mach",
            "loi_huyet_tinh_sinh_hoc",
            "loi_nang_luong_thien_thach_no",
            "loi_ma_phap_hoi_tu_quang_tu",
            "dong_co_xung_kich_tinh_the_thach_anh",
            "loi_ma_phap_hac_nguyet",
            "dong_co_tua_bin_linh_hoa",
            "loi_ma_tran_cong_huong_mones",
            "dong_co_phan_pha_hu_khong",
            "loi_nhiet_hach_ba_loi",
            "dong_co_thuy_luc_ma_phap_dam_lay",
            "dong_co_tua_bin_xoay_nuoc",
            "loi_ma_phap_bao_tu_doc",
            "loi_tu_dien_ma_phap_sam_set",
            "loi_ma_tran_khien_nhat_thuc",
            "loi_dong_co_tai_sinh_chu_thap",
            "loi_thuy_tinh_tri_lieu",
            "loi_lien_ket_song_tinh_nguyen_to",
            "loi_dong_co_luong_cuc_the2face",
            "loi_dong_co_giap_thong_linh_d1onk"
        };

        /// <summary>Tên nhóm và tên ngắn hiển thị trên thẻ, khớp bản thiết kế.</summary>
        private struct DisplayInfo
        {
            public string Category;
            public string Name;

            public DisplayInfo(string category, string name)
            {
                Category = category;
                Name = name;
            }
        }

        private static readonly Dictionary<string, DisplayInfo> Displays = new Dictionary<string, DisplayInfo>
        {
            { "loi_dong_co_trong_luc_dia_mach", new DisplayInfo("LÕI ĐỘNG CƠ", "TRỌNG LỰC ĐỊA MẠCH") },
            { "loi_huyet_tinh_sinh_hoc", new DisplayInfo("LÕI HUYẾT TINH", "SINH HỌC") },
            { "loi_nang_luong_thien_thach_no", new DisplayInfo("LÕI NĂNG LƯỢNG", "THIÊN THẠCH NỔ") },
            { "loi_ma_phap_hoi_tu_quang_tu", new DisplayInfo("LÕI MA PHÁP", "HỘI TỤ QUANG TỬ") },
            { "dong_co_xung_kich_tinh_the_thach_anh", new DisplayInfo("ĐỘNG CƠ XUNG KÍCH", "TINH THỂ THẠCH ANH") },
            { "loi_ma_phap_hac_nguyet", new DisplayInfo("LÕI MA PHÁP", "HẮC NGUYỆT") },
            { "dong_co_tua_bin_linh_hoa", new DisplayInfo("ĐỘNG CƠ", "TUA-BIN LINH HỎA") },
            { "loi_ma_tran_cong_huong_mones", new DisplayInfo("LÕI MA TRẬN", "CỘNG HƯỞNG MONES") },
            { "dong_co_phan_pha_hu_khong", new DisplayInfo("ĐỘNG CƠ", "PHÂN PHA HƯ KHÔNG") },
            { "loi_nhiet_hach_ba_loi", new DisplayInfo("LÕI NHIỆT HẠCH", "BA LÕI") },
            { "dong_co_thuy_luc_ma_phap_dam_lay", new DisplayInfo("ĐỘNG CƠ", "THỦY LỰC MA PHÁP ĐẦM LẦY") },
            { "dong_co_tua_bin_xoay_nuoc", new DisplayInfo("ĐỘNG CƠ", "TUA-BIN XOÁY NƯỚC") },
            { "loi_ma_phap_bao_tu_doc", new DisplayInfo("LÕI MA PHÁP", "BÀO TỬ ĐỘC") },
            { "loi_tu_dien_ma_phap_sam_set", new DisplayInfo("LÕI TỤ ĐIỆN", "MA PHÁP SẤM SÉT") },
            { "loi_ma_tran_khien_nhat_thuc", new DisplayInfo("LÕI MA TRẬN", "KHIÊN NHẬT THỰC") },
            { "loi_dong_co_tai_sinh_chu_thap", new DisplayInfo("LÕI ĐỘNG CƠ", "TÁI SINH CHỮ THẬP") },
            { "loi_thuy_tinh_tri_lieu", new DisplayInfo("LÕI THỦY TINH", "TRỊ LIỆU") },
            { "loi_lien_ket_song_tinh_nguyen_to", new DisplayInfo("LÕI LIÊN KẾT", "SONG TINH NGUYÊN TỐ") },
            { "loi_dong_co_luong_cuc_the2face", new DisplayInfo("LÕI ĐỘNG CƠ", "LƯỠNG CỰC THE2FACE") },
            { "loi_dong_co_giap_thong_linh_d1onk", new DisplayInfo("LÕI SIÊU CƠ GIÁP", "THỐNG LĨNH D1ONK") }
        };

        private sealed class RawItem
        {
            public string Key;
            public string Name;
            public string Description;
            public string[] Stats;
        }

        [MenuItem("Tools/Iron Wasteland/Rebuild Garage Accessory")]
        public static void Rebuild()
        {
            ConfigureSprites();
            AccessoryCatalog catalog = BuildCatalog();
            if (catalog == null)
            {
                Debug.LogError("[GarageAccessoryBuilder] Không tạo được danh mục phụ kiện.");
                return;
            }

            BuildScene(catalog);
        }

        /// <summary>Đặt lại thiết lập import cho toàn bộ ảnh của màn phụ kiện.</summary>
        private static void ConfigureSprites()
        {
            string[] files = Directory.GetFiles(ArtDir, "*.png");
            for (int i = 0; i < files.Length; i++)
            {
                ConfigureSprite(files[i], 2048, true);
            }

            files = Directory.GetFiles(ItemDir, "*.jpg");
            for (int i = 0; i < files.Length; i++)
            {
                ConfigureSprite(files[i], 512, false);
            }
        }

        private static void ConfigureSprite(string file, int maxSize, bool transparent)
        {
            string path = file.Replace('\\', '/');
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = transparent;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = transparent ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
        }

        /// <summary>Đọc data.txt, tạo asset danh mục 20 phụ kiện.</summary>
        private static AccessoryCatalog BuildCatalog()
        {
            string dataPath = Path.Combine(Application.dataPath, "Art/UI/GarageAccessory/accessory_data.txt");
            if (!File.Exists(dataPath))
            {
                Debug.LogError("[GarageAccessoryBuilder] Thiếu file dữ liệu " + dataPath);
                return null;
            }

            List<RawItem> rawItems = ParseData(File.ReadAllText(dataPath));
            if (rawItems.Count == 0)
            {
                Debug.LogError("[GarageAccessoryBuilder] File dữ liệu không có phụ kiện nào.");
                return null;
            }

            Dictionary<string, RawItem> byKey = new Dictionary<string, RawItem>();
            for (int i = 0; i < rawItems.Count; i++)
            {
                if (!byKey.ContainsKey(rawItems[i].Key))
                {
                    byKey.Add(rawItems[i].Key, rawItems[i]);
                }
            }

            List<RawItem> ordered = new List<RawItem>();
            for (int i = 0; i < OrderKeys.Length; i++)
            {
                RawItem item;
                if (byKey.TryGetValue(OrderKeys[i], out item))
                {
                    ordered.Add(item);
                }
                else
                {
                    Debug.LogWarning("[GarageAccessoryBuilder] Thiếu dữ liệu cho " + OrderKeys[i]);
                }
            }

            for (int i = 0; i < rawItems.Count; i++)
            {
                if (!ordered.Contains(rawItems[i]))
                {
                    ordered.Add(rawItems[i]);
                }
            }

            AssetDatabase.DeleteAsset(CatalogPath);
            AccessoryCatalog catalog = ScriptableObject.CreateInstance<AccessoryCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("m_Entries");
            entries.arraySize = ordered.Count;
            for (int i = 0; i < ordered.Count; i++)
            {
                RawItem item = ordered[i];
                DisplayInfo display;
                if (!Displays.TryGetValue(item.Key, out display))
                {
                    display = new DisplayInfo("PHỤ KIỆN", item.Name.ToUpperInvariant());
                }

                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Key").stringValue = item.Key;
                entry.FindPropertyRelative("DisplayName").stringValue = display.Name;
                entry.FindPropertyRelative("Category").stringValue = display.Category;
                entry.FindPropertyRelative("Role").stringValue = DeriveRole(item.Stats);
                entry.FindPropertyRelative("Description").stringValue = item.Description;

                SerializedProperty stats = entry.FindPropertyRelative("Stats");
                stats.arraySize = item.Stats.Length;
                for (int s = 0; s < item.Stats.Length; s++)
                {
                    stats.GetArrayElementAtIndex(s).stringValue = item.Stats[s];
                }

                Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + "/" + item.Key + ".png");
            if (icon == null)
            {
                icon = AssetDatabase.LoadAssetAtPath<Sprite>(ItemDir + "/" + item.Key + ".jpg");
            }
                entry.FindPropertyRelative("SalvageValue").intValue = DeriveSalvageValue(i);
                entry.FindPropertyRelative("Icon").objectReferenceValue = icon;
                if (icon == null)
                {
                    Debug.LogWarning("[GarageAccessoryBuilder] Không tìm thấy icon " + item.Key + ".jpg");
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        /// <summary>
        /// Giá trị phân rã theo thứ tự phụ kiện trong danh mục: món càng về sau càng xịn
        /// nên cho nhiều ngọc hơn. Sửa bảng này khi cần cân bằng lại kinh tế.
        /// </summary>
        private static int DeriveSalvageValue(int index)
        {
            if (index < 8)
            {
                return 5;
            }

            if (index < 14)
            {
                return 8;
            }

            return 12;
        }

        private static List<RawItem> ParseData(string text)
        {
            List<RawItem> items = new List<RawItem>();
            RawItem current = null;
            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("-", StringComparison.Ordinal))
                {
                    if (current != null)
                    {
                        items.Add(current);
                    }

                    string body = line.Substring(1).Trim();
                    int comma = body.IndexOf(',');
                    if (comma <= 0)
                    {
                        current = null;
                        continue;
                    }

                    string name = body.Substring(0, comma).Trim();
                    string rest = body.Substring(comma + 1).Trim();
                    Match match = Regex.Match(rest, "^([a-z0-9_]+)");
                    if (!match.Success)
                    {
                        current = null;
                        continue;
                    }

                    string key = match.Groups[1].Value;
                    string statsText = rest.Substring(match.Length).Trim().TrimStart(',').Trim();
                    current = new RawItem
                    {
                        Key = key,
                        Name = name,
                        Stats = ParseStats(statsText),
                        Description = string.Empty
                    };
                }
                else if (current != null && line.Length > 0)
                {
                    current.Description = current.Description.Length == 0
                        ? line
                        : current.Description + " " + line;
                }
            }

            if (current != null)
            {
                items.Add(current);
            }

            return items;
        }

        private static string[] ParseStats(string text)
        {
            string[] parts = text.Split(',');
            List<string> stats = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string stat = parts[i].Trim().TrimEnd('.');
                if (stat.Length == 0)
                {
                    continue;
                }

                stat = Regex.Replace(stat, "^(\\d+%)(\\S)", "+$1 $2");
                if (char.IsDigit(stat[0]))
                {
                    stat = "+" + stat;
                }

                stats.Add(stat);
            }

            return stats.ToArray();
        }

        private static string DeriveRole(string[] stats)
        {
            string joined = string.Join(" ", stats).ToLowerInvariant();
            bool attack = joined.Contains("tấn công");
            bool health = joined.Contains("giới hạn máu");
            bool defense = joined.Contains("phòng thủ");
            bool speed = joined.Contains("tốc độ");
            bool crit = joined.Contains("tỉ lệ sát thương");
            bool pen = joined.Contains("xuyên giáp");
            bool energy = joined.Contains("năng lượng");
            bool cooldown = joined.Contains("giảm hồi chiêu");

            if (attack && health && defense)
            {
                return "ĐA NHIỆM  /  THỐNG LĨNH";
            }

            if (attack && health)
            {
                return "HỖI PHỤC  /  HỖ TRỢ";
            }

            if (health && defense)
            {
                return "GIÁP  /  SINH TỒN";
            }

            if (attack && pen)
            {
                return "SÁT THƯƠNG  /  XUYÊN GIÁP";
            }

            if (crit && pen)
            {
                return "SÁT THƯƠNG  /  TẦM XA";
            }

            if (crit && speed)
            {
                return "CƠ ĐỘNG  /  SÁT THƯƠNG";
            }

            if (attack && speed)
            {
                return "CƠ ĐỘNG  /  SÁT THƯƠNG";
            }

            if (speed)
            {
                return "CƠ ĐỘNG  /  SINH TỒN";
            }

            if (energy && cooldown)
            {
                return "NĂNG LƯỢNG  /  HỖ TRỢ";
            }

            if (health && cooldown)
            {
                return "HỒI PHỤC  /  HỖ TRỢ";
            }

            if (defense && cooldown)
            {
                return "GIÁP  /  HỖ TRỢ";
            }

            if (attack && cooldown)
            {
                return "SÁT THƯƠNG  /  HỖ TRỢ";
            }

            if (attack)
            {
                return "SÁT THƯƠNG  /  NĂNG LƯỢNG";
            }

            if (health)
            {
                return "SINH TỒN  /  HỖ TRỢ";
            }

            if (defense)
            {
                return "GIÁP  /  PHÒNG THỦ";
            }

            if (energy)
            {
                return "NĂNG LƯỢNG  /  HỖ TRỢ";
            }

            if (crit)
            {
                return "SÁT THƯƠNG  /  CHÍ MẠNG";
            }

            return "HỖ TRỢ  /  LINH HOẠT";
        }

        /// <summary>Dựng scene UI và gắn toàn bộ tham chiếu cho controller.</summary>
        private static void BuildScene(AccessoryCatalog catalog)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            GameObject canvasObject = new GameObject(
                "GarageAccessoryCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
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

            TMP_FontAsset anton = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Generated/AntonMainMenu SDF.asset");
            TMP_FontAsset barlow600 = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Generated/GarageBarlow600 SDF.asset");
            TMP_FontAsset barlow700 = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Generated/BarlowCondensed700MainMenu SDF.asset");
            TMP_FontAsset barlow800 = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Generated/GarageBarlow800 SDF.asset");
            TMP_FontAsset barlow900 = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Generated/GarageBarlow900 SDF.asset");
            TMP_FontAsset mono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Generated/GarageShareTechMono SDF.asset");

            Sprite bg = LoadUi("bg");
            Sprite cardNormal = LoadUi("card_normal");
            Sprite cardSelected = LoadUi("card_selected");
            Sprite cardHover = LoadUi("card_hover");
            Sprite iconFrame = LoadUi("icon_frame");
            Sprite badge = LoadUi("badge_selected");
            Sprite check = LoadUi("check");
            Sprite buttonSecondary = LoadUi("button_secondary");
            Sprite buttonConfirm = LoadUi("button_confirm");
            Sprite close = LoadUi("close");
            Sprite keycap = LoadUi("keycap");
            Sprite scrollThumb = LoadUi("scroll_thumb");

            AddImage(root, "Background", 0f, 0f, DesignWidth, DesignHeight, bg);
            AddImage(root, "TitleAccent", 103f, 151f, 3f, 30f, null, new Color32(0xFF, 0x7A, 0x18, 0xFF));

            TextMeshProUGUI breadcrumb = AddText(root, "TextBreadcrumb", "BLACK SHADOW / PHỤ KIỆN / NHÀ XE", 116f, 140f, 10.2f, new Color32(0x5A, 0x66, 0x75, 0xFF), barlow600, 900f, 2.2f);
            TextMeshProUGUI title = AddText(root, "TextTitle", "CHỌN PHỤ KIỆN TRANG BỊ", 116f, 183f, 30f, new Color32(0xE6, 0xED, 0xF5, 0xFF), anton, 900f, 0.6f);
            TextMeshProUGUI subtitle = AddText(root, "TextSubtitle", "TỔNG 20 PHỤ KIỆN  ·  MỖI XE CHỈ TRANG BỊ ĐƯỢC 1 PHỤ KIỆN", 116f, 216f, 10.2f, new Color32(0x6F, 0x7D, 0x8C, 0xFF), barlow600, 1000f, 1.2f);
            AddText(root, "TextUnique", "1 ĐỘ DUY NHẤT", 1450f, 216f, 9.4f, new Color32(0x5A, 0x66, 0x75, 0xFF), barlow600, 300f, 1.6f, TextAlignmentOptions.TopRight);
            AddImage(root, "Close", 1786f, 160f, 52f, 52f, close);

            RectTransform viewport = AddRect(root, "Viewport", ViewportX, ViewportY, ViewportWidth, ViewportHeight);
            viewport.gameObject.AddComponent<RectMask2D>();
            float contentWidth = (CardCount - 1) * CardPitch + CardWidth;
            RectTransform content = AddRect(viewport, "Content", 0f, 0f, contentWidth, CardHeight);
            Image scroll = AddImage(root, "ScrollThumb", 96f, 882f, 300f, 3f, scrollThumb);

            GarageAccessoryController.CardView[] cards = new GarageAccessoryController.CardView[CardCount];
            for (int i = 0; i < CardCount; i++)
            {
                cards[i] = BuildCard(content, i, cardNormal, cardSelected, cardHover, iconFrame, badge, check, barlow600, barlow700, barlow800, barlow900, mono);
            }

            Image buttonCancelImage = AddImage(root, "ButtonCancel", 1210f, 900f, 290f, 52f, buttonSecondary);
            AddText(root, "TextCancel", "HỦY BỎ", 1210f, 900f, 20f, new Color32(0xC4, 0xD0, 0xDC, 0xFF), barlow800, 290f, 1.8f, TextAlignmentOptions.Center, 52f, false, true);

            Image buttonConfirmImage = AddImage(root, "ButtonConfirm", 1526f, 900f, 296f, 52f, buttonConfirm);
            AddText(root, "TextConfirm", "XÁC NHẬN TRANG BỊ", 1526f, 900f, 18.5f, new Color32(0x0B, 0x0F, 0x14, 0xFF), barlow800, 296f, 1.2f, TextAlignmentOptions.Center, 52f, false, true);
            AddText(root, "TextConfirmArrow", "›", 1788f, 900f, 28f, new Color32(0x0B, 0x0F, 0x14, 0xFF), barlow800, 28f, 0f, TextAlignmentOptions.Center, 52f, false, true);

            AddImage(root, "KeyEsc", 88f, 900f, 54f, 26f, keycap);
            AddText(root, "TextEsc", "ESC", 88f, 900f, 9.6f, new Color32(0x8A, 0x99, 0xAB, 0xFF), mono, 54f, 0f, TextAlignmentOptions.Center, 26f, false, true);
            AddText(root, "TextCancelHint", "HỦY BỎ", 150f, 919f, 12.5f, new Color32(0x8A, 0x99, 0xAB, 0xFF), barlow700, 110f, 0.8f);

            AddImage(root, "KeyEnter", 204f, 900f, 64f, 26f, keycap);
            AddText(root, "TextEnter", "ENTER", 204f, 900f, 9.6f, new Color32(0x8A, 0x99, 0xAB, 0xFF), mono, 64f, 0f, TextAlignmentOptions.Center, 26f, false, true);
            AddText(root, "TextConfirmHint", "XÁC NHẬN TRANG BỊ", 276f, 919f, 12.5f, new Color32(0x8A, 0x99, 0xAB, 0xFF), barlow700, 230f, 0.8f);
            AddText(root, "TextSaveHint", "THAY ĐỔI ĐƯỢC LƯU VÀO NHÀ XE", 480f, 919f, 11.5f, new Color32(0x4B, 0x58, 0x67, 0xFF), barlow600, 380f, 1.4f);

            GarageAccessoryController controller = canvasObject.AddComponent<GarageAccessoryController>();
            SerializedObject serialized = new SerializedObject(controller);
            AccessoryCatalog catalogAsset = AssetDatabase.LoadAssetAtPath<AccessoryCatalog>(CatalogPath);
            serialized.FindProperty("m_Catalog").objectReferenceValue = catalogAsset != null ? catalogAsset : catalog;
            serialized.FindProperty("m_Breadcrumb").objectReferenceValue = breadcrumb;
            serialized.FindProperty("m_Subtitle").objectReferenceValue = subtitle;
            serialized.FindProperty("m_Content").objectReferenceValue = content;
            serialized.FindProperty("m_Viewport").objectReferenceValue = viewport;
            serialized.FindProperty("m_ScrollThumb").objectReferenceValue = scroll;
            serialized.FindProperty("m_CardNormalSprite").objectReferenceValue = cardNormal;
            serialized.FindProperty("m_CardSelectedSprite").objectReferenceValue = cardSelected;

            SerializedProperty cardArray = serialized.FindProperty("m_Cards");
            cardArray.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                SerializedProperty card = cardArray.GetArrayElementAtIndex(i);
                card.FindPropertyRelative("Root").objectReferenceValue = cards[i].Root;
                card.FindPropertyRelative("Background").objectReferenceValue = cards[i].Background;
                card.FindPropertyRelative("Hover").objectReferenceValue = cards[i].Hover;
                card.FindPropertyRelative("SelectedGroup").objectReferenceValue = cards[i].SelectedGroup;
                card.FindPropertyRelative("ChoiceLabel").objectReferenceValue = cards[i].ChoiceLabel;
                card.FindPropertyRelative("Icon").objectReferenceValue = cards[i].Icon;
                card.FindPropertyRelative("Number").objectReferenceValue = cards[i].Number;
                card.FindPropertyRelative("Category").objectReferenceValue = cards[i].Category;
                card.FindPropertyRelative("Name").objectReferenceValue = cards[i].Name;
                card.FindPropertyRelative("Role").objectReferenceValue = cards[i].Role;
                card.FindPropertyRelative("Stats").objectReferenceValue = cards[i].Stats;
                card.FindPropertyRelative("Description").objectReferenceValue = cards[i].Description;
                card.FindPropertyRelative("Footer").objectReferenceValue = cards[i].Footer;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            UIHit.EnsureEventSystem(scene);
            Button closeButton = UIHit.AddZone(root, "HitClose", 1786f, 160f, 52f, 52f);
            UnityEventTools.AddPersistentListener(closeButton.onClick, controller.PressCancel);
            Button cancelButton = UIHit.Attach(buttonCancelImage);
            UnityEventTools.AddPersistentListener(cancelButton.onClick, controller.PressCancel);
            Button confirmButton = UIHit.Attach(buttonConfirmImage);
            UnityEventTools.AddPersistentListener(confirmButton.onClick, controller.PressConfirm);
            for (int i = 0; i < cards.Length; i++)
            {
                Button cardButton = UIHit.Attach(cards[i].Background);
                UnityEventTools.AddIntPersistentListener(cardButton.onClick, controller.SelectCard, i);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[GarageAccessoryBuilder] Đã dựng " + ScenePath + " với " + catalog.Count + " phụ kiện.");
        }

        private static GarageAccessoryController.CardView BuildCard(
            RectTransform content,
            int index,
            Sprite cardNormal,
            Sprite cardSelected,
            Sprite cardHover,
            Sprite iconFrame,
            Sprite badge,
            Sprite check,
            TMP_FontAsset barlow600,
            TMP_FontAsset barlow700,
            TMP_FontAsset barlow800,
            TMP_FontAsset barlow900,
            TMP_FontAsset mono)
        {
            RectTransform cardRoot = AddRect(content, "Card" + (index + 1).ToString("00"), index * CardPitch, 0f, CardWidth, CardHeight);
            Image background = cardRoot.gameObject.AddComponent<Image>();
            background.sprite = cardNormal;
            background.raycastTarget = false;

            Image hover = AddImage(cardRoot, "Hover", 0f, 0f, CardWidth, CardHeight, cardHover);
            hover.enabled = false;

            RectTransform selectedGroup = AddRect(cardRoot, "SelectedGroup", 0f, 0f, CardWidth, CardHeight);
            AddImage(selectedGroup, "Badge", 142f, 8f, 118f, 34f, badge);
            AddText(selectedGroup, "TextBadge", "ĐANG DÙNG", 142f, 8f, 9.2f, new Color32(0x0B, 0x0F, 0x14, 0xFF), barlow800, 118f, 1.2f, TextAlignmentOptions.Center, 34f);
            AddImage(selectedGroup, "Check", 218f, 552f, 38f, 38f, check);
            selectedGroup.gameObject.SetActive(false);

            TextMeshProUGUI number = AddText(cardRoot, "TextNumber", (index + 1).ToString("00"), 20f, 34f, 10.4f, new Color32(0x5A, 0x66, 0x75, 0xFF), mono, 44f, 1.2f);
            TextMeshProUGUI choice = AddText(cardRoot, "TextChoice", "LỰA CHỌN", 0f, 34f, 9.2f, new Color32(0x5A, 0x66, 0x75, 0xFF), barlow600, 256f, 1.4f, TextAlignmentOptions.TopRight);

            AddImage(cardRoot, "IconFrame", 86f, 72f + CardContentShiftY, 104f, 104f, iconFrame);
            Image icon = AddImage(cardRoot, "Icon", 90f, 76f + CardContentShiftY, 96f, 96f, null);
            icon.preserveAspect = true;

            TextMeshProUGUI category = AddText(cardRoot, "TextCategory", "LÕI ĐỘNG CƠ", 0f, 190f + CardContentShiftY, 9.0f, new Color32(0x8A, 0x99, 0xAB, 0xFF), barlow700, 276f, 1.4f, TextAlignmentOptions.Top);
            TextMeshProUGUI name = AddText(cardRoot, "TextName", "TRỌNG LỰC ĐỊA MẠCH", 14f, 220f + CardContentShiftY, 19f, new Color32(0xE6, 0xED, 0xF5, 0xFF), barlow900, 248f, 0.6f, TextAlignmentOptions.Top, 40f, true);
            TextMeshProUGUI role = AddText(cardRoot, "TextRole", "GIÁP / PHÒNG THỦ", 0f, 246f + CardContentShiftY, 9.6f, new Color32(0xC9, 0xA9, 0xFF, 0xFF), barlow700, 276f, 1.0f, TextAlignmentOptions.Top);
            TextMeshProUGUI stats = AddText(cardRoot, "TextStats", "+35% PHÒNG THỦ", 20f, 268f + CardContentShiftY, 13.5f, new Color32(0x4A, 0xDE, 0x80, 0xFF), barlow700, 236f, 0.2f, TextAlignmentOptions.Top, 104f);
            TextMeshProUGUI description = AddText(cardRoot, "TextDescription", "Mô tả hiệu ứng của phụ kiện.", 20f, 440f, 16f, new Color32(0x8A, 0x99, 0xAB, 0xFF), barlow600, 236f, 0.2f, TextAlignmentOptions.Top, 126f, true);
            description.overflowMode = TextOverflowModes.Ellipsis;
            TextMeshProUGUI footer = AddText(cardRoot, "TextFooter", "TRANG BỊ  ›", 20f, 576f, 9.2f, new Color32(0x4B, 0x58, 0x67, 0xFF), barlow600, 236f, 1.0f, TextAlignmentOptions.TopRight);

            GarageAccessoryController.CardView view = new GarageAccessoryController.CardView();
            view.Root = cardRoot.gameObject;
            view.Background = background;
            view.Hover = hover;
            view.SelectedGroup = selectedGroup.gameObject;
            view.ChoiceLabel = choice;
            view.Icon = icon;
            view.Number = number;
            view.Category = category;
            view.Name = name;
            view.Role = role;
            view.Stats = stats;
            view.Description = description;
            view.Footer = footer;
            return view;
        }

        private static RectTransform AddRect(RectTransform parent, string name, float x, float y, float width, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Image AddImage(RectTransform parent, string name, float x, float y, float width, float height, Sprite sprite, Color? color = null)
        {
            RectTransform rect = AddRect(parent, name, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            if (color.HasValue)
            {
                image.color = color.Value;
            }

            return image;
        }

        private static TextMeshProUGUI AddText(
            RectTransform parent,
            string name,
            string value,
            float x,
            float baselineY,
            float fontSize,
            Color color,
            TMP_FontAsset font,
            float width,
            float letterSpacing,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft,
            float fixedHeight = 0f,
            bool autoSize = false,
            bool centerInBox = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            float ascentPx = font.faceInfo.ascentLine / font.faceInfo.pointSize * fontSize;
            float height = fixedHeight > 0f ? fixedHeight : fontSize * 1.8f;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            float top = centerInBox ? baselineY : baselineY - ascentPx;
            rect.anchoredPosition = new Vector2(x, -top);

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.characterSpacing = letterSpacing;
            text.textWrappingMode = TextWrappingModes.Normal;
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

        private static Sprite LoadUi(string fileName)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/" + fileName + ".png");
            if (sprite == null)
            {
                Debug.LogError("[GarageAccessoryBuilder] Thiếu sprite " + fileName + ".png");
            }

            return sprite;
        }

        private static void AddSceneToBuildSettings()
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
    }
}
