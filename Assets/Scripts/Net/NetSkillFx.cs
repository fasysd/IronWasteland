using IronWasteland.Tanks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Cầu nối phát lại hình ảnh kỹ năng cho client.
    /// Host thi triển kỹ năng trên brain thật, rồi gửi named message chứa (chủ xe, chỉ số kỹ năng, điểm ngắm).
    /// Client nhận được sẽ gọi <see cref="TankBrain.PlayRemoteSkillFx"/> để sinh lại đạn/ảo ảnh/vùng bằng pool cục bộ.
    /// Không dùng NetworkBehaviour vì NGO cấm gắn NetworkBehaviour lên GameObject của NetworkManager.
    /// </summary>
    public sealed class NetSkillFx : MonoBehaviour
    {
        private const string MessageName = "IW.SkillFx";

        // ulong owner (8) + byte index (1) + Vector2 aim (8) + Vector2 origin (8) + 2 byte biến thể = 27 byte, cấp dư 40.
        private const int MessageSize = 40;

        /// <summary>Kênh riêng cho vụ nổ ấn tự động (ấn đầy cộng dồn nhờ sát thương thường).</summary>
        private const string MarkExplosionMessageName = "IW.SkillFxMark";

        // ulong owner (8) + ulong enemy NetworkObjectId (8) + Vector3 vị trí nổ (12) = 28 byte, cấp dư 32.
        private const int MarkExplosionMessageSize = 32;

        /// <summary>Kênh riêng cho vệt sáng Mark Hunter (1A) khi kẻ địch mang ấn bị hạ gục.</summary>
        private const string KillStreamMessageName = "IW.SkillFxKillStream";

        // ulong owner (8) + Vector3 vị trí kẻ địch chết (12) = 20 byte, cấp dư 24.
        private const int KillStreamMessageSize = 24;

        /// <summary>
        /// Kênh tổng quát cho hiệu ứng hình ảnh sinh ra trong luồng chỉ máy chủ chạy
        /// (sát thương, hạ gục, hồi máu...) nên không đi qua UseSkill để phát lại được.
        /// Mã hiệu ứng do từng lớp tank tự quy ước.
        /// </summary>
        private const string TankFxMessageName = "IW.SkillFxTank";

        // ulong owner (8) + byte mã hiệu ứng (1) + Vector3 vị trí (12) = 21 byte, cấp dư 24.
        private const int TankFxMessageSize = 24;

        private bool m_Registered;

        /// <summary>Đếm số lần phát lại để chỉ ghi vài dòng log chẩn đoán đầu tiên.</summary>
        private static int s_ReplayLogCount;

        /// <summary>Đếm số lần phát lại vụ nổ ấn để chỉ ghi vài dòng log chẩn đoán đầu tiên.</summary>
        private static int s_MarkExplosionLogCount;

        /// <summary>Đếm số lần phát lại vệt sáng hạ gục để chỉ ghi vài dòng log chẩn đoán đầu tiên.</summary>
        private static int s_KillStreamLogCount;

        /// <summary>Đếm số lần phát lại hiệu ứng tổng quát để chỉ ghi vài dòng log chẩn đoán đầu tiên.</summary>
        private static int s_TankFxLogCount;

        /// <summary>Instance duy nhất trong scene do NetBootstrap/Network root giữ.</summary>
        public static NetSkillFx Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unregister();
        }

        private void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
            {
                Register(manager);
            }
            else
            {
                Unregister();
            }
        }

        /// <summary>
        /// Host gọi sau khi thi triển kỹ năng thành công để client phát lại hình ảnh.
        /// Chỉ server được gửi; client gọi hàm này sẽ bị bỏ qua.
        /// </summary>
        public void Broadcast(TankBrain brain, int skillIndex, Vector2 aim, Vector2 origin, byte option1 = 0, byte option2 = 0)
        {
            if (brain == null) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer) return;
            if (manager.CustomMessagingManager == null) return;

            NetworkObject networkObject = brain.GetComponent<NetworkObject>();
            if (networkObject == null || !networkObject.IsSpawned) return;

            using FastBufferWriter writer = new FastBufferWriter(MessageSize, Allocator.Temp);
            writer.WriteValueSafe(networkObject.OwnerClientId);
            writer.WriteValueSafe((byte)skillIndex);
            writer.WriteValueSafe(aim);
            writer.WriteValueSafe(origin);
            writer.WriteValueSafe(option1);
            writer.WriteValueSafe(option2);
            manager.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer);
        }

        /// <summary>
        /// Host gọi khi ấn Fox Mark đạt cộng dồn tối đa và tự phát nổ trong luồng sát thương.
        /// Chỉ server được gửi; client gọi hàm này sẽ bị bỏ qua.
        /// </summary>
        public void BroadcastMarkExplosion(TankBrain brain, ulong enemyNetworkObjectId, Vector3 position)
        {
            if (brain == null) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer) return;
            if (manager.CustomMessagingManager == null) return;

            NetworkObject networkObject = brain.GetComponent<NetworkObject>();
            if (networkObject == null || !networkObject.IsSpawned) return;

            using FastBufferWriter writer = new FastBufferWriter(MarkExplosionMessageSize, Allocator.Temp);
            writer.WriteValueSafe(networkObject.OwnerClientId);
            writer.WriteValueSafe(enemyNetworkObjectId);
            writer.WriteValueSafe(position);
            manager.CustomMessagingManager.SendNamedMessageToAll(MarkExplosionMessageName, writer);
        }

        /// <summary>
        /// Host gọi khi hạ gục kẻ địch trong lúc Mark Hunter (1A) đang bật, để mọi client vẽ lại
        /// các vệt sáng chảy về xe của người thi triển. Chỉ server được gửi; client gọi sẽ bị bỏ qua.
        /// </summary>
        public void BroadcastKillStream(TankBrain brain, Vector3 enemyPosition)
        {
            if (brain == null) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer) return;
            if (manager.CustomMessagingManager == null) return;

            NetworkObject networkObject = brain.GetComponent<NetworkObject>();
            if (networkObject == null || !networkObject.IsSpawned) return;

            using FastBufferWriter writer = new FastBufferWriter(KillStreamMessageSize, Allocator.Temp);
            writer.WriteValueSafe(networkObject.OwnerClientId);
            writer.WriteValueSafe(enemyPosition);
            manager.CustomMessagingManager.SendNamedMessageToAll(KillStreamMessageName, writer);
        }

        /// <summary>
        /// Host gọi khi vừa vẽ một hiệu ứng hình ảnh chỉ máy chủ sinh ra được (sát thương, hạ gục,
        /// hồi máu...) để mọi client dựng lại đúng hiệu ứng đó. Client gọi hàm này sẽ bị bỏ qua.
        /// </summary>
        /// <param name="brain">Xe của người sở hữu hiệu ứng (chỉ cần để lấy OwnerClientId).</param>
        /// <param name="fxCode">Mã hiệu ứng do lớp tank quy ước.</param>
        /// <param name="position">Vị trí hiển thị hiệu ứng.</param>
        public void BroadcastTankFx(TankBrain brain, byte fxCode, Vector3 position)
        {
            if (brain == null) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer) return;
            if (manager.CustomMessagingManager == null) return;

            NetworkObject networkObject = brain.GetComponent<NetworkObject>();
            if (networkObject == null || !networkObject.IsSpawned) return;

            using FastBufferWriter writer = new FastBufferWriter(TankFxMessageSize, Allocator.Temp);
            writer.WriteValueSafe(networkObject.OwnerClientId);
            writer.WriteValueSafe(fxCode);
            writer.WriteValueSafe(position);
            manager.CustomMessagingManager.SendNamedMessageToAll(TankFxMessageName, writer);
        }

        private void Register(NetworkManager manager)
        {
            if (m_Registered) return;
            if (manager.CustomMessagingManager == null) return;

            manager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, HandleMessage);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(MarkExplosionMessageName, HandleMarkExplosion);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(KillStreamMessageName, HandleKillStream);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(TankFxMessageName, HandleTankFx);
            m_Registered = true;
        }

        private void Unregister()
        {
            if (!m_Registered) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.CustomMessagingManager != null)
            {
                manager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
                manager.CustomMessagingManager.UnregisterNamedMessageHandler(MarkExplosionMessageName);
                manager.CustomMessagingManager.UnregisterNamedMessageHandler(KillStreamMessageName);
                manager.CustomMessagingManager.UnregisterNamedMessageHandler(TankFxMessageName);
            }

            m_Registered = false;
        }

        private void HandleMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return; // chỉ host được phát lại VFX
            reader.ReadValueSafe(out ulong ownerClientId);
            reader.ReadValueSafe(out byte skillIndex);
            reader.ReadValueSafe(out Vector2 aim);
            reader.ReadValueSafe(out Vector2 origin);
            reader.ReadValueSafe(out byte option1);
            reader.ReadValueSafe(out byte option2);

            NetSpawnService spawnService = FindAnyObjectByType<NetSpawnService>();
            if (spawnService == null) return;
            if (!spawnService.TryResolveTank(ownerClientId, out TankBrain tank) || tank == null)
            {
                Debug.LogWarning($"[NetSkillFx] không tìm thấy xe của owner={ownerClientId} để phát lại kỹ năng {skillIndex}.");
                return;
            }

            if (s_ReplayLogCount < 3)
            {
                s_ReplayLogCount++;
                Debug.Log($"[NetSkillFx] phát lại kỹ năng {skillIndex} cho xe {tank.name} (owner={ownerClientId}).");
            }

            tank.PlayRemoteSkillFx(skillIndex, aim, origin, option1, option2);
        }

        /// <summary>
        /// Client nhận lệnh phát lại vụ nổ ấn từ host rồi chuyển cho brain phụ trách hình ảnh.
        /// </summary>
        private void HandleMarkExplosion(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return; // chỉ host được phát lại VFX
            reader.ReadValueSafe(out ulong ownerClientId);
            reader.ReadValueSafe(out ulong enemyNetworkObjectId);
            reader.ReadValueSafe(out Vector3 position);

            NetSpawnService spawnService = FindAnyObjectByType<NetSpawnService>();
            if (spawnService == null) return;
            if (!spawnService.TryResolveTank(ownerClientId, out TankBrain tank) || tank == null)
            {
                Debug.LogWarning($"[NetSkillFx] không tìm thấy xe của owner={ownerClientId} để phát lại nổ ấn.");
                return;
            }

            if (s_MarkExplosionLogCount < 3)
            {
                s_MarkExplosionLogCount++;
                Debug.Log($"[NetSkillFx] phát lại nổ ấn cho xe {tank.name} (owner={ownerClientId}, enemy={enemyNetworkObjectId}).");
            }

            tank.PlayRemoteMarkExplosionFx(position, enemyNetworkObjectId);
        }

        /// <summary>
        /// Client nhận vị trí kẻ địch vừa chết từ host rồi chuyển cho brain dựng lại vệt sáng 1A.
        /// </summary>
        private void HandleKillStream(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return; // chỉ host được phát lại VFX
            reader.ReadValueSafe(out ulong ownerClientId);
            reader.ReadValueSafe(out Vector3 enemyPosition);

            NetSpawnService spawnService = FindAnyObjectByType<NetSpawnService>();
            if (spawnService == null) return;
            if (!spawnService.TryResolveTank(ownerClientId, out TankBrain tank) || tank == null)
            {
                Debug.LogWarning($"[NetSkillFx] không tìm thấy xe của owner={ownerClientId} để phát lại vệt sáng hạ gục.");
                return;
            }

            if (s_KillStreamLogCount < 3)
            {
                s_KillStreamLogCount++;
                Debug.Log($"[NetSkillFx] phát lại vệt sáng hạ gục cho xe {tank.name} (owner={ownerClientId}).");
            }

            tank.PlayRemoteKillStreamFx(enemyPosition);
        }

        /// <summary>
        /// Client nhận mã hiệu ứng tổng quát từ host rồi chuyển cho brain phụ trách hình ảnh dựng lại.
        /// </summary>
        private void HandleTankFx(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return; // chỉ host được phát lại VFX
            reader.ReadValueSafe(out ulong ownerClientId);
            reader.ReadValueSafe(out byte fxCode);
            reader.ReadValueSafe(out Vector3 position);

            NetSpawnService spawnService = FindAnyObjectByType<NetSpawnService>();
            if (spawnService == null) return;
            if (!spawnService.TryResolveTank(ownerClientId, out TankBrain tank) || tank == null)
            {
                Debug.LogWarning($"[NetSkillFx] không tìm thấy xe của owner={ownerClientId} để phát lại hiệu ứng {fxCode}.");
                return;
            }

            if (s_TankFxLogCount < 3)
            {
                s_TankFxLogCount++;
                Debug.Log($"[NetSkillFx] phát lại hiệu ứng {fxCode} cho xe {tank.name} (owner={ownerClientId}).");
            }

            tank.PlayRemoteTankFx(fxCode, position);
        }
    }
}
