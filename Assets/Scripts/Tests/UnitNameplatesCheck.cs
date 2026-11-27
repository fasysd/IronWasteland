#if UNITY_EDITOR
using System.Text;
using IronWasteland.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace IronWasteland.Tests
{
    /// <summary>
    /// Kiểm tra nhãn tên + thanh máu nổi trên đầu quái (UnitNameplates) ở hai seam:
    /// 1. Thanh máu phải vẽ đúng tỉ lệ máu hiện có (Image kiểu Filled chỉ cắt đúng khi có sprite).
    /// 2. Nhãn phải biến mất khi quái chết / bị EnemySpawner đưa về pool (GameObject inactive).
    ///
    /// Chạy từ menu Unity: Tools -> Iron Wasteland -> Run UnitNameplates Self-Check
    /// </summary>
    public static class UnitNameplatesCheck
    {
        private const string HudPrefabPath = "Assets/Prefabs/UI/StageHUD.prefab";

        [MenuItem("Tools/Iron Wasteland/Run UnitNameplates Self-Check")]
        public static void RunAll()
        {
            string report = Run();
            if (report.Contains("FAIL")) Debug.LogError(report);
            else Debug.Log(report);
        }

        public static string Run()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(Check_NameplateBarReflectsHealth());
            builder.AppendLine(Check_NameplateHidesWhenTargetGoesAway());
            return builder.ToString().TrimEnd();
        }

        /// <summary>Seam 1: mesh của thanh máu phải co theo fillAmount.</summary>
        public static string Check_NameplateBarReflectsHealth()
        {
            GameObject hud = InstantiateHud();
            GameObject target = new GameObject("TargetGia", typeof(RectTransform));
            if (hud == null)
            {
                Object.DestroyImmediate(target);
                return "FAIL [thanh] Không tạo được " + HudPrefabPath;
            }

            try
            {
                UnitNameplates plates = hud.GetComponentInChildren<UnitNameplates>(true);
                if (plates == null) return "FAIL [thanh] Prefab " + HudPrefabPath + " thiếu UnitNameplates.";

                RectTransform root = plates.GetOrCreateEntryRoot(target.transform);
                if (root == null) return "FAIL [thanh] Không tạo được nhãn cho mục tiêu giả.";

                Image fill = FindImage(root, "HpFill");
                if (fill == null) return "FAIL [thanh] Nhãn không có Image 'HpFill'.";

                // Image kiểu Filled chỉ cắt theo fillAmount khi có sprite; không sprite thì Unity vẽ đủ khung.
                if (fill.sprite == null)
                    return "FAIL [thanh] Thanh máu thiếu sprite: kiểu Filled bị bỏ qua nên luôn vẽ đầy 100%.";

                fill.fillAmount = 0.4f;
                float ratio = UiMeshProbe.MeasureFillRatio(fill);
                if (ratio < 0f) return "FAIL [thanh] Không đo được mesh của thanh máu.";
                if (Mathf.Abs(ratio - 0.4f) > 0.06f)
                    return "FAIL [thanh] fillAmount 0.4 nhưng mesh chiếm " + ratio.ToString("P0") + " bề rộng.";

                return "PASS [thanh] Thanh máu nhãn quái vẽ đúng tỉ lệ máu hiện có.";
            }
            finally
            {
                Object.DestroyImmediate(hud);
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>Seam 2: nhãn phải ẩn khi mục tiêu bị SetActive(false) (quái chết bị đưa về pool).</summary>
        public static string Check_NameplateHidesWhenTargetGoesAway()
        {
            GameObject hud = InstantiateHud();
            GameObject target = new GameObject("QuaiGia", typeof(RectTransform));
            if (hud == null)
            {
                Object.DestroyImmediate(target);
                return "FAIL [ẩn nhãn] Không tạo được " + HudPrefabPath;
            }

            try
            {
                UnitNameplates plates = hud.GetComponentInChildren<UnitNameplates>(true);
                if (plates == null) return "FAIL [ẩn nhãn] Prefab " + HudPrefabPath + " thiếu UnitNameplates.";

                RectTransform root = plates.GetOrCreateEntryRoot(target.transform);
                if (root == null) return "FAIL [ẩn nhãn] Không tạo được nhãn cho mục tiêu giả.";

                // Nhãn mới tạo đang ẩn sẵn; bật lên để mô phỏng lúc nó đang hiện trên màn hình.
                root.gameObject.SetActive(true);

                target.SetActive(false);
                plates.CleanupStaleEntries();

                if (root.gameObject.activeSelf)
                    return "FAIL [ẩn nhãn] Quái đã bị đưa về pool (GameObject inactive) nhưng nhãn vẫn còn trên màn hình.";

                return "PASS [ẩn nhãn] Nhãn của quái đã chết/bị đưa về pool được ẩn ngay.";
            }
            finally
            {
                Object.DestroyImmediate(hud);
                Object.DestroyImmediate(target);
            }
        }

        private static Image FindImage(RectTransform root, string name)
        {
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].name == name) return images[i];
            }

            return null;
        }
        private static GameObject InstantiateHud()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            return prefab != null ? Object.Instantiate(prefab) : null;
        }
    }
}
#endif
