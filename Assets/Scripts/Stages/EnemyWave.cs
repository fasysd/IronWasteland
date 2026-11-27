using System;
using System.Collections.Generic;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Stages
{
    /// <summary>
    /// Một nhóm quái trong 1 đợt: prefab nào, bao nhiêu con, rơi ở vùng nào.
    ///
    /// Các field giữ đúng tên cũ của <c>StageDefinition.EnemySpawn</c> để dữ liệu YAML
    /// đã lưu trong scene/asset đọc lại được mà không phải migrate tay.
    /// </summary>
    [Serializable]
    public class EnemyWaveSpawn
    {
        [Tooltip("Prefab quái (phải có EnemyBrain).")]
        public EnemyBrain Enemy;

        [Min(1)]
        public int Count = 1;

        [Min(0.01f)]
        public float SpeedMultiplier = 1f;

        [Tooltip("Để trống = rơi ở vùng spawn đầu tiên. Khai báo ZoneId thì quái rơi trong vùng khớp.")]
        public string ZoneId = string.Empty;
    }

    /// <summary>
    /// Một đợt quái của màn. Dữ liệu này do <see cref="EnemySpawner"/> trong scene sở hữu
    /// (không còn nằm trong StageDefinition / StageDirector).
    /// </summary>
    [Serializable]
    public class EnemyWave
    {
        [Tooltip("Tên hiển thị trong log/HUD, ví dụ W1, Đợt 2...")]
        public string Label = "Wave";

        [Tooltip("Giây thứ mấy kể từ lúc màn bắt đầu thì đợt thường xuất hiện.")]
        [Min(0f)]
        public float StartTime;

        [Tooltip("Nếu có, đợt này bắt đầu khi tank bắt đầu bật radar được gán.")]
        public RadarStation StartRadar;

        [Tooltip("Các nhóm quái của đợt.")]
        public List<EnemyWaveSpawn> Spawns = new List<EnemyWaveSpawn>();

        /// <summary>Tổng số quái của đợt (cộng Count các nhóm).</summary>
        public int TotalCount
        {
            get
            {
                int total = 0;
                if (Spawns == null) return total;
                for (int i = 0; i < Spawns.Count; i++)
                {
                    if (Spawns[i] != null) total += Mathf.Max(1, Spawns[i].Count);
                }
                return total;
            }
        }
    }
}
