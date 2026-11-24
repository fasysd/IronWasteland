using System;
using System.Collections.Generic;

namespace IronWasteland.Save
{
    /// <summary>
    /// Dữ liệu hồ sơ người chơi (Game Profile Data) lưu thành file gamedata.json tại folder [folder chứa game]/save.
    /// Bao gồm Ngọc ma pháp, thông tin xe, phụ kiện sở hữu, tiến trình ải và cài đặt thiết bị/âm thanh.
    /// </summary>
    [Serializable]
    public class GameData
    {
        public int Gems;

        public List<TankSaveData> Tanks = new List<TankSaveData>();
        public List<AccessorySaveData> Accessories = new List<AccessorySaveData>();

        public StageProgressSaveData StageProgress = new StageProgressSaveData();

        public SettingsSaveData Settings = new SettingsSaveData();
    }
}
