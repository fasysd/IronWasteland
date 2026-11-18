using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Vòng tròn âm lượng tổng: bấm / kéo quanh vòng để chỉnh, hoặc cuộn chuột.
    /// Gắn lên GameObject có một Image bắt chuột phủ kín vòng; <see cref="Bind"/> truyền vào
    /// Image vòng tô (Filled Radial360) và nhãn số %.
    /// </summary>
    public sealed class RadialVolumeKnob : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
    {
        private const float ScrollStep = 0.02f;

        private Image m_Fill;
        private TextMeshProUGUI m_Label;
        private float m_Value;

        /// <summary>Phát khi NGƯỜI DÙNG đổi giá trị (không phát khi gọi SetValueWithoutNotify).</summary>
        public event Action<float> ValueChanged;

        public float Value => m_Value;

        public void Bind(Image fill, TextMeshProUGUI label)
        {
            m_Fill = fill;
            m_Label = label;
            SetValueWithoutNotify(m_Value);
        }

        public void SetValueWithoutNotify(float value01)
        {
            m_Value = Mathf.Clamp01(value01);
            if (m_Fill != null) m_Fill.fillAmount = m_Value;
            if (m_Label != null) m_Label.text = Mathf.RoundToInt(m_Value * 100f).ToString();
        }

        public void OnPointerDown(PointerEventData eventData) => SetFromPointer(eventData);

        public void OnDrag(PointerEventData eventData) => SetFromPointer(eventData);

        public void OnScroll(PointerEventData eventData)
        {
            float dir = Mathf.Sign(eventData.scrollDelta.y);
            if (Mathf.Approximately(eventData.scrollDelta.y, 0f)) return;
            Apply(m_Value + dir * ScrollStep);
        }

        private void SetFromPointer(PointerEventData eventData)
        {
            var rt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rt, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            Vector2 d = local - rt.rect.center;
            if (d.sqrMagnitude < 400f) return; // gần tâm: bỏ qua

            // Góc tính từ đỉnh vòng, thuận chiều kim đồng hồ.
            float angle = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            float v = angle / 360f;

            // Chống nhảy từ 100% về 0% (và ngược lại) khi kéo qua đỉnh vòng.
            if (m_Value > 0.75f && v < 0.25f) v = 1f;
            else if (m_Value < 0.25f && v > 0.75f) v = 0f;

            Apply(v);
        }

        private void Apply(float value01)
        {
            float v = Mathf.Clamp01(value01);
            if (Mathf.Approximately(v, m_Value)) return;

            SetValueWithoutNotify(v);
            ValueChanged?.Invoke(v);
        }
    }
}
