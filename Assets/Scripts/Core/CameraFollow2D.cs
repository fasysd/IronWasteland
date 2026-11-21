using UnityEngine;
using IronWasteland.Tanks;

namespace IronWasteland.Core
{
    /// <summary>
    /// Camera bám theo tank nguoi choi (2D top-down).
    ///
    /// Vi sao can: camera mac dinh dung yen o (0,0), tank chay ra khoi man hinh
    /// la mat dau. Component nay chi lam 1 viec: moi LateUpdate keo camera ve phia
    /// tank voi do tre nhe (smoothTime) de khong bi giat.
    ///
    /// Cach dung: gan len Main Camera, khong can gan Target neu scene co GameController
    /// (component se tu lay GameController.PlayerTank).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class CameraFollow2D : MonoBehaviour
    {
        [Header("Muc tieu")]
        [Tooltip("De trong = tu tim tank cua GameController trong scene.")]
        [SerializeField] private Transform target;

        [Tooltip("Lech camera so voi tank (vd (0,0) = dung giua tank).")]
        [SerializeField] private Vector2 offset = Vector2.zero;

        [Header("Do muot")]
        [Tooltip("Thoi gian camera duoi kip tank. 0 = bam cung, 0.15 = mem.")]
        [SerializeField, Min(0f)] private float smoothTime = 0.15f;

        [Tooltip("Chup ngay vao tank o frame dau (tranh camera bay tu (0,0) toi tank).")]
        [SerializeField] private bool snapOnStart = true;

        [Header("Gioi han (tuy chon)")]
        [Tooltip("Chan camera khong ra khoi vung map.")]
        [SerializeField] private bool useBounds = false;

        [SerializeField] private Bounds bounds = new Bounds(Vector3.zero, new Vector3(60f, 40f, 0f));

        private Camera m_Camera;
        private Vector3 m_Velocity;
        private GameController m_GameController;

        /// <summary>Bat gioi han camera theo mot vung (StageBounds goi luc khoi tao).</summary>
        public void SetBounds(Bounds area)
        {
            bounds = area;
            useBounds = true;
        }

        /// <summary>Tat gioi han (vd man nao muon camera bay tu do).</summary>
        public void ClearBounds()
        {
            useBounds = false;
        }

        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                if (snapOnStart && target != null) transform.position = DesiredPosition();
            }
        }

        private void Awake()
        {
            m_Camera = GetComponent<Camera>();
        }

        private void Start()
        {
            if (target == null) ResolveTarget();
            if (snapOnStart && target != null)
            {
                m_Velocity = Vector3.zero;
                transform.position = DesiredPosition();
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                ResolveTarget();
                if (target == null) return;
            }

            Vector3 desired = DesiredPosition();
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref m_Velocity, smoothTime);
        }

        private Vector3 DesiredPosition()
        {
            Vector3 point = target.position + (Vector3)offset;
            point.z = transform.position.z;

            if (!useBounds) return point;

            // Clamp theo kich thuoc camera de mep man hinh khong vuot qua bien map.
            float halfHeight = m_Camera != null ? m_Camera.orthographicSize : 5f;
            float halfWidth = halfHeight * (m_Camera != null ? m_Camera.aspect : 1.77f);

            float minX = bounds.min.x + halfWidth;
            float maxX = bounds.max.x - halfWidth;
            float minY = bounds.min.y + halfHeight;
            float maxY = bounds.max.y - halfHeight;

            point.x = minX <= maxX ? Mathf.Clamp(point.x, minX, maxX) : bounds.center.x;
            point.y = minY <= maxY ? Mathf.Clamp(point.y, minY, maxY) : bounds.center.y;
            return point;
        }

        private void ResolveTarget()
        {
            if (TankBrain.NetworkSessionActive && Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.LocalClient?.PlayerObject != null)
            {
                target = Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject.transform;
                return;
            }

            if (m_GameController == null) m_GameController = FindAnyObjectByType<GameController>();
            if (m_GameController != null && m_GameController.PlayerTank != null)
            {
                target = m_GameController.PlayerTank.transform;
                return;
            }

            TankBrain found = FindAnyObjectByType<TankBrain>();
            if (found != null) target = found.transform;
        }

        private void OnDrawGizmosSelected()
        {
            if (!useBounds) return;
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.6f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
