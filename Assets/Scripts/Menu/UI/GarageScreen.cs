using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IronWasteland.Tanks;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Nhà xe: danh sách xe, xem chỉ số theo cấp, nâng cấp bằng Ngọc ma pháp,
    /// chọn 2 kỹ năng tùy chọn, đổi / phân rã phụ kiện, rồi "XÁC NHẬN RA TRẬN" -> Chọn ải.
    /// Xe đang xem cũng chính là xe đang chọn ra trận (ProfileService.Current.selectedTankId).
    /// </summary>
    public sealed class GarageScreen : MenuScreen
    {
        // ---------- Phần tử UI cần cập nhật ----------

        private sealed class TankRow
        {
            public string TankId;
            public Button Button;
            public TankPreviewSlot Icon;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Role;
            public TextMeshProUGUI Level;
            public TextMeshProUGUI Tag;      // "ĐANG CHỌN" hoặc "ẢI x-y" khi bị khóa
            public Image Fill;
        }

        private sealed class SkillCard
        {
            public Image Icon;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Sub;
            public TextMeshProUGUI Key;
        }

        private readonly List<TankRow> m_Rows = new List<TankRow>();
        private readonly SkillCard[] m_Skills = new SkillCard[4];

        private TextMeshProUGUI m_ListCount;

        private TankPreviewSlot m_Preview;
        private TextMeshProUGUI m_CurrentUnit;
        private TextMeshProUGUI m_BadgeLevel;
        private TextMeshProUGUI m_PreviewName;
        private TextMeshProUGUI m_PreviewSub;

        private TextMeshProUGUI m_LevelBig;
        private TextMeshProUGUI m_LevelNext;
        private Image m_LevelFill;
        private Button m_UpgradeButton;
        private TextMeshProUGUI m_UpgradeLabel;
        private TextMeshProUGUI m_UpgradeCost;
        private Image m_UpgradeDot;

        private readonly TextMeshProUGUI[] m_StatValue = new TextMeshProUGUI[8];
        private readonly TextMeshProUGUI[] m_StatDelta = new TextMeshProUGUI[8];
        private readonly Image[] m_StatFill = new Image[8];
        private TextMeshProUGUI m_StatsDebug;

        private Image m_AccIcon;
        private TextMeshProUGUI m_AccName;
        private TextMeshProUGUI m_AccBonus;
        private TextMeshProUGUI m_AccPassive;
        private TextMeshProUGUI m_AccEmpty;
        private Button m_AccSwap;
        private Button m_AccDismantle;

        private Button m_DeployButton;

        // ============================================================
        // DỰNG
        // ============================================================

        protected override void Build()
        {
            BuildHeader("HANGAR / UNIT CONFIGURATION", "QUẢN LÝ XE TĂNG & PHỤ KIỆN");
            BuildTankList();
            BuildPreview();
            BuildLevelBar();
            BuildSkills();
            BuildStats();
            BuildAccessory();
            BuildDeploy();
            BuildFooter("UNITY 6 / TOP-DOWN 2D SHOOTER / CO-OP 1-4 / POST-APOCALYPTIC SCI-FI", "NHẤN ESC ĐỂ QUAY LẠI");
        }

        private void BuildTankList()
        {
            UIKit.Label(Content, "DANH SÁCH XE TĂNG", 95, 200, 260, 22, 16, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            m_ListCount = UIKit.Label(Content, "", 330, 202, 135, 20, 13, UITheme.TextFaint, TextAlignmentOptions.Right);

            const float rowH = 118f;
            const float pitch = 128f;
            int count = Ctx.Tanks.tanks.Count;

            UIKit.ScrollAt(Content, "TankList", 91, 230, 378, 770, false, count * pitch + 8f, out RectTransform content);

            for (int i = 0; i < count; i++)
            {
                TankEntry tank = Ctx.Tanks.tanks[i];
                if (tank == null) continue;

                var row = new TankRow { TankId = tank.id };
                string id = tank.id;

                row.Button = UIKit.Btn(content, "Row_" + id, 4, 4 + i * pitch, 370, rowH, Looks.Card, () => OnTankClicked(id));
                Transform t = row.Button.transform;

                row.Icon = TankPreviewSlot.Create(t, "Icon", 14, 21, 76, 76, Ctx.Preview, false);
                row.Name = UIKit.Label(t, tank.displayName.ToUpperInvariant(), 104, 14, 250, 34, 28, UITheme.Text,
                    TextAlignmentOptions.Left, FontStyles.Bold, false, true);
                row.Role = UIKit.Label(t, tank.roleLine, 104, 50, 250, 20, 14, UITheme.Teal);
                row.Fill = UIKit.BarAt(t, 104, 80, 160, 5, UITheme.PanelLight, UITheme.Purple);
                row.Level = UIKit.Label(t, "", 104, 90, 160, 18, 13, UITheme.TextDim);
                row.Tag = UIKit.Label(t, "", 232, 90, 126, 18, 12, UITheme.Orange,
                    TextAlignmentOptions.Right, FontStyles.Bold);

                m_Rows.Add(row);
            }
        }

        private void BuildPreview()
        {
            Image panel = UIKit.PanelAt(Content, "Preview", 481, 195, 782, 459, UITheme.PanelDark, UITheme.Border);
            UIKit.Brackets(panel.transform, UITheme.Alpha(UITheme.Purple, 0.8f), 22f, 2f, 12f);

            UIKit.Label(panel.transform, "XE TĂNG ĐANG CHỌN", 24, 20, 400, 18, 12, UITheme.Teal,
                TextAlignmentOptions.Left, FontStyles.Bold);
            m_CurrentUnit = UIKit.Label(panel.transform, "", 24, 40, 500, 16, 11, UITheme.TextFaint);

            Image badge = UIKit.PanelAt(panel.transform, "LevelBadge", 630, 16, 128, 62, UITheme.Panel, UITheme.Teal);
            UIKit.Label(badge.transform, "CẤP ĐỘ", 12, 6, 100, 14, 10, UITheme.TextFaint);
            m_BadgeLevel = UIKit.Label(badge.transform, "01", 12, 20, 60, 40, 34, UITheme.Teal,
                TextAlignmentOptions.Left, FontStyles.Normal, false, false, true);
            UIKit.Label(badge.transform, "/ 90", 70, 36, 50, 20, 15, UITheme.TextDim);

            m_Preview = TankPreviewSlot.Create(panel.transform, "Portrait", 231, 40, 320, 330, Ctx.Preview, true);

            m_PreviewName = UIKit.Label(panel.transform, "", 0, 372, 782, 56, 52, UITheme.Text,
                TextAlignmentOptions.Center, FontStyles.Normal, false, true, true);
            UIKit.Stretch(m_PreviewName.rectTransform, 0, 0, 0, 0);
            UIKit.Place(m_PreviewName.rectTransform, 24, 372, 734, 56);
            m_PreviewSub = UIKit.Label(panel.transform, "", 24, 428, 734, 24, 18, UITheme.TextDim, TextAlignmentOptions.Center);
        }

        private void BuildLevelBar()
        {
            Image panel = UIKit.PanelAt(Content, "LevelBar", 481, 662, 782, 72, UITheme.PanelDark, UITheme.Border);

            UIKit.Label(panel.transform, "CẤP ĐỘ HIỆN TẠI", 18, 8, 180, 14, 11, UITheme.TextDim);
            m_LevelBig = UIKit.Label(panel.transform, "01", 18, 22, 60, 44, 40, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Normal, false, false, true);
            UIKit.Label(panel.transform, "/ 90", 78, 40, 50, 22, 18, UITheme.TextDim);
            m_LevelNext = UIKit.Label(panel.transform, "", 130, 40, 110, 22, 15, UITheme.Orange,
                TextAlignmentOptions.Left, FontStyles.Bold);
            m_LevelFill = UIKit.BarAt(panel.transform, 250, 34, 310, 8, UITheme.PanelLight, UITheme.Orange);

            m_UpgradeButton = UIKit.Btn(panel.transform, "Upgrade", 572, 8, 198, 56, Looks.Primary, OnUpgradeClicked);
            m_UpgradeLabel = UIKit.Label(m_UpgradeButton.transform, "NÂNG CẤP", 16, 0, 130, 56, 20, UITheme.TextOnOrange,
                TextAlignmentOptions.Left, FontStyles.Bold, false, true);
            m_UpgradeDot = UIKit.PanelAt(m_UpgradeButton.transform, "GemIcon", 140, 18, 20, 20, UITheme.Purple);
            m_UpgradeDot.sprite = UIKit.CircleSprite;
            m_UpgradeCost = UIKit.Label(m_UpgradeButton.transform, "", 162, 0, 30, 56, 22, UITheme.TextOnOrange,
                TextAlignmentOptions.Left, FontStyles.Bold);
        }

        private void BuildSkills()
        {
            UIKit.Label(Content, "KỸ NĂNG XE TĂNG", 481, 746, 300, 20, 16, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.Label(Content, "TANK SKILLS // 4 SLOTS", 1000, 749, 263, 16, 11, UITheme.TextFaint, TextAlignmentOptions.Right);

            string[] slotNames = { "KỸ NĂNG THƯỜNG", "KỸ NĂNG CHUYÊN BIỆT", "TÙY CHỌN 1", "TÙY CHỌN 2" };

            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                float x = 481 + i * 198f;
                bool clickable = i >= 2;

                Transform root;
                if (clickable)
                    root = UIKit.Btn(Content, "Skill_" + i, x, 774, 188, 240, Looks.Card, () => OnOptionSlotClicked(slot - 2)).transform;
                else
                    root = UIKit.PanelAt(Content, "Skill_" + i, x, 774, 188, 240, UITheme.Panel, UITheme.Border).transform;

                UIKit.PanelAt(root, "TopStrip", 0, 0, 188, 3, i == 0 ? UITheme.Orange : UITheme.Purple);
                UIKit.Label(root, UIKit.Pad2(i + 1), 10, 10, 30, 14, 11, UITheme.TextFaint);

                var card = new SkillCard();
                Image keyBox = UIKit.PanelAt(root, "KeyBox", 146, 8, 32, 28, UITheme.PanelLight, UITheme.BorderBright);
                card.Key = UIKit.Label(keyBox.transform, "", 0, 0, 32, 28, 15, UITheme.Text,
                    TextAlignmentOptions.Center, FontStyles.Bold, false, true);
                UIKit.Stretch(card.Key.rectTransform);

                UIKit.Label(root, slotNames[i], 10, 36, 168, 16, 10, UITheme.TextDim);
                card.Icon = UIKit.IconAt(root, "Icon", 44, 60, 100, 100);
                card.Name = UIKit.Label(root, "", 6, 170, 176, 30, 21, UITheme.Text,
                    TextAlignmentOptions.Center, FontStyles.Bold, false, true);
                card.Sub = UIKit.Label(root, "", 6, 204, 176, 20, 12, i >= 2 ? UITheme.Purple : UITheme.TextDim,
                    TextAlignmentOptions.Center);
                m_Skills[i] = card;
            }
        }

        private void BuildStats()
        {
            Image panel = UIKit.PanelAt(Content, "Stats", 1282, 195, 545, 459, UITheme.PanelDark, UITheme.Border);

            UIKit.Label(panel.transform, "CHỈ SỐ CHIẾN ĐẤU", 24, 20, 320, 22, 18, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.Label(panel.transform, "08 STATS", 400, 24, 120, 16, 11, UITheme.TextFaint, TextAlignmentOptions.Right);
            m_StatsDebug = UIKit.Label(panel.transform, "", 24, 44, 497, 16, 10, UITheme.Gold,
                TextAlignmentOptions.Left, FontStyles.Normal, false, true);

            for (int i = 0; i < StatDisplay.Rows.Length; i++)
            {
                StatDisplay.Row row = StatDisplay.Rows[i];
                float y = 64 + i * 48f;

                UIKit.Label(panel.transform, row.Label, 24, y + 4, 250, 20, 14, UITheme.TextDim);
                m_StatDelta[i] = UIKit.Label(panel.transform, "", 280, y + 4, 150, 22, 14, UITheme.Green,
                    TextAlignmentOptions.Right, FontStyles.Bold);
                m_StatValue[i] = UIKit.Label(panel.transform, "0", 424, y - 6, 78, 34, 28, UITheme.Text,
                    TextAlignmentOptions.Right, FontStyles.Bold, false, true);
                UIKit.Label(panel.transform, row.Unit, 506, y + 6, 32, 16, 11, UITheme.TextFaint);
                m_StatFill[i] = UIKit.BarAt(panel.transform, 24, y + 32, 497, 4, UITheme.PanelLight, row.Color);
            }
        }

        private void BuildAccessory()
        {
            Image panel = UIKit.PanelAt(Content, "Accessory", 1282, 674, 545, 199, UITheme.PanelDark, UITheme.Border);

            UIKit.Label(panel.transform, "PHỤ KIỆN TRANG BỊ", 24, 14, 320, 20, 16, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.Label(panel.transform, "1 / 1 DUY NHẤT", 360, 16, 160, 16, 11, UITheme.TextFaint, TextAlignmentOptions.Right);

            m_AccIcon = UIKit.IconAt(panel.transform, "Icon", 24, 46, 84, 84);
            m_AccName = UIKit.Label(panel.transform, "", 124, 46, 400, 32, 24, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold, false, true);
            m_AccBonus = UIKit.Label(panel.transform, "", 124, 82, 400, 22, 15, UITheme.Green);
            m_AccPassive = UIKit.Label(panel.transform, "", 124, 106, 400, 40, 13, UITheme.TextDim,
                TextAlignmentOptions.TopLeft, FontStyles.Normal, true);
            m_AccEmpty = UIKit.Label(panel.transform, "CHƯA TRANG BỊ PHỤ KIỆN", 124, 70, 400, 40, 18, UITheme.TextFaint,
                TextAlignmentOptions.Left, FontStyles.Bold);

            m_AccSwap = UIKit.TextBtn(panel.transform, "ĐỔI PHỤ KIỆN", 24, 152, 260, 36, Looks.Secondary, 15, OnAccessorySwap);
            m_AccDismantle = UIKit.TextBtn(panel.transform, "PHÂN RÃ", 296, 152, 225, 36, Looks.Danger, 15, OnDismantle);
        }

        private void BuildDeploy()
        {
            m_DeployButton = UIKit.Btn(Content, "Deploy", 1282, 893, 545, 86, Looks.Primary, OnDeploy);
            Transform t = m_DeployButton.transform;
            UIKit.Label(t, "XÁC NHẬN RA TRẬN", 28, 12, 420, 40, 32, UITheme.TextOnOrange,
                TextAlignmentOptions.Left, FontStyles.Bold, false, true);
            UIKit.Label(t, "DEPLOY / SELECT MISSION", 28, 54, 420, 18, 12, UITheme.Alpha(UITheme.TextOnOrange, 0.7f));
            UIKit.Label(t, ">", 470, 18, 50, 50, 44, UITheme.TextOnOrange, TextAlignmentOptions.Center, FontStyles.Bold);
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

        private TankEntry CurrentTank
        {
            get
            {
                TankEntry tank = Ctx.Tanks.Find(ProfileService.Current.selectedTankId);
                if (tank == null && Ctx.Tanks.tanks.Count > 0) tank = Ctx.Tanks.tanks[0];
                return tank;
            }
        }

        private void Refresh()
        {
            RefreshList();

            TankEntry tank = CurrentTank;
            if (tank == null) return;

            TankSave save = ProfileService.GetTank(tank.id);
            int level = save.level;
            bool isMax = level >= TankDefinition.MaxLevel;

            TankStats stats = TankStatsBridge.GetStats(tank.definition, level);
            TankStats next = isMax ? stats : TankStatsBridge.GetStats(tank.definition, level + 1);

            // --- Xem trước ---
            m_CurrentUnit.text = "CURRENT UNIT // " + tank.displayName.ToUpperInvariant();
            m_BadgeLevel.text = UIKit.Pad2(level);
            m_PreviewName.text = tank.displayName.ToUpperInvariant();
            m_PreviewSub.text = tank.subtitle;
            m_Preview.Show(tank);

            // --- Cấp độ + nâng cấp ---
            m_LevelBig.text = UIKit.Pad2(level);
            m_LevelNext.text = isMax ? "TỐI ĐA" : "+ CẤP " + (level + 1);
            UIKit.SetBar(m_LevelFill, (level - TankDefinition.MinLevel) / (float)(TankDefinition.MaxLevel - TankDefinition.MinLevel));

            int cost = ProfileService.GetUpgradeCost(tank.id);
            UIKit.SetEnabled(m_UpgradeButton, !isMax);
            m_UpgradeCost.text = isMax ? "" : cost.ToString();
            m_UpgradeDot.gameObject.SetActive(!isMax);
            m_UpgradeLabel.text = isMax ? "ĐÃ TỐI ĐA" : "NÂNG CẤP";

            // --- Chỉ số ---
            m_StatsDebug.text = Ctx.ShowStatsDebug ? TankStatsBridge.GetReport(tank.definition) : "";
            for (int i = 0; i < StatDisplay.Rows.Length; i++)
            {
                StatDisplay.Row row = StatDisplay.Rows[i];
                float v = StatDisplay.Value(row, stats);
                float d = StatDisplay.Value(row, next) - v;

                m_StatValue[i].text = StatDisplay.Format(row, v);
                m_StatDelta[i].text = isMax ? "" : StatDisplay.FormatDelta(row, d);
                UIKit.SetBar(m_StatFill[i], StatDisplay.Fill01(row, v));
            }

            // --- Kỹ năng ---
            ApplySkill(0, tank.normalSkill, tank.normalSkill.costLabel, GameAction.BasicAttack);
            ApplySkill(1, tank.specialSkill, tank.specialSkill.costLabel, GameAction.SpecialSkill);
            ApplySkill(2, tank.GetOption(0, save.optionSkill1), CountValid(tank.option1) + " LỰA CHỌN", GameAction.Option1);
            ApplySkill(3, tank.GetOption(1, save.optionSkill2), CountValid(tank.option2) + " LỰA CHỌN", GameAction.Option2);

            // --- Phụ kiện ---
            RefreshAccessory(save);
        }

        private void RefreshList()
        {
            string selectedId = ProfileService.Current.selectedTankId;
            m_ListCount.text = Ctx.Tanks.tanks.Count.ToString("00") + " XE TĂNG";

            for (int i = 0; i < m_Rows.Count; i++)
            {
                TankRow row = m_Rows[i];
                TankEntry tank = Ctx.Tanks.Find(row.TankId);
                bool unlocked = ProfileService.IsTankUnlocked(tank);
                bool selected = unlocked && row.TankId == selectedId;
                int level = ProfileService.GetLevel(row.TankId);

                row.Icon.Show(tank);
                UIKit.SetLook(row.Button, !unlocked ? Looks.CardLocked : (selected ? Looks.CardSelected : Looks.Card));
                row.Name.color = unlocked ? UITheme.Text : UITheme.TextFaint;
                row.Level.text = "CẤP " + UIKit.Pad2(level) + " / " + TankDefinition.MaxLevel;
                UIKit.SetBar(row.Fill, (level - TankDefinition.MinLevel) / (float)(TankDefinition.MaxLevel - TankDefinition.MinLevel));

                if (!unlocked) row.Tag.text = "ẢI " + tank.unlockStageId;
                else row.Tag.text = selected ? "ĐANG CHỌN" : "";
            }
        }

        private void ApplySkill(int index, SkillUIInfo info, string sub, GameAction action)
        {
            SkillCard card = m_Skills[index];
            UIKit.SetIcon(card.Icon, info.icon, info.displayName);
            card.Name.text = string.IsNullOrEmpty(info.displayName) ? "—" : info.displayName;
            card.Sub.text = (sub ?? "").ToUpperInvariant();
            card.Key.text = GameSettings.KeyLabel(GameSettings.GetKey(action));
        }

        private static int CountValid(SkillUIInfo[] options)
        {
            int n = 0;
            if (options == null) return 0;
            for (int i = 0; i < options.Length; i++)
                if (options[i] != null && options[i].IsValid) n++;
            return n;
        }

        private void RefreshAccessory(TankSave save)
        {
            bool has = save.equipmentId >= 0;
            AccessoryEntry acc = has ? Ctx.Accessories.Find(save.equipmentId) : null;

            m_AccIcon.gameObject.SetActive(has);
            m_AccName.gameObject.SetActive(has);
            m_AccBonus.gameObject.SetActive(has);
            m_AccPassive.gameObject.SetActive(has);
            m_AccEmpty.gameObject.SetActive(!has);
            m_AccDismantle.gameObject.SetActive(has);

            if (has)
            {
                UIKit.SetIcon(m_AccIcon, acc != null ? acc.icon : null, acc != null ? acc.displayName : "?");
                m_AccName.text = acc != null ? acc.displayName : "Phụ kiện #" + save.equipmentId;
                m_AccBonus.text = acc != null && acc.bonusLines != null ? string.Join(", ", acc.bonusLines) : "";
                m_AccPassive.text = acc != null ? acc.passiveText : "";
            }

            var swapLabel = m_AccSwap.GetComponentInChildren<TextMeshProUGUI>();
            if (swapLabel != null) swapLabel.text = has ? "ĐỔI PHỤ KIỆN" : "TRANG BỊ PHỤ KIỆN";
        }

        // ============================================================
        // HÀNH ĐỘNG
        // ============================================================

        private void OnTankClicked(string id)
        {
            TankEntry tank = Ctx.Tanks.Find(id);
            if (!ProfileService.IsTankUnlocked(tank))
            {
                StageEntry stage = Ctx.Stages != null ? Ctx.Stages.Find(tank.unlockStageId) : null;
                string where = stage != null ? stage.Label : "ẢI " + tank.unlockStageId;
                Ctx.Screens.Toast("HOÀN THÀNH " + where + " ĐỂ MỞ KHÓA " + tank.displayName.ToUpperInvariant() + ".");
                return;
            }

            ProfileService.SelectTank(id); // -> Changed -> Refresh
        }

        private void OnUpgradeClicked()
        {
            TankEntry tank = CurrentTank;
            if (tank == null) return;

            int cost = ProfileService.GetUpgradeCost(tank.id);
            switch (ProfileService.TryUpgrade(tank.id))
            {
                case UpgradeResult.Ok:
                    Ctx.Screens.Toast("ĐÃ NÂNG CẤP " + tank.displayName.ToUpperInvariant() +
                                      " LÊN CẤP " + ProfileService.GetLevel(tank.id), true);
                    break;
                case UpgradeResult.NotEnoughGems:
                    Ctx.Screens.Toast("KHÔNG ĐỦ NGỌC MA PHÁP (CẦN " + cost + ", ĐANG CÓ " + ProfileService.Gems + ").");
                    break;
                case UpgradeResult.MaxLevel:
                    Ctx.Screens.Toast("XE ĐÃ ĐẠT CẤP TỐI ĐA.");
                    break;
            }
        }

        private void OnOptionSlotClicked(int optionTab)
        {
            TankEntry tank = CurrentTank;
            if (tank == null) return;
            Ctx.Screens.Get<SkillPickerPopup>().Open(tank.id, optionTab);
        }

        private void OnAccessorySwap()
        {
            TankEntry tank = CurrentTank;
            if (tank == null) return;
            Ctx.Screens.Get<AccessoryPickerPopup>().Open(tank.id);
        }

        private void OnDismantle()
        {
            TankEntry tank = CurrentTank;
            if (tank == null) return;

            TankSave save = ProfileService.GetTank(tank.id);
            if (save.equipmentId < 0) return;

            AccessoryEntry acc = Ctx.Accessories.Find(save.equipmentId);
            string accName = acc != null ? acc.displayName : "phụ kiện này";
            AccessoryCatalog catalog = Ctx.Accessories;
            string tankId = tank.id;

            Ctx.Screens.Get<ConfirmPopup>().Open(
                "PHÂN RÃ PHỤ KIỆN",
                "Phân rã " + accName + "?\nPhụ kiện sẽ biến mất và bạn nhận ngẫu nhiên " +
                catalog.dismantleMinGems + "-" + catalog.dismantleMaxGems + " Ngọc ma pháp.",
                "PHÂN RÃ", true,
                () =>
                {
                    int gems = catalog.RollDismantleGems();
                    if (ProfileService.DismantleEquipped(tankId, gems))
                        Ctx.Screens.Toast("ĐÃ PHÂN RÃ · +" + gems + " NGỌC MA PHÁP", true);
                });
        }

        private void OnDeploy()
        {
            TankEntry tank = CurrentTank;
            if (tank == null) return;

            ProfileService.SelectTank(tank.id);
            Ctx.Screens.Show<StageSelectScreen>();
        }
    }
}
