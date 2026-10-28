using IronWasteland.Enemies;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Stage
{
    /// <summary>
    /// Điều kiện thắng: Tiêu diệt toàn bộ (hoặc đủ số lượng yêu cầu) các Boss Tinh Anh (EliteEnemyBrain) của màn chơi.
    /// Hỗ trợ màn có từ 1 đến nhiều Boss, kể cả Boss được sinh trễ trong trận (đăng ký qua RegisterBoss).
    /// Cung cấp tỷ lệ tổng lượng máu Boss còn lại cho thanh tiến trình UI (Boss HP Bar).
    /// </summary>
    public class DefeatBossCondition : IStageCondition
    {
        /// <summary>Danh sách toàn bộ Boss thuộc màn chơi (giữ cả Boss đã chết để cộng dồn máu tối đa).</summary>
        private readonly List<EliteEnemyBrain> bossTargets = new List<EliteEnemyBrain>();

        /// <summary>Số lượng Boss bắt buộc phải tiêu diệt. Bằng 0 nghĩa là phải diệt hết toàn bộ danh sách.</summary>
        private int requiredBossCount;

        private bool isMet = false;
        private StageManager stageManager;

        public event Action<IStageCondition> OnConditionMet;
        public event Action<IStageCondition> OnProgressChanged;

        public DefeatBossCondition() { }

        public DefeatBossCondition(IEnumerable<EliteEnemyBrain> bosses, int requiredCount = 0)
        {
            requiredBossCount = Mathf.Max(0, requiredCount);
            SetBossTargets(bosses);
        }

        /// <summary>Danh sách Boss của màn chơi (chỉ đọc).</summary>
        public IReadOnlyList<EliteEnemyBrain> BossTargets => bossTargets;

        /// <summary>Tổng số Boss đã được đăng ký cho màn chơi.</summary>
        public int TotalBosses => bossTargets.Count;

        /// <summary>Số Boss đã bị tiêu diệt.</summary>
        public int DefeatedBosses => CountDefeated();

        /// <summary>Số Boss thực tế cần tiêu diệt (đã quy đổi giá trị 0 thành "toàn bộ danh sách").</summary>
        public int RequiredBossCount => requiredBossCount > 0 ? requiredBossCount : bossTargets.Count;

        public string Description
        {
            get
            {
                if (bossTargets.Count == 1)
                {
                    EliteEnemyBrain boss = bossTargets[0];
                    string bossName = boss != null
                        ? (!string.IsNullOrEmpty(boss.DisplayName) ? boss.DisplayName : boss.name)
                        : "Boss Tinh Anh";
                    return $"Tiêu diệt {bossName}";
                }

                int required = RequiredBossCount;
                return required > 0 ? $"Tiêu diệt toàn bộ {required} Boss" : "Tiêu diệt toàn bộ Boss";
            }
        }

        /// <summary>
        /// Cờ trạng thái đã thắng. Chỉ được bật bên trong điều kiện này (qua <see cref="TryMarkMet"/>)
        /// để sự kiện OnConditionMet luôn phát đúng một lần, thay vì tính lại mỗi lần được hỏi.
        /// </summary>
        public bool IsMet => isMet;

        /// <summary>Điều kiện này chỉ dùng để giành chiến thắng, không bao giờ dùng để gác thất bại.</summary>
        public bool IsFailureOnly => false;

        public float Progress
        {
            get
            {
                ComputeHealth(out float currentHealth, out float maxHealth);
                if (maxHealth <= 0f) return IsMet ? 1.0f : 0.0f;

                // Tiến độ phản ánh phần trăm tổng lượng máu Boss đã bị người chơi rút cạn.
                return Mathf.Clamp01(1.0f - currentHealth / maxHealth);
            }
        }

        public string ProgressText
        {
            get
            {
                if (bossTargets.Count == 0) return "Chờ Boss xuất hiện...";

                int required = RequiredBossCount;
                int defeated = CountDefeated();
                if (defeated >= required) return "Đã tiêu diệt toàn bộ Boss";

                ComputeHealth(out float currentHealth, out float maxHealth);
                int currentHp = Mathf.CeilToInt(Mathf.Max(0f, currentHealth));
                int maxHp = Mathf.CeilToInt(maxHealth);

                return required > 1
                    ? $"Boss {defeated}/{required} · Máu {currentHp}/{maxHp}"
                    : $"Máu Boss: {currentHp}/{maxHp}";
            }
        }

        public void Initialize(StageManager manager)
        {
            this.stageManager = manager;
            ResetCondition();

            // Ưu tiên danh sách Boss do StageManager cung cấp.
            if (bossTargets.Count == 0 && manager != null)
            {
                SetBossTargets(manager.BossTargets);
            }

            // Nếu vẫn chưa có Boss, tự động tìm toàn bộ EliteEnemyBrain trong Scene.
            if (bossTargets.Count == 0)
            {
                SetBossTargets(UnityEngine.Object.FindObjectsByType<EliteEnemyBrain>());
            }
        }

        /// <summary>
        /// Thay thế toàn bộ danh sách Boss của màn chơi.
        /// </summary>
        public void SetBossTargets(IEnumerable<EliteEnemyBrain> bosses)
        {
            if (ReferenceEquals(bosses, bossTargets)) return;

            bossTargets.Clear();
            RegisterBosses(bosses);
        }

        /// <summary>
        /// Đăng ký thêm một Boss vào màn chơi (hữu ích khi Boss được sinh trễ từ Spawner).
        /// Bỏ qua nếu Boss null hoặc đã được đăng ký trước đó.
        /// </summary>
        public void RegisterBoss(EliteEnemyBrain boss)
        {
            if (boss == null || bossTargets.Contains(boss)) return;

            bossTargets.Add(boss);
            OnProgressChanged?.Invoke(this);
        }

        public void UpdateCondition(float deltaTime)
        {
            if (isMet) return;

            // Nhận danh sách Boss nếu StageManager vừa được cập nhật sau khi điều kiện đã khởi tạo.
            if (bossTargets.Count == 0 && stageManager != null && stageManager.BossTargets.Count > 0)
            {
                SetBossTargets(stageManager.BossTargets);
            }

            TryMarkMet();
        }

        public void ResetCondition()
        {
            isMet = false;
        }

        /// <summary>
        /// Chốt trạng thái thắng ngay khi đủ số Boss yêu cầu và phát sự kiện đúng một lần.
        /// Chưa có Boss nào thì không bao giờ chốt, tránh thắng ngay từ frame đầu.
        /// </summary>
        private void TryMarkMet()
        {
            if (isMet) return;

            if (bossTargets.Count == 0 || CountDefeated() < RequiredBossCount)
            {
                OnProgressChanged?.Invoke(this);
                return;
            }

            isMet = true;
            OnProgressChanged?.Invoke(this);
            OnConditionMet?.Invoke(this);
        }

        public void Cleanup()
        {
            bossTargets.Clear();
            OnConditionMet = null;
            OnProgressChanged = null;
        }

        /// <summary>Đăng ký hàng loạt Boss, tự bỏ qua phần tử null hoặc trùng lặp.</summary>
        private void RegisterBosses(IEnumerable<EliteEnemyBrain> bosses)
        {
            if (bosses == null) return;

            foreach (EliteEnemyBrain boss in bosses)
            {
                // So sánh theo THAM CHIẾU, không dùng Contains/== của Unity: Boss đã bị hủy khỏi Scene
                // sẽ "bằng null" với toán tử == và "bằng nhau" với mọi Boss đã hủy khác, khiến danh sách
                // bị rút gọn và màn chơi không bao giờ đủ số Boss yêu cầu. Boss đã hủy vẫn phải được
                // GIỮ LẠI vì nó được tính là đã bị tiêu diệt (xem CountDefeated).
                if (ReferenceEquals(boss, null)) continue;

                bool daCo = false;
                for (int i = 0; i < bossTargets.Count; i++)
                {
                    if (ReferenceEquals(bossTargets[i], boss))
                    {
                        daCo = true;
                        break;
                    }
                }

                if (!daCo) bossTargets.Add(boss);
            }
        }

        /// <summary>Đếm số Boss đã bị tiêu diệt (Boss bị hủy khỏi Scene cũng coi như đã chết).</summary>
        private int CountDefeated()
        {
            int defeated = 0;
            for (int i = 0; i < bossTargets.Count; i++)
            {
                EliteEnemyBrain boss = bossTargets[i];
                if (boss == null || !boss.IsAlive)
                {
                    defeated++;
                }
            }

            return defeated;
        }

        /// <summary>Tính tổng máu hiện tại và tổng máu tối đa của toàn bộ Boss trong màn.</summary>
        private void ComputeHealth(out float currentHealth, out float maxHealth)
        {
            currentHealth = 0f;
            maxHealth = 0f;

            for (int i = 0; i < bossTargets.Count; i++)
            {
                EliteEnemyBrain boss = bossTargets[i];
                if (boss == null) continue;

                maxHealth += boss.MaxHealth;
                if (boss.IsAlive)
                {
                    currentHealth += Mathf.Max(0f, boss.CurrentHealth);
                }
            }
        }
    }
}