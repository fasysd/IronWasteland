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

        // ulong owner (8) + byte index (1) + Vector2 aim (8) + Vector2 origin (8) + 2 byte biến thể = 27 byte, cấp dư 40.
        private const int MessageSize = 40;

        private bool m_Registered;

        /// <summary>Đếm số lần phát lại để chỉ ghi vài dòng log chẩn đoán đầu tiên.</summary>
        private static int s_ReplayLogCount;

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
        public void Broadcast(TankBrain brain, int skillIndex, Vector2 aim, Vector2 origin, byte option1 = 0, byte option2 = 0)
        {
            if (brain == null) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer) return;
            if (manager.CustomMessagingManager == null) return;

            NetworkObject networkObject = brain.GetComponent<NetworkObject>();
            if (networkObject == null || !networkObject.IsSpawned) return;

            using FastBufferWriter writer = new FastBufferWriter(MessageSize, Allocator.Temp);
            writer.WriteValueSafe(networkObject.OwnerClientId);
            writer.WriteValueSafe((byte)skillIndex);
            writer.WriteValueSafe(aim);
            writer.WriteValueSafe(origin);
            writer.WriteValueSafe(option1);
            writer.WriteValueSafe(option2);
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
            if (senderClientId != NetworkManager.ServerClientId) return; // chỉ host được phát lại VFX
            reader.ReadValueSafe(out ulong ownerClientId);
            reader.ReadValueSafe(out byte skillIndex);
            reader.ReadValueSafe(out Vector2 aim);
            reader.ReadValueSafe(out Vector2 origin);
            reader.ReadValueSafe(out byte option1);
            reader.ReadValueSafe(out byte option2);

            NetSpawnService spawnService = FindAnyObjectByType<NetSpawnService>();
            if (spawnService == null) return;
            if (!spawnService.TryResolveTank(ownerClientId, out TankBrain tank) || tank == null)
            {
                Debug.LogWarning($"[NetSkillFx] không tìm thấy xe của owner={ownerClientId} để phát lại kỹ năng {skillIndex}.");
                return;
            }

            if (s_ReplayLogCount < 3)
            {
                s_ReplayLogCount++;
                Debug.Log($"[NetSkillFx] phát lại kỹ năng {skillIndex} cho xe {tank.name} (owner={ownerClientId}).");
            }

            tank.PlayRemoteSkillFx(skillIndex, aim, origin, option1, option2);
        }
    }
}
