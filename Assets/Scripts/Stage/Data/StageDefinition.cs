using IronWasteland.Enemies;
using IronWasteland.Tanks;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Stage
{
    /// <summary>
    /// Bảng dữ liệu ScriptableObject định nghĩa toàn bộ thông số, luật chơi và điều kiện thắng của một màn chơi.
    /// Cho phép Game Designer thiết lập màn chơi trực tiếp từ Unity Editor mà không cần can thiệp code.
    /// </summary>
    [CreateAssetMenu(fileName = "StageDefinition_New", menuName = "IronWasteland/Stage/Stage Definition", order = 10)]
    public class StageDefinition : ScriptableObject
    {
        [Header("Thông Tin Màn Chơi (Stage Info)")]
        [Tooltip("Mã định danh duy nhất của màn chơi (ví dụ: 'stage_1_01', 'stage_1_05_boss').")]
        [SerializeField] private string stageId = "stage_1_01";

        [Tooltip("Tên hiển thị của màn chơi trên giao diện người dùng.")]
        [SerializeField] private string stageName = "Tiền Tuyến Sa Mạc";

        [Tooltip("Mô tả nhiệm vụ và bối cảnh cốt truyện của màn chơi.")]
        [TextArea(2, 4)]
        [SerializeField] private string stageDescription = "Tiêu diệt toàn bộ lực lượng quái vật xâm lược hoặc phòng thủ cứ điểm thành công.";

        [Header("Cấu Trúc Chương & Màn (Chapter & Stage Structure)")]
        [Tooltip("Chương của màn chơi (mỗi chương gồm 4 màn).")]
        [SerializeField, Min(1)] private int chapter = 1;

        [Tooltip("Số thứ tự màn trong chương (Cứ 3 màn thường thì tới 1 màn Boss: màn 4).")]
        [SerializeField, Min(1)] private int stageNumber = 1;

        [Tooltip("Thể loại màn chơi: Normal (thường) hoặc Boss (đấu Trùm Tinh Anh - EliteEnemyBrain).")]
        [SerializeField] private StageType stageType = StageType.Normal;

        [Header("Giới Hạn Thời Gian (Time Limit)")]
        [Tooltip("Thời gian tối đa để hoàn thành màn chơi (giây). Đặt <= 0 nếu không giới hạn thời gian.")]
        [SerializeField, Min(0f)] private float timeLimit = 180.0f;

        [Header("Quy Tắc Thắng (Win Rule)")]
        [Tooltip("Nếu tích chọn (true): Người chơi PHẢI đạt toàn bộ các điều kiện thắng (AND).\nNếu bỏ chọn (false): Người chơi chỉ cần đạt BẤT KỲ 1 điều kiện nào (OR).\n(Các màn Boss luôn mặc định bật true).")]
        [SerializeField] private bool requireAllWinConditions = true;

        [Header("Điều Kiện Thắng 1: Tiêu Diệt Kẻ Địch (Kill Target)")]
        [Tooltip("Bật/Tắt điều kiện tiêu diệt kẻ địch.")]
        [SerializeField] private bool enableKillCountCondition = true;

        [Tooltip("Số lượng quái cần tiêu diệt để thỏa mãn điều kiện.")]
        [SerializeField, Min(1)] private int targetKills = 15;

        [Header("Điều Kiện Thắng 2: Sinh Tồn Theo Thời Gian (Survive Duration)")]
        [Tooltip("Bật/Tắt điều kiện sống sót theo thời gian.")]
        [SerializeField] private bool enableSurviveCondition = false;

        [Tooltip("Thời gian cần sinh tồn an toàn (giây).")]
        [SerializeField, Min(1.0f)] private float surviveDuration = 60.0f;

        [Header("Điều Kiện Thắng 3: Bảo Vệ Mục Tiêu (Defend VIP / Base)")]
        [Tooltip("Bật/Tắt điều kiện bảo vệ mục tiêu xe tăng đồng minh / công trình.")]
        [SerializeField] private bool enableDefendCondition = false;

        [Tooltip("Thời gian cần bảo vệ mục tiêu sống sót (giây). Đặt 0 nếu chỉ cần bảo vệ đến khi các điều kiện khác hoàn tất.")]
        [SerializeField, Min(0f)] private float defendDuration = 60.0f;

        [Header("Điều Kiện Thắng 4: Tiêu Diệt Boss Tinh Anh (Defeat Elite Boss Target)")]
        [Tooltip("Bật/Tắt điều kiện tiêu diệt trùm Boss. Các màn Boss chỉ có yêu cầu duy nhất là đánh bại Boss.")]
        [SerializeField] private bool enableDefeatBossCondition = false;

        [Tooltip("Số lượng Boss bắt buộc phải tiêu diệt để giành chiến thắng.\nĐặt 0 nghĩa là phải diệt hết toàn bộ Boss của màn chơi.")]
        [SerializeField, Min(0)] private int requiredBossCount = 0;

        // Thuộc tính công khai (Read-only Getters)
        public string StageId => stageId;
        public string StageName => stageName;
        public string StageDescription => stageDescription;
        public int Chapter => chapter;
        public int StageNumber => stageNumber;
        public StageType Type => stageType;
        public bool IsBossStage => stageType == StageType.Boss;

        public float TimeLimit => timeLimit;
        public bool HasTimeLimit => timeLimit > 0f;
        public bool RequireAllWinConditions => requireAllWinConditions;

        public bool EnableKillCountCondition => enableKillCountCondition;
        public int TargetKills => targetKills;

        public bool EnableSurviveCondition => enableSurviveCondition;
        public float SurviveDuration => surviveDuration;

        public bool EnableDefendCondition => enableDefendCondition;
        public float DefendDuration => defendDuration;

        public bool EnableDefeatBossCondition => enableDefeatBossCondition;
        public int RequiredBossCount => requiredBossCount;

        /// <summary>
        /// True nếu cấu hình có ít nhất một mục tiêu chiến thắng mà người chơi có thể hoàn thành.
        /// Dùng để phát hiện sớm cấu hình màn chơi không thể kết thúc.
        /// </summary>
        public bool HasAnyWinCondition => stageType == StageType.Boss
            || enableDefeatBossCondition
            || enableKillCountCondition
            || enableSurviveCondition
            || enableDefendCondition;

        /// <summary>
        /// Tổng số màn của một chương theo thiết kế hiện tại: 4 màn.
        /// </summary>
        public const int MaxStagesPerChapter = 4;

        /// <summary>
        /// Chu kỳ màn Boss: cứ 3 màn thường thì tới 1 màn Boss.
        /// </summary>
        public const int BossStageCycle = 4;

        /// <summary>
        /// Kiểm tra màn có phải màn Boss theo quy tắc chuẩn: cứ 3 màn thường thì tới 1 màn Boss.
        /// Ví dụ: Màn 4 (màn cuối của mỗi chương).
        /// </summary>
        public static bool IsStandardBossStage(int stageNum) => stageNum > 0 && stageNum % BossStageCycle == 0;

        /// <summary>
        /// Lấy tổng số màn tối đa của một chương. Hiện tại mọi chương đều có 4 màn.
        /// </summary>
        public static int GetMaxStagesForChapter(int chapterNum) => chapterNum > 0 ? MaxStagesPerChapter : 0;

        private void OnValidate()
        {
            // Kẹp lại các giá trị tối thiểu phòng trường hợp nhập tay không hợp lệ.
            chapter = Mathf.Max(1, chapter);
            stageNumber = Mathf.Max(1, stageNumber);

            // Đồng bộ thể loại màn theo quy tắc thiết kế: cứ 3 màn thường thì tới 1 màn Boss (màn 4).
            stageType = IsStandardBossStage(stageNumber) ? StageType.Boss : StageType.Normal;

            // Cảnh báo sớm khi cấu hình thiếu mục tiêu chiến thắng (màn chơi sẽ không thể kết thúc).
            if (!HasAnyWinCondition)
            {
                Debug.LogWarning($"[StageDefinition] '{stageId}' chưa bật điều kiện thắng nào nên không thể hoàn thành màn chơi.", this);
            }
        }

        /// <summary>
        /// Khởi tạo danh sách các đối tượng IStageCondition tương ứng từ cấu hình.
        /// </summary>
        /// <param name="defendTarget">Mục tiêu cần bảo vệ (nếu màn có điều kiện bảo vệ).</param>
        /// <param name="bosses">Danh sách Boss do StageManager truyền vào lúc chạy. Để trống nếu màn không có Boss.</param>
        public List<IStageCondition> CreateConditions(TankBrain defendTarget = null, IReadOnlyList<EliteEnemyBrain> bosses = null)
        {
            var conditions = new List<IStageCondition>();

            // 1. Màn Boss: mục tiêu duy nhất là tiêu diệt toàn bộ Boss Tinh Anh.
            if (stageType == StageType.Boss)
            {
                conditions.Add(new DefeatBossCondition(bosses, requiredBossCount));
                return conditions;
            }

            // 2. Các điều kiện cho màn thường.
            if (enableDefeatBossCondition)
            {
                conditions.Add(new DefeatBossCondition(bosses, requiredBossCount));
            }

            if (enableKillCountCondition && targetKills > 0)
            {
                conditions.Add(new KillCountCondition(targetKills));
            }

            if (enableSurviveCondition && surviveDuration > 0f)
            {
                conditions.Add(new SurviveDurationCondition(surviveDuration));
            }

            if (enableDefendCondition)
            {
                conditions.Add(new DefendTargetCondition(defendTarget, defendDuration));
            }

            if (conditions.Count == 0)
            {
                Debug.LogWarning($"[StageDefinition] '{stageId}' không tạo được điều kiện thắng nào.", this);
            }

            return conditions;
        }
    }
}
