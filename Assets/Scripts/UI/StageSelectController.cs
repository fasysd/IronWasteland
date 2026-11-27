using System;
using IronWasteland.Net;
using IronWasteland.Save;
using IronWasteland.Stages;
using IronWasteland.Tanks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronWasteland.UI
{
    /// <summary>
    /// Điều khiển màn hình CHỌN ẢI CHIẾN DỊCH. Khung nền, đường nối và 9 thẻ ải đã dựng sẵn;
    /// script cập nhật thẻ Siêu cơ sẵn sàng, ảnh ải đang chọn, thông tin nhiệm vụ và chế độ chơi.
    /// Bấm CHƠI LAN sẽ sang scene PHÒNG LAN; CHƠI ĐƠN vào trận ngay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageSelectController : MonoBehaviour
    {
        /// <summary>Dữ liệu hiển thị của một xe trong thẻ Siêu cơ sẵn sàng.</summary>
        [Serializable]
        public sealed class TankEntry
        {
            [Tooltip("Tên xe hiển thị trên thẻ.")]
            public string DisplayName = "BLACK SHADOW";

            [Tooltip("Dòng vai trò dưới tên xe.")]
            public string Role = "SÁT THỦ CƠ ĐỘNG";

            [Tooltip("Cấp hiện tại của xe.")]
            public int Level = 5;

            [Tooltip("8 chỉ số theo thứ tự ATK, HP, EN, DEF, SPD, CDR, DMG, PEN.")]
            public int[] Stats = new int[8];

            [Tooltip("Tên hai kỹ năng gốc (phím J, K).")]
            public string[] SkillNames = new string[2];

            [Tooltip("Ảnh bốn ô kỹ năng: hai kỹ năng gốc rồi hai kỹ năng tùy chọn mặc định.")]
            public Sprite[] SkillIcons = new Sprite[4];

            [Tooltip("Sáu icon kỹ năng tùy chọn: tab 1 ba lựa chọn rồi tab 2 ba lựa chọn.")]
            public Sprite[] OptionSkillIcons = new Sprite[6];

            [Tooltip("Ảnh xe đang chọn.")]
            public Sprite Portrait;
        }

        /// <summary>Dữ liệu của một thẻ ải trên bản đồ tiến trình.</summary>
        [Serializable]
        public sealed class StageEntry
        {
            [Tooltip("Mã ải, ví dụ 1-2.")]
            public string Code = "1-2";

            [Tooltip("Tên ải hiển thị trên thẻ và panel nhiệm vụ.")]
            public string Name = "HANG ĐÁ GAI";

            [Tooltip("Nhãn loại nhiệm vụ, ví dụ SINH TỒN.")]
            public string Tag = "SINH TỒN";

            [Tooltip("Dòng mục tiêu màu cam dưới tên ải.")]
            public string Objective = "SINH TỒN 100 GIÂY";

            [Tooltip("Mô tả nhiệm vụ trong panel bên phải.")]
            [TextArea] public string Description = string.Empty;

            [Tooltip("Phần thưởng ngọc hiển thị trong panel.")]
            public string Reward = "+15 Ngọc Ma Pháp";

            [Tooltip("Dòng tỷ lệ rơi phụ kiện.")]
            public string DropRate = "Tỷ lệ rơi Phụ kiện: 35%";

            [Tooltip("Ải đã mở khoá để bấm chọn hay chưa.")]
            public bool Unlocked = true;

            [Tooltip("Dữ liệu chiến đấu tương ứng; rỗng nếu ải chưa có StageDefinition.")]
            public StageDefinition Definition;
        }

        [Header("Dữ liệu xe (tạm thời, khớp thứ tự Garage)")]
        [SerializeField]
        [Tooltip("Sáu xe, đúng thứ tự trong Garage.")]
        private TankEntry[] m_Tanks = new TankEntry[0];

        [SerializeField]
        [Tooltip("Xe hiển thị khi chưa từng chọn xe nào.")]
        private int m_DefaultIndex = 2;

        [Header("Dữ liệu ải")]
        [SerializeField]
        [Tooltip("Chín thẻ ải theo đúng thứ tự trên bản đồ tiến trình.")]
        private StageEntry[] m_Stages = new StageEntry[0];

        [SerializeField]
        [Tooltip("Ải mặc định khi mở màn hình lần đầu.")]
        private int m_DefaultStageIndex = 1;

        [SerializeField]
        [Tooltip("Vùng bấm của chín thẻ ải theo toạ độ thiết kế 1920x1080.")]
        private Rect[] m_CardRects = new Rect[0];

        [Header("Tham chiếu thẻ Siêu cơ sẵn sàng")]
        [SerializeField]
        [Tooltip("Ảnh xe trong thẻ.")]
        private Image m_TankPortrait;

        [SerializeField]
        [Tooltip("Tên xe trong thẻ.")]
        private TextMeshProUGUI m_TankName;

        [SerializeField]
        [Tooltip("Dòng CẤP xx / VAI TRÒ trong thẻ.")]
        private TextMeshProUGUI m_TankRoleLevel;

        [SerializeField]
        [Tooltip("Dòng bốn chỉ số ATK HP EN SPD trong thẻ.")]
        private TextMeshProUGUI m_TankStats;

        [SerializeField]
        [Tooltip("Tên bốn kỹ năng, đúng thứ tự J, K, L, I.")]
        private TextMeshProUGUI[] m_SkillNames = new TextMeshProUGUI[4];

        [SerializeField]
        [Tooltip("Ảnh bốn kỹ năng, đúng thứ tự J, K, L, I.")]
        private Image[] m_SkillIcons = new Image[4];

        [Header("Panel nhiệm vụ động")]
        [SerializeField]
        [Tooltip("Nền che nội dung nhiệm vụ tĩnh để vẽ nội dung theo ải đang chọn.")]
        private GameObject m_MissionPanel;

        [SerializeField]
        [Tooltip("Dòng ẢI x-y / LOẠI NHIỆM VỤ.")]
        private TextMeshProUGUI m_MissionCode;

        [SerializeField]
        [Tooltip("Tên ải lớn trong panel nhiệm vụ.")]
        private TextMeshProUGUI m_MissionName;

        [SerializeField]
        [Tooltip("Dòng mục tiêu màu cam.")]
        private TextMeshProUGUI m_MissionObjective;

        [SerializeField]
        [Tooltip("Mô tả nhiệm vụ.")]
        private TextMeshProUGUI m_MissionDescription;

        [SerializeField]
        [Tooltip("Phần thưởng ngọc.")]
        private TextMeshProUGUI m_MissionReward;

        [SerializeField]
        [Tooltip("Tỷ lệ rơi phụ kiện.")]
        private TextMeshProUGUI m_MissionDrop;

        [Header("Ngọc ma pháp")]
        [SerializeField]
        [Tooltip("Số ngọc ma pháp hiển thị ở góc phải header.")]
        private TextMeshProUGUI m_GemCount;

        /// <summary>Giá trị ngọc đã in lần trước, tránh set text mỗi khung hình.</summary>
        private int m_GemCache = int.MinValue;

        [Header("Ải đang chọn trên bản đồ")]
        [SerializeField]
        [Tooltip("Khung cam bao quanh thẻ ải đang chọn.")]
        private RectTransform m_SelectionFrame;

        [SerializeField]
        [Tooltip("Viền xám của thẻ 1-2; ẩn khi thẻ đang được chọn để không lộ viền kép.")]
        private Image m_Card12Border;

        [SerializeField]
        [Tooltip("Lớp phủ làm mờ thẻ 1-2 khi ải này không còn được chọn.")]
        private GameObject m_CurrentCardDim;

        [SerializeField]
        [Tooltip("Nền che trạng thái tĩnh của chín thẻ ải.")]
        private Image[] m_StatusCovers = new Image[9];

        [SerializeField]
        [Tooltip("Chữ trạng thái động của chín thẻ ải.")]
        private TextMeshProUGUI[] m_StatusTexts = new TextMeshProUGUI[9];

        [Header("Chọn chế độ chơi")]
        [SerializeField]
        [Tooltip("Ảnh nút CHƠI ĐƠN.")]
        private Image m_ModeSingleImage;

        [SerializeField]
        [Tooltip("Ảnh nút CHƠI CO-OP.")]
        private Image m_ModeCoopImage;

        [SerializeField]
        [Tooltip("Nhãn chính nút CHƠI ĐƠN.")]
        private TextMeshProUGUI m_ModeSingleLabel;

        [SerializeField]
        [Tooltip("Nhãn phụ nút CHƠI ĐƠN.")]
        private TextMeshProUGUI m_ModeSingleHint;

        [SerializeField]
        [Tooltip("Nhãn chính nút CHƠI CO-OP.")]
        private TextMeshProUGUI m_ModeCoopLabel;

        [SerializeField]
        [Tooltip("Nhãn phụ nút CHƠI CO-OP.")]
        private TextMeshProUGUI m_ModeCoopHint;

        [SerializeField]
        [Tooltip("Dấu tích ở nút đang chọn.")]
        private Image m_ModeCheck;

        [SerializeField]
        [Tooltip("Sprite nút chưa chọn.")]
        private Sprite m_ModeOffSprite;

        [SerializeField]
        [Tooltip("Sprite nút đang chọn.")]
        private Sprite m_ModeOnSprite;

        [Header("Vùng bấm theo toạ độ thiết kế (gốc trên-trái, hệ 1920x1080)")]
        [SerializeField]
        [Tooltip("Vùng bấm nút QUAY LẠI ở góc trên trái.")]
        private Rect m_BackArea = new Rect(72f, 94f, 168f, 50f);

        [SerializeField]
        [Tooltip("Vùng bấm nút CHƠI ĐƠN.")]
        private Rect m_SingleModeArea = new Rect(1295f, 472f, 253f, 48f);

        [SerializeField]
        [Tooltip("Vùng bấm nút CHƠI CO-OP.")]
        private Rect m_CoopModeArea = new Rect(1565f, 472f, 265f, 48f);

        [SerializeField]
        [Tooltip("Vùng bấm nút BẮT ĐẦU CHIẾN ĐẤU.")]
        private Rect m_StartArea = new Rect(1293f, 939f, 536f, 136f);

        [Header("Scene đích")]
        [SerializeField]
        [Tooltip("Tên scene quay lại khi bấm QUAY LẠI hoặc Escape.")]
        private string m_BackSceneName = "MainMenu";

        [SerializeField]
        [Tooltip("Tên scene chiến đấu được mở khi bắt đầu.")]
        private string m_GameSceneName = "SampleScene";

        [SerializeField]
        [Tooltip("Tên scene PHÒNG LAN mở khi bấm CHƠI LAN.")]
        private string m_CoopLobbySceneName = "CoopLobby";

        private const float DesignHeight = 1080f;

        /// <summary>Độ nới của khung chọn để phủ kín viền card (đơn vị thiết kế).</summary>
        private const float SelectionFrameBleed = 1f;

        private Canvas m_Canvas;
        private Vector2 m_PointerScreen;
        private bool m_HasGuiPointer;

        /// <summary>Xe đang hiển thị trên thẻ.</summary>
        public int SelectedIndex { get; private set; }

        /// <summary>Ải đang được chọn.</summary>
        public int SelectedStageIndex { get; private set; }

        private void Awake()
        {
            m_Canvas = GetComponent<Canvas>();
            if (m_Canvas == null)
            {
                m_Canvas = GetComponentInParent<Canvas>();
            }

            if (m_Tanks != null && m_Tanks.Length > 0)
            {
                int start = SaveSystem.Instance.LastSelectedTankIndex >= 0 && SaveSystem.Instance.LastSelectedTankIndex < m_Tanks.Length
                    ? SaveSystem.Instance.LastSelectedTankIndex
                    : m_DefaultIndex;
                ApplyTank(start);
            }

            int stage = m_DefaultStageIndex;
            if (m_Stages != null && m_Stages.Length > 0)
            {
                // Trạng thái mở khoá = màn mở sẵn theo thiết kế (cờ Unlocked lưu trong scene)
                // HOẶC tiến trình người chơi đã mở. Tiến trình chỉ mở thêm chứ không khoá lại
                // màn mà nhà thiết kế đã cho mở sẵn (1-1, 1-2); nếu ghi đè tuyệt đối thì thẻ
                // hiện "đã mở khoá" nhưng bấm vào lại không chọn được.
                for (int i = 0; i < m_Stages.Length; i++)
                {
                    if (m_Stages[i] != null)
                    {
                        m_Stages[i].Unlocked = m_Stages[i].Unlocked || SaveSystem.Instance.IsStageUnlocked(m_Stages[i].Code);
                    }
                }

                for (int i = 0; i < m_Stages.Length; i++)
                {
                    if (m_Stages[i] != null && string.Equals(m_Stages[i].Code, CoopEntry.StageCode, StringComparison.OrdinalIgnoreCase))
                    {
                        stage = i;
                        break;
                    }
                }

                ApplyStage(stage);
            }

            ApplyMode(CoopEntry.Mode);
            RefreshGemCount();
        }

        private void Update()
        {
            RefreshGemCount();
        }

        private void OnGUI()
        {
            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            switch (current.type)
            {
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    m_PointerScreen = ToScreenPoint(current.mousePosition);
                    m_HasGuiPointer = true;
                    break;
                case EventType.MouseDown:
                    if (current.button == 0)
                    {
                        m_PointerScreen = ToScreenPoint(current.mousePosition);
                        m_HasGuiPointer = true;
                    }

                    break;
                case EventType.MouseUp:
                    if (current.button == 0)
                    {
                        m_PointerScreen = ToScreenPoint(current.mousePosition);
                        m_HasGuiPointer = true;
                    }

                    break;
                case EventType.KeyDown:
                    if (current.keyCode == KeyCode.Escape)
                    {
                        GoBack();
                    }
                    else if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
                    {
                        EnterSelectedMode();
                    }

                    break;
            }
        }

        /// <summary>Nút QUAY LẠI gọi vào đây.</summary>
        public void PressBack()
        {
            GoBack();
        }

        /// <summary>Nút BẮT ĐẦU gọi vào đây.</summary>
        public void PressStart()
        {
            EnterSelectedMode();
        }

        /// <summary>Nút CHƠI ĐƠN gọi vào đây.</summary>
        public void PressModeSingle()
        {
            ApplyMode(MatchMode.Single);
        }

        /// <summary>Nút CHƠI CO-OP gọi vào đây.</summary>
        public void PressModeCoop()
        {
            ApplyMode(MatchMode.Coop);
            //EnterCoopLobby();
        }

        /// <summary>Thẻ ải gọi vào đây kèm chỉ số; ải còn khoá thì bỏ qua.</summary>
        public void SelectStage(int index)
        {
            if (m_Stages == null || index < 0 || index >= m_Stages.Length)
            {
                return;
            }

            if (m_Stages[index] == null || !m_Stages[index].Unlocked)
            {
                return;
            }

            ApplyStage(index);
        }

        /// <summary>Cập nhật toàn bộ thẻ xe theo xe đang chọn và lựa chọn kỹ năng đã lưu.</summary>
        private void ApplyTank(int index)
        {
            SelectedIndex = Mathf.Clamp(index, 0, m_Tanks.Length - 1);
            TankEntry tank = m_Tanks[SelectedIndex];
            if (tank == null)
            {
                return;
            }

            int level = SaveSystem.Instance.GetTankLevel(SelectedIndex);
            bool owned = level > 0;
            TankDefinition def = TankRegistry.GetDefinition(SelectedIndex);

            if (m_TankPortrait != null)
            {
                m_TankPortrait.sprite = tank.Portrait;
                m_TankPortrait.preserveAspect = true;
            }

            SetText(m_TankName, def != null ? def.TankName : tank.DisplayName);
            SetText(m_TankRoleLevel, owned ? "CẤP " + level.ToString("00") + " / " + tank.Role : "CHƯA SỞ HỮU / " + tank.Role);

            if (def != null && owned)
            {
                TankStats stats = def.GetStatsForLevel(level);
                SetText(m_TankStats, $"{stats.Attack:0} ATK   {stats.MaxHealth:0} HP   {stats.MaxEnergy:0} EN   {stats.MoveSpeed:0} SPD");
            }
            else if (!owned)
            {
                SetText(m_TankStats, "- ATK   - HP   - EN   - SPD");
            }
            else
            {
                SetText(m_TankStats, FormatStats(tank.Stats));
            }

            for (int slot = 0; slot < 2; slot++)
            {
                string skillName = def != null ? def.GetSkillName(slot) : GetFrom(tank.SkillNames, slot);
                Sprite skillIcon = def != null ? def.GetSkillIcon(slot) : GetFrom(tank.SkillIcons, slot);
                SetText(GetFrom(m_SkillNames, slot), skillName);
                SetIcon(GetFrom(m_SkillIcons, slot), skillIcon);
            }

            // Hai ô cuối đổi theo lựa chọn tùy chọn đang lưu trong SaveSystem.
            for (int tab = 0; tab < SkillCatalog.TabCount; tab++)
            {
                int slot = tab + 2;
                int optionIndex = SaveSystem.Instance.GetOptionSkill(SelectedIndex, tab);
                SkillCatalog.Option option = SkillCatalog.Get(SelectedIndex, tab, optionIndex);
                SetText(GetFrom(m_SkillNames, slot), option.Name);

                int iconIndex = tab * SkillCatalog.OptionCount + optionIndex;
                SetIcon(GetFrom(m_SkillIcons, slot), GetFrom(tank.OptionSkillIcons, iconIndex));
            }
        }

        /// <summary>Đổi ải đang chọn, cập nhật panel nhiệm vụ và trạng thái các thẻ.</summary>
        private void ApplyStage(int index)
        {
            if (m_Stages == null || m_Stages.Length == 0) return;

            SelectedStageIndex = Mathf.Clamp(index, 0, m_Stages.Length - 1);
            StageEntry stage = m_Stages[SelectedStageIndex];
            if (stage == null) return;

            StageDefinition def = stage.Definition;
            string stageName = def != null && !string.IsNullOrEmpty(def.StageName) ? def.StageName.ToUpperInvariant() : stage.Name;
            string objectiveText = def != null && !string.IsNullOrEmpty(def.WinConditionDescription) ? def.WinConditionDescription : stage.Objective;
            string rewardText = def != null ? $"+{def.RewardGems} Ngọc Ma Pháp" : stage.Reward;
            string dropText = def != null ? FormatAccessoryDrop(def) : stage.DropRate;

            CoopEntry.StageCode = def != null && !string.IsNullOrEmpty(def.StageCode) ? def.StageCode : stage.Code;
            CoopEntry.StageName = def != null && !string.IsNullOrEmpty(def.StageName) ? def.StageName : stage.Name;
            CoopEntry.SelectedDefinition = def;

            SetText(m_MissionCode, "ẢI " + CoopEntry.StageCode + " / " + stage.Tag);
            SetText(m_MissionName, stageName);
            SetText(m_MissionObjective, objectiveText);
            SetText(m_MissionDescription, stage.Description);
            SetText(m_MissionReward, rewardText);
            SetText(m_MissionDrop, dropText);

            if (m_SelectionFrame != null && m_CardRects != null && SelectedStageIndex < m_CardRects.Length)
            {
                Rect rect = m_CardRects[SelectedStageIndex];
                m_SelectionFrame.anchorMin = Vector2.zero;
                m_SelectionFrame.anchorMax = Vector2.zero;
                m_SelectionFrame.pivot = Vector2.zero;
                m_SelectionFrame.anchoredPosition = new Vector2(rect.x - SelectionFrameBleed, DesignHeight - (rect.y + rect.height) - SelectionFrameBleed);
                m_SelectionFrame.sizeDelta = new Vector2(rect.width + SelectionFrameBleed * 2f, rect.height + SelectionFrameBleed * 2f);
            }

            if (m_Card12Border != null)
            {
                m_Card12Border.enabled = SelectedStageIndex != 1;
            }

            if (m_CurrentCardDim != null)
            {
                m_CurrentCardDim.SetActive(SelectedStageIndex != 1);
            }

            int statusCount = m_Stages != null ? m_Stages.Length : 0;
            if (m_StatusTexts != null && m_StatusTexts.Length < statusCount)
            {
                statusCount = m_StatusTexts.Length;
            }

            for (int i = 0; i < statusCount; i++)
            {
                StageEntry entry = m_Stages != null && i < m_Stages.Length ? m_Stages[i] : null;
                bool selected = SelectedStageIndex == i;
                bool cleared = entry != null && SaveSystem.Instance.IsStageCleared(entry.Code);
                bool unlocked = entry != null && entry.Unlocked;

                if (m_StatusCovers != null && i < m_StatusCovers.Length && m_StatusCovers[i] != null)
                {
                    m_StatusCovers[i].enabled = true;
                }

                string value;
                Color color;
                if (selected)
                {
                    value = "ĐANG CHỌN";
                    color = new Color32(0xFF, 0x9A, 0x3C, 0xFF);
                }
                else if (cleared)
                {
                    value = "ĐÃ HOÀN THÀNH";
                    color = new Color32(0x2F, 0xE8, 0xE0, 0xFF);
                }
                else if (unlocked)
                {
                    value = "ĐÃ MỞ KHÓA";
                    color = new Color32(0x6F, 0x7D, 0x8C, 0xFF);
                }
                else
                {
                    value = "CHƯA MỞ KHÓA";
                    color = new Color32(0x4E, 0x5A, 0x69, 0xFF);
                }

                TextMeshProUGUI text = m_StatusTexts[i];
                if (text != null)
                {
                    text.text = value;
                    text.color = color;
                }
            }
        }

        /// <summary>Đổi trạng thái hiển thị của hai nút chế độ và dấu tích.</summary>
        private void ApplyMode(MatchMode mode)
        {
            CoopEntry.Mode = mode;
            bool coop = mode == MatchMode.Coop;

            if (m_ModeSingleImage != null && m_ModeOffSprite != null && m_ModeOnSprite != null)
            {
                m_ModeSingleImage.sprite = coop ? m_ModeOffSprite : m_ModeOnSprite;
            }

            if (m_ModeCoopImage != null && m_ModeOffSprite != null && m_ModeOnSprite != null)
            {
                m_ModeCoopImage.sprite = coop ? m_ModeOnSprite : m_ModeOffSprite;
            }

            SetColor(m_ModeSingleLabel, coop ? new Color32(0x6F, 0x7D, 0x8C, 0xFF) : Color.white);
            SetColor(m_ModeSingleHint, coop ? new Color32(0x4E, 0x5A, 0x69, 0xFF) : new Color32(0xFF, 0xE0, 0xB2, 0xFF));
            SetColor(m_ModeCoopLabel, coop ? Color.white : new Color32(0x6F, 0x7D, 0x8C, 0xFF));
            SetColor(m_ModeCoopHint, coop ? new Color32(0xFF, 0xE0, 0xB2, 0xFF) : new Color32(0x4E, 0x5A, 0x69, 0xFF));

            if (m_ModeCheck != null)
            {
                m_ModeCheck.gameObject.SetActive(coop);
            }
        }

        private void EnterSelectedMode()
        {
            if (CoopEntry.Mode == MatchMode.Coop) EnterCoopLobby();
            else StartSingle();
        }

        private void EnterCoopLobby()
        {
            CoopEntry.Mode = MatchMode.Coop;
            CoopEntry.OpenLobby = true;
            LoadScene(m_CoopLobbySceneName);
        }

        private void StartSingle()
        {
            CoopEntry.Mode = MatchMode.Single;
            CoopEntry.OpenLobby = false;

            StageEntry current = (m_Stages != null && SelectedStageIndex >= 0 && SelectedStageIndex < m_Stages.Length)
                ? m_Stages[SelectedStageIndex]
                : null;

            string targetScene = current != null ? SaveSystem.SceneNameFor(current.Code) : string.Empty;
            if (string.IsNullOrEmpty(targetScene) || !Application.CanStreamedLevelBeLoaded(targetScene))
            {
                targetScene = m_GameSceneName;
            }

            LoadScene(targetScene);
        }

        private static string FormatAccessoryDrop(StageDefinition def)
        {
            if (def.RewardAccessoryCount <= 0) return "Không rơi Phụ kiện";
            return def.GuaranteeAccessories
                ? $"Chắc chắn rơi {def.RewardAccessoryCount} Phụ kiện"
                : $"Tỷ lệ rơi Phụ kiện: 20% (tối đa {def.RewardAccessoryCount})";
        }

        private static string FormatStats(int[] stats)
        {
            if (stats == null || stats.Length < 5)
            {
                return "- ATK   - HP   - EN   - SPD";
            }

            return stats[0] + " ATK   " + stats[1] + " HP   " + stats[2] + " EN   " + stats[4] + " SPD";
        }

        /// <summary>Đổ số ngọc ma pháp thật từ tiến trình người chơi vào header.</summary>
        private void RefreshGemCount()
        {
            if (m_GemCount == null)
            {
                return;
            }

            int gems = SaveSystem.Instance.Gems;
            if (gems == m_GemCache)
            {
                return;
            }

            m_GemCache = gems;
            m_GemCount.text = gems.ToString();
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static void SetColor(TextMeshProUGUI target, Color value)
        {
            if (target != null)
            {
                target.color = value;
            }
        }

        private static void SetIcon(Image target, Sprite sprite)
        {
            if (target != null && sprite != null)
            {
                target.sprite = sprite;
            }
        }

        private static string GetFrom(string[] values, int index)
        {
            return values != null && index < values.Length && values[index] != null ? values[index] : string.Empty;
        }

        private static Sprite GetFrom(Sprite[] values, int index)
        {
            return values != null && index < values.Length ? values[index] : null;
        }

        private static TextMeshProUGUI GetFrom(TextMeshProUGUI[] values, int index)
        {
            return values != null && index < values.Length ? values[index] : null;
        }

        private static Image GetFrom(Image[] values, int index)
        {
            return values != null && index < values.Length ? values[index] : null;
        }

        private static Vector2 ToScreenPoint(Vector2 guiPosition)
        {
            return new Vector2(guiPosition.x, Screen.height - guiPosition.y);
        }

        private Vector2 DesignPointerPosition(Vector2 screenPosition)
        {
            float scale = m_Canvas != null ? m_Canvas.scaleFactor : 1f;
            if (scale <= 0f)
            {
                scale = 1f;
            }

            return new Vector2(screenPosition.x / scale, (Screen.height - screenPosition.y) / scale);
        }

        private Vector2 PointerPosition
        {
            get
            {
                if (m_HasGuiPointer)
                {
                    return m_PointerScreen;
                }

#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

        private void GoBack()
        {
            LoadScene(m_BackSceneName);
        }

        /// <summary>Chuyển scene theo tên, chỉ chạy khi scene đã có trong Build Settings.</summary>
        private void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning("[StageSelect] Scene chưa có trong Build Settings: " + sceneName);
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}