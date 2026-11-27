using IronWasteland.Enemies;
using IronWasteland.Enemies.Navigation;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>Receiver trên prefab quái để nhận lệnh phát VFX phía client từ Host.</summary>
    public interface INetVfxReceiver
    {
        void OnClientNetVfx(string key, Vector2 position, float rotationZ, Vector2 dir, float duration, float value);
    }

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
        /// <summary>Chu kỳ host gửi máu; 4 Hz là đủ cho thanh máu (giảm băng thông khi đông quái).</summary>
        private const float SyncInterval = 0.25f;

        /// <summary>Chu kỳ host gửi MoveSpeed / FlipX cho animation; 10 Hz đủ mượt mà nhẹ băng thông.</summary>
        private const float AnimSyncInterval = 0.1f;

        /// <summary>Chỉ ghi khi máu lệch quá ngưỡng này để tránh gói tin thừa.</summary>
        private const float ValueEpsilon = 0.5f;

        /// <summary>Chu kỳ host gửi hướng xoay model; 10 Hz đủ mượt cho quái và boss.</summary>
        private const float PoseInterval = 0.1f;

        /// <summary>Hệ số làm mượt tốc độ suy ra từ NetworkTransform ở client.</summary>
        private const float SpeedSmoothing = 12f;

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

        /// <summary>Góc xoay model (trục Z) do host gửi; NetworkTransform chỉ xoay root nên cần kênh riêng.</summary>
        private readonly NetworkVariable<float> m_ModelYaw = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private EnemyBrain m_Brain;
        private EnemyModel m_Model;
        private Rigidbody2D m_Body;
        private EnemyMovementAgent m_Movement;
        private float m_SyncTimer;
        private float m_PoseTimer;
        private float m_SentHealth;

        private Vector3 m_LastClientPosition;
        private float m_ClientSpeed;
        private bool m_ClientPoseReady;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private int m_LastClientStateHash;
        private float m_NextClientStateLog;
