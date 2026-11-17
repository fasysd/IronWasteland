using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Cầu nối phát số sát thương/hồi máu từ host xuống các client.
    ///
    /// Không dùng <see cref="NetworkBehaviour"/> vì NGO cấm gắn NetworkBehaviour lên
    /// GameObject của NetworkManager (và con của nó). Thay vào đó dùng named message
    /// của <see cref="CustomMessagingManager"/>: host gửi một gói nhỏ, client hiện chữ
    /// tại đúng vị trí thế giới — không cần biết ai đánh ai, không mô phỏng lại sát thương.
    /// </summary>
    public class NetCombatFeedback : MonoBehaviour
    {
        private const string MessageName = "IW.CombatFeedback";
        private const int MessageSize = 16;

        /// <summary>Gộp các tick sát thương theo thời gian (DoT) để khỏi gửi mỗi tick.</summary>
        private const float GroupWindow = 0.5f;

        /// <summary>Kích thước ô chia vị trí khi gộp — đủ lớn để các tick cùng một mục tiêu rơi vào một ô.</summary>
        private const float PositionBucketSize = 0.75f;

        /// <summary>Instance trong scene; host và client đều có.</summary>
        public static NetCombatFeedback Instance { get; private set; }

        private readonly System.Collections.Generic.Dictionary<long, PendingFeedback> m_Pending = new();
        private float m_NextFlushTime;
        private bool m_Registered;

        /// <summary>Số đang chờ gộp cho một ô vị trí.</summary>
        private struct PendingFeedback
        {
            public float Amount;
            public Vector3 Position;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            Unregister();
        }

        private void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            bool listening = manager != null && manager.IsListening;

            if (listening && !m_Registered)
            {
                Register(manager);
            }
            else if (!listening && m_Registered)
            {
                Unregister();
            }

            FlushPending(manager);
        }

        private void Register(NetworkManager manager)
        {
            if (manager.CustomMessagingManager == null) return;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, HandleMessage);
            m_Registered = true;
        }

        private void Unregister()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.CustomMessagingManager != null)
            {
                manager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
            }

            m_Registered = false;
        }

        /// <summary>Host gọi sau khi đã hiện chữ cục bộ; số sẽ được gộp rồi gửi mỗi 0,5 s một lần.</summary>
        public void Broadcast(float amount, Vector3 worldPosition)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || manager.CustomMessagingManager == null) return;
            if (amount <= 0f) return;

            long key = GetBucketKey(worldPosition);
            if (m_Pending.TryGetValue(key, out PendingFeedback pending))
            {
                pending.Amount += amount;
                m_Pending[key] = pending;
            }
            else
            {
                m_Pending[key] = new PendingFeedback { Amount = amount, Position = worldPosition };
            }
        }

        /// <summary>Gửi toàn bộ số đang chờ gộp; gọi mỗi khung hình nhưng chỉ thực sự gửi theo nhịp <see cref="GroupWindow"/>.</summary>
        private void FlushPending(NetworkManager manager)
        {
            if (m_Pending.Count == 0) return;
            if (manager == null || !manager.IsListening || !manager.IsServer || manager.CustomMessagingManager == null) return;
            if (Time.time < m_NextFlushTime) return;

            foreach (System.Collections.Generic.KeyValuePair<long, PendingFeedback> entry in m_Pending)
            {
                using FastBufferWriter writer = new(MessageSize, Allocator.Temp);
                writer.WriteValueSafe(entry.Value.Amount);
                writer.WriteValueSafe((Vector2)entry.Value.Position);
                manager.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer);
            }

            m_Pending.Clear();
            m_NextFlushTime = Time.time + GroupWindow;
        }

        /// <summary>Ô vị trí dùng để gộp các tick sát thương liên tiếp của cùng một mục tiêu.</summary>
        private static long GetBucketKey(Vector3 worldPosition)
        {
            long x = Mathf.RoundToInt(worldPosition.x / PositionBucketSize);
            long y = Mathf.RoundToInt(worldPosition.y / PositionBucketSize);
            return (x << 32) ^ (y & 0xFFFFFFFFL);
        }

        private static void HandleMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return; // chỉ host được phát số sát thương
            reader.ReadValueSafe(out float amount);
            reader.ReadValueSafe(out Vector2 position);
            TextDamageManager.Instance?.Show(amount, position);
        }
    }
}
