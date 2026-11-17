using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Nối xe mạng với điều khiển cục bộ: chỉ chủ sở hữu nhận input và camera bám theo;
    /// các máy khác coi xe là proxy kinematic (vị trí do NetworkTransform đồng bộ).
    /// Server kiểm tra tốc độ di chuyển để chặn teleport từ client.
    /// </summary>
    [RequireComponent(typeof(TankBrain))]
    public class NetPlayerController : NetworkBehaviour
    {
        [Tooltip("Hệ số nới cho phép so với tốc độ tối đa; 1.6 đủ rộng cho sai lệch một tick mạng.")]
        [SerializeField, Min(1f)] private float speedTolerance = 1.6f;

        private const float MinCastRequestInterval = 0.08f;

        private TankBrain m_Brain;
        private Rigidbody2D m_Body;
        private GameController m_GameController;

        private Vector3 m_LastAcceptedPosition;
        private float m_LastAcceptTime;
        private float m_MoveBudget;
        private float m_LastCastRequestTime = -1f;

        public override void OnNetworkSpawn()
        {
            m_Brain = GetComponent<TankBrain>();
            m_Body = GetComponent<Rigidbody2D>();

            m_LastAcceptedPosition = transform.position;
            m_LastAcceptTime = Time.time;

            if (!IsOwner)
            {
                ApplyProxyBody();
                return;
            }

            BindLocalInput();
            BindCamera();
        }

        /// <summary>Proxy: tắt vật lý động để NetworkTransform điều khiển vị trí, collider vẫn còn.</summary>
        private void ApplyProxyBody()
        {
            if (m_Body == null) return;

            m_Body.linearVelocity = Vector2.zero;
            m_Body.angularVelocity = 0f;
            m_Body.bodyType = RigidbodyType2D.Kinematic;
        }

        private void FixedUpdate()
        {
            if (!IsServer || !IsSpawned) return;

            ValidateOwnerMovement();
        }

        /// <summary>
        /// Server so quãng đường thực tế với tốc độ tối đa của xe trong khoảng thời gian trôi qua.
        /// Ngân sách dư tích luỹ có trần (không cộng dồn vô hạn), nhờ vậy client không thể teleport
        /// bằng cách lợi dụng nhiều tick liên tiếp. Vượt ngưỡng thì kéo về vị trí hợp lệ gần nhất.
        /// </summary>
        private void ValidateOwnerMovement()
        {
            float elapsed = Mathf.Clamp(Time.time - m_LastAcceptTime, Time.fixedDeltaTime, 0.25f);
            float maxSpeed = m_Brain != null ? Mathf.Max(0.01f, m_Brain.Stats.MoveSpeed / 100f) : 1f;

            m_MoveBudget = Mathf.Min(
                m_MoveBudget + maxSpeed * elapsed * (speedTolerance - 1f),
                maxSpeed * 0.6f);

            float allowed = maxSpeed * elapsed + m_MoveBudget;
            Vector3 delta = transform.position - m_LastAcceptedPosition;

            // Xe đang bị Stun/Root thì mọi dịch chuyển đều không hợp lệ.
            bool blocked = m_Brain != null && !m_Brain.CanMove;
            if (blocked || delta.sqrMagnitude > allowed * allowed)
            {
                CorrectOwnerPosition(m_LastAcceptedPosition);
                m_MoveBudget = 0f;
                return;
            }

            m_MoveBudget = Mathf.Max(0f, allowed - delta.magnitude);
            m_LastAcceptedPosition = transform.position;
            m_LastAcceptTime = Time.time;
        }

        /// <summary>
        /// Kéo xe về vị trí host chấp nhận. NetworkTransform owner-authority sẽ ghi đè transform
        /// ở tick sau, nên phải gửi ClientRpc riêng cho chủ xe để nó tự kéo về.
        /// </summary>
        private void CorrectOwnerPosition(Vector3 position)
        {
            transform.position = position;
            if (m_Body != null) m_Body.position = position;

            ClientRpcParams target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            };
            CorrectPositionClientRpc(position, target);
        }

        [ClientRpc]
        private void CorrectPositionClientRpc(Vector3 position, ClientRpcParams rpcParams = default)
        {
            if (IsServer) return; // host đã áp trực tiếp
            transform.position = position;
            if (m_Body != null) m_Body.position = position;
        }

        private void BindLocalInput()
        {
            if (m_Brain == null || m_GameController != null) return;

            m_GameController = FindAnyObjectByType<GameController>();
            if (m_GameController == null) return;

            m_GameController.BindTank(m_Brain);
        }

        private void BindCamera()
        {
            NetCameraFollow follow = FindAnyObjectByType<NetCameraFollow>();
            if (follow != null) follow.SetTarget(transform);
        }

        /// <summary>
        /// Client xin host thi triển kỹ năng. Chỉ chủ xe gửi được; host kiểm tra lại năng lượng,
        /// hồi chiêu và trạng thái khống chế trên chính xe của mình trước khi bắn.
        /// </summary>
        public bool RequestCast(int skillIndex, Vector2 aimWorld)
        {
            if (!IsSpawned || !IsOwner || IsServer) return false;
            if (m_Brain == null || !m_Brain.IsAlive) return false;
            if (Time.time - m_LastCastRequestTime < MinCastRequestInterval) return false;

            m_LastCastRequestTime = Time.time;
            RequestCastServerRpc(skillIndex, aimWorld);
            return true;
        }

        [ServerRpc]
        private void RequestCastServerRpc(int skillIndex, Vector2 aimWorld, ServerRpcParams rpcParams = default)
        {
            if (!IsServer || m_Brain == null) return;
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (!m_Brain.IsAlive) return;

            m_Brain.SetLookTarget(aimWorld);
            m_Brain.UseSkill(skillIndex);
        }
    }
}
