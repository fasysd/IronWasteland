using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Bù phần hình ảnh cho xe proxy (không phải xe của mình, không phải host).
    /// Root Rigidbody2D không xoay nên NetworkTransform chỉ đồng bộ vị trí; component này suy hướng thân
    /// từ delta vị trí và xoay nòng theo góc ngắm mà host gửi xuống.
    /// Gắn cùng GameObject với TankBrain + PlayerNetState.
    /// </summary>
    [RequireComponent(typeof(TankBrain))]
    public sealed class NetProxyVisual : MonoBehaviour
    {
        [Tooltip("Tốc độ tối thiểu (unit/giây) để coi là xe đang chạy và xoay thân theo hướng di chuyển.")]
        [SerializeField, Min(0f)] private float moveThreshold = 0.05f;

        [Tooltip("Khoảng cách tạm (unit) để quy đổi góc ngắm thành một điểm trong world cho TankView.Look.")]
        [SerializeField, Min(0.1f)] private float aimRadius = 10f;

        [Tooltip("Tốc độ nội suy góc nòng khi hiển thị (càng cao càng bám sát, càng thấp càng mượt).")]
        [SerializeField, Min(0f)] private float aimLerpSpeed = 18f;

        private TankBrain m_Brain;
        private PlayerNetState m_NetState;
        private Vector3 m_LastPosition;
        private float m_SmoothedAim;
        private bool m_HasSmoothedAim;

        private void Awake()
        {
            m_Brain = GetComponent<TankBrain>();
            m_NetState = GetComponent<PlayerNetState>();
            m_LastPosition = transform.position;
        }

        private void OnEnable()
        {
            m_LastPosition = transform.position;
        }

        private void Update()
        {
            if (m_Brain == null) return;

            // Chỉ xử lý proxy: xe của mình do input local điều khiển, xe host do brain tự chạy.
            // Chỉ xe do máy này điều khiển mới bỏ qua; trên host, xe của client vẫn phải được
            // animate thân/nòng theo vị trí + AimAngle nhận được.
            if (m_Brain.IsLocallyControlled) return;

            Vector3 position = transform.position;
            Vector3 delta = position - m_LastPosition;
            m_LastPosition = position;

            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector2 direction = new Vector2(delta.x, delta.y) / deltaTime;

            if (m_Brain.View != null)
            {
                if (direction.sqrMagnitude > moveThreshold * moveThreshold)
                {
                    m_Brain.View.Move(direction.normalized);
                }
                else
                {
                    m_Brain.View.Stop();
                }

                if (m_NetState != null)
                {
                    // Host chỉ gửi góc ngắm ~15 Hz khi lệch ≥0,5°, nên nội suy để nòng quay mượt
                    // thay vì giật theo từng gói tin.
                    float targetAim = m_NetState.AimAngle;
                    if (!m_HasSmoothedAim)
                    {
                        m_SmoothedAim = targetAim;
                        m_HasSmoothedAim = true;
                    }
                    else
                    {
                        m_SmoothedAim = Mathf.LerpAngle(
                            m_SmoothedAim, targetAim, 1f - Mathf.Exp(-aimLerpSpeed * deltaTime));
                    }

                    float radians = m_SmoothedAim * Mathf.Deg2Rad;
                    Vector2 aimPoint = (Vector2)position + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * aimRadius;
                    m_Brain.View.Look(aimPoint);
                }
            }
        }
    }
}
