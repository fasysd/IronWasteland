#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using IronWasteland.Stages;

/// <summary>
/// Sua cac loi QA cua 9 man: 1-1 thieu objective, chuoi nextStageCode bi dut.
/// Chay: Tools > Iron Wasteland > Fix Stage QA.
/// </summary>
public static class StageQAFix
{
    [MenuItem("Tools/Iron Wasteland/Fix Stage QA")]
    public static void Fix()
    {
        int fixedCount = 0;

        // 1-1 dung kieu cu (killTarget) -> them objective Eliminate cho dong bo.
        fixedCount += AddEliminate("Stage_1_1", 40);

        fixedCount += SetNext("Stage_1_2", "1-3");
        fixedCount += SetNext("Stage_2_1", "2-2");
        fixedCount += SetNext("Stage_2_3", "2-4");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[QA-FIX] da sua " + fixedCount + " muc");
    }

    private static int SetNext(string code, string next)
    {
        StageDefinition def = AssetDatabase.LoadAssetAtPath<StageDefinition>("Assets/Data/Stages/" + code + ".asset");
        if (def == null) { Debug.LogWarning("[QA-FIX] thieu " + code); return 0; }

        SerializedObject so = new SerializedObject(def);
        SerializedProperty p = so.FindProperty("nextStageCode");
        if (p.stringValue == next) return 0;
        p.stringValue = next;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(def);
        Debug.Log("[QA-FIX] " + code + " next=" + next);
        return 1;
    }

    private static int AddEliminate(string code, int count)
    {
        StageDefinition def = AssetDatabase.LoadAssetAtPath<StageDefinition>("Assets/Data/Stages/" + code + ".asset");
        if (def == null) { Debug.LogWarning("[QA-FIX] thieu " + code); return 0; }

        SerializedObject so = new SerializedObject(def);
        SerializedProperty objectives = so.FindProperty("objectives");
        if (objectives.arraySize > 0) return 0;

        objectives.arraySize = 1;
        SerializedProperty e = objectives.GetArrayElementAtIndex(0);
        e.FindPropertyRelative("Type").enumValueIndex = (int)StageObjectiveType.Eliminate;
        e.FindPropertyRelative("Count").intValue = count;
        e.FindPropertyRelative("Duration").floatValue = 0f;
        e.FindPropertyRelative("Distance").floatValue = 0f;
        e.FindPropertyRelative("TargetId").stringValue = string.Empty;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(def);
        Debug.Log("[QA-FIX] " + code + " them objective Eliminate " + count);
        return 1;
    }
}
#endif
