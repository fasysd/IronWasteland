using System;
using System.Collections.Generic;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Stages
{
    /// <summary>
    /// Du lieu cua 1 man choi (1 ai): dieu kien thang, thoi luong du kien va
    /// danh sach cac dot quai (Wave) cung loai quai xuat hien theo moc thoi gian.
    ///
    /// Designer tao asset tu menu chuot phai: Create > Iron Wasteland > Stage > Stage Definition.
    /// Logic chay nam o <see cref="StageManager"/> - asset nay chi la du lieu.
    /// </summary>
    [CreateAssetMenu(fileName = "Stage_New", menuName = "Iron Wasteland/Stage/Stage Definition")]
    public class StageDefinition : ScriptableObject
    {
        [Serializable]
        public class EnemySpawn
        {
            [Tooltip("Prefab quai (phai co component ke thua EnemyBrain).")]
            public EnemyBrain Enemy;

            [Tooltip("So luong quai cua loai nay trong dot.")]
            [Min(1)] public int Count = 1;

            [Tooltip("He so nhan vao MoveSpeed cua prefab (1 = giu nguyen, 1.5 = quai cuong hoa toc do).")]
            [Min(0.01f)] public float SpeedMultiplier = 1f;
        }

        [Serializable]
        public class Wave
        {
            [Tooltip("Ten dot de hien thi / debug.")]
            public string Label = "Wave";

            [Tooltip("Thoi diem xuat hien tinh tu luc bat dau man (giay).")]
            [Min(0f)] public float StartTime;

            [Tooltip("Cac loai quai xuat hien trong dot nay.")]
            public List<EnemySpawn> Spawns = new List<EnemySpawn>();

            public int TotalCount
            {
                get
                {
                    int total = 0;
                    if (Spawns == null) return total;
                    for (int i = 0; i < Spawns.Count; i++)
                    {
                        if (Spawns[i] != null) total += Spawns[i].Count;
                    }
                    return total;
                }
            }
        }

        [Header("Thong tin ai")]
        [Tooltip("Ma ai, vi du 1-1.")]
        [SerializeField] private string stageCode = "1-1";

        [Tooltip("Ten ai, vi du Suong Mu Tim.")]
        [SerializeField] private string stageName = "Ai moi";

        [Header("Dieu kien thang")]
        [Tooltip("So quai phai tieu diet de thang.")]
        [SerializeField, Min(1)] private int killTarget = 40;

        [Tooltip("Thoi luong du kien (giay) - dung de hien thi, khong phai dieu kien thua.")]
        [SerializeField, Min(1f)] private float timeLimitSeconds = 90f;

        [Header("Cac dot quai")]
        [SerializeField] private List<Wave> waves = new List<Wave>();

        public string StageCode => stageCode;
        public string StageName => stageName;
        public int KillTarget => killTarget;
        public float TimeLimitSeconds => timeLimitSeconds;
        public IReadOnlyList<Wave> Waves => waves;

        /// <summary>Tong so quai se spawn cua ca ai.</summary>
        public int TotalEnemies
        {
            get
            {
                int total = 0;
                if (waves == null) return total;
                for (int i = 0; i < waves.Count; i++)
                {
                    if (waves[i] != null) total += waves[i].TotalCount;
                }
                return total;
            }
        }
    }
}
