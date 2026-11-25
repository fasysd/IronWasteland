using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Tồn kho phụ kiện của người chơi: số bản sao của từng phụ kiện, lưu theo khoá ổn định
    /// của <see cref="AccessoryCatalog.Entry.Key"/> trong PlayerPrefs.
    /// Đây là nguồn duy nhất cho câu hỏi "người chơi có bao nhiêu cái này"; mọi nguồn thưởng
    /// (hoàn thành màn, hòm quà, sự kiện) chỉ cần gọi <see cref="Add"/>.
    /// </summary>
    public static class AccessoryInventory
    {
        /// <summary>Tiền tố khoá PlayerPrefs; ghép với Key của phụ kiện.</summary>
        private const string KeyPrefix = "iw.acc.";

        /// <summary>Số bản sao mặc định khi phụ kiện chưa từng được cấp phát.</summary>
        public const int DefaultOwned = 1;

        /// <summary>Lấy số bản sao đang có của phụ kiện; mặc định là <see cref="DefaultOwned"/>.</summary>
        public static int Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return 0;
            }

            return Mathf.Max(0, PlayerPrefs.GetInt(KeyPrefix + key, DefaultOwned));
        }

        /// <summary>Ghi đè số bản sao; giá trị âm bị kéo về 0.</summary>
        public static void Set(string key, int count)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            PlayerPrefs.SetInt(KeyPrefix + key, Mathf.Max(0, count));
            PlayerPrefs.Save();
        }

        /// <summary>Cộng thêm bản sao, dùng cho thưởng sau màn hoặc sự kiện.</summary>
        public static void Add(string key, int count)
        {
            if (string.IsNullOrEmpty(key) || count <= 0)
            {
                return;
            }

            Set(key, Get(key) + count);
        }

        /// <summary>Trừ bản sao nếu đủ; trả về false và không đổi gì khi thiếu.</summary>
        public static bool TrySpend(string key, int count)
        {
            if (string.IsNullOrEmpty(key) || count <= 0)
            {
                return false;
            }

            int owned = Get(key);
            if (owned < count)
            {
                return false;
            }

            Set(key, owned - count);
            return true;
        }

        /// <summary>Xoá toàn bộ tồn kho đã lưu (dùng cho kiểm thử và reset save).</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(KeyPrefix);
            PlayerPrefs.Save();
        }
    }
}
