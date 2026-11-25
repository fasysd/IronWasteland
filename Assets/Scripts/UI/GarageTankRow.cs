using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IronWasteland.UI
{
    /// <summary>
    /// Một hàng xe trong danh sách Garage. Chỉ giữ tham chiếu hiển thị và nhận dữ liệu
    /// đã tính sẵn từ TankListModel, không tự đọc dữ liệu game.
    /// </summary>
    public sealed class GarageTankRow : MonoBehaviour
    {
        [Header("Chữ")]
        [Tooltip("Tên xe, dòng lớn nhất của hàng.")]
        [SerializeField] private TextMeshProUGUI m_NameText;

        [Tooltip("Vai trò chính và phụ, ví dụ TANKER / HỖ TRỢ.")]
        [SerializeField] private TextMeshProUGUI m_Role;

        [Tooltip("Cấp hiện tại dạng CẤP 05 / 90.")]
        [SerializeField] private TextMeshProUGUI m_Level;

        [Tooltip("Nhãn ải cần mở, chỉ hiện khi xe còn khoá.")]
        [SerializeField] private TextMeshProUGUI m_Unlock;

        [Header("Hình")]
        [Tooltip("Ảnh đại diện của xe.")]
        [SerializeField] private Image m_Icon;

        [Tooltip("Thanh tiến trình cấp, chiều rộng co theo tỉ lệ cấp trên cấp tối đa.")]
        [SerializeField] private Image m_BarFill;

        [Tooltip("Lớp phủ khoá, bật khi xe chưa mở.")]
        [SerializeField] private Image m_LockOverlay;

        [Tooltip("Khung sáng của hàng đang chọn.")]
        [SerializeField] private Image m_Selection;

        [Tooltip("Vạch nhấn màu bên trái hàng, lấy theo màu chủ đề của xe.")]
        [SerializeField] private Image m_Accent;

        [Tooltip("Nút bấm nhận chọn xe.")]
        [SerializeField] private Button m_Button;

        private static readonly Color32 NameColor = new Color32(0xE6, 0xED, 0xF5, 0xFF);
        private static readonly Color32 NameLockedColor = new Color32(0x5A, 0x66, 0x75, 0xFF);
        private static readonly Color32 LevelColor = new Color32(0x8A, 0x99, 0xAB, 0xFF);
        private static readonly Color32 LevelLockedColor = new Color32(0x3D, 0x4A, 0x59, 0xFF);

        private RectTransform m_BarFillRect;
        private float m_BarWidth;

        /// <summary>Nút bấm của hàng, dùng để gắn sự kiện chọn xe.</summary>
        public Button Button => m_Button;

        private void Awake()
        {
            if (m_BarFill != null)
            {
                m_BarFillRect = m_BarFill.rectTransform;
                m_BarWidth = m_BarFillRect.sizeDelta.x;
                m_BarFill.type = Image.Type.Filled;
                m_BarFill.fillMethod = Image.FillMethod.Horizontal;
                m_BarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
        }

        /// <summary>Đổ toàn bộ nội dung của một hàng từ dữ liệu đã tính.</summary>
        public void Bind(string displayName, string roleText, string levelText, string unlockText, float barFraction, bool isLocked, Sprite icon, Color accent)
        {
            if (m_NameText != null)
            {
                m_NameText.text = displayName;
                m_NameText.color = isLocked ? NameLockedColor : NameColor;
            }

            if (m_Role != null)
            {
                m_Role.text = roleText;
                m_Role.color = isLocked ? NameLockedColor : accent;
            }

            if (m_Level != null)
            {
                m_Level.text = levelText;
                m_Level.color = isLocked ? LevelLockedColor : LevelColor;
            }

            if (m_Accent != null) m_Accent.color = isLocked ? LevelLockedColor : accent;
            if (m_Unlock != null)
            {
                m_Unlock.text = unlockText;
                m_Unlock.gameObject.SetActive(isLocked && !string.IsNullOrEmpty(unlockText));
            }

            IsLocked = isLocked;
            if (m_Icon != null) m_Icon.sprite = icon;
            if (m_LockOverlay != null) m_LockOverlay.gameObject.SetActive(isLocked);

            if (m_BarFill != null)
            {
                if (m_BarFillRect == null)
                {
                    m_BarFillRect = m_BarFill.rectTransform;
                    m_BarWidth = m_BarFillRect.sizeDelta.x;
                }

                m_BarFill.type = Image.Type.Filled;
                m_BarFill.fillMethod = Image.FillMethod.Horizontal;
                m_BarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                m_BarFill.color = isLocked ? LevelLockedColor : accent;
                m_BarFill.fillAmount = Mathf.Clamp01(barFraction);
            }
        }

        /// <summary>Hàng đang khoá vì ải chưa mở; bấm vào không đổi xe.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>Bật/tắt khung sáng của hàng.</summary>
        public void SetSelected(bool selected)
        {
            if (m_Selection != null)
            {
                m_Selection.gameObject.SetActive(selected);
            }
        }
    }
}
