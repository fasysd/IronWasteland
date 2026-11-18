using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using IronWasteland.Tanks;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Nơi DUY NHẤT sửa dữ liệu người chơi (Ngọc ma pháp, cấp xe, kỹ năng chọn, phụ kiện, tiến độ ải).
    /// Mọi thay đổi đều tự lưu file JSON và phát <see cref="Changed"/> để UI cập nhật.
    /// Màn hình UI KHÔNG tự sửa <see cref="PlayerProfile"/> trực tiếp.
    /// </summary>
    public static class ProfileService
    {
        private const string FileName = "ironwasteland_profile.json";

        public static PlayerProfile Current { get; private set; }

        /// <summary>Phát sau mỗi thay đổi đã được lưu.</summary>
        public static event Action Changed;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>
        /// Khi tắt "Reload Domain" lúc vào Play Mode, biến static (kể cả event) sống sang lần Play sau
        /// và giữ tham chiếu tới màn hình cũ đã bị hủy -> MissingReferenceException. Đặt lại ở đây.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            Changed = null;
        }

        /// <summary>
        /// Phát Changed cho từng người nghe riêng: một người nghe đã bị hủy (hoặc lỗi) không làm
        /// các người nghe còn lại bị bỏ qua, và người nghe đã hủy sẽ tự bị gỡ.
        /// </summary>
        private static void RaiseChanged()
        {
            Action handlers = Changed;
            if (handlers == null) return;

            foreach (Delegate d in handlers.GetInvocationList())
            {
                var action = (Action)d;
                try
                {
                    action();
                }
                catch (MissingReferenceException)
                {
                    Changed -= action;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        // ============================================================
        // LOAD / SAVE
        // ============================================================

        /// <summary>Dùng ở Scene ải khi chạy thẳng Scene (không qua Menu).</summary>
        public static void EnsureLoaded()
        {
            if (Current == null) Load(null, null);
        }

        public static void Load(TankCatalog tanks, ProfileDefaults defaults)
        {
            PlayerProfile loaded = null;
            try
            {
                if (File.Exists(FilePath))
                    loaded = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{nameof(ProfileService)}] Không đọc được file lưu, tạo hồ sơ mới. {e.Message}");
            }

            bool isNew = loaded == null;
            Current = loaded ?? new PlayerProfile();
            if (Current.tanks == null) Current.tanks = new List<TankSave>();
            if (Current.accessories == null) Current.accessories = new List<AccessoryStackSave>();
            if (Current.clearedStageIds == null) Current.clearedStageIds = new List<string>();

            if (isNew && defaults != null)
            {
                Current.gems = Mathf.Max(0, defaults.startingGems);
                if (defaults.starterAccessoryIds != null)
                {
                    for (int i = 0; i < defaults.starterAccessoryIds.Length; i++)
                        AddAccessoryInternal(defaults.starterAccessoryIds[i], 1);
                }
            }

            // Chuẩn hóa dữ liệu theo danh mục xe hiện có.
            for (int i = 0; i < Current.tanks.Count; i++)
            {
                TankSave t = Current.tanks[i];
                t.level = Mathf.Clamp(t.level, TankDefinition.MinLevel, TankDefinition.MaxLevel);
                t.optionSkill1 = Mathf.Max(1, t.optionSkill1);
                t.optionSkill2 = Mathf.Max(1, t.optionSkill2);
            }

            if (tanks != null)
            {
                for (int i = 0; i < tanks.tanks.Count; i++)
                {
                    TankEntry e = tanks.tanks[i];
                    if (e != null && !string.IsNullOrEmpty(e.id)) GetTank(e.id);
                }

                TankEntry selected = tanks.Find(Current.selectedTankId);
                if (selected == null || !IsTankUnlocked(selected))
                {
                    Current.selectedTankId = "";
                    for (int i = 0; i < tanks.tanks.Count; i++)
                    {
                        if (tanks.tanks[i] != null && IsTankUnlocked(tanks.tanks[i]))
                        {
                            Current.selectedTankId = tanks.tanks[i].id;
                            break;
                        }
                    }
                }
            }

            Save();
        }

        public static void Save()
        {
            if (Current == null) return;
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{nameof(ProfileService)}] Không ghi được file lưu: {e.Message}");
            }
        }

        /// <summary>Xóa file lưu và nạp lại hồ sơ mới (dùng khi test).</summary>
        public static void ResetAll(TankCatalog tanks, ProfileDefaults defaults)
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception e) { Debug.LogWarning(e.Message); }

            Current = null;
            Load(tanks, defaults);
            RaiseChanged();
        }

        private static void Commit()
        {
            Save();
            RaiseChanged();
        }

        // ============================================================
        // NGỌC MA PHÁP
        // ============================================================

        public static int Gems
        {
            get { EnsureLoaded(); return Current.gems; }
        }

        public static void AddGems(int amount)
        {
            EnsureLoaded();
            Current.gems = Mathf.Max(0, Current.gems + amount);
            Commit();
        }

        // ============================================================
        // XE TĂNG
        // ============================================================

        public static TankSave GetTank(string id)
        {
            EnsureLoaded();
            List<TankSave> list = Current.tanks;
            for (int i = 0; i < list.Count; i++)
                if (list[i].id == id) return list[i];

            var created = new TankSave { id = id };
            list.Add(created);
            return created;
        }

        public static int GetLevel(string id) => GetTank(id).level;

        public static bool IsMaxLevel(string id) => GetLevel(id) >= TankDefinition.MaxLevel;

        /// <summary>Ngọc cần để lên cấp kế tiếp (0 nếu đã tối đa).</summary>
        public static int GetUpgradeCost(string id)
        {
            int level = GetLevel(id);
            return level >= TankDefinition.MaxLevel ? 0 : TankUpgradeUtility.GetCostForNextLevel(level);
        }

        public static UpgradeResult TryUpgrade(string id)
        {
            TankSave t = GetTank(id);
            if (t.level >= TankDefinition.MaxLevel) return UpgradeResult.MaxLevel;

            int cost = TankUpgradeUtility.GetCostForNextLevel(t.level);
            if (Current.gems < cost) return UpgradeResult.NotEnoughGems;

            Current.gems -= cost;
            t.level++;
            Commit();
            return UpgradeResult.Ok;
        }

        public static bool IsTankUnlocked(TankEntry entry)
        {
            if (entry == null) return false;
            return string.IsNullOrEmpty(entry.unlockStageId) || IsStageCleared(entry.unlockStageId);
        }

        public static void SelectTank(string id)
        {
            EnsureLoaded();
            if (Current.selectedTankId == id) return;
            Current.selectedTankId = id;
            Commit();
        }

        /// <param name="optionTab">0 = Tùy chọn 1, 1 = Tùy chọn 2.</param>
        /// <param name="variant">Bắt đầu từ 1.</param>
        public static void SetOption(string id, int optionTab, int variant)
        {
            TankSave t = GetTank(id);
            variant = Mathf.Max(1, variant);
            if (optionTab == 0) t.optionSkill1 = variant;
            else t.optionSkill2 = variant;
            Commit();
        }

        public static int GetOption(string id, int optionTab)
        {
            TankSave t = GetTank(id);
            return optionTab == 0 ? t.optionSkill1 : t.optionSkill2;
        }

        // ============================================================
        // PHỤ KIỆN
        // ============================================================

        public static int GetOwned(int accessoryId)
        {
            EnsureLoaded();
            AccessoryStackSave s = FindStack(accessoryId);
            return s != null ? s.count : 0;
        }

        /// <summary>Số bản đang gắn trên các xe khác (trừ <paramref name="exceptTankId"/>).</summary>
        public static int GetEquippedCount(int accessoryId, string exceptTankId = null)
        {
            EnsureLoaded();
            int n = 0;
            for (int i = 0; i < Current.tanks.Count; i++)
            {
                TankSave t = Current.tanks[i];
                if (t.equipmentId == accessoryId && t.id != exceptTankId) n++;
            }
            return n;
        }

        /// <summary>Số bản có thể gắn cho xe <paramref name="forTankId"/> (gồm cả bản xe đó đang gắn).</summary>
        public static int GetAvailable(int accessoryId, string forTankId)
            => Mathf.Max(0, GetOwned(accessoryId) - GetEquippedCount(accessoryId, forTankId));

        public static bool EquipAccessory(string tankId, int accessoryId)
        {
            if (GetAvailable(accessoryId, tankId) <= 0) return false;
            GetTank(tankId).equipmentId = accessoryId;
            Commit();
            return true;
        }

        public static void UnequipAccessory(string tankId)
        {
            TankSave t = GetTank(tankId);
            if (t.equipmentId < 0) return;
            t.equipmentId = -1;
            Commit();
        }

        /// <summary>Phân rã phụ kiện đang gắn: mất 1 bản, nhận Ngọc.</summary>
        public static bool DismantleEquipped(string tankId, int gemsReward)
        {
            TankSave t = GetTank(tankId);
            if (t.equipmentId < 0) return false;

            RemoveAccessoryInternal(t.equipmentId, 1);
            t.equipmentId = -1;
            Current.gems += Mathf.Max(0, gemsReward);
            Commit();
            return true;
        }

        public static void AddAccessory(int accessoryId, int count = 1)
        {
            EnsureLoaded();
            AddAccessoryInternal(accessoryId, count);
            Commit();
        }

        private static AccessoryStackSave FindStack(int id)
        {
            for (int i = 0; i < Current.accessories.Count; i++)
                if (Current.accessories[i].id == id) return Current.accessories[i];
            return null;
        }

        private static void AddAccessoryInternal(int id, int count)
        {
            if (count <= 0) return;
            AccessoryStackSave s = FindStack(id);
            if (s == null) Current.accessories.Add(new AccessoryStackSave { id = id, count = count });
            else s.count += count;
        }

        private static void RemoveAccessoryInternal(int id, int count)
        {
            AccessoryStackSave s = FindStack(id);
            if (s == null) return;
            s.count -= count;
            if (s.count <= 0) Current.accessories.Remove(s);
        }

        // ============================================================
        // ẢI / TIẾN ĐỘ
        // ============================================================

        public static bool IsStageCleared(string stageId)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(stageId) && Current.clearedStageIds.Contains(stageId);
        }

        /// <summary>Ải đầu luôn mở; ải sau mở khi ải liền trước (theo thứ tự trong database) đã hoàn thành.</summary>
        public static bool IsStageUnlocked(StageDatabase db, int index)
        {
            if (db == null || index < 0 || index >= db.stages.Count) return false;
            if (index == 0) return true;
            return IsStageCleared(db.stages[index - 1].id);
        }

        public static int ClearedCount(StageDatabase db)
        {
            if (db == null) return 0;
            int n = 0;
            for (int i = 0; i < db.stages.Count; i++)
                if (IsStageCleared(db.stages[i].id)) n++;
            return n;
        }

        public static void SetSelectedStage(string stageId)
        {
            EnsureLoaded();
            if (Current.selectedStageId == stageId) return;
            Current.selectedStageId = stageId;
            Save();
        }

        public static void SetCoopMode(bool coop)
        {
            EnsureLoaded();
            if (Current.coopMode == coop) return;
            Current.coopMode = coop;
            Save();
        }

        /// <summary>
        /// Gọi khi người chơi THẮNG ải: đánh dấu hoàn thành, cộng Ngọc, thử rơi phụ kiện,
        /// mở khóa xe (lần đầu). Thường được gọi qua <see cref="GameSession.ReportVictory"/>.
        ///
        /// Rơi phụ kiện (GDD 5.6): trúng tỉ lệ <see cref="StageEntry.accessoryDropChance"/> thì nhận
        /// <see cref="StageEntry.accessoryDropCount"/> phụ kiện KHÁC NHAU, chọn ngẫu nhiên trong
        /// <see cref="StageEntry.accessoryPool"/> (trống = tất cả).
        /// </summary>
        public static StageResult CompleteStage(StageEntry stage, AccessoryCatalog accessories, TankCatalog tanks)
        {
            EnsureLoaded();
            var result = new StageResult
            {
                Gems = 0,
                AccessoryIds = new int[0],
                UnlockedTankIds = new string[0],
            };
            if (stage == null) return result;

            // --- Mở khóa xe lần đầu ---
            bool firstClear = !Current.clearedStageIds.Contains(stage.id);
            if (firstClear)
            {
                Current.clearedStageIds.Add(stage.id);

                if (tanks != null)
                {
                    var unlocked = new List<string>();
                    for (int i = 0; i < tanks.tanks.Count; i++)
                    {
                        TankEntry t = tanks.tanks[i];
                        if (t != null && t.unlockStageId == stage.id) unlocked.Add(t.id);
                    }
                    result.UnlockedTankIds = unlocked.ToArray();
                }
            }

            // --- Ngọc ma pháp ---
            result.Gems = Mathf.Max(0, stage.rewardGems);
            Current.gems += result.Gems;

            // --- Phụ kiện ---
            if (accessories != null && UnityEngine.Random.value < stage.accessoryDropChance)
            {
                var pool = new List<int>();
                if (stage.accessoryPool != null)
                {
                    for (int i = 0; i < stage.accessoryPool.Count; i++)
                        if (accessories.Find(stage.accessoryPool[i]) != null && !pool.Contains(stage.accessoryPool[i]))
                            pool.Add(stage.accessoryPool[i]);
                }

                if (pool.Count == 0)
                {
                    IReadOnlyList<AccessoryEntry> all = accessories.All;
                    for (int i = 0; i < all.Count; i++) pool.Add(all[i].equipmentId);
                }

                int count = Mathf.Min(Mathf.Max(1, stage.accessoryDropCount), pool.Count);
                var dropped = new List<int>(count);
                for (int n = 0; n < count; n++)
                {
                    int pick = UnityEngine.Random.Range(0, pool.Count);
                    dropped.Add(pool[pick]);
                    pool.RemoveAt(pick); // không nhận trùng trong cùng một lần rơi
                }

                for (int i = 0; i < dropped.Count; i++) AddAccessoryInternal(dropped[i], 1);
                result.AccessoryIds = dropped.ToArray();
            }

            Commit();
            return result;
        }
    }
}
