using System;
using System.Collections.Generic;

namespace IronWasteland.Save
{
    /// <summary>
    /// Tiến trình màn chơi: ải đã mở khóa, ải đã vượt qua, kỷ lục thời gian.
    /// </summary>
    [Serializable]
    public class StageProgressSaveData
    {
        public const string FirstStageCode = "1-1";

        public List<string> ClearedStageCodes = new List<string>();
        public List<string> UnlockedStageCodes = new List<string>();
        public List<StageBestTime> BestTimes = new List<StageBestTime>();

        public bool IsUnlocked(string stageCode)
        {
            if (string.IsNullOrEmpty(stageCode)) return false;
            if (stageCode == FirstStageCode) return true;
            return UnlockedStageCodes.Contains(stageCode) || ClearedStageCodes.Contains(stageCode);
        }

        public bool IsCleared(string stageCode)
        {
            if (string.IsNullOrEmpty(stageCode)) return false;
            return ClearedStageCodes.Contains(stageCode);
        }

        public float BestSeconds(string stageCode)
        {
            if (string.IsNullOrEmpty(stageCode)) return 0f;
            for (int i = 0; i < BestTimes.Count; i++)
            {
                if (BestTimes[i].StageCode == stageCode)
                    return BestTimes[i].Seconds;
            }
            return 0f;
        }

        public void SetBestTime(string stageCode, float seconds)
        {
            if (string.IsNullOrEmpty(stageCode) || seconds <= 0f) return;

            for (int i = 0; i < BestTimes.Count; i++)
            {
                if (BestTimes[i].StageCode == stageCode)
                {
                    if (seconds < BestTimes[i].Seconds)
                        BestTimes[i].Seconds = seconds;
                    return;
                }
            }
            BestTimes.Add(new StageBestTime { StageCode = stageCode, Seconds = seconds });
        }
    }

    [Serializable]
    public class StageBestTime
    {
        public string StageCode;
        public float Seconds;
    }
}
