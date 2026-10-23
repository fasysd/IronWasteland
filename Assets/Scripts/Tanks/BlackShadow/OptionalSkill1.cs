using System;
using UnityEngine;

namespace IronWasteland.Tanks.BlackShadow
{
    /// <summary>
    /// Kỹ năng Tùy Chọn 1 của Black Shadow: Cường Hóa Bóng Tối.
    /// Trong khoảng thời gian cấu hình (timeEnhance), xe tăng ở trạng thái cường hóa:
    /// phát sự kiện OnIsEnhancedChange(true) khi bắt đầu và OnIsEnhancedChange(false) khi hết thời gian.
    /// </summary>
    public class OptionalSkill1 : MonoBehaviour
    {
        public event Action<bool> OnIsEnhancedChange;
        private float remainingTime = 0;
        [Tooltip("Thời gian duy trì trạng thái cường hóa (giây).")]
        [SerializeField] private float timeEnhance = 10f;
        private bool isInvoke = false;

        /// <summary>
        /// Kích hoạt trạng thái cường hóa trong khoảng thời gian cấu hình.
        /// </summary>
        public void Initialize()
        {
            // Dọn dẹp trạng thái của lần kích hoạt trước trước khi bắt đầu lượt mới
            Clear();

            remainingTime = timeEnhance;
            OnIsEnhancedChange?.Invoke(true);
        }

        private void Start()
        {
        }

        // Cập nhật mỗi khung hình: đếm ngược thời gian cường hóa và chỉ phát sự kiện kết thúc đúng một lần
        private void Update()
        {
            if (remainingTime > 0)
            {
                isInvoke = false;
                remainingTime -= Time.deltaTime;
            }
            if (remainingTime <= 0 && isInvoke == false)
            {
                OnIsEnhancedChange?.Invoke(false);
                isInvoke = true;
            }
        }

        /// <summary>
        /// Đặt lại toàn bộ trạng thái runtime của hiệu ứng cường hóa về mặc định.
        /// CHỈ reset state: không phát event, không tắt GameObject, không ghi đè trường cấu hình (timeEnhance).
        /// Dùng chung cho Clear (dọn dẹp khi tái sử dụng) và mọi đường kết thúc khác.
        /// </summary>
        private void ResetRuntimeState()
        {
            remainingTime = 0f;
            isInvoke = false;
        }

        /// <summary>
        /// Dọn dẹp để tái sử dụng: đưa trạng thái runtime về mặc định.
        /// Không phát event, không tắt GameObject, KHÔNG xoá subscriber —
        /// người nghe tự gỡ đăng ký của mình trong OnDisable/OnDestroy.
        /// Initialize gọi hàm này trước tiên.
        /// </summary>
        public void Clear()
        {
            ResetRuntimeState();
        }
    }
}
