using IronWasteland.Stages;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Đồng bộ trạng thái ải (bắt đầu/thắng/thua) từ host xuống client bằng named message.
    /// Gắn trên root <c>Network</c> cùng <see cref="NetworkManager"/>; dùng named message thay vì
    /// <see cref="NetworkBehaviour"/> vì NGO cấm gắn NetworkBehaviour lên GameObject của NetworkManager.
    /// </summary>
    public class NetStageEvents : MonoBehaviour
    {
        /// <summary>Tên kênh named message dùng để gửi trạng thái ải.</summary>
        public const string MessageName = "IW.StageEvent";

        /// <summary>Mã trạng thái: ải vừa bắt đầu.</summary>
        public const byte StateStarted = 0;

        /// <summary>Mã trạng thái: ải đã thắng.</summary>
        public const byte StateCleared = 1;

        /// <summary>Mã trạng thái: ải đã thua.</summary>
        public const byte StateFailed = 2;

        /// <summary>Instance đang hoạt động (mỗi máy chỉ có một).</summary>
        public static NetStageEvents Instance { get; private set; }

        private bool m_Registered;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                m_Registered = false;
                return;
            }

            CustomMessagingManager messaging = manager.CustomMessagingManager;
            if (messaging == null)
            {
                m_Registered = false;
                return;
            }

            if (manager.IsListening && !m_Registered)
            {
                messaging.RegisterNamedMessageHandler(MessageName, HandleMessage);
                m_Registered = true;
            }
            else if (!manager.IsListening && m_Registered)
            {
                messaging.UnregisterNamedMessageHandler(MessageName);
                m_Registered = false;
            }
        }

        /// <summary>Host gửi trạng thái ải hiện tại cho toàn bộ client.</summary>
        public void Broadcast(StageManager stage, byte state)
        {
            if (stage == null) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return;

            CustomMessagingManager messaging = manager.CustomMessagingManager;
            if (messaging == null) return;

            using FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(state);
            writer.WriteValueSafe(stage.Kills);
            writer.WriteValueSafe(stage.KillTarget);
            writer.WriteValueSafe(stage.Elapsed);
            messaging.SendNamedMessageToAll(MessageName, writer);
        }

        /// <summary>Client nhận trạng thái ải và ghi log (HUD thật sẽ do nhóm UI làm sau).</summary>
        private void HandleMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte state);
            reader.ReadValueSafe(out int kills);
            reader.ReadValueSafe(out int target);
            reader.ReadValueSafe(out float elapsed);

            string label = state switch
            {
                StateStarted => "BẮT ĐẦU",
                StateCleared => "THẮNG",
                StateFailed => "THUA",
                _ => "KHÔNG RÕ"
            };

            Debug.Log($"[Stage][Client] {label} - {kills}/{target} mạng sau {elapsed:0.0}s.");
        }
    }
}
