using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Stages
{
    /// <summary>
    /// Thông tin metadata của 1 màn chơi (1 ải): mã ải, tên, mô tả điều kiện thắng và phần thưởng.
    ///
    /// Thiết kế các đợt quái (wave) nằm trên <see cref="EnemySpawner"/>, còn điều kiện thắng
    /// (<see cref="StageObjectiveSpec"/>, killTarget, timeLimitSeconds) nằm trên
    /// <see cref="StageDirector"/> ngay trong scene của màn đó.
    ///
    /// Designer tạo asset từ menu chuột phải: Create > Iron Wasteland > Stage > Stage Definition.
    /// </summary>
    [CreateAssetMenu(fileName = "Stage_New", menuName = "Iron Wasteland/Stage/Stage Definition")]
    public class StageDefinition : ScriptableObject
    {
        [Header("Thông tin ải")]
        [Tooltip("Mã ải, ví dụ 1-1.")]
        [SerializeField] private string stageCode = "1-1";

        [Tooltip("Tên ải, ví dụ Sương Mù Tím.")]
        [SerializeField] private string stageName = "Ải mới";

        [Header("Mô tả hiển thị")]
        [Tooltip("Mô tả điều kiện thắng hiển thị trên UI ngoài trận (chọn ải / sảnh Co-op).")]
        [SerializeField, TextArea(1, 3)] private string winConditionDescription = string.Empty;

        [Header("Thưởng")]
        [Tooltip("Ngọc nhận được khi thắng (theo bảng ải trong GDD).")]
        [SerializeField, Min(0)] private int rewardGems = 20;

        [Tooltip("Số món phụ kiện tối đa rơi ra.")]
        [SerializeField, Min(0)] private int rewardAccessoryCount = 0;

        [Tooltip("Boss màn: chắc chắn đủ số món, không roll 20%.")]
        [SerializeField] private bool guaranteeAccessories = false;

        [Tooltip("Danh sách index phụ kiện (0..19) có thể rơi trong màn này theo GDD.")]
        [SerializeField] private List<int> rewardAccessoryPool = new List<int>();

        [Tooltip("Mã ải tiếp theo, ví dụ 1-2 (rỗng = không tự chuyển).")]
        [SerializeField] private string nextStageCode = string.Empty;

        public string StageCode => stageCode;
        public string StageName => stageName;

        /// <summary>Mô tả điều kiện thắng để hiển thị ở UI ngoài trận.</summary>
        public string WinConditionDescription => winConditionDescription;

        public int RewardGems => rewardGems;
        public int RewardAccessoryCount => rewardAccessoryCount;
        public bool GuaranteeAccessories => guaranteeAccessories;
        public IReadOnlyList<int> RewardAccessoryPool => rewardAccessoryPool;
        public string NextStageCode => nextStageCode;
    }
}
