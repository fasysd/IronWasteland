using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Danh mục 20 phụ kiện trang bị dùng cho màn chọn phụ kiện trong Nhà xe.
    /// Dữ liệu tên, nhóm, chỉ số, mô tả và icon được sinh từ file data của thiết kế;
    /// khi cân bằng lại phụ kiện chỉ cần sửa asset này.
    /// </summary>
    [CreateAssetMenu(
        menuName = "IronWasteland/Tank/Accessory Catalog",
        fileName = "AccessoryCatalog")]
    public sealed class AccessoryCatalog : ScriptableObject
    {
        /// <summary>Một phụ kiện hiển thị trong màn chọn.</summary>
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Khoá ổn định, trùng tên file icon trong thư mục Items.")]
            public string Key;

            [Tooltip("Tên ngắn hiển thị trên thẻ phụ kiện.")]
            public string DisplayName;

            [Tooltip("Nhóm phụ kiện, ví dụ LÕI ĐỘNG CƠ hoặc ĐỘNG CƠ XUNG KÍCH.")]
            public string Category;

            [Tooltip("Dòng vai trò dưới tên, ví dụ GIÁP / PHÒNG THỦ.")]
            public string Role;

            [Tooltip("Các dòng chỉ số cộng thêm, mỗi dòng một phần tử.")]
            public string[] Stats = Array.Empty<string>();

            [Tooltip("Mô tả hiệu ứng của phụ kiện.")]
            [TextArea(2, 6)]
            public string Description;

            [Tooltip("Ảnh vuông của phụ kiện.")]
            public Sprite Icon;
        }

        [Tooltip("Toàn bộ phụ kiện, xếp đúng thứ tự hiển thị trên màn chọn.")]
        [SerializeField] private List<Entry> m_Entries = new List<Entry>();

        /// <summary>Tổng số phụ kiện trong danh mục.</summary>
        public int Count => m_Entries != null ? m_Entries.Count : 0;

        /// <summary>Lấy phụ kiện theo chỉ số; trả về null nếu ngoài phạm vi.</summary>
        public Entry Get(int index)
        {
            if (m_Entries == null || index < 0 || index >= m_Entries.Count)
            {
                return null;
            }

            return m_Entries[index];
        }
    }
}
