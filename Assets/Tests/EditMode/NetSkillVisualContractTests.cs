using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IronWasteland.Tests
{
    /// <summary>
    /// Hợp đồng asset cho đồng bộ visual kỹ năng quái: catalog VFX dùng chung, marker ghost
    /// trên các child Skill, và 5 boss đã đủ thành phần mạng + catalog probe dev-only.
    /// Test chỉ đọc asset, không sửa gì.
    /// </summary>
    public class NetSkillVisualContractTests
    {
        private const string CatalogPath = "Assets/Resources/NetSkillVisualCatalog.asset";
        private const string BossProbeCatalogPath = "Assets/Resources/BossProbeCatalog.asset";

        private static readonly string[] NormalSkillPrefabs =
        {
            "Assets/Prefabs/Enemies/Normal/KIenLuaKhongLo.prefab",
            "Assets/Prefabs/Enemies/Normal/KySinhThienThach.prefab",
            "Assets/Prefabs/Enemies/Normal/MatTrangDen.prefab",
            "Assets/Prefabs/Enemies/Normal/MatTroiDo.prefab"
        };

        private static readonly string[] BossPrefabs =
        {
            "Assets/Prefabs/Enemies/BongMaTuDia/BongMaTuDia.prefab",
            "Assets/Prefabs/Enemies/SeauBearus/SEAUBEARUS.prefab",
            "Assets/Prefabs/Enemies/D1onk/TruongQuanNgucD1ONK .prefab",
            "Assets/Prefabs/Enemies/TuThanNguyenTo/TuThanNguyenTo.prefab",
            "Assets/Prefabs/Enemies/The2Face/The2Face.prefab"
        };

        [Test]
        public void Catalog_ky_nang_co_id_duy_nhat_va_prefab()
        {
            ScriptableObject catalog = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CatalogPath);
            Assert.IsNotNull(catalog, "Thiếu Assets/Resources/NetSkillVisualCatalog.asset.");

            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("m_Entries");
            Assert.IsNotNull(entries, "Catalog phải có mảng m_Entries.");
            Assert.GreaterOrEqual(entries.arraySize, 30, "Catalog phải chứa đủ VFX boss đã quét.");

            HashSet<int> ids = new HashSet<int>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                int id = entry.FindPropertyRelative("Id").intValue;
                GameObject prefab = entry.FindPropertyRelative("Prefab").objectReferenceValue as GameObject;

                Assert.AreNotEqual(0, id, "VFX tại index " + i + " chưa có Id.");
                Assert.IsTrue(ids.Add(id), "Id VFX bị trùng: " + id);
                Assert.IsNotNull(prefab, "VFX tại index " + i + " chưa gán prefab.");
            }
        }

        [Test]
        public void Quai_thuong_co_marker_ghost_tren_child_Skill()
        {
            foreach (string path in NormalSkillPrefabs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, "Thiếu prefab quái: " + path);

                Transform skill = FindChild(prefab.transform, "Skill");
                Assert.IsNotNull(skill, "Prefab " + prefab.name + " không có child Skill.");
                Assert.IsNotNull(skill.GetComponent("NetVisualGhost"),
                    "Child Skill của " + prefab.name + " thiếu marker NetVisualGhost.");
            }
        }

        [Test]
        public void Boss_co_thanh_phan_mang_va_nam_trong_catalog_probe()
        {
            foreach (string path in BossPrefabs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, "Thiếu prefab boss: " + path);
                Assert.IsNotNull(prefab.GetComponent("NetworkObject"), prefab.name + " thiếu NetworkObject.");
                Assert.IsNotNull(prefab.GetComponent("EnemyNetState"), prefab.name + " thiếu EnemyNetState.");
                Assert.IsNotNull(prefab.GetComponent("NetworkTransform"), prefab.name + " thiếu NetworkTransform.");
            }

            ScriptableObject probe = AssetDatabase.LoadAssetAtPath<ScriptableObject>(BossProbeCatalogPath);
            Assert.IsNotNull(probe, "Thiếu Assets/Resources/BossProbeCatalog.asset.");
            SerializedProperty prefabs = new SerializedObject(probe).FindProperty("m_Prefabs");
            Assert.IsNotNull(prefabs, "BossProbeCatalog phải có mảng m_Prefabs.");
            Assert.GreaterOrEqual(prefabs.arraySize, BossPrefabs.Length + 12,
                "BossProbeCatalog phải chứa 5 boss + 12 quái thường để -iw-spawn dùng được.");
        }

        [Test]
        public void MatTroiDo_co_prefab_mang_cho_Ky_Sinh_Thien_Thach()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Normal/MatTroiDo.prefab");
            Assert.IsNotNull(prefab, "Thiếu prefab MatTroiDo.");

            MonoBehaviour brain = null;
            MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i].GetType().Name == "NormalEnemy_MatTroiDo")
                {
                    brain = behaviours[i];
                    break;
                }
            }

            Assert.IsNotNull(brain, "MatTroiDo thiếu component NormalEnemy_MatTroiDo.");
            SerializedProperty field = new SerializedObject(brain).FindProperty("kySinhThienThachNetworkPrefab");
            Assert.IsNotNull(field, "NormalEnemy_MatTroiDo thiếu field kySinhThienThachNetworkPrefab.");

            Component kstt = field.objectReferenceValue as Component;
            Assert.IsNotNull(kstt, "MatTroiDo chưa gán prefab mạng cho Ký Sinh Thiên Thạch.");
            Assert.IsNotNull(kstt.gameObject.GetComponent("NetworkObject"),
                "Prefab Ký Sinh Thiên Thạch dùng qua mạng phải có NetworkObject.");
        }

        private static Transform FindChild(Transform root, string name)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == name) return children[i];
            }

            return null;
        }
    }
}
