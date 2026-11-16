using IronWasteland.Tanks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Cầu nối phát lại hình ảnh kỹ năng cho client.
    /// Host thi triển kỹ năng trên brain thật, rồi gửi named message chứa (chủ xe, chỉ số kỹ năng, điểm ngắm).
    /// Client nhận được sẽ gọi <see cref="TankBrain.PlayRemoteSkillFx"/> để sinh lại đạn/ảo ảnh/vùng bằng pool cục bộ.
    /// Không dùng NetworkBehaviour vì NGO cấm gắn NetworkBehaviour lên GameObject của NetworkManager.
    /// </summary>
    public sealed class NetSkillFx : MonoBehaviour
    {
        private const string MessageName = "IW.SkillFx";

        // ulong owner (8) + byte index (1) + Vector2 aim (8) = 17 byte, cấp dư 32 cho an toàn.
        private const int MessageSize = 32;

        private bool m_Registered;

        /// <summary>Instance duy nhất trong scene do NetBootstrap/Network root giữ.</summary>
        public static NetSkillFx Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unregister();
        }

        private void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
            {
                Register(manager);
            }
            else
            {
                Unregister();
            }
        }

        /// <summary>
        /// Host gọi sau khi thi triển kỹ năng thành công để client phát lại hình ảnh.
        /// Chỉ server được gửi; client gọi hàm này sẽ bị bỏ qua.
        /// </summary>
        public void Broadcast(TankBrain brain, int skillIndex, Vector2 aim)
        {
            if (brain == null) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer) return;

            NetworkObject networkObject = brain.GetComponent<NetworkObject>();
            if (networkObject == null || !networkObject.IsSpawned) return;

            using FastBufferWriter writer = new FastBufferWriter(MessageSize, Allocator.Temp);
            writer.WriteValueSafe(networkObject.OwnerClientId);
            writer.WriteValueSafe((byte)skillIndex);
            writer.WriteValueSafe(aim);
            manager.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer);
        }

        private void Register(NetworkManager manager)
        {
            if (m_Registered) return;
            if (manager.CustomMessagingManager == null) return;

            manager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, HandleMessage);
            m_Registered = true;
        }

        private void Unregister()
        {
            if (!m_Registered) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.CustomMessagingManager != null)
            {
                manager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
            }

            m_Registered = false;
        }

        private void HandleMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out ulong ownerClientId);
            reader.ReadValueSafe(out byte skillIndex);
            reader.ReadValueSafe(out Vector2 aim);

            NetSpawnService spawnService = FindAnyObjectByType<NetSpawnService>();
            if (spawnService == null) return;
            if (!spawnService.TryGetTank(ownerClientId, out TankBrain tank) || tank == null) return;

            tank.PlayRemoteSkillFx(skillIndex, aim);
        }
    }
}
