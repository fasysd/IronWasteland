#if UNITY_EDITOR
using System.Collections.Generic;
using IronWasteland.Enemies;
using IronWasteland.Stages;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Kiem tra du lieu that trong 9 scene man: wave nam tren EnemySpawner, vung spawn,
/// vat can, va cac dieu kien rieng cua man 1-4 (3 loi khong tu hoi mau).
///
/// Chay tay: Tools > Iron Wasteland > Validate Stage Data.
/// Chay headless (CI/agent, Unity dang dong):
///   Unity.exe -batchmode -nographics -quit -projectPath &lt;repo&gt; \
///     -executeMethod StageDataValidator.ValidateAll
/// Exit code 0 = tat ca PASS, 1 = co man FAIL.
/// </summary>
public static class StageDataValidator
{
    private static readonly string[] Codes =
    {
        "Stage_1_1", "Stage_1_2", "Stage_1_3", "Stage_1_4",
        "Stage_2_1", "Stage_2_2", "Stage_2_3", "Stage_2_4", "Stage_2_5",
    };

    [MenuItem("Tools/Iron Wasteland/Validate Stage Data")]
    public static void ValidateAll()
    {
        int failures = 0;

        for (int i = 0; i < Codes.Length; i++)
        {
            if (!Validate(Codes[i])) failures++;
        }

        if (failures == 0)
        {
            Debug.Log($"[VALIDATE] PASS {Codes.Length}/{Codes.Length} man.");
        }
        else
        {
            Debug.LogError($"[VALIDATE] FAIL {failures}/{Codes.Length} man.");
        }

        // Trong batchmode, tra exit code de script/agent biet ket qua.
        if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static bool Validate(string code)
    {
        string scenePath = "Assets/Scenes/" + code + ".unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var problems = new List<string>();

        var director = Object.FindAnyObjectByType<StageDirector>(FindObjectsInactive.Include);
        var spawner = Object.FindAnyObjectByType<EnemySpawner>(FindObjectsInactive.Include);
        var obstacles = Object.FindAnyObjectByType<StageObstacleField>(FindObjectsInactive.Include);

        if (director == null) problems.Add("thieu StageDirector");
        else
        {
            if (director.Definition == null) problems.Add("StageDirector chua gan Definition");
            if ((director.ObjectiveSpecs == null || director.ObjectiveSpecs.Count == 0) && director.KillTarget <= 0)
            {
                problems.Add("StageDirector chua khai bao dieu kien thang");
            }
        }

        if (spawner == null)
        {
            problems.Add("thieu EnemySpawner");
        }
        else
        {
            if (spawner.TotalWaves <= 0) problems.Add("EnemySpawner khong co wave");
            if (spawner.TotalEnemyCount <= 0) problems.Add("tong quai = 0");
            if (spawner.SpawnZones == null || spawner.SpawnZones.Count == 0) problems.Add("khong co SpawnZone");

            for (int w = 0; w < spawner.Waves.Count; w++)
            {
                EnemyWave wave = spawner.Waves[w];
                if (wave == null) { problems.Add($"wave {w} null"); continue; }
                if (wave.Spawns == null || wave.Spawns.Count == 0) problems.Add($"wave {w} rong");
                if (wave.Spawns == null) continue;

                for (int s = 0; s < wave.Spawns.Count; s++)
                {
                    if (wave.Spawns[s] == null || wave.Spawns[s].Enemy == null)
                    {
                        problems.Add($"wave {w} spawn {s} thieu prefab quai");
                    }
                }
            }
        }

        if (obstacles == null) problems.Add("thieu StageObstacleField");

        int cores = 0;
        if (code == "Stage_1_4")
        {
            var targets = Object.FindObjectsByType<DestructibleTarget>(FindObjectsInactive.Include);
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i].TargetId != "core") continue;

                cores++;
                var brain = targets[i].GetComponent<EnemyBrain>();
                if (brain == null) { problems.Add("loi core khong co EnemyBrain"); continue; }

                var so = new SerializedObject(brain);
                SerializedProperty includeSelf = so.FindProperty("includeSelf");
                SerializedProperty maxHealth = so.FindProperty("stats.MaxHealth");

                if (includeSelf != null && includeSelf.boolValue)
                {
                    problems.Add("loi core van includeSelf = true (tu hoi mau)");
                }
                else if (includeSelf == null)
                {
                    problems.Add("khong doc duoc includeSelf tren EnemyBrain (kiem tra kieu component)");
                }

                if (maxHealth != null && maxHealth.floatValue > 350f)
                {
                    problems.Add($"loi core MaxHealth {maxHealth.floatValue} qua cao");
                }
            }

            if (cores != 3) problems.Add($"1-4 co {cores} loi 'core', can dung 3");
        }

        if (problems.Count == 0)
        {
            Debug.Log($"[VALIDATE] PASS {code} ({scene.name}): {spawner.TotalWaves} dot / {spawner.TotalEnemyCount} quai" +
                      (code == "Stage_1_4" ? $", {cores} loi core" : string.Empty));
            return true;
        }

        for (int i = 0; i < problems.Count; i++) Debug.LogError($"[VALIDATE] FAIL {code}: {problems[i]}");
        return false;
    }
}
#endif
