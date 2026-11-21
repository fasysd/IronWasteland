using System;
using System.Collections.Generic;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Stages
{
    public enum StageRunState
    {
        Idle = 0,
        Running = 1,
        Cleared = 2,
        Failed = 3,
    }

    /// <summary>
    /// Dieu phoi 1 man choi: doc <see cref="StageDefinition"/>, sinh quai theo dot qua
    /// <see cref="EnemySpawner"/>, theo doi cac <see cref="StageObjective"/> va quyet dinh
    /// thang / thua.
    ///
    /// Khac voi <see cref="StageManager"/> (ban cu, chi biet "diet du N quai"):
    /// - Nhieu dieu kien thang cung luc (diet + sinh ton, pha tru + sinh ton...).
    /// - Quai sinh theo vung (<see cref="SpawnZone"/>) chu khong phai vong tron quanh 1 diem.
    /// - Co su kien de HUD / am thanh / chuyen man lang nghe.
    ///
    /// Cach dung trong scene:
    /// 1. GameObject "StageDirector" -> gan component nay.
    /// 2. Keo asset StageDefinition (vi du Stage_1_1.asset) vao o Definition.
    /// 3. Keo GameObject co GameController cua tank vao o Game Controller.
    /// 4. Keo GameObject co EnemySpawner vao o Spawner, va cac SpawnZone vao list Spawn Zones.
    /// </summary>
    [DisallowMultipleComponent]
    public class StageDirector : MonoBehaviour
    {
        [Header("Dữ liệu màn")]
        [SerializeField] private StageDefinition definition;

        [Tooltip("GameController đang giữ tank của người chơi trong scene này.")]
        [SerializeField] private GameController gameController;

        [Header("Sinh quái")]
        [SerializeField] private EnemySpawner spawner;

        [Tooltip("Các vùng sinh quái trên map. Đợt nào khai báo ZoneId thì quái rơi trong vùng đó.")]
        [SerializeField] private List<SpawnZone> spawnZones = new List<SpawnZone>();

        [Header("Tuỳ chọn")]
        [Tooltip("Tự động bắt đầu khi scene chạy.")]
        [SerializeField] private bool autoStart = true;

        [Tooltip("Co-op: nhân đôi máu quái.")]
        [SerializeField] private bool coopMode = false;

        [Tooltip("Tank chết là thua màn.")]
        [SerializeField] private bool failOnPlayerDeath = true;

        [SerializeField] private bool logEvents = true;

        private readonly List<StageObjective> m_Objectives = new List<StageObjective>();
        private readonly HashSet<int> m_FiredWaves = new HashSet<int>();
        private Action m_PlayerDeathHandler;
        private TankBrain m_Player;
        private float m_Elapsed;
        private StageRunState m_State = StageRunState.Idle;
        private int m_Kills;

        /// <summary>Man bat dau chay.</summary>
        public event Action<StageDirector> StageStarted;

        /// <summary>Mot dot quai vua duoc kich hoat (index, du lieu dot).</summary>
        public event Action<int, StageDefinition.Wave> WaveStarted;

        /// <summary>So quai da diet thay doi.</summary>
        public event Action<int> KillCountChanged;

        /// <summary>Thang man, kem thuong.</summary>
        public event Action<StageDirector, StageRewardResult> StageCleared;

        /// <summary>Thua man, kem ly do.</summary>
        public event Action<StageDirector, string> StageFailed;

        public bool CoopMode
        {
            get => coopMode;
            set
            {
                coopMode = value;
                if (spawner != null) spawner.CoopMode = coopMode;
            }
        }

        public StageDefinition Definition => definition;
        public StageRunState State => m_State;
        public float Elapsed => m_Elapsed;
        public int Kills => m_Kills;
        public int AliveCount => spawner != null ? spawner.AliveCount : 0;
        public IReadOnlyList<StageObjective> Objectives => m_Objectives;
        public int WaveIndex => m_FiredWaves.Count;
        public int TotalWaves => definition != null && definition.Waves != null ? definition.Waves.Count : 0;
        public bool IsRunning => m_State == StageRunState.Running;

        private void Awake()
        {
            if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
            if (gameController == null) gameController = FindAnyObjectByType<GameController>();
        }

        private void Start()
        {
            if (autoStart && !TankBrain.NetworkSessionActive) Begin();
        }

        private void OnDestroy()
        {
            UnhookPlayer();
            UnhookSpawner();
        }

        /// <summary>Gan du lieu bang code (dung cho test hoac scene dung chung 1 director).</summary>
        public void Configure(StageDefinition stageDefinition, GameController controller, EnemySpawner enemySpawner,
            IEnumerable<SpawnZone> zones = null)
        {
            definition = stageDefinition;
            gameController = controller;
            spawner = enemySpawner;

            if (zones != null)
            {
                spawnZones.Clear();
                spawnZones.AddRange(zones);
            }
        }

        /// <summary>Bat dau man.</summary>
        public void Begin()
        {
            if (m_State == StageRunState.Running) return;

            if (definition == null)
            {
                Debug.LogError("[StageDirector] Chua gan StageDefinition, khong the bat dau man.", this);
                return;
            }

            // Nếu người chơi vào từ màn chọn chế độ thì đồng bộ CoopMode theo CoopEntry
            if (IronWasteland.UI.CoopEntry.IsCoop || TankBrain.NetworkSessionActive)
            {
                coopMode = true;
            }

            m_State = StageRunState.Running;
            m_Elapsed = 0f;
            m_Kills = 0;
            m_FiredWaves.Clear();
            m_Objectives.Clear();

            BuildObjectives();
            KillCountChanged?.Invoke(0);
            StageStarted?.Invoke(this);

            if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner != null)
            {
                spawner.CoopMode = coopMode;
                UnhookSpawner();
                spawner.EnemyDied += OnSpawnerEnemyDied;
            }
            else
            {
                Debug.LogWarning("[StageDirector] Khong co EnemySpawner, man se khong co quai.", this);
            }

            // Client trong phiên mạng không tự sinh quái hay tính thắng/thua, chỉ đồng bộ hiển thị từ host.
            if (TankBrain.NetworkSessionActive && !Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                return;
            }

            if (failOnPlayerDeath)
            {
                UnhookPlayer();
                m_PlayerDeathHandler = OnPlayerDied;
                if (TankBrain.NetworkSessionActive)
                {
                    TankBrain[] allTanks = FindObjectsByType<TankBrain>(FindObjectsInactive.Exclude);
                    for (int i = 0; i < allTanks.Length; i++)
                    {
                        if (allTanks[i] != null && allTanks[i].GetComponent<IronWasteland.Net.NetPlayerController>() != null)
                        {
                            allTanks[i].OnDeathEvent += m_PlayerDeathHandler;
                        }
                    }
                }
                else
                {
                    if (gameController == null) gameController = FindAnyObjectByType<GameController>();
                    m_Player = gameController != null ? gameController.PlayerTank : null;
                    if (m_Player != null)
                    {
                        m_Player.OnDeathEvent += m_PlayerDeathHandler;
                    }
                    else
                    {
                        Debug.LogWarning("[StageDirector] Khong tim thay tank nguoi choi (GameController.PlayerTank = null).", this);
                    }
                }
            }

            if (logEvents)
            {
                Debug.Log($"[StageDirector] Bat dau ai {definition.StageCode} - {definition.StageName} | {m_Objectives.Count} dieu kien, {TotalWaves} dot quai (CoopMode: {coopMode})");
            }
        }

        /// <summary>Dung man (chuyen scene / ve menu).</summary>
        public void Stop()
        {
            UnhookPlayer();
            UnhookSpawner();
            if (spawner != null) spawner.DespawnAll();
            m_State = StageRunState.Idle;
        }

        private void Update()
        {
            if (m_State != StageRunState.Running) return;

            // Client trong phiên mạng chỉ tick thời gian và điều kiện hiển thị HUD, không kích wave hay tự xử lý kết quả.
            if (TankBrain.NetworkSessionActive && !Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                m_Elapsed += Time.deltaTime;
                for (int i = 0; i < m_Objectives.Count; i++) m_Objectives[i].Tick(Time.deltaTime);
                return;
            }

            Tick(Time.deltaTime);
        }

        /// <summary>Cập nhật số mạng hạ từ host xuống client.</summary>
        public void SetKills(int kills)
        {
            m_Kills = kills;
            KillCountChanged?.Invoke(m_Kills);
        }

        /// <summary>Client ép kết thúc thắng từ sự kiện mạng.</summary>
        public void ForceClear()
        {
            Clear();
        }

        /// <summary>Client ép kết thúc thua từ sự kiện mạng.</summary>
        public void ForceFail(string reason)
        {
            Fail(reason);
        }

        /// <summary>
        /// Day thoi gian cho man. Tach khoi Update() de test headless va de sau nay
        /// dung Time.timeScale / pause khong phai sua logic.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (m_State != StageRunState.Running) return;

            m_Elapsed += deltaTime;

            FireDueWaves();

            for (int i = 0; i < m_Objectives.Count; i++) m_Objectives[i].Tick(deltaTime);

            EvaluateOutcome();
        }

        /// <summary>Kich hoat dot quai den gio (theo StartTime).</summary>
        private void FireDueWaves()
        {
            if (definition.Waves == null) return;

            for (int i = 0; i < definition.Waves.Count; i++)
            {
                if (m_FiredWaves.Contains(i)) continue;

                StageDefinition.Wave wave = definition.Waves[i];
                if (wave == null || m_Elapsed < wave.StartTime) continue;

                m_FiredWaves.Add(i);
                SpawnWave(wave);
                WaveStarted?.Invoke(i, wave);

                if (logEvents) Debug.Log($"[StageDirector] Dot {i + 1}/{TotalWaves} '{wave.Label}' - {wave.TotalCount} quai tai {m_Elapsed:0.0}s");
            }
        }

        private void SpawnWave(StageDefinition.Wave wave)
        {
            if (wave.Spawns == null) return;

            for (int i = 0; i < wave.Spawns.Count; i++)
            {
                StageDefinition.EnemySpawn spawn = wave.Spawns[i];
                if (spawn == null || spawn.Enemy == null) continue;

                SpawnZone zone = PickZone(spawn.ZoneId);
                int count = Mathf.Max(1, spawn.Count);
                for (int c = 0; c < count; c++)
                {
                    Vector2 position = zone != null ? zone.RandomPoint() : (Vector2)transform.position;
                    if (spawner == null) break;
                    spawner.Spawn(spawn.Enemy, position, spawn.SpeedMultiplier);
                }
            }
        }

        private SpawnZone PickZone(string zoneId)
        {
            if (spawnZones == null || spawnZones.Count == 0) return null;

            SpawnZone fallback = null;
            int matchCount = 0;
            for (int i = 0; i < spawnZones.Count; i++)
            {
                SpawnZone zone = spawnZones[i];
                if (zone == null) continue;
                if (fallback == null) fallback = zone;
                if (zone.Matches(zoneId)) matchCount++;
            }

            if (matchCount == 0) return fallback;

            int pick = UnityEngine.Random.Range(0, matchCount);
            for (int i = 0; i < spawnZones.Count; i++)
            {
                SpawnZone zone = spawnZones[i];
                if (zone == null || !zone.Matches(zoneId)) continue;
                if (pick-- == 0) return zone;
            }
            return fallback;
        }

        private void BuildObjectives()
        {
            m_Objectives.Clear();

            IReadOnlyList<StageDefinition.ObjectiveSpec> specs = definition.Objectives;
            if (specs != null)
            {
                for (int i = 0; i < specs.Count; i++)
                {
                    StageDefinition.ObjectiveSpec spec = specs[i];
                    if (spec == null) continue;

                    switch (spec.Type)
                    {
                        case StageObjectiveType.Eliminate:
                            m_Objectives.Add(new EliminateObjective(spec.Count));
                            break;
                        case StageObjectiveType.Survive:
                            m_Objectives.Add(new SurviveObjective(spec.Duration));
                            break;
                        case StageObjectiveType.DestroyTargets:
                            m_Objectives.Add(new DestroyTargetsObjective(spec.TargetId, spec.Count));
                            break;
                        case StageObjectiveType.Escort:
                            m_Objectives.Add(new EscortObjective(spec.TargetId, spec.Distance));
                            break;
                        case StageObjectiveType.Radar:
                            m_Objectives.Add(new RadarObjective(spec.Count));
                            break;
                    }
                }
            }

            // Ai cu khong khai bao objective -> mac dinh diet du KillTarget.
            if (m_Objectives.Count == 0 && definition.KillTarget > 0)
            {
                m_Objectives.Add(new EliminateObjective(definition.KillTarget));
            }
        }

        private void EvaluateOutcome()
        {
            for (int i = 0; i < m_Objectives.Count; i++)
            {
                StageObjective objective = m_Objectives[i];
                if (objective.IsFailed)
                {
                    Fail(objective.FailReason ?? "Không đạt điều kiện màn");
                    return;
                }
            }

            if (m_Objectives.Count == 0) return;

            bool allComplete = true;
            for (int i = 0; i < m_Objectives.Count; i++)
            {
                if (!m_Objectives[i].IsComplete)
                {
                    allComplete = false;
                    break;
                }
            }

            if (allComplete) Clear();
        }

        private void Clear()
        {
            if (m_State != StageRunState.Running) return;
            m_State = StageRunState.Cleared;

            StageRewardResult reward = StageReward.Roll(definition);
            UnhookPlayer();
            UnhookSpawner();
            if (spawner != null) spawner.DespawnAll();

            if (logEvents) Debug.Log($"[StageDirector] THANG ai {definition.StageCode} sau {m_Elapsed:0.0}s - {reward}");

            StageCleared?.Invoke(this, reward);
        }

        private void Fail(string reason)
        {
            if (m_State != StageRunState.Running) return;
            m_State = StageRunState.Failed;

            UnhookPlayer();
            UnhookSpawner();
            if (spawner != null) spawner.DespawnAll();

            if (logEvents) Debug.Log($"[StageDirector] THUA ai {definition.StageCode}: {reason}");

            StageFailed?.Invoke(this, reason);
        }

        #region Su kien tu ben ngoai (vat the muc tieu, xe ho tong, radar)

        /// <summary>Vat the muc tieu (Cum Nam Me, Tru Dien Truong...) vua bi pha huy.</summary>
        public void ReportTargetDestroyed(string targetId)
        {
            for (int i = 0; i < m_Objectives.Count; i++) m_Objectives[i].OnTargetDestroyed(targetId);
        }

        /// <summary>Tram radar vua duoc kich hoat.</summary>
        public void ReportRadarActivated(string radarId)
        {
            for (int i = 0; i < m_Objectives.Count; i++) m_Objectives[i].OnRadarActivated(radarId);
        }

        /// <summary>Xe ho tong bao da di duoc bao nhieu met.</summary>
        public void ReportEscortProgress(string escortId, float distance)
        {
            for (int i = 0; i < m_Objectives.Count; i++) m_Objectives[i].OnEscortProgress(escortId, distance);
        }

        /// <summary>Xe ho tong bi pha huy.</summary>
        public void ReportEscortDestroyed(string escortId)
        {
            for (int i = 0; i < m_Objectives.Count; i++) m_Objectives[i].OnEscortDestroyed(escortId);
        }

        #endregion

        private void OnSpawnerEnemyDied(EnemyBrain enemy)
        {
            if (m_State != StageRunState.Running) return;

            m_Kills++;
            KillCountChanged?.Invoke(m_Kills);

            for (int i = 0; i < m_Objectives.Count; i++) m_Objectives[i].OnEnemyKilled(enemy);
        }

        private void OnPlayerDied()
        {
            if (!failOnPlayerDeath) return;

            if (coopMode || TankBrain.NetworkSessionActive)
            {
                TankBrain[] allTanks = FindObjectsByType<TankBrain>(FindObjectsInactive.Exclude);
                for (int i = 0; i < allTanks.Length; i++)
                {
                    if (allTanks[i] != null && allTanks[i].IsAlive && allTanks[i].GetComponent<IronWasteland.Net.NetPlayerController>() != null)
                    {
                        return; // Vẫn còn đồng đội sống
                    }
                }
            }

            Fail("Toàn bộ xe của người chơi bị phá huỷ");
        }

        private void UnhookPlayer()
        {
            if (m_PlayerDeathHandler != null)
            {
                if (m_Player != null)
                {
                    m_Player.OnDeathEvent -= m_PlayerDeathHandler;
                }

                TankBrain[] allTanks = FindObjectsByType<TankBrain>(FindObjectsInactive.Exclude);
                for (int i = 0; i < allTanks.Length; i++)
                {
                    if (allTanks[i] != null)
                    {
                        allTanks[i].OnDeathEvent -= m_PlayerDeathHandler;
                    }
                }
            }
            m_PlayerDeathHandler = null;
        }

        private void UnhookSpawner()
        {
            if (spawner != null) spawner.EnemyDied -= OnSpawnerEnemyDied;
        }
    }
}
