using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Helper tạo bản sao visual cho template con (đạn, vùng, bản sao...).
    /// Offline: chỉ Instantiate như cũ. Online: host Instantiate rồi gửi yêu cầu để client dựng bản sao không va chạm.
    /// Client không tự gọi hàm này (mọi chỗ spawn template đều nằm sau cổng quyền của host).
    /// </summary>
    public static class NetVisual
    {
        /// <summary>Instantiate template tại vị trí/góc xoay, kèm nhân bản visual xuống client nếu đang chạy mạng.</summary>
        public static GameObject Spawn(GameObject template, Vector3 position, Quaternion rotation,
            float lifetime = 0f, bool animate = true)
        {
            if (template == null) return null;

            GameObject instance = Object.Instantiate(template, position, rotation);
            instance.SetActive(true);

            if (TankBrain.NetworkSessionActive && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                NetVisualSpawn.Instance?.Broadcast(template, position, rotation, lifetime, animate);

            return instance;
        }

        /// <summary>Instantiate template dạng component (đạn/vùng khai báo bằng kiểu component) và nhân bản visual.</summary>
        public static T Spawn<T>(T template, Vector3 position, Quaternion rotation,
            float lifetime = 0f, bool animate = true) where T : Component
        {
            if (template == null) return null;

            GameObject instance = Object.Instantiate(template.gameObject, position, rotation);
            instance.SetActive(true);

            if (TankBrain.NetworkSessionActive && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                NetVisualSpawn.Instance?.Broadcast(template.gameObject, position, rotation, lifetime, animate);

            return instance.GetComponent<T>();
        }

        /// <summary>Instantiate template làm con của một transform (dùng cho vùng hiệu ứng cần theo chân quái).</summary>
        public static GameObject Spawn(GameObject template, Transform parent, float lifetime = 0f, bool animate = true)
        {
            if (template == null) return null;

            GameObject instance = Object.Instantiate(template, parent);
            instance.SetActive(true);

            if (TankBrain.NetworkSessionActive && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                NetVisualSpawn.Instance?.Broadcast(template, instance.transform.position, instance.transform.rotation, lifetime, animate);

            return instance;
        }
    }
}
