using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Gắn script này vào MỘT GameObject rỗng trong Scene "MainMenu", gán 3 asset dữ liệu rồi bấm Play.
    /// Nó tự: nạp cài đặt + hồ sơ, tạo EventSystem, dựng Canvas, đăng ký các màn hình và mở Main Menu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuBootstrap : MonoBehaviour
    {
        [Header("Dữ liệu (bắt buộc)")]
        [SerializeField] private TankCatalog tankCatalog;
        [SerializeField] private AccessoryCatalog accessoryCatalog;
        [SerializeField] private StageDatabase stageDatabase;

        [Header("Giao diện")]
        [Tooltip("Font CHỮ THƯỜNG theo mockup: Barlow Condensed (có đủ ký tự tiếng Việt). Để trống sẽ dùng font mặc định của TMP.")]
        [SerializeField] private TMP_FontAsset uiFont;

        [Tooltip("Font TIÊU ĐỀ theo mockup: Anton Regular (tiêu đề trang, tên xe lớn, số lớn, \"ẢI 1-1\"). Để trống sẽ dùng font chữ thường.")]
        [SerializeField] private TMP_FontAsset displayFont;

        [Tooltip("Ảnh nền toàn màn hình (tùy chọn).")]
        [SerializeField] private Sprite backgroundSprite;

        [SerializeField, Range(0f, 1f)] private float backgroundOpacity = 0.35f;

        [Header("Hồ sơ mới (chỉ áp dụng khi chưa có file lưu)")]
        [SerializeField] private ProfileDefaults profileDefaults = new ProfileDefaults();

        [Header("Dò lỗi")]
        [Tooltip("Hiện một dòng nhỏ màu vàng ở bảng chỉ số của Nhà xe: nguồn đọc chỉ số + chỉ số cấp 1/cấp 90. Tắt khi đã ổn.")]
        [SerializeField] private bool showStatsDebug = true;

        [Header("Co-op")]
        [Tooltip("Chưa có mạng LAN: cho phép bấm BẮT ĐẦU ở chế độ Co-op khi chỉ có một mình (để thử).")]
        [SerializeField] private bool allowCoopWithoutNetwork = true;

        private ScreenManager m_Screens;

        private void Awake()
        {
            if (!Validate())
            {
                enabled = false;
                return;
            }

            Time.timeScale = 1f;
            MenuLanding landing = GameSession.NextLanding;
            GameSession.NextLanding = MenuLanding.MainMenu;
            GameSession.Clear();
            GameSession.MenuSceneName = SceneManager.GetActiveScene().name;

            UITheme.Font = uiFont;
            UITheme.DisplayFont = displayFont;
            UIFontUtility.Warmup(uiFont);
            UIFontUtility.Warmup(displayFont);
            GameSettings.Load();
            ProfileService.Load(tankCatalog, profileDefaults);

            if (MenuServices.CoopLobby is OfflineCoopLobby offline)
                offline.AllowSoloStart = allowCoopWithoutNetwork;

            EnsureEventSystem();
            RectTransform canvasRoot = BuildCanvas();

            var ctx = new MenuContext
            {
                Tanks = tankCatalog,
                Accessories = accessoryCatalog,
                Stages = stageDatabase,
                CanvasRoot = canvasRoot,
                Defaults = profileDefaults,
                Preview = new GameObject("TankPreviewStage").AddComponent<TankPreviewStage>(),
                ShowStatsDebug = showStatsDebug,
            };

            m_Screens = gameObject.AddComponent<ScreenManager>();
            ctx.Screens = m_Screens;
            m_Screens.Init(ctx);

            // Màn hình trước, popup sau (thứ tự đăng ký = thứ tự vẽ).
            m_Screens.Register<MainMenuScreen>();
            m_Screens.Register<GarageScreen>();
            m_Screens.Register<StageSelectScreen>();
            m_Screens.Register<SettingsScreen>();
            m_Screens.Register<SkillPickerPopup>();
            m_Screens.Register<AccessoryPickerPopup>();
            m_Screens.Register<ConfirmPopup>();

            m_Screens.Show<MainMenuScreen>();
            if (landing == MenuLanding.StageSelect) m_Screens.Show<StageSelectScreen>(); // Back -> MainMenu
        }

        private bool Validate()
        {
            bool ok = true;

            if (tankCatalog == null) { Debug.LogError($"[{nameof(MenuBootstrap)}] Chưa gán TankCatalog.", this); ok = false; }
            if (accessoryCatalog == null) { Debug.LogError($"[{nameof(MenuBootstrap)}] Chưa gán AccessoryCatalog.", this); ok = false; }
            if (stageDatabase == null) { Debug.LogError($"[{nameof(MenuBootstrap)}] Chưa gán StageDatabase.", this); ok = false; }

            if (ok && tankCatalog.tanks.Count == 0)
                Debug.LogWarning($"[{nameof(MenuBootstrap)}] TankCatalog đang trống.", this);

            return ok;
        }

        // ============================================================
        // HẠ TẦNG UI
        // ============================================================

        private static void EnsureEventSystem()
        {
            EventSystem es = FindAnyObjectByType<EventSystem>();
            if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();

            // Dự án dùng Input System mới -> cần InputSystemUIInputModule (StandaloneInputModule sẽ báo lỗi).
            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                foreach (BaseInputModule module in es.GetComponents<BaseInputModule>())
                    Destroy(module);

                var uiModule = es.gameObject.AddComponent<InputSystemUIInputModule>();
                uiModule.AssignDefaultActions();
            }
        }

        private RectTransform BuildCanvas()
        {
            var go = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = 5; // UI

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            // Expand: canvas luôn >= 1920x1080 nên khung thiết kế 1920x1080 ở giữa không bao giờ bị cắt.
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var root = (RectTransform)go.transform;

            RectTransform bg = UIKit.NewRect("Background", root);
            UIKit.Stretch(bg);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = UITheme.Bg;
            bgImg.raycastTarget = false;

            if (backgroundSprite != null)
            {
                RectTransform art = UIKit.NewRect("BackgroundArt", root);
                UIKit.Stretch(art);
                var artImg = art.gameObject.AddComponent<Image>();
                artImg.sprite = backgroundSprite;
                artImg.color = new Color(1f, 1f, 1f, backgroundOpacity);
                artImg.raycastTarget = false;
            }

            return root;
        }
    }
}
