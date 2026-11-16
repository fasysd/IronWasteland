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

        [Tooltip("Khoảng đệm (đơn vị thế giới) cộng thêm vào quãng đường cho phép mỗi lần kiểm tra.")]
        [SerializeField, Min(0f)] private float positionSlack = 0.15f;

        private TankBrain m_Brain;
        private Rigidbody2D m_Body;
        private GameController m_GameController;

        private Vector3 m_LastAcceptedPosition;
        private float m_LastAcceptTime;

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
        /// Vượt ngưỡng thì kéo xe về vị trí hợp lệ gần nhất thay vì tin client.
        /// </summary>
        private void ValidateOwnerMovement()
        {
            float elapsed = Mathf.Max(Time.fixedDeltaTime, Time.time - m_LastAcceptTime);
            float maxSpeed = m_Brain != null ? Mathf.Max(0.01f, m_Brain.Stats.MoveSpeed / 100f) : 1f;
            float allowed = maxSpeed * elapsed * speedTolerance + positionSlack;

            Vector3 delta = transform.position - m_LastAcceptedPosition;
            if (delta.sqrMagnitude > allowed * allowed)
            {
                transform.position = m_LastAcceptedPosition;
                if (m_Body != null) m_Body.position = m_LastAcceptedPosition;
                return;
            }

            m_LastAcceptedPosition = transform.position;
            m_LastAcceptTime = Time.time;
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
