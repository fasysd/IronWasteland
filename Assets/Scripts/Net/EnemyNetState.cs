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

        private const float SpeedEpsilon = 0.05f;
        private const float RotEpsilon = 1f;
        private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");

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

        private readonly NetworkVariable<float> m_MoveSpeed = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> m_FlipX = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> m_ModelRotationZ = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private EnemyBrain m_Brain;
        private EnemyModel m_Model;
        private Rigidbody2D m_Body;
        private EnemyMovementAgent m_Movement;
        private float m_SyncTimer;
        private float m_AnimSyncTimer;
        private float m_SentHealth;
        private float m_SentMoveSpeed;
        private bool m_SentFlipX;
        private float m_SentModelRotZ;

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
            m_MoveSpeed.OnValueChanged += HandleMoveSpeedChanged;
            m_FlipX.OnValueChanged += HandleFlipXChanged;

            if (IsServer)
            {
                m_Movement?.SetServerSimulated(true);
                m_MaxHealth.Value = m_Brain.MaxHealth;
                m_SentHealth = m_Brain.CurrentHealth;
                m_Health.Value = m_SentHealth;
                m_SentMoveSpeed = ReadAnimatorMoveSpeed();
                m_MoveSpeed.Value = m_SentMoveSpeed;
                m_SentFlipX = ReadSpriteFlipX();
                m_FlipX.Value = m_SentFlipX;
                m_SentModelRotZ = ReadModelRotationZ();
                m_ModelRotationZ.Value = m_SentModelRotZ;
            }
            else
            {
                ApplyProxyMode();

                // Client tự khởi tạo brain một lần (để SetHealth chịu tác dụng) rồi mới áp máu host gửi.
                if (!m_Brain.IsInitialized) m_Brain.Initialize();
                ApplyMaxHealth(m_MaxHealth.Value);
                ApplyHealth(m_Health.Value);
                ApplyMoveSpeed(m_MoveSpeed.Value);
                ApplyFlipX(m_FlipX.Value);
                if (m_Model != null)
                {
                    m_Model.transform.localRotation = Quaternion.Euler(0f, 0f, m_ModelRotationZ.Value);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            m_Health.OnValueChanged -= HandleHealthChanged;
            m_MaxHealth.OnValueChanged -= HandleMaxHealthChanged;
            m_MoveSpeed.OnValueChanged -= HandleMoveSpeedChanged;
            m_FlipX.OnValueChanged -= HandleFlipXChanged;
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (!IsServer)
            {
                if (m_Model != null)
                {
                    Quaternion targetRot = Quaternion.Euler(0f, 0f, m_ModelRotationZ.Value);
                    m_Model.transform.localRotation = Quaternion.Slerp(
                        m_Model.transform.localRotation, targetRot, 1f - Mathf.Exp(-14f * Time.deltaTime));
                }
                return;
            }

            if (m_Brain == null) return;

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

            m_AnimSyncTimer += Time.deltaTime;
            if (m_AnimSyncTimer >= AnimSyncInterval)
            {
                m_AnimSyncTimer = 0f;
                float speed = ReadAnimatorMoveSpeed();
                if (Mathf.Abs(speed - m_SentMoveSpeed) >= SpeedEpsilon)
                {
                    m_SentMoveSpeed = speed;
                    m_MoveSpeed.Value = speed;
                }

                bool flipX = ReadSpriteFlipX();
                if (flipX != m_SentFlipX)
                {
                    m_SentFlipX = flipX;
                    m_FlipX.Value = flipX;
                }

                float rotZ = ReadModelRotationZ();
                if (Mathf.Abs(Mathf.DeltaAngle(m_SentModelRotZ, rotZ)) >= RotEpsilon)
                {
                    m_SentModelRotZ = rotZ;
                    m_ModelRotationZ.Value = rotZ;
                }
            }
        }

        private float ReadModelRotationZ()
        {
            return m_Model != null ? m_Model.transform.localEulerAngles.z : 0f;
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

        private float ReadAnimatorMoveSpeed()
        {
            Animator anim = m_Model != null ? m_Model.Animator : null;
            return anim != null ? anim.GetFloat(HashMoveSpeed) : 0f;
        }

        private bool ReadSpriteFlipX()
        {
            return m_Model != null && m_Model.SpriteRenderer != null && m_Model.SpriteRenderer.flipX;
        }

        private void HandleMoveSpeedChanged(float previousValue, float newValue)
        {
            if (IsServer) return;
            ApplyMoveSpeed(newValue);
        }

        private void ApplyMoveSpeed(float speed)
        {
            m_Model?.SetMoveSpeed(speed);
        }

        private void HandleFlipXChanged(bool previousValue, bool newValue)
        {
            if (IsServer) return;
            ApplyFlipX(newValue);
        }

        private void ApplyFlipX(bool flipX)
        {
            if (m_Model != null && m_Model.SpriteRenderer != null)
            {
                m_Model.SpriteRenderer.flipX = flipX;
            }
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
