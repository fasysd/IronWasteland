#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using IronWasteland.Enemies;
using IronWasteland.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace IronWasteland.Tests
{
    /// <summary>
    /// Kiểm tra HUD của màn chơi (prefab StageHUD) ở seam: thanh máu boss phải vẽ đúng tỉ lệ máu.
    /// Chạy từ menu Unity: Tools -> Iron Wasteland -> Run StageHUD Self-Check
    /// </summary>
    public static class StageHudCheck
    {
        private const string HudPrefabPath = "Assets/Prefabs/UI/StageHUD.prefab";

        [MenuItem("Tools/Iron Wasteland/Run StageHUD Self-Check")]
        public static void RunAll()
        {
            string report = Run();
            if (report.Contains("FAIL")) Debug.LogError(report);
            else Debug.Log(report);
        }

        public static string Run()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(Check_BossBarReflectsHealth());
            builder.AppendLine(Check_NearestEnemiesGetNameplates());
            return builder.ToString().TrimEnd();
        }

        /// <summary>Seam: mesh thanh máu boss phải co theo fillAmount.</summary>
        public static string Check_BossBarReflectsHealth()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (prefab == null) return "FAIL [boss] Không tìm thấy " + HudPrefabPath;

            GameObject hud = Object.Instantiate(prefab);
            try
            {
                StageHUD stageHud = hud.GetComponent<StageHUD>();
                if (stageHud == null) return "FAIL [boss] Prefab " + HudPrefabPath + " thiếu StageHUD.";

                Image fill = stageHud.BossFillImage;
                if (fill == null) return "FAIL [boss] StageHUD.bossFill chưa được nối vào Image nào.";

                // Image kiểu Filled chỉ cắt theo fillAmount khi có sprite.
                if (fill.sprite == null)
                    return "FAIL [boss] Thanh máu boss thiếu sprite: kiểu Filled bị bỏ qua nên luôn vẽ đầy 100%.";

                fill.fillAmount = 0.35f;
                float ratio = UiMeshProbe.MeasureFillRatio(fill);
                if (ratio < 0f) return "FAIL [boss] Không đo được mesh của thanh máu boss.";
                if (Mathf.Abs(ratio - 0.35f) > 0.06f)
                    return "FAIL [boss] fillAmount 0.35 nhưng mesh chiếm " + ratio.ToString("P0") + " bề rộng.";

                return "PASS [boss] Thanh máu boss vẽ đúng tỉ lệ máu còn lại.";
            }
            finally
            {
                Object.DestroyImmediate(hud);
            }
        }

        /// <summary>
        /// Seam: quái đang sống và nằm trong khung hình phải có nhãn + thanh máu.
        /// Dựng 26 quái giả trong tầm nhìn rồi cho UnitNameplates chạy đúng vòng LateUpdate của nó.
        /// </summary>
        public static string Check_NearestEnemiesGetNameplates()
        {
            const int expectedVisible = 24;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (prefab == null) return "FAIL [nhãn quái] Không tìm thấy " + HudPrefabPath;

            Camera camera = CreateTestCamera();
            GameObject hud = Object.Instantiate(prefab);
            List<GameObject> spawned = new List<GameObject>();
            List<EnemyBrain> sceneEnemies = new List<EnemyBrain>();
            bool[] sceneEnemyState = new bool[0];

            try
            {
                UnitNameplates plates = hud.GetComponentInChildren<UnitNameplates>(true);
                if (plates == null) return "FAIL [nhãn quái] Prefab " + HudPrefabPath + " thiếu UnitNameplates.";

                // Tắt quái có sẵn trong scene đang mở để không chiếm suất hiển thị.
                sceneEnemies.AddRange(Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None));
                sceneEnemyState = new bool[sceneEnemies.Count];
                for (int i = 0; i < sceneEnemies.Count; i++)
                {
                    sceneEnemyState[i] = sceneEnemies[i].gameObject.activeSelf;
                    sceneEnemies[i].gameObject.SetActive(false);
                }

                List<Vector3> positions = BuildGridPositions(26);
                positions.Sort((a, b) => b.sqrMagnitude.CompareTo(a.sqrMagnitude));

                for (int i = 0; i < positions.Count; i++)
                {
                    spawned.Add(CreateEnemy(positions[i]));
                }

                plates.SendMessage("LateUpdate");

                int visible = 0;
                for (int i = 0; i < spawned.Count; i++)
                {
                    if (plates.HasVisibleNameplate(spawned[i].GetComponent<EnemyBrain>())) visible++;
                }

                if (visible < expectedVisible)
                    return "FAIL [nhãn quái] Chỉ " + visible + "/" + expectedVisible +
                           " quái gần nhất có thanh máu dù tất cả đang đứng trong khung hình.";

                return "PASS [nhãn quái] " + visible + "/26 quái trong khung hình có nhãn + thanh máu.";
            }
            finally
            {
                for (int i = 0; i < spawned.Count; i++)
                {
                    if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
                }

                for (int i = 0; i < sceneEnemies.Count; i++)
                {
                    if (sceneEnemies[i] != null) sceneEnemies[i].gameObject.SetActive(sceneEnemyState[i]);
                }

                Object.DestroyImmediate(hud);
                if (camera != null) Object.DestroyImmediate(camera.gameObject);
            }
        }

        private static Camera CreateTestCamera()
        {
            var go = new GameObject("TestCamera") { tag = "MainCamera" };
            Camera camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            return camera;
        }

        /// <summary>Lưới điểm nằm gọn trong khung hình của camera test.</summary>
        private static List<Vector3> BuildGridPositions(int count)
        {
            var positions = new List<Vector3>(count);
            for (int row = 0; row < 4 && positions.Count < count; row++)
            {
                for (int column = 0; column < 7 && positions.Count < count; column++)
                {
                    positions.Add(new Vector3(-5f + column * 1.6667f, -4.5f + row * 3f, 0f));
                }
            }

            return positions;
        }

        private static GameObject CreateEnemy(Vector3 position)
        {
            var go = new GameObject("FakeEnemy");
            go.transform.position = position;
            FakeEnemyBrain brain = go.AddComponent<FakeEnemyBrain>();
            var stats = new EnemyStats { MaxHealth = 100f };
            brain.Initialize(stats);
            return go;
        }
    }
}
#endif
