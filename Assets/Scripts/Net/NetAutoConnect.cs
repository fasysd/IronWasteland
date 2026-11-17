using System;
using System.Collections;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Tự động mở/ vào phòng khi chạy player từ dòng lệnh, phục vụ test nhiều client trên một máy.
    /// Tham số hỗ trợ:
    /// -iw-host            : chạy làm host.
    /// -iw-join &lt;địa chỉ&gt; : chạy làm client và kết nối tới địa chỉ đó (ví dụ 127.0.0.1).
    /// -iw-port &lt;cổng&gt;   : đổi cổng kết nối (mặc định 7777).
    /// Không truyền tham số nào thì component im lặng, lobby IMGUI vẫn dùng bình thường.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class NetAutoConnect : MonoBehaviour
    {
        [Tooltip("Cho phép đọc tham số dòng lệnh để tự vào phòng. Chỉ nên bật khi test nhiều client.")]
        [SerializeField] private bool enableCommandLine = true;

        [Tooltip("Số giây giữa hai lần in trạng thái mạng ra log (dùng để kiểm chứng khi test).")]
        [SerializeField, Min(0.5f)] private float logInterval = 2f;

        [Header("Tự kiểm tra khi test (chỉ host)")]
        [Tooltip("Số giây chờ sau khi mở phòng rồi tự đánh vào xe của host để kiểm chứng đồng bộ máu.")]
        [SerializeField, Min(1f)] private float selfTestDelay = 12f;

        [Tooltip("Lượng sát thương thô dùng cho lần tự kiểm tra.")]
        [SerializeField, Min(1f)] private float selfTestDamageAmount = 120f;

        [Tooltip("Chỉ số kỹ năng client sẽ bấm thử qua RPC khi có tham số -iw-selftest-cast (1 = chuyên biệt).")]
        [SerializeField, Range(0, 3)] private int selfTestSkillIndex = 1;

        [Tooltip("Prefab quái dùng cho lần tự kiểm tra spawn/đồng bộ máu (tham số -iw-selftest-enemy).")]
        [SerializeField] private EnemyBrain selfTestEnemyPrefab;

        [Tooltip("Tên GameObject con dùng cho lần tự kiểm tra nhân bản visual (tham số -iw-selftest-visual).")]
        [SerializeField] private string selfTestVisualName = "ShadowExchangeClone";

        [Tooltip("Số giây tồn tại của bản sao visual tự kiểm tra.")]
        [SerializeField, Min(0.5f)] private float selfTestVisualLifetime = 6f;

        private void Start()
        {
            // Chỉ chạy trên player thật khi có tham số; trong Editor bỏ qua để không phá Play mode thường.
            if (!enableCommandLine || Application.isEditor) return;

            string[] args = Environment.GetCommandLineArgs();
            NetBootstrap boot = FindAnyObjectByType<NetBootstrap>();
            if (boot == null) return;

            // Bật log chi tiết của NGO khi cần chẩn đoán (tham số -iw-netlog).
            if (HasFlag(args, "-iw-netlog"))
            {
                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null) manager.LogLevel = LogLevel.Developer;
                Debug.Log("[NetAutoConnect] bat log chi tiet NGO (Developer).");
            }

            // Giữ xe luôn sống khi test tương tác thật, áp dụng cho cả đường host IP lẫn host Relay.
            if (HasFlag(args, "-iw-godmode")) StartCoroutine(GodModeRoutine());

            // Dọn sạch quái và tắt màn chơi để có sân test yên tĩnh (dùng kèm -iw-godmode khi test input).
            if (HasFlag(args, "-iw-clean")) StartCoroutine(CleanArenaRoutine());

            if (HasFlag(args, "-iw-host"))
            {
                ushort hostPort = ReadPort(args);
                if (hostPort > 0) boot.SetHostPort(hostPort);

                bool ok = boot.StartHost();
                Debug.Log($"[NetAutoConnect] start host ok={ok}");

                if (HasFlag(args, "-iw-selftest-enemy")) StartCoroutine(EnemySelfTestRoutine());
                if (HasFlag(args, "-iw-selftest-visual")) StartCoroutine(VisualSelfTestRoutine());
                if (HasFlag(args, "-iw-selftest-stage")) StartCoroutine(StageSelfTestRoutine());
                if (HasFlag(args, "-iw-selftest-win")) StartCoroutine(WinSelfTestRoutine());
                if (HasFlag(args, "-iw-rehost-after")) StartCoroutine(RehostRoutine(boot, ReadFloat(args, "-iw-rehost-after", 10f)));
            }
            else if (HasFlag(args, "-iw-relay-host"))
            {
                boot.StartHostWithRelay();
                Debug.Log("[NetAutoConnect] start relay host");
            }
            else
            {
                string relayJoin = ReadValue(args, "-iw-relay-join");
                string lobbyJoin = ReadValue(args, "-iw-lobby-join");
                string lobbyQuery = ReadValue(args, "-iw-lobby-query");
                if (!string.IsNullOrEmpty(lobbyQuery))
                {
                    StartCoroutine(QueryLobbyRoutine(lobbyQuery, boot));
                    Debug.Log($"[NetAutoConnect] query lobby {lobbyQuery}");
                }
                else if (!string.IsNullOrEmpty(lobbyJoin))
                {
                    StartCoroutine(JoinLobbyRoutine(lobbyJoin, boot));
                    Debug.Log($"[NetAutoConnect] join lobby {lobbyJoin}");
                }
                else if (!string.IsNullOrEmpty(relayJoin))
                {
                    boot.StartClientWithRelay(relayJoin);
                    Debug.Log($"[NetAutoConnect] join relay {relayJoin}");
                }
                else
                {
                    string join = ReadValue(args, "-iw-join");
                    if (string.IsNullOrEmpty(join)) return;

                    boot.SetJoinTarget(join, ReadPort(args));
                    bool ok = boot.StartClient();
                    Debug.Log($"[NetAutoConnect] join {join} ok={ok}");

                    if (HasFlag(args, "-iw-retry")) StartCoroutine(RetryConnectRoutine(boot));
                    if (HasFlag(args, "-iw-selftest-cast")) StartCoroutine(CastSelfTestRoutine());
                    if (HasFlag(args, "-iw-selftest-move")) StartCoroutine(MoveSelfTestRoutine());

                    // Thoát êm sau N giây (dùng để test disconnect sạch thay vì kill tiến trình).
                    float exitAfter = ReadFloat(args, "-iw-exit-after", 0f);
                    if (exitAfter > 0f) StartCoroutine(ExitAfterRoutine(boot, exitAfter));
                }
            }

            StartCoroutine(LogStateRoutine());

            if (HasFlag(args, "-iw-selftest")) StartCoroutine(SelfTestRoutine());
        }

        /// <summary>
        /// Thử kết nối lại nếu sau vài giây vẫn chưa vào được phòng.
        /// Dùng khi kiểm chứng tình huống client join lại sau khi bị ngắt (tham số -iw-retry).
        /// </summary>
        private IEnumerator RetryConnectRoutine(NetBootstrap boot)
        {
            const int maxRetries = 2;
            const float waitBeforeRetry = 6f;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                yield return new WaitForSeconds(waitBeforeRetry);

                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null && manager.IsConnectedClient) yield break;

                Debug.LogWarning($"[NetAutoConnect] chua vao duoc phong sau {waitBeforeRetry}s (lan thu {attempt + 1}/{maxRetries + 1}) -> khoi dong lai client.");
                boot.Shutdown();
                yield return new WaitForSeconds(1f);
                boot.StartClient();
            }
        }

        /// <summary>
        /// Thoát êm sau N giây: tắt mạng rồi thoát tiến trình, để test "client rời phòng bình thường"
        /// thay vì kill tiến trình (kill gây timeout phía host). Tham số -iw-exit-after &lt;giây&gt;.
        /// </summary>
        private IEnumerator ExitAfterRoutine(NetBootstrap boot, float delay)
        {
            yield return new WaitForSeconds(delay);

            Debug.Log("[NetAutoConnect] thoat em sau " + delay + "s (disconnect sach).");
            boot.Shutdown();
            yield return new WaitForSeconds(0.8f);
            Application.Quit();
        }

        /// <summary>
        /// Host tạo lại phòng sau N giây: tắt phiên hiện tại rồi mở phòng mới, dùng để kiểm chứng
        /// đường khôi phục khi transport đã sập sau một client bị timeout. Tham số -iw-rehost-after &lt;giây&gt;.
        /// </summary>
        private IEnumerator RehostRoutine(NetBootstrap boot, float delay)
        {
            yield return new WaitForSeconds(delay);

            Debug.Log("[NetAutoConnect] rehost sau " + delay + "s (tao lai phong).");
            boot.Shutdown();

            // Chờ một nhịp để NGO nhả socket và dọn NetworkObject trước khi mở phòng mới.
            yield return new WaitForSeconds(2f);

            bool ok = boot.StartHost();
            bool listening = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            Debug.Log("[NetAutoConnect] rehost StartHost ok=" + ok + " listening=" + listening);
        }

        /// <summary>
        /// Giữ mọi xe luôn đầy máu để test tương tác thật (bàn phím/chuột) mà không bị quái giết.
        /// Chỉ dùng khi chạy player với tham số -iw-godmode (công cụ test, không phải cơ chế game).
        /// </summary>
        private IEnumerator GodModeRoutine()
        {
            var wait = new WaitForSeconds(0.25f);

            while (true)
            {
                var tanks = UnityEngine.Object.FindObjectsByType<TankBrain>(FindObjectsInactive.Exclude);
                for (int i = 0; i < tanks.Length; i++)
                {
                    var tank = tanks[i];
                    if (tank != null && tank.IsAlive) tank.SetHealth(tank.Stats.MaxHealth);
                }

                yield return wait;
            }
        }

        /// <summary>
        /// Tắt màn chơi và dọn toàn bộ quái để test input thật trong sân yên tĩnh.
        /// Chỉ dùng khi chạy player với tham số -iw-clean (công cụ test).
        /// </summary>
        private IEnumerator CleanArenaRoutine()
        {
            yield return new WaitForSeconds(2f);

            var stage = FindAnyObjectByType<IronWasteland.Stages.StageManager>();
            if (stage != null) stage.enabled = false;

            var enemies = UnityEngine.Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include);
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null) UnityEngine.Object.Destroy(enemies[i].gameObject);
            }

            Debug.Log("[NetAutoConnect] clean: tat man choi va don " + enemies.Length + " quai.");
        }

        /// <summary>
        /// Đánh vào xe của host sau vài giây để kiểm chứng máu được nhân bản xuống client.
        /// Chỉ chạy trên host và chỉ khi có tham số -iw-selftest.
        /// </summary>
        private IEnumerator SelfTestRoutine()
        {
            yield return new WaitForSeconds(selfTestDelay);

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) yield break;

            TankBrain[] tanks = FindObjectsByType<TankBrain>(FindObjectsInactive.Exclude);
            TankBrain target = null;
            for (int i = 0; i < tanks.Length; i++)
            {
                NetworkObject networkObject = tanks[i].GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.OwnerClientId == NetworkManager.ServerClientId)
                {
                    target = tanks[i];
                    break;
                }
            }

            if (target == null)
            {
                Debug.Log("[NetSelfTest] không tìm thấy xe của host");
                yield break;
            }

            float before = target.CurrentHealth;
            target.TakeDamage(selfTestDamageAmount);
            Debug.Log($"[NetSelfTest] hp host {before:F0} -> {target.CurrentHealth:F0} (thô {selfTestDamageAmount:F0})");
        }

        /// <summary>
        /// Client tự bấm một kỹ năng qua RPC để kiểm chứng yêu cầu được host xử lý.
        /// Chỉ chạy khi có tham số -iw-selftest-cast.
        /// </summary>
        private IEnumerator CastSelfTestRoutine()
        {
            yield return new WaitForSeconds(selfTestDelay);

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer) yield break;

            TankBrain[] tanks = FindObjectsByType<TankBrain>(FindObjectsInactive.Exclude);
            TankBrain target = null;
            NetPlayerController controller = null;
            for (int i = 0; i < tanks.Length; i++)
            {
                NetworkObject networkObject = tanks[i].GetComponent<NetworkObject>();
                if (networkObject == null || networkObject.OwnerClientId != manager.LocalClientId) continue;

                target = tanks[i];
                controller = tanks[i].GetComponent<NetPlayerController>();
                break;
            }

            if (target == null || controller == null)
            {
                Debug.Log("[NetSelfTest] client không tìm thấy xe của mình");
                yield break;
            }

            float energyBefore = target.CurrentEnergy;
            bool requested = controller.RequestCast(selfTestSkillIndex, (Vector2)target.transform.position + Vector2.right * 10f);
            Debug.Log($"[NetSelfTest] client xin cast {selfTestSkillIndex} requested={requested} energy={energyBefore:F0}");
        }

        /// <summary>
        /// Client tự di chuyển xe của mình một đoạn ngắn để kiểm tra đồng bộ vị trí sau sự cố.
        /// Tốc độ thấp hơn MoveSpeed để không bị host kéo ngược trong ValidateOwnerMovement.
        /// Chỉ chạy khi có tham số -iw-selftest-move.
        /// </summary>
        private IEnumerator MoveSelfTestRoutine()
        {
            yield return new WaitForSeconds(selfTestDelay);

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer) yield break;

            TankBrain[] tanks = FindObjectsByType<TankBrain>(FindObjectsInactive.Exclude);
            TankBrain target = null;
            for (int i = 0; i < tanks.Length; i++)
            {
                NetworkObject networkObject = tanks[i].GetComponent<NetworkObject>();
                if (networkObject == null || networkObject.OwnerClientId != manager.LocalClientId) continue;

                target = tanks[i];
                break;
            }

            if (target == null)
            {
                Debug.Log("[NetSelfTest] move: client không tìm thấy xe của mình");
                yield break;
            }

            Rigidbody2D body = target.GetComponent<Rigidbody2D>();
            Vector2 start = target.transform.position;
            const float duration = 6f;
            const float speed = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Vector2 next = (Vector2)target.transform.position + Vector2.right * (speed * Time.deltaTime);
                target.transform.position = next;
                if (body != null) body.position = next;
                yield return null;
            }

            Debug.Log($"[NetSelfTest] move {start.x:F1},{start.y:F1} -> {target.transform.position.x:F1},{target.transform.position.y:F1} trong {duration:F0}s");
        }

        /// <summary>
        /// Host tự spawn một quái, đánh nó rồi despawn để kiểm tra client nhận đúng máu và sự kiện huỷ.
        /// Chỉ chạy khi có tham số -iw-selftest-enemy.
        /// </summary>
        private IEnumerator EnemySelfTestRoutine()
        {
            yield return new WaitForSeconds(selfTestDelay);

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) yield break;

            NetSpawnService spawn = FindAnyObjectByType<NetSpawnService>();
            if (spawn == null || selfTestEnemyPrefab == null)
            {
                Debug.Log("[NetSelfTest] thiếu NetSpawnService hoặc prefab quái để test");
                yield break;
            }

            EnemyBrain enemy = spawn.SpawnEnemy(selfTestEnemyPrefab, (Vector2)transform.position + Vector2.up * 4f, null);
            if (enemy == null)
            {
                Debug.Log("[NetSelfTest] spawn quái thất bại");
                yield break;
            }

            Debug.Log($"[NetSelfTest] spawn quái {enemy.name} hp={enemy.CurrentHealth:F0}");

            yield return new WaitForSeconds(3f);
            float before = enemy.CurrentHealth;
            enemy.TakeDamage(100f);
            Debug.Log($"[NetSelfTest] đánh quái {before:F0} -> {enemy.CurrentHealth:F0}");

            yield return new WaitForSeconds(3f);
            NetEntity.Despawn(enemy.gameObject);
            Debug.Log("[NetSelfTest] despawn quái");
        }

        /// <summary>
        /// Host tự gửi một bản sao visual của template con xuống client để kiểm tra cơ chế NetVisualSpawn.
        /// Chỉ chạy khi có tham số -iw-selftest-visual.
        /// </summary>
        private IEnumerator VisualSelfTestRoutine()
        {
            yield return new WaitForSeconds(selfTestDelay);

            NetworkManager manager = NetworkManager.Singleton;
            NetVisualSpawn visual = NetVisualSpawn.Instance;
            if (manager == null || !manager.IsServer || visual == null) yield break;

            TankBrain owner = null;
            TankBrain[] tanks = FindObjectsByType<TankBrain>(FindObjectsInactive.Exclude);
            for (int i = 0; i < tanks.Length; i++)
            {
                NetworkObject networkObject = tanks[i].GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.OwnerClientId == NetworkManager.ServerClientId)
                {
                    owner = tanks[i];
                    break;
                }
            }

            NetworkObject root = owner != null ? owner.GetComponent<NetworkObject>() : null;
            if (root == null)
            {
                Debug.Log("[NetSelfTest] không tìm thấy xe host để test visual");
                yield break;
            }

            Vector3 position = owner.transform.position + Vector3.up * 3f;
            visual.BroadcastByName(root, selfTestVisualName, position, Quaternion.identity, selfTestVisualLifetime, false);
            Debug.Log($"[NetSelfTest] gửi visual '{selfTestVisualName}' tại {position} sống {selfTestVisualLifetime:F0}s");
        }

        /// <summary>
        /// Giết vài quái của ải trên host để kiểm chứng bộ đếm mạng chạy đúng trên build thật.
        /// Chỉ dùng cho test tự động: -iw-selftest-stage.
        /// </summary>
        private IEnumerator StageSelfTestRoutine()
        {
            yield return new WaitForSeconds(selfTestDelay);

            IronWasteland.Stages.StageManager stage = FindAnyObjectByType<IronWasteland.Stages.StageManager>();
            if (stage == null)
            {
                Debug.Log("[NetSelfTest] khong tim thay StageManager");
                yield break;
            }

            int killed = 0;
            EnemyBrain[] enemies = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include);
            for (int i = 0; i < enemies.Length && killed < 2; i++)
            {
                EnemyBrain enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                enemy.TakeDamage(99999f);
                killed++;
            }

            Debug.Log($"[NetSelfTest] giet {killed} quai -> Kills={stage.Kills}/{stage.KillTarget} state={stage.State}");
        }

        /// <summary>
        /// Tìm phòng công khai theo mã rồi lấy mã Relay (không join phòng) — dùng để test trên cùng một máy
        /// vì hai tiến trình dùng chung danh tính Unity Services nên không thể join phòng của chính mình.
        /// Chỉ dùng cho test tự động: -iw-lobby-query &lt;mã phòng&gt;.
        /// </summary>
        private IEnumerator QueryLobbyRoutine(string lobbyCode, NetBootstrap boot)
        {
            var task = NetRelayService.FindLobbyRelayCodeAsync(lobbyCode);
            while (!task.IsCompleted) yield return null;

            string relayCode = task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion ? task.Result : null;
            if (string.IsNullOrEmpty(relayCode))
            {
                Debug.LogWarning($"[NetAutoConnect] lobby {lobbyCode} -> khong tim thay ma relay ({NetRelayService.LastError})");
                yield break;
            }

            boot.StartClientWithRelay(relayCode);
            Debug.Log($"[NetAutoConnect] lobby {lobbyCode} -> relay {relayCode} (qua query)");
        }

        /// <summary>
        /// Vào phòng bằng mã Lobby: tra mã Relay trong dữ liệu phòng rồi mới kết nối.
        /// Chỉ dùng cho test tự động: -iw-lobby-join &lt;mã phòng&gt;.
        /// </summary>
        private IEnumerator JoinLobbyRoutine(string lobbyCode, NetBootstrap boot)
        {
            var task = NetRelayService.JoinLobbyAndGetRelayCodeAsync(lobbyCode);
            while (!task.IsCompleted) yield return null;

            string relayCode = task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion ? task.Result : null;
            if (string.IsNullOrEmpty(relayCode))
            {
                Debug.LogWarning($"[NetAutoConnect] lobby {lobbyCode} -> khong lay duoc ma relay ({NetRelayService.LastError})");
                yield break;
            }

            boot.StartClientWithRelay(relayCode);
            Debug.Log($"[NetAutoConnect] lobby {lobbyCode} -> relay {relayCode}");
        }

        /// <summary>
        /// Giết sạch quái trên host cho tới khi đạt mục tiêu để kiểm chứng nhánh THẮNG của ải.
        /// Giữ xe host còn sống trong lúc test (batchmode không điều khiển được) để ải không thua trước.
        /// Chỉ dùng cho test tự động: -iw-selftest-win.
        /// </summary>
        private IEnumerator WinSelfTestRoutine()
        {
            yield return new WaitForSeconds(selfTestDelay);

            IronWasteland.Stages.StageManager stage = FindAnyObjectByType<IronWasteland.Stages.StageManager>();
            if (stage == null)
            {
                Debug.Log("[NetSelfTest] khong tim thay StageManager");
                yield break;
            }

            float deadline = Time.time + 150f;
            while (Time.time < deadline && stage.State == IronWasteland.Stages.StageState.Running)
            {
                TankBrain[] tanks = FindObjectsByType<TankBrain>(FindObjectsInactive.Exclude);
                for (int i = 0; i < tanks.Length; i++)
                {
                    if (tanks[i] == null || !tanks[i].IsAlive) continue;
                    tanks[i].SetHealth(tanks[i].Stats.MaxHealth);
                    tanks[i].SetEnergy(tanks[i].Stats.MaxEnergy);
                }

                EnemyBrain[] enemies = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include);
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyBrain enemy = enemies[i];
                    if (enemy == null || !enemy.IsAlive) continue;
                    enemy.TakeDamage(99999f);
                }

                yield return new WaitForSeconds(0.5f);
            }

            Debug.Log($"[NetSelfTest] win -> state={stage.State} kills={stage.Kills}/{stage.KillTarget}");
        }

        private IEnumerator LogStateRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(logInterval);

                NetworkManager manager = NetworkManager.Singleton;
                if (manager == null) continue;

                TankBrain[] tanks = FindObjectsByType<TankBrain>(FindObjectsInactive.Exclude);
                string details = string.Empty;
                for (int i = 0; i < tanks.Length; i++)
                {
                    TankBrain tank = tanks[i];
                    NetworkObject networkObject = tank.GetComponent<NetworkObject>();
                    string owner = networkObject != null ? networkObject.OwnerClientId.ToString() : "?";
                    details += $" [owner={owner} hp={tank.CurrentHealth:F0} en={tank.CurrentEnergy:F0}" +
                               $" pos={tank.transform.position.x:F1},{tank.transform.position.y:F1}" +
                               $" local={tank.IsLocallyControlled} server={tank.IsServerSimulated}]";
                }

                EnemyBrain[] enemies = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude);
                string enemyDetails = string.Empty;
                for (int i = 0; i < enemies.Length && i < 3; i++)
                {
                    NetworkObject enemyObject = enemies[i].GetComponent<NetworkObject>();
                    string id = enemyObject != null ? enemyObject.NetworkObjectId.ToString() : "?";
                    enemyDetails += $" [id={id} hp={enemies[i].CurrentHealth:F0}]";
                }

                var stage = FindAnyObjectByType<IronWasteland.Stages.StageManager>();
                string stageText = stage != null
                    ? $" stage={stage.State} kills={stage.Kills}/{stage.KillTarget} wave={stage.WaveIndex + 1}"
                    : " stage=none";

                NetBootstrap boot = FindAnyObjectByType<NetBootstrap>();
                Debug.Log($"[NetState] listening={manager.IsListening} server={manager.IsServer}" +
                          $" lost={(boot != null && boot.ConnectionLost)}" +
                          $" ended={(boot != null && boot.SessionEnded)}" +
                          $" clients={manager.ConnectedClientsIds.Count} tick={manager.NetworkConfig.TickRate}" +
                          $" tanks={tanks.Length}{details} enemies={enemies.Length}{enemyDetails}" +
                          $" ghosts={(NetVisualSpawn.Instance != null ? NetVisualSpawn.Instance.GhostCount : 0)}" +
                          stageText);
            }
        }

        private static bool HasFlag(string[] args, string flag)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static string ReadValue(string[] args, string flag)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }

            return null;
        }

        private static ushort ReadPort(string[] args)
        {
            string raw = ReadValue(args, "-iw-port");
            return ushort.TryParse(raw, out ushort port) && port > 0 ? port : (ushort)7777;
        }

        private static float ReadFloat(string[] args, string flag, float fallback)
        {
            string raw = ReadValue(args, flag);
            return float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) && value > 0f ? value : fallback;
        }
    }
}
