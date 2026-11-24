using System;

namespace IronWasteland.Save
{
    /// <summary>
    /// Dữ liệu cài đặt thiết bị và tùy chọn người chơi, được lưu chung trong gamedata.json.
    /// Bao gồm cài đặt âm lượng và chỉ số xe được chọn cuối cùng.
    /// </summary>
    [Serializable]
    public class SettingsSaveData
    {
        public int MasterVolume = 78;
        public int SfxVolume = 65;
        public int BgmVolume = 45;
        public int AnnouncerVolume = 80;

        public int LastSelectedTankIndex = 0;
    }
}
