using IronWasteland.Enemies;
using IronWasteland.Enemies.Navigation;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Đồng bộ máu quái từ host xuống client và chia quyền mô phỏng.
    ///
    /// Host chạy toàn bộ AI, va chạm và sát thương; client chỉ nhận máu để vẽ thanh máu / hiệu ứng chết.
    /// Vì vậy trên client: tắt <see cref="Enemies.EnemyBrain.IsServerSimulated"/> (EnemyBrain.Update tự
    /// bỏ qua), tắt <c>EnemyMovementAgent</c> và chuyển Rigidbody2D sang kinematic để quái đứng yên
    /// đúng vị trí host gửi qua <see cref="NetworkTransform"/> (nếu prefab có gắn).
    /// </summary>
    [RequireComponent(typeof(EnemyBrain))]
    public class EnemyNetState : NetworkBehaviour
    {
        /// <summary>Chu kỳ host gửi máu; 10 Hz là đủ cho thanh máu, không cần mỗi khung hình.</summary>
        private const float SyncInterval = 0.1f;

        /// <summary>Chỉ ghi khi máu lệch quá ngưỡng này để tránh gói tin thừa.</summary>
        private const float ValueEpsilon = 0.1f;

        private readonly NetworkVariable<float> m_Health = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Máu tối đa host dùng lúc spawn. Gửi một lần kèm snapshot spawn (NetworkVariable khởi tạo),
        /// không gửi lại mỗi tick — client dùng để vẽ thanh máu đúng tỉ lệ.
        /// </summary>
        private readonly NetworkVariable<float> m_MaxHealth = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private EnemyBrain m_Brain;
        private Rigidbody2D m_Body;
        private EnemyMovementAgent m_Movement;
        private float m_SyncTimer;
        private float m_SentHealth;

        /// <summary>Máu hiện tại phía client (host trả máu thật từ brain).</summary>
        public float Health => m_Brain != null ? m_Brain.CurrentHealth : m_Health.Value;

        /// <summary>Máu tối đa host gửi lúc spawn (client có thể chưa kịp nhận thì đọc từ NetworkVariable).</summary>
        public float MaxHealth => m_Brain != null ? m_Brain.MaxHealth : m_MaxHealth.Value;

        private void Awake()
        {
            m_Brain = GetComponent<EnemyBrain>();
            m_Body = GetComponent<Rigidbody2D>();
            m_Movement = GetComponent<EnemyMovementAgent>();
        }

        public override void OnNetworkSpawn()
        {
            if (m_Brain == null) m_Brain = GetComponent<EnemyBrain>();

            m_Brain.IsServerSimulated = IsServer;
            m_Health.OnValueChanged += HandleHealthChanged;
            m_MaxHealth.OnValueChanged += HandleMaxHealthChanged;

            if (IsServer)
            {
                m_Movement?.SetServerSimulated(true);
                m_MaxHealth.Value = m_Brain.MaxHealth;
                m_SentHealth = m_Brain.CurrentHealth;
                m_Health.Value = m_SentHealth;
            }
            else
            {
                ApplyProxyMode();

                // Client tự khởi tạo brain một lần (để SetHealth chịu tác dụng) rồi mới áp máu host gửi.
                if (!m_Brain.IsInitialized) m_Brain.Initialize();
                ApplyMaxHealth(m_MaxHealth.Value);
                ApplyHealth(m_Health.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            m_Health.OnValueChanged -= HandleHealthChanged;
            m_MaxHealth.OnValueChanged -= HandleMaxHealthChanged;
        }

        private void Update()
        {
            if (!IsSpawned) return;
            if (!IsServer || m_Brain == null) return;
            if (!IsSpawned) return;

            m_SyncTimer += Time.deltaTime;
            if (m_SyncTimer < SyncInterval) return;
            m_SyncTimer = 0f;

            float current = m_Brain.CurrentHealth;
            if (Mathf.Abs(current - m_SentHealth) < ValueEpsilon) return;

            m_SentHealth = current;
            m_Health.Value = current;
        }

        private void HandleHealthChanged(float previousValue, float newValue)
        {
            if (IsServer) return;
            ApplyHealth(newValue);
        }

        /// <summary>Client áp máu nhận từ host; brain tự gọi Die() khi về 0 nên hiệu ứng chết chạy đúng.</summary>
        private void ApplyHealth(float value)
        {
            if (m_Brain == null) return;

            if (!m_Brain.IsInitialized) m_Brain.Initialize();
            m_Brain.SetHealth(value);
        }

        private void HandleMaxHealthChanged(float previousValue, float newValue)
        {
            if (IsServer) return;
            ApplyMaxHealth(newValue);
        }

        /// <summary>Client áp máu tối đa host gửi lúc spawn để thanh máu hiển thị đúng tỉ lệ.</summary>
        private void ApplyMaxHealth(float value)
        {
            if (m_Brain == null || value <= 0f) return;

            if (!m_Brain.IsInitialized) m_Brain.Initialize();
            m_Brain.SetMaxHealth(value);
        }

        /// <summary>Client không mô phỏng AI/di chuyển; giữ collider để va chạm cục bộ không phá vị trí.</summary>
        private void ApplyProxyMode()
        {
            if (m_Movement != null)
            {
                m_Movement.SetServerSimulated(false);
            }

            if (m_Body != null)
            {
                m_Body.linearVelocity = Vector2.zero;
                m_Body.angularVelocity = 0f;
                m_Body.bodyType = RigidbodyType2D.Kinematic;
            }
        }
    }
}
