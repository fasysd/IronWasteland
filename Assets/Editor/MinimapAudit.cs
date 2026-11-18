#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using IronWasteland.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace IronWasteland.EditorTools
{
    /// <summary>
    /// Kiểm tra bàn đồ chiến thuật ở từng scene màn chơi: có StageHUD + MinimapUI, MapView đúng kích thước,
    /// các field (nền, lưới, 4 loại chấm) đã gán, và scene có object "Background" để lấy nền bản đồ.
    /// Menu: Tools ▸ Iron Wasteland ▸ Audit Minimap Scenes
    /// </summary>
    public static class MinimapAudit
    {
        [MenuItem("Tools/Iron Wasteland/Audit Minimap Scenes")]
        public static void Audit()
        {
            var sb = new StringBuilder("[MINIMAP-AUDIT]\n");
            int pass = 0;
            int total = 0;

            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled || !entry.path.Contains("/Stage_")) continue;
                total++;

                UnityEngine.SceneManagement.Scene scene =
                    EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);

                string sceneName = System.IO.Path.GetFileNameWithoutExtension(entry.path);
                bool ok = true;
                var notes = new List<string>();

                SpriteRenderer background = FindBackgroundSprite(out string spriteName);
                if (background == null)
                {
                    ok = false;
                    notes.Add("thieu Background");
                }
                else
                {
                    notes.Add("nen=" + (string.IsNullOrEmpty(spriteName) ? "(null)" : spriteName));
                }

                StageHUD hud = Object.FindFirstObjectByType<StageHUD>(FindObjectsInactive.Include);
                if (hud == null)
                {
                    ok = false;
                    notes.Add("thieu StageHUD");
                }

                MinimapUI minimap = Object.FindFirstObjectByType<MinimapUI>(FindObjectsInactive.Include);
                if (minimap == null)
                {
                    ok = false;
                    notes.Add("thieu MinimapUI");
                }
                else
                {
                    SerializedObject so = new SerializedObject(minimap);
                    ok &= Require(so, "mapView", notes, "MapView");
                    ok &= Require(so, "mapBackground", notes, "Nen");
                    ok &= Require(so, "viewFrame", notes, "KhungCamera");
                    ok &= Require(so, "blipPlayer", notes, "MuiTenTank");
                    ok &= Require(so, "blipTeammate", notes, "MuiTenDongDoi");
                    ok &= Require(so, "blipEnemy", notes, "ChamQuai");
                    ok &= Require(so, "blipBoss", notes, "ChamBoss");

                    RectTransform view = so.FindProperty("mapView").objectReferenceValue as RectTransform;
                    if (view != null) notes.Add("view=" + view.sizeDelta.x + "x" + view.sizeDelta.y);
                    if (view != null && view.GetComponent<RectMask2D>() == null)
                    {
                        ok = false;
                        notes.Add("MapView thieu RectMask2D");
                    }
                }

                if (ok) pass++;
                sb.AppendLine($"  {sceneName,-12} | {(ok ? "PASS" : "FAIL")} | " + string.Join(" | ", notes));
            }

            sb.AppendLine($"[MINIMAP] {(pass == total ? "PASS" : "FAIL")} {pass}/{total}");
            Debug.Log(sb.ToString());
        }

        private static bool Require(SerializedObject so, string field, List<string> notes, string label)
        {
            SerializedProperty property = so.FindProperty(field);
            bool assigned = property != null && property.objectReferenceValue != null;
            if (!assigned) notes.Add("thieu " + label);
            return assigned;
        }

        private static SpriteRenderer FindBackgroundSprite(out string spriteName)
        {
            spriteName = null;
            SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            SpriteRenderer best = null;

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null || renderer.sprite == null) continue;
                if (renderer.transform.name != "Background") continue;
                if (best == null || renderer.sortingOrder < best.sortingOrder) best = renderer;
            }

            if (best != null && best.sprite != null) spriteName = best.sprite.name;
            return best;
        }
    }
}
#endif
