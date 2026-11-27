using System;
using System.Text;
using IronWasteland.Stages;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        private const byte StateWaveGate = 5;

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

        // Mã trận tăng dần ở host, gắn vào mọi gói trạng thái ải. Client dùng mã này để bỏ qua
        // gói của trận cũ còn tới muộn sau khi đã sang trận mới (named message không có thứ tự
        // xuyên phiên nên chỉ dựa vào thứ tự gói là không đủ).
        private const int MaxFailReasonBytes = 120;
        private uint m_MatchId;
        private uint m_CurrentMatchId;
        private bool m_HasCurrentMatchId;

        /// <summary>Mã trận hiện tại của host; client dùng để lọc gói trạng thái cũ.</summary>
        public uint MatchId => m_MatchId;

        /// <summary>
        /// Host bắt đầu một trận mới: tăng mã trận để client phân biệt gói của trận trước.
        /// Gọi một lần cho mỗi trận, trước khi gửi StateStarted.
        /// </summary>
        public void BeginMatch()
        {
            m_MatchId++;
            if (m_MatchId == 0) m_MatchId = 1;
        }

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
            // Giữ Instance của object persistent khi scene PHÒNG LAN bị nạp lại lúc chơi lại.
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

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

        /// <summary>
        /// Host gửi trạng thái ải hiện tại cho toàn bộ client từ StageDirector.
        /// Với trạng thái thua, gửi kèm lý do thật để hai máy hiện cùng một thông báo.
        /// </summary>
        public void Broadcast(StageDirector director, byte state, string reason = null)
        {
            if (director == null) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return;

            CustomMessagingManager messaging = manager.CustomMessagingManager;
            if (messaging == null) return;

            int target = director.KillTarget;
            byte[] reasonBytes = state == StateFailed ? EncodeFailReason(director, reason) : null;

            int capacity = 16 + 4 + (reasonBytes != null ? 1 + reasonBytes.Length : 0);
            using FastBufferWriter writer = new FastBufferWriter(capacity, Allocator.Temp);
            writer.WriteValueSafe(state);
            writer.WriteValueSafe(director.Kills);
            writer.WriteValueSafe(target);
            writer.WriteValueSafe(director.Elapsed);
            writer.WriteValueSafe(m_MatchId);
            if (reasonBytes != null)
            {
                writer.WriteValueSafe((byte)reasonBytes.Length);
                for (int i = 0; i < reasonBytes.Length; i++)
                {
                    writer.WriteValueSafe(reasonBytes[i]);
                }
            }

            messaging.SendNamedMessageToAll(MessageName, writer);

            ApplyState(state, director.Kills, target, director.Elapsed);
        }

        /// <summary>Chọn lý do thua ngắn gọn, tối đa 120 byte UTF-8 để vừa một gói tin.</summary>
        private static byte[] EncodeFailReason(StageDirector director, string reason)
        {
            string text = !string.IsNullOrEmpty(reason) ? reason : director.FailReason;
            if (string.IsNullOrEmpty(text)) text = "Thất bại";

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            if (bytes.Length <= MaxFailReasonBytes) return bytes;

            byte[] clipped = new byte[MaxFailReasonBytes];
            Array.Copy(bytes, clipped, MaxFailReasonBytes);
            return clipped;
        }

        /// <summary>Host gửi trạng thái thắng ải kèm phần thưởng đã roll cho toàn bộ client.</summary>
        public void BroadcastCleared(StageDirector director, StageRewardResult reward)
        {
            if (director == null) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return;

            CustomMessagingManager messaging = manager.CustomMessagingManager;
            if (messaging == null) return;

            int[] indices = reward.DroppedAccessoryIndices ?? System.Array.Empty<int>();
            int dropLen = Mathf.Min(indices.Length, 32);
            int capacity = 36 + dropLen * 4;
            int target = director.KillTarget;

            using FastBufferWriter writer = new FastBufferWriter(capacity, Allocator.Temp);
            writer.WriteValueSafe(StateCleared);
            writer.WriteValueSafe(director.Kills);
            writer.WriteValueSafe(target);
            writer.WriteValueSafe(director.Elapsed);
            writer.WriteValueSafe(reward.Gems);
            writer.WriteValueSafe(reward.AccessoryCount);
            writer.WriteValueSafe((byte)dropLen);
            for (int i = 0; i < dropLen; i++)
            {
                writer.WriteValueSafe(indices[i]);
            }
            writer.WriteValueSafe(m_MatchId);

            messaging.SendNamedMessageToAll(MessageName, writer);

            ApplyState(StateCleared, director.Kills, target, director.Elapsed);
        }

        /// <summary>Host gửi trạng thái ải hiện tại cho toàn bộ client.</summary>
        public void Broadcast(StageManager stage, byte state)
        {
            if (stage == null) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return;

            CustomMessagingManager messaging = manager.CustomMessagingManager;
            if (messaging == null) return;

            using FastBufferWriter writer = new FastBufferWriter(20, Allocator.Temp);
            writer.WriteValueSafe(state);
            writer.WriteValueSafe(stage.Kills);
            writer.WriteValueSafe(stage.KillTarget);
            writer.WriteValueSafe(stage.Elapsed);
            writer.WriteValueSafe(m_MatchId);
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

            using FastBufferWriter writer = new FastBufferWriter(20, Allocator.Temp);
            writer.WriteValueSafe(StateIdle);
            writer.WriteValueSafe(0);
            writer.WriteValueSafe(0);
            writer.WriteValueSafe(0f);
            writer.WriteValueSafe(m_MatchId);
            messaging.SendNamedMessageToAll(MessageName, writer);

            ResetState();
        }

        internal void SendWaveGateState(string sceneName, int waveIndex, bool opened, int firedWaves,
            ulong? targetClientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || manager.CustomMessagingManager == null) return;

            using FastBufferWriter writer = new FastBufferWriter(80, Allocator.Temp);
            writer.WriteValueSafe(StateWaveGate);
            writer.WriteValueSafe((FixedString64Bytes)sceneName);
            writer.WriteValueSafe(waveIndex);
            writer.WriteValueSafe(opened);
            writer.WriteValueSafe(firedWaves);
            if (targetClientId.HasValue)
                manager.CustomMessagingManager.SendNamedMessage(MessageName, targetClientId.Value, writer);
            else
                manager.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer);
        }

        internal void SendWaveGateSnapshot(ulong clientId, string sceneName)
        {
            foreach (StageWaveGate gate in FindObjectsByType<StageWaveGate>(FindObjectsInactive.Exclude))
            {
                if (gate.gameObject.scene.name == sceneName) gate.PublishState(clientId);
            }
        }

        /// <summary>Client nhận trạng thái ải và ghi log (HUD thật sẽ do nhóm UI làm sau).</summary>
        private void HandleMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return; // chỉ host được gửi trạng thái ải
            reader.ReadValueSafe(out byte state);
            if (state == StateWaveGate)
            {
                reader.ReadValueSafe(out FixedString64Bytes sceneName);
                reader.ReadValueSafe(out int index);
                reader.ReadValueSafe(out bool opened);
                reader.ReadValueSafe(out int firedWaves);
                Scene scene = SceneManager.GetActiveScene();
                if (scene.name != sceneName.ToString()) return;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (StageWaveGate gate in root.GetComponentsInChildren<StageWaveGate>())
                    {
                        if (gate.NextWaveIndex == index) gate.ApplyRemoteState(opened, firedWaves);
                    }
                }
                return;
            }
            reader.ReadValueSafe(out int kills);
            reader.ReadValueSafe(out int target);
            reader.ReadValueSafe(out float elapsed);

            bool hasReward = false;
            StageRewardResult reward = default;
            if (state == StateCleared && reader.Length - reader.Position >= 9)
            {
                reader.ReadValueSafe(out int gems);
                reader.ReadValueSafe(out int accCount);
                reader.ReadValueSafe(out byte dropLen);
                int[] dropped = dropLen > 0 ? new int[dropLen] : System.Array.Empty<int>();
                for (int i = 0; i < dropLen; i++)
                {
                    if (reader.Length - reader.Position < 4) break;
                    reader.ReadValueSafe(out dropped[i]);
                }
                reward = new StageRewardResult(gems, accCount, dropped);
                hasReward = true;
            }

            // Host mới ghi mã trận ngay sau phần thưởng (nếu có). Host cũ không có trường này:
            // khi đó coi như không lọc được và giữ hành vi cũ.
            bool hasMatchId = false;
            uint matchId = 0;
            if (reader.Length - reader.Position >= 4)
            {
                reader.ReadValueSafe(out matchId);
                hasMatchId = true;
            }

            // Lý do thua do host gửi kèm; host cũ không có thì dùng câu chung.
            string failReason = null;
            if (state == StateFailed && reader.Length - reader.Position >= 1)
            {
                reader.ReadValueSafe(out byte reasonLength);
                if (reasonLength > 0 && reader.Length - reader.Position >= reasonLength)
                {
                    byte[] reasonBytes = new byte[reasonLength];
                    for (int i = 0; i < reasonLength; i++)
                    {
                        reader.ReadValueSafe(out reasonBytes[i]);
                    }
                    failReason = Encoding.UTF8.GetString(reasonBytes);
                }
            }

            // Bỏ qua gói của trận cũ: named message của phiên trước có thể tới muộn sau khi
            // client đã vào trận mới, làm hiện nhầm bảng THUA/BẮT ĐẦU.
            if (state == StateIdle)
            {
                m_HasCurrentMatchId = false;
            }
            else if (state == StateStarted)
            {
                m_CurrentMatchId = matchId;
                m_HasCurrentMatchId = hasMatchId;
            }
            else if (!m_HasCurrentMatchId)
            {
                m_CurrentMatchId = matchId;
                m_HasCurrentMatchId = hasMatchId;
            }
            else if (hasMatchId && matchId != m_CurrentMatchId)
            {
                Debug.Log($"[Stage][Client] Bỏ qua trạng thái {state} của trận cũ (mã {matchId}, đang ở trận {m_CurrentMatchId}).");
                return;
            }

            ApplyState(state, kills, target, elapsed);

            StageDirector director = FindAnyObjectByType<StageDirector>();
            if (director != null)
            {
                director.SetKills(kills);
                if (state == StateStarted && director.State == StageRunState.Idle)
                {
                    director.Begin();
                }
                else if (state == StateCleared)
                {
                    if (hasReward) director.ForceClear(reward);
                    else director.ForceClear();
                }
                else if (state == StateFailed)
                {
                    director.ForceFail(string.IsNullOrEmpty(failReason) ? "Thất bại" : failReason);
                }
            }

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
