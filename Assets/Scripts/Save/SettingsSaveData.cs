using System;

namespace IronWasteland.Save
{
    /// <summary>
    /// Dữ liệu cài đặt thiết bị lưu riêng tại device_settings.json.
    /// Không ảnh hưởng đến dữ liệu gameplay chính, mục đích tiện ích trên máy người chơi.
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
