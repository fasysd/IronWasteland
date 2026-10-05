using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// ScriptableObject tao TankBrain tu prefab.
    /// Chi "Level" va "View ID" duoc truyen tu ben ngoi (vd Player);
    /// cac chi so khac duoc tinh trong chinh TankBrain prefab.
    /// </summary>
    [CreateAssetMenu(menuName = "IronWasteland/Tank/Tank Brain Factory", fileName = "TankBrainFactory")]
    public class TankBrainFactory : ScriptableObject
    {
        [Header("Prefab")]
        [Tooltip("Prefab chua TankBrain (khong co TankView).")]
        [SerializeField] private TankBrain tankPrefab;

        [Header("Views")]
        [Tooltip("Danh sach TankView prefab. Index trong mang la View ID.")]
        [SerializeField] private TankView[] viewPrefabs;

        public TankBrain TankPrefab => tankPrefab;
        public TankView[] ViewPrefabs => viewPrefabs;

        /// <summary>Tao TankBrain moi theo Level va View ID.</summary>
        public TankBrain CreateTank(int level, int viewId, Vector3 position, Transform parent = null)
        {
            if (tankPrefab == null)
            {
                Debug.LogError($"[{nameof(TankBrainFactory)}] Chua gan TankPrefab.");
                return null;
            }

            TankView viewPrefab = GetViewPrefab(viewId);
            if (viewPrefab == null)
            {
                Debug.LogError($"[{nameof(TankBrainFactory)}] View ID {viewId} khong hop le.");
                return null;
            }

            TankBrain brain = Instantiate(tankPrefab, position, Quaternion.identity, parent);
            brain.Initialize(level);
            brain.SetViewPrefab(viewPrefab);

            return brain;
        }

        /// <summary>Lay TankView prefab theo ID (index trong mang).</summary>
        public TankView GetViewPrefab(int viewId)
        {
            if (viewPrefabs == null || viewId < 0 || viewId >= viewPrefabs.Length) return null;
            return viewPrefabs[viewId];
        }

        public int ViewCount => viewPrefabs != null ? viewPrefabs.Length : 0;
    }
}