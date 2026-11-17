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
            return Spawn(template, position, rotation, default(Vector2), lifetime, animate);
        }

        /// <summary>
        /// Như bản trên nhưng truyền vận tốc bay cho đạn. Client sẽ tự di chuyển bản sao theo vận tốc này
        /// (script projectile gốc chỉ chạy sau khi Initialize trên host nên trên client sẽ đứng yên).
        /// </summary>
        public static T Spawn<T>(T template, Vector3 position, Quaternion rotation, Vector2 velocity,
            float lifetime = 0f, bool animate = true) where T : Component
        {
            if (template == null) return null;

            GameObject instance = Object.Instantiate(template.gameObject, position, rotation);
            instance.SetActive(true);

            if (TankBrain.NetworkSessionActive && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                NetVisualSpawn.Instance?.Broadcast(template.gameObject, position, rotation, velocity, lifetime, animate);

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

        /// <summary>
        /// Như <see cref="Spawn{T}(T, Vector3, Quaternion, Vector2, float, bool)"/> nhưng đạn đuổi mục tiêu:
        /// gửi kèm id mục tiêu để client bẻ lái giống hệt bản gốc trên host (thay vì bay thẳng).
        /// </summary>
        public static T SpawnHoming<T>(T template, Vector3 position, Quaternion rotation, Vector2 velocity,
            float lifetime, Transform homingTarget, float turnSpeed, float homingDelay, float homingDuration)
            where T : Component
        {
            if (template == null) return null;

            GameObject instance = Object.Instantiate(template.gameObject, position, rotation);
            instance.SetActive(true);

            if (TankBrain.NetworkSessionActive && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                ulong targetId = 0UL;
                if (homingTarget != null)
                {
                    NetworkObject targetObject = homingTarget.GetComponentInParent<NetworkObject>();
                    if (targetObject != null && targetObject.IsSpawned)
                        targetId = targetObject.NetworkObjectId;
                }

                NetworkObject root = template.GetComponentInParent<NetworkObject>(true);
                if (root != null)
                {
                    NetVisualSpawn.Instance?.BroadcastByName(
                        root,
                        template.gameObject.name,
                        position,
                        rotation,
                        velocity,
                        lifetime,
                        false,
                        targetId,
                        turnSpeed,
                        homingDelay,
                        homingDuration);
                }
            }

            return instance.GetComponent<T>();
        }

        /// <summary>
        /// Nhân bản visual thủ tục (object do host tạo bằng <c>new GameObject</c>, không có template trong prefab).
        /// Dùng cho vệt lửa và gai đá; client chỉ vẽ sprite, không có logic gameplay.
        /// </summary>
        public static int SpawnPrimitive(Vector3 position, Vector2 velocity, float radius, float lifetime,
            bool fade, int sortingOrder, Color color, float rotationDeg = 0f, float stretchX = 1f)
        {
            if (!TankBrain.NetworkSessionActive || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return 0;

            return NetVisualSpawn.Instance != null
                ? NetVisualSpawn.Instance.BroadcastPrimitive(position, velocity, radius, lifetime, fade, sortingOrder,
                    color, rotationDeg, stretchX)
                : 0;
        }

        /// <summary>Thu hồi sớm một ghost thủ tục theo handle (host gọi khi hiệu ứng kết thúc trước hạn).</summary>
        public static void EndGhost(int handle)
        {
            if (handle <= 0) return;
            if (!TankBrain.NetworkSessionActive || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return;

            NetVisualSpawn.Instance?.BroadcastDespawn(handle);
        }
    }
}
