using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using IronWasteland.Tanks;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Chọn ải: bản đồ các ải (đi theo hình chữ S), thông tin nhiệm vụ, chế độ Chơi đơn / Co-op LAN,
    /// xe ra trận và nút BẮT ĐẦU CHIẾN ĐẤU.
    ///
    /// Bố cục bản đồ: 5 ải mỗi hàng, hàng lẻ đi từ phải sang trái (như mockup: 1-1..1-4, 2-1 ở hàng trên;
    /// 2-2..2-5 ở hàng dưới). Thiết kế cho tối đa 2 hàng (10 ải).
    /// </summary>
    public sealed class StageSelectScreen : MenuScreen
    {
        private sealed class NodeUI
        {
            public int Index;
            public Button Button;
            public TextMeshProUGUI Status;
        }

        private sealed class ConnectorUI
        {
            public int FromIndex; // nối ải FromIndex -> FromIndex + 1
            public Image Line;
        }

        private const int PerRow = 5;
        private const float NodeW = 200f;
        private const float NodeH = 150f;

        private readonly List<NodeUI> m_Nodes = new List<NodeUI>();
        private readonly List<ConnectorUI> m_Connectors = new List<ConnectorUI>();

        private int m_Selected;

        // --- Nút theo trạng thái ---
        private ButtonLook m_LookReady, m_LookCleared, m_LookElite, m_LookFinal, m_LookSelected, m_LookLocked;

        // --- Thông tin nhiệm vụ ---
        private TextMeshProUGUI m_StageName;
        private TextMeshProUGUI m_ChapterTag;
        private TextMeshProUGUI m_StageKindLine;
        private TextMeshProUGUI m_Description;
        private TextMeshProUGUI m_RewardGems;
        private TextMeshProUGUI m_RewardDrop;

        // --- Chế độ chơi ---
        private Button m_ModeSingle;
        private Button m_ModeCoop;
        private CanvasGroup m_CoopGroup;
        private readonly Image[] m_SlotPanels = new Image[4];
        private readonly TextMeshProUGUI[] m_SlotTitles = new TextMeshProUGUI[4];
        private readonly TextMeshProUGUI[] m_SlotStates = new TextMeshProUGUI[4];
        private TextMeshProUGUI m_CoopStatus;

        // --- Xe ra trận ---
        private TankPreviewSlot m_TankPreview;
        private TextMeshProUGUI m_TankName;
        private TextMeshProUGUI m_TankRole;
        private TextMeshProUGUI m_TankStats;
        private readonly Image[] m_TankSkillIcons = new Image[4];

        // --- Bắt đầu ---
        private Button m_StartButton;
        private TextMeshProUGUI m_StartLabel;

        // ============================================================
        // DỰNG
        // ============================================================

        protected override void Build()
        {
            BuildHeader("CHIẾN DỊCH / MỞ MÀN CHƠI", "CHỌN ẢI CHIẾN DỊCH");

            BuildLooks();
            BuildMap();
            BuildInfoPanel();
            BuildModePanel();
            BuildTankCard();
            BuildStartButton();
            BuildFooter("CHỌN ẢI ĐỂ XEM THÔNG TIN NHIỆM VỤ", "NHẤN ESC ĐỂ QUAY LẠI");
        }

        private static ButtonLook MakeLook(Color border, Color fill)
        {
            return new ButtonLook(
                fill,
                Color.Lerp(fill, Color.white, 0.07f),
                Color.Lerp(fill, Color.black, 0.35f),
                Color.Lerp(fill, Color.black, 0.5f),
                border,
                UITheme.Text);
        }

        private void BuildLooks()
        {
            m_LookReady = MakeLook(UITheme.BorderBright, UITheme.Panel);
            m_LookCleared = MakeLook(UITheme.Teal, UITheme.Hex("#0D1B22"));
            m_LookElite = MakeLook(UITheme.Purple, UITheme.Hex("#1A1430"));
            m_LookFinal = MakeLook(UITheme.Red, UITheme.Hex("#25121A"));
            m_LookSelected = MakeLook(UITheme.Orange, UITheme.Hex("#26180F"));
            m_LookLocked = MakeLook(UITheme.Hex("#1B2238"), UITheme.Hex("#0A0D16"));
        }

        private static Vector2 NodePosition(int i)
        {
            int row = i / PerRow;
            int k = i % PerRow;
            int col = (row % 2 == 0) ? k : PerRow - 1 - k;
            return new Vector2(131f + col * 222f, 380f + row * 290f);
        }

        private void BuildMap()
        {
            Image panel = UIKit.PanelAt(Content, "MapPanel", 95, 195, 1160, 790, UITheme.PanelDark, UITheme.Border);
            UIKit.Label(panel.transform, "BẢN ĐỒ TIẾN TRÌNH CHIẾN DỊCH", 30, 24, 800, 28, 22, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold, false, true);
            UIKit.Label(panel.transform, "CHỌN ẢI ĐỂ XEM THÔNG TIN NHIỆM VỤ VÀ CHUẨN BỊ XUẤT TRẬN", 30, 56, 900, 16, 12, UITheme.TextFaint);

            List<StageEntry> stages = Ctx.Stages.stages;

            // Đường nối vẽ trước để nằm dưới các ô.
            for (int i = 0; i < stages.Count - 1; i++)
            {
                Vector2 a = NodePosition(i);
                Vector2 b = NodePosition(i + 1);
                Image line;

                if (Mathf.Approximately(a.y, b.y))
                {
                    float x1 = Mathf.Min(a.x, b.x) + NodeW;
                    float x2 = Mathf.Max(a.x, b.x);
                    line = UIKit.PanelAt(Content, "Link_" + i, x1, a.y + NodeH * 0.5f - 1.5f, x2 - x1, 3, UITheme.Border);
                }
                else
                {
                    line = UIKit.PanelAt(Content, "Link_" + i, a.x + NodeW * 0.5f - 1.5f, a.y + NodeH, 3, b.y - a.y - NodeH, UITheme.Border);
                }

                m_Connectors.Add(new ConnectorUI { FromIndex = i, Line = line });
            }

            for (int i = 0; i < stages.Count; i++)
            {
                int index = i;
                StageEntry stage = stages[i];
                Vector2 p = NodePosition(i);

                var node = new NodeUI { Index = i };
                node.Button = UIKit.Btn(Content, "Stage_" + stage.id, p.x, p.y, NodeW, NodeH, m_LookReady, () => OnNodeClicked(index));
                Transform t = node.Button.transform;

                node.Status = UIKit.Label(t, "", 0, 12, NodeW, 16, 11, UITheme.TextDim, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Label(t, stage.Label, 0, 32, NodeW, 48, 40, UITheme.Text,
                    TextAlignmentOptions.Center, FontStyles.Normal, false, true, true);
                UIKit.Label(t, stage.displayName.ToUpperInvariant(), 10, 84, NodeW - 20, 42, 15, UITheme.Text,
                    TextAlignmentOptions.Top, FontStyles.Bold, true);
                UIKit.Label(t, "ĐỀ XUẤT CẤP " + stage.recommendedLevel, 0, 130, NodeW, 14, 10, UITheme.TextFaint,
                    TextAlignmentOptions.Center);

                m_Nodes.Add(node);
            }

            BuildLegend();
        }

        private void BuildLegend()
        {
            (string text, Color color)[] items =
            {
                ("ĐÃ HOÀN THÀNH", UITheme.Teal),
                ("SẴN SÀNG", UITheme.BorderBright),
                ("BOSS / TINH ANH", UITheme.Purple),
                ("BOSS CHUNG CUỘC", UITheme.Red),
                ("CHƯA MỞ KHÓA", UITheme.TextFaint),
            };

            for (int i = 0; i < items.Length; i++)
            {
                float x = 125 + i * 215f;
                UIKit.PanelAt(Content, "LegendDot", x, 946, 12, 12, items[i].color);
                UIKit.Label(Content, items[i].text, x + 20, 943, 190, 18, 11, UITheme.TextDim);
            }
        }

        private void BuildInfoPanel()
        {
            Image panel = UIKit.PanelAt(Content, "Info", 1295, 195, 550, 215, UITheme.PanelDark, UITheme.Border);

            UIKit.PanelAt(panel.transform, "Accent", 16, 16, 3, 28, UITheme.Teal);
            UIKit.Label(panel.transform, "THÔNG TIN NHIỆM VỤ", 28, 14, 300, 18, 13, UITheme.Teal,
                TextAlignmentOptions.Left, FontStyles.Bold);
            m_ChapterTag = UIKit.Label(panel.transform, "", 28, 32, 400, 14, 10, UITheme.TextFaint);

            m_StageName = UIKit.Label(panel.transform, "", 20, 58, 280, 60, 38, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Normal, false, true, true);
            m_StageKindLine = UIKit.Label(panel.transform, "", 20, 120, 280, 16, 12, UITheme.Orange,
                TextAlignmentOptions.Left, FontStyles.Bold);

            Image reward = UIKit.PanelAt(panel.transform, "Reward", 310, 54, 224, 92, UITheme.Panel, UITheme.Border);
            UIKit.Label(reward.transform, "PHẦN THƯỞNG HOÀN THÀNH", 12, 8, 200, 14, 9, UITheme.TextFaint);
            m_RewardGems = UIKit.Label(reward.transform, "", 12, 30, 200, 22, 17, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold, false, true);
            m_RewardDrop = UIKit.Label(reward.transform, "", 12, 58, 200, 22, 15, UITheme.Gold,
                TextAlignmentOptions.Left, FontStyles.Normal, false, true);

            m_Description = UIKit.Label(panel.transform, "", 20, 152, 510, 58, 13, UITheme.TextDim,
                TextAlignmentOptions.TopLeft, FontStyles.Normal, true);
        }

        private void BuildModePanel()
        {
            UIKit.Label(Content, "CHỌN CHẾ ĐỘ CHƠI", 1295, 428, 400, 20, 15, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);

            m_ModeSingle = BuildModeButton(1295, "CHƠI ĐƠN", "SINGLE PLAYER", () => SetCoop(false));
            m_ModeCoop = BuildModeButton(1577, "CHƠI CO-OP", "2-4 NGƯỜI · LAN", () => SetCoop(true));

            Image panel = UIKit.PanelAt(Content, "CoopRoom", 1295, 524, 550, 114, UITheme.PanelDark, UITheme.Border);
            m_CoopGroup = panel.gameObject.AddComponent<CanvasGroup>();

            UIKit.Label(panel.transform, "PHÒNG CO-OP / 2-4 NGƯỜI", 16, 10, 400, 14, 11, UITheme.TextFaint);

            for (int i = 0; i < 4; i++)
            {
                m_SlotPanels[i] = UIKit.PanelAt(panel.transform, "Slot_" + i, 16 + i * 131f, 32, 123, 52, UITheme.Panel, UITheme.Border);
                m_SlotTitles[i] = UIKit.Label(m_SlotPanels[i].transform, "", 0, 7, 123, 20, 13, UITheme.Text,
                    TextAlignmentOptions.Center, FontStyles.Bold, false, true);
                m_SlotStates[i] = UIKit.Label(m_SlotPanels[i].transform, "", 0, 29, 123, 16, 10, UITheme.TextFaint,
                    TextAlignmentOptions.Center, FontStyles.Normal, false, true);
            }

            m_CoopStatus = UIKit.Label(panel.transform, "", 16, 92, 520, 16, 10, UITheme.TextFaint,
                TextAlignmentOptions.Left, FontStyles.Normal, false, true);
        }

        private Button BuildModeButton(float x, string title, string sub, UnityEngine.Events.UnityAction onClick)
        {
            Button b = UIKit.Btn(Content, "Mode_" + title, x, 454, 268, 56, Looks.Card, onClick);
            UIKit.Label(b.transform, title, 18, 8, 232, 24, 18, UITheme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.Label(b.transform, sub, 18, 33, 232, 16, 10, UITheme.TextFaint);
            return b;
        }

        private void BuildTankCard()
        {
            UIKit.Label(Content, "CHỌN XE TĂNG RA TRẬN", 1295, 656, 400, 20, 15, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);

            Image panel = UIKit.PanelAt(Content, "TankCard", 1295, 682, 550, 230, UITheme.PanelDark, UITheme.Border);

            UIKit.PanelAt(panel.transform, "Accent", 16, 14, 3, 14, UITheme.Purple);
            UIKit.Label(panel.transform, "XE ĐÃ SẴN SÀNG", 28, 12, 300, 18, 12, UITheme.Teal,
                TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.TextBtn(panel.transform, "ĐỔI XE", 428, 8, 106, 32, Looks.Secondary, 13,
                () => Ctx.Screens.Show<GarageScreen>());

            m_TankPreview = TankPreviewSlot.Create(panel.transform, "Thumb", 16, 48, 84, 84, Ctx.Preview, true);
            m_TankName = UIKit.Label(panel.transform, "", 116, 46, 300, 36, 28, UITheme.Text,
                TextAlignmentOptions.Left, FontStyles.Bold, false, true);
            m_TankRole = UIKit.Label(panel.transform, "", 116, 84, 300, 18, 13, UITheme.Purple);
            m_TankStats = UIKit.Label(panel.transform, "", 116, 106, 410, 18, 12, UITheme.Teal,
                TextAlignmentOptions.Left, FontStyles.Normal, false, true);

            for (int i = 0; i < 4; i++)
            {
                Image frame = UIKit.PanelAt(panel.transform, "SkillFrame_" + i, 16 + i * 72f, 152, 60, 60,
                    UITheme.Panel, i < 2 ? UITheme.Orange : UITheme.Purple);
                m_TankSkillIcons[i] = UIKit.IconAt(frame.transform, "Icon", 4, 4, 52, 52);
            }
        }

        private void BuildStartButton()
        {
            m_StartButton = UIKit.Btn(Content, "Start", 1295, 924, 550, 66, Looks.Primary, OnStartClicked);
            Transform t = m_StartButton.transform;
            m_StartLabel = UIKit.Label(t, "BẮT ĐẦU CHIẾN ĐẤU", 24, 8, 440, 34, 28, UITheme.TextOnOrange,
                TextAlignmentOptions.Left, FontStyles.Bold, false, true);
            UIKit.Label(t, "NHẤN ĐỂ VÀO TRẬN", 24, 42, 440, 16, 11, UITheme.Alpha(UITheme.TextOnOrange, 0.7f));
            UIKit.Label(t, ">", 490, 8, 40, 50, 40, UITheme.TextOnOrange, TextAlignmentOptions.Center, FontStyles.Bold);
        }

        // ============================================================
        // HIỂN THỊ
        // ============================================================

        public override void OnShown()
        {
            base.OnShown();

            MenuServices.CoopLobby.Changed += RefreshCoop;
            if (ProfileService.Current.coopMode) MenuServices.CoopLobby.Open();

            m_Selected = ResolveInitialSelection();
            RefreshAll();
        }

        public override void OnHidden()
        {
            base.OnHidden();
            MenuServices.CoopLobby.Changed -= RefreshCoop;
            m_TankPreview.Hide();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            MenuServices.CoopLobby.Changed -= RefreshCoop;
        }

        protected override void OnProfileChanged() => RefreshAll();

        private int ResolveInitialSelection()
        {
            StageDatabase db = Ctx.Stages;
            if (db.stages.Count == 0) return 0;

            int idx = db.IndexOf(ProfileService.Current.selectedStageId);
            if (idx >= 0 && ProfileService.IsStageUnlocked(db, idx)) return idx;

            // Mặc định: ải đầu tiên đã mở mà chưa hoàn thành; nếu xong hết thì ải cuối.
            for (int i = 0; i < db.stages.Count; i++)
            {
                if (ProfileService.IsStageUnlocked(db, i) && !ProfileService.IsStageCleared(db.stages[i].id)) return i;
            }
            return db.stages.Count - 1;
        }

        private void RefreshAll()
        {
            RefreshMap();
            RefreshInfo();
            RefreshMode();
            RefreshCoop();
            RefreshTank();
            RefreshStart();
        }

        private void RefreshMap()
        {
            StageDatabase db = Ctx.Stages;

            for (int i = 0; i < m_Nodes.Count; i++)
            {
                NodeUI node = m_Nodes[i];
                StageEntry stage = db.stages[i];
                bool unlocked = ProfileService.IsStageUnlocked(db, i);
                bool cleared = ProfileService.IsStageCleared(stage.id);
                bool selected = i == m_Selected;

                ButtonLook look;
                string status;
                Color statusColor;

                if (selected)
                {
                    look = m_LookSelected; status = "ĐANG CHỌN"; statusColor = UITheme.Orange;
                }
                else if (!unlocked)
                {
                    look = m_LookLocked; status = "CHƯA MỞ KHÓA"; statusColor = UITheme.TextFaint;
                }
                else if (stage.kind == StageKind.FinalBoss)
                {
                    look = m_LookFinal; status = "BOSS / CHUNG CUỘC"; statusColor = UITheme.Red;
                }
                else if (stage.kind == StageKind.Elite)
                {
                    look = m_LookElite; status = "BOSS / TINH ANH"; statusColor = UITheme.Purple;
                }
                else if (cleared)
                {
                    look = m_LookCleared; status = "ĐÃ HOÀN THÀNH"; statusColor = UITheme.Teal;
                }
                else
                {
                    look = m_LookReady; status = "SẴN SÀNG"; statusColor = UITheme.TextDim;
                }

                UIKit.SetLook(node.Button, look);
                node.Status.text = status;
                node.Status.color = statusColor;
            }

            for (int i = 0; i < m_Connectors.Count; i++)
            {
                ConnectorUI c = m_Connectors[i];
                bool passed = ProfileService.IsStageCleared(db.stages[c.FromIndex].id);
                c.Line.color = passed ? UITheme.Teal : UITheme.Border;
            }
        }

        private StageEntry SelectedStage =>
            (m_Selected >= 0 && m_Selected < Ctx.Stages.stages.Count) ? Ctx.Stages.stages[m_Selected] : null;

        private void RefreshInfo()
        {
            StageEntry s = SelectedStage;
            if (s == null)
            {
                m_StageName.text = "—";
                return;
            }

            m_ChapterTag.text = s.chapterLabel;
            m_StageName.text = s.displayName.ToUpperInvariant();
            string unlockLine = "";
            if (!ProfileService.IsStageCleared(s.id))
            {
                for (int i = 0; i < Ctx.Tanks.tanks.Count; i++)
                {
                    TankEntry t = Ctx.Tanks.tanks[i];
                    if (t != null && t.unlockStageId == s.id)
                        unlockLine += "\nLẦN ĐẦU VƯỢT ẢI: MỞ KHÓA XE " + t.displayName.ToUpperInvariant();
                }
            }
            m_Description.text = s.description + unlockLine;
            m_RewardGems.text = "+" + s.rewardGems + " Ngọc ma pháp";
            m_RewardDrop.text = "Tỉ lệ rơi phụ kiện: " + Mathf.RoundToInt(s.accessoryDropChance * 100f) + "%"
                                + (s.accessoryDropCount > 1 ? " (x" + s.accessoryDropCount + ")" : "");

            string kind = s.kind == StageKind.FinalBoss ? "BOSS CHUNG CUỘC"
                        : s.kind == StageKind.Elite ? "BOSS TINH ANH"
                        : "ẢI THƯỜNG";
            m_StageKindLine.text = s.Label + " · " + kind + " · ĐỀ XUẤT CẤP " + s.recommendedLevel;
        }

        private void RefreshMode()
        {
            bool coop = ProfileService.Current.coopMode;
            UIKit.SetLook(m_ModeSingle, coop ? Looks.Card : Looks.CardActive);
            UIKit.SetLook(m_ModeCoop, coop ? Looks.CardActive : Looks.Card);
            m_CoopGroup.alpha = coop ? 1f : 0.35f;
        }

        private void RefreshCoop()
        {
            ICoopLobby lobby = MenuServices.CoopLobby;
            IReadOnlyList<CoopSlot> slots = lobby.Slots;

            for (int i = 0; i < 4; i++)
            {
                CoopSlot slot = i < slots.Count ? slots[i] : new CoopSlot { State = CoopSlotState.Empty };

                string title = "P" + (i + 1);
                string state;
                Color color;
                switch (slot.State)
                {
                    case CoopSlotState.Host: title += " / HOST"; state = "SẴN SÀNG"; color = UITheme.Green; break;
                    case CoopSlotState.Ready: title += " / " + slot.PlayerName; state = "SẴN SÀNG"; color = UITheme.Green; break;
                    case CoopSlotState.Joined: title += " / " + slot.PlayerName; state = "CHƯA SẴN SÀNG"; color = UITheme.Gold; break;
                    default: title += " / TRỐNG"; state = "CHỜ THAM GIA"; color = UITheme.TextFaint; break;
                }

                m_SlotTitles[i].text = title;
                m_SlotTitles[i].color = color == UITheme.TextFaint ? UITheme.TextDim : UITheme.Text;
                m_SlotStates[i].text = state;
                m_SlotStates[i].color = color;
                UIKit.SetBorder(m_SlotPanels[i], color == UITheme.TextFaint ? UITheme.Border : color);
            }

            m_CoopStatus.text = lobby.StatusText;
            RefreshStart();
        }

        private TankEntry CurrentTank
        {
            get
            {
                TankEntry tank = Ctx.Tanks.Find(ProfileService.Current.selectedTankId);
                if (tank == null && Ctx.Tanks.tanks.Count > 0) tank = Ctx.Tanks.tanks[0];
                return tank;
            }
        }

        private void RefreshTank()
        {
            TankEntry tank = CurrentTank;
            if (tank == null)
            {
                m_TankName.text = "CHƯA CÓ XE";
                return;
            }

            TankSave save = ProfileService.GetTank(tank.id);
            TankStats stats = TankStatsBridge.GetStats(tank.definition, save.level);

            m_TankName.text = tank.displayName.ToUpperInvariant();
            m_TankRole.text = tank.roleLine + " · CẤP " + UIKit.Pad2(save.level);
            m_TankStats.text =
                "ATK " + Mathf.RoundToInt(stats.Attack) +
                " · HP " + Mathf.RoundToInt(stats.MaxHealth) +
                " · EN " + Mathf.RoundToInt(stats.MaxEnergy) +
                " · SPD " + Mathf.RoundToInt(stats.MoveSpeed * StatDisplay.MoveSpeedDisplayScale);
            m_TankPreview.Show(tank);

            SkillUIInfo[] skills =
            {
                tank.normalSkill,
                tank.specialSkill,
                tank.GetOption(0, save.optionSkill1),
                tank.GetOption(1, save.optionSkill2),
            };
            for (int i = 0; i < 4; i++)
                UIKit.SetIcon(m_TankSkillIcons[i], skills[i].icon, skills[i].displayName);
        }

        private void RefreshStart()
        {
            if (m_StartButton == null) return;

            StageEntry s = SelectedStage;
            bool unlocked = s != null && ProfileService.IsStageUnlocked(Ctx.Stages, m_Selected);
            bool coop = ProfileService.Current.coopMode;
            bool coopOk = !coop || MenuServices.CoopLobby.CanStart;

            UIKit.SetEnabled(m_StartButton, unlocked && coopOk);

            if (!unlocked) m_StartLabel.text = "ẢI CHƯA MỞ KHÓA";
            else if (!coopOk) m_StartLabel.text = "CHỜ ĐỒNG ĐỘI";
            else m_StartLabel.text = "BẮT ĐẦU CHIẾN ĐẤU";
        }

        // ============================================================
        // HÀNH ĐỘNG
        // ============================================================

        private void OnNodeClicked(int index)
        {
            StageDatabase db = Ctx.Stages;
            if (!ProfileService.IsStageUnlocked(db, index))
            {
                string prev = index > 0 ? db.stages[index - 1].Label : "ẢI TRƯỚC";
                Ctx.Screens.Toast("HOÀN THÀNH " + prev + " ĐỂ MỞ KHÓA " + db.stages[index].Label + ".");
                return;
            }

            m_Selected = index;
            ProfileService.SetSelectedStage(db.stages[index].id);
            RefreshAll();
        }

        private void SetCoop(bool coop)
        {
            if (ProfileService.Current.coopMode == coop) return;

            ProfileService.SetCoopMode(coop);
            if (coop) MenuServices.CoopLobby.Open();
            else MenuServices.CoopLobby.Close();

            RefreshMode();
            RefreshStart();
        }

        private void OnStartClicked()
        {
            StageEntry stage = SelectedStage;
            TankEntry tank = CurrentTank;

            if (stage == null || tank == null) return;

            if (tank.definition == null)
            {
                Ctx.Screens.Toast("XE " + tank.displayName.ToUpperInvariant() + " CHƯA GÁN TankDefinition TRONG TankCatalog.");
                return;
            }

            if (string.IsNullOrEmpty(stage.sceneName))
            {
                Ctx.Screens.Toast(stage.Label + " CHƯA GÁN TÊN SCENE TRONG StageDatabase.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(stage.sceneName))
            {
                Ctx.Screens.Toast("SCENE \"" + stage.sceneName + "\" CHƯA CÓ TRONG BUILD SETTINGS.");
                return;
            }

            bool coop = ProfileService.Current.coopMode;
            if (coop && !MenuServices.CoopLobby.CanStart)
            {
                Ctx.Screens.Toast(MenuServices.CoopLobby.StatusText);
                return;
            }

            TankSave save = ProfileService.GetTank(tank.id);
            var context = new TankBrainContext(save.level, save.equipmentId, save.optionSkill1, save.optionSkill2);

            GameSession.Begin(tank.id, tank.definition, context, stage, Ctx.Accessories, Ctx.Tanks, coop);
            SceneManager.LoadScene(stage.sceneName);
        }
    }
}
