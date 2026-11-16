using System.Collections.Generic;
using IronWasteland.Tanks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
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

        [Header("Prefab mạng")]
        [Tooltip("Các TankDefinition có prefab sẽ được đăng ký làm NetworkPrefab (chỉ tank, không đăng ký skill/VFX).")]
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
            EnsureTransport();
            RegisterNetworkPrefabs();

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

            EnsureTransport();
            NetLoadout loadout = BuildLocalLoadout();
            m_Loadouts[ServerClientId] = loadout;

            bool started = networkManager.StartHost();
            if (!started) m_Loadouts.Remove(ServerClientId);
            return started;
        }

        /// <summary>Vào phòng của host theo địa chỉ IP/cổng đã cấu hình.</summary>
        public bool StartClient()
        {
            if (networkManager == null) return false;

            EnsureTransport();
            UnityTransport transport = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport != null) transport.SetConnectionData(joinAddress, joinPort);

            networkManager.NetworkConfig.ConnectionData = BuildLocalLoadout().ToPayload();
            return networkManager.StartClient();
        }

        /// <summary>Đóng phòng ở cả hai phía.</summary>
        public void Shutdown()
        {
            if (networkManager != null && networkManager.IsListening) networkManager.Shutdown();
        }

        private void EnsureTransport()
        {
            if (networkManager == null) return;
            if (networkManager.NetworkConfig.NetworkTransport != null) return;

            UnityTransport transport = GetComponent<UnityTransport>();
            if (transport == null) transport = gameObject.AddComponent<UnityTransport>();
            networkManager.NetworkConfig.NetworkTransport = transport;
        }

        private void RegisterNetworkPrefabs()
        {
            if (networkTankDefinitions == null) return;

            for (int i = 0; i < networkTankDefinitions.Length; i++)
            {
                TankDefinition definition = networkTankDefinitions[i];
                if (definition == null || definition.TankPrefab == null) continue;

                GameObject prefab = definition.TankPrefab.gameObject;
                if (prefab.GetComponent<NetworkObject>() == null)
                {
                    Debug.LogWarning($"[NetBootstrap] Prefab '{prefab.name}' chưa có NetworkObject nên bị bỏ qua.", this);
                    continue;
                }

                networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            }
        }

        private void HandleConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            bool valid = NetLoadout.TryFromPayload(request.Payload, out NetLoadout loadout);
            response.Approved = valid;
            response.CreatePlayerObject = false;
            response.Pending = false;

            if (!valid)
            {
                response.Reason = "Payload NetLoadout thiếu hoặc sai định dạng.";
                return;
            }

            m_Loadouts[request.ClientNetworkId] = loadout;
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
