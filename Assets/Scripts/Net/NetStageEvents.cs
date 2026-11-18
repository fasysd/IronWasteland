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

        /// <summary>Mã trạng thái: ải đang chạy (gói cập nhật số mạng/thời gian định kỳ).</summary>
        public const byte StateRunning = 3;

        /// <summary>Mã trạng thái: không có ải nào (về lobby / tạo phòng mới) — xoá cache phía client.</summary>
        public const byte StateIdle = 4;

        /// <summary>Instance đang hoạt động (mỗi máy chỉ có một).</summary>
        public static NetStageEvents Instance { get; private set; }

        /// <summary>Đã từng nhận trạng thái ải nào chưa (dùng cho HUD debug).</summary>
        public bool HasStageState { get; private set; }

        /// <summary>Mã trạng thái ải gần nhất.</summary>
        public byte StageState { get; private set; }

        /// <summary>Số mạng đã hạ tại lần cập nhật gần nhất.</summary>
        public int StageKills { get; private set; }

        /// <summary>Mục tiêu mạng của ải.</summary>
        public int StageKillTarget { get; private set; }

        /// <summary>Thời điểm (giây) của lần cập nhật gần nhất.</summary>
        public float StageElapsed { get; private set; }

        private bool m_Registered;

        /// <summary>Xoá trạng thái ải đã nhớ (dùng khi tạo phòng mới để HUD không còn hiện THẮNG/THUA cũ).</summary>
        public void ResetState()
        {
            HasStageState = false;
            StageState = StateStarted;
            StageKills = 0;
            StageKillTarget = 0;
            StageElapsed = 0f;
        }

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

            // Host cũng cần trạng thái mới nhất để HUD đọc (SendNamedMessageToAll không gửi cho chính host).
            ApplyState(state, stage.Kills, stage.KillTarget, stage.Elapsed);
        }

        /// <summary>
        /// Host gửi nhịp cập nhật số mạng/thời gian khi ải đang chạy.
        /// Không log mỗi gói để tránh spam; client chỉ cập nhật cache cho HUD và màn kết quả.
        /// </summary>
        public void BroadcastStatus(StageManager stage)
        {
            Broadcast(stage, StateRunning);
        }

        /// <summary>
        /// Host báo "không còn ải" (về lobby / tạo phòng mới) để client xoá cache THẮNG/THUA cũ.
        /// Nếu không gửi, client giữ trạng thái của trận trước và HUD hiện banner THUA ngay khi vào lobby.
        /// </summary>
        public void BroadcastIdle()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return;

            CustomMessagingManager messaging = manager.CustomMessagingManager;
            if (messaging == null) return;

            using FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(StateIdle);
            writer.WriteValueSafe(0);
            writer.WriteValueSafe(0);
            writer.WriteValueSafe(0f);
            messaging.SendNamedMessageToAll(MessageName, writer);

            ResetState();
        }

        /// <summary>Client nhận trạng thái ải và ghi log (HUD thật sẽ do nhóm UI làm sau).</summary>
        private void HandleMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return; // chỉ host được gửi trạng thái ải
            reader.ReadValueSafe(out byte state);
            reader.ReadValueSafe(out int kills);
            reader.ReadValueSafe(out int target);
            reader.ReadValueSafe(out float elapsed);

            ApplyState(state, kills, target, elapsed);

            if (state == StateRunning || state == StateIdle) return; // gói nhịp / gói xoá cache không log

            string label = state switch
            {
                StateStarted => "BẮT ĐẦU",
                StateCleared => "THẮNG",
                StateFailed => "THUA",
                _ => "KHÔNG RÕ"
            };

            Debug.Log($"[Stage][Client] {label} - {kills}/{target} mạng sau {elapsed:0.0}s.");
        }

        /// <summary>Ghi nhớ trạng thái ải mới nhất để HUD/debug đọc lại.</summary>
        private void ApplyState(byte state, int kills, int target, float elapsed)
        {
            StageState = state;
            StageKills = kills;
            StageKillTarget = target;
            StageElapsed = elapsed;
            HasStageState = state != StateIdle;
        }
    }
}
