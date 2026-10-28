using IronWasteland.Tanks;
using System;
using UnityEngine;

namespace IronWasteland.Stage
{
    /// <summary>
    /// Điều kiện thắng: Bảo vệ một mục tiêu chỉ định (xe tăng VIP, công trình hoặc đồng minh).
    /// Nếu mục tiêu bị phá hủy, màn chơi lập tức thất bại (DefendTargetDestroyed).
    /// Nếu mục tiêu sống sót qua hết khoảng thời gian yêu cầu, điều kiện được hoàn thành.
    /// </summary>
    public class DefendTargetCondition : IStageCondition
    {
        /// <summary>Mục tiêu xe tăng/công trình cần bảo vệ (nếu để trống sẽ nhận từ StageManager lúc khởi chạy).</summary>
        private TankBrain defendTarget;

        /// <summary>Thời gian cần bảo vệ mục tiêu sống sót (giây). Đặt &lt;= 0 nếu chỉ cần gác thất bại.</summary>
        private float defendDuration = 60.0f;

        private float elapsedDefendTime = 0.0f;
        private bool isMet = false;
        private StageManager stageManager;

        public event Action<IStageCondition> OnConditionMet;
        public event Action<IStageCondition> OnProgressChanged;

        public DefendTargetCondition() { }

        public DefendTargetCondition(TankBrain target, float duration = 0f)
        {
            this.defendTarget = target;
            this.defendDuration = Mathf.Max(0f, duration);
        }

        public string Description
        {
            get
            {
                string targetName = defendTarget != null ? defendTarget.name : "Mục tiêu";
                return defendDuration > 0f
                    ? $"Bảo vệ {targetName} trong {FormatTime(defendDuration)}"
                    : $"Bảo vệ {targetName} an toàn";
            }
        }

        public bool IsMet => isMet;

        /// <summary>
        /// True khi cấu hình không giới hạn thời gian bảo vệ (defendDuration &lt;= 0):
        /// điều kiện chỉ còn tác dụng gác thất bại, không phải một mục tiêu để hoàn thành.
        /// </summary>
        public bool IsFailureOnly => defendDuration <= 0f;

        public TankBrain DefendTarget => defendTarget;
        public float DefendDuration => defendDuration;
        public float ElapsedDefendTime => elapsedDefendTime;

        public float Progress
        {
            get
            {
                if (defendDuration > 0f)
                {
                    return Mathf.Clamp01(elapsedDefendTime / defendDuration);
                }
                return (defendTarget != null && defendTarget.IsAlive) ? 1.0f : 0.0f;
            }
        }

        public string ProgressText
        {
            get
            {
                if (defendTarget == null || !defendTarget.IsAlive)
                {
                    return "Mục tiêu bị tiêu diệt";
                }

                if (defendDuration > 0f)
                {
                    return $"{FormatTime(elapsedDefendTime)} / {FormatTime(defendDuration)} (Máu: {Mathf.CeilToInt(defendTarget.CurrentHealth)})";
                }

                return $"Đang bảo vệ (Máu: {Mathf.CeilToInt(defendTarget.CurrentHealth)})";
            }
        }

        public void Initialize(StageManager manager)
        {
            this.stageManager = manager;
            if (this.defendTarget == null && manager != null)
            {
                this.defendTarget = manager.DefendTarget;
            }

            ResetCondition();

            if (defendTarget != null)
            {
                defendTarget.OnDied -= HandleTargetDied;
                defendTarget.OnDied += HandleTargetDied;
            }
        }

        public void UpdateCondition(float deltaTime)
        {
            if (isMet) return;
            if (stageManager != null && stageManager.State != StageState.Playing) return;

            // Nếu chưa gán mục tiêu, thử lấy lại từ StageManager nếu vừa được khởi tạo trễ
            if (defendTarget == null && stageManager != null && stageManager.DefendTarget != null)
            {
                defendTarget = stageManager.DefendTarget;
                defendTarget.OnDied -= HandleTargetDied;
                defendTarget.OnDied += HandleTargetDied;
            }

            // Kiểm tra trạng thái sống còn của mục tiêu (chỉ kích hoạt thất bại khi mục tiêu hợp lệ bị tiêu diệt)
            if (defendTarget != null && !defendTarget.IsAlive)
            {
                HandleTargetDied(defendTarget);
                return;
            }

            if (defendDuration > 0f)
            {
                elapsedDefendTime += deltaTime;
                OnProgressChanged?.Invoke(this);

                if (elapsedDefendTime >= defendDuration)
                {
                    elapsedDefendTime = defendDuration;
                    isMet = true;
                    OnConditionMet?.Invoke(this);
                }
            }
            else
            {
                OnProgressChanged?.Invoke(this);
            }
        }

        public void ResetCondition()
        {
            elapsedDefendTime = 0.0f;
            isMet = false;
        }

        public void Cleanup()
        {
            if (defendTarget != null)
            {
                defendTarget.OnDied -= HandleTargetDied;
            }
            OnConditionMet = null;
            OnProgressChanged = null;
        }

        private void HandleTargetDied(TankBrain deadTarget)
        {
            if (stageManager != null && stageManager.State == StageState.Playing)
            {
                stageManager.FailStage(StageDefeatReason.DefendTargetDestroyed);
            }
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
