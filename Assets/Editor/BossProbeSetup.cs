using System.Collections.Generic;
using System.IO;
using IronWasteland.Net;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace IronWasteland.EditorTools
{
    /// <summary>
    /// Chuẩn bị 5 boss + 12 quái thường cho kiểm thử mạng: gắn EnemyNetState + NetworkTransform
    /// (nếu thiếu) và sinh catalog probe. Chạy menu "Iron Wasteland/Net/Rebuild Boss Probe Catalog".
    /// </summary>
    public static class BossProbeSetup
    {
        private const string CatalogPath = "Assets/Resources/BossProbeCatalog.asset";
        private static readonly string[] ProbePaths =
        {
            "Assets/Prefabs/Enemies/BongMaTuDia/BongMaTuDia.prefab",
            "Assets/Prefabs/Enemies/SeauBearus/SEAUBEARUS.prefab",
            "Assets/Prefabs/Enemies/D1onk/TruongQuanNgucD1ONK .prefab",
            "Assets/Prefabs/Enemies/TuThanNguyenTo/TuThanNguyenTo.prefab",
            "Assets/Prefabs/Enemies/The2Face/The2Face.prefab",
            "Assets/Prefabs/Enemies/Normal/KIenLuaKhongLo.prefab",
            "Assets/Prefabs/Enemies/Normal/KySinhThienThach.prefab",
            "Assets/Prefabs/Enemies/Normal/MatTrangDen.prefab",
            "Assets/Prefabs/Enemies/Normal/MatTroiDo.prefab",
            "Assets/Prefabs/Enemies/Normal/NamDocGaiThach.prefab",
            "Assets/Prefabs/Enemies/Normal/QuaiVatDamLay.prefab",
            "Assets/Prefabs/Enemies/Normal/QuaiVatTinhThach.prefab",
            "Assets/Prefabs/Enemies/Normal/SaoDen.prefab",
            "Assets/Prefabs/Enemies/Normal/TinhLinhNghiaDia.prefab",
            "Assets/Prefabs/Enemies/Normal/TinhLinhSamSetNho.prefab",
            "Assets/Prefabs/Enemies/Normal/XaThuTinhThe.prefab",
            "Assets/Prefabs/Enemies/Normal/XenNuoc.prefab"
        };

        [MenuItem("Iron Wasteland/Net/Rebuild Boss Probe Catalog")]
        public static void Rebuild()
        {
            EnsureFolder("Assets/Resources");
            var prefabs = new List<GameObject>(ProbePaths.Length);

            for (int i = 0; i < ProbePaths.Length; i++)
            {
                string path = ProbePaths[i];
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    if (root.GetComponent<EnemyNetState>() == null)
                    {
                        root.AddComponent<EnemyNetState>();
                        changed = true;
                    }

                    if (root.GetComponent<NetworkTransform>() == null)
                    {
                        root.AddComponent<NetworkTransform>();
                        changed = true;
                    }

                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                    if (root.GetComponent<NetworkObject>() == null) Debug.LogError("[BossProbeSetup] Thiếu NetworkObject: " + path);
                    prefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            BossProbeCatalog catalog = AssetDatabase.LoadAssetAtPath<BossProbeCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BossProbeCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.SetPrefabs(prefabs.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BossProbeSetup] Đã chuẩn bị " + prefabs.Count + " prefab (boss + quái thường) và catalog " + CatalogPath + ".");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
