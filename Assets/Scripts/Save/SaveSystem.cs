using System;
using System.Collections.Generic;
using System.IO;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Save
{
    /// <summary>
    /// Hệ thống quản lý lưu trữ JSON trung tâm của game.
    /// Tự động khởi tạo (Lazy Singleton & BeforeSceneLoad) đảm bảo luôn sẵn sàng trong mọi Scene.
    /// Phân tách 2 file lưu:
    /// - gamedata.json: Ngọc ma pháp, thông tin xe, phụ kiện sở hữu, tiến trình màn chơi.
    /// - device_settings.json: Cài đặt âm thanh, xe chọn cuối cùng.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public class SaveSystem : MonoBehaviour
    {
        private const string GameDataFileName = "gamedata.json";
        private const string DeviceSettingsFileName = "device_settings.json";

        private static SaveSystem s_Instance;

        public static SaveSystem Instance
        {
            get
            {
                EnsureInstance();
                return s_Instance;
            }
        }

        private string m_GameDataPath;
        private string m_DeviceSettingsPath;

        public GameData Data { get; private set; }
        public SettingsSaveData Settings { get; private set; }

        private bool m_Initialized = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (s_Instance != null)
            {
                if (string.IsNullOrEmpty(s_Instance.m_GameDataPath) || s_Instance.Data == null)
                {
                    s_Instance.InitializePaths();
                    s_Instance.LoadAll();
                }
                return;
            }

            s_Instance = FindFirstObjectByType<SaveSystem>();
            if (s_Instance == null)
            {
                var go = new GameObject("[SaveSystem]");
                s_Instance = go.AddComponent<SaveSystem>();
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(go);
                }
            }

            s_Instance.InitializePaths();
            s_Instance.LoadAll();
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            s_Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            InitializePaths();
            LoadAll();
        }

        private void InitializePaths()
        {
            m_GameDataPath = Path.Combine(Application.persistentDataPath, GameDataFileName);
            m_DeviceSettingsPath = Path.Combine(Application.persistentDataPath, DeviceSettingsFileName);
        }

        #region Load & Save

        public void LoadAll()
        {
            if (string.IsNullOrEmpty(m_GameDataPath))
            {
                InitializePaths();
            }

            LoadGameData();
            LoadDeviceSettings();
            m_Initialized = true;
        }

        private void LoadGameData()
        {
            if (File.Exists(m_GameDataPath))
            {
                try
                {
                    string json = File.ReadAllText(m_GameDataPath);
                    Data = JsonUtility.FromJson<GameData>(json);
                    if (Data == null) Data = CreateDefaultGameData();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveSystem] Lỗi đọc {GameDataFileName}: {e.Message}. Tạo mặc định.");
                    Data = CreateDefaultGameData();
                }
            }
            else
            {
                Data = CreateDefaultGameData();
            }

            EnsureTanksInitialized();
        }

        private void LoadDeviceSettings()
        {
            if (File.Exists(m_DeviceSettingsPath))
            {
                try
                {
                    string json = File.ReadAllText(m_DeviceSettingsPath);
                    Settings = JsonUtility.FromJson<SettingsSaveData>(json);
                    if (Settings == null) Settings = new SettingsSaveData();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveSystem] Lỗi đọc {DeviceSettingsFileName}: {e.Message}. Tạo mặc định.");
                    Settings = new SettingsSaveData();
                }
            }
            else
            {
                Settings = new SettingsSaveData();
            }
        }

        public void SaveGameData()
        {
            if (Data == null) return;
            try
            {
                string json = JsonUtility.ToJson(Data, true);
                File.WriteAllText(m_GameDataPath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Lỗi ghi {GameDataFileName}: {e.Message}");
            }
        }

        public void SaveDeviceSettings()
        {
            if (Settings == null) return;
            try
            {
                string json = JsonUtility.ToJson(Settings, true);
                File.WriteAllText(m_DeviceSettingsPath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Lỗi ghi {DeviceSettingsFileName}: {e.Message}");
            }
        }

        public void SaveAll()
        {
            SaveGameData();
            SaveDeviceSettings();
        }

        private void OnApplicationQuit()
        {
            SaveAll();
        }

        #endregion

        #region Tank Data & Operations

        public TankSaveData GetTankSave(int tankIndex)
        {
            EnsureTanksInitialized();
            if (tankIndex < 0 || tankIndex >= Data.Tanks.Count) return null;
            return Data.Tanks[tankIndex];
        }

        public int GetTankLevel(int tankIndex)
        {
            var tank = GetTankSave(tankIndex);
            return tank != null ? tank.Level : 0;
        }

        public bool IsTankOwned(int tankIndex)
        {
            return GetTankLevel(tankIndex) > 0;
        }

        public void UnlockTank(int tankIndex)
        {
            EnsureTanksInitialized();
            var tank = GetTankSave(tankIndex);
            if (tank != null && tank.Level == 0)
            {
                tank.Level = 1;
                SaveGameData();
            }
        }

        public void SetTankLevel(int tankIndex, int level)
        {
            EnsureTanksInitialized();
            var tank = GetTankSave(tankIndex);
            if (tank != null)
            {
                tank.Level = Mathf.Clamp(level, 0, TankDefinition.MaxLevel);
                SaveGameData();
            }
        }

        public bool UpgradeTank(int tankIndex)
        {
            var tank = GetTankSave(tankIndex);
            if (tank == null || tank.Level <= 0) return false;
            if (tank.Level >= TankDefinition.MaxLevel) return false;

            int cost = TankUpgradeUtility.GetCostForNextLevel(tank.Level);
            if (Data.Gems < cost) return false;

            Data.Gems -= cost;
            tank.Level++;
            SaveGameData();
            return true;
        }

        public int GetEquippedAccessory(int tankIndex)
        {
            var tank = GetTankSave(tankIndex);
            return tank != null ? tank.EquippedAccessoryIndex : -1;
        }

        public void SetEquippedAccessory(int tankIndex, int accessoryIndex)
        {
            EnsureTanksInitialized();
            var tank = GetTankSave(tankIndex);
            if (tank != null)
            {
                tank.EquippedAccessoryIndex = accessoryIndex >= 0 ? accessoryIndex : -1;
                SaveGameData();
            }
        }

        public int GetOptionSkill(int tankIndex, int tabIndex)
        {
            var tank = GetTankSave(tankIndex);
            if (tank == null) return 0;
            return tabIndex == 0 ? tank.OptionSkill1 : tank.OptionSkill2;
        }

        public void SetOptionSkill(int tankIndex, int tabIndex, int optionIndex)
        {
            EnsureTanksInitialized();
            var tank = GetTankSave(tankIndex);
            if (tank != null)
            {
                if (tabIndex == 0) tank.OptionSkill1 = Mathf.Clamp(optionIndex, 0, 2);
                else tank.OptionSkill2 = Mathf.Clamp(optionIndex, 0, 2);
                SaveGameData();
            }
        }

        #endregion

        #region Owned Accessories

        public int GetAccessoryQuantity(int accessoryIndex)
        {
            if (Data?.Accessories == null || accessoryIndex < 0) return 0;
            for (int i = 0; i < Data.Accessories.Count; i++)
            {
                if (Data.Accessories[i].AccessoryIndex == accessoryIndex)
                    return Data.Accessories[i].Quantity;
            }
            return 0;
        }

        public bool HasAccessory(int accessoryIndex)
        {
            return GetAccessoryQuantity(accessoryIndex) > 0;
        }

        public void AddAccessory(int accessoryIndex, int count = 1)
        {
            if (accessoryIndex < 0 || count <= 0) return;
            if (Data == null) Data = CreateDefaultGameData();
            if (Data.Accessories == null) Data.Accessories = new List<AccessorySaveData>();

            for (int i = 0; i < Data.Accessories.Count; i++)
            {
                if (Data.Accessories[i].AccessoryIndex == accessoryIndex)
                {
                    Data.Accessories[i].Quantity += count;
                    SaveGameData();
                    return;
                }
            }

            Data.Accessories.Add(new AccessorySaveData { AccessoryIndex = accessoryIndex, Quantity = count });
            SaveGameData();
        }

        public bool ConsumeAccessory(int accessoryIndex, int count = 1)
        {
            if (accessoryIndex < 0 || count <= 0 || Data?.Accessories == null) return false;

            for (int i = 0; i < Data.Accessories.Count; i++)
            {
                if (Data.Accessories[i].AccessoryIndex == accessoryIndex)
                {
                    if (Data.Accessories[i].Quantity < count) return false;
                    Data.Accessories[i].Quantity -= count;
                    if (Data.Accessories[i].Quantity <= 0)
                    {
                        Data.Accessories.RemoveAt(i);
                    }
                    SaveGameData();
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<AccessorySaveData> GetOwnedAccessories()
        {
            return Data?.Accessories ?? (IReadOnlyList<AccessorySaveData>)Array.Empty<AccessorySaveData>();
        }

        #endregion

        #region Gems

        public int Gems => Data?.Gems ?? 0;

        public void AddGems(int amount)
        {
            if (amount <= 0) return;
            if (Data == null) Data = CreateDefaultGameData();
            Data.Gems += amount;
            SaveGameData();
        }

        public bool SpendGems(int amount)
        {
            if (amount <= 0) return false;
            if (Data == null || Data.Gems < amount) return false;
            Data.Gems -= amount;
            SaveGameData();
            return true;
        }

        #endregion

        #region Stage Progress

        public bool IsStageUnlocked(string stageCode)
        {
            return Data?.StageProgress?.IsUnlocked(stageCode) ?? false;
        }

        public bool IsStageCleared(string stageCode)
        {
            return Data?.StageProgress?.IsCleared(stageCode) ?? false;
        }

        public float GetBestSeconds(string stageCode)
        {
            return Data?.StageProgress?.BestSeconds(stageCode) ?? 0f;
        }

        public void MarkCleared(string stageCode, int gems, float seconds, string nextStageCode)
        {
            if (string.IsNullOrEmpty(stageCode)) return;
            if (Data == null) Data = CreateDefaultGameData();

            if (!Data.StageProgress.ClearedStageCodes.Contains(stageCode))
                Data.StageProgress.ClearedStageCodes.Add(stageCode);

            if (!string.IsNullOrEmpty(nextStageCode) && !Data.StageProgress.UnlockedStageCodes.Contains(nextStageCode))
                Data.StageProgress.UnlockedStageCodes.Add(nextStageCode);

            if (seconds > 0f)
                Data.StageProgress.SetBestTime(stageCode, seconds);

            if (gems > 0)
                Data.Gems += gems;

            SaveGameData();
        }

        public void UnlockStage(string stageCode)
        {
            if (string.IsNullOrEmpty(stageCode)) return;
            if (Data == null) Data = CreateDefaultGameData();

            if (!Data.StageProgress.UnlockedStageCodes.Contains(stageCode))
            {
                Data.StageProgress.UnlockedStageCodes.Add(stageCode);
                SaveGameData();
            }
        }

        public static string SceneNameFor(string stageCode)
        {
            if (string.IsNullOrEmpty(stageCode)) return string.Empty;
            return "Stage_" + stageCode.Replace('-', '_');
        }

        #endregion

        #region Device Settings & Utilities

        public void SetSettings(int master, int sfx, int bgm, int announcer)
        {
            if (Settings == null) Settings = new SettingsSaveData();
            Settings.MasterVolume = Mathf.Clamp(master, 0, 100);
            Settings.SfxVolume = Mathf.Clamp(sfx, 0, 100);
            Settings.BgmVolume = Mathf.Clamp(bgm, 0, 100);
            Settings.AnnouncerVolume = Mathf.Clamp(announcer, 0, 100);
            SaveDeviceSettings();
        }

        public int LastSelectedTankIndex
        {
            get => Settings?.LastSelectedTankIndex ?? 0;
            set
            {
                if (Settings == null) Settings = new SettingsSaveData();
                Settings.LastSelectedTankIndex = Mathf.Clamp(value, 0, 5);
                SaveDeviceSettings();
            }
        }

        #endregion

        #region Skill Picker Transient Request

        public int PendingTankIndex = -1;
        public int PendingTabIndex = -1;

        public void RequestSkillPicker(int tankIndex, int tabIndex)
        {
            PendingTankIndex = Mathf.Clamp(tankIndex, 0, 5);
            PendingTabIndex = Mathf.Clamp(tabIndex, 0, 1);
            LastSelectedTankIndex = PendingTankIndex;
        }

        public void ConsumeSkillPickerRequest(out int tankIndex, out int tabIndex)
        {
            tankIndex = PendingTankIndex >= 0 ? PendingTankIndex : LastSelectedTankIndex;
            tabIndex = PendingTabIndex >= 0 ? PendingTabIndex : 0;
            PendingTankIndex = -1;
            PendingTabIndex = -1;
        }

        #endregion

        #region Defaults & Reset

        public void ResetAll()
        {
            Data = CreateDefaultGameData();
            Settings = new SettingsSaveData();
            SaveAll();
        }

        private void EnsureTanksInitialized()
        {
            if (Data == null) Data = CreateDefaultGameData();
            if (Data.Tanks == null) Data.Tanks = new List<TankSaveData>();

            string[] tankIds = { "WallTitan", "RedNova", "BlackShadow", "Leviathan", "WaterB", "GreenFox" };
            for (int i = 0; i < tankIds.Length; i++)
            {
                if (i >= Data.Tanks.Count)
                {
                    int defaultLevel = (i == 0 || i == 1) ? 1 : 0;
                    Data.Tanks.Add(new TankSaveData
                    {
                        TankId = tankIds[i],
                        Level = defaultLevel,
                        EquippedAccessoryIndex = -1,
                        OptionSkill1 = 0,
                        OptionSkill2 = 0
                    });
                }
                else
                {
                    Data.Tanks[i].TankId = tankIds[i];
                }
            }
        }

        private GameData CreateDefaultGameData()
        {
            var data = new GameData
            {
                Gems = 0,
                Tanks = new List<TankSaveData>
                {
                    new TankSaveData { TankId = "WallTitan", Level = 1, EquippedAccessoryIndex = -1 },
                    new TankSaveData { TankId = "RedNova", Level = 1, EquippedAccessoryIndex = -1 },
                    new TankSaveData { TankId = "BlackShadow", Level = 0, EquippedAccessoryIndex = -1 },
                    new TankSaveData { TankId = "Leviathan", Level = 0, EquippedAccessoryIndex = -1 },
                    new TankSaveData { TankId = "WaterB", Level = 0, EquippedAccessoryIndex = -1 },
                    new TankSaveData { TankId = "GreenFox", Level = 0, EquippedAccessoryIndex = -1 }
                },
                Accessories = new List<AccessorySaveData>(),
                StageProgress = new StageProgressSaveData()
            };

            data.StageProgress.UnlockedStageCodes.Add(StageProgressSaveData.FirstStageCode);
            return data;
        }

        #endregion
    }
}
