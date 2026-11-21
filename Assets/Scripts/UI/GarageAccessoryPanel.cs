using IronWasteland.Tanks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IronWasteland.UI
{
    /// <summary>
    /// Hiển thị phụ kiện đang trang bị của xe trong panel góc dưới bên phải màn hình Garage.
    /// Đọc lựa chọn đã lưu trong AccessoryLoadout và danh mục AccessoryCatalog,
    /// cập nhật ảnh, tên, nhóm và hiệu ứng mỗi khi đổi xe hoặc quay lại từ màn chọn phụ kiện.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarageAccessoryPanel : MonoBehaviour
    {
        [Header("Dữ liệu")]
        [Tooltip("Danh mục 20 phụ kiện dùng để tra cứu tên, icon và hiệu ứng.")]
        [SerializeField] private AccessoryCatalog m_Catalog;

        [Header("Tham chiếu giao diện")]
        [Tooltip("Ảnh vuông của phụ kiện đang trang bị.")]
        [SerializeField] private Image m_Icon;

        [Tooltip("Tên phụ kiện đang trang bị.")]
        [SerializeField] private TextMeshProUGUI m_AccessoryName;

        [Tooltip("Nhóm phụ kiện, ví dụ CỔ VẬT hoặc LÕI ĐỘNG CƠ.")]
        [SerializeField] private TextMeshProUGUI m_Category;

        [Tooltip("Các dòng chỉ số cộng thêm của phụ kiện.")]
        [SerializeField] private TextMeshProUGUI m_Stats;

        [Tooltip("Mô tả hiệu ứng của phụ kiện.")]
        [SerializeField] private TextMeshProUGUI m_Description;

        private void OnEnable()
        {
            Refresh();
        }

        /// <summary>Đọc lại phụ kiện đã lưu của xe đang mở và cập nhật toàn bộ nội dung panel.</summary>
        public void Refresh()
        {
            if (m_Catalog == null)
            {
                return;
            }

            AccessoryCatalog.Entry entry = m_Catalog.Get(AccessoryLoadout.Get(SkillLoadout.LastTankIndex));
            if (entry == null)
            {
                SetText(m_AccessoryName, "CHƯA TRANG BỊ");
                SetText(m_Category, string.Empty);
                SetText(m_Stats, string.Empty);
                SetText(m_Description, "MỞ MÀN PHỤ KIỆN ĐỂ CHỌN TRANG BỊ.");
                if (m_Icon != null)
                {
                    m_Icon.enabled = false;
                }

                return;
            }

            if (m_Icon != null)
            {
                m_Icon.sprite = entry.Icon;
                m_Icon.enabled = entry.Icon != null;
            }

            SetText(m_AccessoryName, entry.DisplayName);
            SetText(m_Category, entry.Category);
            SetText(m_Stats, entry.Stats != null ? string.Join("   ", entry.Stats) : string.Empty);
            SetText(m_Description, entry.Description);
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
