#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IronWasteland.Tanks;

namespace IronWasteland.Menu.EditorTools
{
    /// <summary>
    /// Menu Editor hỗ trợ dựng Menu nhanh:
    ///   IronWasteland/Menu/1. Tạo dữ liệu mẫu từ GDD   -> TankCatalog + AccessoryCatalog + StageDatabase
    ///   IronWasteland/Menu/2. Tạo Scene MainMenu        -> Scene có MenuBootstrap đã gán sẵn dữ liệu
    ///   IronWasteland/Menu/Xóa hồ sơ người chơi         -> test lại từ đầu
    ///
    /// Dữ liệu lấy từ file GddMenuData.json (trích từ Tài liệu thiết kế Game): tên ải, phần thưởng Ngọc,
    /// danh sách phụ kiện có thể rơi, xe mở khóa ở ải nào, tên + thông số kỹ năng của 6 xe.
    /// </summary>
    public static class MenuSampleDataCreator
    {
        private const string DataRoot = "Assets/GameData/Menu";
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        // ---------- Mô hình JSON (khớp GddMenuData.json) ----------

        [Serializable] public class SkillJson { public string name; public string desc; public string energy; public string cooldown; public bool gain; }
        [Serializable] public class StatsJson { public float attack; public float maxHealth; public float maxEnergy; public float defense; public float moveSpeed; public float cooldownReduction; public float damageRate; public float armorPenetration; }
        [Serializable] public class TankJson { public string id; public string name; public string role; public string role2; public SkillJson normal; public SkillJson special; public SkillJson[] option1; public SkillJson[] option2; public StatsJson base1; public StatsJson base90; }
        [Serializable] public class StageJson { public string id; public string name; public string win; public string time; public string unlock; public string kind; public int gems; public int dropCount; public float dropChance; public string[] pool; }
        [Serializable] public class GddJson { public TankJson[] tanks; public StageJson[] stages; }

        // Giá trị tạm, hãy chỉnh lại trong StageDatabase cho hợp độ khó thật.
        private static readonly int[] PlaceholderRecommendedLevels = { 1, 5, 10, 20, 30, 40, 50, 60, 75 };

        // ============================================================
        // 1. DỮ LIỆU MẪU
        // ============================================================

        [MenuItem("IronWasteland/Menu/1. Tạo dữ liệu mẫu từ GDD")]
        public static void CreateSampleData()
        {
            GddJson gdd = LoadJson();
            if (gdd == null) return;

            EnsureFolder("Assets/GameData");
            EnsureFolder(DataRoot);

            string tankPath = DataRoot + "/TankCatalog.asset";
            string accPath = DataRoot + "/AccessoryCatalog.asset";
            string stagePath = DataRoot + "/StageDatabase.asset";

            bool exists = File.Exists(tankPath) || File.Exists(accPath) || File.Exists(stagePath);
            if (exists && !EditorUtility.DisplayDialog("Ghi đè dữ liệu Menu?",
                    "Đã có dữ liệu Menu trong " + DataRoot + ".\nGhi đè sẽ mất các chỉnh sửa tay (icon, ảnh xe...).",
                    "Ghi đè", "Hủy"))
                return;

            AccessoryCatalog acc = LoadOrCreate<AccessoryCatalog>(accPath);
            AssignDatabase(acc);

            var warnings = new List<string>();
            TankCatalog tanks = LoadOrCreate<TankCatalog>(tankPath);
            FillTanks(tanks, gdd, warnings);

            StageDatabase stages = LoadOrCreate<StageDatabase>(stagePath);
            FillStages(stages, gdd, acc, warnings);

            EditorUtility.SetDirty(acc);
            EditorUtility.SetDirty(tanks);
            EditorUtility.SetDirty(stages);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = tanks;
            EditorGUIUtility.PingObject(tanks);

            Debug.Log($"[Menu] Đã tạo dữ liệu mẫu trong {DataRoot}: {tanks.tanks.Count} xe, {stages.stages.Count} ải." +
                      (warnings.Count > 0 ? "\nLưu ý:\n - " + string.Join("\n - ", warnings) : ""));
        }

        private static GddJson LoadJson()
        {
            string[] guids = AssetDatabase.FindAssets("GddMenuData t:TextAsset");
            if (guids.Length == 0)
            {
                Debug.LogError("[Menu] Không tìm thấy GddMenuData.json (phải nằm trong thư mục Assets).");
                return null;
            }

            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return JsonUtility.FromJson<GddJson>(text.text);
        }

        private static void AssignDatabase(AccessoryCatalog acc)
        {
            string[] guids = AssetDatabase.FindAssets("t:EquipmentDatabase");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[Menu] Không thấy EquipmentDatabase trong project. Hãy gán tay vào AccessoryCatalog.");
                return;
            }

