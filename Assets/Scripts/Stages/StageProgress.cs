using UnityEngine;

namespace IronWasteland.Stages
{
    /// <summary>
    /// Luu tien do choi bang PlayerPrefs: man nao da mo khoa, man nao da thang,
    /// tong ngoc, ky luc thoi gian.
    ///
    /// Vi sao dung PlayerPrefs: do la cach don gian nhat trong Unity de luu vai con so
    /// giua cac lan chay (khong can file rieng, khong can he thong save phuc tap).
    /// Khi nao can luu nhieu hon (tung phu kien, tung tank da mua) thi thay bang file JSON.
    ///
    /// Quy uoc key:
    ///   iw_gems                    -> tong ngoc
    ///   iw_stage_unlocked_&lt;code&gt;  -> 1 neu da mo khoa
    ///   iw_stage_cleared_&lt;code&gt;   -> 1 neu da thang it nhat 1 lan
    ///   iw_stage_best_&lt;code&gt;      -> thoi gian thang nhanh nhat (giay)
    /// </summary>
    public static class StageProgress
    {
        public const string FirstStageCode = "1-1";

        private const string GemsKey = "iw_gems";
        private const string UnlockedPrefix = "iw_stage_unlocked_";
        private const string ClearedPrefix = "iw_stage_cleared_";
        private const string BestPrefix = "iw_stage_best_";

        /// <summary>Tong ngoc dang co.</summary>
        public static int Gems => PlayerPrefs.GetInt(GemsKey, 0);

        /// <summary>
        /// Man dau tien luon mo; cac man khac phai duoc mo khoa.
        /// Man da thang coi nhu da mo (tranh truong hop "Khoa" o man tung choi qua).
        /// </summary>
        public static bool IsUnlocked(string stageCode)
        {
            if (string.IsNullOrEmpty(stageCode)) return false;
            if (stageCode == FirstStageCode) return true;
            if (PlayerPrefs.GetInt(UnlockedPrefix + stageCode, 0) == 1) return true;
            return PlayerPrefs.GetInt(ClearedPrefix + stageCode, 0) == 1;
        }

        public static bool IsCleared(string stageCode)
        {
            if (string.IsNullOrEmpty(stageCode)) return false;
            return PlayerPrefs.GetInt(ClearedPrefix + stageCode, 0) == 1;
        }

        public static void Unlock(string stageCode)
        {
            if (string.IsNullOrEmpty(stageCode)) return;
            PlayerPrefs.SetInt(UnlockedPrefix + stageCode, 1);
            PlayerPrefs.Save();
        }

        public static void AddGems(int amount)
        {
            if (amount <= 0) return;
            PlayerPrefs.SetInt(GemsKey, Gems + amount);
            PlayerPrefs.Save();
        }

        /// <summary>Ky luc thoi gian (giay). 0 = chua co.</summary>
        public static float BestSeconds(string stageCode)
        {
            if (string.IsNullOrEmpty(stageCode)) return 0f;
            return PlayerPrefs.GetFloat(BestPrefix + stageCode, 0f);
        }

        /// <summary>
        /// Ghi nhan 1 lan thang man: cong ngoc, mo khoa man ke, cap nhat ky luc.
        /// Goi 1 lan khi StageDirector phat StageCleared.
        /// </summary>
        public static void MarkCleared(string stageCode, int gems, float seconds, string nextStageCode)
        {
            if (string.IsNullOrEmpty(stageCode)) return;

            PlayerPrefs.SetInt(ClearedPrefix + stageCode, 1);
            AddGems(gems);

            if (!string.IsNullOrEmpty(nextStageCode)) Unlock(nextStageCode);

            if (seconds > 0f)
            {
                float best = BestSeconds(stageCode);
                if (best <= 0f || seconds < best) PlayerPrefs.SetFloat(BestPrefix + stageCode, seconds);
            }

            PlayerPrefs.Save();
        }

        /// <summary>Ten scene tuong ung voi ma man: "1-2" -> "Stage_1_2".</summary>
        public static string SceneNameFor(string stageCode)
        {
            if (string.IsNullOrEmpty(stageCode)) return string.Empty;
            return "Stage_" + stageCode.Replace('-', '_');
        }

        /// <summary>Xoa sach tien do (dung khi test).</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(GemsKey);
            PlayerPrefs.Save();
        }

        /// <summary>Xoa tien do cua 1 man cu the (dung khi test).</summary>
        public static void ResetStage(string stageCode)
        {
            if (string.IsNullOrEmpty(stageCode)) return;
            PlayerPrefs.DeleteKey(UnlockedPrefix + stageCode);
            PlayerPrefs.DeleteKey(ClearedPrefix + stageCode);
            PlayerPrefs.DeleteKey(BestPrefix + stageCode);
            PlayerPrefs.Save();
        }
    }
}
