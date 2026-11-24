#if UNITY_EDITOR
using System;
using System.IO;
using IronWasteland.Save;
using IronWasteland.Tanks;
using UnityEditor;
using UnityEngine;

namespace IronWasteland.Tests
{
    /// <summary>
    /// Công cụ tự động kiểm tra hệ thống SaveSystem và TankRegistry.
    /// Có thể chạy từ menu Unity: Tools -> Iron Wasteland -> Run SaveSystem Self-Check
    /// </summary>
    public static class SaveSystemTests
    {
        [MenuItem("Tools/Iron Wasteland/Run SaveSystem Self-Check")]
        public static void RunAllTests()
        {
            Debug.Log("<color=cyan>====== BẮT ĐẦU KIỂM TRA SAVESYSTEM ======</color>");
            int passed = 0;
            int total = 5;

            try
            {
                Test_DefaultSaveData();
                passed++;
                Debug.Log("✔ [PASS] 1. Khởi tạo mặc định: Wall Titan & Red Nova Level 1, các xe khác Level 0, Màn 1-1 mở khóa, Gems = 0, 0 phụ kiện.");

                Test_GemsAndUpgrade();
                passed++;
                Debug.Log("✔ [PASS] 2. Nâng cấp xe & trừ Ngọc ma pháp chuẩn xác.");

                Test_AccessoryInventory();
                passed++;
                Debug.Log("✔ [PASS] 3. Quản lý kho phụ kiện sở hữu & trang bị xe.");

                Test_StageProgression();
                passed++;
                Debug.Log("✔ [PASS] 4. Tiến trình vượt màn, mở khóa ải kế tiếp & kỷ lục thời gian.");

                Test_DeviceSettings();
                passed++;
                Debug.Log("✔ [PASS] 5. Gộp cài đặt và lưu file gamedata.json duy nhất tại folder [folder chứa game]/save.");

                Debug.Log($"<color=green>====== TẤT CẢ {passed}/{total} BÀI TEST THÀNH CÔNG! ======</color>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [FAIL] Kiểm tra thất bại: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new Exception($"[Assert Failed] {message}. Kỳ vọng: '{expected}', Thực tế: '{actual}'");
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[Assert Failed] {message}. Kỳ vọng true nhưng nhận false.");
            }
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition)
            {
                throw new Exception($"[Assert Failed] {message}. Kỳ vọng false nhưng nhận true.");
            }
        }

        private static void Test_DefaultSaveData()
        {
            SaveSystem.Instance.ResetAll();

            AssertEqual(1, SaveSystem.Instance.GetTankLevel(0), "Wall Titan phải có level 1");
            AssertEqual(1, SaveSystem.Instance.GetTankLevel(1), "Red Nova phải có level 1");
            AssertTrue(SaveSystem.Instance.IsTankOwned(0), "Wall Titan phải là đã sở hữu");
            AssertTrue(SaveSystem.Instance.IsTankOwned(1), "Red Nova phải là đã sở hữu");

            for (int i = 2; i < 6; i++)
            {
                AssertEqual(0, SaveSystem.Instance.GetTankLevel(i), $"Tank index {i} phải có level 0");
                AssertFalse(SaveSystem.Instance.IsTankOwned(i), $"Tank index {i} chưa được sở hữu");
            }

            AssertTrue(SaveSystem.Instance.IsStageUnlocked("1-1"), "Màn 1-1 phải mở khóa ban đầu");
            AssertFalse(SaveSystem.Instance.IsStageUnlocked("1-2"), "Màn 1-2 ban đầu phải bị khóa");
            AssertFalse(SaveSystem.Instance.IsStageCleared("1-1"), "Màn 1-1 ban đầu chưa hoàn thành");

            AssertEqual(-1, SaveSystem.Instance.GetEquippedAccessory(0), "Wall Titan ban đầu chưa trang bị phụ kiện");
            AssertEqual(-1, SaveSystem.Instance.GetEquippedAccessory(1), "Red Nova ban đầu chưa trang bị phụ kiện");
            AssertEqual(0, SaveSystem.Instance.GetOwnedAccessories().Count, "Danh sách phụ kiện sở hữu ban đầu phải rỗng");

            for (int acc = 0; acc < 20; acc++)
            {
                AssertEqual(0, SaveSystem.Instance.GetAccessoryQuantity(acc), $"Phụ kiện #{acc} phải có số lượng = 0");
                AssertFalse(SaveSystem.Instance.HasAccessory(acc), $"Phụ kiện #{acc} chưa được sở hữu");
            }

            AssertEqual(0, SaveSystem.Instance.Gems, "Ngọc ban đầu phải là 0");
        }

