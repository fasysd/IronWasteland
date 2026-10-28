using IronWasteland.Enemies;
using IronWasteland.Tanks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Stage
{
    /// <summary>
    /// Bộ quản lý trung tâm điều phối toàn bộ vòng đời và tiến trình của một màn chơi (StageManager).
    /// Quản lý máy trạng thái FSM (NotStarted, Playing, Paused, Victory, Defeat), đồng hồ đếm giờ,
    /// các điều kiện thắng/thua và phát sự kiện độc lập cho hệ thống UI/HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public class StageManager : MonoBehaviour
    {
        public static StageManager Instance { get; private set; }

        [Header("Cấu Hình Màn Chơi (Stage Setup)")]
        [Tooltip("Bảng dữ liệu StageDefinition chứa luật chơi và điều kiện của màn này.")]
        [SerializeField] private StageDefinition stageDefinition;

        [Tooltip("Tham chiếu tới xe tăng người chơi (tùy chọn: tự tìm nếu để trống).")]
        [SerializeField] private TankBrain playerTank;

        [Tooltip("Tham chiếu tới mục tiêu xe tăng/công trình cần bảo vệ (nếu là màn bảo vệ).")]
        [SerializeField] private TankBrain defendTarget;

        [Tooltip("Danh sách Boss Tinh Anh (EliteEnemyBrain) của màn chơi (nếu là màn đấu Boss).\nCó thể bổ sung Boss mới trong trận bằng RegisterBoss().")]
        [SerializeField] private List<EliteEnemyBrain> bossTargets = new List<EliteEnemyBrain>();

        [Tooltip("Tự động bắt đầu màn chơi ngay khi hàm Start() được gọi.")]
        [SerializeField] private bool autoStartOnStart = true;

        [Tooltip("Có tự động điều chỉnh Time.timeScale khi Pause/Resume màn chơi hay không.")]
        [SerializeField] private bool manageTimeScaleOnPause = true;

        [Tooltip("Có tự động đóng băng thời gian (Time.timeScale = 0) khi màn chơi Kết Thúc (Chiến Thắng / Thất Bại) hay không.")]
        [SerializeField] private bool freezeTimeOnStageEnd = true;

        // Trạng thái nội bộ (Runtime State)
        private StageState state = StageState.NotStarted;
        private StageDefeatReason defeatReason = StageDefeatReason.None;
        private float elapsedTime = 0.0f;
        private float remainingTime = 0.0f;
        private readonly List<IStageCondition> activeConditions = new List<IStageCondition>();

        // Cờ nội bộ: chỉ cảnh báo một lần để không làm ngập Console mỗi frame.
        private bool warnedMissingPlayer = false;

        // Sự kiện công khai (Events C#)
        public event Action<StageState> OnStateChanged;
        public event Action<float, float> OnTimerUpdated; // (elapsedTime, remainingTime)
        public event Action<IStageCondition> OnConditionMet;
        public event Action<IStageCondition> OnConditionProgressChanged;
        public event Action OnStageVictory;
        public event Action<StageDefeatReason> OnStageDefeat;

        // Thuộc tính công khai (Read-only Getters)
        public StageState State => state;
        public StageDefeatReason DefeatReason => defeatReason;
        public StageDefinition CurrentDefinition => stageDefinition;
        public TankBrain PlayerTank => playerTank;
        public TankBrain DefendTarget => defendTarget;
        public EliteEnemyBrain BossTarget => bossTargets.Count > 0 ? bossTargets[0] : null;
        public IReadOnlyList<EliteEnemyBrain> BossTargets => bossTargets;
        public float ElapsedTime => elapsedTime;
        public float RemainingTime => remainingTime;
        public IReadOnlyList<IStageCondition> ActiveConditions => activeConditions;

        public bool IsPlaying => state == StageState.Playing;
        public bool IsCompleted => state == StageState.Victory || state == StageState.Defeat;
        public bool ManageTimeScaleOnPause => manageTimeScaleOnPause;
        public bool FreezeTimeOnStageEnd => freezeTimeOnStageEnd;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Debug.LogWarning($"[{nameof(StageManager)}] Đã tồn tại một instance khác trong scene. Hủy component trùng lặp trên {gameObject.name}.");
                enabled = false;
                Destroy(this);
                return;
            }
        }

        private void Start()
        {
            if (Instance != this) return;

            if (autoStartOnStart && state == StageState.NotStarted)
            {
                StartStage(stageDefinition, playerTank, defendTarget, bossTargets);
            }
        }

        private void Update()
        {
            if (state != StageState.Playing) return;

            // Kiểm tra an toàn: Người chơi bị tiêu diệt hoặc GameObject bị hủy
            if (playerTank == null)
            {
                // Chưa gán / chưa kịp khởi tạo: cảnh báo một lần rồi chờ, KHÔNG kết liễu màn chơi.
                if (!warnedMissingPlayer)
                {
                    warnedMissingPlayer = true;
                    Debug.LogWarning($"[{nameof(StageManager)}] Chưa có TankBrain của người chơi. " +
                                     "Hãy gán playerTank trên Inspector hoặc thêm GameController vào Scene.", this);
                }
                return;
            }

            if (!playerTank.IsAlive)
            {
                FailStage(StageDefeatReason.PlayerDied);
                return;
            }

            float dt = Time.deltaTime;
            elapsedTime += dt;

            // 1. Cập nhật các điều kiện dựa trên thời gian TRƯỚC khi tính hết giờ
            for (int i = 0; i < activeConditions.Count; i++)
            {
                var cond = activeConditions[i];
                if (cond != null && !cond.IsMet)
                {
                    cond.UpdateCondition(dt);
                }
            }

            // Nếu việc cập nhật điều kiện đã dẫn tới kết thúc màn chơi, dừng cập nhật
            if (state != StageState.Playing) return;

            // 2. Quản lý thời gian giới hạn (nếu có)
            if (stageDefinition != null && stageDefinition.HasTimeLimit)
            {
                remainingTime = Mathf.Max(0f, remainingTime - dt);
                OnTimerUpdated?.Invoke(elapsedTime, remainingTime);

                if (remainingTime <= 0f)
                {
                    FailStage(StageDefeatReason.TimeExpired);
                    return;
                }
            }
            else
            {
                OnTimerUpdated?.Invoke(elapsedTime, 0f);
            }
        }

        /// <summary>
        /// Bắt đầu một màn chơi với cấu hình và các tham chiếu chỉ định.
        /// </summary>
        public void StartStage(StageDefinition definition = null, TankBrain player = null, TankBrain vip = null, IReadOnlyList<EliteEnemyBrain> bosses = null)
        {
            // Đảm bảo thời gian chạy bình thường khi bắt đầu màn
            if (Time.timeScale == 0f)
            {
                Time.timeScale = 1f;
            }

            // Hủy đăng ký từ người chơi cũ nếu có
            if (playerTank != null)
            {
                playerTank.OnDied -= HandlePlayerDied;
            }

            if (definition != null) this.stageDefinition = definition;
            if (player != null) this.playerTank = player;
            if (vip != null) this.defendTarget = vip;
            if (bosses != null && bosses.Count > 0)
            {
                // Sao chép sang danh sách tạm trước khi xóa: RestartStage() và Start() truyền
                // chính bossTargets vào đây, nếu xóa trước khi đọc thì danh sách sẽ rỗng.
                var incomingBosses = new List<EliteEnemyBrain>(bosses);
                bossTargets.Clear();
                for (int i = 0; i < incomingBosses.Count; i++)
                {
                    if (incomingBosses[i] != null && !bossTargets.Contains(incomingBosses[i]))
                    {
                        bossTargets.Add(incomingBosses[i]);
                    }
                }
            }

            // Nếu chưa có playerTank, thử tìm từ GameController hoặc trong Scene
            if (playerTank == null)
            {
                var gameController = FindAnyObjectByType<GameController>();
                if (gameController != null && gameController.PlayerTank != null && gameController.PlayerTank != defendTarget)
                {
                    playerTank = gameController.PlayerTank;
                }
                else
                {
                    var allTanks = FindObjectsByType<TankBrain>();
                    for (int i = 0; i < allTanks.Length; i++)
                    {
                        if (allTanks[i] != defendTarget)
                        {
                            playerTank = allTanks[i];
                            break;
                        }
                    }
                }
            }

            // Tự tìm toàn bộ Boss Tinh Anh trong Scene nếu chưa được gán và màn chơi có yêu cầu Boss
            if (bossTargets.Count == 0 && stageDefinition != null && (stageDefinition.IsBossStage || stageDefinition.EnableDefeatBossCondition))
            {
                var sceneBosses = FindObjectsByType<EliteEnemyBrain>();
                for (int i = 0; i < sceneBosses.Length; i++)
                {
                    if (sceneBosses[i] != null && !bossTargets.Contains(sceneBosses[i]))
                    {
                        bossTargets.Add(sceneBosses[i]);
                    }
                }
            }

            // Đăng ký sự kiện sống còn của người chơi.
            // Luôn đăng ký kể cả khi player trùng mục tiêu bảo vệ: nếu player chết thì vẫn phải kết thúc màn.
            if (playerTank != null)
            {
                playerTank.OnDied -= HandlePlayerDied;
                playerTank.OnDied += HandlePlayerDied;
            }

            // Dọn dẹp điều kiện cũ (nếu có)
            ClearActiveConditions();

            // Khởi tạo trạng thái ban đầu
            elapsedTime = 0.0f;
            remainingTime = stageDefinition != null ? stageDefinition.TimeLimit : 0.0f;
            defeatReason = StageDefeatReason.None;
            warnedMissingPlayer = false;

            // Sinh các điều kiện từ StageDefinition
            if (stageDefinition != null)
            {
                var createdConditions = stageDefinition.CreateConditions(defendTarget, bossTargets);
                for (int i = 0; i < createdConditions.Count; i++)
                {
                    AddCondition(createdConditions[i]);
                }
            }

            // Cảnh báo cấu hình thiếu (không chặn chạy, chỉ báo để Designer biết).
            if (activeConditions.Count == 0)
            {
                Debug.LogWarning($"[{nameof(StageManager)}] Màn '{(stageDefinition != null ? stageDefinition.StageName : "(chưa gán)")}' " +
                                 "không có điều kiện thắng nào → màn sẽ không bao giờ hoàn thành.", this);
            }

            if (stageDefinition != null && stageDefinition.Type == StageType.Boss && bossTargets.Count == 0)
            {
                Debug.LogWarning($"[{nameof(StageManager)}] Màn Boss nhưng chưa có Boss nào. " +
                                 "Hãy gán danh sách bossTargets, hoặc để DefeatBossCondition tự tìm EliteEnemyBrain trong Scene.", this);
            }

            // Đưa FSM về NotStarted trước khi vào Playing để OnStateChanged luôn phát,
            // kể cả khi khởi động lại một màn đang chạy (RestartStage).
            if (state != StageState.NotStarted)
            {
                SetState(StageState.NotStarted);
            }

            SetState(StageState.Playing);
            OnTimerUpdated?.Invoke(elapsedTime, remainingTime);
        }

        /// <summary>
        /// Thay thế toàn bộ danh sách Boss Tinh Anh của màn chơi (hữu ích khi Spawner sinh đủ Boss ngay từ đầu màn).
        /// </summary>
        public void SetBossTargets(IEnumerable<EliteEnemyBrain> bosses)
        {
            var incoming = new List<EliteEnemyBrain>();
            if (bosses != null)
            {
                foreach (EliteEnemyBrain boss in bosses)
                {
                    if (boss == null || incoming.Contains(boss)) continue;
                    incoming.Add(boss);
                }
            }

            bossTargets.Clear();
            bossTargets.AddRange(incoming);
            SyncBossTargetsToConditions();
        }

        /// <summary>
        /// Bổ sung một Boss Tinh Anh vào màn chơi đang chạy (hữu ích khi Boss được sinh trễ từ Spawner).
        /// </summary>
        public void RegisterBoss(EliteEnemyBrain boss)
        {
            if (boss == null || bossTargets.Contains(boss)) return;

            bossTargets.Add(boss);
            SyncBossTargetsToConditions();
        }

        /// <summary>
        /// Gán duy nhất một Boss Tinh Anh cho màn chơi (giữ tương thích với API cũ).
        /// </summary>
        public void SetBossTarget(EliteEnemyBrain boss)
        {
            SetBossTargets(boss != null ? new[] { boss } : null);
        }

        /// <summary>Đồng bộ danh sách Boss xuống mọi điều kiện tiêu diệt Boss đang hoạt động.</summary>
        private void SyncBossTargetsToConditions()
        {
            for (int i = 0; i < activeConditions.Count; i++)
            {
                if (activeConditions[i] is DefeatBossCondition bossCondition)
                {
                    bossCondition.SetBossTargets(bossTargets);
                }
            }
        }

        /// <summary>
        /// Thêm một điều kiện thắng mới vào danh sách điều kiện của màn chơi.
        /// Tự động liên kết (Initialize) và lắng nghe sự kiện từ điều kiện đó.
        /// </summary>
        public void AddCondition(IStageCondition condition)
        {
            if (condition == null || activeConditions.Contains(condition)) return;

            condition.Initialize(this);
            condition.OnConditionMet += HandleConditionMet;
            condition.OnProgressChanged += HandleConditionProgressChanged;
            activeConditions.Add(condition);
        }

        /// <summary>
        /// Tạm dừng màn chơi.
        /// </summary>
        public void PauseStage()
        {
            if (state == StageState.Playing)
            {
                SetState(StageState.Paused);
                if (manageTimeScaleOnPause)
                {
                    Time.timeScale = 0f;
                }
            }
        }

        /// <summary>
        /// Tiếp tục màn chơi sau khi tạm dừng.
        /// </summary>
        public void ResumeStage()
        {
            if (state == StageState.Paused)
            {
                if (manageTimeScaleOnPause)
                {
                    Time.timeScale = 1f;
                }
                SetState(StageState.Playing);
            }
        }

        /// <summary>
        /// Chơi lại màn chơi hiện tại từ đầu.
        /// </summary>
        public void RestartStage()
        {
            if (Time.timeScale == 0f)
            {
                Time.timeScale = 1f;
            }
            StartStage(stageDefinition, playerTank, defendTarget, bossTargets);
        }

        /// <summary>
        /// Kích hoạt trạng thái Chiến Thắng cho màn chơi.
        /// Tự động đóng băng thời gian (Time.timeScale = 0) nếu bật cờ freezeTimeOnStageEnd.
        /// </summary>
        public void CompleteStage()
        {
            if (state != StageState.Playing) return;

            SetState(StageState.Victory);
            OnStageVictory?.Invoke();

            if (freezeTimeOnStageEnd)
            {
                Time.timeScale = 0f;
            }
        }

        /// <summary>
        /// Kích hoạt trạng thái Thất Bại cho màn chơi với lý do cụ thể.
        /// Tự động đóng băng thời gian (Time.timeScale = 0) nếu bật cờ freezeTimeOnStageEnd.
        /// </summary>
        public void FailStage(StageDefeatReason reason)
        {
            if (state != StageState.Playing) return;

            this.defeatReason = reason;
            SetState(StageState.Defeat);
            OnStageDefeat?.Invoke(reason);

            if (freezeTimeOnStageEnd)
            {
                Time.timeScale = 0f;
            }
        }

        /// <summary>
        /// Người chơi chủ động đầu hàng, kết thúc màn chơi với lý do Surrendered.
        /// </summary>
        public void Surrender()
        {
            FailStage(StageDefeatReason.Surrendered);
        }

        private void SetState(StageState newState)
        {
            if (state == newState) return;

            state = newState;
            OnStateChanged?.Invoke(state);
        }

        private void HandlePlayerDied(TankBrain deadPlayer)
        {
            if (state == StageState.Playing)
            {
                FailStage(StageDefeatReason.PlayerDied);
            }
        }

        private void HandleConditionMet(IStageCondition condition)
        {
            OnConditionMet?.Invoke(condition);
            EvaluateWinConditions();
        }

        private void HandleConditionProgressChanged(IStageCondition condition)
        {
            OnConditionProgressChanged?.Invoke(condition);
        }

        /// <summary>
        /// Đánh giá xem toàn bộ điều kiện thắng đã được thỏa mãn hay chưa.
        /// </summary>
        public void EvaluateWinConditions()
        {
            if (state != StageState.Playing) return;
            if (activeConditions.Count == 0) return;

            // Nếu toàn bộ điều kiện đang bật chỉ để gác thất bại (ví dụ bảo vệ mục tiêu không giới hạn giờ),
            // thì màn chơi không có mục tiêu nào để hoàn thành → không thể thắng.
            bool hasWinObjective = false;
            for (int i = 0; i < activeConditions.Count; i++)
            {
                if (!activeConditions[i].IsFailureOnly)
                {
                    hasWinObjective = true;
                    break;
                }
            }
            if (!hasWinObjective) return;

            bool requireAll = stageDefinition == null || stageDefinition.RequireAllWinConditions;

            if (requireAll)
            {
                // Bắt buộc tất cả các điều kiện thắng đều phải hoàn thành (AND)
                for (int i = 0; i < activeConditions.Count; i++)
                {
                    var condition = activeConditions[i];
                    if (condition.IsFailureOnly) continue;
                    if (!condition.IsMet)
                    {
                        return;
                    }
                }
                CompleteStage();
            }
            else
            {
                // Chỉ cần đạt ít nhất một điều kiện bất kỳ (OR)
                for (int i = 0; i < activeConditions.Count; i++)
                {
                    var condition = activeConditions[i];
                    if (condition.IsFailureOnly) continue;
                    if (condition.IsMet)
                    {
                        CompleteStage();
                        return;
                    }
                }
            }
        }

        private void ClearActiveConditions()
        {
            for (int i = 0; i < activeConditions.Count; i++)
            {
                var cond = activeConditions[i];
                if (cond != null)
                {
                    cond.OnConditionMet -= HandleConditionMet;
                    cond.OnProgressChanged -= HandleConditionProgressChanged;
                    cond.Cleanup();
                }
            }
            activeConditions.Clear();
        }

        private void OnDisable()
        {
            if (Time.timeScale == 0f)
            {
                Time.timeScale = 1f;
            }
        }

        private void OnDestroy()
        {
            if (Time.timeScale == 0f)
            {
                Time.timeScale = 1f;
            }

            if (playerTank != null)
            {
                playerTank.OnDied -= HandlePlayerDied;
            }

            ClearActiveConditions();

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
