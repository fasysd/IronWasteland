using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    /// <summary>
    /// Mark Hunter (1A): khi hạ gục kẻ địch, 1 luồng sáng xanh lục chảy cong từ chỗ kẻ địch chết về phía Green Fox.
    /// Gắn script này lên prefab (hoặc để trống ô prefab ở TankBrain_GreenFox, script sẽ tự tạo vệt sáng mặc định).
    /// Nếu prefab có sẵn TrailRenderer thì dùng cái đó; không có thì tự thêm.
    /// </summary>
    [DisallowMultipleComponent]
    public class GreenFox_KillStreamEffect : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float travelTime = 0.7f;
        [SerializeField, Min(0f)] private float arcHeight = 2.5f;      // Độ cong của luồng
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Default Trail (chỉ dùng khi prefab chưa có TrailRenderer)")]
        [SerializeField] private Color startColor = new Color(0.25f, 1f, 0.45f, 1f);
        [SerializeField] private Color endColor = new Color(0.05f, 0.7f, 0.3f, 0f);
        [SerializeField, Min(0.01f)] private float trailWidth = 0.35f;
        [SerializeField, Min(0.05f)] private float trailTime = 0.45f;

        private Transform m_Target;
        private Vector3 m_Start;
        private Vector3 m_ArcOffset;
        private float m_Progress;
        private bool m_Arrived;
        private TrailRenderer m_Trail;

        public void Initialize(Vector3 from, Transform target)
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            m_Target = target;
            m_Start = from;
            m_Start.z = 0f;
            transform.position = m_Start;

            // Độ cong vuông góc với hướng bay, mỗi luồng cong 1 kiểu cho tự nhiên
            Vector2 dir = ((Vector2)target.position - (Vector2)m_Start);
            Vector2 perp = dir.sqrMagnitude > 0.0001f ? Vector2.Perpendicular(dir.normalized) : Vector2.up;
            float side = Random.value < 0.5f ? -1f : 1f;
            m_ArcOffset = perp * (arcHeight * side * Random.Range(0.6f, 1.2f));

            EnsureTrail();
            m_Trail.Clear();
            m_Trail.emitting = true;
        }

        private void EnsureTrail()
        {
            m_Trail = GetComponent<TrailRenderer>();
            if (m_Trail != null) return;

            m_Trail = gameObject.AddComponent<TrailRenderer>();
            m_Trail.time = trailTime;
            m_Trail.startWidth = trailWidth;
            m_Trail.endWidth = 0f;
            m_Trail.minVertexDistance = 0.05f;
            m_Trail.numCapVertices = 4;
            m_Trail.material = new Material(Shader.Find("Sprites/Default"));
            m_Trail.startColor = startColor;
            m_Trail.endColor = endColor;
        }

        private void Update()
        {
            if (m_Arrived) return;

            if (m_Target == null)
            {
                Finish();
                return;
            }

            m_Progress += Time.deltaTime / travelTime;
            float e = ease.Evaluate(Mathf.Clamp01(m_Progress));

            // Bezier bậc 2: đích bám theo Green Fox (kể cả khi đang di chuyển)
            Vector3 end = m_Target.position;
            end.z = 0f;
            Vector3 control = (m_Start + end) * 0.5f + m_ArcOffset;

            float inv = 1f - e;
            transform.position = inv * inv * m_Start + 2f * inv * e * control + e * e * end;

            if (m_Progress >= 1f) Finish();
        }

        private void Finish()
        {
            m_Arrived = true;
            if (m_Trail != null) m_Trail.emitting = false;

            // Chờ vệt sáng tan hết rồi mới xóa
            Destroy(gameObject, m_Trail != null ? m_Trail.time : 0.1f);
        }
    }
}
