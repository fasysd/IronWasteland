#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IronWasteland.Stages;
using IronWasteland.Tanks;
using IronWasteland.UI;

/// <summary>
/// Thay HUD trong tat ca scene man choi bang ban moi nhat cua prefab
/// (tranh override cu trong scene de len giao dien vua lam lai),
/// roi noi lai StageHUD.director va TankStatusHUD.gameController.
///
/// Chay: Tools > Iron Wasteland > Refresh HUD In Scenes.
/// </summary>
public static class HudSceneRefresh
{
    private const string PrefabPath = "Assets/Prefabs/UI/StageHUD.prefab";

    [MenuItem("Tools/Iron Wasteland/Refresh HUD In Scenes")]
    public static void Refresh()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[HUD-REFRESH] khong tim thay " + PrefabPath);
            return;
        }

        List<string> scenes = new List<string>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s.path.Contains("/Stage_")) scenes.Add(s.path);
        }

        if (!Application.isPlaying && UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty)
        {
            EditorSceneManager.SaveOpenScenes();
        }

        int done = 0;
        for (int i = 0; i < scenes.Count; i++)
        {
            Scene scene = EditorSceneManager.OpenScene(scenes[i], OpenSceneMode.Single);

            StageHUD old = Object.FindAnyObjectByType<StageHUD>(FindObjectsInactive.Include);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "StageHUD";

            StageHUD hud = instance.GetComponent<StageHUD>();
            TankStatusHUD status = instance.GetComponent<TankStatusHUD>();

            StageDirector director = Object.FindAnyObjectByType<StageDirector>(FindObjectsInactive.Include);
            GameController controller = Object.FindAnyObjectByType<GameController>(FindObjectsInactive.Include);

            if (hud != null && director != null)
            {
                var so = new SerializedObject(hud);
                so.FindProperty("director").objectReferenceValue = director;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (status != null && controller != null)
            {
                var so2 = new SerializedObject(status);
                so2.FindProperty("gameController").objectReferenceValue = controller;
                so2.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            done++;
            Debug.Log("[HUD-REFRESH] " + scene.name + " -> director=" + (director != null) + " gc=" + (controller != null) + " luu=" + saved);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[HUD-REFRESH] xong " + done + " scene");
    }
}
#endif