#endif

        /// <summary>Máu hiện tại phía client (host trả máu thật từ brain).</summary>
        public float Health => m_Brain != null ? m_Brain.CurrentHealth : m_Health.Value;

        /// <summary>Máu tối đa host gửi lúc spawn (client có thể chưa kịp nhận thì đọc từ NetworkVariable).</summary>
        public float MaxHealth => m_Brain != null ? m_Brain.MaxHealth : m_MaxHealth.Value;

        private void Awake()
        {
            m_Brain = GetComponent<EnemyBrain>();
            m_Model = GetComponentInChildren<EnemyModel>();
            m_Body = GetComponent<Rigidbody2D>();
            m_Movement = GetComponent<EnemyMovementAgent>();
        }

        public override void OnNetworkSpawn()
        {
            if (m_Brain == null) m_Brain = GetComponent<EnemyBrain>();
            if (m_Model == null) m_Model = GetComponentInChildren<EnemyModel>();

            m_Brain.IsServerSimulated = IsServer;
            m_Health.OnValueChanged += HandleHealthChanged;
            m_MaxHealth.OnValueChanged += HandleMaxHealthChanged;

            if (IsServer)
            {
                m_Movement?.SetServerSimulated(true);
                m_MaxHealth.Value = m_Brain.MaxHealth;
                m_SentHealth = m_Brain.CurrentHealth;
                m_Health.Value = m_SentHealth;
                m_ModelYaw.Value = ReadModelYaw();
            }
            else
            {
                ApplyProxyMode();

                // Client tự khởi tạo brain một lần (để SetHealth chịu tác dụng) rồi mới áp máu host gửi.
                if (!m_Brain.IsInitialized) m_Brain.Initialize();
                ApplyMaxHealth(m_MaxHealth.Value);
                ApplyHealth(m_Health.Value);
                m_LastClientPosition = transform.position;
                m_ClientPoseReady = true;
                ApplyModelYaw(m_ModelYaw.Value, true);
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

            if (!IsServer)
            {
                UpdateClientVisuals();
                return;
            }

            if (m_Brain == null) return;

            UpdateServerHealth();
            UpdateServerPose();
        }

        private void UpdateServerHealth()
        {
            m_SyncTimer += Time.deltaTime;
            if (m_SyncTimer >= SyncInterval)
            {
                m_SyncTimer = 0f;
                float current = m_Brain.CurrentHealth;
                if (Mathf.Abs(current - m_SentHealth) >= ValueEpsilon)
                {
                    m_SentHealth = current;
                    m_Health.Value = current;
                }
            }
        }

        /// <summary>Host phát trigger animation xuống toàn bộ client.</summary>
        public void BroadcastAnimTrigger(int triggerHash)
        {
            if (!IsSpawned || !IsServer) return;
            TriggerAnimClientRpc(triggerHash);
        }

        /// <summary>Host phát trigger animation theo tên xuống toàn bộ client.</summary>
        public void BroadcastAnimTrigger(string triggerName)
        {
            if (string.IsNullOrEmpty(triggerName)) return;
            BroadcastAnimTrigger(Animator.StringToHash(triggerName));
        }

        /// <summary>Host phát lệnh dựng VFX kỹ năng xuống toàn bộ client (client chỉ hiển thị, không gây sát thương).</summary>
        public void BroadcastVfx(string key, Vector2 position, float rotationZ = 0f,
            Vector2 dir = default, float duration = 0f, float value = 0f)
        {
            if (!IsSpawned || !IsServer || string.IsNullOrEmpty(key)) return;
            PlayVfxClientRpc(key, position, rotationZ, dir, duration, value);
        }

        [ClientRpc]
        private void PlayVfxClientRpc(string key, Vector2 position, float rotationZ,
            Vector2 dir, float duration, float value)
        {
            if (IsServer) return;
            INetVfxReceiver[] receivers = GetComponentsInChildren<INetVfxReceiver>(true);
            for (int i = 0; i < receivers.Length; i++)
            {
                receivers[i]?.OnClientNetVfx(key, position, rotationZ, dir, duration, value);
            }
        }

        [ClientRpc]
        private void TriggerAnimClientRpc(int triggerHash)
        {
            if (IsServer) return;
            Animator anim = m_Model != null ? m_Model.Animator : GetComponentInChildren<Animator>();
            if (anim != null) anim.SetTrigger(triggerHash);
        }

        private void UpdateServerPose()
        {
            if (m_Brain.Model == null) return;

            m_PoseTimer += Time.deltaTime;
            if (m_PoseTimer < PoseInterval) return;
            m_PoseTimer = 0f;

            float yaw = ReadModelYaw();
            if (Mathf.Abs(Mathf.DeltaAngle(m_ModelYaw.Value, yaw)) < 0.5f) return;
            m_ModelYaw.Value = yaw;
        }

        /// <summary>
        /// Client không chạy AI nên Animator đứng ở Idle. Tự suy tốc độ di chuyển từ delta vị trí
        /// NetworkTransform để bật animation đi, đồng thời bám hướng xoay model host gửi.
        /// </summary>
        private void UpdateClientVisuals()
        {
            if (m_Brain == null || m_Brain.Model == null) return;

            Vector3 position = transform.position;
            float deltaTime = Time.deltaTime;
            if (!m_ClientPoseReady)
            {
                m_LastClientPosition = position;
                m_ClientPoseReady = true;
            }

            if (deltaTime > 0.0001f)
            {
                float measuredSpeed = Vector3.Distance(position, m_LastClientPosition) / deltaTime;
                if (measuredSpeed < 0.05f) measuredSpeed = 0f;

                float blend = 1f - Mathf.Exp(-SpeedSmoothing * deltaTime);
                m_ClientSpeed = Mathf.Lerp(m_ClientSpeed, measuredSpeed, blend);
                m_Brain.Model.SetMoveSpeed(m_ClientSpeed);
            }

            m_LastClientPosition = position;
            ApplyModelYaw(m_ModelYaw.Value, false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogClientAnimationState();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Ghi trạng thái Animator client mỗi khi đổi state để kiểm chứng đồng bộ animation.</summary>
        private void LogClientAnimationState()
        {
            if (Time.time < m_NextClientStateLog) return;
            Animator animator = m_Brain.Model.Animator;
            if (animator == null) return;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash == m_LastClientStateHash) return;

            m_LastClientStateHash = info.shortNameHash;
            m_NextClientStateLog = Time.time + 0.5f;
            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
            string clip = clips.Length > 0 && clips[0].clip != null ? clips[0].clip.name : "?";
            Debug.Log("[NetAnim] client " + name + " state=" + clip + " speed=" + m_ClientSpeed.ToString("F2") +
                      " yaw=" + m_Brain.Model.transform.eulerAngles.z.ToString("F0"));
        }
#endif

        private float ReadModelYaw()
        {
            return m_Brain != null && m_Brain.Model != null
                ? m_Brain.Model.transform.eulerAngles.z
                : transform.eulerAngles.z;
        }

        private void ApplyModelYaw(float yaw, bool instant)
        {
            if (m_Brain == null || m_Brain.Model == null) return;

            Transform model = m_Brain.Model.transform;
            float next = instant
                ? yaw
                : Mathf.MoveTowardsAngle(model.eulerAngles.z, yaw, 720f * Time.deltaTime);
            model.rotation = Quaternion.Euler(0f, 0f, next);
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
