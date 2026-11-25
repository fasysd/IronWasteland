using System;
using System.Collections.Generic;
using IronWasteland.Save;
using IronWasteland.Stages;
using IronWasteland.Tanks;
using IronWasteland.UiModels;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronWasteland.UI
{
    /// <summary>
    /// Điều khiển màn hình Garage: danh sách xe bên trái, khu trưng bày ở giữa,
    /// 8 chỉ số bên phải, thanh cấp độ và 4 thẻ kỹ năng. Toàn bộ nội dung lấy từ
    /// TankRoster (TankDefinition) + tiến trình đã lưu (PlayerProgress, StageProgress).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarageController : MonoBehaviour
    {
        private const float DesignHeight = 1080f;

        [Header("Dữ liệu xe")]
        [Tooltip("Bộ xe hiển thị trong Garage, đúng thứ tự muốn hiện trong danh sách.")]
        [SerializeField] private TankRoster m_Roster;

        [Header("Khu trưng bày")]
        [Tooltip("Ảnh chân dung lớn của xe đang chọn.")]
        [SerializeField] private Image m_HeroImage;

        private Vector2 m_HeroBasePosition;

        /// <summary>Giá trị ngọc đã in lần trước, dùng để bỏ qua cập nhật thừa.</summary>
        private int m_GemCache = int.MinValue;

        [Tooltip("Dòng CURRENT UNIT / <tên xe> phía trên khung tròn.")]
        [SerializeField] private TextMeshProUGUI m_HeroCaption;

        [Tooltip("Tên xe lớn ở khu trưng bày.")]
        [SerializeField] private TextMeshProUGUI m_HeroName;

        [Tooltip("Dòng vai trò dưới tên xe lớn.")]
        [SerializeField] private TextMeshProUGUI m_HeroRole;

        [Tooltip("Số cấp lớn trong ô CẤP ĐỘ.")]
        [SerializeField] private TextMeshProUGUI m_HeroLevel;

        [Header("Thanh cấp độ")]
        [Tooltip("Số cấp hiện tại ở thanh nâng cấp.")]
        [SerializeField] private TextMeshProUGUI m_LevelValue;

        [Tooltip("Nhãn + CẤP <n> cạnh thanh nâng cấp.")]
        [SerializeField] private TextMeshProUGUI m_LevelNext;

        [Tooltip("Giá nâng cấp in trên nút NÂNG CẤP.")]
        [SerializeField] private TextMeshProUGUI m_UpgradeCost;

        [Tooltip("Số ngọc ma pháp hiện có, in đè lên ô số nướng trong header.")]
        [SerializeField] private TextMeshProUGUI m_GemCount;

        [Tooltip("Phần tô của thanh cấp độ, chạy theo cấp hiện tại.")]
        [SerializeField] private Image m_LevelBarFill;

        [Header("Kỹ năng")]
        [Tooltip("Tên 4 kỹ năng, đúng thứ tự 01 đến 04.")]
        [SerializeField] private TextMeshProUGUI[] m_SkillNames = new TextMeshProUGUI[4];

        [Tooltip("Mô tả 4 kỹ năng, đúng thứ tự 01 đến 04.")]
        [SerializeField] private TextMeshProUGUI[] m_SkillDescriptions = new TextMeshProUGUI[4];

        [Tooltip("Ảnh 4 ô kỹ năng, đúng thứ tự 01 đến 04.")]
        [SerializeField] private Image[] m_SkillIcons = new Image[4];

        [Tooltip("36 icon kỹ năng tùy chọn: 6 xe x 2 tab x 3 lựa chọn, đúng thứ tự TankRoster.")]
        [SerializeField] private Sprite[] m_OptionSkillIcons = new Sprite[0];

        [Header("Chỉ số")]
        [Tooltip("8 giá trị chỉ số bên phải, đúng thứ tự ATK, HP, EN, DEF, SPD, CDR, DMG, PEN.")]
        [SerializeField] private TextMeshProUGUI[] m_StatValues = new TextMeshProUGUI[8];

        [Tooltip("8 thanh chỉ số động, chiều dài chạy theo chỉ số của xe đang chọn.")]
        [SerializeField] private Image[] m_StatBars = new Image[8];

        [Header("Nút")]
        [SerializeField] private Button m_BackButton;
        [SerializeField] private Button m_DeployButton;
        [SerializeField] private Button m_UpgradeButton;
        [SerializeField] private Button m_OptionButton1;
        [SerializeField] private Button m_OptionButton2;
        [SerializeField] private Button m_AccessoryButton;

        [Tooltip("Nút PHÂN RÃ mở màn phân rã phụ kiện.")]
        [SerializeField] private Button m_SalvageButton;

        [Header("Danh sách xe")]
        [Tooltip("Vùng nội dung cuộn chứa các hàng xe; chiều cao tự giãn theo số xe.")]
        [SerializeField] private RectTransform m_RowContent;

        [Tooltip("Hàng xe mẫu (ẩn sẵn trong scene) để nhân bản cho từng xe.")]
        [SerializeField] private GarageTankRow m_RowTemplate;

        [Tooltip("Thanh cuộn dọc của danh sách xe.")]
        [SerializeField] private ScrollRect m_TankScroll;

        [Tooltip("Khoảng hở dọc giữa hai hàng xe liên tiếp, tính bằng pixel thiết kế.")]
        [SerializeField] private float m_RowSpacing = 12f;

        [Tooltip("Lề trên của hàng xe đầu tiên so với vùng nội dung.")]
        [SerializeField] private float m_RowPadding = 0f;

        [Header("Phụ kiện trang bị")]
        [Tooltip("Panel hiển thị ảnh, tên và hiệu ứng của phụ kiện đang trang bị.")]
        [SerializeField] private GarageAccessoryPanel m_AccessoryPanel;

        [Header("Scene đích")]
        [Tooltip("Tên scene quay lại khi bấm QUAY LẠI.")]
        [SerializeField] private string m_BackSceneName = "MainMenu";

        [Tooltip("Tên scene chọn kỹ năng tùy chọn.")]
        [SerializeField] private string m_PickerSceneName = "SkillPicker";

        [Tooltip("Tên scene chọn phụ kiện trang bị.")]
        [SerializeField] private string m_AccessorySceneName = "GarageAccessory";

        [Tooltip("Scene phân rã phụ kiện, nạp chồng lên Nhà xe.")]
        [SerializeField] private string m_SalvageSceneName = "AccessorySalvage";

        private const string SkillChoice = "3 LỰA CHỌN  ›";

        private readonly float[] m_StatMaxima = new float[StatBarModel.Count];
        private readonly List<GarageTankRow> m_Rows = new List<GarageTankRow>();

        private Canvas m_Canvas;
        private int m_SelectedIndex = -1;
        private bool m_PickerOpen;
        private bool m_AccessoryOpen;
        private bool m_AccessoryLoaded;
        private bool m_SalvageOpen;
        private bool m_SalvageLoaded;

        /// <summary>Xe đang được chọn trong danh sách.</summary>
        public int SelectedIndex => m_SelectedIndex;

        private void Awake()
        {
            m_Canvas = GetComponent<Canvas>();
            if (m_Canvas == null)
            {
                m_Canvas = GetComponentInParent<Canvas>();
            }

            if (m_HeroImage != null)
            {
                // Vị trí gốc do builder đặt; mỗi xe chỉ cộng thêm độ dời cân khối tâm của riêng nó.
                m_HeroBasePosition = m_HeroImage.rectTransform.anchoredPosition;
            }

            RefreshGemCount();
            BuildTankRows();
            ComputeStatMaxima();
            WireButtons();
            SelectInitial();
        }

        private void OnGUI()
        {
            if (m_PickerOpen || m_AccessoryOpen || m_SalvageOpen)
            {
                return;
            }

            Event current = Event.current;
            if (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                GoBack();
            }
        }

        private void Update()
        {
            RefreshGemCount();
            SyncAdditivePicker();
            if (m_AccessoryOpen)
            {
                SyncAdditiveAccessory();
            }

            if (m_SalvageOpen)
            {
                SyncAdditiveSalvage();
            }
        }

        /// <summary>Gắn sự kiện bấm cho toàn bộ nút của màn hình.</summary>
        private void WireButtons()
        {
            Wire(m_BackButton, GoBack);
            Wire(m_DeployButton, Deploy);
            Wire(m_UpgradeButton, TryUpgrade);
            Wire(m_OptionButton1, () => OpenPicker(0));
            Wire(m_OptionButton2, () => OpenPicker(1));
            Wire(m_AccessoryButton, OpenAccessory);
            Wire(m_SalvageButton, OpenSalvage);
        }

        private static void Wire(Button button, UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        /// <summary>Chọn xe lúc mở màn: xe đã lưu, nhưng bỏ qua xe còn khoá.</summary>
        private void SelectInitial()
        {
            if (m_Roster == null || m_Roster.Count == 0)
            {
                return;
            }

            int count = m_Roster.Tanks.Count;
            int start = SaveSystem.Instance.LastSelectedTankIndex;
            if (start < 0 || start >= count)
            {
                start = 0;
            }

            if (IsTankLocked(start))
            {
                int unlocked = FirstUnlockedIndex();
                if (unlocked >= 0)
                {
                    start = unlocked;
                }
            }

            Select(start);
        }

        private int FirstUnlockedIndex()
        {
            IReadOnlyList<TankDefinition> tanks = m_Roster != null ? m_Roster.Tanks : null;
            if (tanks == null)
            {
                return -1;
            }

            for (int i = 0; i < tanks.Count; i++)
            {
                if (!IsTankLocked(i))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Xe còn khoá khi ải mở khoá của nó chưa được mở trong tiến trình.</summary>
        private bool IsTankLocked(int index)
        {
            if (m_Roster == null)
            {
                return false;
            }

            IReadOnlyList<TankDefinition> tanks = m_Roster.Tanks;
            if (index < 0 || index >= tanks.Count)
            {
                return false;
            }

            TankDefinition definition = tanks[index];
            return definition != null
                && !string.IsNullOrEmpty(definition.UnlockStageCode)
                && !SaveSystem.Instance.IsStageUnlocked(definition.UnlockStageCode);
        }

        /// <summary>Dựng danh sách xe từ TankRoster: mỗi xe một hàng, nội dung tự giãn để cuộn được.</summary>
        private void BuildTankRows()
        {
            if (m_RowContent == null || m_RowTemplate == null || m_Roster == null)
            {
                return;
            }

            m_RowTemplate.gameObject.SetActive(false);

            IReadOnlyList<TankDefinition> tanks = m_Roster.Tanks;
            TankRowSource[] sources = new TankRowSource[tanks.Count];
            for (int i = 0; i < sources.Length; i++)
            {
                TankDefinition definition = tanks[i];
                sources[i] = new TankRowSource(
                    definition != null ? definition.name : string.Empty,
                    definition != null ? definition.TankName : string.Empty,
                    definition != null ? definition.RolePrimary : string.Empty,
                    definition != null ? definition.RoleSecondary : string.Empty,
                    definition != null ? definition.UnlockStageCode : string.Empty);
            }

            TankRow[] rows = TankListModel.Build(sources, LevelOf, SaveSystem.Instance.IsStageUnlocked);

            RectTransform templateRect = m_RowTemplate.transform as RectTransform;
            float rowHeight = templateRect != null ? templateRect.rect.height : 108f;
            float pitch = rowHeight + m_RowSpacing;

            for (int i = 0; i < rows.Length; i++)
            {
                GarageTankRow row = Instantiate(m_RowTemplate, m_RowContent);
                RectTransform rect = row.transform as RectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, rowHeight);
                rect.anchoredPosition = new Vector2(0f, -(m_RowPadding + pitch * i));

                TankDefinition definition = tanks[i];
                Color accent = definition != null ? definition.RosterAccent : new Color32(0xFF, 0x7A, 0x18, 0xFF);
                TankRow data = rows[i];
                row.Bind(data.DisplayName, data.RoleText, data.LevelText, data.UnlockText, data.BarFraction, data.IsLocked,
                    definition != null ? definition.RosterIcon : null, accent);
                row.gameObject.SetActive(true);

                int index = i;
                if (row.Button != null)
                {
                    row.Button.onClick.AddListener(() => OnRowClicked(index));
                }

                m_Rows.Add(row);
            }

            m_RowContent.sizeDelta = new Vector2(m_RowContent.sizeDelta.x, m_RowPadding * 2f + pitch * rows.Length);

            if (m_TankScroll != null)
            {
                m_TankScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>Hàng còn khoá thì không đổi xe, chỉ rung nhẹ về mặt dữ liệu (không đổi gì).</summary>
        private void OnRowClicked(int index)
        {
            if (index < 0 || index >= m_Rows.Count)
            {
                return;
            }

            if (m_Rows[index].IsLocked)
            {
                return;
            }

            Select(index);
        }

        /// <summary>Tài liệu hoá lại hàng xe sau khi nâng cấp để cấp mới hiện ngay.</summary>
        private void RefreshTankRows()
        {
            for (int i = 0; i < m_Rows.Count; i++)
            {
                if (m_Rows[i] != null)
                {
                    Destroy(m_Rows[i].gameObject);
                }
            }

            m_Rows.Clear();
            BuildTankRows();
        }

        /// <summary>Tính chỉ số lớn nhất của cả bộ xe ở cấp tối đa để chuẩn hoá 8 thanh chỉ số.</summary>
        private void ComputeStatMaxima()
        {
            for (int i = 0; i < m_StatMaxima.Length; i++)
            {
                m_StatMaxima[i] = 0f;
            }

            if (m_Roster == null)
            {
                return;
            }

            IReadOnlyList<TankDefinition> tanks = m_Roster.Tanks;
            for (int i = 0; i < tanks.Count; i++)
            {
                TankDefinition definition = tanks[i];
                if (definition == null)
                {
                    continue;
                }

                float[] values = StatsToArray(definition.GetStatsForLevel(TankDefinition.MaxLevel));
                for (int s = 0; s < m_StatMaxima.Length && s < values.Length; s++)
                {
                    if (values[s] > m_StatMaxima[s])
                    {
                        m_StatMaxima[s] = values[s];
                    }
                }
            }
        }

        /// <summary>Chọn một xe: cập nhật ảnh, tên, cấp, thanh cấp, 4 kỹ năng và 8 chỉ số.</summary>
        public void Select(int index)
        {
            if (m_Roster == null || m_Roster.Count == 0)
            {
                return;
            }

            IReadOnlyList<TankDefinition> tanks = m_Roster.Tanks;
            int clamped = Mathf.Clamp(index, 0, tanks.Count - 1);
            TankDefinition definition = tanks[clamped];
            if (definition == null)
            {
                return;
            }

            m_SelectedIndex = clamped;
            SaveSystem.Instance.LastSelectedTankIndex = clamped;

            for (int i = 0; i < m_Rows.Count; i++)
            {
                m_Rows[i].SetSelected(i == clamped);
            }

            int level = LevelOf(definition.name);
            int maxLevel = TankDefinition.MaxLevel;
            int nextLevel = Mathf.Min(level + 1, maxLevel);
            int upgradeCost = level >= maxLevel ? 0 : TankUpgradeUtility.GetCostForNextLevel(level);

            if (m_HeroImage != null)
            {
                m_HeroImage.sprite = definition.Portrait;
                m_HeroImage.enabled = definition.Portrait != null;
                m_HeroImage.rectTransform.anchoredPosition = m_HeroBasePosition + definition.PortraitOffset;
            }

            SetText(m_HeroCaption, "CURRENT UNIT  /  " + definition.TankName);
            SetText(m_HeroName, definition.TankName);
            SetText(m_HeroRole, RoleText(definition));
            SetText(m_HeroLevel, level.ToString("00"));
            SetText(m_LevelValue, level.ToString("00"));
            SetText(m_LevelNext, level >= maxLevel ? "ĐÃ TỐI ĐA" : "+ CẤP " + nextLevel);
            SetText(m_UpgradeCost, upgradeCost.ToString());
            RefreshGemCount();

            if (m_LevelBarFill != null)
            {
                m_LevelBarFill.fillAmount = Mathf.Clamp01(level / (float)maxLevel);
            }

            if (m_UpgradeButton != null)
            {
                m_UpgradeButton.interactable = upgradeCost > 0 && SaveSystem.Instance.Gems >= upgradeCost;
            }

            ApplySkills(clamped, definition);
            ApplyStats(definition, level);

            if (m_AccessoryPanel != null)
            {
                m_AccessoryPanel.Refresh();
            }
        }

        /// <summary>In số ngọc ma pháp hiện có; chỉ cập nhật khi giá trị đổi để không sinh rác mỗi khung hình.</summary>
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
            SetText(m_GemCount, gems.ToString());
        }

        /// <summary>Đổ 4 thẻ kỹ năng: 2 kỹ năng gốc của xe và 2 kỹ năng tùy chọn đang chọn.</summary>
        private void ApplySkills(int tankIndex, TankDefinition definition)
        {
            SkillSlotSource[] sources = new SkillSlotSource[SkillSlotModel.SlotCount];
            for (int slot = 0; slot < sources.Length; slot++)
            {
                if (slot < 2)
                {
                    sources[slot] = new SkillSlotSource(
                        definition.GetSkillName(slot),
                        definition.GetSkillDescription(slot),
                        string.Empty);
                    continue;
                }

                int tab = slot - 2;
                int optionIndex = SaveSystem.Instance.GetOptionSkill(tankIndex, tab);
                SkillCatalog.Option option = SkillCatalog.Get(tankIndex, tab, optionIndex);
                sources[slot] = new SkillSlotSource(
                    option != null ? option.Name : string.Empty,
                    SkillChoice,
                    string.Empty);
            }

            SkillSlot[] slots = SkillSlotModel.Build(sources);
            for (int i = 0; i < slots.Length; i++)
            {
                if (m_SkillNames != null && i < m_SkillNames.Length)
                {
                    SetText(m_SkillNames[i], slots[i].NameText);
                }

                if (m_SkillDescriptions != null && i < m_SkillDescriptions.Length)
                {
                    SetText(m_SkillDescriptions[i], slots[i].DescText);
                }

                if (m_SkillIcons == null || i >= m_SkillIcons.Length || m_SkillIcons[i] == null)
                {
                    continue;
                }

                Sprite icon = i < 2 ? definition.GetSkillIcon(i) : OptionIcon(tankIndex, i - 2);
                if (icon == null)
                {
                    icon = definition.GetSkillIcon(i);
                }

                m_SkillIcons[i].sprite = icon;
                m_SkillIcons[i].enabled = icon != null;
            }
        }

        /// <summary>Icon của kỹ năng tùy chọn đang dùng, lấy từ bảng 36 icon gắn sẵn trong scene.</summary>
        private Sprite OptionIcon(int tankIndex, int tab)
        {
            if (m_OptionSkillIcons == null || m_OptionSkillIcons.Length == 0)
            {
                return null;
            }

            int optionIndex = SaveSystem.Instance.GetOptionSkill(tankIndex, tab);
            int index = tankIndex * (SkillCatalog.TabCount * SkillCatalog.OptionCount)
                + tab * SkillCatalog.OptionCount + optionIndex;
            if (index < 0 || index >= m_OptionSkillIcons.Length)
            {
                return null;
            }

            return m_OptionSkillIcons[index];
        }

        /// <summary>Đổ số và thanh của 8 chỉ số theo cấp hiện tại của xe.</summary>
        private void ApplyStats(TankDefinition definition, int level)
        {
            float[] statValues = StatsToArray(definition.GetStatsForLevel(level));
            float[] barFractions = StatBarModel.Build(statValues, m_StatMaxima);

            for (int i = 0; i < m_StatValues.Length; i++)
            {
                SetText(m_StatValues[i], i < statValues.Length ? Mathf.RoundToInt(statValues[i]).ToString() : "-");
            }

            for (int i = 0; i < m_StatBars.Length; i++)
            {
                if (m_StatBars[i] == null)
                {
                    continue;
                }

                m_StatBars[i].fillAmount = i < barFractions.Length ? barFractions[i] : 0f;
            }
        }

        /// <summary>Nâng cấp xe đang chọn: trừ ngọc theo bảng giá rồi tăng cấp đã lưu.</summary>
        private void TryUpgrade()
        {
            if (m_Roster == null || m_SelectedIndex < 0 || m_SelectedIndex >= m_Roster.Tanks.Count)
            {
                return;
            }

            TankDefinition definition = m_Roster.Tanks[m_SelectedIndex];
            if (definition == null)
            {
                return;
            }

            int level = LevelOf(definition.name);
            if (level >= TankDefinition.MaxLevel)
            {
                return;
            }

            int cost = TankUpgradeUtility.GetCostForNextLevel(level);
            if (cost <= 0 || !SaveSystem.Instance.SpendGems(cost))
            {
                return;
            }

            SaveSystem.Instance.SetTankLevel(m_SelectedIndex, level + 1);
            RefreshTankRows();
            Select(m_SelectedIndex);
        }

        /// <summary>Chuỗi vai trò hiển thị ở khu trưng bày: chính, hoặc chính / phụ.</summary>
        private static string RoleText(TankDefinition definition)
        {
            string primary = definition.RolePrimary;
            string secondary = definition.RoleSecondary;
            if (string.IsNullOrEmpty(primary))
            {
                return string.IsNullOrEmpty(secondary) ? string.Empty : secondary;
            }

            return string.IsNullOrEmpty(secondary) ? primary : primary + "  /  " + secondary;
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        /// <summary>Đổi một bộ chỉ số sang mảng 8 phần tử theo thứ tự: ATK, HP, EN, DEF, SPD, CDR, DMG, PEN.</summary>
        private static float[] StatsToArray(TankStats stats)
        {
            float[] values = new float[StatBarModel.Count];
            if (stats == null)
            {
                return values;
            }

            values[0] = stats.Attack;
            values[1] = stats.MaxHealth;
            values[2] = stats.MaxEnergy;
            values[3] = stats.Defense;
            values[4] = stats.MoveSpeed;
            values[5] = stats.CooldownReduction * 100f;
            values[6] = stats.DamageRate * 100f;
            values[7] = stats.ArmorPenetration * 100f;
            return values;
        }

        /// <summary>Tìm asset xe theo tên, phục vụ tra cấp khởi điểm.</summary>
        private TankDefinition FindTank(string tankKey)
        {
            if (m_Roster == null || string.IsNullOrEmpty(tankKey))
            {
                return null;
            }

            IReadOnlyList<TankDefinition> tanks = m_Roster.Tanks;
            for (int i = 0; i < tanks.Count; i++)
            {
                TankDefinition definition = tanks[i];
                if (definition != null && definition.name == tankKey)
                {
                    return definition;
                }
            }

            return null;
        }

        /// <summary>Cấp hiện tại của xe: ưu tiên tiến trình đã lưu, chưa có thì lấy cấp khởi điểm của asset.</summary>
        private int LevelOf(string tankKey)
        {
            if (m_Roster != null && !string.IsNullOrEmpty(tankKey))
            {
                IReadOnlyList<TankDefinition> tanks = m_Roster.Tanks;
                for (int i = 0; i < tanks.Count; i++)
                {
                    if (tanks[i] != null && tanks[i].name == tankKey)
                    {
                        return SaveSystem.Instance.GetTankLevel(i);
                    }
                }
            }

            TankDefinition definition = FindTank(tankKey);
            return definition != null ? definition.StartingLevel : 1;
        }

        /// <summary>Khi Picker được nạp chồng, tạm ẩn Garage; đóng Picker thì hiện lại và đọc lựa chọn mới.</summary>
        private void SyncAdditivePicker()
        {
            if (!GarageFlow.OpenedFromLobby)
            {
                return;
            }

            bool pickerLoaded = SceneManager.GetSceneByName(m_PickerSceneName).isLoaded;
            if (pickerLoaded == m_PickerOpen)
            {
                return;
            }

            m_PickerOpen = pickerLoaded;
            if (m_Canvas != null)
            {
                m_Canvas.enabled = !pickerLoaded;
            }

            if (!pickerLoaded && m_SelectedIndex >= 0)
            {
                Select(m_SelectedIndex);
            }
        }

        /// <summary>Mở màn chọn phụ kiện dạng cộng thêm để giữ nguyên Nhà xe và phòng mạng.</summary>
        private void OpenAccessory()
        {
            OpenOverlay(m_AccessorySceneName, ref m_AccessoryOpen, ref m_AccessoryLoaded);
        }

        /// <summary>Mở màn phân rã phụ kiện dạng cộng thêm, cùng cơ chế với màn chọn phụ kiện.</summary>
        private void OpenSalvage()
        {
            OpenOverlay(m_SalvageSceneName, ref m_SalvageOpen, ref m_SalvageLoaded);
        }

        /// <summary>Nạp chồng một scene phủ toàn màn hình và tạm ẩn giao diện Nhà xe.</summary>
        private void OpenOverlay(string sceneName, ref bool open, ref bool loaded)
        {
            if (open)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning("[Garage] Scene chưa có trong Build Settings: " + sceneName);
                return;
            }

            open = true;
            loaded = false;
            if (m_Canvas != null)
            {
                m_Canvas.enabled = false;
            }

            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }

        /// <summary>Chờ scene phủ nạp xong rồi chờ nó đóng; trả về true đúng lúc scene vừa đóng.</summary>
        private bool SyncOverlay(string sceneName, ref bool open, ref bool loaded)
        {
            if (!open)
            {
                return false;
            }

            Scene overlay = SceneManager.GetSceneByName(sceneName);
            if (!loaded)
            {
                if (overlay.isLoaded)
                {
                    loaded = true;
                }

                return false;
            }

            if (overlay.isLoaded)
            {
                return false;
            }

            open = false;
            loaded = false;
            if (m_Canvas != null)
            {
                m_Canvas.enabled = true;
            }

            return true;
        }

        /// <summary>Khi màn phụ kiện đóng lại thì bật lại giao diện Nhà xe và làm mới panel phụ kiện.</summary>
        private void SyncAdditiveAccessory()
        {
            if (SyncOverlay(m_AccessorySceneName, ref m_AccessoryOpen, ref m_AccessoryLoaded) && m_AccessoryPanel != null)
            {
                m_AccessoryPanel.Refresh();
            }
        }

        /// <summary>Khi màn phân rã đóng lại thì làm mới số ngọc và panel phụ kiện vì tồn kho đã đổi.</summary>
        private void SyncAdditiveSalvage()
        {
            if (!SyncOverlay(m_SalvageSceneName, ref m_SalvageOpen, ref m_SalvageLoaded))
            {
                return;
            }

            RefreshGemCount();
            if (m_AccessoryPanel != null)
            {
                m_AccessoryPanel.Refresh();
            }
        }

        /// <summary>Mở màn chọn kỹ năng cho tab tương ứng của xe đang chọn.</summary>
        private void OpenPicker(int tabIndex)
        {
            if (m_SelectedIndex < 0)
            {
                return;
            }

            SaveSystem.Instance.RequestSkillPicker(m_SelectedIndex, tabIndex);
            if (GarageFlow.OpenedFromLobby)
            {
                if (SceneManager.GetSceneByName(m_PickerSceneName).isLoaded)
                {
                    return;
                }

                m_PickerOpen = true;
                if (m_Canvas != null)
                {
                    m_Canvas.enabled = false;
                }

                SceneManager.LoadSceneAsync(m_PickerSceneName, LoadSceneMode.Additive);
                return;
            }

            LoadScene(m_PickerSceneName);
        }

        private void GoBack()
        {
            if (GarageFlow.OpenedFromLobby)
            {
                SceneManager.UnloadSceneAsync(gameObject.scene);
                return;
            }

            LoadScene(m_BackSceneName);
        }

        /// <summary>XÁC NHẬN RA TRẬN chỉ chốt xe đang chọn rồi quay về Main Menu.</summary>
        private void Deploy()
        {
            GoBack();
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
                Debug.LogWarning("[Garage] Scene chưa có trong Build Settings: " + sceneName);
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}
