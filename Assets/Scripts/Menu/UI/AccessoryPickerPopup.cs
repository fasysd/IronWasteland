using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Bảng chọn phụ kiện trang bị (mockup "CHỌN PHỤ KIỆN TRANG BỊ").
    /// Chỉ liệt kê phụ kiện người chơi đang sở hữu và còn bản trống (không bị xe khác chiếm).
    /// Bấm thẻ để chọn, "XÁC NHẬN TRANG BỊ" để áp dụng, "THÁO PHỤ KIỆN" để gỡ.
    /// </summary>
    public sealed class AccessoryPickerPopup : MenuPopup
    {
        private string m_TankId;
        private int m_Pending = -1;

        private TextMeshProUGUI m_Breadcrumb;
        private TextMeshProUGUI m_Counter;
        private TextMeshProUGUI m_Empty;
        private RectTransform m_Content;
        private Button m_UnequipButton;
        private Button m_ConfirmButton;

        private readonly List<AccessoryEntry> m_Shown = new List<AccessoryEntry>();

        public void Open(string tankId)
        {
            m_TankId = tankId;
            m_Pending = ProfileService.GetTank(tankId).equipmentId;
            Ctx.Screens.OpenPopup(this);
        }

        protected override void Build()
        {
            Image panel = UIKit.PanelAt(Content, "Panel", 90, 140, 1740, 790,
                UITheme.PanelDark, UITheme.BorderBright, 1.5f, true);
            UIKit.Brackets(panel.transform, UITheme.Alpha(UITheme.Orange, 0.8f), 26f, 2f, 10f);

            UIKit.PanelAt(panel.transform, "AccentBar", 22, 26, 4, 70, UITheme.Orange);
            m_Breadcrumb = UIKit.Label(panel.transform, "", 40, 22, 900, 18, 12, UITheme.TextFaint);
            UIKit.Label(panel.transform, "CHỌN PHỤ KIỆN TRANG BỊ", 40, 42, 900, 48, 36, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold, false, true);
            UIKit.Label(panel.transform, "CHỈ HIỂN THỊ PHỤ KIỆN BẠN ĐANG SỞ HỮU VÀ CÒN BẢN TRỐNG", 40, 94, 900, 18, 12, UITheme.TextFaint);
            m_Counter = UIKit.Label(panel.transform, "", 1180, 94, 500, 18, 12, UITheme.TextDim, TextAlignmentOptions.Right);

            UIKit.TextBtn(panel.transform, "X", 1658, 22, 44, 44, Looks.Secondary, 20, Close);
            UIKit.PanelAt(panel.transform, "HeaderLine", 38, 126, 1664, 1, UITheme.Border);

            UIKit.ScrollAt(panel.transform, "Scroll", 38, 138, 1664, 540, true, 1664, out m_Content);

            m_Empty = UIKit.Label(panel.transform,
                "BẠN CHƯA CÓ PHỤ KIỆN NÀO.\nHOÀN THÀNH ẢI ĐỂ CÓ CƠ HỘI NHẬN PHỤ KIỆN RƠI RA.",
                38, 330, 1664, 80, 20, UITheme.TextFaint, TextAlignmentOptions.Center, FontStyles.Normal, true);

            UIKit.PanelAt(panel.transform, "FooterLine", 38, 692, 1664, 1, UITheme.Border);
            UIKit.Label(panel.transform, "CUỘN CHUỘT ĐỂ XEM THÊM · ESC ĐỂ ĐÓNG", 40, 722, 900, 18, 12, UITheme.TextFaint);

            m_UnequipButton = UIKit.TextBtn(panel.transform, "THÁO PHỤ KIỆN", 1032, 706, 210, 56, Looks.Danger, 17, OnUnequip);
            UIKit.TextBtn(panel.transform, "HỦY BỎ", 1262, 706, 170, 56, Looks.Secondary, 17, Close);
            m_ConfirmButton = UIKit.TextBtn(panel.transform, "XÁC NHẬN TRANG BỊ", 1452, 706, 250, 56, Looks.Primary, 17, OnConfirm);
        }

        public override void OnShown() => Refresh();

        // ============================================================
        // HIỂN THỊ
        // ============================================================

        private void Refresh()
        {
            TankEntry tank = Ctx.Tanks.Find(m_TankId);
            if (tank != null) m_Breadcrumb.text = tank.displayName.ToUpperInvariant() + " / PHỤ KIỆN / KHO ĐỒ";

            int equippedNow = ProfileService.GetTank(m_TankId).equipmentId;

            // Danh sách: phụ kiện đang gắn lên trước, sau đó theo độ hiếm giảm dần.
            m_Shown.Clear();
            int ownedKinds = 0;
            for (int i = 0; i < Ctx.Accessories.All.Count; i++)
            {
                AccessoryEntry a = Ctx.Accessories.All[i];
                if (a == null) continue;
                if (ProfileService.GetOwned(a.equipmentId) > 0) ownedKinds++;
                if (ProfileService.GetAvailable(a.equipmentId, m_TankId) > 0) m_Shown.Add(a);
            }

            m_Shown.Sort((a, b) =>
            {
                int ea = a.equipmentId == equippedNow ? 0 : 1;
                int eb = b.equipmentId == equippedNow ? 0 : 1;
                if (ea != eb) return ea.CompareTo(eb);
                int r = ((int)b.rarity).CompareTo((int)a.rarity);
                return r != 0 ? r : a.equipmentId.CompareTo(b.equipmentId);
            });

            m_Counter.text = "SỞ HỮU " + ownedKinds + " / " + Ctx.Accessories.All.Count;
            m_Empty.gameObject.SetActive(m_Shown.Count == 0);
            UIKit.SetEnabled(m_UnequipButton, equippedNow >= 0);
            UIKit.SetEnabled(m_ConfirmButton, m_Shown.Count > 0);

            UIKit.ClearChildren(m_Content);
            m_Content.sizeDelta = new Vector2(Mathf.Max(1664f, m_Shown.Count * 286f), 0f);

            for (int i = 0; i < m_Shown.Count; i++)
                BuildCard(i, m_Shown[i], equippedNow);
        }

        private void BuildCard(int index, AccessoryEntry acc, int equippedNow)
        {
            const float w = 270f;
            const float h = 520f;

            bool selected = acc.equipmentId == m_Pending;
            Button card = UIKit.Btn(m_Content, "Card_" + acc.equipmentId, index * 286f, 4, w, h,
                selected ? Looks.CardActive : Looks.Card, () => OnPick(acc.equipmentId));
            Transform t = card.transform;

            UIKit.Label(t, UIKit.Pad2(index + 1), 14, 12, 40, 16, 12, UITheme.TextFaint);
            UIKit.ChipAt(t, RarityLabel(acc.rarity), w - 98, 10, 84, 20, RarityColor(acc.rarity), 10);

            Image icon = UIKit.IconAt(t, "Icon", (w - 120f) * 0.5f, 44, 120, 120);
            UIKit.SetIcon(icon, acc.icon, acc.displayName);

            UIKit.Label(t, acc.displayName.ToUpperInvariant(), 10, 180, w - 20, 32, 22, UITheme.Text,
                TextAlignmentOptions.Center, FontStyles.Bold, false, true);

            int owned = ProfileService.GetOwned(acc.equipmentId);
            string type = string.IsNullOrEmpty(acc.typeLabel) ? "PHỤ KIỆN" : acc.typeLabel;
            if (owned > 1) type += " · x" + owned;
            UIKit.Label(t, type, 10, 214, w - 20, 18, 12, UITheme.TextDim, TextAlignmentOptions.Center);

            float y = 252f;
            if (acc.bonusLines != null)
            {
                for (int i = 0; i < acc.bonusLines.Length && i < 4; i++)
                {
                    UIKit.Label(t, acc.bonusLines[i], 18, y, w - 36, 22, 15, UITheme.Green);
                    y += 24f;
                }
            }

            UIKit.Label(t, acc.passiveText, 18, 352, w - 36, 110, 13, UITheme.TextDim,
                TextAlignmentOptions.TopLeft, FontStyles.Normal, true);

            if (acc.equipmentId == equippedNow)
                UIKit.ChipAt(t, "ĐANG DÙNG", 14, h - 38, 104, 26, UITheme.Orange, 11);
            if (selected)
                UIKit.ChipAt(t, "ĐÃ CHỌN", w - 14 - 104, h - 38, 104, 26, UITheme.Green, 11);
        }

        private static string RarityLabel(AccessoryRarity r)
        {
            switch (r)
            {
                case AccessoryRarity.Rare: return "HIẾM";
                case AccessoryRarity.Epic: return "SỬ THI";
                case AccessoryRarity.Legendary: return "HUYỀN THOẠI";
                default: return "THƯỜNG";
            }
        }

        private static Color RarityColor(AccessoryRarity r)
        {
            switch (r)
            {
                case AccessoryRarity.Rare: return UITheme.Blue;
                case AccessoryRarity.Epic: return UITheme.Purple;
                case AccessoryRarity.Legendary: return UITheme.Gold;
                default: return UITheme.TextDim;
            }
        }

        // ============================================================
        // HÀNH ĐỘNG
        // ============================================================

        private void OnPick(int equipmentId)
        {
            m_Pending = equipmentId;
            Refresh();
        }

        private void OnConfirm()
        {
            int equippedNow = ProfileService.GetTank(m_TankId).equipmentId;
            if (m_Pending < 0 || m_Pending == equippedNow)
            {
                Close();
                return;
            }

            if (ProfileService.EquipAccessory(m_TankId, m_Pending))
            {
                AccessoryEntry acc = Ctx.Accessories.Find(m_Pending);
                Ctx.Screens.Toast("ĐÃ TRANG BỊ: " + (acc != null ? acc.displayName.ToUpperInvariant() : "#" + m_Pending), true);
                Close();
            }
            else
            {
                Ctx.Screens.Toast("PHỤ KIỆN NÀY ĐANG ĐƯỢC XE KHÁC SỬ DỤNG.");
                Refresh();
            }
        }

        private void OnUnequip()
        {
            ProfileService.UnequipAccessory(m_TankId);
            Ctx.Screens.Toast("ĐÃ THÁO PHỤ KIỆN.", true);
            Close();
        }
    }
}
