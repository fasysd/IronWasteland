using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Danh sách boss dùng cho cờ kiểm thử dev-only. Không được tham chiếu bởi luồng chơi thật.
    /// </summary>
    [CreateAssetMenu(fileName = "BossProbeCatalog", menuName = "Iron Wasteland/Net/Boss Probe Catalog")]
    public sealed class BossProbeCatalog : ScriptableObject
    {
        [Tooltip("Prefab boss theo đúng thứ tự dùng cho tham số -iw-boss (0..n-1).")]
        [SerializeField] private GameObject[] m_Prefabs = new GameObject[0];

        /// <summary>Danh sách prefab boss đã gán trong Inspector.</summary>
        public GameObject[] Prefabs => m_Prefabs;

        /// <summary>Ghi đè danh sách prefab từ công cụ Editor.</summary>
        public void SetPrefabs(GameObject[] prefabs)
        {
            m_Prefabs = prefabs ?? new GameObject[0];
        }
    }
}
