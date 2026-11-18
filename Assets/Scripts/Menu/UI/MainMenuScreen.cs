using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using IronWasteland.Tanks;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Main Menu: 4 nút (Chọn màn chơi, Nhà xe, Cài đặt, Thoát) + thẻ xe tăng đang chọn.
    /// ESC trên màn này mở Cài đặt (đúng dòng chú thích trong mockup).
    /// </summary>
    public sealed class MainMenuScreen : MenuScreen
    {
        private TextMeshProUGUI m_CurrentUnit;
        private TextMeshProUGUI m_LevelNumber;
        private TextMeshProUGUI m_Name;
        private TextMeshProUGUI m_Subtitle;
        private TextMeshProUGUI m_ProgressPercent;
        private TextMeshProUGUI m_Footer;
        private TankPreviewSlot m_Preview;
        private Image m_ProgressFill;

        private readonly TextMeshProUGUI[] m_ChipLabels = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] m_StatValues = new TextMeshProUGUI[4];
        private readonly Image[] m_StatFills = new Image[4];

        protected override void Build()
        {
            Transform c = Content;
            UIKit.Brackets(c, UITheme.Alpha(UITheme.Orange, 0.75f), 60f, 3f, 28f);

            BuildTopBar(c);
            BuildTitleBlock(c);

            BuildMenuItem(0, UIIcons.Play, "CHỌN MÀN CHƠI", "CAMPAIGN · CHỌN ẢI & CHIẾN ĐẤU",
                () => Ctx.Screens.Show<StageSelectScreen>());
            BuildMenuItem(1, UIIcons.Tank, "NHÀ XE & NÂNG CẤP", "GARAGE · QUẢN LÝ XE TĂNG",
                () => Ctx.Screens.Show<GarageScreen>());
            BuildMenuItem(2, UIIcons.Gear, "CÀI ĐẶT", "SETTINGS · ÂM THANH & ĐIỀU KHIỂN",
                () => Ctx.Screens.Show<SettingsScreen>());
            BuildMenuItem(3, UIIcons.Exit, "THOÁT GAME", "QUIT · THOÁT TRÒ CHƠI", AskQuit);

            BuildUnitCard(c);
            BuildStatCards(c);

            m_Footer = UIKit.Label(c, "", 95, 1040, 1200, 20, 12, UITheme.TextFaint);
            UIKit.Label(c, "NHẤN ESC ĐỂ MỞ CÀI ĐẶT", 1225, 1040, 600, 20, 12, UITheme.TextFaint,
                TextAlignmentOptions.Right);
        }

        // ============================================================
        // DỰNG
        // ============================================================

        private void BuildTopBar(Transform c)
        {
            Image logo = UIKit.PanelAt(c, "LogoMark", 62, 34, 34, 34, UITheme.Orange);
            UIKit.PanelAt(logo.transform, "LogoHole", 9, 9, 16, 16, UITheme.Bg);

            UIKit.Label(c, "IRON WASTELAND", 110, 32, 400, 26, 22, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.Label(c, "UNIT COMMAND // v0.1.0-alpha", 110, 60, 400, 16, 12, UITheme.TextFaint);

            // Nút bánh răng góc phải trên (mockup): mở Cài đặt.
            Button gear = UIKit.Btn(c, "SettingsButton", 1768, 30, 46, 46, Looks.MenuItem,
                () => Ctx.Screens.Show<SettingsScreen>());
            Image gearIcon = UIKit.SpriteAt(gear.transform, "GearIcon", UIIcons.Gear, 11, 11, 24, 24, UITheme.TextDim);
            gear.GetComponent<UIButtonFx>().HoverChanged += over =>
                gearIcon.color = over ? UITheme.TextOnOrange : UITheme.TextDim;
        }

        private void BuildTitleBlock(Transform c)
        {
            UIKit.Label(c, "TOP-DOWN 2D TACTICAL TANK COMBAT", 97, 150, 700, 18, 13, UITheme.TextFaint);

            UIKit.Label(c, "IRON", 97, 172, 740, 150, 150, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Normal, false, true, true);
            UIKit.Label(c, "WASTELAND", 97, 306, 740, 150, 150, UITheme.Orange,
                TextAlignmentOptions.Left, FontStyles.Normal, false, true, true);

            UIKit.Label(c, "IRON WASTELAND UNIT // BUILD v0.1.0-alpha", 97, 462, 700, 16, 11, UITheme.TextFaint);
        }

        /// <summary>
        /// Nút menu chính. Mặc định nền tối; CHỈ khi rê chuột vào mới chuyển sang cam
        /// (nền, viền, chữ, icon đổi cùng lúc qua <see cref="UIButtonFx.HoverChanged"/>).
        /// </summary>
        private void BuildMenuItem(int index, Sprite icon, string title, string sub, UnityAction onClick)
        {
            float y = 500 + index * 118;
            Button b = UIKit.Btn(Content, "Menu_" + index, 97, y, 618, 90, Looks.MenuItem, onClick);

            Image box = UIKit.PanelAt(b.transform, "IconBox", 24, 21, 48, 48, UITheme.PanelDark);
            Image iconImg = UIKit.SpriteAt(box.transform, "Icon", icon, 10, 10, 28, 28, UITheme.TextDim);

            TextMeshProUGUI titleLabel = UIKit.Label(b.transform, title, 94, 14, 440, 38, 32, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold, false, true);
            TextMeshProUGUI subLabel = UIKit.Label(b.transform, sub, 94, 54, 470, 20, 13, UITheme.TextDim);
            TextMeshProUGUI chevron = UIKit.Label(b.transform, ">", 560, 20, 40, 50, 34, UITheme.TextDim,
                TextAlignmentOptions.Center, FontStyles.Bold);

            b.GetComponent<UIButtonFx>().HoverChanged += over =>
            {
                titleLabel.color = over ? UITheme.TextOnOrange : UITheme.Text;
                subLabel.color = over ? UITheme.Alpha(UITheme.TextOnOrange, 0.7f) : UITheme.TextDim;
                chevron.color = over ? UITheme.TextOnOrange : UITheme.TextDim;
                iconImg.color = over ? UITheme.TextOnOrange : UITheme.TextDim;
                box.color = over ? UITheme.Alpha(Color.black, 0.22f) : UITheme.PanelDark;
            };
        }

        private void BuildUnitCard(Transform c)
        {
            Image card = UIKit.PanelAt(c, "UnitCard", 883, 128, 962, 739, UITheme.PanelDark, UITheme.Border);
            UIKit.Brackets(card.transform, UITheme.Alpha(UITheme.Purple, 0.8f), 22f, 2f, 12f);

            UIKit.Label(card.transform, "XE TĂNG ĐANG CHỌN", 32, 24, 420, 20, 13, UITheme.Teal,
                TextAlignmentOptions.Left, FontStyles.Bold);
            m_CurrentUnit = UIKit.Label(card.transform, "", 32, 46, 560, 16, 11, UITheme.TextFaint);

            // Huy hiệu cấp độ (góc phải trên)
            Image badge = UIKit.PanelAt(card.transform, "LevelBadge", 767, 22, 170, 64, UITheme.Panel, UITheme.Teal);
            UIKit.Label(badge.transform, "CẤP ĐỘ", 14, 6, 100, 14, 10, UITheme.TextFaint);
            m_LevelNumber = UIKit.Label(badge.transform, "01", 14, 20, 80, 40, 34, UITheme.Teal,
                TextAlignmentOptions.Left, FontStyles.Normal, false, false, true);
            UIKit.Label(badge.transform, "/ 90", 90, 36, 60, 20, 16, UITheme.TextDim);

            m_Preview = TankPreviewSlot.Create(card.transform, "Portrait", 241, 84, 480, 470, Ctx.Preview, true);

            m_Name = UIKit.Label(card.transform, "", 32, 566, 640, 74, 68, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Normal, false, true, true);
            m_Subtitle = UIKit.Label(card.transform, "", 34, 642, 560, 26, 19, UITheme.TextDim);

            for (int i = 0; i < m_ChipLabels.Length; i++)
                m_ChipLabels[i] = UIKit.ChipAt(card.transform, "", 34 + i * 114, 682, 106, 28, UITheme.Purple);

            UIKit.Label(card.transform, "TIẾN ĐỘ CẤP", 707, 620, 140, 14, 10, UITheme.TextFaint);
            m_ProgressPercent = UIKit.Label(card.transform, "0%", 847, 616, 85, 18, 14, UITheme.Orange,
                TextAlignmentOptions.Right, FontStyles.Bold);
            m_ProgressFill = UIKit.BarAt(card.transform, 707, 642, 225, 6, UITheme.PanelLight, UITheme.Orange);
        }

        private void BuildStatCards(Transform c)
        {
            for (int i = 0; i < StatDisplay.MainMenuRows.Length; i++)
            {
                StatDisplay.Row row = StatDisplay.Rows[StatDisplay.MainMenuRows[i]];
                float x = 883 + i * 244.67f;

                Image card = UIKit.PanelAt(c, "Stat_" + row.Unit, x, 882, 228, 98, UITheme.PanelDark, UITheme.Border);
                UIKit.PanelAt(card.transform, "Accent", 0, 0, 3, 98, row.Color);

                UIKit.Label(card.transform, row.Label, 16, 10, 150, 16, 11, UITheme.TextDim);
                UIKit.Label(card.transform, row.Unit, 160, 10, 56, 16, 11, UITheme.TextFaint, TextAlignmentOptions.Right);
                m_StatValues[i] = UIKit.Label(card.transform, "0", 16, 30, 200, 52, 46, UITheme.Text,
                    TextAlignmentOptions.Left, FontStyles.Bold, false, true);
                m_StatFills[i] = UIKit.BarAt(card.transform, 16, 86, 196, 3, UITheme.PanelLight, row.Color);
            }
        }

        // ============================================================
        // HIỂN THỊ
        // ============================================================

        public override void OnShown()
        {
            base.OnShown();
            Refresh();
        }

        public override void OnHidden()
        {
            base.OnHidden();
            m_Preview.Hide();
        }

        protected override void OnProfileChanged() => Refresh();

        public override bool OnBack()
        {
            Ctx.Screens.Show<SettingsScreen>();
            return true;
        }

        private TankEntry ResolveTank()
        {
            TankEntry tank = Ctx.Tanks.Find(ProfileService.Current.selectedTankId);
            if (tank == null && Ctx.Tanks.tanks.Count > 0) tank = Ctx.Tanks.tanks[0];
            return tank;
        }

        private void Refresh()
        {
            TankEntry tank = ResolveTank();
            if (tank == null)
            {
                m_Preview.Show(null);
                m_Name.text = "CHƯA CÓ XE";
                m_Subtitle.text = "Hãy thêm xe vào TankCatalog.";
                return;
            }

            int level = ProfileService.GetLevel(tank.id);
            TankStats stats = TankStatsBridge.GetStats(tank.definition, level);

            m_CurrentUnit.text = "CURRENT UNIT // " + tank.displayName.ToUpperInvariant();
            m_LevelNumber.text = UIKit.Pad2(level);
            m_Name.text = tank.displayName.ToUpperInvariant();
            m_Subtitle.text = tank.subtitle;
            m_Preview.Show(tank);

            for (int i = 0; i < m_ChipLabels.Length; i++)
            {
                bool has = tank.tags != null && i < tank.tags.Length && !string.IsNullOrEmpty(tank.tags[i]);
                m_ChipLabels[i].transform.parent.gameObject.SetActive(has);
                if (has) m_ChipLabels[i].text = tank.tags[i].ToUpperInvariant();
            }

            float span = TankDefinition.MaxLevel - TankDefinition.MinLevel;
            float progress = span > 0f ? (level - TankDefinition.MinLevel) / span : 0f;
            m_ProgressPercent.text = Mathf.RoundToInt(progress * 100f) + "%";
            UIKit.SetBar(m_ProgressFill, progress);

            for (int i = 0; i < StatDisplay.MainMenuRows.Length; i++)
            {
                StatDisplay.Row row = StatDisplay.Rows[StatDisplay.MainMenuRows[i]];
                float v = StatDisplay.Value(row, stats);
                m_StatValues[i].text = StatDisplay.Format(row, v);
                UIKit.SetBar(m_StatFills[i], StatDisplay.Fill01(row, v));
            }

            int total = Ctx.Stages != null ? Ctx.Stages.stages.Count : 0;
            int cleared = ProfileService.ClearedCount(Ctx.Stages);
            m_Footer.text = "UNITY 6 / TOP-DOWN 2D SHOOTER / CO-OP 1-4 NGƯỜI / TIẾN ĐỘ CHIẾN DỊCH "
                            + UIKit.Pad2(cleared) + " / " + UIKit.Pad2(total) + " ẢI";
        }

        private void AskQuit()
        {
            Ctx.Screens.Get<ConfirmPopup>().Open(
                "THOÁT GAME", "Bạn có chắc muốn thoát game?", "THOÁT", true, QuitGame);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
