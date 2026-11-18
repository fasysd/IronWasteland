using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Menu
{
    public enum StageKind
    {
        Normal = 0,
        Elite = 1,      // Boss tinh anh
        FinalBoss = 2,  // Boss chung cuộc của chương
    }

    [Serializable]
    public class StageEntry
    {
        [Tooltip("Id ải, vd: 1-1. Dùng làm khóa lưu tiến độ.")]
        public string id = "1-1";

        public string displayName = "Tên ải";

        [Tooltip("Vd: CHƯƠNG 1 · LỤC ĐỊA ĐEN")]
        public string chapterLabel = "";

        [TextArea(2, 4)] public string description = "";

        [Tooltip("Tên Scene của ải (mỗi ải là một Scene riêng, nhớ thêm vào Build Settings).")]
        public string sceneName = "";

        public StageKind kind = StageKind.Normal;

        [Min(1)] public int recommendedLevel = 1;

        [Min(0)] public int rewardGems = 10;

        [Range(0f, 1f)] public float accessoryDropChance = 0.2f;

        [Tooltip("ID phụ kiện (trong EquipmentDatabase) có thể rơi ở ải này. Để trống = tất cả phụ kiện.")]
        public List<int> accessoryPool = new List<int>();

        [Tooltip("Khi trúng tỉ lệ rơi: số phụ kiện KHÁC NHAU nhận được (theo GDD: ải thường 1, 1-4 là 3, 2-4 là 2, 2-5 là 3).")]
        [Min(1)] public int accessoryDropCount = 1;

        public string Label => "ẢI " + id;
    }

    [CreateAssetMenu(menuName = "IronWasteland/Menu/Stage Database", fileName = "StageDatabase")]
    public class StageDatabase : ScriptableObject
    {
        [Tooltip("Thứ tự = thứ tự mở khóa. Ải đầu luôn mở; ải sau mở khi ải liền trước đã hoàn thành.")]
        public List<StageEntry> stages = new List<StageEntry>();

        public StageEntry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < stages.Count; i++)
                if (stages[i] != null && stages[i].id == id) return stages[i];
            return null;
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < stages.Count; i++)
                if (stages[i] != null && stages[i].id == id) return i;
            return -1;
        }
    }
}
