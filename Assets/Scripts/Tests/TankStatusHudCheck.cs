#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using IronWasteland.Tanks;
using IronWasteland.UI;
using UnityEditor;
using UnityEngine;

namespace IronWasteland.Tests
{
    /// <summary>
    /// Kiểm tra HUD màn chơi (TankStatusHUD) ở hai seam:
    /// 1. Ảnh đại diện trong panel phải đổi theo xe đang chơi.
    /// 2. Thanh máu phải vẽ được và phản ánh đúng tỉ lệ máu hiện có.
    ///
    /// Chạy từ menu Unity: Tools -> Iron Wasteland -> Run TankStatusHUD Self-Check
    /// </summary>
    public static class TankStatusHudCheck
    {
        private const string HudPrefabPath = "Assets/Prefabs/UI/StageHUD.prefab";

        [MenuItem("Tools/Iron Wasteland/Run TankStatusHUD Self-Check")]
        public static void RunAll()
        {
            string report = Run();
            if (report.Contains("FAIL")) Debug.LogError(report);
            else Debug.Log(report);
        }

        public static string Run()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(Check_AvatarFollowsSelectedTank());
            builder.AppendLine(Check_HealthBarReflectsHealth());
            return builder.ToString().TrimEnd();
        }

        /// <summary>Seam 1: TankStatusHUD.ApplyTankIdentity + TankDefinition.Portrait.</summary>
        public static string Check_AvatarFollowsSelectedTank()
        {
            GameObject instance = InstantiateHud();
            if (instance == null) return "FAIL [avatar] Không tạo được " + HudPrefabPath;

            try
            {
                TankStatusHUD hud = instance.GetComponentInChildren<TankStatusHUD>(true);
                if (hud == null) return "FAIL [avatar] Prefab " + HudPrefabPath + " thiếu TankStatusHUD.";

                HashSet<string> seen = new HashSet<string>();
                for (int index = 0; index < TankRegistry.TankCount; index++)
                {
                    TankDefinition definition = TankRegistry.GetDefinition(index);
                    if (definition == null)
                        return "FAIL [avatar] Không nạp được TankDefinition #" + index + ".";

                    hud.ApplyTankIdentity(definition);

                    if (definition.Portrait == null)
                        return "FAIL [avatar] " + definition.TankName + " chưa có ảnh đại diện trong TankDefinition.";

                    if (hud.PortraitSprite != definition.Portrait)
                    {
                        string showing = hud.PortraitSprite != null ? hud.PortraitSprite.name : "null";
                        return "FAIL [avatar] " + definition.TankName + ": HUD đang hiện '" + showing +
                               "' thay vì '" + definition.Portrait.name + "'.";
                    }

                    if (!seen.Add(definition.Portrait.name))
                        return "FAIL [avatar] Hai xe dùng chung một ảnh: " + definition.Portrait.name + ".";
                }

                return "PASS [avatar] " + TankRegistry.TankCount + "/" + TankRegistry.TankCount +
                       " xe hiện đúng ảnh đại diện riêng.";
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>Seam 2: TankStatusHUD.ApplyHealth — thanh máu vẽ được và khớp tỉ lệ.</summary>
        public static string Check_HealthBarReflectsHealth()
        {
            GameObject instance = InstantiateHud();
            if (instance == null) return "FAIL [máu] Không tạo được " + HudPrefabPath;

            try
            {
                TankStatusHUD hud = instance.GetComponentInChildren<TankStatusHUD>(true);
                if (hud == null) return "FAIL [máu] Prefab " + HudPrefabPath + " thiếu TankStatusHUD.";

                if (hud.HealthFillSprite == null)
                    return "FAIL [máu] Image của thanh máu chưa có sprite nên thanh không bao giờ vẽ ra.";

                hud.ApplyHealth(50f, 200f);
                if (Mathf.Abs(hud.HealthFillAmount - 0.25f) > 0.001f)
                    return "FAIL [máu] 50/200 phải là 0.25 nhưng thanh đang là " +
                           hud.HealthFillAmount.ToString("0.###") + ".";

                hud.ApplyHealth(200f, 200f);
                if (Mathf.Abs(hud.HealthFillAmount - 1f) > 0.001f)
                    return "FAIL [máu] Đầy máu phải là 1.0 nhưng thanh đang là " +
                           hud.HealthFillAmount.ToString("0.###") + ".";

                hud.ApplyHealth(0f, 200f);
                if (Mathf.Abs(hud.HealthFillAmount) > 0.001f)
                    return "FAIL [máu] Hết máu phải là 0 nhưng thanh đang là " +
                           hud.HealthFillAmount.ToString("0.###") + ".";

                return "PASS [máu] Thanh máu vẽ được và khớp tỉ lệ máu hiện có.";
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static GameObject InstantiateHud()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            return prefab != null ? Object.Instantiate(prefab) : null;
        }
    }
}
#endif
