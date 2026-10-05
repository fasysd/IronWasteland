using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// ScriptableObject tao TankBrain tu prefab.
    /// Chi "Level" va "Model ID" duoc truyen tu ben ngoi (vd Player);
    /// cac chi so khac duoc tinh trong chinh TankBrain prefab.
    /// </summary>
    [CreateAssetMenu(menuName = "IronWasteland/Tank/Tank Brain Factory", fileName = "TankBrainFactory")]
    public class TankBrainFactory : ScriptableObject
    {
        [Header("Prefab")]
        [Tooltip("Prefab chua TankBrain (khong chua TankModel).")]
        [SerializeField] private TankBrain tankPrefab;

        [Header("Models")]
        [Tooltip("Danh sach TankModel prefab. Index trong mang la Model ID.")]
        [SerializeField] private TankView[] modelPrefabs;

        public TankBrain TankPrefab => tankPrefab;
        public TankView[] ModelPrefabs => modelPrefabs;

        /// <summary>Tao TankBrain moi theo Level va Model ID.</summary>
        public TankBrain CreateTank(int level, int modelId, Vector3 position, Transform parent = null)
        {
            if (tankPrefab == null)
            {
                Debug.LogError($"[{nameof(TankBrainFactory)}] Chua gan TankPrefab.");
                return null;
            }

            TankView modelPrefab = GetModelPrefab(modelId);
            if (modelPrefab == null)
            {
                Debug.LogError($"[{nameof(TankBrainFactory)}] Model ID {modelId} khong hop le.");
                return null;
            }

            TankBrain brain = Instantiate(tankPrefab, position, Quaternion.identity, parent);
            brain.Initialize(level);
            brain.SetModelPrefab(modelPrefab);

            return brain;
        }

        /// <summary>Lay TankModel prefab theo ID (index trong mang).</summary>
        public TankView GetModelPrefab(int modelId)
        {
            if (modelPrefabs == null || modelId < 0 || modelId >= modelPrefabs.Length) return null;
            return modelPrefabs[modelId];
        }

        public int ModelCount => modelPrefabs != null ? modelPrefabs.Length : 0;
    }
}