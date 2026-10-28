using System;
using UnityEngine;

namespace IronWasteland.Stage
{
    /// <summary>
    /// Điều kiện thắng: Sinh tồn trong một khoảng thời gian nhất định trong màn chơi.
    /// Tích lũy thời gian sinh tồn theo từng khung hình khi màn chơi đang ở trạng thái Playing.
    /// </summary>
    public class SurviveDurationCondition : IStageCondition
    {
        /// <summary>Khoảng thời gian người chơi cần sinh tồn để hoàn thành điều kiện (giây).</summary>
        private float requiredDuration = 60.0f;

        private float elapsedSurvivalTime = 0.0f;
        private bool isMet = false;
        private StageManager stageManager;

        public event Action<IStageCondition> OnConditionMet;
        public event Action<IStageCondition> OnProgressChanged;

        public SurviveDurationCondition() { }

        public SurviveDurationCondition(float duration)
        {
            this.requiredDuration = Mathf.Max(1.0f, duration);
        }

        public string Description => $"Sinh tồn trong {FormatTime(requiredDuration)}";
        public bool IsMet => isMet;

        /// <summary>Điều kiện này chỉ dùng để giành chiến thắng, không bao giờ dùng để gác thất bại.</summary>
        public bool IsFailureOnly => false;
        public float ElapsedSurvivalTime => elapsedSurvivalTime;
        public float RequiredDuration => requiredDuration;
        public float RemainingSurvivalTime => Mathf.Max(0f, requiredDuration - elapsedSurvivalTime);
        public float Progress => requiredDuration > 0f ? Mathf.Clamp01(elapsedSurvivalTime / requiredDuration) : 1f;
        public string ProgressText => $"{FormatTime(elapsedSurvivalTime)} / {FormatTime(requiredDuration)}";

        public void Initialize(StageManager manager)
        {
            this.stageManager = manager;
            ResetCondition();
        }

        public void UpdateCondition(float deltaTime)
        {
            if (isMet) return;
            if (stageManager != null && stageManager.State != StageState.Playing) return;

            elapsedSurvivalTime += deltaTime;
            OnProgressChanged?.Invoke(this);

            if (elapsedSurvivalTime >= requiredDuration)
            {
                elapsedSurvivalTime = requiredDuration;
                isMet = true;
                OnConditionMet?.Invoke(this);
            }
        }

        public void ResetCondition()
        {
            elapsedSurvivalTime = 0.0f;
            isMet = false;
        }

        public void Cleanup()
        {
            OnConditionMet = null;
            OnProgressChanged = null;
        }

        private static string FormatTime(float seconds)
        {
            int totalSec = Mathf.FloorToInt(Mathf.Max(0f, seconds));
            int min = totalSec / 60;
            int sec = totalSec % 60;
            return $"{min:00}:{sec:00}";
        }
    }
}
