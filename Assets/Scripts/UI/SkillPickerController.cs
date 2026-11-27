using System;
using IronWasteland.Save;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronWasteland.UI
{
    /// <summary>
    /// Điều khiển màn hình chọn kỹ năng tùy chọn (TÙY CHỌN 1 / TÙY CHỌN 2).
    /// Mở từ Garage: đọc yêu cầu trong SkillLoadout, hiển thị 3 lựa chọn của đúng xe
    /// và đúng tab. Bấm một thẻ để dùng kỹ năng đó rồi quay lại Garage; bấm ĐÓNG hoặc
    /// ESC để quay lại mà không đổi lựa chọn.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillPickerController : MonoBehaviour
    {
        /// <summary>Tham chiếu hiển thị của một thẻ lựa chọn.</summary>
        [Serializable]
        public sealed class CardView
        {
            [Tooltip("Ảnh nền thẻ theo đúng vị trí 1, 2 hoặc 3.")]
            public Image Base;

            [Tooltip("Ảnh viền cam của thẻ đang dùng.")]
            public Image Selected;

            [Tooltip("Ảnh viền sáng khi rê chuột.")]
            public Image Hover;

            [Tooltip("Nhãn LỰA CHỌN ở góc phải thẻ, ẩn khi thẻ đang được dùng.")]
            public Image ChoiceLabel;

            [Tooltip("Ảnh icon kỹ năng của lựa chọn.")]
            public Image Icon;

            [Tooltip("Tên kỹ năng.")]
            public TextMeshProUGUI Name;

            [Tooltip("Dòng vai trò dưới tên kỹ năng.")]
            public TextMeshProUGUI Role;

            [Tooltip("Số năng lượng tiêu hao.")]
            public TextMeshProUGUI Energy;

            [Tooltip("Thời gian hồi chiêu.")]
            public TextMeshProUGUI Cooldown;

            [Tooltip("3 dòng mô tả kỹ năng.")]
            public TextMeshProUGUI[] Description = new TextMeshProUGUI[3];
        }

        [Header("Dữ liệu")]
        [Tooltip("36 icon kỹ năng, xếp theo thứ tự xe × 6 + tab × 3 + lựa chọn.")]
        [SerializeField] private Sprite[] m_OptionIcons = new Sprite[SkillCatalog.TankCount * SkillCatalog.TabCount * SkillCatalog.OptionCount];

        [Header("Tham chiếu giao diện")]
        [Tooltip("Dòng đường dẫn trên tiêu đề: <tên xe> / KỸ NĂNG TÙY CHỌN / NHÀ XE.")]
        [SerializeField] private TextMeshProUGUI m_Breadcrumb;

        [Tooltip("Ảnh nền 2 tab; đổi giữa bản tab trái sáng và tab phải sáng.")]
        [SerializeField] private Image m_TabsBackground;

        [Tooltip("Ảnh nền khi tab TÙY CHỌN 1 đang mở.")]
        [SerializeField] private Sprite m_TabsLeftSprite;

        [Tooltip("Ảnh nền khi tab TÙY CHỌN 2 đang mở.")]
        [SerializeField] private Sprite m_TabsRightSprite;

        [Tooltip("Nhãn 2 tab, đúng thứ tự TÙY CHỌN 1 rồi TÙY CHỌN 2.")]
        [SerializeField] private TextMeshProUGUI[] m_TabLabels = new TextMeshProUGUI[2];

        [Tooltip("3 thẻ lựa chọn từ trái sang phải.")]
        [SerializeField] private CardView[] m_Cards = new CardView[SkillCatalog.OptionCount];

        [Header("Vùng rê chuột của thẻ (gốc trên-trái, hệ 1920x1080)")]
        [Tooltip("Vùng của thẻ đầu tiên, dùng để bật viền sáng khi rê chuột.")]
        [SerializeField] private Rect m_CardArea = new Rect(432f, 380f, 336f, 400f);

        [Tooltip("Khoảng cách ngang giữa hai thẻ lựa chọn.")]
        [SerializeField] private float m_CardPitch = 360f;

        [Header("Scene đích")]
        [Tooltip("Scene quay lại khi đóng màn chọn kỹ năng.")]
        [SerializeField] private string m_BackSceneName = "Garage";

        private const float DesignHeight = 1080f;
        private static readonly Color ActiveTabColor = new Color32(0x0B, 0x0F, 0x14, 0xFF);
        private static readonly Color IdleTabColor = new Color32(0x6C, 0x7A, 0x8A, 0xFF);

        private Canvas m_Canvas;
        private int m_TankIndex;
        private int m_TabIndex;
        private int m_SelectedOption;
        private int m_HoveredCard = -1;
        private Vector2 m_PointerScreen;
        private bool m_HasGuiPointer;

        /// <summary>Xe đang được chọn kỹ năng (chỉ đọc).</summary>
        public int TankIndex => m_TankIndex;

        /// <summary>Tab đang mở: 0 là TÙY CHỌN 1, 1 là TÙY CHỌN 2.</summary>
        public int TabIndex => m_TabIndex;

        private void Awake()
        {
            m_Canvas = GetComponent<Canvas>();
            if (m_Canvas == null)
            {
                m_Canvas = GetComponentInParent<Canvas>();
            }

            SaveSystem.Instance.ConsumeSkillPickerRequest(out m_TankIndex, out m_TabIndex);
            m_SelectedOption = SaveSystem.Instance.GetOptionSkill(m_TankIndex, m_TabIndex);
            RefreshAll();
        }

        private void Update()
        {
            UpdateHover(DesignPointerPosition(PointerPosition));
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
                case EventType.KeyDown:
                    if (current.keyCode == KeyCode.Escape)
                    {
                        Close();
                    }

                    break;
            }
        }

        /// <summary>Cập nhật viền sáng của thẻ đang rê chuột.</summary>
        private void UpdateHover(Vector2 designPosition)
        {
            int hovered = -1;
            for (int i = 0; i < SkillCatalog.OptionCount; i++)
            {
                if (GetCardArea(i).Contains(designPosition))
                {
                    hovered = i;
                    break;
                }
            }

            if (hovered == m_HoveredCard)
            {
                return;
            }

            m_HoveredCard = hovered;
            for (int i = 0; i < m_Cards.Length; i++)
            {
                if (m_Cards[i] != null && m_Cards[i].Hover != null)
                {
                    m_Cards[i].Hover.enabled = i == m_HoveredCard && i != m_SelectedOption;
                }
            }
        }

        /// <summary>Vẽ lại toàn bộ nội dung theo xe và tab hiện tại.</summary>
        private void RefreshAll()
        {
            m_HoveredCard = -1;

            if (m_Breadcrumb != null)
            {
                m_Breadcrumb.text = SkillCatalog.GetTankName(m_TankIndex) + " / KỸ NĂNG TÙY CHỌN / NHÀ XE";
            }

            if (m_TabsBackground != null)
            {
                m_TabsBackground.sprite = m_TabIndex == 0 ? m_TabsLeftSprite : m_TabsRightSprite;
            }

            for (int i = 0; i < m_TabLabels.Length; i++)
            {
                if (m_TabLabels[i] != null)
                {
                    m_TabLabels[i].color = i == m_TabIndex ? ActiveTabColor : IdleTabColor;
                }
            }

            for (int i = 0; i < m_Cards.Length; i++)
            {
                CardView card = m_Cards[i];
                if (card == null)
                {
                    continue;
                }

                SkillCatalog.Option option = SkillCatalog.Get(m_TankIndex, m_TabIndex, i);
                SetText(card.Name, option.Name.ToUpperInvariant());
                SetText(card.Role, option.Role);
                SetText(card.Energy, option.Energy.ToString());
                SetText(card.Cooldown, FormatCooldown(option.Cooldown));

                string[] lines = WrapDescription(option.Description, 3);
                if (card.Description != null)
                {
                    for (int line = 0; line < card.Description.Length; line++)
                    {
                        SetText(card.Description[line], line < lines.Length ? lines[line] : string.Empty);
                    }
                }

                if (card.Icon != null && m_OptionIcons != null)
                {
                    int spriteIndex = SkillCatalog.GetIconIndex(m_TankIndex, m_TabIndex, i);
                    if (spriteIndex < m_OptionIcons.Length && m_OptionIcons[spriteIndex] != null)
                    {
                        card.Icon.sprite = m_OptionIcons[spriteIndex];
                    }
                }

                bool isSelected = i == m_SelectedOption;
                if (card.Selected != null)
                {
                    card.Selected.enabled = isSelected;
                }

                if (card.ChoiceLabel != null)
                {
                    card.ChoiceLabel.enabled = !isSelected;
                }

                if (card.Hover != null)
                {
                    card.Hover.enabled = false;
                }
            }
        }

        private Rect GetCardArea(int index)
        {
            return new Rect(m_CardArea.x + m_CardPitch * index, m_CardArea.y, m_CardArea.width, m_CardArea.height);
        }

        /// <summary>Nút ĐÓNG góc trên phải gọi vào đây.</summary>
        public void PressClose()
        {
            Close();
        }

        /// <summary>Tab TÙY CHỌN 1 gọi vào đây.</summary>
        public void PressTab1()
        {
            SwitchTab(0);
        }

        /// <summary>Tab TÙY CHỌN 2 gọi vào đây.</summary>
        public void PressTab2()
        {
            SwitchTab(1);
        }

        /// <summary>Đổi tab và giữ nguyên xe đang mở.</summary>
        public void SwitchTab(int tabIndex)
        {
            m_TabIndex = Mathf.Clamp(tabIndex, 0, SkillCatalog.TabCount - 1);
            m_SelectedOption = SaveSystem.Instance.GetOptionSkill(m_TankIndex, m_TabIndex);
            m_HoveredCard = -1;
            RefreshAll();
        }

        /// <summary>Dùng lựa chọn đang bấm rồi quay lại Garage.</summary>
        public void Choose(int optionIndex)
        {
            int option = Mathf.Clamp(optionIndex, 0, SkillCatalog.OptionCount - 1);
            SaveSystem.Instance.SetOptionSkill(m_TankIndex, m_TabIndex, option);
            SaveSystem.Instance.LastSelectedTankIndex = m_TankIndex;
            if (GarageFlow.OpenedFromLobby)
            {
                SceneManager.UnloadSceneAsync(gameObject.scene);
                return;
            }

            LoadScene(m_BackSceneName);
        }

        /// <summary>Đóng màn chọn kỹ năng mà không đổi lựa chọn.</summary>
        public void Close()
        {
            SaveSystem.Instance.LastSelectedTankIndex = m_TankIndex;
            if (GarageFlow.OpenedFromLobby)
            {
                SceneManager.UnloadSceneAsync(gameObject.scene);
                return;
            }

            LoadScene(m_BackSceneName);
        }

        /// <summary>Chia mô tả thành tối đa 3 dòng, mỗi dòng khoảng 34 ký tự.</summary>
        private static string[] WrapDescription(string text, int lineCount)
        {
            string[] lines = new string[lineCount];
            if (string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < lineCount; i++)
                {
                    lines[i] = string.Empty;
                }

                return lines;
            }

            string[] words = text.Split(' ');
            int line = 0;
            string current = string.Empty;
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (candidate.Length > 34 && current.Length > 0 && line < lineCount - 1)
                {
                    lines[line] = current;
                    line++;
                    current = word;
                }
                else
                {
                    current = candidate;
                }
            }

            lines[line] = current;
            for (int i = line + 1; i < lineCount; i++)
            {
                lines[i] = string.Empty;
            }

            return lines;
        }

        private static string FormatCooldown(float cooldown)
        {
            return cooldown % 1f == 0f ? cooldown.ToString("0") + "s" : cooldown.ToString("0.#") + "s";
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
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

        /// <summary>Chuyển scene theo tên, chỉ chạy khi scene đã có trong Build Settings.</summary>
        private void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning("[SkillPicker] Scene chưa có trong Build Settings: " + sceneName);
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}