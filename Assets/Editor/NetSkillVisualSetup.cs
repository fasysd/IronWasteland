using System;
using System.Collections.Generic;
using System.IO;
using IronWasteland.Net;
using UnityEditor;
using UnityEngine;

namespace IronWasteland.EditorTools
{
    /// <summary>
    /// Công cụ một lần: gắn marker NetVisualGhost vào prefab skill/VFX và sinh catalog Id ổn định.
    /// Chạy menu "Iron Wasteland/Net/Rebuild Skill Visual Catalog" sau khi thêm prefab VFX mới.
    /// </summary>
    public static class NetSkillVisualSetup
    {
        private const string CatalogPath = "Assets/Resources/NetSkillVisualCatalog.asset";
        private static readonly string[] BossVfxFolders =
        {
            "Assets/Prefabs/Enemies/BongMaTuDia",
            "Assets/Prefabs/Enemies/D1onk",
            "Assets/Prefabs/Enemies/TuThanNguyenTo"
        };

        [MenuItem("Iron Wasteland/Net/Rebuild Skill Visual Catalog")]
        public static void Rebuild()
        {
            EnsureFolder("Assets/Resources");

            List<string> paths = CollectVfxPrefabs();
            paths.Sort(StringComparer.Ordinal);

            var entries = new List<NetSkillVisualCatalog.Entry>(paths.Count);
            var used = new Dictionary<int, string>(paths.Count);
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                int id = StableId(path);
                while (used.ContainsKey(id))
                {
                    id = id == int.MaxValue ? 1 : id + 1;
                }

                used.Add(id, path);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                MarkPrefab(path, prefab, false);
                entries.Add(new NetSkillVisualCatalog.Entry
                {
                    Id = id,
                    Prefab = prefab,
                    Lifetime = 5f
                });
            }

            MarkNormalSkillPrefabs();

            NetSkillVisualCatalog catalog = AssetDatabase.LoadAssetAtPath<NetSkillVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<NetSkillVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.SetEntries(entries.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            NetSkillVisualCatalog.InvalidateCache();
            Debug.Log("[NetSkillVisualSetup] Đã tạo catalog " + CatalogPath + " với " + entries.Count + " VFX.");
        }

        private static List<string> CollectVfxPrefabs()
        {
            var result = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:Prefab", BossVfxFolders);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;
                if (!Path.GetFileName(path).StartsWith("VFX_", StringComparison.Ordinal)) continue;
                result.Add(path);
            }

            return result;
        }

        private static void MarkNormalSkillPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Enemies/Normal" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                Transform skill = null;
                Transform[] children = prefab.GetComponentsInChildren<Transform>(true);
                for (int c = 0; c < children.Length; c++)
                {
                    if (children[c].name != "Skill") continue;
                    skill = children[c];
                    break;
                }

                if (skill == null) continue;
                MarkPrefab(path, prefab, true);
            }
        }

        private static void MarkPrefab(string path, GameObject prefab, bool skillOnly)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            try
            {
                if (skillOnly)
                {
                    Transform[] children = root.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < children.Length; i++)
                    {
                        if (children[i].name != "Skill") continue;
                        if (children[i].GetComponent<NetVisualGhost>() != null) continue;
                        children[i].gameObject.AddComponent<NetVisualGhost>();
                        changed = true;
                    }
                }
                else if (root.GetComponent<NetVisualGhost>() == null)
                {
                    root.AddComponent<NetVisualGhost>();
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int StableId(string path)
        {
            uint hash = 2166136261u;
            string normalized = path.Replace('\\', '/').ToLowerInvariant();
            for (int i = 0; i < normalized.Length; i++)
            {
                hash ^= normalized[i];
                hash *= 16777619u;
            }

            int id = (int)(hash & 0x7FFFFFFF);
            return id == 0 ? 1 : id;
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
