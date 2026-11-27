using System;
using System.Collections;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronWasteland.Net
{
    /// <summary>
    /// Tự động mở/ vào phòng khi chạy player từ dòng lệnh, phục vụ test nhiều client trên một máy.
    /// Tham số hỗ trợ:
    /// -iw-host            : chạy làm host.
    /// -iw-join &lt;địa chỉ&gt; : chạy làm client và kết nối tới địa chỉ đó (ví dụ 127.0.0.1).
    /// -iw-port &lt;cổng&gt;   : đổi cổng kết nối (mặc định 7777).
    /// Không truyền tham số nào thì component im lặng, màn PHÒNG LAN vẫn dùng bình thường.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class NetAutoConnect : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Toàn bộ công cụ test dòng lệnh (-iw-*, gồm -iw-rehost-after) chỉ tồn tại ở Editor và Development Build.
        // Bản build phát hành (Release) biên dịch ra component rỗng, không có logic tự rehost hay tự kết nối.
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

            // Giữ xe luôn sống khi test tương tác thật.
            if (HasFlag(args, "-iw-godmode")) StartCoroutine(GodModeRoutine());

            // Dọn sạch quái và tắt màn chơi để có sân test yên tĩnh (dùng kèm -iw-godmode khi test input).
            if (HasFlag(args, "-iw-clean")) StartCoroutine(CleanArenaRoutine());

            if (HasFlag(args, "-iw-host"))
            {
                ushort hostPort = ReadPort(args);
                if (hostPort > 0) boot.SetHostPort(hostPort);

                bool ok = boot.StartHost();
                Debug.Log($"[NetAutoConnect] start host ok={ok}");
                if (ok) ApplyHostFlow(boot, args);

                // -iw-spawn nhận tên bất kỳ prefab trong BossProbeCatalog (5 boss + 12 quái thường).
                // -iw-boss giữ làm bí danh cũ để các script test hiện có không phải sửa.
                string spawnProbe = ReadValue(args, "-iw-spawn");
                if (string.IsNullOrEmpty(spawnProbe)) spawnProbe = ReadValue(args, "-iw-boss");
                if (!string.IsNullOrEmpty(spawnProbe))
                {
                    // Sân test chỉ còn quái được chọn; bỏ qua để giữ đợt quái gốc của ải.
                    if (!HasFlag(args, "-iw-keep-waves"))
                    {
                        IronWasteland.Stages.StageDirector.SuppressEnemyWaves = true;
                        Debug.Log("[NetAutoConnect] solo spawn: da tat dot quai cua man.");
                    }

                    if (BossProbe.Request(spawnProbe))
                    {
                        StartCoroutine(BossProbe.AutoStartRoutine(boot));
                        StartCoroutine(SpawnProbeHotkeyRoutine(boot, spawnProbe));
                    }
                    else Debug.LogWarning("[NetAutoConnect] khong tim thay probe: " + spawnProbe);
                }

                string killEnemy = ReadValue(args, "-iw-kill");
                if (!string.IsNullOrEmpty(killEnemy)) StartCoroutine(KillEnemyRoutine(killEnemy));

                if (HasFlag(args, "-iw-selftest-enemy")) StartCoroutine(EnemySelfTestRoutine());
                if (HasFlag(args, "-iw-selftest-visual")) StartCoroutine(VisualSelfTestRoutine());
                if (HasFlag(args, "-iw-selftest-stage")) StartCoroutine(StageSelfTestRoutine());
                if (HasFlag(args, "-iw-selftest-win")) StartCoroutine(WinSelfTestRoutine());

                if (HasFlag(args, "-iw-rehost-after")) StartCoroutine(RehostRoutine(boot, ReadFloat(args, "-iw-rehost-after", 10f)));
            }
            else
            {
                {
                    string join = ReadValue(args, "-iw-join");
                    if (string.IsNullOrEmpty(join)) return;

                    boot.SetJoinTarget(join, ReadPort(args));

                    // Chọn xe/biến thể khi test tự động (không có UI để bấm trong batchmode).
                    int tankArg = ReadInt(args, "-iw-tank", -1);
                    if (tankArg >= 0) boot.SetLocalTankIndex((byte)Mathf.Clamp(tankArg, 0, 5));
                    int opt1Arg = ReadInt(args, "-iw-opt1", -1);
                    int opt2Arg = ReadInt(args, "-iw-opt2", -1);
                    if (opt1Arg >= 0 || opt2Arg >= 0)
                    {
                        boot.SetLocalOptionSkills(Mathf.Clamp(opt1Arg, 0, 2), Mathf.Clamp(opt2Arg, 0, 2));
                    }

                    bool ok = boot.StartClient();
                    Debug.Log($"[NetAutoConnect] join {join} ok={ok} tank={tankArg} opt1={opt1Arg} opt2={opt2Arg}");

                    if (HasFlag(args, "-iw-retry")) StartCoroutine(RetryConnectRoutine(boot));
                    if (HasFlag(args, "-iw-selftest-cast")) StartCoroutine(CastSelfTestRoutine());
                    if (HasFlag(args, "-iw-selftest-move")) StartCoroutine(MoveSelfTestRoutine());
                    if (HasFlag(args, "-iw-selftest-kills")) StartCoroutine(KillSelfTestRoutine());

                    // Tự động thi triển đúng một kỹ năng chỉ định vào quái gần nhất để kiểm chứng
                    // hiệu ứng hình ảnh hiển thị đồng thời ở host và client (dùng khi test tự động
                    // không có chuột/bàn phím để chọn kỹ năng).
                    // Truyền giá trị lớn hơn số kỹ năng (ví dụ 9) để luân phiên 1 → 2 → 3.
                    int autoCastSkill = ReadInt(args, "-iw-autocast", -1);
                    if (autoCastSkill >= 0)
                    {
                        int firstSkill = autoCastSkill >= TankBrain.SkillCount
                            ? -1
                            : Mathf.Clamp(autoCastSkill, 0, TankBrain.SkillCount - 1);
                        StartCoroutine(AutoCastRoutine(firstSkill, ReadFloat(args, "-iw-autocast-interval", 4f)));
                    }

                    // Thoát êm sau N giây (dùng để test disconnect sạch thay vì kill tiến trình).
                    float exitAfter = ReadFloat(args, "-iw-exit-after", 0f);
                    if (exitAfter > 0f) StartCoroutine(ExitAfterRoutine(boot, exitAfter));
                }
            }

            StartCoroutine(LogStateRoutine());

            // Client tự sẵn sàng khi vào lobby (dùng cho test tự động 2–4 máy).
            if (HasFlag(args, "-iw-ready")) StartCoroutine(AutoReadyRoutine(boot));

            if (HasFlag(args, "-iw-selftest")) StartCoroutine(SelfTestRoutine());
        }

        /// <summary>
        /// Host chọn luồng chơi sau khi mở phòng: mặc định chơi đơn (vào trận ngay, giữ hành vi cũ),
        /// truyền <c>-iw-flow coop</c> để dừng ở lobby chờ người khác sẵn sàng.
        /// </summary>
        private static void ApplyHostFlow(NetBootstrap boot, string[] args)
        {
            MatchFlow flow = boot.Flow;
            if (flow == null) return;

            // Cho phép chọn màn trực tiếp khi test tự động (ví dụ -iw-stage 2-3).
            string stageCode = ReadValue(args, "-iw-stage");
            if (!string.IsNullOrEmpty(stageCode))
            {
                if (flow.SelectStageByCode(stageCode)) Debug.Log("[NetAutoConnect] chon man " + stageCode);
                else Debug.LogWarning("[NetAutoConnect] khong tim thay man " + stageCode);
            }

            string mode = ReadValue(args, "-iw-flow");
            if (!string.IsNullOrEmpty(mode) && mode.ToLowerInvariant() == "coop")
            {
                flow.SetMode(MatchMode.Coop);
                Debug.Log("[NetAutoConnect] che do co-op: cho nguoi choi san sang");
                return;
            }

            flow.StartSingle();
            Debug.Log("[NetAutoConnect] che do choi don: vao tran ngay");
        }

        /// <summary>Client tự bấm sẵn sàng khi đã thấy danh sách phòng (test tự động).</summary>
        private IEnumerator AutoReadyRoutine(NetBootstrap boot)
        {
            float deadline = Time.time + 25f;
            while (Time.time < deadline)
            {
                MatchFlow flow = boot.Flow;
                if (flow != null && flow.PlayerCount > 0 && flow.Phase == MatchPhase.Lobby)
                {
                    if (!flow.LocalReady) flow.RequestReady(true);
                    yield break;
                }

                yield return new WaitForSeconds(0.25f);
            }
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
        /// Client liên tục ngắm quái gần nhất rồi xin host thi triển đòn đánh thường, để kiểm chứng
        /// vòng client → RPC → host gây sát thương → tăng số mạng hạ gục trong màn chơi.
        /// Chỉ chạy khi có tham số -iw-selftest-kills.
        /// </summary>
        private IEnumerator KillSelfTestRoutine()
        {
            yield return new WaitForSeconds(selfTestDelay);

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer) yield break;

            NetSpawnService spawnService = FindAnyObjectByType<NetSpawnService>();
            TankBrain ownTank = null;
            if (spawnService != null) spawnService.TryGetTank(manager.LocalClientId, out ownTank);

            NetPlayerController controller = ownTank != null ? ownTank.GetComponent<NetPlayerController>() : null;
            if (controller == null)
            {
                Debug.Log("[NetSelfTest] kills: client không tìm thấy xe hoặc controller của mình");
                yield break;
            }

            const float duration = 40f;
            const float interval = 0.5f;
            float elapsed = 0f;
            int casts = 0;

            while (elapsed < duration)
            {
                yield return new WaitForSeconds(interval);
                elapsed += interval;

                EnemyBrain target = null;
                float bestDistance = float.MaxValue;
                EnemyBrain[] enemies = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude);
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyBrain enemy = enemies[i];
                    if (enemy == null || !enemy.IsAlive) continue;

                    float distance = ((Vector2)enemy.transform.position - (Vector2)ownTank.transform.position).sqrMagnitude;
                    if (distance >= bestDistance) continue;

                    bestDistance = distance;
                    target = enemy;
                }

                if (target == null) continue;
                if (controller.RequestCast(0, target.transform.position)) casts++;
            }

            Debug.Log($"[NetSelfTest] kills: client xin thi trien {casts} lan trong {duration:F0}s");
        }

        /// <summary>
        /// Client tự ngắm quái gần nhất rồi xin host thi triển đúng kỹ năng được chỉ định, lặp lại theo chu kỳ.
        /// Dùng để kiểm chứng hiệu ứng hình ảnh của một kỹ năng cụ thể có xuất hiện đồng thời ở host và client
        /// hay không khi môi trường test tự động không thể bấm phím chọn kỹ năng.
        /// Chỉ chạy khi có tham số -iw-autocast (kèm -iw-autocast-interval nếu muốn đổi nhịp).
        /// Truyền skillIndex = -1 để luân phiên ba kỹ năng 1 → 2 → 3 (ấn → 1A → 2B của GreenFox).
        /// </summary>
        private IEnumerator AutoCastRoutine(int skillIndex, float interval)
        {
            yield return new WaitForSeconds(selfTestDelay);

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer) yield break;

            float period = Mathf.Max(0.5f, interval);
            bool rotate = skillIndex < 0;
            int[] rotation = { 1, 2, 3 };
            int rotationIndex = 0;
            Debug.Log($"[NetSelfTest] autocast {(rotate ? "luân phiên 1→2→3" : "skill=" + skillIndex)} mỗi {period:F1}s");

            while (true)
            {
                yield return new WaitForSeconds(period);

                MatchFlow flow = FindAnyObjectByType<MatchFlow>();
                if (flow == null)
                {
                    Debug.Log("[NetSelfTest] autocast bỏ qua: chưa tìm thấy MatchFlow");
                    continue;
                }

                if (flow.Phase != MatchPhase.Playing)
                {
                    Debug.Log($"[NetSelfTest] autocast bỏ qua: phase={flow.Phase}");
                    continue;
                }

                // Client không giữ danh sách spawn của host (chỉ host gọi SpawnPlayerTank), nên
                // NetSpawnService.TryGetTank luôn trả về false ở client. Phải quét NetworkObject
                // đang tồn tại để tìm xe của chính mình giống CastSelfTestRoutine.
                TankBrain ownTank = null;
                TankBrain[] tanks = FindObjectsByType<TankBrain>(FindObjectsInactive.Exclude);
                for (int i = 0; i < tanks.Length; i++)
                {
                    NetworkObject networkObject = tanks[i].GetComponent<NetworkObject>();
                    if (networkObject == null || !networkObject.IsSpawned) continue;
                    if (networkObject.OwnerClientId != manager.LocalClientId) continue;

                    ownTank = tanks[i];
                    break;
                }

                if (ownTank == null)
                {
                    Debug.Log("[NetSelfTest] autocast bỏ qua: không tìm thấy xe của client");
                    continue;
                }

                NetPlayerController controller = ownTank.GetComponent<NetPlayerController>();
                if (controller == null || !ownTank.IsAlive)
                {
                    Debug.Log($"[NetSelfTest] autocast bỏ qua: controller={(controller != null)} alive={ownTank.IsAlive} hp={ownTank.CurrentHealth:F0}");
                    continue;
                }

                EnemyBrain target = null;
                float bestDistance = float.MaxValue;
                EnemyBrain[] enemies = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude);
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyBrain enemy = enemies[i];
                    if (enemy == null || !enemy.IsAlive) continue;

                    float distance = ((Vector2)enemy.transform.position - (Vector2)ownTank.transform.position).sqrMagnitude;
                    if (distance >= bestDistance) continue;

                    bestDistance = distance;
                    target = enemy;
                }

                if (target == null)
                {
                    Debug.Log("[NetSelfTest] autocast bỏ qua: không còn quái sống");
                    continue;
                }

                int castIndex = rotate ? rotation[rotationIndex % rotation.Length] : skillIndex;
                if (rotate) rotationIndex++;

                bool requested = controller.RequestCast(castIndex, (Vector2)target.transform.position);
                Debug.Log($"[NetSelfTest] autocast xin thi triển {castIndex} vào {target.name} tại {target.transform.position.x:F1},{target.transform.position.y:F1} requested={requested} en={ownTank.CurrentEnergy:F0}");
            }
        }

        /// <summary>
        /// Host bấm F8 để spawn lại quái probe đang chọn ngay trong phòng hiện tại,
        /// không cần thoát/restart. Chỉ bật khi có -iw-spawn hoặc -iw-boss.
        /// </summary>
        private IEnumerator SpawnProbeHotkeyRoutine(NetBootstrap boot, string token)
        {
            yield return null;
            string current = token;
            Debug.Log("[NetAutoConnect] F8: spawn lai probe '" + current + "', F9: chon va spawn probe ke tiep.");

            while (true)
            {
                MatchFlow flow = boot != null ? boot.Flow : null;
                bool playing = flow != null && flow.Phase == MatchPhase.Playing;
                if (playing && Keyboard.current != null)
                {
                    if (Keyboard.current[Key.F9].wasPressedThisFrame)
                    {
                        current = NextProbeToken(current);
                        Debug.Log("[NetAutoConnect] F9: chon probe '" + current + "'.");
                        TrySpawnProbe(current, "F9");
                    }
                    else if (Keyboard.current[Key.F8].wasPressedThisFrame)
                    {
                        TrySpawnProbe(current, "F8");
                    }
                }

                yield return null;
            }
        }

        private static void TrySpawnProbe(string token, string source)
        {
            if (!BossProbe.Request(token))
            {
                Debug.LogWarning("[NetAutoConnect] " + source + ": khong tim thay probe " + token);
                return;
            }

            if (!BossProbe.TrySpawn(new Vector3(0f, 6f, 0f)))
                Debug.LogWarning("[NetAutoConnect] " + source + ": spawn that bai (host chua san sang?).");
        }

        private static string NextProbeToken(string current)
        {
            BossProbeCatalog catalog = Resources.Load<BossProbeCatalog>("BossProbeCatalog");
            if (catalog == null || catalog.Prefabs == null || catalog.Prefabs.Length == 0) return current;

            int index = 0;
            for (int i = 0; i < catalog.Prefabs.Length; i++)
            {
                if (catalog.Prefabs[i] != null && catalog.Prefabs[i].name == current)
                {
                    index = i;
                    break;
                }
            }

            int next = (index + 1) % catalog.Prefabs.Length;
            return catalog.Prefabs[next] != null ? catalog.Prefabs[next].name : current;
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

        /// <summary>
        /// Giết quái theo tên để kích hoạt kỹ năng nổ khi chết (chỉ dùng cho test tự động).
        /// </summary>
        private IEnumerator KillEnemyRoutine(string prefix)
        {
            yield return new WaitForSeconds(5f);

            while (true)
            {
                EnemyBrain[] enemies = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude);
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyBrain enemy = enemies[i];
                    if (enemy == null || !enemy.IsAlive) continue;
                    if (!enemy.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    enemy.TakeDamage(999999f);
                }

                yield return new WaitForSeconds(1f);
            }
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
            return ushort.TryParse(raw, out ushort port) && port > 0 ? port : NetBootstrap.DefaultHostPort;
        }

        private static float ReadFloat(string[] args, string flag, float fallback)
        {
            string raw = ReadValue(args, flag);
            return float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) && value > 0f ? value : fallback;
        }

        /// <summary>Đọc số nguyên từ dòng lệnh (dùng cho -iw-tank/-iw-opt1/-iw-opt2), trả fallback khi thiếu hoặc sai.</summary>
        private static int ReadInt(string[] args, string flag, int fallback)
        {
            string raw = ReadValue(args, flag);
            return int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value) ? value : fallback;
        }
#endif
    }
}