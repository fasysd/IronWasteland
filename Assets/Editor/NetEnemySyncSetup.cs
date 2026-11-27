using System.Collections;
using System.Reflection;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace IronWasteland.EditorTools
{
    /// <summary>
    /// Cấu hình đồng bộ animation cho toàn bộ prefab quái và dọn các visual kỹ năng tĩnh.
    /// Menu: Iron Wasteland/Net/Rebuild Enemy Animation Sync.
    /// </summary>
    public static class NetEnemySyncSetup
    {
        private static readonly string[] EnemyPrefabs =
        {
            "Assets/Prefabs/Enemies/BongMaTuDia.prefab",
            "Assets/Prefabs/Enemies/SEAUBEARUS.prefab",
            "Assets/Prefabs/Enemies/TruongQuanNgucD1ONK .prefab",
            "Assets/Prefabs/Enemies/TuThanNguyenTo.prefab",
            "Assets/Prefabs/Enemies/The2Face/The2Face.prefab",
            "Assets/Prefabs/Enemies/Enemy_Elite.prefab",
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

        [MenuItem("Iron Wasteland/Net/Rebuild Enemy Animation Sync")]
        public static void Rebuild()
        {
            int configured = 0;
            int failed = 0;
            foreach (string path in EnemyPrefabs)
            {
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                {
                    Debug.LogError("[NetEnemySyncSetup] Không tìm thấy prefab: " + path);
                    failed++;
                    continue;
                }

                Animator animator = FindBodyAnimator(root);
                if (animator == null)
                {
                    Debug.LogWarning("[NetEnemySyncSetup] Prefab không có Animator, bỏ qua: " + path);
                    failed++;
                    continue;
                }

                NetworkAnimator networkAnimator = root.GetComponent<NetworkAnimator>();
                if (networkAnimator == null) networkAnimator = root.AddComponent<NetworkAnimator>();
                networkAnimator.Animator = animator;
                ConfigureParameters(networkAnimator, animator);

                if (path.EndsWith("MatTroiDo.prefab"))
                {
                    // Chỉ xóa Skill chết ở con trực tiếp của root; giữ nguyên Skill trong template triệu hồi.
                    Transform deadSkill = root.transform.Find("Skill");
                    if (deadSkill != null) Object.DestroyImmediate(deadSkill.gameObject);
                }

                if (path.EndsWith("KIenLuaKhongLo.prefab") || path.EndsWith("KySinhThienThach.prefab") ||
                    path.EndsWith("MatTrangDen.prefab"))
                {
                    Transform skill = FindDeep(root.transform, "Skill");
                    SpriteRenderer renderer = skill != null ? skill.GetComponent<SpriteRenderer>() : null;
                    if (renderer != null)
                    {
                        renderer.sortingOrder = 10;
                        EditorUtility.SetDirty(renderer);
                    }
                }

                EditorUtility.SetDirty(root);
                PrefabUtility.SavePrefabAsset(root);
                configured++;
                Debug.Log("[NetEnemySyncSetup] Đã cấu hình " + path + " (Animator=" + animator.name + ").");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NetEnemySyncSetup] Hoàn tất: " + configured + " prefab, lỗi/bỏ qua: " + failed + ".");
        }

        private static Animator FindBodyAnimator(GameObject root)
        {
            Animator[] animators = root.GetComponentsInChildren<Animator>(true);
            if (animators.Length == 0) return null;
            foreach (Animator animator in animators)
            {
                if (animator.gameObject.name == "EnemyModel") return animator;
            }
            return animators[0];
        }

        private static void ConfigureParameters(NetworkAnimator networkAnimator, Animator animator)
        {
            FieldInfo entriesField = typeof(NetworkAnimator).GetField(
                "AnimatorParameterEntries", BindingFlags.Instance | BindingFlags.NonPublic);
            if (entriesField == null) return;

            object container = entriesField.GetValue(networkAnimator);
            if (container == null)
            {
                container = System.Activator.CreateInstance(entriesField.FieldType, true);
                entriesField.SetValue(networkAnimator, container);
            }

            FieldInfo listField = container.GetType().GetField("ParameterEntries");
            IList entries = listField != null ? listField.GetValue(container) as IList : null;
            if (entries == null) return;
            entries.Clear();

            System.Type entryType = typeof(NetworkAnimator).GetNestedType(
                "AnimatorParameterEntry", BindingFlags.Public | BindingFlags.NonPublic);
            if (entryType == null) return;
            FieldInfo nameField = entryType.GetField("name");
            FieldInfo hashField = entryType.GetField("NameHash");
            FieldInfo syncField = entryType.GetField("Synchronize");
            FieldInfo typeField = entryType.GetField("ParameterType");
            if (nameField == null || hashField == null || syncField == null || typeField == null) return;

            AnimatorControllerParameter[] parameters = animator.parameters;
            foreach (AnimatorControllerParameter parameter in parameters)
            {
                object entry = System.Activator.CreateInstance(entryType, true);
                nameField.SetValue(entry, parameter.name);
                hashField.SetValue(entry, parameter.nameHash);
                syncField.SetValue(entry, parameter.name != "MoveSpeed");
                typeField.SetValue(entry, parameter.type);
                entries.Add(entry);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