        private static void Test_GemsAndUpgrade()
        {
            SaveSystem.Instance.AddGems(500);
            AssertEqual(500, SaveSystem.Instance.Gems, "Cộng ngọc thất bại");

            int prevLevel = SaveSystem.Instance.GetTankLevel(0);
            int cost = TankUpgradeUtility.GetCostForNextLevel(prevLevel);

            bool success = SaveSystem.Instance.UpgradeTank(0);
            AssertTrue(success, "Nâng cấp xe phải thành công khi đủ ngọc");
            AssertEqual(prevLevel + 1, SaveSystem.Instance.GetTankLevel(0), "Level xe chưa tăng");
            AssertEqual(500 - cost, SaveSystem.Instance.Gems, "Ngọc chưa bị trừ đúng");

            bool unownedUpgrade = SaveSystem.Instance.UpgradeTank(2);
            AssertFalse(unownedUpgrade, "Không được phép nâng cấp xe chưa sở hữu");
        }

        private static void Test_AccessoryInventory()
        {
            SaveSystem.Instance.AddAccessory(5, 2);
            AssertEqual(2, SaveSystem.Instance.GetAccessoryQuantity(5), "Số lượng phụ kiện thêm vào sai");
            AssertTrue(SaveSystem.Instance.HasAccessory(5), "Phải báo đã sở hữu phụ kiện 5");

            SaveSystem.Instance.SetEquippedAccessory(0, 5);
            AssertEqual(5, SaveSystem.Instance.GetEquippedAccessory(0), "Trang bị phụ kiện thất bại");

            SaveSystem.Instance.SetEquippedAccessory(0, -1);
            AssertEqual(-1, SaveSystem.Instance.GetEquippedAccessory(0), "Gỡ phụ kiện thất bại");

            bool consumed = SaveSystem.Instance.ConsumeAccessory(5, 1);
            AssertTrue(consumed, "Tiêu thụ phụ kiện thất bại");
            AssertEqual(1, SaveSystem.Instance.GetAccessoryQuantity(5), "Số lượng còn lại sai");

            SaveSystem.Instance.ConsumeAccessory(5, 1);
            AssertEqual(0, SaveSystem.Instance.GetAccessoryQuantity(5), "Số lượng sau khi dùng hết phải là 0");
            AssertFalse(SaveSystem.Instance.HasAccessory(5), "Không còn phụ kiện");
        }

        private static void Test_StageProgression()
        {
            AssertFalse(SaveSystem.Instance.IsStageCleared("1-1"), "Màn 1-1 ban đầu chưa hoàn thành");
            AssertFalse(SaveSystem.Instance.IsStageUnlocked("1-2"), "Màn 1-2 ban đầu chưa mở khóa");

            SaveSystem.Instance.MarkCleared("1-1", 20, 45.5f, "1-2");

            AssertTrue(SaveSystem.Instance.IsStageCleared("1-1"), "Màn 1-1 phải ghi nhận đã clear");
            AssertTrue(SaveSystem.Instance.IsStageUnlocked("1-2"), "Màn 1-2 phải được mở khóa");
            AssertEqual(45.5f, SaveSystem.Instance.GetBestSeconds("1-1"), "Kỷ lục thời gian lưu sai");
        }

        private static void Test_DeviceSettings()
        {
            SaveSystem.Instance.SetSettings(85, 70, 50, 90);
            SaveSystem.Instance.LastSelectedTankIndex = 1;

            AssertEqual(85, SaveSystem.Instance.Settings.MasterVolume, "MasterVolume lưu sai");
            AssertEqual(70, SaveSystem.Instance.Settings.SfxVolume, "SfxVolume lưu sai");
            AssertEqual(50, SaveSystem.Instance.Settings.BgmVolume, "BgmVolume lưu sai");
            AssertEqual(90, SaveSystem.Instance.Settings.AnnouncerVolume, "AnnouncerVolume lưu sai");
            AssertEqual(1, SaveSystem.Instance.LastSelectedTankIndex, "LastSelectedTankIndex lưu sai");

            string saveFilePath = SaveSystem.Instance.SaveFilePath;
            AssertTrue(File.Exists(saveFilePath), $"File gamedata.json phải tồn tại tại {saveFilePath}");
            AssertTrue(saveFilePath.EndsWith(Path.Combine("save", "gamedata.json")), "Đường dẫn file save phải nằm trong thư mục save");
        }
    }
}
#endif