            var db = AssetDatabase.LoadAssetAtPath<EquipmentDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var so = new SerializedObject(acc);
            so.FindProperty("database").objectReferenceValue = db;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Xe ----------

        private static void FillTanks(TankCatalog catalog, GddJson gdd, List<string> warnings)
        {
            // Tìm sẵn TankDefinition trong project để gán theo tên.
            var definitions = new List<TankDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:TankDefinition"))
            {
                var d = AssetDatabase.LoadAssetAtPath<TankDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (d != null) definitions.Add(d);
            }

            // Tìm sẵn các prefab có TankView (tên chứa "_View").
            var views = new List<TankView>();
            foreach (string guid in AssetDatabase.FindAssets("_View t:Prefab"))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go == null) continue;
                var view = go.GetComponent<TankView>();
                if (view != null) views.Add(view);
            }

            catalog.tanks.Clear();
            foreach (TankJson t in gdd.tanks)
            {
                var e = new TankEntry
                {
                    id = t.id,
                    displayName = t.name,
                    roleLine = (RoleVi(t.role) + " / " + RoleVi(t.role2)).ToUpperInvariant(),
                    subtitle = t.id == "black_shadow" ? "SÁT THỦ CƠ ĐỘNG" : "",
                    tags = new[] { RoleVi(t.role).ToUpperInvariant(), RoleVi(t.role2).ToUpperInvariant() },
                    unlockStageId = FindUnlockStage(gdd, t.id),
                    normalSkill = ToSkill(t.normal, true),
                    specialSkill = ToSkill(t.special, false),
                    option1 = ToOptions(t.option1),
                    option2 = ToOptions(t.option2),
                };

                string key = AccessoryCatalog.NormalizeName(t.name);
                foreach (TankDefinition d in definitions)
                {
                    if (AccessoryCatalog.NormalizeName(d.name).Contains(key)) { e.definition = d; break; }
                }
                if (e.definition == null)
                    warnings.Add($"Chưa tìm thấy TankDefinition cho {t.name} (hãy gán tay `definition`, `portrait`, `listIcon`).");

                // Prefab TankView (vd Leviathan_View) để Menu hiển thị xe thật.
                foreach (TankView v in views)
                {
                    if (AccessoryCatalog.NormalizeName(v.name).Contains(key)) { e.viewPrefab = v; break; }
                }
                if (e.viewPrefab == null)
                    warnings.Add($"Chưa tìm thấy prefab *_View cho {t.name}: Menu sẽ thử đọc từ TankDefinition, nếu không được hãy gán tay `viewPrefab`.");

                catalog.tanks.Add(e);
            }
        }

        private static string RoleVi(string role)
        {
            if (string.IsNullOrEmpty(role)) return "";
            return role.Trim().Equals("Support", StringComparison.OrdinalIgnoreCase) ? "Hỗ trợ" : role.Trim();
        }

        private static string FindUnlockStage(GddJson gdd, string tankId)
        {
            foreach (StageJson s in gdd.stages)
                if (s.unlock == tankId) return s.id;
            return "";
        }

        private static SkillUIInfo ToSkill(SkillJson j, bool isNormal)
        {
            var info = new SkillUIInfo();
            if (j == null) return info;

            info.displayName = j.name;
            info.description = j.desc;
            info.cooldownText = string.IsNullOrEmpty(j.cooldown) ? "-" : j.cooldown;
            info.energyText = string.IsNullOrEmpty(j.energy) ? "-" : (j.gain ? "+" + j.energy : j.energy);

            if (j.gain || isNormal) info.costLabel = "KHÔNG TỐN NĂNG LƯỢNG";
            else info.costLabel = "TỐN NĂNG LƯỢNG";
            return info;
        }

        private static SkillUIInfo[] ToOptions(SkillJson[] list)
        {
            var result = new SkillUIInfo[3];
            for (int i = 0; i < 3; i++)
            {
                SkillJson j = list != null && i < list.Length ? list[i] : null;
                SkillUIInfo info = ToSkill(j, false);
                if (j != null) info.costLabel = "";
                result[i] = info;
            }
            return result;
        }

        // ---------- Ải ----------

        private static void FillStages(StageDatabase db, GddJson gdd, AccessoryCatalog acc, List<string> warnings)
        {
            db.stages.Clear();

            for (int i = 0; i < gdd.stages.Length; i++)
            {
                StageJson s = gdd.stages[i];
                bool chapter1 = s.id.StartsWith("1-");

                string chapter = chapter1 ? "CHƯƠNG 1 · LỤC ĐỊA ĐEN"
                               : s.id == "2-5" ? "CHƯƠNG 2 · KỲ HẠM HIRA"
                               : "CHƯƠNG 2 · VÙNG ĐẤT NGUYÊN TỐ";

                string desc = "Điều kiện thắng: " + Sentence(s.win) + ".";
                if (!string.IsNullOrEmpty(s.time)) desc += " Thời lượng " + s.time + ".";

                var e = new StageEntry
                {
                    id = s.id,
                    displayName = s.name,
                    chapterLabel = chapter,
                    description = desc,
                    sceneName = "Stage_" + s.id.Replace('-', '_'),
                    kind = s.kind == "final" ? StageKind.FinalBoss : s.kind == "elite" ? StageKind.Elite : StageKind.Normal,
                    recommendedLevel = i < PlaceholderRecommendedLevels.Length ? PlaceholderRecommendedLevels[i] : 1,
                    rewardGems = s.gems,
                    accessoryDropChance = s.dropChance,
                    accessoryDropCount = Mathf.Max(1, s.dropCount),
                    accessoryPool = new List<int>(),
                };

                if (s.pool != null)
                {
                    foreach (string name in s.pool)
                    {
                        int id = acc.FindIdByName(name);
                        if (id >= 0) e.accessoryPool.Add(id);
                        else warnings.Add($"Ải {s.id}: không khớp được phụ kiện \"{name}\" với EquipmentDatabase (kiểm tra displayName trong prefab).");
                    }
                }

                db.stages.Add(e);
            }
        }

        private static string Sentence(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            // GDD viết hoa toàn bộ ở ải Boss -> hạ xuống cho dễ đọc.
            bool allUpper = s == s.ToUpperInvariant();
            string t = allUpper ? s.ToLowerInvariant() : s;
            return char.ToUpperInvariant(t[0]) + t.Substring(1);
        }

        // ============================================================
        // 2. SCENE MAINMENU
        // ============================================================

        [MenuItem("IronWasteland/Menu/2. Tạo Scene MainMenu")]
        public static void CreateMenuScene()
        {
            var tanks = AssetDatabase.LoadAssetAtPath<TankCatalog>(DataRoot + "/TankCatalog.asset");
            var acc = AssetDatabase.LoadAssetAtPath<AccessoryCatalog>(DataRoot + "/AccessoryCatalog.asset");
            var stages = AssetDatabase.LoadAssetAtPath<StageDatabase>(DataRoot + "/StageDatabase.asset");

            if (tanks == null || acc == null || stages == null)
            {
                EditorUtility.DisplayDialog("Thiếu dữ liệu",
                    "Hãy chạy \"1. Tạo dữ liệu mẫu từ GDD\" trước.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera + AudioListener (Canvas Overlay không cần camera nhưng Unity sẽ cảnh báo nếu thiếu).
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UITheme.Bg;
            cam.orthographic = true;

            var go = new GameObject("MenuBootstrap");
            var bootstrap = go.AddComponent<MenuBootstrap>();
            var so = new SerializedObject(bootstrap);
            so.FindProperty("tankCatalog").objectReferenceValue = tanks;
            so.FindProperty("accessoryCatalog").objectReferenceValue = acc;
            so.FindProperty("stageDatabase").objectReferenceValue = stages;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath, 0);

            Debug.Log($"[Menu] Đã tạo {ScenePath} và đặt làm Scene đầu trong Build Settings. " +
                      "Nhớ thêm các Scene ải (Stage_1_1...) vào Build Settings và gán font tiếng Việt cho MenuBootstrap.");
        }

        private static void AddSceneToBuildSettings(string path, int index)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == path);
            list.Insert(Mathf.Clamp(index, 0, list.Count), new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ============================================================
        // TIỆN ÍCH
        // ============================================================

        [MenuItem("IronWasteland/Menu/Xóa hồ sơ người chơi (test lại từ đầu)")]
        public static void ResetProfile()
        {
            if (!EditorUtility.DisplayDialog("Xóa hồ sơ?",
                    "Xóa file lưu (Ngọc, cấp xe, phụ kiện, tiến độ ải) và cài đặt âm thanh / phím?", "Xóa", "Hủy"))
                return;

            string path = ProfileService.FilePath;
            if (File.Exists(path)) File.Delete(path);
            PlayerPrefs.DeleteKey("IronWasteland.Settings.v1");
            PlayerPrefs.Save();
            Debug.Log("[Menu] Đã xóa hồ sơ: " + path);
        }

        // ============================================================
        // 3. CHỈ SỐ CƠ BẢN TỪ GDD -> TankDefinition
        // ============================================================

        [MenuItem("IronWasteland/Menu/3. Điền chỉ số cơ bản từ GDD vào TankDefinition")]
        public static void FillBaseStatsFromGdd()
        {
            GddJson gdd = LoadJson();
            if (gdd == null) return;

            var catalog = AssetDatabase.LoadAssetAtPath<TankCatalog>(DataRoot + "/TankCatalog.asset");

            var jsons = new List<TankJson>();
            var defs = new List<TankDefinition>();
            foreach (TankJson t in gdd.tanks)
            {
                if (t.base1 == null || t.base90 == null) continue;

                TankDefinition def = null;
                TankEntry entry = catalog != null ? catalog.Find(t.id) : null;
                if (entry != null) def = entry.definition;
                if (def == null) def = FindDefinitionByName(t.name);

                if (def == null) { Debug.LogWarning($"[Menu] Chưa tìm thấy TankDefinition cho {t.name}, bỏ qua."); continue; }
                jsons.Add(t);
                defs.Add(def);
            }

            if (defs.Count == 0)
            {
                EditorUtility.DisplayDialog("Không có xe nào", "Không tìm thấy TankDefinition nào để điền.", "OK");
                return;
            }

            string names = "";
            for (int i = 0; i < jsons.Count; i++) names += "\n - " + jsons[i].name + "  (" + defs[i].name + ")";

            if (!EditorUtility.DisplayDialog("Ghi đè chỉ số cơ bản?",
                    "Sẽ ghi chỉ số cấp 1 và cấp 90 (theo bảng 'Chỉ số cơ bản' trong GDD) vào:" + names +
                    "\n\nGiá trị đang nhập trong các asset này sẽ bị thay.", "Ghi đè", "Hủy"))
                return;

            int ok = 0;
            for (int i = 0; i < defs.Count; i++)
            {
                var so = new SerializedObject(defs[i]);
                SerializedProperty p1 = FindStatsProperty(so, "1");
                SerializedProperty p90 = FindStatsProperty(so, "90");
                if (p1 == null || p90 == null)
                {
                    Debug.LogWarning($"[Menu] {defs[i].name}: không thấy hai field TankStats kết thúc bằng Level1 / Level90.", defs[i]);
                    continue;
                }

                WriteStats(p1, jsons[i].base1);
                WriteStats(p90, jsons[i].base90);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(defs[i]);
                ok++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Menu] Đã điền chỉ số cơ bản GDD cho {ok}/{defs.Count} TankDefinition.");
        }

        private static TankDefinition FindDefinitionByName(string tankName)
        {
            string key = AccessoryCatalog.NormalizeName(tankName);
            foreach (string guid in AssetDatabase.FindAssets("t:TankDefinition"))
            {
                var d = AssetDatabase.LoadAssetAtPath<TankDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (d != null && AccessoryCatalog.NormalizeName(d.name).Contains(key)) return d;
            }
            return null;
        }

        private static SerializedProperty FindStatsProperty(SerializedObject so, string level)
        {
            string a = "level" + level;
            string b = "lv" + level;

            SerializedProperty it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false; // chỉ duyệt thuộc tính cấp cao nhất
                if (it.propertyType != SerializedPropertyType.Generic || it.type != "TankStats") continue;

                string n = AccessoryCatalog.NormalizeName(it.name);
                if (n.EndsWith(a, StringComparison.Ordinal) || n.EndsWith(b, StringComparison.Ordinal)) return it.Copy();
            }
            return null;
        }

        private static void WriteStats(SerializedProperty p, StatsJson s)
        {
            Set(p, "Attack", s.attack);
            Set(p, "Defense", s.defense);
            Set(p, "DamageRate", s.damageRate);
            Set(p, "ArmorPenetration", s.armorPenetration);
            Set(p, "MaxHealth", s.maxHealth);
            Set(p, "MaxEnergy", s.maxEnergy);
            Set(p, "MoveSpeed", s.moveSpeed);
            Set(p, "CooldownReduction", s.cooldownReduction);
        }

        private static void Set(SerializedProperty parent, string field, float value)
        {
            SerializedProperty f = parent.FindPropertyRelative(field);
            if (f != null) f.floatValue = value;
        }

        [MenuItem("IronWasteland/Menu/Thêm 500 Ngọc ma pháp (test)")]
        public static void AddTestGems()
        {
            ProfileService.EnsureLoaded();
            ProfileService.AddGems(500);
            Debug.Log($"[Menu] Đã thêm 500 Ngọc ma pháp. Hiện có {ProfileService.Gems}.");
        }

        [MenuItem("IronWasteland/Menu/Mở thư mục lưu hồ sơ")]
        public static void RevealSaveFolder() => EditorUtility.RevealInFinder(Application.persistentDataPath);

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
