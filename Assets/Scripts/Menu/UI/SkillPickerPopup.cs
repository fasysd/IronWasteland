using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Bảng chọn kỹ năng tùy chọn (mockup 02b_GARAGE_SKILL_PICKER).
    /// Hai tab: Tùy chọn 1 / Tùy chọn 2. Bấm vào một thẻ là áp dụng ngay
    /// (lưu vào TankSave.optionSkill1 / optionSkill2 -> TankBrainContext).
    /// </summary>
    public sealed class SkillPickerPopup : MenuPopup
    {
        private string m_TankId;
        private int m_Tab;

        private TextMeshProUGUI m_Breadcrumb;
        private readonly Button[] m_Tabs = new Button[2];
        private RectTransform m_CardsRoot;

        public void Open(string tankId, int tab)
        {
            m_TankId = tankId;
            m_Tab = Mathf.Clamp(tab, 0, 1);
            Ctx.Screens.OpenPopup(this);
        }

        protected override void Build()
        {
            Image panel = UIKit.PanelAt(Content, "Panel", 340, 200, 1240, 680,
                UITheme.PanelDark, UITheme.BorderBright, 1.5f, true);
            UIKit.Brackets(panel.transform, UITheme.Alpha(UITheme.Orange, 0.8f), 24f, 2f, 10f);

            UIKit.PanelAt(panel.transform, "AccentBar", 22, 26, 4, 60, UITheme.Orange);
            m_Breadcrumb = UIKit.Label(panel.transform, "", 40, 22, 800, 18, 12, UITheme.TextFaint);
            UIKit.Label(panel.transform, "CHỌN KỸ NĂNG TÙY CHỌN", 40, 42, 800, 44, 34, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold, false, true);

            UIKit.TextBtn(panel.transform, "X", 1170, 22, 44, 44, Looks.Secondary, 20, Close);

            m_Tabs[0] = UIKit.TextBtn(panel.transform, "TÙY CHỌN 1", 38, 108, 260, 46, Looks.TabActive, 18, () => SetTab(0));
            m_Tabs[1] = UIKit.TextBtn(panel.transform, "TÙY CHỌN 2", 306, 108, 260, 46, Looks.TabIdle, 18, () => SetTab(1));

            m_CardsRoot = UIKit.NewRect("Cards", panel.transform);
            UIKit.Place(m_CardsRoot, 38, 176, 1164, 380);

            UIKit.PanelAt(panel.transform, "FooterLine", 38, 588, 1164, 1, UITheme.Border);
            UIKit.Label(panel.transform,
                "BẤM VÀO MỘT KỸ NĂNG ĐỂ ÁP DỤNG NGAY. BẠN CÓ THỂ ĐỔI LẠI BẤT CỨ LÚC NÀO TRONG NHÀ XE.",
                38, 606, 960, 40, 12, UITheme.TextFaint, TextAlignmentOptions.TopLeft, FontStyles.Normal, true);
            UIKit.TextBtn(panel.transform, "ĐÓNG", 1050, 604, 152, 46, Looks.Secondary, 18, Close);
        }

        public override void OnShown() => Refresh();

        private void SetTab(int tab)
        {
            m_Tab = tab;
            Refresh();
        }

        private void Refresh()
        {
            TankEntry tank = Ctx.Tanks.Find(m_TankId);
            if (tank == null) return;

            m_Breadcrumb.text = tank.displayName.ToUpperInvariant() + " / KỸ NĂNG TÙY CHỌN";

            for (int i = 0; i < 2; i++)
                UIKit.SetLook(m_Tabs[i], i == m_Tab ? Looks.TabActive : Looks.TabIdle, true);

            UIKit.ClearChildren(m_CardsRoot);

            int current = ProfileService.GetOption(m_TankId, m_Tab);
            SkillUIInfo[] options = tank.GetOptions(m_Tab);
            if (options == null) return;

            int slot = 0;
            for (int i = 0; i < options.Length; i++)
            {
                SkillUIInfo info = options[i];
                if (info == null || !info.IsValid) continue;

                int variant = i + 1; // OptionSkill bắt đầu từ 1
                BuildCard(slot, variant, info, variant == current);
                slot++;
            }
        }

        private void BuildCard(int slot, int variant, SkillUIInfo info, bool equipped)
        {
            const float w = 377f;
            const float h = 380f;
            float x = slot * (w + 16f);

            Button card = UIKit.Btn(m_CardsRoot, "Card_" + variant, x, 0, w, h,
                equipped ? Looks.CardActive : Looks.Card, () => OnPick(variant, info));
            Transform t = card.transform;

            UIKit.Label(t, UIKit.Pad2(variant), 16, 12, 40, 16, 12, UITheme.TextFaint);

            if (equipped)
            {
                TextMeshProUGUI badge = UIKit.ChipAt(t, "ĐANG DÙNG", w - 112, 10, 100, 22, UITheme.Orange, 11);
                badge.color = UITheme.TextOnOrange;
                UIKit.SetBorder(badge.transform.parent.GetComponent<Image>(), UITheme.Orange);
                badge.transform.parent.GetComponent<Image>().color = UITheme.Orange;
            }

            Image icon = UIKit.IconAt(t, "Icon", (w - 120f) * 0.5f, 40, 120, 120);
            UIKit.SetIcon(icon, info.icon, info.displayName);

            UIKit.Label(t, info.displayName.ToUpperInvariant(), 12, 176, w - 24, 34, 24, UITheme.Text,
                TextAlignmentOptions.Center, FontStyles.Bold, false, true);
            UIKit.Label(t, info.subtitle, 12, 212, w - 24, 18, 13, UITheme.TextDim, TextAlignmentOptions.Center);

            BuildStatBox(t, 16, 242, 166, "NĂNG LƯỢNG", info.energyText);
            BuildStatBox(t, w - 16 - 166, 242, 166, "HỒI CHIÊU", info.cooldownText);

            UIKit.Label(t, info.description, 18, 298, w - 36, 76, 14, UITheme.TextDim,
                TextAlignmentOptions.TopLeft, FontStyles.Normal, true);
        }

        private static void BuildStatBox(Transform parent, float x, float y, float w, string label, string value)
        {
            Image box = UIKit.PanelAt(parent, "Stat", x, y, w, 44, UITheme.PanelDark, UITheme.Border);
            UIKit.Label(box.transform, label, 10, 5, w - 20, 14, 10, UITheme.TextFaint);
            UIKit.Label(box.transform, string.IsNullOrEmpty(value) ? "-" : value, 10, 18, w - 20, 24, 18, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
        }

        private void OnPick(int variant, SkillUIInfo info)
        {
            if (ProfileService.GetOption(m_TankId, m_Tab) == variant) return;

            ProfileService.SetOption(m_TankId, m_Tab, variant);
            Ctx.Screens.Toast("ĐÃ CHỌN KỸ NĂNG: " + info.displayName.ToUpperInvariant(), true);
            Refresh();
        }
    }
}
