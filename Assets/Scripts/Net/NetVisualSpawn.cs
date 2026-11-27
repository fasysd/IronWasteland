using System.Collections.Generic;
using System.Runtime.CompilerServices;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
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

        /// <summary>Kênh dựng visual thủ tục (vệt lửa, gai đá) — các object này không có template trong prefab.</summary>
        private const string PrimitiveMessageName = "IW.VisualPrimitive";

        /// <summary>Kênh visual kỹ năng quái: bản sao bám theo root NetworkObject hoặc đứng tại vị trí cast.</summary>
        private const string SkillMessageName = "IW.SkillVisual";
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

            /// <summary>Vận tốc bay (unit/giây) cho đạn; bằng 0 với vùng đứng yên và bản sao tĩnh.</summary>
            public Vector2 Velocity;

            /// <summary>Renderer của ghost thủ tục (null với ghost dựng từ template).</summary>
            public SpriteRenderer Renderer;

            /// <summary>Màu gốc để mờ dần theo thời gian (chỉ dùng cho ghost thủ tục có hiệu ứng mờ).</summary>
            public Color BaseColor;

            /// <summary>True: alpha giảm dần đều tới 0 trong suốt vòng đời (vệt lửa).</summary>
            public bool Fade;

            public float SpawnTime;
            public float Lifetime;

            /// <summary>NetworkObjectId của mục tiêu mà ghost đạn đang bám theo (0 = bay thẳng).</summary>
            public ulong TargetId;

            /// <summary>Tốc độ bẻ lái ban đầu (độ/giây) và thời gian trễ trước khi bẻ lái.</summary>
            public float TurnSpeed;
            public float HomingDelay;

            /// <summary>Cửa sổ bẻ lái; trong cửa sổ này tốc độ xoay giảm dần về 0.</summary>
            public float HomingDuration;

            /// <summary>True: ghost được parent vào root NetworkObject và bám theo quái (aura, cone, skill con).</summary>
            public bool Attached;
        }

        /// <summary>Số bản sao visual client đang giữ (dùng cho log/kiểm chứng).</summary>
        public int GhostCount => m_Ghosts.Count;

        private void Awake()
        {
            // Bản sao trên object của scene mới không được cướp Instance persistent.
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

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
                manager.CustomMessagingManager.UnregisterNamedMessageHandler(PrimitiveMessageName);
                manager.CustomMessagingManager.UnregisterNamedMessageHandler(SkillMessageName);
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
                messaging.RegisterNamedMessageHandler(PrimitiveMessageName, HandlePrimitiveMessage);
                messaging.RegisterNamedMessageHandler(SkillMessageName, HandleSkillMessage);
                m_Registered = true;
            }
            else if (!listening && m_Registered)
            {
                if (messaging != null)
                {
                    messaging.UnregisterNamedMessageHandler(MessageName);
                    messaging.UnregisterNamedMessageHandler(EndMessageName);
                    messaging.UnregisterNamedMessageHandler(PrimitiveMessageName);
                    messaging.UnregisterNamedMessageHandler(SkillMessageName);
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

                // Đạn: client tự bay theo vận tốc host gửi, không phụ thuộc script của projectile
                // (script gốc chỉ chạy khi đã Initialize trên host nên trên client sẽ đứng yên).
                if (ghost.Velocity.sqrMagnitude > 0f)
                {
                    // Đạn đuổi mục tiêu: bẻ lái giống hệt bản gốc trên host rồi mới bay.
                    if (ghost.TargetId != 0UL)
                    {
                        float elapsed = now - ghost.SpawnTime;
                        if (elapsed >= ghost.HomingDelay)
                        {
                            Transform homingTarget = ResolveNetworkTransform(ghost.TargetId);
                            if (homingTarget != null)
                            {
                                float window = ghost.HomingDuration > 0f
                                    ? ghost.HomingDuration
                                    : Mathf.Max(0.001f, ghost.Lifetime - ghost.HomingDelay);
                                float progress = Mathf.Clamp01((elapsed - ghost.HomingDelay) / Mathf.Max(0.001f, window));
                                float turnDegrees = Mathf.Lerp(ghost.TurnSpeed, 0f, progress);

                                Vector2 toTarget = (Vector2)homingTarget.position - (Vector2)ghost.Instance.transform.position;
                                if (turnDegrees > 0f && toTarget.sqrMagnitude > 0.0001f)
                                {
                                    float speed = ghost.Velocity.magnitude;
                                    Vector2 direction = Vector2.MoveTowards(
                                        ghost.Velocity.normalized,
                                        toTarget.normalized,
                                        turnDegrees * Mathf.Deg2Rad * Time.deltaTime);
                                    ghost.Velocity = direction.normalized * speed;
                                }
                            }
                        }
                    }

                    ghost.Instance.transform.position += (Vector3)(ghost.Velocity * Time.deltaTime);
                }

                // Vệt lửa: client tự mờ dần đúng như bản gốc trên host.
                if (ghost.Fade && ghost.Renderer != null)
                {
                    float lifeRatio = ghost.Lifetime > 0.0001f
                        ? Mathf.Clamp01(1f - (now - ghost.SpawnTime) / ghost.Lifetime)
                        : 1f;
                    Color color = ghost.BaseColor;
                    color.a *= lifeRatio;
                    ghost.Renderer.color = color;
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
            return Broadcast(template, position, rotation, default(Vector2), lifetime, animate);
        }

        /// <summary>
        /// Như <see cref="Broadcast(GameObject, Vector3, Quaternion, float, bool)"/> nhưng có vận tốc bay
        /// cho đạn: client sẽ tự di chuyển bản sao theo vận tốc này thay vì chờ script projectile tự chạy.
        /// </summary>
        public int Broadcast(GameObject template, Vector3 position, Quaternion rotation, Vector2 velocity,
            float lifetime = 0f, bool animate = true)
        {
            if (template == null) return 0;

            NetworkObject root = template.GetComponentInParent<NetworkObject>(true);
            if (root == null) return 0;

            return BroadcastByName(root, template.name, position, rotation, velocity, lifetime, animate);
        }

        /// <summary>
        /// Gửi bản sao visual khi đã biết trước NetworkObject gốc và tên template
        /// (dùng cho các template vừa được tách khỏi cây phân cấp, ví dụ bản sao Shadow Exchange).
        /// </summary>
        public int BroadcastByName(NetworkObject root, string templateName, Vector3 position, Quaternion rotation,
            float lifetime = 0f, bool animate = true)
        {
            return BroadcastByName(root, templateName, position, rotation, default(Vector2), lifetime, animate);
        }

        /// <summary>Bản có vận tốc bay cho đạn (client tự di chuyển bản sao), kèm tuỳ chọn đuổi mục tiêu.</summary>
        public int BroadcastByName(NetworkObject root, string templateName, Vector3 position, Quaternion rotation,
            Vector2 velocity, float lifetime = 0f, bool animate = true, ulong homingTargetId = 0UL,
            float turnSpeed = 0f, float homingDelay = 0f, float homingDuration = 0f)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || root == null) return 0;
            if (manager.CustomMessagingManager == null) return 0;

            if (string.IsNullOrEmpty(templateName)) return 0;
            if (templateName.Length > maxNameLength) templateName = templateName.Substring(0, maxNameLength);

            int handle = m_NextHandle++;
            if (m_NextHandle <= 0) m_NextHandle = 1;

            using FastBufferWriter writer = new FastBufferWriter(160, Allocator.Temp);
            writer.WriteValueSafe(handle);
            writer.WriteValueSafe(root.NetworkObjectId);
            writer.WriteValueSafe(templateName);
            writer.WriteValueSafe((Vector2)position);
            writer.WriteValueSafe(rotation.eulerAngles.z);
            writer.WriteValueSafe(velocity);
            writer.WriteValueSafe(lifetime);
            writer.WriteValueSafe(animate);
            writer.WriteValueSafe(homingTargetId);
            writer.WriteValueSafe(turnSpeed);
            writer.WriteValueSafe(homingDelay);
            writer.WriteValueSafe(homingDuration);
            manager.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer);

            return handle;
        }

        /// <summary>
        /// Host gửi bản sao của một object con (ví dụ "Skill" đang inactive) xuống client.
        /// Bản sao được parent vào root NetworkObject và giữ offset local, nên aura/cone bám đúng quái.
        /// </summary>
        public int BroadcastSkillVisual(GameObject template, NetworkObject root, Vector3 localOffset,
            float localRotationZ, float lifetime = 0f, bool attachToRoot = true, bool animate = false,
            Vector2 velocity = default(Vector2), ulong homingTargetId = 0UL, float turnSpeed = 0f,
            float homingDelay = 0f, float homingDuration = 0f)
        {
            if (template == null) return 0;
            return BroadcastSkillPayload(root, template.name, 0, localOffset, localRotationZ,
                template.transform.lossyScale, lifetime, attachToRoot, animate, velocity, homingTargetId,
                turnSpeed, homingDelay, homingDuration);
        }

        /// <summary>
        /// Host gửi visual kỹ năng theo Id trong catalog dùng chung (VFX standalone không có NetworkObject).
        /// </summary>
        public int BroadcastSkillVfx(int visualId, NetworkObject root, Vector3 localOffset, float localRotationZ,
            Vector3 worldScale, float lifetime = 0f, bool attachToRoot = false, bool animate = false,
            Vector2 velocity = default(Vector2), ulong homingTargetId = 0UL, float turnSpeed = 0f,
            float homingDelay = 0f, float homingDuration = 0f)
        {
            if (visualId == 0) return 0;
            return BroadcastSkillPayload(root, string.Empty, visualId, localOffset, localRotationZ,
                worldScale, lifetime, attachToRoot, animate, velocity, homingTargetId, turnSpeed,
                homingDelay, homingDuration);
        }

        private int BroadcastSkillPayload(NetworkObject root, string templateName, int visualId,
            Vector3 localOffset, float localRotationZ, Vector3 worldScale, float lifetime,
            bool attachToRoot, bool animate, Vector2 velocity, ulong homingTargetId, float turnSpeed,
            float homingDelay, float homingDuration)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || root == null) return 0;
            if (manager.CustomMessagingManager == null) return 0;
            if (visualId == 0 && string.IsNullOrEmpty(templateName)) return 0;

            if (templateName == null) templateName = string.Empty;
            if (templateName.Length > maxNameLength) templateName = templateName.Substring(0, maxNameLength);

            int handle = m_NextHandle++;
            if (m_NextHandle <= 0) m_NextHandle = 1;

            using FastBufferWriter writer = new FastBufferWriter(200, Allocator.Temp);
            writer.WriteValueSafe(handle);
            writer.WriteValueSafe(root.NetworkObjectId);
            writer.WriteValueSafe(visualId);
            writer.WriteValueSafe(templateName);
            writer.WriteValueSafe(localOffset);
            writer.WriteValueSafe(localRotationZ);
            writer.WriteValueSafe(worldScale);
            writer.WriteValueSafe(lifetime);
            writer.WriteValueSafe(attachToRoot);
            writer.WriteValueSafe(animate);
            writer.WriteValueSafe(velocity);
            writer.WriteValueSafe(homingTargetId);
            writer.WriteValueSafe(turnSpeed);
            writer.WriteValueSafe(homingDelay);
            writer.WriteValueSafe(homingDuration);
            manager.CustomMessagingManager.SendNamedMessageToAll(SkillMessageName, writer);

            return handle;
        }

        /// <summary>
        /// Host phát visual của một object con đang active (skill quái). Tự tính offset local so với root
        /// và ghi nhớ handle để <see cref="End"/> thu hồi khi skill kết thúc.
        /// </summary>
        public static int PlaySkillVisual(GameObject template, float lifetime = 0f, bool animate = false,
            bool attachToRoot = true, Vector2 velocity = default(Vector2), ulong homingTargetId = 0UL,
            float turnSpeed = 0f, float homingDelay = 0f, float homingDuration = 0f)
        {
            if (template == null) return 0;
            if (!TankBrain.NetworkSessionActive || NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsServer || Instance == null) return 0;

            NetworkObject root = template.GetComponentInParent<NetworkObject>(true);
            if (root == null)
            {
                Debug.LogWarning("[NetVisualSpawn] Skill visual không nằm trong NetworkObject: " + template.name, template);
                return 0;
            }

            Vector3 localOffset = root.transform.InverseTransformPoint(template.transform.position);
            float localRotationZ = Mathf.DeltaAngle(root.transform.eulerAngles.z, template.transform.eulerAngles.z);
            int handle = Instance.BroadcastSkillVisual(template, root, localOffset, localRotationZ, lifetime,
                attachToRoot, animate, velocity, homingTargetId, turnSpeed, homingDelay, homingDuration);
            Remember(template, handle);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[NetVisual] host skill ghost " + template.name + " handle=" + handle + " root=" + root.name);
#endif
            return handle;
        }

        /// <summary>
        /// Host phát một VFX standalone (không có NetworkObject) theo catalog Id.
        /// Client dựng bản sao visual-only tại offset local của boss.
        /// </summary>
        public static int PlayVfx(GameObject prefab, GameObject instance, NetworkObject root, float lifetime = 0f,
            bool attachToRoot = false, Vector2 velocity = default(Vector2), ulong homingTargetId = 0UL,
            float turnSpeed = 0f, float homingDelay = 0f, float homingDuration = 0f)
        {
            if (prefab == null || instance == null) return 0;
            if (!TankBrain.NetworkSessionActive || NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsServer || Instance == null) return 0;

            if (root == null) root = instance.GetComponentInParent<NetworkObject>(true);
            if (root == null) return 0;

            int visualId = NetSkillVisualCatalog.FindId(prefab);
            if (visualId == 0)
            {
                Debug.LogWarning("[NetVisualSpawn] VFX chưa có trong NetSkillVisualCatalog: " + prefab.name, prefab);
                return 0;
            }

            float configuredLifetime = lifetime > 0f ? lifetime : NetSkillVisualCatalog.GetLifetime(prefab);
            Vector3 localOffset = root.transform.InverseTransformPoint(instance.transform.position);
            float localRotationZ = Mathf.DeltaAngle(root.transform.eulerAngles.z, instance.transform.eulerAngles.z);
            int handle = Instance.BroadcastSkillVfx(visualId, root, localOffset, localRotationZ,
                instance.transform.lossyScale, configuredLifetime, attachToRoot, false, velocity,
                homingTargetId, turnSpeed, homingDelay, homingDuration);
            Remember(instance, handle);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[NetVisual] host vfx ghost " + prefab.name + " id=" + visualId + " handle=" + handle + " root=" + root.name);
#endif
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
        /// Host gửi visual thủ tục (không có template trong prefab): vệt lửa, gai đá...
        /// Client tự dựng SpriteRenderer đơn giản, bay theo vận tốc và mờ dần nếu bật <paramref name="fade"/>.
        /// </summary>
        public int BroadcastPrimitive(Vector3 position, Vector2 velocity, float radius, float lifetime,
            bool fade, int sortingOrder, Color color, float rotationDeg = 0f, float stretchX = 1f)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || manager.CustomMessagingManager == null) return 0;

            int handle = m_NextHandle++;
            if (m_NextHandle <= 0) m_NextHandle = 1;

            using FastBufferWriter writer = new FastBufferWriter(80, Allocator.Temp);
            writer.WriteValueSafe(handle);
            writer.WriteValueSafe((Vector2)position);
            writer.WriteValueSafe(velocity);
            writer.WriteValueSafe(Mathf.Max(0.01f, radius));
            writer.WriteValueSafe(lifetime);
            writer.WriteValueSafe(fade);
            writer.WriteValueSafe(sortingOrder);
            Color32 packed = color;
            writer.WriteValueSafe(packed);
            writer.WriteValueSafe(rotationDeg);
            writer.WriteValueSafe(Mathf.Max(0.01f, stretchX));
            manager.CustomMessagingManager.SendNamedMessageToAll(PrimitiveMessageName, writer);

            return handle;
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
            if (senderClientId != NetworkManager.ServerClientId) return; // chỉ host được gửi ghost
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer) return;

            reader.ReadValueSafe(out int handle);
            reader.ReadValueSafe(out ulong rootId);
            reader.ReadValueSafe(out string templateName);
            reader.ReadValueSafe(out Vector2 position);
            reader.ReadValueSafe(out float rotationZ);
            reader.ReadValueSafe(out Vector2 velocity);
            reader.ReadValueSafe(out float lifetime);
            reader.ReadValueSafe(out bool animate);
            reader.ReadValueSafe(out ulong homingTargetId);
            reader.ReadValueSafe(out float turnSpeed);
            reader.ReadValueSafe(out float homingDelay);
            reader.ReadValueSafe(out float homingDuration);

            if (string.IsNullOrEmpty(templateName)) return;
            if (m_Ghosts.Count >= maxGhosts) RemoveOldestGhost();
            if (manager.SpawnManager == null || !manager.SpawnManager.SpawnedObjects.TryGetValue(rootId, out NetworkObject root)) return;

            Transform template = FindChildByName(root.transform, templateName);
            if (template == null) return;

            GameObject ghost = Instantiate(template.gameObject, position, Quaternion.Euler(0f, 0f, rotationZ));
            ghost.name = templateName + " (Ghost)";
            ghost.SetActive(true);

            Collider2D[] colliders = ghost.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            // Đạn: tắt hết script (script gốc cần Initialize của host nên trên client sẽ đứng yên)
            // rồi để NetVisualSpawn tự bay bản sao theo vận tốc. Vật thể tĩnh (vùng, decoy) giữ nguyên.
            bool flying = velocity.sqrMagnitude > 0f;
            if (flying || !animate)
            {
                MonoBehaviour[] behaviours = ghost.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++) behaviours[i].enabled = false;
            }

            m_Ghosts.Add(new Ghost
            {
                Instance = ghost,
                Handle = handle,
                DestroyAt = lifetime > 0f ? Time.time + lifetime : Time.time + DefaultGhostLifetime,
                Velocity = flying ? velocity : Vector2.zero,
                SpawnTime = Time.time,
                Lifetime = lifetime,
                TargetId = homingTargetId,
                TurnSpeed = turnSpeed,
                HomingDelay = homingDelay,
                HomingDuration = homingDuration
            });
        }

        /// <summary>
        /// Client dựng bản sao skill/VFX do host gửi. Bản gắn root được parent để bám quái;
        /// bản standalone (VFX) đứng tại vị trí cast. Mọi bản sao đều bị tắt collider + script gameplay.
        /// </summary>
        private void HandleSkillMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return; // chỉ host được gửi ghost
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer) return;

            reader.ReadValueSafe(out int handle);
            reader.ReadValueSafe(out ulong rootId);
            reader.ReadValueSafe(out int visualId);
            reader.ReadValueSafe(out string templateName);
            reader.ReadValueSafe(out Vector3 localOffset);
            reader.ReadValueSafe(out float localRotationZ);
            reader.ReadValueSafe(out Vector3 worldScale);
            reader.ReadValueSafe(out float lifetime);
            reader.ReadValueSafe(out bool attachToRoot);
            reader.ReadValueSafe(out bool animate);
            reader.ReadValueSafe(out Vector2 velocity);
            reader.ReadValueSafe(out ulong homingTargetId);
            reader.ReadValueSafe(out float turnSpeed);
            reader.ReadValueSafe(out float homingDelay);
            reader.ReadValueSafe(out float homingDuration);

            if (m_Ghosts.Count >= maxGhosts) RemoveOldestGhost();
            if (manager.SpawnManager == null || !manager.SpawnManager.SpawnedObjects.TryGetValue(rootId, out NetworkObject root)) return;

            GameObject prefab = null;
            float configuredLifetime = 0f;
            if (visualId != 0)
            {
                if (!NetSkillVisualCatalog.TryGet(visualId, out prefab, out configuredLifetime))
                {
                    Debug.LogWarning("[NetVisualSpawn] Client thiếu VFX Id " + visualId + " trong NetSkillVisualCatalog.");
                    return;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(templateName)) return;
                Transform template = FindChildByName(root.transform, templateName);
                if (template == null) return;
                prefab = template.gameObject;
            }

            if (prefab == null) return;

            GameObject ghost;
            if (attachToRoot)
            {
                ghost = Instantiate(prefab, root.transform, false);
                ghost.transform.localPosition = localOffset;
                ghost.transform.localRotation = Quaternion.Euler(0f, 0f, localRotationZ);
                ghost.transform.localScale = DivideScale(worldScale, root.transform.lossyScale);
            }
            else
            {
                Vector3 worldPosition = root.transform.TransformPoint(localOffset);
                Quaternion worldRotation = Quaternion.Euler(0f, 0f, root.transform.eulerAngles.z + localRotationZ);
                ghost = Instantiate(prefab, worldPosition, worldRotation);
                ghost.transform.localScale = worldScale;
            }

            ghost.name = prefab.name + " (Skill Ghost)";
            ghost.SetActive(true);
            NetVisualGhost.Sanitize(ghost, true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SpriteRenderer ghostRenderer = ghost.GetComponentInChildren<SpriteRenderer>(true);
            Debug.Log("[NetVisual] client skill ghost " + ghost.name + " attached=" + attachToRoot + " root=" + root.name +
                      " scale=" + ghost.transform.lossyScale.ToString("F2") + " sorting=" +
                      (ghostRenderer != null ? ghostRenderer.sortingOrder : -999) +
                      " localPos=" + ghost.transform.localPosition.ToString("F2"));
#endif

            float ghostLifetime = lifetime > 0f ? lifetime : configuredLifetime;
            m_Ghosts.Add(new Ghost
            {
                Instance = ghost,
                Handle = handle,
                DestroyAt = ghostLifetime > 0f ? Time.time + ghostLifetime : Time.time + DefaultGhostLifetime,
                Velocity = attachToRoot ? Vector2.zero : velocity,
                SpawnTime = Time.time,
                Lifetime = ghostLifetime,
                TargetId = attachToRoot ? 0UL : homingTargetId,
                TurnSpeed = turnSpeed,
                HomingDelay = homingDelay,
                HomingDuration = homingDuration,
                Attached = attachToRoot
            });
        }

        /// <summary>Tìm transform của NetworkObject theo id (dùng cho ghost đạn đuổi mục tiêu).</summary>
        private static Transform ResolveNetworkTransform(ulong networkObjectId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.SpawnManager == null) return null;
            return manager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject target)
                ? target.transform
                : null;
        }

        /// <summary>Thời gian sống mặc định cho ghost không khai báo lifetime — tránh ghost "vĩnh viễn" khoá bộ đệm.</summary>
        private const float DefaultGhostLifetime = 30f;

        private void RemoveOldestGhost()
        {
            if (m_Ghosts.Count == 0) return;

            Ghost oldest = m_Ghosts[0];
            if (oldest.Instance != null) Destroy(oldest.Instance);
            m_Ghosts.RemoveAt(0);
        }

        private void HandleEndMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return; // chỉ host được thu hồi ghost
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

        /// <summary>Client dựng lại visual thủ tục do host gửi (vệt lửa, gai đá).</summary>
        private void HandlePrimitiveMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return; // chỉ host được gửi ghost
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer) return;

            reader.ReadValueSafe(out int handle);
            reader.ReadValueSafe(out Vector2 position);
            reader.ReadValueSafe(out Vector2 velocity);
            reader.ReadValueSafe(out float radius);
            reader.ReadValueSafe(out float lifetime);
            reader.ReadValueSafe(out bool fade);
            reader.ReadValueSafe(out int sortingOrder);
            reader.ReadValueSafe(out Color32 packedColor);
            reader.ReadValueSafe(out float rotationDeg);
            reader.ReadValueSafe(out float stretchX);

            if (m_Ghosts.Count >= maxGhosts) RemoveOldestGhost();

            GameObject ghost = new GameObject(fade ? "SoftCircle (Ghost)" : "Circle (Ghost)");
            ghost.transform.position = position;
            ghost.transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);
            ghost.transform.localScale = new Vector3(
                Mathf.Max(0.01f, radius) * 2f * Mathf.Max(0.01f, stretchX),
                Mathf.Max(0.01f, radius) * 2f,
                1f);

            SpriteRenderer renderer = ghost.AddComponent<SpriteRenderer>();
            renderer.sprite = fade ? SpriteFactory.SoftCircle : SpriteFactory.Circle;
            renderer.color = packedColor;
            renderer.sortingOrder = sortingOrder;

            float ghostLifetime = lifetime > 0f ? lifetime : DefaultGhostLifetime;
            m_Ghosts.Add(new Ghost
            {
                Instance = ghost,
                Handle = handle,
                DestroyAt = Time.time + ghostLifetime,
                Velocity = velocity,
                Renderer = renderer,
                BaseColor = packedColor,
                Fade = fade,
                SpawnTime = Time.time,
                Lifetime = ghostLifetime
            });
        }

        /// <summary>Chia scale từng trục để bản ghost gắn root giữ đúng kích thước thế giới của host.</summary>
        private static Vector3 DivideScale(Vector3 value, Vector3 divisor)
        {
            return new Vector3(
                Mathf.Abs(divisor.x) > 0.0001f ? value.x / divisor.x : value.x,
                Mathf.Abs(divisor.y) > 0.0001f ? value.y / divisor.y : value.y,
                Mathf.Abs(divisor.z) > 0.0001f ? value.z / divisor.z : value.z);
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
