using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>Lý do host từ chối một yêu cầu thi triển kỹ năng; gửi riêng cho chủ xe để HUD hiện thông báo.</summary>
    public enum CastResult : byte
    {
        Ok = 0,
        NoEnergy = 1,
        OnCooldown = 2,
        CrowdControlled = 3,
        RateLimited = 4,
        Invalid = 5
    }

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

        /// <summary>Khoảng tối thiểu giữa 2 lệnh teleport gửi xuống chủ xe (tránh spam RPC khi kỹ năng bám theo đồng minh).</summary>
        private const float MinTeleportRpcInterval = 0.05f;

        private TankBrain m_Brain;
        private Rigidbody2D m_Body;
        private GameController m_GameController;

        private Vector3 m_LastAcceptedPosition;
        private float m_LastAcceptTime;
        private float m_MoveBudget;
        private float m_LastCastRequestTime = -1f;
        private float m_LastServerCastTime = -1f;

        /// <summary>Thông báo lý do cast bị từ chối, chỉ hiển thị trên HUD debug của chủ xe.</summary>
        public static string LocalCastMessage { get; private set; }

        /// <summary>Mốc thời gian hết hạn thông báo cast (Time.time).</summary>
        public static float LocalCastMessageUntil { get; private set; }

        /// <summary>Trong khoảng này bỏ qua kiểm tra tốc độ vì vừa có lệnh dịch chuyển hợp lệ từ kỹ năng.</summary>
        private float m_TeleportGraceUntil;
        private float m_ClientTeleportGraceUntil;
        private float m_LastTeleportSendTime = -1f;
        private Vector3 m_LastTeleportSentPosition;

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
            // Vừa có teleport hợp lệ từ kỹ năng: giữ mốc là vị trí teleport, chỉ dời mốc thời gian.
            // (Không lấy transform hiện tại làm mốc vì vị trí đó đang trễ so với client do độ trễ mạng,
            //  lấy nó sẽ khiến client bị kéo lùi một chút ngay sau khi teleport.)
            if (Time.time < m_TeleportGraceUntil)
            {
                // Ghim bản sao trên host vào đúng vị trí teleport: gói vị trí cũ của chủ xe còn bay
                // trên đường sẽ bị ghi đè trước khi kịp phát lại cho người khác.
                transform.position = m_LastAcceptedPosition;
                if (m_Body != null) m_Body.position = m_LastAcceptedPosition;
                m_LastAcceptTime = Time.time;
                return;
            }

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

            // Bỏ qua hiệu chỉnh bắn tới ngay sau khi vừa teleport (gói cũ còn bay trên đường),
            // tránh cảnh xe nhảy tới rồi bị kéo lùi một chút.
            if (Time.time < m_ClientTeleportGraceUntil) return;

            transform.position = position;
            if (m_Body != null) m_Body.position = position;
        }

        /// <summary>
        /// Server gọi khi kỹ năng dịch chuyển tức thời chiếc xe này (Shadow Step, Shadow Ambush,
        /// Shadow Possession, Shadow Exchange, Nova Rush…). Cập nhật vị trí chuẩn, mở cửa sổ
        /// miễn kiểm tra tốc độ để không bị kéo về, rồi lệnh cho chủ xe áp cùng vị trí.
        /// </summary>
        public void ServerTeleport(Vector3 position)
        {
            if (!IsServer || !IsSpawned || m_Brain == null)
                return;

            Vector3 fixedPosition = new Vector3(position.x, position.y, transform.position.z);

            transform.position = fixedPosition;
            if (m_Body != null)
            {
                m_Body.position = fixedPosition;
                m_Body.linearVelocity = Vector2.zero;
            }

            float maxSpeed = Mathf.Max(0.01f, m_Brain.Stats.MoveSpeed / 100f);

            m_LastAcceptedPosition = fixedPosition;
            m_LastAcceptTime = Time.time;
            // Cấp sẵn ngân sách để sau cửa sổ miễn kiểm tra, phần client đã chạy thêm trong lúc
            // gói tin còn trên đường không bị coi là vượt tốc độ (tránh giật lùi sau teleport).
            m_MoveBudget = maxSpeed * 0.6f;
            m_TeleportGraceUntil = Time.time + 0.45f;

            // Host chính là chủ xe thì không cần gửi RPC.
            if (OwnerClientId == NetworkManager.ServerClientId)
                return;

            // Kỹ năng bám theo đồng minh (Shadow Possession) gọi mỗi frame — gộp bớt RPC,
            // nhưng teleport xa thì luôn gửi ngay.
            bool withinInterval = Time.time - m_LastTeleportSendTime < MinTeleportRpcInterval;
            bool smallStep = (fixedPosition - m_LastTeleportSentPosition).sqrMagnitude < 0.75f * 0.75f;
            if (withinInterval && smallStep)
                return;

            m_LastTeleportSendTime = Time.time;
            m_LastTeleportSentPosition = fixedPosition;

            ClientRpcParams target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            };
            TeleportOwnerClientRpc(fixedPosition, target);
        }

        [ClientRpc]
        private void TeleportOwnerClientRpc(Vector3 position, ClientRpcParams rpcParams = default)
        {
            if (IsServer) return; // host đã áp trực tiếp

            Vector3 fixedPosition = new Vector3(position.x, position.y, transform.position.z);
            transform.position = fixedPosition;
            if (m_Body != null)
            {
                m_Body.position = fixedPosition;
                m_Body.linearVelocity = Vector2.zero;
            }

            // Bỏ qua các hiệu chỉnh vị trí bắn tới ngay sau lệnh teleport này.
            m_ClientTeleportGraceUntil = Time.time + 0.35f;
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

            IronWasteland.Core.CameraFollow2D cameraFollow = FindAnyObjectByType<IronWasteland.Core.CameraFollow2D>();
            if (cameraFollow != null) cameraFollow.Target = transform;
        }

        /// <summary>
        /// Client xin host thi triển kỹ năng. Chỉ chủ xe gửi được; host kiểm tra lại năng lượng,
        /// hồi chiêu và trạng thái khống chế trên chính xe của mình trước khi bắn.
        /// </summary>
        public bool RequestCast(int skillIndex, Vector2 aimWorld)
        {
            if (!IsSpawned || !IsOwner || IsServer) return false;
            if (m_Brain == null || !m_Brain.IsAlive)
            {
                SetLocalCastMessage(CastResult.Invalid);
                return false;
            }

            if (Time.time - m_LastCastRequestTime < MinCastRequestInterval)
            {
                SetLocalCastMessage(CastResult.RateLimited);
                return false;
            }

            m_LastCastRequestTime = Time.time;
            RequestCastServerRpc(skillIndex, aimWorld);
            return true;
        }

        [ServerRpc]
        private void RequestCastServerRpc(int skillIndex, Vector2 aimWorld, ServerRpcParams rpcParams = default)
        {
            if (!IsServer || m_Brain == null) return;
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

            if (!m_Brain.IsAlive || skillIndex < 0 || skillIndex >= TankBrain.SkillCount)
            {
                RejectCast(CastResult.Invalid, skillIndex);
                return;
            }

            if (Time.time - m_LastServerCastTime < MinCastRequestInterval)
            {
                RejectCast(CastResult.RateLimited, skillIndex);
                return;
            }

            if (!m_Brain.CanUseSkill)
            {
                RejectCast(CastResult.CrowdControlled, skillIndex);
                return;
            }

            if (!m_Brain.HasEnergyForSkill(skillIndex))
            {
                RejectCast(CastResult.NoEnergy, skillIndex);
                return;
            }

            if (m_Brain.GetSkillCooldown(skillIndex) > 0f)
            {
                RejectCast(CastResult.OnCooldown, skillIndex);
                return;
            }

            m_LastServerCastTime = Time.time;
            m_Brain.SetLookTarget(aimWorld);
            m_Brain.UseSkill(skillIndex);
        }

        /// <summary>Gửi lý do từ chối riêng cho chủ xe; chỉ chủ xe hiện thông báo trên HUD.</summary>
        private void RejectCast(CastResult result, int skillIndex)
        {
            ClientRpcParams target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            };
            CastRejectedClientRpc((byte)result, skillIndex, target);
        }

        [ClientRpc]
        private void CastRejectedClientRpc(byte result, int skillIndex, ClientRpcParams rpcParams = default)
        {
            if (IsServer) return; // host hiển thị trực tiếp khi gọi UseSkill, không cần vòng RPC
            SetLocalCastMessage((CastResult)result);
        }

        private static void SetLocalCastMessage(CastResult result)
        {
            LocalCastMessage = DescribeCastResult(result);
            LocalCastMessageUntil = Time.time + 2.5f;
        }

        /// <summary>Mô tả lý do để HUD hiện; giữ ngắn gọn, chỉ là thông báo debug.</summary>
        private static string DescribeCastResult(CastResult result)
        {
            switch (result)
            {
                case CastResult.NoEnergy:
                    return "không đủ năng lượng";
                case CastResult.OnCooldown:
                    return "kỹ năng đang hồi";
                case CastResult.CrowdControlled:
                    return "đang bị khống chế";
                case CastResult.RateLimited:
                    return "bấm quá nhanh";
                default:
                    return "không hợp lệ";
            }
        }
    }
}
