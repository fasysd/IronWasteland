using System;
using System.Linq;
using TMPro;
using IronWasteland.Save;
using IronWasteland.Tanks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronWasteland.UI
{
    /// <summary>
    /// Điều khiển màn hình Garage (Quản lý xe tăng và phụ kiện).
    /// Người chơi bấm vào thẻ xe trong danh sách bên trái để đổi xe đang chọn:
    /// ảnh trưng bày, cấp độ, kỹ năng và 8 chỉ số bên phải đều cập nhật theo xe đó.
    /// Dữ liệu xe (cấp độ, kỹ năng tùy chọn, phụ kiện) đọc từ SaveSystem (JSON).
    /// Chỉ số hiển thị tính từ TankDefinition.GetStatsForLevel(level).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarageController : MonoBehaviour
    {
        [Header("Dữ liệu xe (TankDefinition assets)")]
        [Tooltip("Danh sách TankDefinition hiển thị trong Garage, đúng thứ tự thẻ từ trên xuống.")]
        [SerializeField] private TankDefinition[] m_TankDefinitions = new TankDefinition[0];

        [Header("Tham chiếu giao diện")]
        [Tooltip("Ảnh lớn của xe đang chọn ở khu trưng bày giữa màn hình.")]
        [SerializeField] private Image m_HeroImage;

        [Tooltip("Khung tím ĐANG CHỌN trong danh sách xe bên trái.")]
        [SerializeField] private RectTransform m_SelectionFrame;

        [Tooltip("Tên xe lớn ở khu trưng bày.")]
        [SerializeField] private TextMeshProUGUI m_HeroName;

        [Tooltip("Dòng vai trò dưới tên xe lớn.")]
        [SerializeField] private TextMeshProUGUI m_HeroRole;

        [Tooltip("Dòng CURRENT UNIT / <tên xe> phía trên khung tròn.")]
        [SerializeField] private TextMeshProUGUI m_HeroCaption;

        [Tooltip("Số cấp lớn ở góc phải khung tròn.")]
        [SerializeField] private TextMeshProUGUI m_HeroLevel;

        [Tooltip("Số cấp hiện tại ở thanh nâng cấp.")]
        [SerializeField] private TextMeshProUGUI m_LevelValue;

        [Tooltip("Nhãn + CẤP <n> cạnh thanh nâng cấp.")]
        [SerializeField] private TextMeshProUGUI m_LevelNext;

        [Tooltip("Số nguyên liệu nâng cấp trên nút NÂNG CẤP.")]
        [SerializeField] private TextMeshProUGUI m_UpgradeCost;

        [Tooltip("Tên 4 kỹ năng, đúng thứ tự 01 đến 04.")]
        [SerializeField] private TextMeshProUGUI[] m_SkillNames = new TextMeshProUGUI[4];

        [Tooltip("Mô tả 4 kỹ năng, đúng thứ tự 01 đến 04.")]
        [SerializeField] private TextMeshProUGUI[] m_SkillDescriptions = new TextMeshProUGUI[4];

        [Tooltip("Ảnh 4 ô kỹ năng, đúng thứ tự 01 đến 04.")]
        [SerializeField] private Image[] m_SkillIcons = new Image[4];

        [Tooltip("8 giá trị chỉ số bên phải, đúng thứ tự ATK, HP, EN, DEF, SPD, CDR, DMG, PEN.")]
        [SerializeField] private TextMeshProUGUI[] m_StatValues = new TextMeshProUGUI[8];

        [Header("Vùng bấm theo toạ độ thiết kế (gốc trên-trái, hệ 1920x1080)")]
        [Tooltip("Thẻ xe đầu tiên: x, y đỉnh thẻ, rộng, cao.")]
        [SerializeField] private Rect m_CardArea = new Rect(71f, 244f, 374f, 108f);

        [Tooltip("Khoảng cách dọc giữa hai thẻ xe liên tiếp.")]
        [SerializeField] private float m_CardPitch = 120f;

        [Tooltip("Vùng bấm nút NÂNG CẤP.")]
        [SerializeField] private Rect m_UpgradeArea = new Rect(1186f, 690f, 150f, 60f);

        [Tooltip("Vùng bấm nút QUAY LẠI ở góc trên trái.")]
        [SerializeField] private Rect m_BackArea = new Rect(72f, 94f, 168f, 50f);

        [Tooltip("Vùng bấm nút XÁC NHẬN RA TRẬN.")]
        [SerializeField] private Rect m_DeployArea = new Rect(1291f, 889f, 558f, 88f);

        [Tooltip("Vùng bấm ô kỹ năng TÙY CHỌN 1 (ô thứ 3 trong hàng kỹ năng).")]
        [SerializeField] private Rect m_OptionArea1 = new Rect(921.5f, 832f, 96f, 96f);

        [Tooltip("Vùng bấm ô kỹ năng TÙY CHỌN 2 (ô thứ 4 trong hàng kỹ năng).")]
        [SerializeField] private Rect m_OptionArea2 = new Rect(1124.5f, 832f, 96f, 96f);

        [Tooltip("Toạ độ đỉnh của khung ĐANG CHỌN khi chọn thẻ đầu tiên (cách đỉnh thẻ 2 px).")]
        [SerializeField] private Vector2 m_FrameDesignPosition = new Vector2(32f, 242f);

        [Header("Scene đích")]
        [Tooltip("Tên scene quay lại khi bấm QUAY LẠI.")]
        [SerializeField] private string m_BackSceneName = "MainMenu";

        [Tooltip("Tên scene chọn kỹ năng tùy chọn.")]
        [SerializeField] private string m_PickerSceneName = "SkillPicker";

        [Tooltip("Tên scene chọn phụ kiện trang bị.")]
        [SerializeField] private string m_AccessorySceneName = "GarageAccessory";

        [Header("Phụ kiện trang bị")]
        [Tooltip("Vùng bấm panel PHỤ KIỆN TRANG BỊ ở góc dưới bên phải.")]
        [SerializeField] private Rect m_AccessoryArea = new Rect(1291f, 675f, 558f, 202f);

        [Tooltip("Panel hiển thị ảnh, tên và hiệu ứng của phụ kiện đang trang bị.")]
        [SerializeField] private GarageAccessoryPanel m_AccessoryPanel;

        private const float DesignHeight = 1080f;

        private int m_SelectedIndex = -1;
        private Canvas m_Canvas;
        private Vector2 m_PointerScreen;
        private bool m_HasGuiPointer;
        private bool m_GuiPointerDown;
        private bool m_GuiPointerUp;
        private bool m_Pressed;
        private bool m_PickerOpen;
        private bool m_AccessoryOpen;
        private bool m_AccessoryLoaded;

        /// <summary>Xe đang được chọn (chỉ đọc, dùng cho các tính năng nối sau này).</summary>
        public int SelectedIndex => m_SelectedIndex;

        private void Awake()
        {
            m_Canvas = GetComponent<Canvas>();
            if (m_Canvas == null)
            {
                m_Canvas = GetComponentInParent<Canvas>();
            }

            if ((m_TankDefinitions == null || m_TankDefinitions.Length == 0) && TankRegistry.TankCount > 0)
            {
                m_TankDefinitions = new TankDefinition[TankRegistry.TankCount];
                for (int i = 0; i < TankRegistry.TankCount; i++)
                {
                    m_TankDefinitions[i] = TankRegistry.GetDefinition(i);
                }
            }

            if (m_TankDefinitions != null && m_TankDefinitions.Length > 0 && SaveSystem.Instance != null)
            {
                int start = SaveSystem.Instance.LastSelectedTankIndex;
                if (start < 0 || start >= m_TankDefinitions.Length) start = 0;
                Select(start);
            }
        }

        private void Update()
        {
            SyncAdditivePicker();
            if (m_PickerOpen)
            {
                return;
            }

            if (m_AccessoryOpen)
            {
                SyncAdditiveAccessory();
                return;
            }

            if (m_GuiPointerDown)
            {
                m_GuiPointerDown = false;
                m_Pressed = true;
            }

            if (m_GuiPointerUp)
            {
                m_GuiPointerUp = false;
                if (m_Pressed)
                {
                    m_Pressed = false;
                    HandleClick(DesignPointerPosition(PointerPosition));
                }
            }
        }

        private void OnGUI()
        {
            if (m_PickerOpen || m_AccessoryOpen)
            {
                return;
            }

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
                        m_GuiPointerDown = true;
                    }

                    break;
                case EventType.MouseUp:
                    if (current.button == 0)
                    {
                        m_PointerScreen = ToScreenPoint(current.mousePosition);
                        m_HasGuiPointer = true;
                        m_GuiPointerUp = true;
                    }

                    break;
                case EventType.KeyDown:
                    if (current.keyCode == KeyCode.Escape)
                    {
                        GoBack();
                    }

                    break;
            }
        }

        /// <summary>Đổi điểm chuột của IMGUI (gốc trên-trái) sang toạ độ màn hình của Unity (gốc dưới-trái).</summary>
        private static Vector2 ToScreenPoint(Vector2 guiPosition)
        {
            return new Vector2(guiPosition.x, Screen.height - guiPosition.y);
        }

        /// <summary>Đổi điểm chuột trên màn hình sang toạ độ thiết kế 1920x1080 (gốc trên-trái).</summary>
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

        /// <summary>
        /// Khi mở từ lobby, Picker được nạp chồng và Garage tạm ẩn canvas.
        /// Đóng Picker thì hiện lại Garage và đọc lại lựa chọn kỹ năng vừa đổi.
        /// </summary>
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

        /// <summary>Xử lý cú bấm chuột tại một điểm trong hệ toạ độ thiết kế.</summary>
        private void HandleClick(Vector2 designPosition)
        {
            if (m_TankDefinitions != null)
            {
                for (int i = 0; i < m_TankDefinitions.Length; i++)
                {
                    Rect card = new Rect(m_CardArea.x, m_CardArea.y + m_CardPitch * i, m_CardArea.width, m_CardArea.height);
                    if (card.Contains(designPosition))
                    {
                        Select(i);
                        return;
                    }
                }
            }

            if (m_OptionArea1.Contains(designPosition))
            {
                OpenPicker(0);
                return;
            }

            if (m_OptionArea2.Contains(designPosition))
            {
                OpenPicker(1);
                return;
            }

            if (m_UpgradeArea.Contains(designPosition))
            {
                TryUpgrade();
                return;
            }

            if (m_AccessoryArea.Contains(designPosition))
            {
                OpenAccessory();
                return;
            }

            if (m_BackArea.Contains(designPosition))
            {
                GoBack();
                return;
            }

            if (m_DeployArea.Contains(designPosition))
            {
                Deploy();
            }
        }

        /// <summary>Chọn một xe: cập nhật khung ĐANG CHỌN, ảnh trưng bày, tên, cấp, kỹ năng và chỉ số.</summary>
        public void Select(int index)
        {
            if (m_TankDefinitions == null || m_TankDefinitions.Length == 0 || SaveSystem.Instance == null)
            {
                return;
            }

            int clamped = Mathf.Clamp(index, 0, m_TankDefinitions.Length - 1);
            m_SelectedIndex = clamped;
            SaveSystem.Instance.LastSelectedTankIndex = clamped;
            TankDefinition def = m_TankDefinitions[clamped];
            int level = SaveSystem.Instance.GetTankLevel(clamped);
            bool owned = level > 0;

            if (m_SelectionFrame != null)
            {
                float top = m_FrameDesignPosition.y + m_CardPitch * clamped;
                float height = m_SelectionFrame.sizeDelta.y;
                m_SelectionFrame.anchoredPosition = new Vector2(m_FrameDesignPosition.x, DesignHeight - top - height);
            }

            // Hiển thị tên từ TankDefinition
            SetText(m_HeroCaption, "CURRENT UNIT  /  " + def.TankName);
            SetText(m_HeroName, def.TankName);
            SetText(m_HeroRole, owned ? "ĐÃ SỞ HỮU" : "CHƯA SỞ HỮU");

            if (owned)
            {
                SetText(m_HeroLevel, level.ToString("00"));
                SetText(m_LevelValue, level.ToString("00"));
                int nextLevel = level + 1;
                if (nextLevel <= TankDefinition.MaxLevel)
                {
                    SetText(m_LevelNext, "+ CẤP " + nextLevel);
                    int cost = TankUpgradeUtility.GetCostForNextLevel(level);
                    SetText(m_UpgradeCost, cost.ToString());
                }
                else
                {
                    SetText(m_LevelNext, "TỐI ĐA");
                    SetText(m_UpgradeCost, "-");
                }
            }
            else
            {
                SetText(m_HeroLevel, "--");
                SetText(m_LevelValue, "--");
                SetText(m_LevelNext, "MUA XE");
                SetText(m_UpgradeCost, "-");
            }

            // Tên kỹ năng từ TankDefinition
            for (int i = 0; i < m_SkillNames.Length; i++)
            {
                SetText(m_SkillNames[i], def.GetSkillName(i));
            }

            // Mô tả kỹ năng (để trống nếu TankDefinition không có)
            for (int i = 0; i < m_SkillDescriptions.Length; i++)
            {
                SetText(m_SkillDescriptions[i], string.Empty);
            }

            // Icon kỹ năng từ TankDefinition
            for (int i = 0; i < m_SkillIcons.Length; i++)
            {
                if (m_SkillIcons[i] == null) continue;
                Sprite icon = def.GetSkillIcon(i);
                if (icon != null) m_SkillIcons[i].sprite = icon;
            }

            // Hai ô cuối là kỹ năng tùy chọn: lấy từ SaveSystem.
            for (int tab = 0; tab < SkillCatalog.TabCount; tab++)
            {
                int slot = tab + 2;
                int optionIndex = SaveSystem.Instance.GetOptionSkill(clamped, tab);
                SkillCatalog.Option option = SkillCatalog.Get(clamped, tab, optionIndex);
                if (m_SkillNames != null && slot < m_SkillNames.Length)
                {
                    SetText(m_SkillNames[slot], option.Name);
                }

                if (m_SkillIcons != null && slot < m_SkillIcons.Length && m_SkillIcons[slot] != null)
                {
                    // TankDefinition chi co icon co ban cho 4 slot; su dung icon slot ung dung.
                    Sprite icon = def.GetSkillIcon(slot);
                    if (icon != null) m_SkillIcons[slot].sprite = icon;
                }
            }

            // 8 chỉ số từ TankDefinition.GetStatsForLevel
            if (owned)
            {
                TankStats stats = def.GetStatsForLevel(level);
                float[] values = new float[]
                {
                    stats.Attack,
                    stats.MaxHealth,
                    stats.MaxEnergy,
                    stats.Defense,
                    stats.MoveSpeed,
                    stats.CooldownReduction,
                    stats.DamageRate,
                    stats.ArmorPenetration
                };
                for (int i = 0; i < m_StatValues.Length; i++)
                {
                    SetText(m_StatValues[i], values[i].ToString("0.#"));
                }
            }
            else
            {
                for (int i = 0; i < m_StatValues.Length; i++)
                {
                    SetText(m_StatValues[i], "-");
                }
            }

            if (m_AccessoryPanel != null)
            {
                m_AccessoryPanel.Refresh();
            }
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static string GetFrom(string[] values, int index)
        {
            return values != null && index < values.Length && values[index] != null ? values[index] : string.Empty;
        }

        /// <summary>Mở màn chọn phụ kiện dạng cộng thêm để giữ nguyên Nhà xe và phòng mạng.</summary>
        private void OpenAccessory()
        {
            if (m_AccessoryOpen)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(m_AccessorySceneName))
            {
                Debug.LogWarning("[Garage] Scene chưa có trong Build Settings: " + m_AccessorySceneName);
                return;
            }

            m_AccessoryOpen = true;
            m_AccessoryLoaded = false;
            if (m_Canvas != null)
            {
                m_Canvas.enabled = false;
            }

            SceneManager.LoadSceneAsync(m_AccessorySceneName, LoadSceneMode.Additive);
        }

        /// <summary>Khi màn phụ kiện đóng lại thì bật lại giao diện Nhà xe.</summary>
        private void SyncAdditiveAccessory()
        {
            if (!m_AccessoryOpen)
            {
                return;
            }

            Scene accessory = SceneManager.GetSceneByName(m_AccessorySceneName);
            if (!m_AccessoryLoaded)
            {
                if (accessory.isLoaded)
                {
                    m_AccessoryLoaded = true;
                }

                return;
            }

            if (accessory.isLoaded)
            {
                return;
            }

            m_AccessoryOpen = false;
            m_AccessoryLoaded = false;
            if (m_Canvas != null)
            {
                m_Canvas.enabled = true;
            }

            if (m_AccessoryPanel != null)
            {
                m_AccessoryPanel.Refresh();
            }
        }

        /// <summary>Mở màn chọn kỹ năng cho tab tương ứng của xe đang chọn.</summary>
        private void OpenPicker(int tabIndex)
        {
            if (SaveSystem.Instance != null)
            {
                SaveSystem.Instance.PendingTankIndex = m_SelectedIndex;
                SaveSystem.Instance.PendingTabIndex = tabIndex;
                SaveSystem.Instance.LastSelectedTankIndex = m_SelectedIndex;
            }
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

        /// <summary>Nâng cấp xe hiện tại nếu đủ ngọc.</summary>
        public void TryUpgrade()
        {
            if (m_SelectedIndex < 0 || SaveSystem.Instance == null) return;
            if (SaveSystem.Instance.UpgradeTank(m_SelectedIndex))
            {
                Select(m_SelectedIndex);
            }
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

        /// <summary>
        /// XÁC NHẬN RA TRẬN chỉ chốt xe đang chọn rồi quay về Main Menu để người chơi
        /// xem lại đúng xe vừa chọn; vào trận thật từ nút CHỌN MÀN CHƠI ở Main Menu.
        /// </summary>
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
