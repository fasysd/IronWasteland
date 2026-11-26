using System.Collections.Generic;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Host dựng xe cho từng client: chọn <see cref="TankDefinition"/> theo <see cref="NetLoadout"/>,
    /// spawn qua NGO rồi mới gọi <c>Initialize</c> — nhờ vậy không còn tank nào được tạo ngoài mạng
    /// và mọi máy nhìn thấy cùng một đối tượng mạng.
    /// </summary>
    public class NetSpawnService : MonoBehaviour
    {
        [Header("Roster")]
        [Tooltip("Danh sách xe có thể chọn; thứ tự trùng với TankIndex trong NetLoadout.")]
        [SerializeField] private TankDefinition[] tankDefinitions;

        [Header("Vị trí spawn")]
        [Tooltip("Khoảng cách giữa các xe khi nhiều người chơi cùng vào phòng.")]
        [SerializeField] private float spawnSpacing = 2f;

        [Tooltip("Gốc toạ độ để xếp xe của 4 người chơi.")]
        [SerializeField] private Vector3 spawnOrigin = Vector3.zero;

        public static NetSpawnService Instance { get; private set; }

        private readonly Dictionary<ulong, TankBrain> m_Spawned = new Dictionary<ulong, TankBrain>();

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Danh sách xe đã spawn trên host, dùng để gửi snapshot hiệu ứng cho người mới.</summary>
        public IReadOnlyDictionary<ulong, TankBrain> SpawnedTanks => m_Spawned;

        public int TankCount => tankDefinitions != null ? tankDefinitions.Length : 0;

        /// <summary>Lấy định nghĩa xe theo chỉ số roster; fallback sang TankRegistry nếu mảng trên scene trống.</summary>
        public TankDefinition GetDefinition(byte tankIndex)
        {
            if (tankDefinitions != null && tankDefinitions.Length > 0)
            {
                TankDefinition def = tankDefinitions[Mathf.Clamp(tankIndex, 0, tankDefinitions.Length - 1)];
                if (def != null && def.TankPrefab != null) return def;
            }

            return TankRegistry.GetDefinition(tankIndex);
        }

        /// <summary>Xe đã spawn cho client chưa (dùng để tránh spawn trùng).</summary>
        public bool TryGetTank(ulong clientId, out TankBrain brain)
        {
            if (m_Spawned.TryGetValue(clientId, out brain) && brain != null) return true;
            brain = null;
            return false;
        }

        /// <summary>
        /// Tra xe theo client sở hữu trên mọi máy. Client không giữ danh sách spawn của host
        /// (chỉ host gọi <see cref="SpawnPlayerTank"/>), nên khi cần phát lại visual phải quét các
        /// NetworkObject đang tồn tại thay vì chỉ đọc dictionary cục bộ.
        /// </summary>
        public bool TryResolveTank(ulong ownerClientId, out TankBrain brain)
        {
            if (TryGetTank(ownerClientId, out brain) && brain != null) return true;

            TankBrain[] tanks = UnityEngine.Object.FindObjectsByType<TankBrain>(FindObjectsInactive.Exclude);
            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain candidate = tanks[i];
                if (candidate == null) continue;

                NetworkObject networkObject = candidate.GetComponent<NetworkObject>();
                if (networkObject == null || !networkObject.IsSpawned) continue;
                if (networkObject.OwnerClientId != ownerClientId) continue;

                brain = candidate;
                return true;
            }

            brain = null;
            return false;
        }

        /// <summary>Server dọn toàn bộ xe đang spawn (khi về lobby hoặc chơi lại).</summary>
        public void DespawnAll()
        {
            List<ulong> ids = new List<ulong>(m_Spawned.Keys);
            for (int i = 0; i < ids.Count; i++) DespawnPlayerTank(ids[i]);
        }

        /// <summary>Server huỷ xe của người chơi đã rời phòng để không còn xe "ma" đứng giữa sân.</summary>
        public void DespawnPlayerTank(ulong clientId)
        {
            if (!m_Spawned.TryGetValue(clientId, out TankBrain brain)) return;
            m_Spawned.Remove(clientId);
            if (brain == null) return;

            NetworkObject networkObject = brain.GetComponent<NetworkObject>();
            NetworkManager manager = NetworkManager.Singleton;
            if (networkObject != null && networkObject.IsSpawned && manager != null && manager.IsServer)
            {
                networkObject.Despawn(true);
            }
            else
            {
                Destroy(brain.gameObject);
            }
        }

        /// <summary>Server dựng xe cho một client. Gọi trên host sau khi client được duyệt.</summary>
        public TankBrain SpawnPlayerTank(ulong clientId, NetLoadout loadout)
        {
            if (TryGetTank(clientId, out TankBrain existing)) return existing;

            TankDefinition definition = GetDefinition(loadout.TankIndex);
            if (definition == null || definition.TankPrefab == null)
            {
                Debug.LogError("[NetSpawnService] Thiếu TankDefinition hoặc TankPrefab cho xe được chọn.", this);
                return null;
            }

            GameController gc = FindAnyObjectByType<GameController>();
            Vector3 origin = gc != null ? (Vector3)gc.SpawnPosition : spawnOrigin;
            Vector3 position = origin + new Vector3(spawnSpacing * clientId, 0f, 0f);
            TankBrain brain = Instantiate(definition.TankPrefab, position, Quaternion.identity);
            NetworkObject networkObject = brain.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"[NetSpawnService] Prefab '{definition.TankPrefab.name}' chưa có NetworkObject.", this);
                Destroy(brain.gameObject);
                return null;
            }

            // Ghi loadout TRƯỚC khi spawn để payload spawn đã mang đúng xe/cấp/trang bị;
            // nếu ghi sau, client sẽ khởi tạo bằng giá trị mặc định và không áp lại được.
            PlayerNetState netState = brain.GetComponent<PlayerNetState>();
            if (netState != null)
            {
                netState.SetServerLoadout(loadout);
            }

            // Khởi tạo xe TRƯỚC khi SpawnAsPlayerObject: NetworkVariable máu/năng lượng/thống kê
            // được chụp ngay tại thời điểm spawn, nên nếu init sau thì client nhận giá trị prefab
            // (level 1, không trang bị) và thanh máu/kỹ năng sẽ lệch suốt trận.
            TankBrainContext context = new TankBrainContext(
                Mathf.Clamp(loadout.Level, TankDefinition.MinLevel, TankDefinition.MaxLevel),
                loadout.EquipmentId,
                loadout.OptionSkill1,
                loadout.OptionSkill2);
            definition.InitializeTank(brain, context);

            networkObject.SpawnAsPlayerObject(clientId);

            if (!networkObject.IsSpawned)
            {
                // Prefab chưa đăng ký NetworkPrefabs thì spawn thất bại nhưng object đã Initialize
                // và sẽ lọt roster của StageManager (khiến ải không bao giờ thua). Dọn ngay.
                Debug.LogError($"[NetSpawnService] SpawnAsPlayerObject thất bại cho client {clientId} — huỷ xe.", this);
                Destroy(brain.gameObject);
                return null;
            }

            m_Spawned[clientId] = brain;
            return brain;
        }

        /// <summary>
        /// Server spawn một quái qua NGO rồi mới <c>Initialize</c> — nhờ vậy giá trị máu đầu tiên
        /// mà client nhận được đã đúng với hệ số của đợt spawn (tốc độ, máu cường hoá...).
        /// </summary>
        public EnemyBrain SpawnEnemy(EnemyBrain prefab, Vector3 position, EnemyStats stats)
        {
            if (prefab == null) return null;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return null;

            EnemyBrain enemy = Instantiate(prefab, position, Quaternion.identity);
            enemy.gameObject.SetActive(true);

            NetworkObject networkObject = enemy.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"[NetSpawnService] Prefab quái '{prefab.name}' chưa có NetworkObject.", this);
                Destroy(enemy.gameObject);
                return null;
            }

            enemy.Initialize(stats);
            networkObject.Spawn(true);
            return enemy;
        }
    }
}
