using System.Collections.Generic;
using System.IO;
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
    /// Dựng scene PHÂN RÃ PHỤ KIỆN (Assets/Scenes/AccessorySalvage.unity).
    /// Bố cục sao y màn CHỌN PHỤ KIỆN TRANG BỊ: cùng canvas, cùng vùng nhìn, cùng 20 thẻ cuộn ngang,
    /// chỉ khác phần đầu màn (nhãn NGỌC thay cho "1 ĐỘ DUY NHẤT"), thêm thanh chọn số lượng ở chân màn
    /// và đổi nhãn chân thẻ thành PHÂN RÃ.
    /// Chạy lại bằng menu Tools/Iron Wasteland/Rebuild Accessory Salvage.
    /// </summary>
    public static class AccessorySalvageBuilder
    {
        private const string ArtDir = "Assets/Art/UI/GarageAccessory";

        /// <summary>
        /// Thư mục nướng các sprite panel sinh bằng code thành file .png.
        /// Lý do: sprite tạo lúc dựng bằng code là object tạm, không lưu được vào scene —
        /// mở lại game tham chiếu thành null và Image vẽ ra ô vuông trắng.
        /// </summary>
        private const string PanelDir = "Assets/Art/UI/Generated/Panels";

        /// <summary>Danh mục dùng chung với Nhà xe; builder này CHỈ đọc, không tạo lại.</summary>
        private const string CatalogPath = "Assets/Settings/AccessoryCatalog.asset";

        private const string ScenePath = "Assets/Scenes/AccessorySalvage.unity";

        /// <summary>Tên đầy đủ của controller, tra bằng reflection để file này vẫn biên dịch được khi controller đang viết song song.</summary>
        private const string ControllerTypeName = "IronWasteland.UI.AccessorySalvageController";

        private const string ControllerAssemblyName = "Assembly-CSharp";

        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;
        private const int CardCount = 20;
        private const float CardWidth = 276f;
        private const float CardHeight = 596f;

        /// <summary>
        /// Dời cả cụm icon + chữ để trùng vị trí với màn CHỌN PHỤ KIỆN TRANG BỊ (đo theo ảnh thiết kế):
        /// tên card ở local y≈203 chứ không phải 220 như khung mặc định.
        /// </summary>
        private const float CardContentShiftY = -18f;

        private const float CardPitch = 290.4f;
        private const float ViewportX = 96f;
        private const float ViewportY = 276f;
        private const float ViewportWidth = 1728f;
        private const float ViewportHeight = 596f;

        private static readonly Color32 InkBright = new Color32(0xE6, 0xED, 0xF5, 0xFF);
        private static readonly Color32 InkSoft = new Color32(0xC4, 0xD0, 0xDC, 0xFF);
        private static readonly Color32 InkValue = new Color32(0xEA, 0xF2, 0xFF, 0xFF);
        private static readonly Color32 InkDim = new Color32(0x8A, 0x99, 0xAB, 0xFF);
        private static readonly Color32 InkGhost = new Color32(0x5A, 0x66, 0x75, 0xFF);
        private static readonly Color32 InkFaint = new Color32(0x4B, 0x58, 0x67, 0xFF);
        private static readonly Color32 InkDeep = new Color32(0x0B, 0x0F, 0x14, 0xFF);
        private static readonly Color32 GoldInk = new Color32(0xFF, 0xD5, 0x4F, 0xFF);
        private static readonly Color32 StatsGreen = new Color32(0x4A, 0xDE, 0x80, 0xFF);
        private static readonly Color32 RolePurple = new Color32(0xC9, 0xA9, 0xFF, 0xFF);
        private static readonly Color32 PanelTop = new Color32(0x12, 0x18, 0x21, 0xFF);
        private static readonly Color32 PanelBorder = new Color32(0x26, 0x31, 0x3D, 0xFF);

        [MenuItem("Tools/Iron Wasteland/Rebuild Accessory Salvage")]
        public static void Rebuild()
        {
            AccessoryCatalog catalog = AssetDatabase.LoadAssetAtPath<AccessoryCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[AccessorySalvageBuilder] Không tìm thấy danh mục phụ kiện tại " + CatalogPath +
                               ". Hãy dựng màn CHỌN PHỤ KIỆN TRANG BỊ trước để sinh danh mục.");
                return;
            }

            BuildScene(catalog);
        }

        /// <summary>Dựng scene UI và gắn toàn bộ tham chiếu cho controller.</summary>
        private static void BuildScene(AccessoryCatalog catalog)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            GameObject canvasObject = new GameObject(
                "AccessorySalvageCanvas",
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

            TextMeshProUGUI breadcrumb = AddText(root, "TextBreadcrumb", "BLACK SHADOW / PHỤ KIỆN / NHÀ XE", 116f, 140f, 10.2f, InkGhost, barlow600, 900f, 2.2f);
            AddText(root, "TextTitle", "PHÂN RÃ PHỤ KIỆN", 116f, 183f, 30f, InkBright, anton, 900f, 0.6f);
            TextMeshProUGUI subtitle = AddText(root, "TextSubtitle", "TỔNG 20 PHỤ KIỆN  ·  MỖI LẦN CHỈ PHÂN RÃ 1 LOẠI", 116f, 216f, 10.2f, new Color32(0x6F, 0x7D, 0x8C, 0xFF), barlow600, 1000f, 1.2f);
            TextMeshProUGUI gems = AddText(root, "TextGems", "NGỌC: 0", 1450f, 216f, 9.4f, InkGhost, barlow600, 300f, 1.6f, TextAlignmentOptions.TopRight);
            Image closeImage = AddImage(root, "Close", 1786f, 160f, 52f, 52f, close);

            RectTransform viewport = AddRect(root, "Viewport", ViewportX, ViewportY, ViewportWidth, ViewportHeight);
            viewport.gameObject.AddComponent<RectMask2D>();
            float contentWidth = (CardCount - 1) * CardPitch + CardWidth;
            RectTransform content = AddRect(viewport, "Content", 0f, 0f, contentWidth, CardHeight);
            Image scroll = AddImage(root, "ScrollThumb", 96f, 882f, 300f, 3f, scrollThumb);

            // Thanh chọn số lượng: chỉ dựng hình, controller tự bắt vùng bấm theo toạ độ.
            Sprite quantityPanelSprite = PanelSprite("salvage_quantity_panel", 336, 44, 8, 8, PanelTop, PanelTop, PanelBorder, 1f, 0);
            Image quantityPanel = AddImage(root, "QuantityPanel", 470f, 894f, 336f, 44f, quantityPanelSprite);
            quantityPanel.type = Image.Type.Simple;
            quantityPanel.raycastTarget = false;

            Sprite keySmall = PanelSprite("salvage_key_small", 36, 36, 6, 6, PanelTop, PanelTop, PanelBorder, 1f, 0);
            Sprite keyMax = PanelSprite("salvage_key_max", 62, 36, 6, 6, PanelTop, PanelTop, PanelBorder, 1f, 0);

            Image minus = AddImage(root, "KeyMinus", 478f, 898f, 36f, 36f, keySmall);
            AddText(minus.rectTransform, "TextMinus", "−", 0f, 0f, 20f, InkSoft, barlow700, 36f, 0f, TextAlignmentOptions.Center, 36f, false, true);

            TextMeshProUGUI quantity = AddText(root, "QuantityValue", "1", 520f, 900f, 20f, InkValue, barlow800, 84f, 0f, TextAlignmentOptions.Center, 36f, false, true);

            Image plus = AddImage(root, "KeyPlus", 610f, 898f, 36f, 36f, keySmall);
            AddText(plus.rectTransform, "TextPlus", "+", 0f, 0f, 20f, InkSoft, barlow700, 36f, 0f, TextAlignmentOptions.Center, 36f, false, true);

            Image max = AddImage(root, "KeyMax", 656f, 898f, 62f, 36f, keyMax);
            AddText(max.rectTransform, "TextMax", "MAX", 0f, 0f, 13f, InkSoft, barlow800, 62f, 0.8f, TextAlignmentOptions.Center, 36f, false, true);

            TextMeshProUGUI salvageHint = AddText(root, "SalvageHint", "NHẬN: 5 NGỌC", 740f, 906f, 13f, GoldInk, barlow600, 400f, 0.8f, TextAlignmentOptions.Left, 24f, false, true);

            Image cancelButton = AddImage(root, "ButtonCancel", 1210f, 900f, 290f, 52f, buttonSecondary);
            AddText(root, "TextCancel", "HỦY BỎ", 1210f, 900f, 20f, InkSoft, barlow800, 290f, 1.8f, TextAlignmentOptions.Center, 52f, false, true);

            Image confirmButton = AddImage(root, "ButtonConfirm", 1526f, 900f, 296f, 52f, buttonConfirm);
            TextMeshProUGUI confirmText = AddText(root, "TextConfirm", "XÁC NHẬN PHÂN RÃ", 1526f, 900f, 18.5f, InkDeep, barlow800, 296f, 1.2f, TextAlignmentOptions.Center, 52f, false, true);
            AddText(root, "TextConfirmArrow", "›", 1788f, 900f, 28f, InkDeep, barlow800, 28f, 0f, TextAlignmentOptions.Center, 52f, false, true);

            AddImage(root, "KeyEsc", 88f, 900f, 54f, 26f, keycap);
            AddText(root, "TextEsc", "ESC", 88f, 900f, 9.6f, InkDim, mono, 54f, 0f, TextAlignmentOptions.Center, 26f, false, true);
            AddText(root, "TextCancelHint", "HỦY BỎ", 150f, 919f, 12.5f, InkDim, barlow700, 110f, 0.8f);

            AddImage(root, "KeyEnter", 204f, 900f, 64f, 26f, keycap);
            AddText(root, "TextEnter", "ENTER", 204f, 900f, 9.6f, InkDim, mono, 64f, 0f, TextAlignmentOptions.Center, 26f, false, true);
            AddText(root, "TextConfirmHint", "XÁC NHẬN PHÂN RÃ", 276f, 919f, 12.5f, InkDim, barlow700, 230f, 0.8f);

            // Giữ object nhưng để trống: màn này không còn dòng gợi ý lưu thay đổi.
            AddText(root, "TextSaveHint", string.Empty, 480f, 919f, 11.5f, InkFaint, barlow600, 380f, 1.4f);

            // Thông báo ngắn sau khi phân rã, nằm trong dải trống giữa thẻ và hàng nút.
            TextMeshProUGUI toast = AddText(root, "ToastText", string.Empty, 706f, 852f, 14f, GoldInk, barlow800,
                508f, 1.2f, TextAlignmentOptions.Center, 26f, false, true);

            System.Type controllerType = ResolveControllerType();
            if (controllerType == null)
            {
                Debug.LogError("[AccessorySalvageBuilder] Chưa tìm thấy kiểu " + ControllerTypeName +
                               ". Hãy tạo AccessorySalvageController rồi chạy lại menu này.");
                return;
            }

            Component controller = canvasObject.AddComponent(controllerType);
            SerializedObject serialized = new SerializedObject(controller);

            AccessoryCatalog catalogAsset = AssetDatabase.LoadAssetAtPath<AccessoryCatalog>(CatalogPath);
            SetObject(serialized, "m_Catalog", catalogAsset != null ? catalogAsset : catalog);
            SetObject(serialized, "m_Breadcrumb", breadcrumb);
            SetObject(serialized, "m_Subtitle", subtitle);
            SetObject(serialized, "m_GemsText", gems);
            SetObject(serialized, "m_Content", content);
            SetObject(serialized, "m_Viewport", viewport);
            SetObject(serialized, "m_ScrollThumb", scroll);
            SetObject(serialized, "m_CardNormalSprite", cardNormal);
            SetObject(serialized, "m_CardSelectedSprite", cardSelected);
            SetObject(serialized, "m_QuantityText", quantity);
            SetObject(serialized, "m_SalvageHint", salvageHint);
            SetObject(serialized, "m_ConfirmText", confirmText);
            SetObject(serialized, "m_ConfirmButtonImage", confirmButton);
            SetObject(serialized, "m_ToastText", toast);

            SetRect(serialized, "m_ViewportArea", new Rect(ViewportX, ViewportY, ViewportWidth, ViewportHeight));

            SerializedProperty cardArray = serialized.FindProperty("m_Cards");
            if (cardArray == null)
            {
                Debug.LogError("[AccessorySalvageBuilder] Controller thiếu mảng m_Cards.");
            }
            else
            {
                cardArray.arraySize = CardCount;
                for (int i = 0; i < CardCount; i++)
                {
                    BuildCard(content, i, cardArray.GetArrayElementAtIndex(i), cardNormal, cardHover,
                        iconFrame, badge, check, barlow600, barlow700, barlow800, barlow900, mono);
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            UIHit.EnsureEventSystem(scene);
            UnityEventTools.AddPersistentListener(UIHit.Attach(closeImage).onClick, BindAction(controller, "PressClose"));
            UnityEventTools.AddPersistentListener(UIHit.Attach(cancelButton).onClick, BindAction(controller, "PressCancel"));
            UnityEventTools.AddPersistentListener(UIHit.Attach(confirmButton).onClick, BindAction(controller, "PressConfirm"));
            UnityEventTools.AddPersistentListener(UIHit.Attach(minus).onClick, BindAction(controller, "PressMinus"));
            UnityEventTools.AddPersistentListener(UIHit.Attach(plus).onClick, BindAction(controller, "PressPlus"));
            UnityEventTools.AddPersistentListener(UIHit.Attach(max).onClick, BindAction(controller, "PressMax"));

            UnityAction<int> selectCard = BindIntAction(controller, "SelectCard");
            for (int i = 0; i < CardCount; i++)
            {
                Transform cardRoot = content.Find("Card" + (i + 1).ToString("00"));
                if (cardRoot == null || !cardRoot.TryGetComponent(out Image cardGraphic))
                {
                    continue;
                }

                Button cardButton = UIHit.Attach(cardGraphic);
                UnityEventTools.AddIntPersistentListener(cardButton.onClick, selectCard, i);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("AccessorySalvage scene rebuilt");
        }

        /// <summary>
        /// Dựng một thẻ phụ kiện và gán thẳng vào phần tử mảng m_Cards của controller.
        /// Truyền SerializedProperty thay vì kiểu CardView để file này không phụ thuộc lúc biên dịch vào controller.
        /// </summary>
        /// <summary>Tạo delegate cho một hàm public của controller vốn được nạp động bằng kiểu.</summary>
        private static UnityAction BindAction(Component target, string methodName)
        {
            return (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), target, methodName);
        }

        /// <summary>Tạo delegate nhận chỉ số cho một hàm public của controller nạp động.</summary>
        private static UnityAction<int> BindIntAction(Component target, string methodName)
        {
            return (UnityAction<int>)System.Delegate.CreateDelegate(typeof(UnityAction<int>), target, methodName);
        }

        private static void BuildCard(
            RectTransform content,
            int index,
            SerializedProperty card,
            Sprite cardNormal,
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
            AddText(selectedGroup, "TextBadge", "ĐANG DÙNG", 142f, 8f, 9.2f, InkDeep, barlow800, 118f, 1.2f, TextAlignmentOptions.Center, 34f);
            AddImage(selectedGroup, "Check", 218f, 552f, 38f, 38f, check);
            selectedGroup.gameObject.SetActive(false);

            TextMeshProUGUI number = AddText(cardRoot, "TextNumber", (index + 1).ToString("00"), 20f, 34f, 10.4f, InkGhost, mono, 44f, 1.2f);
            TextMeshProUGUI choice = AddText(cardRoot, "TextChoice", "LỰA CHỌN", 0f, 34f, 9.2f, InkGhost, barlow600, 256f, 1.4f, TextAlignmentOptions.TopRight);

            AddImage(cardRoot, "IconFrame", 86f, 72f + CardContentShiftY, 104f, 104f, iconFrame);
            Image icon = AddImage(cardRoot, "Icon", 90f, 76f + CardContentShiftY, 96f, 96f, null);
            icon.preserveAspect = true;

            TextMeshProUGUI category = AddText(cardRoot, "TextCategory", "LÕI ĐỘNG CƠ", 0f, 190f + CardContentShiftY, 9.0f, InkDim, barlow700, 276f, 1.4f, TextAlignmentOptions.Top);
            TextMeshProUGUI name = AddText(cardRoot, "TextName", "TRỌNG LỰC ĐỊA MẠCH", 14f, 220f + CardContentShiftY, 19f, InkBright, barlow900, 248f, 0.6f, TextAlignmentOptions.Top, 40f, true);
            TextMeshProUGUI role = AddText(cardRoot, "TextRole", "GIÁP / PHÒNG THỦ", 0f, 246f + CardContentShiftY, 9.6f, RolePurple, barlow700, 276f, 1.0f, TextAlignmentOptions.Top);
            TextMeshProUGUI stats = AddText(cardRoot, "TextStats", "+35% PHÒNG THỦ", 20f, 268f + CardContentShiftY, 13.5f, StatsGreen, barlow700, 236f, 0.2f, TextAlignmentOptions.Top, 104f);
            TextMeshProUGUI description = AddText(cardRoot, "TextDescription", "Mô tả hiệu ứng của phụ kiện.", 20f, 390f, 16f, InkDim, barlow600, 236f, 0.2f, TextAlignmentOptions.Top, 188f, true);
            description.overflowMode = TextOverflowModes.Ellipsis;
            TextMeshProUGUI footer = AddText(cardRoot, "TextFooter", "PHÂN RÃ  ›", 20f, 576f, 9.2f, InkFaint, barlow600, 236f, 1.0f, TextAlignmentOptions.TopRight);

            bool warn = index == 0;
            SetCardObject(card, "Root", cardRoot.gameObject, warn);
            SetCardObject(card, "Background", background, warn);
            SetCardObject(card, "Hover", hover, warn);
            SetCardObject(card, "SelectedGroup", selectedGroup.gameObject, warn);
            SetCardObject(card, "ChoiceLabel", choice, warn);
            SetCardObject(card, "Icon", icon, warn);
            SetCardObject(card, "Number", number, warn);
            SetCardObject(card, "Category", category, warn);
            SetCardObject(card, "Name", name, warn);
            SetCardObject(card, "Role", role, warn);
            SetCardObject(card, "Stats", stats, warn);
            SetCardObject(card, "Description", description, warn);
            SetCardObject(card, "Footer", footer, warn);
        }

        private static void SetCardObject(SerializedProperty card, string fieldName, Object value, bool warn)
        {
            SerializedProperty property = card.FindPropertyRelative(fieldName);
            if (property == null)
            {
                if (warn)
                {
                    Debug.LogError("[AccessorySalvageBuilder] CardView thiếu trường " + fieldName + ".");
                }

                return;
            }

            property.objectReferenceValue = value;
        }

        private static void SetObject(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError("[AccessorySalvageBuilder] Controller thiếu thuộc tính " + propertyName + ".");
                return;
            }

            property.objectReferenceValue = value;
        }

        private static void SetRect(SerializedObject serialized, string propertyName, Rect value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError("[AccessorySalvageBuilder] Controller thiếu thuộc tính " + propertyName + ".");
                return;
            }

            property.rectValue = value;
        }

        /// <summary>Tra kiểu controller theo tên; trả null nếu file controller chưa tồn tại.</summary>
        private static System.Type ResolveControllerType()
        {
            System.Type direct = System.Type.GetType(ControllerTypeName + ", " + ControllerAssemblyName);
            if (direct != null)
            {
                return direct;
            }

            System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                System.Type type = assemblies[i].GetType(ControllerTypeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
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
            float ascentPx = font != null ? font.faceInfo.ascentLine / font.faceInfo.pointSize * fontSize : 0f;
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
                Debug.LogError("[AccessorySalvageBuilder] Thiếu sprite " + fileName + ".png");
            }

            return sprite;
        }

        /// <summary>
        /// Sinh sprite panel bằng UISprites rồi nướng thành file .png trong Assets/Art/UI/Generated/Panels.
        /// Bắt buộc phải nướng: sprite tạo lúc chạy là object tạm, lưu scene xong sẽ thành null.
        /// </summary>
        private static Sprite PanelSprite(string assetName, int width, int height, int cut, int radius,
            Color top, Color bottom, Color border, float borderWidth, int cutCorner)
        {
            string path = PanelDir + "/" + assetName + ".png";
            Sprite runtime = UISprites.PanelShape(width, height, cut, radius, top, bottom, border, borderWidth, cutCorner);
            if (runtime != null && runtime.texture != null)
            {
                if (!Directory.Exists(PanelDir))
                {
                    Directory.CreateDirectory(PanelDir);
                }

                File.WriteAllBytes(path, runtime.texture.EncodeToPNG());
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
            if (asset == null)
            {
                Debug.LogWarning("[AccessorySalvageBuilder] Không nướng được sprite " + path +
                                 "; dùng sprite tạm (sẽ mất khi mở lại scene).");
                return runtime;
            }

            return asset;
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