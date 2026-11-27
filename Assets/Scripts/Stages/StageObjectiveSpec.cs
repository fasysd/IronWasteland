using System;
using UnityEngine;

namespace IronWasteland.Stages
{
    /// <summary>
    /// Mô tả 1 điều kiện thắng trong scene của màn chơi. Nhiều spec trên cùng 1 màn = phải thỏa TẤT CẢ.
    /// (Tách khỏi StageDefinition để điều kiện thắng nằm ngay trên StageDirector trong từng scene.)
    /// </summary>
    [Serializable]
    public class StageObjectiveSpec
    {
        [Tooltip("Loại điều kiện.")]
        public StageObjectiveType Type = StageObjectiveType.Eliminate;

        [Tooltip("Số lượng cần đạt: số quái (Eliminate), số vật thể (DestroyTargets), số radar (Radar).")]
        [Min(1)] public int Count = 40;

        [Tooltip("Thời gian cần sống (giây) - chỉ dùng cho Survive.")]
        [Min(0f)] public float Duration = 100f;

        [Tooltip("Quãng đường cần hộ tống (mét) - chỉ dùng cho Escort.")]
        [Min(0f)] public float Distance = 150f;

        [Tooltip("Id của vật thể / xe hộ tống / radar trên map (không dùng cho Eliminate, Survive).")]
        public string TargetId = string.Empty;
    }
}
