using Action = System.Action;
using System.Collections.Generic;
using IronWasteland.Enemies;
using IronWasteland.Save;
using IronWasteland.Stages;
using IronWasteland.Tanks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using IronWasteland.Net.Models;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Điểm vào chế độ online: dựng <see cref="NetworkManager"/> + UnityTransport, đăng ký prefab tank,
    /// duyệt kết nối theo <see cref="NetLoadout"/> và nhờ <see cref="NetSpawnService"/> dựng xe cho từng client.
    /// Offline không gọi Start thì game vẫn chạy như cũ.
    /// </summary>
    public class NetBootstrap : MonoBehaviour
    {
        /// <summary>Tick mạng 20 Hz — đủ mượt cho 4 người, nhẹ hơn mặc định 30 Hz.</summary>
        public const uint TickRate = 20;

        /// <summary>ID client của host/server trong NGO.</summary>
        public const ulong ServerClientId = 0UL;

        /// <summary>Sự kiện kích hoạt khi client mất kết nối hoặc host đã đóng phòng.</summary>
        public static event Action HostDisconnected;

        [Header("Tham chiếu")]
        [Tooltip("NetworkManager dùng cho phòng chơi; để trống sẽ lấy trên cùng GameObject.")]
        [SerializeField] private NetworkManager networkManager;

        [Tooltip("Service dựng xe khi người chơi vào phòng.")]
        [SerializeField] private NetSpawnService spawnService;

        [Tooltip("Quản lý màn chơi; host tự bắt đầu ải khi mở phòng (để trống nếu chưa dùng màn chơi).")]
        [SerializeField] private StageManager stageManager;

        [Tooltip("Điều phối vòng đời phiên chơi (lobby → vào trận → thắng/thua).")]
        [SerializeField] private MatchFlow matchFlow;

        [Tooltip("Dịch vụ quảng bá/tìm phòng LAN; để trống sẽ tự gắn vào cùng GameObject.")]
        [SerializeField] private LanDiscoveryService discovery;

        [Header("Prefab mạng")]
        [Tooltip("Các TankDefinition có prefab sẽ được dùng làm roster khi spawn người chơi.")]
        [SerializeField] private TankDefinition[] networkTankDefinitions;

        [Header("Lựa chọn của người chơi máy này")]
        [SerializeField] private byte tankIndex;
        [SerializeField, Min(TankDefinition.MinLevel)] private int level = TankDefinition.MinLevel;
        [SerializeField] private int equipmentId = -1;
        [SerializeField] private int optionSkill1;
        [SerializeField] private int optionSkill2;

        [Header("Kết nối LAN trực tiếp")]
        [Tooltip("Địa chỉ IP (hoặc IP:cổng) của host khi bấm vào phòng.")]
        [SerializeField] private string joinAddress = "127.0.0.1";

        [Tooltip("Cổng UDP của host khi bấm Join.")]
        [SerializeField] private ushort joinPort = DefaultHostPort;

        [Tooltip("Cổng UDP host sẽ mở phòng (mặc định 7777; chỉ đổi bằng tham số dòng lệnh khi test).")]
        [SerializeField, HideInInspector] private ushort hostPort = DefaultHostPort;

        /// <summary>Cổng ưu tiên khi mở phòng LAN.</summary>
        public const ushort DefaultHostPort = 7777;

        /// <summary>Số cổng liên tiếp thử mở khi cổng ưu tiên đang bị ứng dụng khác chiếm.</summary>
        public const int HostPortFallbackCount = 10;

        private readonly Dictionary<ulong, NetLoadout> m_Loadouts = new Dictionary<ulong, NetLoadout>();

        /// <summary>
        /// Slot đã duyệt nhưng client chưa kết nối xong. NGO gọi callback duyệt TRƯỚC khi thêm
        /// client vào <c>ConnectedClientsIds</c>, nên nếu chỉ đếm danh sách đó thì hai request
        /// cùng frame đều lọt qua cổng 4 người.
        /// </summary>
        private readonly HashSet<ulong> m_ReservedClients = new HashSet<ulong>();

        /// <summary>Phiên mạng đang chạy hay chưa (dùng để tắt đường offline).</summary>
        public bool IsRunning => networkManager != null && networkManager.IsListening;

        /// <summary>NetworkManager của phòng (null nếu scene chưa cấu hình).</summary>
        public NetworkManager Manager => networkManager;

        /// <summary>Quản lý màn chơi dùng chung.</summary>
        public StageManager Stage => stageManager;

        /// <summary>Service dựng xe/quái qua mạng.</summary>
        public NetSpawnService Spawner => spawnService;

        /// <summary>Điều phối vòng đời phiên chơi.</summary>
        public MatchFlow Flow => matchFlow;

        /// <summary>Tank máy này đang chọn trước khi vào phòng.</summary>
        public byte LocalTankIndex => tankIndex;

        /// <summary>
        /// Client đã từng vào phòng nhưng mất kết nối (thường do host thoát).
        /// Dùng cho HUD/debug để báo "MẤT KẾT NỐI" thay vì để màn hình đứng im.
        /// </summary>
        public bool ConnectionLost { get; private set; }

        /// <summary>
        /// Host đã từng có người chơi nhưng hiện chỉ còn lại mình (mọi client đã rời/ngắt).
        /// Khi đó phiên không thể nhận thêm người mới nếu transport đã sập — cần tạo lại phòng.
        /// </summary>
        public bool SessionEnded { get; private set; }

        /// <summary>
        /// Lý do NGO ngắt kết nối (ví dụ "Phòng đã đủ 4 người."), để UI phân biệt bị đuổi với mất mạng.
        /// </summary>
        public string LastDisconnectReason { get; private set; }

        /// <summary>Cổng host đang mở (dùng để hiển thị địa chỉ cho đồng đội).</summary>
        public ushort HostPort => hostPort > 0 ? hostPort : joinPort;

        /// <summary>Địa chỉ host client đã dùng để vào phòng, giữ lại khi scene lobby được load lại.</summary>
        public string JoinAddress => joinAddress;

        /// <summary>Mã ải host đang chọn, ghi vào gói quảng bá LAN.</summary>
        public string LanStageCode => m_LanStageCode;

        private string m_LanStageCode = "1-1";
        private int m_LastLanPlayers = -1;
        private bool m_LastLanInMatch;
        private bool m_HadRemoteClient;

        public static NetBootstrap Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Bản sao nằm trong scene mới (PHÒNG LAN, CHỌN ẢI) phải tự huỷ.
                // TUYỆT ĐỐI không Destroy bản persistent cũ dù nó đang không chạy: huỷ NetworkManager
                // trước khi ShutdownInternal sẽ bỏ lại toàn bộ xe/đối tượng mạng mồ côi, và trận sau
                // hai NetworkManager cùng spawn xe cho một client — đúng lỗi hai xe chồng nhau.
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            if (discovery == null) discovery = GetComponent<LanDiscoveryService>();
            if (discovery == null) discovery = gameObject.AddComponent<LanDiscoveryService>();

            if (networkManager == null) networkManager = GetComponent<NetworkManager>();
            if (spawnService == null) spawnService = GetComponent<NetSpawnService>();

            if (networkManager == null)
            {
                Debug.LogError("[NetBootstrap] Thiếu NetworkManager — chế độ online sẽ không chạy.", this);
                return;
            }

            networkManager.NetworkConfig.TickRate = TickRate;
            networkManager.NetworkConfig.EnableSceneManagement = false;
            // Bắt buộc bật cờ này thì NGO mới gọi ConnectionApprovalCallback.
            // Thiếu nó, loadout client gửi qua ConnectionData bị bỏ qua và mọi người vào phòng
            // đều nhận xe mặc định (TankIndex 0, Level 1), đồng thời không chặn được payload sai.
            networkManager.NetworkConfig.ConnectionApproval = true;
            EnsureTransport();

            networkManager.ConnectionApprovalCallback -= HandleConnectionApproval;
            networkManager.ConnectionApprovalCallback += HandleConnectionApproval;
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        private void Update()
        {
            if (discovery == null || !discovery.IsHosting) return;

            int players = networkManager != null && networkManager.IsListening
                ? Mathf.Max(1, networkManager.ConnectedClientsIds.Count)
                : 1;
            bool inMatch = matchFlow != null && matchFlow.Phase == MatchPhase.Playing;
            if (players == m_LastLanPlayers && inMatch == m_LastLanInMatch) return;

            m_LastLanPlayers = players;
            m_LastLanInMatch = inMatch;
            discovery.UpdateHost(players, inMatch);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            discovery?.EndHost();

            // Dọn xe trước khi NetworkManager chết. Nếu để ShutdownInternal lo, nó chỉ dọn object
            // còn trỏ đúng NetworkManager hiện tại; manager bị huỷ trước sẽ để xe nằm lại DDOL.
            if (spawnService != null) spawnService.DespawnAll();

            if (networkManager == null) return;

            // NetworkManager.Shutdown() chỉ hẹn giờ ở Update kế tiếp. Thoát Play mode ngay sau đó
            // khiến Update không chạy và driver UDP vẫn giữ cổng, lần chơi sau không mở được phòng.
            // Đóng thẳng transport ở đây để nhả cổng ngay khi object bị huỷ.
            if (networkManager.IsListening)
            {
                // NetworkManager có thể ném lỗi cấu hình khi đang huỷ (ví dụ NetworkConfig chưa có transport).
                // OnDestroy không được để exception thoát ra, nếu không object phía sau sẽ không được dọn.
                try
                {
                    networkManager.Shutdown(true);
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning("[NetBootstrap] Lỗi khi đóng NetworkManager: " + exception.Message);
                }

                UnityTransport transport = networkManager.NetworkConfig != null
                    ? networkManager.NetworkConfig.NetworkTransport as UnityTransport
                    : null;
                if (transport != null)
                {
                    try
                    {
                        transport.Shutdown();
                    }
                    catch (System.Exception exception)
                    {
                        Debug.LogWarning("[NetBootstrap] Lỗi khi đóng transport: " + exception.Message);
                    }
                }
            }

            networkManager.ConnectionApprovalCallback -= HandleConnectionApproval;
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        /// <summary>Mở phòng (host vừa là server vừa là người chơi).</summary>
        /// <param name="advertise">Có quảng bá phòng lên LAN hay không; chơi đơn truyền false.</param>
        public bool StartHost(bool advertise = true)
        {
            if (networkManager == null) return false;

            ConnectionLost = false;
            LastDisconnectReason = null;
            m_ReservedClients.Clear();
            SessionEnded = false;
            m_HadRemoteClient = false;
            ClearSceneEnemies();
            DestroyLocalOfflineTank();
            EnsureTransport();

            // Dọn trạng thái trận của phiên trước TRƯỚC khi mở mạng: nếu để Phase còn Playing,
            // callback OnClientConnected của host mới sẽ spawn xe ngay trong lobby.
            matchFlow?.PrepareNewSession();

            // Mở phòng trên cổng đã cấu hình (mặc định 7777). Chỉ nghe trên mọi card mạng nội bộ.
            // Nếu cổng ưu tiên đang bị ứng dụng khác (hoặc phiên test cũ) chiếm, thử các cổng kế tiếp
            // để người chơi vẫn tạo được phòng; cổng thực tế được ghi lại vào hostPort để quảng bá.
            UnityTransport hostTransport = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
            ushort preferredPort = HostPort;
            ushort chosenPort = preferredPort;
            NetLoadout loadout = BuildLocalLoadout();
            m_Loadouts[ServerClientId] = loadout;

            int attempts = hostTransport != null ? HostPortFallbackCount : 1;
            bool started = false;
            for (int i = 0; i < attempts; i++)
            {
                int candidate = preferredPort + i;
                if (candidate > ushort.MaxValue) break;

                if (hostTransport != null) hostTransport.SetConnectionData("127.0.0.1", (ushort)candidate, "0.0.0.0");

                // StartHost trả false khi cổng bận, nhưng cũng có thể ném exception khi NetworkConfig
                // sai (Editor từng báo "Allowed types ..." rồi lỗi dây chuyền trong ShutdownInternal).
                // Bắt lại để UI nhận false và báo lỗi, thay vì exception thoát ra ngoài vòng fallback.
                try
                {
                    started = networkManager.StartHost();
                }
                catch (System.Exception exception)
                {
                    Debug.LogError("[NetBootstrap] StartHost lỗi ở cổng " + candidate + ": " + exception.Message, this);
                    started = false;
                    break;
                }

                if (started)
                {
                    chosenPort = (ushort)candidate;
                    break;
                }

                Debug.LogWarning("[NetBootstrap] Không mở được cổng " + candidate + ", thử cổng tiếp theo.");
            }

            if (!started)
            {
                m_Loadouts.Remove(ServerClientId);
                return false;
            }

            hostPort = chosenPort;

            // Vòng đời trận (lobby, chọn màn, vào trận) do MatchFlow quyết định; trước đây chỗ này
            // tự StartStage() nên vào phòng là quái lao vào ngay, không kịp chọn tank/màn.
            if (started)
            {
                matchFlow?.OnHostStarted();
                m_LastLanPlayers = -1;
                m_LastLanInMatch = false;
                if (advertise) discovery?.BeginHost(m_LanStageCode, HostPort, 1);
            }

            return started;
        }

        /// <summary>Vào phòng của host theo địa chỉ IP/cổng đã cấu hình.</summary>
        public bool StartClient()
        {
            if (networkManager == null) return false;

            ConnectionLost = false;
            LastDisconnectReason = null;
            m_ReservedClients.Clear();
            SessionEnded = false;
            m_HadRemoteClient = false;
            ClearSceneEnemies();
            DestroyLocalOfflineTank();
            EnsureTransport();
            matchFlow?.PrepareNewSession();
            if (!LanAddress.TryParse(joinAddress, joinPort, out string host, out ushort port))
            {
                Debug.LogWarning("[NetBootstrap] Địa chỉ host không hợp lệ: " + joinAddress, this);
                return false;
            }

            UnityTransport transport = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport != null) transport.SetConnectionData(host, port);

            networkManager.NetworkConfig.ConnectionData = BuildLocalLoadout().ToPayload();
            return networkManager.StartClient();
        }

        /// <summary>Đổi địa chỉ/cổng sẽ kết nối (dùng cho test nhiều client với tham số dòng lệnh).</summary>
        public void SetJoinTarget(string address, ushort port)
        {
            if (!string.IsNullOrEmpty(address)) joinAddress = address;
            if (port > 0) joinPort = port;
        }

        /// <summary>Đổi cổng host sẽ mở phòng (dùng cho test nhiều phòng trên một máy).</summary>
        public void SetHostPort(ushort port)
        {
            if (port > 0) hostPort = port;
        }

        /// <summary>Ghi mã ải đang chọn vào gói quảng bá trước khi mở phòng.</summary>
        public void SetLanStageCode(string stageCode)
        {
            if (!string.IsNullOrWhiteSpace(stageCode)) m_LanStageCode = stageCode.Trim();
        }

        /// <summary>Đổi tank máy này chọn trước khi vào phòng (lobby cho phép đổi xe).</summary>
        public void SetLocalTankIndex(byte index)
        {
            tankIndex = index;
            if (SaveSystem.Instance != null)
            {
                int savedLevel = SaveSystem.Instance.GetTankLevel(index);
                if (savedLevel > 0) level = savedLevel;
                equipmentId = SaveSystem.Instance.GetEquippedAccessory(index);
                optionSkill1 = SaveSystem.Instance.GetOptionSkill(index, 0);
                optionSkill2 = SaveSystem.Instance.GetOptionSkill(index, 1);
            }
        }

        /// <summary>Cập nhật toàn bộ loadout cho máy cục bộ trước khi vào phòng hoặc đổi xe.</summary>
        public void SetLocalLoadout(NetLoadout loadout)
        {
            tankIndex = loadout.TankIndex;
            level = Mathf.Clamp(loadout.Level, TankDefinition.MinLevel, TankDefinition.MaxLevel);
            equipmentId = loadout.EquipmentId;
            optionSkill1 = Mathf.Clamp(loadout.OptionSkill1, 0, 2);
            optionSkill2 = Mathf.Clamp(loadout.OptionSkill2, 0, 2);

            if (networkManager != null && networkManager.IsListening)
            {
                m_Loadouts[networkManager.LocalClientId] = BuildLocalLoadout();
            }
        }

        /// <summary>Gán hoặc cập nhật loadout cho một client cụ thể (do host quản lý).</summary>
        public void SetClientLoadout(ulong clientId, NetLoadout loadout)
        {
            m_Loadouts[clientId] = loadout;
        }

        /// <summary>Biến thể Optional Skill 1 mà máy này đang chọn (dùng cho UI test trong lobby).</summary>
        public int LocalOptionSkill1 => optionSkill1;

        /// <summary>Biến thể Optional Skill 2 mà máy này đang chọn (dùng cho UI test trong lobby).</summary>
        public int LocalOptionSkill2 => optionSkill2;

        /// <summary>
        /// Đặt biến thể Optional Skill cho máy này trước khi vào phòng (mỗi nhóm 0..2).
        /// Loadout gửi kèm lúc kết nối nên client cần chọn trước khi bấm vào phòng.
        /// </summary>
        public void SetLocalOptionSkills(int option1, int option2)
        {
            optionSkill1 = Mathf.Clamp(option1, 0, 2);
            optionSkill2 = Mathf.Clamp(option2, 0, 2);
        }

        /// <summary>Loadout đã lưu của một client (level, trang bị, biến thể kỹ năng).</summary>
        public NetLoadout GetLoadout(ulong clientId)
        {
            return m_Loadouts.TryGetValue(clientId, out NetLoadout loadout) ? loadout : default;
        }

        /// <summary>
        /// Dựng xe cho một người chơi theo tank đã chọn trong lobby; các trường còn lại
        /// (level, trang bị, biến thể kỹ năng) lấy từ loadout lúc kết nối.
        /// </summary>
        public void SpawnTankFor(ulong clientId, byte tankIndexIndex)
        {
            NetLoadout saved = m_Loadouts.TryGetValue(clientId, out NetLoadout loadout) ? loadout : default;
            SpawnTankFor(clientId, tankIndexIndex, saved.OptionSkill1, saved.OptionSkill2);
        }

        /// <summary>
        /// Dựng xe với tank và biến thể Optional Skill do lobby chọn (host quyết định).
        /// Dùng khi bắt đầu trận hoặc khi người chơi vào giữa trận.
        /// </summary>
        public void SpawnTankFor(ulong clientId, byte tankIndexIndex, int option1, int option2)
        {
            if (spawnService == null) return;

            NetLoadout loadout = m_Loadouts.TryGetValue(clientId, out NetLoadout saved) ? saved : default;
            loadout.TankIndex = tankIndexIndex;
            loadout.OptionSkill1 = Mathf.Clamp(option1, 0, 2);
            loadout.OptionSkill2 = Mathf.Clamp(option2, 0, 2);
            spawnService.SpawnPlayerTank(clientId, loadout);
        }

        /// <summary>Dọn toàn bộ xe do mạng spawn (dùng khi về lobby hoặc chơi lại).</summary>
        public void DespawnAllTanks()
        {
            spawnService?.DespawnAll();
        }

        /// <summary>Tank mà một người chơi đã chọn lúc kết nối (dùng để dựng danh sách lobby).</summary>
        public byte GetLoadoutTankIndex(ulong clientId)
        {
            return m_Loadouts.TryGetValue(clientId, out NetLoadout loadout) ? loadout.TankIndex : (byte)0;
        }

        /// <summary>Đóng phòng ở cả hai phía.</summary>
        public void Shutdown()
        {
            ConnectionLost = false;
            LastDisconnectReason = null;
            m_ReservedClients.Clear();
            SessionEnded = false;
            m_HadRemoteClient = false;

            // Rời phiên: dọn trạng thái trận ngay trên máy này trước khi tắt mạng, để menu không
            // còn Phase Playing/kills cũ và không spawn quái offline khi quay về.
            matchFlow?.PrepareNewSession();
            // Phòng khi MatchFlow chưa kịp gán hoặc đã bị huỷ: vẫn phải dọn xe trước khi đóng transport,
            // nếu không các NetworkObject cũ sẽ sống tiếp vào phiên sau.
            if (spawnService != null) spawnService.DespawnAll();

            // Ngừng phát phòng trước khi tắt mạng để đồng đội không thấy phòng đã chết.
            discovery?.EndHost();
            if (networkManager != null && networkManager.IsListening) networkManager.Shutdown();
        }

        /// <summary>Tạo lại phòng khi phiên cũ đã chết (host hết client hoặc client mất host).</summary>
        public void Rehost()
        {
            if (!isActiveAndEnabled)
            {
                Debug.LogWarning("[NetBootstrap] Không thể tạo lại phòng khi object đang tắt.");
                return;
            }

            StartCoroutine(RehostRoutine());
        }

        private System.Collections.IEnumerator RehostRoutine()
        {
            Debug.Log("[NetBootstrap] Tạo lại phòng LAN…");
            Shutdown();

            // Chờ NGO dọn transport + callback xong rồi mới dựng phiên mới.
            yield return null;
            yield return new WaitForSecondsRealtime(0.6f);
            StartHost();
        }

        private void EnsureTransport()
        {
            if (networkManager == null) return;

            if (networkManager.NetworkConfig.NetworkTransport == null)
            {
                UnityTransport created = GetComponent<UnityTransport>();
                if (created == null) created = gameObject.AddComponent<UnityTransport>();
                networkManager.NetworkConfig.NetworkTransport = created;
            }

            // Mặc định của Unity Transport là 30 giây mới phát hiện host biến mất — quá lâu với co-op.
            // Hạ xuống 10 giây để client báo "MẤT KẾT NỐI" sớm, vẫn đủ rộng cho mạng yếu.
            if (networkManager.NetworkConfig.NetworkTransport is UnityTransport transport)
            {
                transport.DisconnectTimeoutMS = 10000;
            }
        }

        /// <summary>
        /// Huỷ quái đặt sẵn trong scene trước khi vào phòng: chúng không phải NetworkObject nên mỗi máy
        /// sẽ mô phỏng một bản riêng, gây lệch trạng thái. Quái online do host spawn qua NGO.
        /// </summary>
        private static void ClearSceneEnemies()
        {
            EnemyBrain[] enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyBrain enemy = enemies[i];
                if (enemy == null) continue;

                // Quái đặt sẵn trong scene có thể đã thừa hưởng NetworkObject từ prefab,
                // nhưng chúng không do NGO spawn — vẫn phải dọn để không mô phỏng trùng.
                NetworkObject networkObject = enemy.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned) continue;

                Destroy(enemy.gameObject);
            }
        }

        /// <summary>
        /// Huỷ xe offline do GameController tạo trong Awake trước khi vào phòng: nếu giữ lại, mỗi máy
        /// sẽ có thêm một xe không thuộc mạng và người chơi điều khiển nhầm hai xe.
        /// </summary>
        private static void DestroyLocalOfflineTank()
        {
            GameController controller = Object.FindAnyObjectByType<GameController>();
            if (controller != null) controller.DestroyLocalTank();
        }

        private void HandleConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            // Host không gửi payload loadout (NGO tự duyệt chính nó khi StartHost), nên phải
            // chấp nhận riêng trước khi kiểm tra payload — nếu không sẽ log "từ chối" vô nghĩa.
            if (request.ClientNetworkId == ServerClientId)
            {
                response.Approved = true;
                response.CreatePlayerObject = false;
                response.Pending = false;
                return;
            }

            // Chặn phòng quá 4 người ngay tại cổng duyệt: chỉ dựa vào MatchFlow thì client thứ 5
            // vẫn kết nối được, chiếm slot rồi bị treo ở lobby.
            // Cộng thêm slot đã duyệt nhưng chưa connect xong — NGO gọi approval trước AddClient.
            int occupiedSlots = (networkManager != null ? networkManager.ConnectedClientsIds.Count : 0)
                + m_ReservedClients.Count;
            if (occupiedSlots >= MatchFlow.MaxPlayers)
            {
                response.Approved = false;
                response.CreatePlayerObject = false;
                response.Pending = false;
                response.Reason = "Phòng đã đủ 4 người.";
                Debug.LogWarning($"[NetBootstrap] Từ chối client {request.ClientNetworkId}: phòng đã đủ {MatchFlow.MaxPlayers} người.");
                return;
            }

            bool valid = NetLoadout.TryFromPayload(request.Payload, out NetLoadout loadout);
            response.Approved = valid;
            response.CreatePlayerObject = false;
            response.Pending = false;

            if (!valid)
            {
                response.Reason = "Payload NetLoadout thiếu hoặc sai định dạng.";
                Debug.LogWarning($"[NetBootstrap] Từ chối client {request.ClientNetworkId}: payload {request.Payload?.Length ?? 0} byte không hợp lệ.");
                return;
            }

            m_Loadouts[request.ClientNetworkId] = loadout;
            m_ReservedClients.Add(request.ClientNetworkId);
            Debug.Log($"[NetBootstrap] Duyệt client {request.ClientNetworkId}: tank={loadout.TankIndex} level={loadout.Level} equipment={loadout.EquipmentId} opt1={loadout.OptionSkill1} opt2={loadout.OptionSkill2}.");
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (networkManager == null || !networkManager.IsServer) return;

            // Client đã connect xong: slot chuyển từ "đã duyệt" sang ConnectedClientsIds.
            m_ReservedClients.Remove(clientId);

            if (spawnService == null)
            {
                Debug.LogError("[NetBootstrap] Thiếu NetSpawnService — không dựng được xe.", this);
                return;
            }

            NetLoadout loadout = m_Loadouts.TryGetValue(clientId, out NetLoadout saved) ? saved : default;

            if (matchFlow != null)
            {
                // MatchFlow giữ danh sách phòng: thêm người, quyết định có spawn xe ngay hay đợi vào trận.
                if (!matchFlow.OnClientJoined(clientId))
                {
                    // Phòng đầy (có thể do race với cổng duyệt): không spawn, ngắt luôn để không có xe ma.
                    m_Loadouts.Remove(clientId);
                    networkManager.DisconnectClient(clientId, "Phòng đã đủ 4 người.");
                    return;
                }
            }
            else
            {
                spawnService.SpawnPlayerTank(clientId, loadout);
            }

            m_HadRemoteClient = true;

            // Người mới vào cần thấy aura/khiên của MỌI xe đang có, không chỉ xe mình.
            SendEffectSnapshotTo(clientId);

            // Người vào giữa trận cần biết ải đang ở trạng thái nào. Scene ải hiện dùng
            // StageDirector (không có StageManager), nên phải gửi từ director trước.
            StageDirector director = FindAnyObjectByType<StageDirector>();
            if (director != null && director.State != StageRunState.Idle)
            {
                byte state = director.State == StageRunState.Cleared
                    ? NetStageEvents.StateCleared
                    : director.State == StageRunState.Failed
                        ? NetStageEvents.StateFailed
                        : NetStageEvents.StateStarted;
                string reason = director.State == StageRunState.Failed ? director.FailReason : null;
                NetStageEvents.Instance?.Broadcast(director, state, reason);
            }
            else if (stageManager != null && stageManager.State != StageState.Idle)
            {
                byte state = stageManager.State == StageState.Cleared
                    ? NetStageEvents.StateCleared
                    : stageManager.State == StageState.Failed
                        ? NetStageEvents.StateFailed
                        : NetStageEvents.StateStarted;
                NetStageEvents.Instance?.Broadcast(stageManager, state);
            }
        }

        /// <summary>Gửi trạng thái hiệu ứng/khiên hiện có của mọi xe tới một client vừa vào phòng.</summary>
        private void SendEffectSnapshotTo(ulong targetClientId)
        {
            NetEffectSync sync = NetEffectSync.Instance;
            if (sync == null || spawnService == null) return;

            foreach (KeyValuePair<ulong, TankBrain> pair in spawnService.SpawnedTanks)
            {
                if (pair.Value == null) continue;
                sync.SendSnapshotToClient(targetClientId, pair.Key, pair.Value.ActiveEffects);
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            // Xe đã rời thì xoá cache hiệu ứng của xe đó trên mọi máy, tránh aura/buff "ma".
            NetEffectSync.Instance?.ClearOwner(clientId);

            // Trên client: host đóng phòng (hoặc mạng đứt) thì NGO báo disconnect cho chính mình.
            // Không dọn loadout ở đây vì client không sở hữu danh sách người chơi.
            if (networkManager != null && !networkManager.IsServer && clientId == networkManager.LocalClientId)
            {
                // Lưu lý do (ví dụ "Phòng đã đủ 4 người.") để UI phân biệt bị đuổi với mất mạng.
                LastDisconnectReason = networkManager.DisconnectReason;
                ConnectionLost = true;
                Debug.LogWarning($"[NetBootstrap] Mất kết nối tới host (clientId={clientId}). Lý do: {LastDisconnectReason}.");

                // Nếu đang trong màn chơi thì kích hoạt trận đấu thất bại vì host đã rời phòng.
                StageDirector director = FindAnyObjectByType<StageDirector>();
                if (director != null && director.State == StageRunState.Running)
                {
                    director.ForceFail("Chủ phòng đã thoát phòng. Trận đấu thất bại.");
                }

                matchFlow?.OnHostDisconnected();
                HostDisconnected?.Invoke();

                if (networkManager != null && networkManager.IsListening)
                {
                    networkManager.Shutdown();
                }
                return;
            }

            m_Loadouts.Remove(clientId);
            m_ReservedClients.Remove(clientId);
            spawnService?.DespawnPlayerTank(clientId);
            matchFlow?.OnClientLeft(clientId);
        }

        public NetLoadout BuildLocalLoadout()
        {
            int currentTank = tankIndex;
            int currentLevel = level;
            int currentEquipment = equipmentId;
            int opt1 = optionSkill1;
            int opt2 = optionSkill2;

            if (SaveSystem.Instance != null)
            {
                int savedLevel = SaveSystem.Instance.GetTankLevel(currentTank);
                if (currentLevel <= TankDefinition.MinLevel && savedLevel > 0)
                {
                    currentLevel = savedLevel;
                }

                int savedEquip = SaveSystem.Instance.GetEquippedAccessory(currentTank);
                if (currentEquipment < 0 && savedEquip >= 0)
                {
                    currentEquipment = savedEquip;
                }

                if (opt1 == 0)
                {
                    opt1 = SaveSystem.Instance.GetOptionSkill(currentTank, 0);
                }

                if (opt2 == 0)
                {
                    opt2 = SaveSystem.Instance.GetOptionSkill(currentTank, 1);
                }
            }

            return new NetLoadout
            {
                TankIndex = (byte)currentTank,
                Level = Mathf.Clamp(currentLevel, TankDefinition.MinLevel, TankDefinition.MaxLevel),
                EquipmentId = currentEquipment,
                OptionSkill1 = Mathf.Clamp(opt1, 0, 2),
                OptionSkill2 = Mathf.Clamp(opt2, 0, 2),
            };
        }
    }
}