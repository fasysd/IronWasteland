using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Tô màu nền + viền của nút theo trạng thái (thường / hover / nhấn / vô hiệu).
    /// Không dùng ColorTint của Button vì tint nhân luôn vào viền (Outline) làm viền mất màu.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Button m_Button;
        private Image m_Image;
        private Outline m_Outline;
        private ButtonLook m_Look;
        private bool m_Over;
        private bool m_Down;

        public ButtonLook Look => m_Look;

        /// <summary>Phát khi chuột vào (true) / ra (false) nút. Cũng phát false mỗi lần nút được bật lại để màn hình đặt lại màu chữ.</summary>
        public event Action<bool> HoverChanged;

        private void Awake() => Cache();

        private void Cache()
        {
            if (m_Button != null) return;

            m_Button = GetComponent<Button>();
            m_Image = GetComponent<Image>();
            m_Outline = GetComponent<Outline>();
            if (m_Button != null) m_Button.transition = Selectable.Transition.None;
        }

        public void SetLook(ButtonLook look)
        {
            Cache();
            m_Look = look;
            Refresh();
        }

        public void Refresh()
        {
            Cache();
            if (m_Look == null || m_Image == null || m_Button == null) return;

            Color fill;
            if (!m_Button.interactable) fill = m_Look.Disabled;
            else if (m_Down) fill = m_Look.Pressed;
            else if (m_Over) fill = m_Look.Hover;
            else fill = m_Look.Normal;

            m_Image.color = fill;
            if (m_Outline != null)
                m_Outline.effectColor = (m_Over && m_Button.interactable) ? m_Look.HoverBorder : m_Look.Border;
        }

        private void OnEnable()
        {
            m_Over = false;
            m_Down = false;
            Refresh();
            HoverChanged?.Invoke(false);
        }

        private void OnDisable()
        {
            m_Over = false;
            m_Down = false;
        }

        public void OnPointerEnter(PointerEventData eventData) { m_Over = true; Refresh(); HoverChanged?.Invoke(true); }
        public void OnPointerExit(PointerEventData eventData) { m_Over = false; m_Down = false; Refresh(); HoverChanged?.Invoke(false); }
        public void OnPointerDown(PointerEventData eventData) { m_Down = true; Refresh(); }
        public void OnPointerUp(PointerEventData eventData) { m_Down = false; Refresh(); }
    }
}
