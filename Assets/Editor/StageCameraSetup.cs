#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IronWasteland.Core;

/// <summary>
/// Chinh camera cua tat ca man choi: keo camera ra xa (orthographic size lon hon) de tank
/// nho lai giong wireframe, va bat gioi han camera theo bien ban do.
///
/// Chay: Tools > Iron Wasteland > Setup Cameras.
/// </summary>
public static class StageCameraSetup
{
    private const float OrthoSize = 7f;

    [MenuItem("Tools/Iron Wasteland/Setup Cameras")]
    public static void Setup()
    {
        int count = 0;

        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            if (!entry.path.Contains("/Stage_")) continue;

            var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
            Camera cam = Camera.main;
            if (cam == null)
            {
                Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                if (cameras.Length > 0) cam = cameras[0];
            }

            if (cam == null)
            {
                Debug.LogWarning("[CAMERA] " + entry.path + " khong co camera");
                continue;
            }

            var so = new SerializedObject(cam);
            so.FindProperty("orthographic").boolValue = true;
            so.FindProperty("orthographic size").floatValue = OrthoSize;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Nền trời mặc định của Unity màu xanh: đổi sang màu tối để phần ngoài bản đồ
            // (nếu có) không còn lộ màu xanh của skybox.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x07, 0x0A, 0x0E, 0xFF);
            EditorUtility.SetDirty(cam);

            if (cam.GetComponent<CameraFollow2D>() == null) cam.gameObject.AddComponent<CameraFollow2D>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            count++;
            Debug.Log($"[CAMERA] {System.IO.Path.GetFileNameWithoutExtension(entry.path)} -> ortho {OrthoSize}");
        }

        Debug.Log($"[CAMERA] xong {count} scene");
    }
}
#endif
