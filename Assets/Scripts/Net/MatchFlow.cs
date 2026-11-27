using System;
using System.Collections.Generic;
using IronWasteland.Save;
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

        /// <summary>Tên message host yêu cầu client chuyển sang scene màn chơi / lobby.</summary>
        public const string SceneSyncMessage = "IW.Scene";

        public const byte OpSetTank = 0;
        public const byte OpSetReady = 1;
        public const byte OpStart = 2;
        public const byte OpReplay = 3;
        public const byte OpSetOption1 = 4;
        public const byte OpSetOption2 = 5;
        public const byte OpSetLoadout = 7;
        public const byte OpSceneLoaded = 8;

        public const int MaxPlayers = 4;

        [Header("Tham chiếu")]
        [Tooltip("Bootstrap mạng; để trống sẽ lấy trên cùng GameObject.")]
        [SerializeField] private NetBootstrap bootstrap;

        [Header("Màn chơi")]
        [Tooltip("Danh sách màn host có thể chọn trong lobby.")]
        [SerializeField] private StageDefinition[] stageOptions;

        private readonly ulong[] m_ClientIds = new ulong[MaxPlayers];
        private readonly byte[] m_TankIndexes = new byte[MaxPlayers];
        private readonly bool[] m_Ready = new bool[MaxPlayers];
        private readonly byte[] m_Option1s = new byte[MaxPlayers];
        private readonly byte[] m_Option2s = new byte[MaxPlayers];
        private int m_PlayerCount;

        private bool m_Registered;
        private bool m_HostHooked;
        private bool m_StageHooked;
        private string m_PendingTargetScene;
        private bool m_WaitingForSceneLoad;
        private Coroutine m_SceneLoadCoroutine;
        private Coroutine m_ClientSceneLoadCoroutine;
        private readonly HashSet<ulong> m_SceneReadyClients = new HashSet<ulong>();
        private StageDirector m_Director;

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
                StopSceneLoadCoroutines();
                return;
            }

            CustomMessagingManager messaging = Manager.CustomMessagingManager;
            if (messaging == null) return;

            if (!m_Registered)
            {
                messaging.RegisterNamedMessageHandler(SnapshotMessage, HandleSnapshot);
                messaging.RegisterNamedMessageHandler(RequestMessage, HandleRequest);
                messaging.RegisterNamedMessageHandler(SceneSyncMessage, HandleSceneSync);
                m_Registered = true;
            }

            if (IsServer && !m_HostHooked)
            {
                HookHost();
            }
        }

        private void OnDestroy()
        {
            Unregister();
            UnhookHost();
            UnhookStage();
            UnhookDirector();
            StopSceneLoadCoroutines();
        }

        private void StopSceneLoadCoroutines()
        {
            if (m_SceneLoadCoroutine != null)
            {
                StopCoroutine(m_SceneLoadCoroutine);
                m_SceneLoadCoroutine = null;
            }
            if (m_ClientSceneLoadCoroutine != null)
            {
                StopCoroutine(m_ClientSceneLoadCoroutine);
                m_ClientSceneLoadCoroutine = null;
            }
            m_WaitingForSceneLoad = false;
            m_PendingTargetScene = null;
            m_SceneReadyClients.Clear();
        }

        /// <summary>
        /// Xoá trạng thái ải trên host và báo client xoá cache tương ứng.
        /// Nếu chỉ ResetState cục bộ, client vẫn giữ THẮNG/THUA của trận trước nên HUD hiện
        /// banner sai ngay khi vừa về lobby hoặc vừa vào trận mới.
        /// </summary>
        private static void ResetStageEventsState()
        {
            NetStageEvents events = NetStageEvents.Instance;
            if (events == null) return;

            events.ResetState();
            events.BroadcastIdle();
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
            messaging.RegisterNamedMessageHandler(SceneSyncMessage, HandleSceneSync);
            m_Registered = true;
        }

        private void Unregister()
        {
            CustomMessagingManager messaging = Manager != null ? Manager.CustomMessagingManager : null;
            if (messaging != null)
            {
                messaging.UnregisterNamedMessageHandler(SnapshotMessage);
                messaging.UnregisterNamedMessageHandler(RequestMessage);
                messaging.UnregisterNamedMessageHandler(SceneSyncMessage);
            }

            m_Registered = false;
        }

        private void HookHost()
        {
            // Roster do NetBootstrap.HandleClientConnected gọi OnClientJoined; MatchFlow KHÔNG đăng ký
            // callback OnClientConnected riêng để tránh xử lý một client hai lần (add/spawn/broadcast trùng).
            m_HostHooked = true;
        }

        private void UnhookHost()
        {
            m_HostHooked = false;
        }

        /// <summary>
        /// Adapter cho callback <c>Action&lt;ulong&gt;</c> của NGO: hàm xử lý chính trả bool
        /// (để NetBootstrap biết phòng đầy mà ngắt client), còn callback thì bắt buộc void.
        /// </summary>
        private void HandleServerClientConnectedCallback(ulong clientId)
        {
            HandleServerClientConnected(clientId);
        }

        #region Host API

        /// <summary>Host gọi ngay sau khi StartHost(): đưa phiên về lobby và thêm chính host vào danh sách.</summary>
        public void OnHostStarted()
        {
            if (!IsServer) return;

            RegisterIfNeeded();
            HookHost();

            // Dọn sạch trạng thái phiên cũ; PrepareNewSession chỉ reset cục bộ nên gọi lặp vô hại.
            PrepareNewSession();
            NetLoadout hostLoadout = bootstrap != null ? bootstrap.GetLoadout(NetBootstrap.ServerClientId) : default;
            AddOrUpdatePlayer(NetBootstrap.ServerClientId, bootstrap != null ? bootstrap.LocalTankIndex : (byte)0, true,
                (byte)hostLoadout.OptionSkill1, (byte)hostLoadout.OptionSkill2);
            Broadcast();
        }

        /// <summary>
        /// Dọn trạng thái phiên cũ trên máy này trước khi mở/join phiên mới: despawn xe, đưa Phase
        /// về Lobby, reset ải + cache sự kiện ải. Nếu bỏ bước này, host mới có thể vẫn ở Phase Playing
        /// và spawn xe ngay trong lobby (hoặc quái của ải cũ còn sót khi về menu).
        /// </summary>
        public void PrepareNewSession()
        {
            bootstrap?.DespawnAllTanks();
            bootstrap?.Stage?.ResetToIdle();
            ResetStageEventsState();

            m_PlayerCount = 0;
            Phase = MatchPhase.Lobby;
            Mode = MatchMode.Coop;
            for (int i = 0; i < MaxPlayers; i++) m_Ready[i] = false;

            Changed?.Invoke();
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

        /// <summary>Host chọn màn theo mã ải (ví dụ "1-2"). Trả false nếu ải chưa có StageDefinition.</summary>
        public bool SelectStageByCode(string stageCode)
        {
            if (!IsServer || Phase != MatchPhase.Lobby || string.IsNullOrWhiteSpace(stageCode)) return false;

            for (int i = 0; i < StageCount; i++)
            {
                if (stageOptions[i] != null && string.Equals(stageOptions[i].StageCode, stageCode.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    SetStage(i);
                    return true;
                }
            }

            Debug.LogWarning("[MatchFlow] Không tìm thấy StageDefinition cho ải " + stageCode);
            return false;
        }

        /// <summary>Tra tên ải theo mã (ví dụ "1-2") mà không đổi màn đang chọn. Trả chuỗi rỗng nếu không tìm thấy.</summary>
        public string GetStageNameByCode(string stageCode)
        {
            if (string.IsNullOrWhiteSpace(stageCode)) return string.Empty;

            for (int i = 0; i < StageCount; i++)
            {
                if (stageOptions[i] != null && string.Equals(stageOptions[i].StageCode, stageCode.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return stageOptions[i].StageName;
                }
            }

            return string.Empty;
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
            UnhookDirector();
            ResetStageEventsState();

            for (int i = 0; i < m_PlayerCount; i++) m_Ready[i] = false;
            m_Ready[0] = true; // host luôn sẵn sàng
            Phase = MatchPhase.Lobby;
            Broadcast();

            string lobbyScene = Application.CanStreamedLevelBeLoaded("CoopLobby") ? "CoopLobby" : "SampleScene";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != lobbyScene && Application.CanStreamedLevelBeLoaded(lobbyScene))
            {
                StopSceneLoadCoroutines();
                SendSceneSyncToAll(lobbyScene);
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(lobbyScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }

        /// <summary>Đóng phòng ở phía máy này.</summary>
        public void Leave()
        {
            if (IsServer && Phase == MatchPhase.Playing)
            {
                Phase = MatchPhase.Failed;
                StageDirector director = FindAnyObjectByType<StageDirector>();
                if (director != null)
                {
                    director.ForceFail("Chủ phòng đã thoát trận.");
                    NetStageEvents.Instance?.Broadcast(director, NetStageEvents.StateFailed);
                }
            }

            UnhookDirector();
            StopSceneLoadCoroutines();
            bootstrap?.Shutdown();
            ResetLocal();
            Changed?.Invoke();

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene != "SampleScene" && currentScene != "CoopLobby" && currentScene != "StageSelect" && currentScene != "MainMenu")
            {
                Time.timeScale = 1f;
                if (Application.CanStreamedLevelBeLoaded("StageSelect"))
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("StageSelect");
                }
            }
        }

        private void StartMatchInternal()
        {
            StageDefinition stage = SelectedStage;
            if (stage == null)
            {
                Debug.LogWarning("[MatchFlow] Chưa cấu hình màn chơi — không thể bắt đầu.", this);
                return;
            }

            string sceneName = SaveSystem.SceneNameFor(stage.StageCode);
            if (IsServer && !string.IsNullOrEmpty(sceneName) && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != sceneName && Application.CanStreamedLevelBeLoaded(sceneName))
            {
                if (m_SceneLoadCoroutine != null) StopCoroutine(m_SceneLoadCoroutine);
                Debug.Log($"[MatchFlow] Chuyển cả phòng sang scene '{sceneName}' cho màn {stage.StageCode}...");
                m_SceneLoadCoroutine = StartCoroutine(LoadSceneForMatchRoutine(sceneName));
                return;
            }

            OnStageSceneLoaded();
        }

        private System.Collections.IEnumerator LoadSceneForMatchRoutine(string sceneName)
        {
            m_PendingTargetScene = sceneName;
            m_WaitingForSceneLoad = true;
            m_SceneReadyClients.Clear();

            SendSceneSyncToAll(sceneName);

            AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            while (op != null && !op.isDone)
            {
                yield return null;
            }

            m_SceneReadyClients.Add(NetBootstrap.ServerClientId);

            // Chờ toàn bộ client đang kết nối load xong scene (tối đa 10s để tránh kẹt nếu client rớt).
            float elapsed = 0f;
            const float timeout = 10f;
            while (elapsed < timeout && IsServer && Listening && !AllConnectedClientsSceneReady())
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            m_WaitingForSceneLoad = false;
            m_PendingTargetScene = null;
            m_SceneLoadCoroutine = null;

            yield return null;
            if (IsServer && Listening)
            {
                OnStageSceneLoaded();
            }
        }

        private bool AllConnectedClientsSceneReady()
        {
            if (Manager == null) return true;
            IReadOnlyList<ulong> ids = Manager.ConnectedClientsIds;
            for (int i = 0; i < ids.Count; i++)
            {
                if (!m_SceneReadyClients.Contains(ids[i])) return false;
            }
            return true;
        }

        private void OnStageSceneLoaded()
        {
            StageDefinition stage = SelectedStage;
            ResetStageEventsState();

            if (bootstrap?.Stage != null)
            {
                bootstrap.Stage.CoopMode = (Mode == MatchMode.Coop);
                if (stage != null) bootstrap.Stage.SetDefinition(stage);
                bootstrap.Stage.ResetToIdle();
                HookStage();
            }

            StageDirector director = FindAnyObjectByType<StageDirector>();
            if (director != null)
            {
                director.CoopMode = (Mode == MatchMode.Coop);
                HookDirector(director);
            }

            for (int i = 0; i < m_PlayerCount; i++)
            {
                bootstrap?.SpawnTankFor(m_ClientIds[i], m_TankIndexes[i], m_Option1s[i], m_Option2s[i]);
            }

            Phase = MatchPhase.Playing;
            Broadcast();

            if (director != null)
            {
                director.Begin();
                NetStageEvents.Instance?.Broadcast(director, NetStageEvents.StateStarted);
            }
            else if (bootstrap?.Stage != null)
            {
                bootstrap.Stage.StartStage();
            }

            Debug.Log($"[MatchFlow] Đã vào trận màn {(stage != null ? stage.StageCode : "N/A")} ({Mode}, {m_PlayerCount} người).");
        }

        private void HookDirector(StageDirector director)
        {
            UnhookDirector();
            m_Director = director;
            if (m_Director != null)
            {
                m_Director.StageCleared += HandleDirectorCleared;
                m_Director.StageFailed += HandleDirectorFailed;
            }
        }

        private void UnhookDirector()
        {
            if (m_Director != null)
            {
                m_Director.StageCleared -= HandleDirectorCleared;
                m_Director.StageFailed -= HandleDirectorFailed;
                m_Director = null;
            }
        }

        private void HandleDirectorCleared(StageDirector d, StageRewardResult reward)
        {
            if (!IsServer || Phase != MatchPhase.Playing) return;
            Phase = MatchPhase.Cleared;
            Broadcast();
            NetStageEvents.Instance?.BroadcastCleared(d, reward);
        }

        private void HandleDirectorFailed(StageDirector d, string reason)
        {
            if (!IsServer || Phase != MatchPhase.Playing) return;
            Phase = MatchPhase.Failed;
            Broadcast();
            NetStageEvents.Instance?.Broadcast(d, NetStageEvents.StateFailed);
        }

        private bool AddOrUpdatePlayer(ulong clientId, byte tankIndex, bool ready, byte option1, byte option2)
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_ClientIds[i] != clientId) continue;
                m_TankIndexes[i] = tankIndex;
                m_Ready[i] = ready;
                m_Option1s[i] = option1;
                m_Option2s[i] = option2;
                return true;
            }

            if (m_PlayerCount >= MaxPlayers) return false;

            m_ClientIds[m_PlayerCount] = clientId;
            m_TankIndexes[m_PlayerCount] = tankIndex;
            m_Ready[m_PlayerCount] = ready;
            m_Option1s[m_PlayerCount] = option1;
            m_Option2s[m_PlayerCount] = option2;
            m_PlayerCount++;
            return true;
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

        private bool HandleServerClientConnected(ulong clientId)
        {
            if (!IsServer) return false;

            byte tankIndex = bootstrap != null ? bootstrap.GetLoadoutTankIndex(clientId) : (byte)0;
            NetLoadout loadout = bootstrap != null ? bootstrap.GetLoadout(clientId) : default;
            bool added = AddOrUpdatePlayer(clientId, tankIndex, false, (byte)loadout.OptionSkill1, (byte)loadout.OptionSkill2);

            if (!added)
            {
                // Cổng duyệt của NGO có thể lọt 2 request cùng frame; chốt chặn cuối ở đây
                // để không bao giờ spawn xe thứ 5 nằm ngoài danh sách phòng.
                Debug.LogWarning($"[MatchFlow] Từ chối client {clientId}: phòng đã đủ {MaxPlayers} người.");
                bootstrap?.Manager?.DisconnectClient(clientId, "Phòng đã đủ 4 người.");
                return false;
            }

            // Người vào giữa trận: báo client chuyển sang scene màn chơi và spawn xe ngay.
            if (Phase == MatchPhase.Playing)
            {
                string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                SendSceneSyncToClient(clientId, activeScene);
                bootstrap?.SpawnTankFor(clientId, tankIndex, loadout.OptionSkill1, loadout.OptionSkill2);
            }

            Broadcast();
            return true;
        }

        /// <summary>NetBootstrap gọi khi một client vừa kết nối (đã lưu loadout).</summary>
        /// <summary>Host xử lý khi có client kết nối; trả false nếu phòng đầy (client sẽ bị ngắt).</summary>
        public bool OnClientJoined(ulong clientId)
        {
            if (!IsServer) return false;
            return HandleServerClientConnected(clientId);
        }

        /// <summary>NetBootstrap gọi khi một client rời phòng.</summary>
        public void OnClientLeft(ulong clientId)
        {
            if (!IsServer) return;
            RemovePlayer(clientId);
            Broadcast();
        }

        /// <summary>Client xử lý khi host ngắt kết nối hoặc đóng phòng.</summary>
        public void OnHostDisconnected()
        {
            if (Phase == MatchPhase.Playing)
            {
                Phase = MatchPhase.Failed;
            }

            UnhookDirector();
            StopSceneLoadCoroutines();
            ResetLocal();
            Changed?.Invoke();

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene != "SampleScene" && currentScene != "CoopLobby" && currentScene != "StageSelect" && currentScene != "MainMenu")
            {
                Time.timeScale = 1f;
                if (Application.CanStreamedLevelBeLoaded("StageSelect"))
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("StageSelect");
                }
            }
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

        /// <summary>Client xin đổi toàn bộ loadout (xe, cấp, phụ kiện, biến thể kỹ năng) trong lobby.</summary>
        public void RequestLoadout(NetLoadout loadout)
        {
            if (!Listening) return;
            if (IsServer)
            {
                ApplyLoadoutRequest(NetBootstrap.ServerClientId, loadout);
                return;
            }

            CustomMessagingManager messaging = Manager != null ? Manager.CustomMessagingManager : null;
            if (messaging == null) return;

            using FastBufferWriter writer = new FastBufferWriter(18, Allocator.Temp);
            writer.WriteValueSafe(OpSetLoadout);
            loadout.WriteTo(writer);
            messaging.SendNamedMessage(RequestMessage, NetBootstrap.ServerClientId, writer);
        }

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

        private void ApplyLoadoutRequest(ulong clientId, NetLoadout loadout)
        {
            if (!IsServer || Phase != MatchPhase.Lobby) return;

            int count = bootstrap != null && bootstrap.Spawner != null ? bootstrap.Spawner.TankCount : 0;
            if (count > 0 && loadout.TankIndex >= count) return;

            if (bootstrap != null)
            {
                bootstrap.SetClientLoadout(clientId, loadout);
            }

            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_ClientIds[i] != clientId) continue;
                m_TankIndexes[i] = loadout.TankIndex;
                m_Option1s[i] = (byte)Mathf.Clamp(loadout.OptionSkill1, 0, 2);
                m_Option2s[i] = (byte)Mathf.Clamp(loadout.OptionSkill2, 0, 2);
                m_Ready[i] = false;
                break;
            }

            Broadcast();
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

            if (bootstrap != null)
            {
                NetLoadout loadout = bootstrap.GetLoadout(clientId);
                loadout.TankIndex = tankIndex;
                bootstrap.SetClientLoadout(clientId, loadout);
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

            if (bootstrap != null)
            {
                NetLoadout loadout = bootstrap.GetLoadout(clientId);
                if (firstOption) loadout.OptionSkill1 = clamped;
                else loadout.OptionSkill2 = clamped;
                bootstrap.SetClientLoadout(clientId, loadout);
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

                MatchPhase previousPhase = Phase;
                Phase = (MatchPhase)phase;
                Mode = (MatchMode)mode;
                StageIndex = stageIndex;
                m_PlayerCount = Mathf.Clamp((int)count, 0, MaxPlayers);

                if (previousPhase != Phase)
                {
                    Debug.Log($"[MatchFlow] Client nhận phase mạng: {previousPhase} -> {Phase}");
                }

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

                if (op == OpSetLoadout)
                {
                    if (NetLoadout.TryFromReader(reader, out NetLoadout loadout))
                    {
                        ApplyLoadoutRequest(senderClientId, loadout);
                    }
                    return;
                }

                if (op == OpSceneLoaded)
                {
                    reader.ReadValueSafe(out FixedString64Bytes loadedScene);
                    if (m_WaitingForSceneLoad && loadedScene.ToString() == m_PendingTargetScene)
                    {
                        m_SceneReadyClients.Add(senderClientId);
                    }
                    else if (Phase == MatchPhase.Playing &&
                        loadedScene.ToString() == UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
                    {
                        NetStageEvents.Instance?.SendWaveGateSnapshot(senderClientId, loadedScene.ToString());
                    }
                    return;
                }

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
                        // Chỉ host được quyết định chơi lại. Trước đây client bấm "xin" là cả phòng bị kéo
                        // về lobby, kể cả khi host đang muốn xem kết quả.
                        Debug.Log($"[MatchFlow] Bỏ qua yêu cầu chơi lại từ client {senderClientId} — chỉ host được đổi trạng thái phòng.");
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MatchFlow] Yêu cầu từ client {senderClientId} không hợp lệ: {exception.Message}");
            }
        }

        private void SendSceneSyncToAll(string sceneName)
        {
            if (!IsServer || string.IsNullOrEmpty(sceneName)) return;
            CustomMessagingManager messaging = Manager != null ? Manager.CustomMessagingManager : null;
            if (messaging == null) return;

            using FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteValueSafe((FixedString64Bytes)sceneName);
            messaging.SendNamedMessageToAll(SceneSyncMessage, writer);
        }

        private void SendSceneSyncToClient(ulong clientId, string sceneName)
        {
            if (!IsServer || string.IsNullOrEmpty(sceneName) || clientId == NetBootstrap.ServerClientId) return;
            CustomMessagingManager messaging = Manager != null ? Manager.CustomMessagingManager : null;
            if (messaging == null) return;

            using FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteValueSafe((FixedString64Bytes)sceneName);
            messaging.SendNamedMessage(SceneSyncMessage, clientId, writer);
        }

        private void HandleSceneSync(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetBootstrap.ServerClientId || IsServer) return;

            string sceneName;
            try
            {
                reader.ReadValueSafe(out FixedString64Bytes rawScene);
                sceneName = rawScene.ToString();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[MatchFlow] Thông điệp chuyển scene không hợp lệ: " + exception.Message);
                return;
            }

            if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName)) return;

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == sceneName)
            {
                SendSceneLoadedAck(sceneName);
                return;
            }

            if (m_ClientSceneLoadCoroutine != null) StopCoroutine(m_ClientSceneLoadCoroutine);
            m_ClientSceneLoadCoroutine = StartCoroutine(ClientLoadSceneRoutine(sceneName));
        }

        private System.Collections.IEnumerator ClientLoadSceneRoutine(string sceneName)
        {
            AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            while (op != null && !op.isDone)
            {
                yield return null;
            }

            m_ClientSceneLoadCoroutine = null;
            SendSceneLoadedAck(sceneName);
        }

        private void SendSceneLoadedAck(string sceneName)
        {
            CustomMessagingManager messaging = Manager != null ? Manager.CustomMessagingManager : null;
            if (messaging == null || IsServer) return;

            using FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteValueSafe(OpSceneLoaded);
            writer.WriteValueSafe((FixedString64Bytes)sceneName);
            messaging.SendNamedMessage(RequestMessage, NetBootstrap.ServerClientId, writer);
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
