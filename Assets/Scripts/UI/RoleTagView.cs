using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IronWasteland.UI
{
    /// <summary>
    /// Một pill vai trò ở khu trưng bày main menu: nền tối, viền màu theo vai trò và chữ cùng màu.
    /// Builder dựng sẵn hình khối, controller chỉ đổ chữ/màu và nới bề rộng.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoleTagView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Nền tối của pill.")]
        private Image m_Background;

        [SerializeField]
        [Tooltip("Viền màu theo vai trò.")]
        private Image m_Border;

        [SerializeField]
        [Tooltip("Chữ vai trò bên trong pill.")]
        private TextMeshProUGUI m_Label;

        [SerializeField]
        [Tooltip("Khoảng hở hai bên chữ khi tính bề rộng pill.")]
        private float m_HorizontalPadding = 9f;

        private RectTransform m_Rect;

        /// <summary>Đổ chữ + màu và nới pill vừa nội dung; trả về bề rộng sau khi nới.</summary>
        public float Bind(string text, Color32 color)
        {
            gameObject.SetActive(true);
            if (m_Rect == null)
            {
                m_Rect = (RectTransform)transform;
            }

            m_Label.text = text;
            m_Label.color = color;
            m_Label.ForceMeshUpdate();

            m_Border.color = new Color32(color.r, color.g, color.b, 150);
            m_Background.color = new Color32(0x12, 0x18, 0x21, 0xE6);

            float width = Mathf.Ceil(m_Label.preferredWidth) + m_HorizontalPadding * 2f;
            m_Rect.sizeDelta = new Vector2(width, m_Rect.sizeDelta.y);
            return width;
        }

        /// <summary>Dời pill tới mép trái cho trước, giữ nguyên độ cao.</summary>
        public void SetLeft(float x)
        {
            if (m_Rect == null)
            {
                m_Rect = (RectTransform)transform;
            }

            Vector2 position = m_Rect.anchoredPosition;
            position.x = x;
            m_Rect.anchoredPosition = position;
        }

        /// <summary>Ẩn pill khi xe không có vai trò tương ứng.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
