using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Đánh dấu script VFX thuần hiển thị được phép chạy trên bản sao client.
    /// Chỉ dùng cho script đã kiểm tra không gây sát thương/không spawn gameplay trong Update;
    /// collider vẫn luôn bị tắt nên các hiệu ứng trigger chỉ còn phần hình ảnh.
    /// </summary>
    public interface INetVisualGhostDriver
    {
    }

    /// <summary>
    /// Đánh dấu một prefab (hoặc object con như "Skill" của quái) là visual mạng thuần tuý.
    /// Khi host gửi bản sao xuống client, NetVisualSpawn sẽ tắt toàn bộ collider và script gameplay
    /// trên bản sao, chỉ giữ lại phần hiển thị (SpriteRenderer, Animator, ParticleSystem...).
    /// Gắn marker này lên prefab skill để tránh client tự chạy logic gây sát thương.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetVisualGhost : MonoBehaviour
    {
        /// <summary>
        /// Biến bản sao thành visual chỉ đọc: tắt collider và mọi MonoBehaviour (trừ marker này).
        /// Animator/ParticleSystem/SpriteRenderer không phải MonoBehaviour nên vẫn hiển thị bình thường.
        /// </summary>
        public static void Sanitize(GameObject ghost)
        {
            Sanitize(ghost, false);
        }

        /// <summary>
        /// Biến bản sao thành visual chỉ đọc. Khi <paramref name="allowVisualDrivers"/> bật,
        /// các script đánh dấu <see cref="INetVisualGhostDriver"/> được giữ chạy để diễn hoạt VFX;
        /// mọi script gameplay khác vẫn bị tắt.
        /// </summary>
        public static void Sanitize(GameObject ghost, bool allowVisualDrivers)
        {
            if (ghost == null) return;

            Collider2D[] colliders = ghost.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = false;
            }

            MonoBehaviour[] behaviours = ghost.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null || behaviours[i] is NetVisualGhost) continue;
                if (allowVisualDrivers && behaviours[i] is INetVisualGhostDriver) continue;
                behaviours[i].enabled = false;
            }
        }

        private void OnDestroy()
        {
            // Host: khi VFX nguồn bị dọn thì thu hồi luôn bản sao bên client.
            // Client: không có handle trong bảng của host nên lệnh này không gửi gì.
            NetVisualSpawn.End(gameObject);
        }
    }
}
