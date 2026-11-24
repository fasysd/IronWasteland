using System;
using System.Collections.Generic;

namespace IronWasteland.Save
{
    /// <summary>
    /// Dữ liệu hồ sơ người chơi (Game Profile Data) lưu thành file gamedata.json.
    /// Bao gồm Ngọc ma pháp, thông tin xe, phụ kiện sở hữu và tiến trình ải.
    /// </summary>
    [Serializable]
    public class GameData
    {
        public int Gems;

        public List<TankSaveData> Tanks = new List<TankSaveData>();
        public List<AccessorySaveData> Accessories = new List<AccessorySaveData>();

        public StageProgressSaveData StageProgress = new StageProgressSaveData();
    }
}
