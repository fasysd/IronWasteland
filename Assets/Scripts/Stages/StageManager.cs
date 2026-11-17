using System;
using System.Collections.Generic;
using IronWasteland.Enemies;
using IronWasteland.Net;
using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Stages
{
    public enum StageState
    {
        Idle,
        Running,
        Cleared,
        Failed
    }

    /// <summary>
    /// Dieu khien 1 man choi: spawn quai theo tung dot, dem so quai bi tieu diet,
    /// kiem tra dieu kien thang (du so mang) va thua (tank nguoi choi chet).
    ///
    /// Chua co UI: trang thai duoc log ra Console qua <see cref="logEvents"/> va
    /// co the doc truc tiep qua cac property public (Kills / WaveIndex / State...).
    /// </summary>
    [DisallowMultipleComponent]
    public class StageManager : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Du lieu ai (dot quai, dieu kien thang).")]
        [SerializeField] private StageDefinition definition;

        [Tooltip("GameController de lay tank nguoi choi. De trong se tu tim trong scene.")]
        [SerializeField] private GameController gameController;

        [Header("Vung spawn")]
        [Tooltip("Tam vung spawn quai (world space).")]
        [SerializeField] private Vector2 arenaCenter = Vector2.zero;

        [Tooltip("Ban kinh nho nhat quanh tam de dat quai.")]
        [SerializeField, Min(0f)] private float spawnRadiusMin = 11f;

        [Tooltip("Ban kinh lon nhat quanh tam de dat quai.")]
        [SerializeField, Min(0f)] private float spawnRadiusMax = 14f;

        [Header("Tuy chon")]
        [Tooltip("Tu dong bat dau ai khi scene chay.")]
        [SerializeField] private bool autoStart = true;

        [Tooltip("Log tien trinh ra Console.")]
        [SerializeField] private bool logEvents = true;

        public event Action<int, StageDefinition.Wave> WaveStarted;
        public event Action<int> KillsChanged;
        public event Action<StageManager> StageCleared;
        public event Action<StageManager> StageFailed;

        public StageDefinition Definition => definition;
        public StageState State { get; private set; } = StageState.Idle;
        public float Elapsed { get; private set; }
        public int Kills { get; private set; }
        public int KillTarget => definition != null ? definition.KillTarget : 0;
        public float TimeLimitSeconds => definition != null ? definition.TimeLimitSeconds : 0f;
        public int WaveIndex { get; private set; } = -1;
        public int SpawnedCount => m_SpawnedCount;
        public int AliveCount
        {
            get
            {
                int alive = 0;
                for (int i = 0; i < m_Spawned.Count; i++)
                {
                    EnemyBrain enemy = m_Spawned[i];
                    if (enemy != null && enemy.IsAlive) alive++;
                }
                return alive;
            }
        }

        private readonly List<StageDefinition.Wave> m_PendingWaves = new List<StageDefinition.Wave>();
        private readonly List<EnemyBrain> m_Spawned = new List<EnemyBrain>();

        // Danh sách tank người chơi (4 người khi chơi mạng). Với chơi mạng, host nhìn thấy
        // cả xe của chính nó lẫn xe proxy của các client nên chỉ cần quét scene.
        private readonly List<TankBrain> m_Players = new List<TankBrain>();
        private NetSpawnService m_NetSpawnService;

        private Transform m_Container;
        private float m_RosterTimer;
        private int m_SpawnedCount;
        private int m_TotalWaves;
        private bool m_Initialized;

        #region Lifecycle

        private void Start()
        {
            Initialize();

            // Client chỉ nhận trạng thái ải qua NetStageEvents; không tự bắt đầu ải cục bộ.
            if (TankBrain.NetworkSessionActive && NetworkManager.Singleton != null
                && !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            if (autoStart) StartStage();
        }

        private void Update()
        {
            if (State != StageState.Running) return;

            // Client không mô phỏng ải: host giữ đồng hồ, spawn và xét thắng/thua,
            // client chỉ nhận quái/sự kiện qua mạng. Chế độ offline không bị ảnh hưởng.
            if (TankBrain.NetworkSessionActive && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer) return;

            Elapsed += Time.deltaTime;

            SpawnDueWaves();
            RefreshRoster();
            EvaluateOutcome();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < m_Spawned.Count; i++)
            {
                EnemyBrain enemy = m_Spawned[i];
                if (enemy != null) enemy.OnDeathEvent -= OnEnemyDied;
            }

            m_Spawned.Clear();
            m_PendingWaves.Clear();
        }

        #endregion

        #region Public API

        /// <summary>Bat dau ai (duoc goi tu dong neu autoStart = true).</summary>
        public void StartStage()
        {
            Initialize();

            if (definition == null)
            {
                Debug.LogError($"[{nameof(StageManager)}] Chua gan StageDefinition.", this);
                return;
            }

            Kills = 0;
            Elapsed = 0f;
            WaveIndex = -1;
            m_SpawnedCount = 0;
            m_Spawned.Clear();
            m_PendingWaves.Clear();

            if (definition.Waves != null)
            {
                for (int i = 0; i < definition.Waves.Count; i++)
                {
                    if (definition.Waves[i] != null) m_PendingWaves.Add(definition.Waves[i]);
                }
            }

            m_PendingWaves.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            m_TotalWaves = m_PendingWaves.Count;

            State = StageState.Running;

            if (logEvents)
            {
                Debug.Log($"[Stage] Bat dau ai {definition.StageCode} - {definition.StageName}: " +
                          $"tieu diet {definition.KillTarget} quai, du kien {definition.TimeLimitSeconds:0}s, " +
                          $"{m_TotalWaves} dot / {definition.TotalEnemies} quai.");
            }

            KillsChanged?.Invoke(Kills);

            // Host gửi trạng thái ải xuống client (named message, không cần NetworkObject).
            NetStageEvents.Instance?.Broadcast(this, NetStageEvents.StateStarted);
        }

        /// <summary>Đổi màn chơi trước khi bắt đầu (host chọn màn trong lobby).</summary>
        public void SetDefinition(StageDefinition stage)
        {
            if (stage == null) return;
            definition = stage;
        }

        /// <summary>
        /// Đưa ải về trạng thái chờ và dọn sạch quái đã spawn — dùng khi cả phòng về lobby
        /// để chơi lại màn khác hoặc chơi lại từ đầu.
        /// </summary>
        public void ResetToIdle()
        {
            for (int i = 0; i < m_Spawned.Count; i++)
            {
                EnemyBrain enemy = m_Spawned[i];
                if (enemy == null) continue;

                enemy.OnDeathEvent -= OnEnemyDied;
                NetEntity.Despawn(enemy.gameObject);
            }

            m_Spawned.Clear();
            m_PendingWaves.Clear();

            Kills = 0;
            Elapsed = 0f;
            WaveIndex = -1;
            m_SpawnedCount = 0;
            State = StageState.Idle;

            KillsChanged?.Invoke(Kills);
        }

        #endregion

        #region Waves

        private void Initialize()
        {
            if (m_Initialized) return;
            m_Initialized = true;

            if (gameController == null) gameController = FindAnyObjectByType<GameController>();
            if (m_NetSpawnService == null) m_NetSpawnService = FindAnyObjectByType<NetSpawnService>();

            if (m_Container == null)
            {
                var container = new GameObject("Enemies");
                container.transform.SetParent(transform, false);
                m_Container = container.transform;
            }

            if (spawnRadiusMax < spawnRadiusMin) spawnRadiusMax = spawnRadiusMin;
        }

        private void SpawnDueWaves()
        {
            while (m_PendingWaves.Count > 0)
            {
                StageDefinition.Wave wave = m_PendingWaves[0];
                if (wave.StartTime > Elapsed) break;

                m_PendingWaves.RemoveAt(0);
                SpawnWave(wave);
            }
        }

        private void SpawnWave(StageDefinition.Wave wave)
        {
            WaveIndex++;

            if (wave.Spawns != null)
            {
                for (int i = 0; i < wave.Spawns.Count; i++)
                {
                    StageDefinition.EnemySpawn spawn = wave.Spawns[i];
                    if (spawn == null || spawn.Enemy == null)
                    {
                        Debug.LogWarning($"[Stage] Dot {WaveIndex + 1} co entry quai rong, bo qua.", this);
                        continue;
                    }

                    for (int c = 0; c < spawn.Count; c++) SpawnEnemy(spawn);
                }
            }

            if (logEvents)
            {
                Debug.Log($"[Stage] Dot {WaveIndex + 1}/{m_TotalWaves} - {wave.Label} " +
                          $"({wave.TotalCount} quai) tai t={Elapsed:0.0}s.");
            }

            WaveStarted?.Invoke(WaveIndex, wave);
        }

        private void SpawnEnemy(StageDefinition.EnemySpawn spawn)
        {
            Vector2 position = RandomSpawnPoint();

            // Chi so: lay tu prefab roi nhan he so cai dat cua dot (quai cuong hoa toc do...).
            EnemyStats stats = spawn.Enemy.Stats != null ? spawn.Enemy.Stats.Clone() : new EnemyStats();
            stats.MoveSpeed *= Mathf.Max(0.01f, spawn.SpeedMultiplier);

            EnemyBrain enemy;
            bool networked = TankBrain.NetworkSessionActive;

            if (networked && m_NetSpawnService != null)
            {
                // Host spawn qua NGO để mọi máy thấy cùng một con quái; SpawnEnemy tự Initialize.
                enemy = m_NetSpawnService.SpawnEnemy(spawn.Enemy, position, stats);
            }
            else
            {
                enemy = Instantiate(spawn.Enemy, position, Quaternion.identity, m_Container);
                enemy.Initialize(stats);
            }

            if (enemy == null) return;

            enemy.OnDeathEvent += OnEnemyDied;

            m_Spawned.Add(enemy);
            m_SpawnedCount++;
        }

        private Vector2 RandomSpawnPoint()
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float radius = UnityEngine.Random.Range(spawnRadiusMin, spawnRadiusMax);
            return arenaCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        #endregion

        #region Outcome

        private void OnEnemyDied()
        {
            if (State != StageState.Running) return;

            Kills++;
            KillsChanged?.Invoke(Kills);

            if (logEvents)
            {
                Debug.Log($"[Stage] Ha guc {Kills}/{KillTarget}.");
            }
        }

        /// <summary>
        /// Làm mới danh sách tank người chơi. Dùng <see cref="FindObjectsInactive.Include"/>
        /// để xe đã chết (bị tắt) vẫn nằm trong danh sách — nhờ đó phát hiện được "cả đội chết".
        /// </summary>
        private void RefreshRoster()
        {
            m_RosterTimer -= Time.deltaTime;
            if (m_RosterTimer > 0f && m_Players.Count > 0) return;
            m_RosterTimer = 0.5f;

            m_Players.Clear();
            TankBrain[] tanks = FindObjectsByType<TankBrain>(FindObjectsInactive.Include);
            for (int i = 0; i < tanks.Length; i++)
            {
                if (tanks[i] != null) m_Players.Add(tanks[i]);
            }
        }

        private bool AnyPlayerAlive()
        {
            for (int i = 0; i < m_Players.Count; i++)
            {
                TankBrain tank = m_Players[i];
                if (tank != null && tank.IsAlive) return true;
            }
            return false;
        }

        private void EvaluateOutcome()
        {
            // Chỉ thua khi cả đội đã chết; lúc mới vào chưa kịp spawn thì bỏ qua.
            if (m_Players.Count > 0 && !AnyPlayerAlive())
            {
                FailStage();
                return;
            }

            if (Kills >= KillTarget) ClearStage();
        }

        private void ClearStage()
        {
            if (State != StageState.Running) return;

            State = StageState.Cleared;

            if (logEvents)
            {
                Debug.Log($"[Stage] THANG ai {definition.StageCode} - {definition.StageName} " +
                          $"sau {Elapsed:0.0}s ({Kills}/{KillTarget} mang).");
            }

            StageCleared?.Invoke(this);
            NetStageEvents.Instance?.Broadcast(this, NetStageEvents.StateCleared);
        }

        private void FailStage()
        {
            if (State != StageState.Running) return;

            State = StageState.Failed;

            if (logEvents)
            {
                Debug.Log($"[Stage] THUA ai {definition.StageCode} - tank nguoi choi bi ha " +
                          $"tai t={Elapsed:0.0}s ({Kills}/{KillTarget} mang).");
            }

            StageFailed?.Invoke(this);
            NetStageEvents.Instance?.Broadcast(this, NetStageEvents.StateFailed);
        }

        #endregion

        private void OnValidate()
        {
            if (spawnRadiusMin < 0f) spawnRadiusMin = 0f;
            if (spawnRadiusMax < spawnRadiusMin) spawnRadiusMax = spawnRadiusMin;
        }
    }
}
