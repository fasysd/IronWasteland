#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using IronWasteland.Stages;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// QA tu dong cho 9 man: kiem tra asset man + scene + chuoi nextStageCode.
/// Tu 2026-11, dot quai nam tren component EnemySpawner trong scene (khong con trong
/// StageDefinition), nen audit phai mo scene theo kieu additive de doc spawner.
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

            int objCount = so.FindProperty("objectives").arraySize;

            WaveStats waves = ReadWavesFromScene(scenePath);

            bool objOk = objCount > 0;
            bool waveOk = waves.WaveCount > 0 && waves.BadSpawns == 0;
            bool sceneOk = File.ReadAllText(scenePath).Contains(AssetDatabase.AssetPathToGUID(assetPath));

            string status = objOk && waveOk && sceneOk ? "PASS" : "FAIL";
            if (status == "PASS")
            {
                pass++;
            }
            else
            {
                problems.Add(code + ": obj=" + objOk + " wave=" + waveOk + " scene=" + sceneOk);
            }

            sb.AppendLine(string.Format(
                "  {0} | {1,-10} | {2,-24} | obj={3} wave={4} spawn={5} ngoc={6} next={7} scene={8} => {9}",
                code, stageCode, stageName, objCount, waves.WaveCount, waves.SpawnTotal, gems,
                string.IsNullOrEmpty(next) ? "(het)" : next, sceneOk, status));
        }

        sb.AppendLine("[QA] PASS " + pass + "/" + Codes.Length);
        for (int i = 0; i < problems.Count; i++) sb.AppendLine("  ! " + problems[i]);

        Debug.Log("[QA-AUDIT]\n" + sb.ToString());
    }

    private struct WaveStats
    {
        public int WaveCount;
        public int SpawnTotal;
        public int BadSpawns;
    }

    /// <summary>Mo scene additive, dem wave cua EnemySpawner trong scene roi dong lai.</summary>
    private static WaveStats ReadWavesFromScene(string scenePath)
    {
        var stats = new WaveStats();
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;

        if (openedHere)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        try
        {
            EnemySpawner[] spawners = Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include);
            for (int s = 0; s < spawners.Length; s++)
            {
                EnemySpawner spawner = spawners[s];
                if (spawner == null || spawner.gameObject.scene != scene) continue;

                IReadOnlyList<EnemyWave> waves = spawner.Waves;
                if (waves == null) continue;

                for (int w = 0; w < waves.Count; w++)
                {
                    EnemyWave wave = waves[w];
                    if (wave == null) continue;

                    stats.WaveCount++;
                    if (wave.Spawns == null) continue;

                    for (int e = 0; e < wave.Spawns.Count; e++)
                    {
                        stats.SpawnTotal++;
                        if (wave.Spawns[e] == null || wave.Spawns[e].Enemy == null) stats.BadSpawns++;
                    }
                }
            }
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }

        return stats;
    }
}
#endif
