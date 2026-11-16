using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Hàm tiện ích dùng chung cho các đối tượng có thể đã được NGO quản lý.
    ///
    /// Trong chế độ offline (không có <see cref="NetworkObject"/> hoặc chưa spawn) thì hành vi
    /// giữ nguyên như cũ: huỷ ngay GameObject. Khi đang chạy mạng, đối tượng do NGO spawn phải
    /// được despawn qua NGO để client khác cũng thấy biến mất — nếu gọi <c>Destroy</c> trực tiếp
    /// trên host, NGO sẽ mất đồng bộ và các client giữ lại một bản sao "ma".
    /// </summary>
    public static class NetEntity
    {
        /// <summary>
        /// Huỷ một đối tượng theo đúng đường của mạng: despawn nếu đã được spawn, ngược lại Destroy.
        /// Trên client (không phải server) thì không làm gì — client chờ lệnh despawn từ host.
        /// </summary>
        public static void Despawn(GameObject target)
        {
            if (target == null) return;

            NetworkObject networkObject = target.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null && manager.IsServer)
                {
                    networkObject.Despawn(true);
                }

                return;
            }

            Object.Destroy(target);
        }
    }
}
