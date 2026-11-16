using System.Collections.Generic;
using IronWasteland.Enemies;
using IronWasteland.Stages;
using IronWasteland.Tanks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
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

        [Header("Tham chiếu")]
        [Tooltip("NetworkManager dùng cho phòng chơi; để trống sẽ lấy trên cùng GameObject.")]
        [SerializeField] private NetworkManager networkManager;

        [Tooltip("Service dựng xe khi người chơi vào phòng.")]
        [SerializeField] private NetSpawnService spawnService;

        [Tooltip("Quản lý màn chơi; host tự bắt đầu ải khi mở phòng (để trống nếu chưa dùng màn chơi).")]
        [SerializeField] private StageManager stageManager;

        [Header("Prefab mạng")]
        [Tooltip("Các TankDefinition có prefab sẽ được dùng làm roster khi spawn người chơi.")]
        [SerializeField] private TankDefinition[] networkTankDefinitions;

        [Header("Lựa chọn của người chơi máy này")]
        [SerializeField] private byte tankIndex;
        [SerializeField, Min(TankDefinition.MinLevel)] private int level = TankDefinition.MinLevel;
        [SerializeField] private int equipmentId = -1;
        [SerializeField] private int optionSkill1;
        [SerializeField] private int optionSkill2;

        [Header("Kết nối trực tiếp (chưa có Relay)")]
        [Tooltip("Địa chỉ IP của host khi bấm Join.")]
        [SerializeField] private string joinAddress = "127.0.0.1";

        [Tooltip("Cổng UDP của host khi bấm Join.")]
        [SerializeField] private ushort joinPort = 7777;

        private readonly Dictionary<ulong, NetLoadout> m_Loadouts = new Dictionary<ulong, NetLoadout>();

        /// <summary>Phiên mạng đang chạy hay chưa (dùng để tắt đường offline).</summary>
        public bool IsRunning => networkManager != null && networkManager.IsListening;

        private void Awake()
        {
            if (networkManager == null) networkManager = GetComponent<NetworkManager>();
            if (spawnService == null) spawnService = GetComponent<NetSpawnService>();

            if (networkManager == null)
            {
                Debug.LogError("[NetBootstrap] Thiếu NetworkManager — chế độ online sẽ không chạy.", this);
                return;
            }

            networkManager.NetworkConfig.TickRate = TickRate;
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

        private void OnDestroy()
        {
            if (networkManager == null) return;

            networkManager.ConnectionApprovalCallback -= HandleConnectionApproval;
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        /// <summary>Mở phòng (host vừa là server vừa là người chơi).</summary>
        public bool StartHost()
        {
            if (networkManager == null) return false;

            ClearSceneEnemies();
            DestroyLocalOfflineTank();
            EnsureTransport();
            NetLoadout loadout = BuildLocalLoadout();
            m_Loadouts[ServerClientId] = loadout;

            bool started = networkManager.StartHost();
            if (!started) m_Loadouts.Remove(ServerClientId);

            // Host là máy duy nhất mô phỏng ải, nên chỉ host bắt đầu màn chơi.
            if (started) stageManager?.StartStage();

            return started;
        }

        /// <summary>Vào phòng của host theo địa chỉ IP/cổng đã cấu hình.</summary>
        public bool StartClient()
        {
            if (networkManager == null) return false;

            ClearSceneEnemies();
            DestroyLocalOfflineTank();
            EnsureTransport();
            UnityTransport transport = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport != null) transport.SetConnectionData(joinAddress, joinPort);

            networkManager.NetworkConfig.ConnectionData = BuildLocalLoadout().ToPayload();
            return networkManager.StartClient();
        }

        /// <summary>Đổi địa chỉ/cổng sẽ kết nối (dùng cho test nhiều client với tham số dòng lệnh).</summary>
        public void SetJoinTarget(string address, ushort port)
        {
            if (!string.IsNullOrEmpty(address)) joinAddress = address;
            if (port > 0) joinPort = port;
        }

        /// <summary>Mở phòng qua Relay của Unity Services: tạo allocation, gắn transport rồi StartHost.</summary>
        public async void StartHostWithRelay(string region = null)
        {
            if (networkManager == null || networkManager.IsListening) return;

            RelayServerData? relayData = await NetRelayService.CreateHostAllocationAsync(region);
            if (relayData == null)
            {
                Debug.LogError("[NetBootstrap] Không tạo được Relay: " + NetRelayService.LastError, this);
                return;
            }

            EnsureTransport();
            UnityTransport transport = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport == null)
            {
                Debug.LogError("[NetBootstrap] Thiếu UnityTransport để gắn Relay.", this);
                return;
            }

            transport.SetRelayServerData(relayData.Value);
            if (!StartHost())
            {
                Debug.LogError("[NetBootstrap] StartHost qua Relay thất bại.", this);
                return;
            }

            bool lobbyCreated = await NetRelayService.CreateLobbyAsync("IronWasteland");
            Debug.Log($"[NetBootstrap] Relay code={NetRelayService.RelayJoinCode} lobby={lobbyCreated} lobbyCode={NetRelayService.LobbyCode} err={NetRelayService.LastError}");
        }

        /// <summary>Vào phòng Relay bằng mã join code (không dùng địa chỉ IP).</summary>
        public async void StartClientWithRelay(string relayJoinCode)
        {
            if (networkManager == null || networkManager.IsListening) return;

            RelayServerData? relayData = await NetRelayService.JoinAllocationAsync(relayJoinCode);
            if (relayData == null)
            {
                Debug.LogError("[NetBootstrap] Không join được Relay: " + NetRelayService.LastError, this);
                return;
            }

            ClearSceneEnemies();
            DestroyLocalOfflineTank();
            EnsureTransport();
            UnityTransport transport = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport == null)
            {
                Debug.LogError("[NetBootstrap] Thiếu UnityTransport để gắn Relay.", this);
                return;
            }

            transport.SetRelayServerData(relayData.Value);
            networkManager.NetworkConfig.ConnectionData = BuildLocalLoadout().ToPayload();
            networkManager.StartClient();
        }

        /// <summary>Đóng phòng ở cả hai phía.</summary>
        public void Shutdown()
        {
            if (networkManager != null && networkManager.IsListening) networkManager.Shutdown();
            NetRelayService.ResetSession();
        }

        private void EnsureTransport()
        {
            if (networkManager == null) return;
            if (networkManager.NetworkConfig.NetworkTransport != null) return;

            UnityTransport transport = GetComponent<UnityTransport>();
            if (transport == null) transport = gameObject.AddComponent<UnityTransport>();
            networkManager.NetworkConfig.NetworkTransport = transport;
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
            Debug.Log($"[NetBootstrap] Duyệt client {request.ClientNetworkId}: tank={loadout.TankIndex} level={loadout.Level} equipment={loadout.EquipmentId} opt1={loadout.OptionSkill1} opt2={loadout.OptionSkill2}.");
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (networkManager == null || !networkManager.IsServer) return;
            if (spawnService == null)
            {
                Debug.LogError("[NetBootstrap] Thiếu NetSpawnService — không dựng được xe.", this);
                return;
            }

            NetLoadout loadout = m_Loadouts.TryGetValue(clientId, out NetLoadout saved) ? saved : default;
            spawnService.SpawnPlayerTank(clientId, loadout);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            m_Loadouts.Remove(clientId);
        }

        private NetLoadout BuildLocalLoadout()
        {
            return new NetLoadout
            {
                TankIndex = tankIndex,
                Level = level,
                EquipmentId = equipmentId,
                OptionSkill1 = optionSkill1,
                OptionSkill2 = optionSkill2,
            };
        }
    }
}
