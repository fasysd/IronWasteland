using UnityEngine;
using UnityEngine.SceneManagement;
using IronWasteland.Tanks;

namespace IronWasteland.Menu
{
    /// <summary>Màn hình Menu sẽ mở khi quay về từ một ải.</summary>
    public enum MenuLanding
    {
        MainMenu = 0,     // Thắng ải -> về sảnh chính (GDD 2.2)
        StageSelect = 1,  // Thua ải khi chơi đơn -> về giao diện chọn ải
    }

    /// <summary>
    /// Cầu nối giữa Menu và Scene ải (static, sống qua việc đổi Scene).
    ///
    /// Menu gọi <see cref="Begin"/> rồi load Scene ải. Trong Scene ải:
    ///   - GameController đọc <see cref="Tank"/> + <see cref="Context"/> để spawn xe.
    ///   - Khi thắng, gọi <see cref="ReportVictory"/> để cộng Ngọc / tính rơi phụ kiện / mở xe, mở ải kế.
    ///   - Muốn quay về Menu, gọi <see cref="ReturnToMenu"/> (thắng -> MainMenu, thua chơi đơn -> StageSelect).
    ///
    /// Nếu chạy thẳng Scene ải (không qua Menu) thì <see cref="HasSelection"/> = false và
    /// GameController dùng giá trị gán tay trong Inspector như trước.
    /// </summary>
    public static class GameSession
    {
        public static bool HasSelection { get; private set; }
        public static string TankId { get; private set; }
        public static TankDefinition Tank { get; private set; }
        public static TankBrainContext Context { get; private set; }
        public static StageEntry Stage { get; private set; }
        public static AccessoryCatalog Accessories { get; private set; }
        public static TankCatalog Tanks { get; private set; }
        public static bool IsCoop { get; private set; }

        /// <summary>Tên Scene chứa Menu. MenuBootstrap tự gán lúc khởi động.</summary>
        public static string MenuSceneName { get; set; } = "MainMenu";

        /// <summary>Màn Menu sẽ mở ở lần nạp Menu kế tiếp. MenuBootstrap đọc rồi đặt lại về MainMenu.</summary>
        public static MenuLanding NextLanding { get; set; } = MenuLanding.MainMenu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clear();
            NextLanding = MenuLanding.MainMenu;
        }

        public static void Begin(
            string tankId,
            TankDefinition tank,
            TankBrainContext context,
            StageEntry stage,
            AccessoryCatalog accessories,
            TankCatalog tanks,
            bool isCoop)
        {
            TankId = tankId;
            Tank = tank;
            Context = context;
            Stage = stage;
            Accessories = accessories;
            Tanks = tanks;
            IsCoop = isCoop;
            HasSelection = true;
        }

        public static void Clear()
        {
            HasSelection = false;
            TankId = null;
            Tank = null;
            Stage = null;
            Accessories = null;
            Tanks = null;
            IsCoop = false;
            Context = default;
        }

        /// <summary>
        /// Người chơi thắng ải: lưu tiến độ + phần thưởng. Trả về phần thưởng để hiện lên màn kết quả
        /// (Ngọc, phụ kiện rơi ra, xe mới mở khóa).
        /// </summary>
        public static StageResult ReportVictory()
        {
            if (Stage == null)
                return new StageResult { Gems = 0, AccessoryIds = new int[0], UnlockedTankIds = new string[0] };

            return ProfileService.CompleteStage(Stage, Accessories, Tanks);
        }

        public static void ReturnToMenu(MenuLanding landing = MenuLanding.MainMenu)
        {
            NextLanding = landing;
            Time.timeScale = 1f;
            SceneManager.LoadScene(MenuSceneName);
        }
    }
}
