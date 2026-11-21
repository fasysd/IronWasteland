#if UNITY_EDITOR
using System.Collections.Generic;
using IronWasteland.Stages;
using IronWasteland.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Việt hoá chữ hiển thị trong dữ liệu màn chơi:
///  - stageName của 9 asset Stage_1_1 … Stage_2_5;
///  - displayName của các boss trong scene 1-4, 2-4, 2-5 (thanh máu boss lấy tên này).
///
/// Chạy: Tools > Iron Wasteland > Localize Stage Text.
/// </summary>
public static class StageTextLocalize
{
    private static readonly Dictionary<string, string> StageNames = new Dictionary<string, string>
    {
        { "Stage_1_1", "Sương Mù Tím" },
        { "Stage_1_2", "Hang Đá Gai" },
        { "Stage_1_3", "Dấu Vết Tử Binh" },
        { "Stage_1_4", "Vòng Xoay Không Gian" },
        { "Stage_2_1", "Vành Đai Bão Lửa" },
        { "Stage_2_2", "Đầm Lầy Độc Khí" },
        { "Stage_2_3", "Đột Kích Tiền Đồn" },
        { "Stage_2_4", "Phi Thuyền Hai Mặt" },
        { "Stage_2_5", "Chiến Hạm D1ONK" },
    };

    private static readonly Dictionary<string, string> BossNames = new Dictionary<string, string>
    {
        { "Boss_BongMaTuDia", "Bóng Ma Tử Địa" },
        { "Boss_TuThanNguyenTo", "Tử Thần Nguyên Tố" },
        { "Boss_The2Face", "The2Face" },
        { "Boss_D1ONK", "D1ONK" },
    };

    [MenuItem("Tools/Iron Wasteland/Localize Stage Text")]
    public static void Run()
    {
        int stages = LocalizeStages();
        int bosses = LocalizeBosses();
        Debug.Log($"[LOCALIZE] stageName: {stages}/9, boss displayName: {bosses}");
    }

    private static int LocalizeStages()
    {
        string[] guids = AssetDatabase.FindAssets("t:StageDefinition", new[] { "Assets/Data/Stages" });
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StageDefinition def = AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
            if (def == null) continue;

            string key = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!StageNames.TryGetValue(key, out string name)) continue;

            var so = new SerializedObject(def);
            SerializedProperty prop = so.FindProperty("stageName");
            if (prop == null) continue;

            prop.stringValue = name;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
            count++;
        }

        AssetDatabase.SaveAssets();
        return count;
    }

    private static int LocalizeBosses()
    {
        int count = 0;

        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            if (!entry.path.Contains("/Stage_")) continue;
            if (!entry.path.Contains("1_4") && !entry.path.Contains("2_4") && !entry.path.Contains("2_5")) continue;

            var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
            bool dirty = false;

            EnemyBrain[] brains = Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);
            foreach (EnemyBrain brain in brains)
            {
                if (brain.GetComponent<BossTag>() == null) continue;
                if (!BossNames.TryGetValue(brain.gameObject.name, out string name)) continue;

                var so = new SerializedObject(brain);
                SerializedProperty prop = so.FindProperty("displayName");
                if (prop == null) continue;

                prop.stringValue = name;
                so.ApplyModifiedPropertiesWithoutUndo();
                dirty = true;
                count++;
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        return count;
    }
}
#endif
