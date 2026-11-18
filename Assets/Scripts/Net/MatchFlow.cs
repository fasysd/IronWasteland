using System;
using System.Collections.Generic;
using IronWasteland.Stages;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>Giai đoạn của một phiên chơi (dùng chung cho lobby và trong trận).</summary>
    public enum MatchPhase : byte
    {
        Lobby = 0,
        Playing = 1,
        Cleared = 2,
        Failed = 3,
    }

    /// <summary>Chế độ chơi: một mình hoặc đồng đội.</summary>
    public enum MatchMode : byte
    {
        Coop = 0,
        Single = 1,
    }

    /// <summary>
    /// Điều phối vòng đời một phiên chơi: chọn màn → lobby (đổi tank, sẵn sàng) → vào trận →
    /// thắng/thua → chơi lại/thoát. Host là bên quyết định, client chỉ gửi yêu cầu.
    /// Dùng named message (không phải NetworkBehaviour) vì NGO cấm gắn NetworkBehaviour lên
    /// GameObject của NetworkManager.
    /// </summary>
    public class MatchFlow : MonoBehaviour
    {
        /// <summary>Tên message host gửi trạng thái phòng xuống client.</summary>
        public const string SnapshotMessage = "IW.Match";

        /// <summary>Tên message client gửi yêu cầu lên host.</summary>
        public const string RequestMessage = "IW.MatchRequest";

        public const byte OpSetTank = 0;
        public const byte OpSetReady = 1;
        public const byte OpStart = 2;
        public const byte OpReplay = 3;
        public const byte OpSetOption1 = 4;
        public const byte OpSetOption2 = 5;

        public const int MaxPlayers = 4;

        [Header("Tham chiếu")]
        [Tooltip("Bootstrap mạng; để trống sẽ lấy trên cùng GameObject.")]
        [SerializeField] private NetBootstrap bootstrap;

        [Header("Màn chơi")]
        [Tooltip("Danh sách màn host có thể chọn trong lobby.")]
        [SerializeField] private StageDefinition[] stageOptions;

        [Tooltip("Tự vào trận khi mọi người đã sẵn sàng.")]
        [SerializeField] private bool autoStartWhenAllReady = true;

        private readonly ulong[] m_ClientIds = new ulong[MaxPlayers];
        private readonly byte[] m_TankIndexes = new byte[MaxPlayers];
        private readonly bool[] m_Ready = new bool[MaxPlayers];
        private readonly byte[] m_Option1s = new byte[MaxPlayers];
        private readonly byte[] m_Option2s = new byte[MaxPlayers];
        private int m_PlayerCount;

        private bool m_Registered;
        private bool m_HostHooked;
        private bool m_StageHooked;

        /// <summary>Giai đoạn hiện tại của phiên.</summary>
        public MatchPhase Phase { get; private set; } = MatchPhase.Lobby;

        /// <summary>Chế độ chơi hiện tại.</summary>
        public MatchMode Mode { get; private set; } = MatchMode.Coop;

        /// <summary>Chỉ số màn đang chọn trong <see cref="stageOptions"/>.</summary>
        public int StageIndex { get; private set; }

        /// <summary>Số người đang ở trong phòng.</summary>
        public int PlayerCount => m_PlayerCount;

        /// <summary>Phát ra khi host đổi trạng thái phòng (client nhận snapshot cũng phát).</summary>
        public event Action Changed;

        private NetworkManager Manager => bootstrap != null ? bootstrap.Manager : NetworkManager.Singleton;

        private bool IsServer => Manager != null && Manager.IsServer;

        private bool Listening => Manager != null && Manager.IsListening;

        /// <summary>Màn đang chọn (null nếu chưa cấu hình).</summary>
        public StageDefinition SelectedStage =>
            stageOptions != null && stageOptions.Length > 0
                ? stageOptions[Mathf.Clamp(StageIndex, 0, stageOptions.Length - 1)]
                : null;

        /// <summary>Số màn có thể chọn.</summary>
        public int StageCount => stageOptions != null ? stageOptions.Length : 0;

        private void Awake()
        {
            if (bootstrap == null) bootstrap = GetComponent<NetBootstrap>();
        }

        private void Update()
        {
            if (!Listening)
            {
                if (m_Registered) Unregister();
                if (m_HostHooked) UnhookHost();
                return;
            }

            CustomMessagingManager messaging = Manager.CustomMessagingManager;
            if (messaging == null) return;

            if (!m_Registered)
            {
                messaging.RegisterNamedMessageHandler(SnapshotMessage, HandleSnapshot);
                messaging.RegisterNamedMessageHandler(RequestMessage, HandleRequest);
                m_Registered = true;
            }

            if (IsServer && !m_HostHooked) HookHost();
        }

        private void OnDestroy()
        {
            Unregister();
            UnhookHost();
            UnhookStage();
        }

        private void HookStage()
        {
            StageManager stage = bootstrap != null ? bootstrap.Stage : null;
            if (stage == null || m_StageHooked) return;

            stage.StageCleared -= HandleStageCleared;
            stage.StageCleared += HandleStageCleared;
            stage.StageFailed -= HandleStageFailed;
            stage.StageFailed += HandleStageFailed;
            m_StageHooked = true;
        }

        private void UnhookStage()
        {
            StageManager stage = bootstrap != null ? bootstrap.Stage : null;
            if (stage == null) return;

            stage.StageCleared -= HandleStageCleared;
            stage.StageFailed -= HandleStageFailed;
            m_StageHooked = false;
        }

        private void HandleStageCleared(StageManager stage)
        {
            if (!IsServer || Phase != MatchPhase.Playing) return;
            Phase = MatchPhase.Cleared;
            Broadcast();
        }

        private void HandleStageFailed(StageManager stage)
        {
            if (!IsServer || Phase != MatchPhase.Playing) return;
            Phase = MatchPhase.Failed;
            Broadcast();
        }

        private void RegisterIfNeeded()
        {
            CustomMessagingManager messaging = Manager != null ? Manager.CustomMessagingManager : null;
            if (messaging == null || m_Registered) return;

            messaging.RegisterNamedMessageHandler(SnapshotMessage, HandleSnapshot);
            messaging.RegisterNamedMessageHandler(RequestMessage, HandleRequest);
            m_Registered = true;
        }

        private void Unregister()
        {
            CustomMessagingManager messaging = Manager != null ? Manager.CustomMessagingManager : null;
            if (messaging != null)
            {
                messaging.UnregisterNamedMessageHandler(SnapshotMessage);
                messaging.UnregisterNamedMessageHandler(RequestMessage);
            }

            m_Registered = false;
        }

        private void HookHost()
        {
            if (Manager == null) return;
            Manager.OnClientConnectedCallback -= HandleServerClientConnected;
            Manager.OnClientConnectedCallback += HandleServerClientConnected;
            m_HostHooked = true;
        }

        private void UnhookHost()
        {
            if (Manager != null) Manager.OnClientConnectedCallback -= HandleServerClientConnected;
            m_HostHooked = false;
        }

        #region Host API

        /// <summary>Host gọi ngay sau khi StartHost(): đưa phiên về lobby và thêm chính host vào danh sách.</summary>
        public void OnHostStarted()
        {
            if (!IsServer) return;

            RegisterIfNeeded();
            HookHost();

            m_PlayerCount = 0;
            Phase = MatchPhase.Lobby;
            NetLoadout hostLoadout = bootstrap != null ? bootstrap.GetLoadout(NetBootstrap.ServerClientId) : default;
            AddOrUpdatePlayer(NetBootstrap.ServerClientId, bootstrap != null ? bootstrap.LocalTankIndex : (byte)0, true,
                (byte)hostLoadout.OptionSkill1, (byte)hostLoadout.OptionSkill2);
            Broadcast();
        }

        /// <summary>Chế độ chơi đơn: vào trận ngay với một mình host.</summary>
        public void StartSingle()
        {
            if (!IsServer) return;

            if (m_PlayerCount == 0)
            {
                RegisterIfNeeded();
                HookHost();
                NetLoadout hostLoadout = bootstrap != null ? bootstrap.GetLoadout(NetBootstrap.ServerClientId) : default;
                AddOrUpdatePlayer(NetBootstrap.ServerClientId, bootstrap != null ? bootstrap.LocalTankIndex : (byte)0, true,
                    (byte)hostLoadout.OptionSkill1, (byte)hostLoadout.OptionSkill2);
            }

            Mode = MatchMode.Single;
            StartMatchInternal();
        }

        /// <summary>Host chọn chế độ (chỉ khi còn ở lobby).</summary>
        public void SetMode(MatchMode mode)
        {
            if (!IsServer || Phase != MatchPhase.Lobby) return;
            Mode = mode;
            Broadcast();
        }

        /// <summary>Host đổi màn đang chọn (chỉ khi còn ở lobby).</summary>
        public void SetStage(int index)
        {
            if (!IsServer || Phase != MatchPhase.Lobby || StageCount == 0) return;
            StageIndex = Mathf.Clamp(index, 0, StageCount - 1);
            Broadcast();
        }

        /// <summary>Host xoay vòng màn đang chọn.</summary>
        public void CycleStage()
        {
            if (StageCount == 0) return;
            SetStage((StageIndex + 1) % StageCount);
        }

        /// <summary>Host bắt đầu trận (bỏ qua điều kiện sẵn sàng khi gọi trực tiếp).</summary>
        public void StartMatch()
        {
            if (!IsServer || Phase != MatchPhase.Lobby) return;
            StartMatchInternal();
        }

        /// <summary>Host cho cả phòng về lobby để chơi lại.</summary>
        public void Replay()
        {
            if (!IsServer) return;

            bootstrap?.DespawnAllTanks();
            bootstrap?.Stage?.ResetToIdle();

            for (int i = 0; i < m_PlayerCount; i++) m_Ready[i] = false;
            m_Ready[0] = true; // host luôn sẵn sàng
            Phase = MatchPhase.Lobby;
            Broadcast();
        }

        /// <summary>Đóng phòng ở phía máy này.</summary>
        public void Leave()
        {
            bootstrap?.Shutdown();
            ResetLocal();
            Changed?.Invoke();
        }

        private void StartMatchInternal()
        {
            StageDefinition stage = SelectedStage;
            if (stage == null)
            {
                Debug.LogWarning("[MatchFlow] Chưa cấu hình màn chơi — không thể bắt đầu.", this);
                return;
            }

            bootstrap?.Stage?.SetDefinition(stage);
            HookStage();

            for (int i = 0; i < m_PlayerCount; i++)
            {
                bootstrap?.SpawnTankFor(m_ClientIds[i], m_TankIndexes[i], m_Option1s[i], m_Option2s[i]);
            }

            Phase = MatchPhase.Playing;
            Broadcast();

            bootstrap?.Stage?.StartStage();
            Debug.Log($"[MatchFlow] Vào trận màn {stage.StageCode} - {stage.StageName} ({Mode}, {m_PlayerCount} người).");
        }

        private void StartMatchIfReady()
        {
            if (!autoStartWhenAllReady || Phase != MatchPhase.Lobby || m_PlayerCount == 0) return;

            // Co-op phải có ít nhất 2 người mới tự vào trận; một mình thì để host bấm "Bắt đầu".
            if (Mode == MatchMode.Coop && m_PlayerCount < 2) return;

            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (!m_Ready[i]) return;
            }

            StartMatchInternal();
        }

        private void AddOrUpdatePlayer(ulong clientId, byte tankIndex, bool ready, byte option1, byte option2)
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_ClientIds[i] != clientId) continue;
                m_TankIndexes[i] = tankIndex;
                m_Ready[i] = ready;
                m_Option1s[i] = option1;
                m_Option2s[i] = option2;
                return;
            }

            if (m_PlayerCount >= MaxPlayers) return;

            m_ClientIds[m_PlayerCount] = clientId;
            m_TankIndexes[m_PlayerCount] = tankIndex;
            m_Ready[m_PlayerCount] = ready;
            m_Option1s[m_PlayerCount] = option1;
            m_Option2s[m_PlayerCount] = option2;
            m_PlayerCount++;
        }

        private void RemovePlayer(ulong clientId)
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_ClientIds[i] != clientId) continue;

                for (int j = i; j < m_PlayerCount - 1; j++)
                {
                    m_ClientIds[j] = m_ClientIds[j + 1];
                    m_TankIndexes[j] = m_TankIndexes[j + 1];
                    m_Ready[j] = m_Ready[j + 1];
                    m_Option1s[j] = m_Option1s[j + 1];
                    m_Option2s[j] = m_Option2s[j + 1];
                }

                m_PlayerCount--;
                return;
            }
        }

        private void HandleServerClientConnected(ulong clientId)
        {
            if (!IsServer) return;

            byte tankIndex = bootstrap != null ? bootstrap.GetLoadoutTankIndex(clientId) : (byte)0;
            NetLoadout loadout = bootstrap != null ? bootstrap.GetLoadout(clientId) : default;
            AddOrUpdatePlayer(clientId, tankIndex, false, (byte)loadout.OptionSkill1, (byte)loadout.OptionSkill2);

            // Người vào giữa trận: spawn xe ngay với tank đã chọn để không bị đứng ngoài.
            if (Phase == MatchPhase.Playing)
            {
                bootstrap?.SpawnTankFor(clientId, tankIndex, loadout.OptionSkill1, loadout.OptionSkill2);
            }

            Broadcast();
        }

        /// <summary>NetBootstrap gọi khi một client vừa kết nối (đã lưu loadout).</summary>
        public void OnClientJoined(ulong clientId)
        {
            if (!IsServer) return;
            HandleServerClientConnected(clientId);
        }

        /// <summary>NetBootstrap gọi khi một client rời phòng.</summary>
        public void OnClientLeft(ulong clientId)
        {
            if (!IsServer) return;
            RemovePlayer(clientId);
            Broadcast();
        }

        private void ResetLocal()
        {
            m_PlayerCount = 0;
            Phase = MatchPhase.Lobby;
            Mode = MatchMode.Coop;
            StageIndex = 0;
        }

        #endregion

        #region Client requests

        /// <summary>Client xin đổi tank trong lobby.</summary>
        public void RequestTank(byte tankIndex)
        {
            if (!Listening) return;
            if (IsServer)
            {
                ApplyTankRequest(NetBootstrap.ServerClientId, tankIndex);
                return;
            }

            SendRequest(OpSetTank, tankIndex);
        }

        /// <summary>Client xin đổi biến thể Optional Skill (mỗi nhóm 0..2) trong lobby.</summary>
        public void RequestOptionSkills(byte option1, byte option2)
        {
            if (!Listening) return;

            if (IsServer)
            {
                ApplyOptionRequest(NetBootstrap.ServerClientId, true, option1);
                ApplyOptionRequest(NetBootstrap.ServerClientId, false, option2);
                return;
            }

            SendRequest(OpSetOption1, option1);
            SendRequest(OpSetOption2, option2);
        }

        /// <summary>Client xin đổi trạng thái sẵn sàng.</summary>
        public void RequestReady(bool ready)
        {
            if (!Listening) return;
            if (IsServer)
            {
                ApplyReadyRequest(NetBootstrap.ServerClientId, ready);
                return;
            }

            SendRequest(OpSetReady, ready ? (byte)1 : (byte)0);
        }

        /// <summary>Client xin host chơi lại.</summary>
        public void RequestReplay()
        {
            if (!Listening) return;
            if (IsServer)
            {
                Replay();
                return;
            }

            SendRequest(OpReplay, 0);
        }

        private void SendRequest(byte op, byte value)
        {
            CustomMessagingManager messaging = Manager != null ? Manager.CustomMessagingManager : null;
            if (messaging == null) return;

            using FastBufferWriter writer = new FastBufferWriter(2, Allocator.Temp);
            writer.WriteValueSafe(op);
            writer.WriteValueSafe(value);
            messaging.SendNamedMessage(RequestMessage, NetBootstrap.ServerClientId, writer);
        }

        private void ApplyTankRequest(ulong clientId, byte tankIndex)
        {
            if (!IsServer || Phase != MatchPhase.Lobby) return;

            int count = bootstrap != null && bootstrap.Spawner != null ? bootstrap.Spawner.TankCount : 0;
            if (count > 0 && tankIndex >= count) return;

            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_ClientIds[i] != clientId) continue;
                m_TankIndexes[i] = tankIndex;
                m_Ready[i] = false;
                break;
            }

            Broadcast();
        }

        private void ApplyOptionRequest(ulong clientId, bool firstOption, byte value)
        {
            if (!IsServer || Phase != MatchPhase.Lobby) return;

            byte clamped = (byte)Mathf.Clamp((int)value, 0, 2);
            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_ClientIds[i] != clientId) continue;
                if (firstOption) m_Option1s[i] = clamped;
                else m_Option2s[i] = clamped;
                m_Ready[i] = false;
                break;
            }

            Broadcast();
        }

        private void ApplyReadyRequest(ulong clientId, bool ready)
        {
            if (!IsServer || Phase != MatchPhase.Lobby) return;

            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_ClientIds[i] != clientId) continue;
                m_Ready[i] = ready;
                break;
            }

            Broadcast();
            StartMatchIfReady();
        }

        #endregion

        #region Replication

        /// <summary>Host phát snapshot trạng thái phòng cho toàn bộ client.</summary>
        public void Broadcast()
        {
            if (!IsServer) return;

            CustomMessagingManager messaging = Manager != null ? Manager.CustomMessagingManager : null;
            if (messaging == null) return;

            using FastBufferWriter writer = new FastBufferWriter(96, Allocator.Temp);
            writer.WriteValueSafe((byte)Phase);
            writer.WriteValueSafe((byte)Mode);
            writer.WriteValueSafe((byte)Mathf.Clamp(StageIndex, 0, 255));
            writer.WriteValueSafe((byte)m_PlayerCount);

            for (int i = 0; i < m_PlayerCount; i++)
            {
                writer.WriteValueSafe(m_ClientIds[i]);
                writer.WriteValueSafe(m_TankIndexes[i]);
                writer.WriteValueSafe(m_Ready[i]);
                writer.WriteValueSafe(m_Option1s[i]);
                writer.WriteValueSafe(m_Option2s[i]);
            }

            messaging.SendNamedMessageToAll(SnapshotMessage, writer);
            Changed?.Invoke();
        }

        private void HandleSnapshot(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetBootstrap.ServerClientId) return;

            try
            {
                reader.ReadValueSafe(out byte phase);
                reader.ReadValueSafe(out byte mode);
                reader.ReadValueSafe(out byte stageIndex);
                reader.ReadValueSafe(out byte count);

                Phase = (MatchPhase)phase;
                Mode = (MatchMode)mode;
                StageIndex = stageIndex;
                m_PlayerCount = Mathf.Clamp((int)count, 0, MaxPlayers);

                for (int i = 0; i < m_PlayerCount; i++)
                {
                    reader.ReadValueSafe(out m_ClientIds[i]);
                    reader.ReadValueSafe(out m_TankIndexes[i]);
                    reader.ReadValueSafe(out m_Ready[i]);
                    reader.ReadValueSafe(out m_Option1s[i]);
                    reader.ReadValueSafe(out m_Option2s[i]);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[MatchFlow] Snapshot phòng không hợp lệ: " + exception.Message);
                return;
            }

            Changed?.Invoke();
        }

        private void HandleRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsServer || senderClientId == NetBootstrap.ServerClientId) return;

            try
            {
                reader.ReadValueSafe(out byte op);
                reader.ReadValueSafe(out byte value);

                switch (op)
                {
                    case OpSetTank:
                        ApplyTankRequest(senderClientId, value);
                        break;
                    case OpSetReady:
                        ApplyReadyRequest(senderClientId, value != 0);
                        break;
                    case OpSetOption1:
                        ApplyOptionRequest(senderClientId, true, value);
                        break;
                    case OpSetOption2:
                        ApplyOptionRequest(senderClientId, false, value);
                        break;
                    case OpReplay:
                        if (Phase == MatchPhase.Cleared || Phase == MatchPhase.Failed) Replay();
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MatchFlow] Yêu cầu từ client {senderClientId} không hợp lệ: {exception.Message}");
            }
        }

        #endregion

        #region Truy vấn cho UI

        /// <summary>Lấy thông tin người chơi theo chỉ số trong danh sách phòng.</summary>
        public bool TryGetPlayer(int index, out ulong clientId, out byte tankIndex, out bool ready)
        {
            return TryGetPlayer(index, out clientId, out tankIndex, out ready, out _, out _);
        }

        /// <summary>Lấy thông tin người chơi kèm biến thể Optional Skill trong lobby.</summary>
        public bool TryGetPlayer(int index, out ulong clientId, out byte tankIndex, out bool ready,
            out byte option1, out byte option2)
        {
            clientId = 0UL;
            tankIndex = 0;
            ready = false;
            option1 = 0;
            option2 = 0;

            if (index < 0 || index >= m_PlayerCount) return false;

            clientId = m_ClientIds[index];
            tankIndex = m_TankIndexes[index];
            ready = m_Ready[index];
            option1 = m_Option1s[index];
            option2 = m_Option2s[index];
            return true;
        }

        /// <summary>Tank của máy này đang chọn trong lobby (client lấy từ snapshot).</summary>
        public byte LocalTankIndex
        {
            get
            {
                ulong localId = Manager != null ? Manager.LocalClientId : 0UL;
                for (int i = 0; i < m_PlayerCount; i++)
                {
                    if (m_ClientIds[i] == localId) return m_TankIndexes[i];
                }

                return bootstrap != null ? bootstrap.LocalTankIndex : (byte)0;
            }
        }

        /// <summary>Biến thể Optional Skill 1 của máy này (client lấy từ snapshot).</summary>
        public byte LocalOption1
        {
            get
            {
                ulong localId = Manager != null ? Manager.LocalClientId : 0UL;
                for (int i = 0; i < m_PlayerCount; i++)
                {
                    if (m_ClientIds[i] == localId) return m_Option1s[i];
                }

                return bootstrap != null ? (byte)bootstrap.LocalOptionSkill1 : (byte)0;
            }
        }

        /// <summary>Biến thể Optional Skill 2 của máy này (client lấy từ snapshot).</summary>
        public byte LocalOption2
        {
            get
            {
                ulong localId = Manager != null ? Manager.LocalClientId : 0UL;
                for (int i = 0; i < m_PlayerCount; i++)
                {
                    if (m_ClientIds[i] == localId) return m_Option2s[i];
                }

                return bootstrap != null ? (byte)bootstrap.LocalOptionSkill2 : (byte)0;
            }
        }

        /// <summary>Máy này đã sẵn sàng chưa.</summary>
        public bool LocalReady
        {
            get
            {
                ulong localId = Manager != null ? Manager.LocalClientId : 0UL;
                for (int i = 0; i < m_PlayerCount; i++)
                {
                    if (m_ClientIds[i] == localId) return m_Ready[i];
                }

                return false;
            }
        }

        #endregion
    }
}
