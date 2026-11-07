using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Aqua Assault (2C): hình ảnh GẮN VÀO GIỮA Water B trong suốt thời gian kích hoạt rồi tự biến mất.
    /// Chỉ là hình ảnh, không có chức năng (buff chỉ số do TankBrain_WaterB xử lý).
    ///
    /// - Gắn script này lên prefab của bạn (sprite, particle...). Script lo: bám theo Water B, xoay, nhấp nháy,
    ///   phóng to khi bật và thu nhỏ khi hết.
    /// - Để trống ô prefab ở TankBrain_WaterB: script tự tạo hiệu ứng mặc định (vòng nước xoay + các giọt nước bay quanh).
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterB_AquaAssaultVisual : MonoBehaviour
    {
        [Header("Motion")]
        [SerializeField] private float spinSpeed = 90f;                 // độ/giây (0 = không xoay)
        [SerializeField, Min(0f)] private float pulseAmount = 0.06f;    // nhấp nháy to/nhỏ
        [SerializeField, Min(0f)] private float pulseSpeed = 5f;
        [SerializeField, Min(0.01f)] private float fadeInTime = 0.25f;  // phóng to khi bật
        [SerializeField, Min(0.01f)] private float fadeOutTime = 0.3f;  // thu nhỏ khi sắp hết

        [Header("Default Visual (chỉ dùng khi prefab không có Renderer nào)")]
        [SerializeField, Min(0.1f)] private float radius = 1.4f;
        [SerializeField, Min(0.01f)] private float ringWidth = 0.12f;
        [SerializeField, Min(0)] private int dropletCount = 3;
        [SerializeField] private Color ringColor = new Color(0.35f, 0.9f, 1f, 0.7f);
        [SerializeField] private Color dropletColor = new Color(0.6f, 1f, 1f, 1f);
        [SerializeField, Min(0.01f)] private float dropletWidth = 0.25f;
        [SerializeField, Min(0.05f)] private float dropletTrailTime = 0.35f;

        private TankBrain m_Owner;
        private float m_StartTime;
        private float m_Duration;
        private Vector3 m_BaseScale = Vector3.one;

        public void Initialize(TankBrain owner, float duration)
        {
            if (owner == null)
            {
                Destroy(gameObject);
                return;
            }

            m_Owner = owner;
            m_Duration = duration > 0f ? duration : 7f;
            m_StartTime = Time.time;
            m_BaseScale = transform.localScale;

            if (GetComponentsInChildren<Renderer>(true).Length == 0) BuildDefaultVisual();

            Follow();
            transform.localScale = Vector3.zero; // phóng to dần ở Update
        }

        private void Update()
        {
            float elapsed = Time.time - m_StartTime;
            float remaining = m_Duration - elapsed;

            if (m_Owner == null || remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            Follow();

            if (spinSpeed != 0f) transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            float grow = Mathf.Clamp01(elapsed / fadeInTime);
            float shrink = Mathf.Clamp01(remaining / fadeOutTime);
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = m_BaseScale * (Mathf.Min(grow, shrink) * pulse);
        }

        /// <summary>Đặt đúng giữa Water B.</summary>
        private void Follow()
        {
            Vector3 p = m_Owner.transform.position;
            p.z = 0f;
            transform.position = p;
        }

        private void BuildDefaultVisual()
        {
            Material mat = new Material(Shader.Find("Sprites/Default"));

            // Vòng nước
            GameObject ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(transform, false);
            LineRenderer ring = ringGo.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 48;
            for (int i = 0; i < 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
            ring.startWidth = ringWidth;
            ring.endWidth = ringWidth;
            ring.material = mat;
            ring.startColor = ringColor;
            ring.endColor = ringColor;
            ring.sortingOrder = 4;

            // Các giọt nước bay quanh (xoay theo cả object)
            for (int i = 0; i < dropletCount; i++)
            {
                float a = (dropletCount > 0 ? i / (float)dropletCount : 0f) * Mathf.PI * 2f;

                GameObject d = new GameObject("Droplet");
                d.transform.SetParent(transform, false);
                d.transform.localPosition = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);

                TrailRenderer tr = d.AddComponent<TrailRenderer>();
                tr.time = dropletTrailTime;
                tr.startWidth = dropletWidth;
                tr.endWidth = 0f;
                tr.minVertexDistance = 0.05f;
                tr.numCapVertices = 4;
                tr.material = mat;
                tr.startColor = dropletColor;
                tr.endColor = new Color(dropletColor.r, dropletColor.g, dropletColor.b, 0f);
                tr.sortingOrder = 5;
            }
        }
    }
}
