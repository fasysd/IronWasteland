using IronWasteland.Tanks;
using IronWasteland.UiModels;
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

            int tankIndex = SkillLoadout.LastTankIndex;
            int savedIndex = AccessoryLoadout.Has(tankIndex) ? AccessoryLoadout.Get(tankIndex) : AccessoryLoadout.None;
            AccessoryCatalog.Entry entry = m_Catalog.Get(savedIndex);
            if (entry == null || AccessoryInventory.Get(entry.Key) <= 0)
            {
                // Save cũ có thể trỏ tới món đã phân rã hoặc ngoài danh mục: dọn về chưa trang bị.
                if (savedIndex != AccessoryLoadout.None)
                {
                    AccessoryLoadout.Clear(tankIndex);
                }

                entry = null;
            }
            AccessoryCard card = entry != null
                ? AccessoryCardModel.Build(new AccessoryCardSource(entry.DisplayName, entry.Stats, entry.Description))
                : AccessoryCardModel.Build(null);

            if (m_Icon != null)
            {
                m_Icon.sprite = entry != null ? entry.Icon : null;
                m_Icon.enabled = card.HasAccessory && entry != null && entry.Icon != null;
            }

            // Nhóm phụ kiện đã gộp vào tên nên nhãn riêng để trống.
            SetText(m_AccessoryName, card.NameText);
            SetText(m_Category, string.Empty);
            SetText(m_Stats, card.BonusText);
            SetText(m_Description, card.HasAccessory ? card.PassiveText : "MỞ MÀN PHỤ KIỆN ĐỂ CHỌN TRANG BỊ.");
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
