using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IronWasteland.Menu
{
    /// <summary>Dữ liệu và dịch vụ dùng chung cho mọi màn hình Menu.</summary>
    public sealed class MenuContext
    {
        public TankCatalog Tanks;
        public AccessoryCatalog Accessories;
        public StageDatabase Stages;
        public ScreenManager Screens;
        public RectTransform CanvasRoot;
        public ProfileDefaults Defaults;
        public TankPreviewStage Preview;
        public bool ShowStatsDebug;
    }

    /// <summary>
    /// Nền của mọi màn hình / popup.
    /// - <see cref="Root"/>: phủ kín Canvas (popup có thêm lớp tối phía sau).
    /// - <see cref="Content"/>: khung 1920x1080 đặt giữa Root; mọi tọa độ trong code tính theo khung này.
    /// </summary>
    public abstract class MenuView : MonoBehaviour
    {
        public MenuContext Ctx { get; private set; }
        public RectTransform Root { get; private set; }
        public RectTransform Content { get; private set; }

        /// <summary>True: có lớp tối + chặn bấm xuyên (popup).</summary>
        protected virtual bool UseDim => false;

        public bool IsPopup => UseDim;

        /// <summary>Do ScreenManager gọi đúng một lần ngay sau khi tạo.</summary>
        public void Initialize(MenuContext ctx)
        {
            Ctx = ctx;
            Root = (RectTransform)transform;
            UIKit.Stretch(Root);

            if (UseDim)
            {
                RectTransform dim = UIKit.NewRect("Dim", Root);
                UIKit.Stretch(dim);
                var dimImg = dim.gameObject.AddComponent<Image>();
                dimImg.color = new Color(0f, 0f, 0f, 0.78f);
                dimImg.raycastTarget = true;
            }

            Content = UIKit.NewRect("Content", Root);
            Content.anchorMin = new Vector2(0.5f, 0.5f);
            Content.anchorMax = new Vector2(0.5f, 0.5f);
            Content.pivot = new Vector2(0.5f, 0.5f);
            Content.sizeDelta = new Vector2(1920f, 1080f);
            Content.anchoredPosition = Vector2.zero;

            Build();
            gameObject.SetActive(false);
        }

        /// <summary>Dựng toàn bộ phần tử UI (chạy một lần).</summary>
        protected abstract void Build();

        /// <summary>Gọi mỗi lần màn hình / popup được hiện.</summary>
        public virtual void OnShown() { }

        /// <summary>Gọi mỗi lần bị ẩn.</summary>
        public virtual void OnHidden() { }

        /// <summary>Nhấn ESC. Trả về true nếu đã tự xử lý.</summary>
        public virtual bool OnBack() => false;
    }

    /// <summary>Màn hình đầy đủ (Main Menu, Nhà xe, Chọn ải, Cài đặt).</summary>
    public abstract class MenuScreen : MenuView
    {
        private TextMeshProUGUI m_GemLabel;

        public override void OnShown()
        {
            ProfileService.Changed += HandleProfileChanged;
            RefreshGems();
        }

        public override void OnHidden()
        {
            ProfileService.Changed -= HandleProfileChanged;
        }

        /// <summary>Gỡ đăng ký khi bị hủy (đổi Scene, thoát Play) để event static không giữ màn hình đã chết.</summary>
        protected virtual void OnDestroy()
        {
            ProfileService.Changed -= HandleProfileChanged;
        }

        private void HandleProfileChanged()
        {
            RefreshGems();
            OnProfileChanged();
        }

        /// <summary>Hồ sơ người chơi vừa đổi (Ngọc, cấp xe, phụ kiện...).</summary>
        protected virtual void OnProfileChanged() { }

        // ---------- Phần dùng chung: tiêu đề, chip Ngọc, chân trang ----------

        protected void BuildHeader(string breadcrumb, string title, bool showBack = true, bool showGems = true)
        {
            UIKit.Brackets(Content, UITheme.Alpha(UITheme.Orange, 0.75f), 60f, 3f, 28f);

            if (showBack)
            {
                UIKit.TextBtn(Content, "< QUAY LẠI", 95, 92, 170, 54, Looks.Secondary, 20,
                    () => Ctx.Screens.Back());
            }

            UIKit.Label(Content, breadcrumb, 295, 84, 900, 22, 14, UITheme.TextFaint);
            UIKit.Label(Content, title, 295, 104, 1100, 64, 50, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Normal, false, true, true);
            UIKit.PanelAt(Content, "TitleLine", 295, 172, 700, 2, UITheme.Alpha(UITheme.Orange, 0.45f));

            if (showGems) BuildGemChip();
        }

        private void BuildGemChip()
        {
            Image chip = UIKit.PanelAt(Content, "GemChip", 1625, 86, 200, 60, UITheme.PanelDark, UITheme.Border);

            Image dot = UIKit.PanelAt(chip.transform, "GemIcon", 14, 18, 24, 24, UITheme.Purple);
            dot.sprite = UIKit.CircleSprite;

            m_GemLabel = UIKit.Label(chip.transform, "0", 50, 6, 140, 32, 28, UITheme.Gold,
                TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.Label(chip.transform, "NGỌC MA PHÁP", 50, 38, 140, 16, 11, UITheme.TextDim);
        }

        protected void RefreshGems()
        {
            if (m_GemLabel != null) m_GemLabel.text = ProfileService.Gems.ToString();
        }

        protected void BuildFooter(string left, string right)
        {
            UIKit.Label(Content, left, 95, 1040, 1200, 20, 12, UITheme.TextFaint);
            UIKit.Label(Content, right, 1225, 1040, 600, 20, 12, UITheme.TextFaint, TextAlignmentOptions.Right);
        }
    }

    /// <summary>Hộp thoại phủ lên màn hình hiện tại (chọn kỹ năng, chọn phụ kiện, xác nhận).</summary>
    public abstract class MenuPopup : MenuView
    {
        protected override bool UseDim => true;

        public void Close() => Ctx.Screens.ClosePopup(this);

        public override bool OnBack()
        {
            Close();
            return true;
        }
    }
}
