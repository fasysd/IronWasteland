using System.Collections.Generic;
using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Host dựng xe cho từng client: chọn <see cref="TankDefinition"/> theo <see cref="NetLoadout"/>,
    /// spawn qua NGO rồi mới gọi <c>Initialize</c> — nhờ vậy không còn tank nào được tạo ngoài mạng
    /// và mọi máy nhìn thấy cùng một đối tượng mạng.
    /// </summary>
    public class NetSpawnService : MonoBehaviour
    {
        [Header("Roster")]
        [Tooltip("Danh sách xe có thể chọn; thứ tự trùng với TankIndex trong NetLoadout.")]
        [SerializeField] private TankDefinition[] tankDefinitions;

        [Header("Vị trí spawn")]
        [Tooltip("Khoảng cách giữa các xe khi nhiều người chơi cùng vào phòng.")]
        [SerializeField] private float spawnSpacing = 2f;

        [Tooltip("Gốc toạ độ để xếp xe của 4 người chơi.")]
        [SerializeField] private Vector3 spawnOrigin = Vector3.zero;

        private readonly Dictionary<ulong, TankBrain> m_Spawned = new Dictionary<ulong, TankBrain>();

        public int TankCount => tankDefinitions != null ? tankDefinitions.Length : 0;

        /// <summary>Lấy định nghĩa xe theo chỉ số roster; trả null nếu roster chưa gán.</summary>
        public TankDefinition GetDefinition(byte tankIndex)
        {
            if (tankDefinitions == null || tankDefinitions.Length == 0) return null;
            return tankDefinitions[Mathf.Clamp(tankIndex, 0, tankDefinitions.Length - 1)];
        }

        /// <summary>Xe đã spawn cho client chưa (dùng để tránh spawn trùng).</summary>
        public bool TryGetTank(ulong clientId, out TankBrain brain)
        {
            if (m_Spawned.TryGetValue(clientId, out brain) && brain != null) return true;
            brain = null;
            return false;
        }

        /// <summary>Server dựng xe cho một client. Gọi trên host sau khi client được duyệt.</summary>
        public TankBrain SpawnPlayerTank(ulong clientId, NetLoadout loadout)
        {
            if (TryGetTank(clientId, out TankBrain existing)) return existing;

            TankDefinition definition = GetDefinition(loadout.TankIndex);
            if (definition == null || definition.TankPrefab == null)
            {
                Debug.LogError("[NetSpawnService] Thiếu TankDefinition hoặc TankPrefab cho xe được chọn.", this);
                return null;
            }

            Vector3 position = spawnOrigin + new Vector3(spawnSpacing * clientId, 0f, 0f);
            TankBrain brain = Instantiate(definition.TankPrefab, position, Quaternion.identity);
            NetworkObject networkObject = brain.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"[NetSpawnService] Prefab '{definition.TankPrefab.name}' chưa có NetworkObject.", this);
                Destroy(brain.gameObject);
                return null;
            }

            networkObject.SpawnAsPlayerObject(clientId);

            TankBrainContext context = new TankBrainContext(
                Mathf.Clamp(loadout.Level, TankDefinition.MinLevel, TankDefinition.MaxLevel),
                loadout.EquipmentId,
                loadout.OptionSkill1,
                loadout.OptionSkill2);
            definition.InitializeTank(brain, context);

            PlayerNetState netState = brain.GetComponent<PlayerNetState>();
            if (netState != null)
            {
                netState.SetServerLoadout(loadout);
            }

            m_Spawned[clientId] = brain;
            return brain;
        }
    }
}
