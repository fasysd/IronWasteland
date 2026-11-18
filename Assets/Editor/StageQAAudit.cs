#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using IronWasteland.Stages;

/// <summary>
/// QA tu dong cho 9 man: kiem tra asset man + scene + chuoi nextStageCode.
/// Chay: Tools > Iron Wasteland > QA Audit Stages.
/// </summary>
public static class StageQAAudit
{
    private static readonly string[] Codes =
    {
        "Stage_1_1", "Stage_1_2", "Stage_1_3", "Stage_1_4",
        "Stage_2_1", "Stage_2_2", "Stage_2_3", "Stage_2_4", "Stage_2_5",
    };

    [MenuItem("Tools/Iron Wasteland/QA Audit Stages")]
    public static void Audit()
    {
        StringBuilder sb = new StringBuilder();
        int pass = 0;
        var problems = new List<string>();

        for (int i = 0; i < Codes.Length; i++)
        {
            string code = Codes[i];
            string assetPath = "Assets/Data/Stages/" + code + ".asset";
            string scenePath = "Assets/Scenes/" + code + ".unity";

            StageDefinition def = AssetDatabase.LoadAssetAtPath<StageDefinition>(assetPath);
            if (def == null) { problems.Add(code + ": THIEU ASSET"); continue; }
            if (!File.Exists(scenePath)) { problems.Add(code + ": THIEU SCENE"); continue; }

            SerializedObject so = new SerializedObject(def);
            string stageCode = so.FindProperty("stageCode").stringValue;
            string stageName = so.FindProperty("stageName").stringValue;
            string next = so.FindProperty("nextStageCode").stringValue;
            int gems = so.FindProperty("rewardGems").intValue;

            SerializedProperty objectives = so.FindProperty("objectives");
            SerializedProperty waves = so.FindProperty("waves");

            int objCount = objectives.arraySize;
            int waveCount = waves.arraySize;
            int spawnTotal = 0;
            int badSpawns = 0;

            for (int w = 0; w < waveCount; w++)
            {
                SerializedProperty spawns = waves.GetArrayElementAtIndex(w).FindPropertyRelative("Spawns");
                for (int s = 0; s < spawns.arraySize; s++)
                {
                    SerializedProperty e = spawns.GetArrayElementAtIndex(s).FindPropertyRelative("Enemy");
                    spawnTotal++;
                    if (e.objectReferenceValue == null) badSpawns++;
                }
            }

            bool objOk = objCount > 0;
            bool waveOk = waveCount > 0 && badSpawns == 0;
            bool sceneOk = File.ReadAllText(scenePath).Contains(AssetDatabase.AssetPathToGUID(assetPath));

            string status = objOk && waveOk && sceneOk ? "PASS" : "FAIL";
            if (status == "PASS") pass++; else problems.Add(code + ": obj=" + objOk + " wave=" + waveOk + " scene=" + sceneOk);

            sb.AppendLine(string.Format("  {0} | {1,-10} | {2,-24} | obj={3} wave={4} spawn={5} ngoc={6} next={7} scene={8} => {9}",
                code, stageCode, stageName, objCount, waveCount, spawnTotal, gems,
                string.IsNullOrEmpty(next) ? "(het)" : next, sceneOk, status));
        }

        sb.AppendLine("[QA] PASS " + pass + "/" + Codes.Length);
        for (int i = 0; i < problems.Count; i++) sb.AppendLine("  ! " + problems[i]);

        Debug.Log("[QA-AUDIT]\n" + sb.ToString());
    }
}
#endif
