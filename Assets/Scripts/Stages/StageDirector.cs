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
    /// Dieu phoi 1 man choi: doc <see cref="StageDefinition"/> (dieu kien thang/thua,
    /// thuong), ra lenh cho <see cref="EnemySpawner"/> chay lich dot quai, theo doi cac
    /// <see cref="StageObjective"/> va quyet dinh thang / thua.
    ///
    /// TU 2026-11: thiet ke dot quai (dot nao, luc nao, quai gi, roi o vung nao) do
    /// <see cref="EnemySpawner"/> trong scene so huu — StageDirector khong con doc
    /// Waves tu asset nua. Muon sua dot quai thi chon object "EnemySpawner" trong scene.
    ///
    /// Cach dung trong scene:
    /// 1. GameObject "StageDirector" -> gan component nay.
    /// 2. Keo asset StageDefinition (vi du Stage_1_1.asset) vao o Definition.
    /// 3. Keo GameObject co GameController cua tank vao o Game Controller.
    /// 4. Keo GameObject co EnemySpawner vao o Spawner.
    /// 5. Tren EnemySpawner: khai bao Waves + keo cac SpawnZone vao danh sach cua no.
    /// </summary>
    [DisallowMultipleComponent]
    public class StageDirector : MonoBehaviour
    {
        [Header("Dữ liệu màn")]
        [SerializeField] private StageDefinition definition;

        [Tooltip("GameController đang giữ tank của người chơi trong scene này.")]
        [SerializeField] private GameController gameController;

        [Header("Sinh quái")]
        [Tooltip("Spawner sở hữu lịch đợt quái và các vùng spawn của màn này.")]
        [SerializeField] private EnemySpawner spawner;

        [Header("Điều kiện thắng của màn")]
        [Tooltip("Số quái phải tiêu diệt (chỉ dùng khi danh sách Objectives rỗng, hoặc hiển thị HUD).")]
        [SerializeField, Min(0)] private int killTarget = 40;

        [Tooltip("Thời lượng dự kiến (giây) - dùng để hiển thị, không phải điều kiện thua.")]
        [SerializeField, Min(1f)] private float timeLimitSeconds = 90f;

        [Tooltip("Danh sách điều kiện phải thỏa HẾT để thắng. Rỗng = dùng killTarget.")]
        [SerializeField] private List<StageObjectiveSpec> objectives = new List<StageObjectiveSpec>();

        [Header("Tuỳ chọn")]
        [Tooltip("Tự động bắt đầu khi scene chạy.")]
        [SerializeField] private bool autoStart = true;

        [Tooltip("Co-op: nhân đôi máu quái.")]
        [SerializeField] private bool coopMode = false;

        [Tooltip("Tank chết là thua màn.")]
        [SerializeField] private bool failOnPlayerDeath = true;

        [SerializeField] private bool logEvents = true;

        private readonly List<StageObjective> m_Objectives = new List<StageObjective>();
        private Action m_PlayerDeathHandler;
        private TankBrain m_Player;
        private float m_Elapsed;
        private StageRunState m_State = StageRunState.Idle;
        private int m_Kills;

        /// <summary>Man bat dau chay.</summary>
        public event Action<StageDirector> StageStarted;

        /// <summary>Mot dot quai vua duoc kich hoat (index, du lieu dot do EnemySpawner so huu).</summary>
        public event Action<int, EnemyWave> WaveStarted;

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
        public int KillTarget => killTarget;
        public float TimeLimitSeconds => timeLimitSeconds;
        public IReadOnlyList<StageObjectiveSpec> ObjectiveSpecs => objectives;
        public int AliveCount => spawner != null ? spawner.AliveCount : 0;
        public IReadOnlyList<StageObjective> Objectives => m_Objectives;
        public int WaveIndex => spawner != null ? spawner.WaveIndex : -1;
        public int TotalWaves => spawner != null ? spawner.TotalWaves : 0;

        /// <summary>Số đợt đã bắt đầu — đúng thứ tự thời gian, dùng cho HUD.</summary>
        public int WavesFired => spawner != null ? spawner.WavesFired : 0;
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

            // Vung spawn gio thuoc ve EnemySpawner — director chi chuyen tiep de API cu van dung duoc.
            if (zones != null && spawner != null)
            {
                spawner.ConfigureDesign(null, zones);
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
                spawner.WaveStarted += OnSpawnerWaveStarted;
            }
            else
            {
                Debug.LogWarning("[StageDirector] Khong co EnemySpawner, man se khong co quai.", this);
            }

            // Client trong phiên mạng không tự sinh quái hay tính thắng/thua, chỉ đồng bộ hiển thị từ host.
            if (TankBrain.NetworkSessionActive && Unity.Netcode.NetworkManager.Singleton != null &&
                !Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                return;
            }

            // Thiết kế wave nằm trên spawner; giờ mới bắt đầu chạy lịch.
            if (spawner != null) spawner.BeginWaves();

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
            if (spawner != null)
            {
                spawner.StopWaves();
                spawner.DespawnAll();
            }
            m_State = StageRunState.Idle;
        }

        private void Update()
        {
            if (m_State != StageRunState.Running) return;

            // Client trong phiên mạng chỉ tick thời gian và điều kiện hiển thị HUD, không kích wave hay tự xử lý kết quả.
            if (TankBrain.NetworkSessionActive && Unity.Netcode.NetworkManager.Singleton != null &&
                !Unity.Netcode.NetworkManager.Singleton.IsServer)
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
            ForceClear(StageReward.Roll(definition));
        }

        /// <summary>Client ép kết thúc thắng từ sự kiện mạng với phần thưởng Host đã roll.</summary>
        public void ForceClear(StageRewardResult reward)
        {
            if (m_State == StageRunState.Cleared) return;
            m_State = StageRunState.Cleared;

            UnhookPlayer();
            UnhookSpawner();
            if (spawner != null) spawner.DespawnAll();

            if (logEvents) Debug.Log($"[StageDirector] THANG ai {definition?.StageCode} sau {m_Elapsed:0.0}s - {reward}");

            StageCleared?.Invoke(this, reward);
        }

        /// <summary>Client ép kết thúc thua từ sự kiện mạng.</summary>
        public void ForceFail(string reason)
        {
            if (m_State == StageRunState.Cleared || m_State == StageRunState.Failed) return;
            m_State = StageRunState.Failed;

            UnhookPlayer();
            UnhookSpawner();
            if (spawner != null) spawner.DespawnAll();

            if (logEvents) Debug.Log($"[StageDirector] THUA ai {definition?.StageCode}: {reason}");

            StageFailed?.Invoke(this, reason);
        }

        /// <summary>
        /// Day thoi gian cho man. Tach khoi Update() de test headless va de sau nay
        /// dung Time.timeScale / pause khong phai sua logic.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (m_State != StageRunState.Running) return;

            m_Elapsed += deltaTime;

            // Spawner tự quyết định đợt nào tới giờ; director chỉ đẩy đồng hồ.
            if (spawner != null) spawner.TickWaves(deltaTime);

            for (int i = 0; i < m_Objectives.Count; i++) m_Objectives[i].Tick(deltaTime);

            EvaluateOutcome();
        }

        private void OnSpawnerWaveStarted(int index, EnemyWave wave)
        {
            WaveStarted?.Invoke(index, wave);
        }

        private void BuildObjectives()
        {
            m_Objectives.Clear();

            IReadOnlyList<StageObjectiveSpec> specs = objectives;
            if (specs != null)
            {
                for (int i = 0; i < specs.Count; i++)
                {
                    StageObjectiveSpec spec = specs[i];
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
            if (m_Objectives.Count == 0 && killTarget > 0)
            {
                m_Objectives.Add(new EliminateObjective(killTarget));
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
            if (spawner != null)
            {
                spawner.StopWaves();
                spawner.DespawnAll();
            }

            if (logEvents) Debug.Log($"[StageDirector] THANG ai {definition.StageCode} sau {m_Elapsed:0.0}s - {reward}");

            StageCleared?.Invoke(this, reward);
        }

        private void Fail(string reason)
        {
            if (m_State != StageRunState.Running) return;
            m_State = StageRunState.Failed;

            UnhookPlayer();
            UnhookSpawner();
            if (spawner != null)
            {
                spawner.StopWaves();
                spawner.DespawnAll();
            }

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
            if (spawner == null) return;
            spawner.EnemyDied -= OnSpawnerEnemyDied;
            spawner.WaveStarted -= OnSpawnerWaveStarted;
        }
    }
}
