using IronWasteland.Enemies;
using System;
using UnityEngine;

namespace IronWasteland.Stage
{
    /// <summary>
    /// Điều kiện thắng: Tiêu diệt đủ số lượng kẻ địch chỉ định trong màn chơi.
    /// Lắng nghe sự kiện tử trận từ EnemyBrain.OnAnyEnemyDied để cập nhật tiến trình tức thì (zero-polling).
    /// </summary>
    public class KillCountCondition : IStageCondition
    {
        /// <summary>Số lượng kẻ địch cần tiêu diệt để hoàn thành điều kiện.</summary>
        private int targetKills = 10;

        private int currentKills = 0;
        private bool isMet = false;
        private StageManager stageManager;

        public event Action<IStageCondition> OnConditionMet;
        public event Action<IStageCondition> OnProgressChanged;

        public KillCountCondition() { }

        public KillCountCondition(int targetKills)
        {
            this.targetKills = Mathf.Max(1, targetKills);
        }

        public string Description => $"Tiêu diệt {targetKills} kẻ địch";
        public bool IsMet => isMet;

        /// <summary>Điều kiện này chỉ dùng để giành chiến thắng, không bao giờ dùng để gác thất bại.</summary>
        public bool IsFailureOnly => false;
        public int CurrentKills => currentKills;
        public int TargetKills => targetKills;
        public float Progress => targetKills > 0 ? Mathf.Clamp01((float)currentKills / targetKills) : 1f;
        public string ProgressText => $"{currentKills}/{targetKills} Kẻ địch";

        public void Initialize(StageManager manager)
        {
            this.stageManager = manager;
            ResetCondition();

            EnemyBrain.OnAnyEnemyDied -= HandleEnemyDied;
            EnemyBrain.OnAnyEnemyDied += HandleEnemyDied;
        }

        public void UpdateCondition(float deltaTime)
        {
            // Điều kiện này chạy dựa trên sự kiện tử trận của quái vật, không cần polling theo frame.
        }

        public void ResetCondition()
        {
            currentKills = 0;
            isMet = false;
        }

        public void Cleanup()
        {
            EnemyBrain.OnAnyEnemyDied -= HandleEnemyDied;
            OnConditionMet = null;
            OnProgressChanged = null;
        }

        private void HandleEnemyDied(EnemyBrain enemy)
        {
            if (isMet) return;
            if (stageManager != null && stageManager.State != StageState.Playing) return;

            currentKills++;
            OnProgressChanged?.Invoke(this);

            if (currentKills >= targetKills)
            {
                isMet = true;
                OnConditionMet?.Invoke(this);
            }
        }
    }
}
