using System.Collections.Generic;
using System.Runtime.CompilerServices;
using IronWasteland.Tanks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Nhân bản "bóng" visual của một template con (đạn, vùng, bản sao...) xuống client.
    /// Template là GameObject con nằm trong prefab quái/xe nên client đã có sẵn bản gốc:
    /// host chỉ cần gửi tên template + NetworkObjectId của gốc để client tự dựng một bản sao
    /// không va chạm, không logic gameplay.
    /// </summary>
    public sealed class NetVisualSpawn : MonoBehaviour
    {
        private const string MessageName = "IW.Visual";
        private const string EndMessageName = "IW.VisualEnd";
        private const int MaxNameLength = 64;

        /// <summary>Instance duy nhất trên root Network.</summary>
        public static NetVisualSpawn Instance { get; private set; }

        [Tooltip("Tên template tối đa được gửi xuống client (chặn payload quá dài).")]
        [SerializeField, Min(8)] private int maxNameLength = MaxNameLength;

        [Tooltip("Số bản sao visual tối đa client giữ cùng lúc để tránh rác khi host spam.")]
        [SerializeField, Min(1)] private int maxGhosts = 120;

        private bool m_Registered;
        private int m_NextHandle = 1;
        private readonly List<Ghost> m_Ghosts = new List<Ghost>(64);
        private readonly List<Ghost> m_GhostBuffer = new List<Ghost>(64);

        /// <summary>Ánh xạ object nguồn (host) sang handle ghost đã gửi, để gửi lệnh thu hồi đúng bản sao.</summary>
        private static readonly ConditionalWeakTable<GameObject, GhostHandle> s_Handles =
            new ConditionalWeakTable<GameObject, GhostHandle>();
        private sealed class GhostHandle { public int Handle; }

        private struct Ghost
        {
            public GameObject Instance;
            public int Handle;
            public float DestroyAt;
        }

        /// <summary>Số bản sao visual client đang giữ (dùng cho log/kiểm chứng).</summary>
        public int GhostCount => m_Ghosts.Count;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (!m_Registered) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.CustomMessagingManager != null)
            {
                manager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
                manager.CustomMessagingManager.UnregisterNamedMessageHandler(EndMessageName);
            }

            m_Registered = false;
        }

        private void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null) return;

            // CustomMessagingManager có thể còn null trong lúc NGO đang khởi tạo hoặc vừa shutdown;
            // truy cập thẳng sẽ ném NullReferenceException mỗi frame (từng làm ngập log player build).
            CustomMessagingManager messaging = manager.CustomMessagingManager;
            bool listening = manager.IsListening;

            if (listening && !m_Registered && messaging != null)
            {
                messaging.RegisterNamedMessageHandler(MessageName, HandleMessage);
                messaging.RegisterNamedMessageHandler(EndMessageName, HandleEndMessage);
                m_Registered = true;
            }
            else if (!listening && m_Registered)
            {
                if (messaging != null)
                {
                    messaging.UnregisterNamedMessageHandler(MessageName);
                    messaging.UnregisterNamedMessageHandler(EndMessageName);
                }

                m_Registered = false;
            }

            if (m_Ghosts.Count == 0) return;

            float now = Time.time;
            m_GhostBuffer.Clear();
            for (int i = 0; i < m_Ghosts.Count; i++)
            {
                Ghost ghost = m_Ghosts[i];
                if (ghost.Instance == null)
                {
                    continue;
                }

                if (ghost.DestroyAt > 0f && now >= ghost.DestroyAt)
                {
                    Destroy(ghost.Instance);
                    continue;
                }

                m_GhostBuffer.Add(ghost);
            }

            m_Ghosts.Clear();
            m_Ghosts.AddRange(m_GhostBuffer);
        }

        /// <summary>
        /// Host gửi yêu cầu dựng bản sao visual xuống toàn bộ client.
        /// </summary>
        /// <param name="template">Template con (đang inactive) nằm trong prefab gốc.</param>
        /// <param name="position">Vị trí thế giới của bản sao.</param>
        /// <param name="rotation">Góc xoay của bản sao.</param>
        /// <param name="lifetime">Số giây tồn tại (0 = để client tự dọn theo logic gốc).</param>
        /// <param name="animate">True: giữ script để bản sao tự diễn hoạt; False: đóng băng thành sprite tĩnh.</param>
        /// <returns>Handle của bản sao (0 nếu không gửi được) để gọi <see cref="BroadcastDespawn"/> khi cần thu hồi.</returns>
        public int Broadcast(GameObject template, Vector3 position, Quaternion rotation, float lifetime = 0f, bool animate = true)
        {
            if (template == null) return 0;

            NetworkObject root = template.GetComponentInParent<NetworkObject>(true);
            if (root == null) return 0;

            return BroadcastByName(root, template.name, position, rotation, lifetime, animate);
        }

        /// <summary>
        /// Gửi bản sao visual khi đã biết trước NetworkObject gốc và tên template
        /// (dùng cho các template vừa được tách khỏi cây phân cấp, ví dụ bản sao Shadow Exchange).
        /// </summary>
        public int BroadcastByName(NetworkObject root, string templateName, Vector3 position, Quaternion rotation,
            float lifetime = 0f, bool animate = true)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || root == null) return 0;
            if (manager.CustomMessagingManager == null) return 0;

            if (string.IsNullOrEmpty(templateName)) return 0;
            if (templateName.Length > maxNameLength) templateName = templateName.Substring(0, maxNameLength);

            int handle = m_NextHandle++;
            if (m_NextHandle <= 0) m_NextHandle = 1;

            using FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteValueSafe(handle);
            writer.WriteValueSafe(root.NetworkObjectId);
            writer.WriteValueSafe(templateName);
            writer.WriteValueSafe((Vector2)position);
            writer.WriteValueSafe(rotation.eulerAngles.z);
            writer.WriteValueSafe(lifetime);
            writer.WriteValueSafe(animate);
            manager.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer);

            return handle;
        }

        /// <summary>Host gửi lệnh thu hồi một bản sao visual đã gửi trước đó.</summary>
        public void BroadcastDespawn(int handle)
        {
            if (handle == 0) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || manager.CustomMessagingManager == null) return;

            using FastBufferWriter writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
            writer.WriteValueSafe(handle);
            manager.CustomMessagingManager.SendNamedMessageToAll(EndMessageName, writer);
        }

        /// <summary>
        /// Gắn ghost cho một object vừa spawn (vùng hiệu ứng, đạn...) và ghi nhớ handle để thu hồi sau.
        /// Offline hoặc khi không phải host thì chỉ ghi nhớ 0 (không gửi gì).
        /// </summary>
        public static void Follow(GameObject template, GameObject instance, float lifetime = 0f, bool animate = true)
        {
            if (template == null || instance == null) return;

            int handle = 0;
            if (TankBrain.NetworkSessionActive && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && Instance != null)
                handle = Instance.Broadcast(template, instance.transform.position, instance.transform.rotation, lifetime, animate);

            Remember(instance, handle);
        }

        /// <summary>Ghi nhớ handle ghost gắn với một object nguồn để thu hồi đúng lúc (host).</summary>
        public static void Remember(GameObject source, int handle)
        {
            if (source == null) return;

            s_Handles.Remove(source);
            if (handle != 0) s_Handles.Add(source, new GhostHandle { Handle = handle });
        }

        /// <summary>Gửi lệnh thu hồi ghost của object nguồn (host, gọi khi object nguồn hết hiệu lực).</summary>
        public static void End(GameObject source)
        {
            if (source == null) return;
            if (!s_Handles.TryGetValue(source, out GhostHandle handle)) return;

            s_Handles.Remove(source);
            Instance?.BroadcastDespawn(handle.Handle);
        }

        private void HandleMessage(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer) return;

            reader.ReadValueSafe(out int handle);
            reader.ReadValueSafe(out ulong rootId);
            reader.ReadValueSafe(out string templateName);
            reader.ReadValueSafe(out Vector2 position);
            reader.ReadValueSafe(out float rotationZ);
            reader.ReadValueSafe(out float lifetime);
            reader.ReadValueSafe(out bool animate);

            if (string.IsNullOrEmpty(templateName) || m_Ghosts.Count >= maxGhosts) return;
            if (manager.SpawnManager == null || !manager.SpawnManager.SpawnedObjects.TryGetValue(rootId, out NetworkObject root)) return;

            Transform template = FindChildByName(root.transform, templateName);
            if (template == null) return;

            GameObject ghost = Instantiate(template.gameObject, position, Quaternion.Euler(0f, 0f, rotationZ));
            ghost.name = templateName + " (Ghost)";
            ghost.SetActive(true);

            Collider2D[] colliders = ghost.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            if (!animate)
            {
                MonoBehaviour[] behaviours = ghost.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++) behaviours[i].enabled = false;
            }

            m_Ghosts.Add(new Ghost
            {
                Instance = ghost,
                Handle = handle,
                DestroyAt = lifetime > 0f ? Time.time + lifetime : 0f
            });
        }

        private void HandleEndMessage(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer) return;

            reader.ReadValueSafe(out int handle);
            if (handle == 0) return;

            for (int i = m_Ghosts.Count - 1; i >= 0; i--)
            {
                if (m_Ghosts[i].Handle != handle) continue;
                if (m_Ghosts[i].Instance != null) Destroy(m_Ghosts[i].Instance);
                m_Ghosts.RemoveAt(i);
            }
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != root && children[i].name == name) return children[i];
            }

            return null;
        }
    }
}
