using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Stages
{
    /// <summary>
    /// Du lieu cua 1 man choi (1 ai): dieu kien thang, thoi luong du kien va phan thuong.
    ///
    /// Thiet ke cac dot quai (wave) KHONG con nam o day: tu 2026-11, wave thuoc ve
    /// component <see cref="EnemySpawner"/> trong scene cua man do. Ly do: wave la
    /// thu designer chinh lien tuc khi can bang man, de chung voi vung spawn (SpawnZone)
    /// tren cung 1 component thi sua nhanh hon la mo asset roi doi chieu scene.
    ///
    /// Designer tao asset tu menu chuot phai: Create > Iron Wasteland > Stage > Stage Definition.
    /// Logic chay nam o <see cref="StageDirector"/> - asset nay chi la du lieu.
    /// </summary>
    [CreateAssetMenu(fileName = "Stage_New", menuName = "Iron Wasteland/Stage/Stage Definition")]
    public class StageDefinition : ScriptableObject
    {
        /// <summary>
        /// Mo ta 1 dieu kien thang. Nhieu spec tren cung 1 man = phai thoa TAT CA.
        /// </summary>
        [Serializable]
        public class ObjectiveSpec
        {
            [Tooltip("Loai dieu kien.")]
            public StageObjectiveType Type = StageObjectiveType.Eliminate;

            [Tooltip("So luong can dat: so quai (Eliminate), so vat the (DestroyTargets), so radar (Radar).")]
            [Min(1)] public int Count = 40;

            [Tooltip("Thoi gian can song (giay) - chi dung cho Survive.")]
            [Min(0f)] public float Duration = 100f;

            [Tooltip("Quang duong can ho tong (met) - chi dung cho Escort.")]
            [Min(0f)] public float Distance = 150f;

            [Tooltip("Id cua vat the / xe ho tong / radar tren map (khong dung cho Eliminate, Survive).")]
            public string TargetId = string.Empty;
        }

        [Header("Thong tin ai")]
        [Tooltip("Ma ai, vi du 1-1.")]
        [SerializeField] private string stageCode = "1-1";

        [Tooltip("Ten ai, vi du Suong Mu Tim.")]
        [SerializeField] private string stageName = "Ai moi";

        [Header("Dieu kien thang (kieu cu - chi dung khi Objectives rong)")]
        [Tooltip("So quai phai tieu diet de thang.")]
        [SerializeField, Min(1)] private int killTarget = 40;

        [Tooltip("Thoi luong du kien (giay) - dung de hien thi, khong phai dieu kien thua.")]
        [SerializeField, Min(1f)] private float timeLimitSeconds = 90f;

        [Header("Dieu kien thang (moi)")]
        [Tooltip("Danh sach dieu kien phai thoa HET de thang. Rong = dung killTarget cu.")]
        [SerializeField] private List<ObjectiveSpec> objectives = new List<ObjectiveSpec>();

        [Header("Thuong")]
        [Tooltip("Ngoc nhan duoc khi thang (theo bang ai trong GDD).")]
        [SerializeField, Min(0)] private int rewardGems = 20;

        [Tooltip("So mon phu kien toi da roi ra.")]
        [SerializeField, Min(0)] private int rewardAccessoryCount = 0;

        [Tooltip("Boss man: chac chan du so mon, khong roll 20%.")]
        [SerializeField] private bool guaranteeAccessories = false;

        [Tooltip("Danh sach index phu kien (0..19) co the roi trong man nay theo GDD.")]
        [SerializeField] private List<int> rewardAccessoryPool = new List<int>();

        [Tooltip("Ma ai tiep theo, vi du 1-2 (rong = khong tu chuyen).")]
        [SerializeField] private string nextStageCode = string.Empty;

        public string StageCode => stageCode;
        public string StageName => stageName;
        public int KillTarget => killTarget;
        public float TimeLimitSeconds => timeLimitSeconds;

        /// <summary>Cac dieu kien thang khai bao trong asset (co the rong).</summary>
        public IReadOnlyList<ObjectiveSpec> Objectives => objectives;

        public int RewardGems => rewardGems;
        public int RewardAccessoryCount => rewardAccessoryCount;
        public bool GuaranteeAccessories => guaranteeAccessories;
        public IReadOnlyList<int> RewardAccessoryPool => rewardAccessoryPool;
        public string NextStageCode => nextStageCode;
    }
}
