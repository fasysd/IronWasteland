#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sửa lỗi "quái đứng yên, chỉ đuổi khi người chơi lại gần".
///
/// Nguyên nhân: NormalEnemy_BaseChaser quét mục tiêu bằng
/// Physics2D.OverlapCircleAll(anchorPointD, radiusC, tankLayer) — tức chỉ thấy tank trong
/// bán kính C quanh điểm gốc D của prefab (mặc định 10). Ngoài bán kính đó quái đứng yên tại chỗ.
///
/// Cách sửa:
///  - radiusC = 60 (lớn hơn đường chéo bản đồ 37,6 x 21 ≈ 43) để quái thấy tank ở mọi nơi;
///  - autoSetAnchorDFromSpawnPosition = true để điểm D là chỗ quái thực sự xuất hiện
///    (spawner sinh quái ở nhiều vùng khác nhau, không phải toạ độ tác giả đặt trong prefab).
///
/// Chạy: Tools > Iron Wasteland > Fix Enemy Aggro.
/// </summary>
public static class EnemyAggroFix
{
    private const float AggroRadius = 60f;

    [MenuItem("Tools/Iron Wasteland/Fix Enemy Aggro")]
    public static void Fix()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Enemies" });
        int touched = 0;
        var report = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            bool changed = false;
            string detail = null;

            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour mb = behaviours[i];
                if (mb == null) continue;

                var so = new SerializedObject(mb);
                SerializedProperty radius = so.FindProperty("radiusC");
                if (radius == null) continue;

                SerializedProperty autoD = so.FindProperty("autoSetAnchorDFromSpawnPosition");

                so.FindProperty("radiusC").floatValue = AggroRadius;
                if (autoD != null) autoD.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                changed = true;
                detail = System.IO.Path.GetFileNameWithoutExtension(path) + " (" + mb.GetType().Name + ")";
                break;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                touched++;
                report.Add(detail);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[AGGRO] da sua {touched} prefab quái:\n  " + string.Join("\n  ", report));
    }
}
#endif
