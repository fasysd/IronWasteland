using IronWasteland.Net;
using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland
{
    /// <summary>
    /// Cửa ngõ duy nhất để hiện số sát thương / hồi máu lên màn hình.
    ///
    /// Lý do tồn tại: khi chơi online, chỉ host mô phỏng combat nên chỉ host biết con số
    /// thật. Mọi call site cũ gọi thẳng <see cref="TextValueManager"/> sẽ khiến client
    /// không thấy gì. Đi qua lớp này thì:
    /// - Offline (không có phiên mạng): hiện ngay như cũ.
    /// - Online, trên host: hiện ngay + phát RPC xuống client.
    /// - Online, trên client: bỏ qua — client không tự tính sát thương.
    /// </summary>
    public static class CombatFeedback
    {
        /// <summary>
        /// Hiện số sát thương/hồi máu/khiên tại vị trí thế giới. Giữ nguyên hành vi cũ khi offline.
        /// </summary>
        public static void Show(float amount, Vector3 worldPosition, TextValueType type = TextValueType.EnemyDamage)
        {
            if (amount <= 0f) return;

            if (!TankBrain.NetworkSessionActive)
            {
                TextValueManager.Instance?.Show(amount, worldPosition, type);
                return;
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return;

            TextValueManager.Instance?.Show(amount, worldPosition, type);
            NetCombatFeedback.Instance?.Broadcast(amount, worldPosition, type);
        }
    }
}
