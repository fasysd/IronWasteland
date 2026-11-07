using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Life Drain: vòng tròn xanh nhạt hiển thị vùng hồi máu đồng minh, gắn giữa Water B trong suốt thời gian kích hoạt.
    /// Chỉ là HÌNH ẢNH - bán kính được truyền vào từ TankBrain_WaterB (chính là bán kính tìm đồng minh thật).
    ///
    /// - Để trống prefab ở TankBrain_WaterB: script tự tạo vòng viền + nền mờ.
    /// - Nếu prefab có SpriteRenderer: script tự co/giãn sprite phủ đúng đường kính = 2 * bán kính.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterB_LifeDrainRangeVisual : MonoBehaviour
    {
        [Header("Motion")]
        [SerializeField, Min(0.01f)] private float fadeInTime = 0.25f;
        [SerializeField, Min(0.01f)] private float fadeOutTime = 0.3f;
        [SerializeField, Min(0f)] private float pulseAmount = 0.01f;
        [SerializeField, Min(0f)] private float pulseSpeed = 3f;

        [Header("Default Visual (chỉ dùng khi prefab không có Renderer nào)")]
        [SerializeField, Min(0.01f)] private float ringWidth = 0.12f;
        [SerializeField] private Color ringColor = new Color(0.55f, 0.95f, 1f, 0.7f);
        [SerializeField] private Color fillColor = new Color(0.55f, 0.95f, 1f, 0.12f);
        [SerializeField] private int sortingOrder = 1; // thấp để nằm dưới xe tăng

        private TankBrain m_Owner;
        private float m_StartTime;
        private float m_Duration;
        private Vector3 m_BaseScale = Vector3.one;

        public void Initialize(TankBrain owner, float radius, float duration)
        {
            if (owner == null)
            {
                Destroy(gameObject);
                return;
            }

            m_Owner = owner;
            m_Duration = duration > 0f ? duration : 5f;
            m_StartTime = Time.time;

            SpriteRenderer existing = GetComponentInChildren<SpriteRenderer>(true);
            if (GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                BuildDefaultVisual(radius);
            }
            else if (existing != null)
            {
                WaterB_Utils.FitVisualToRadius(transform, existing, radius);
            }
            m_BaseScale = transform.localScale;

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

            float grow = Mathf.Clamp01(elapsed / fadeInTime);
            float shrink = Mathf.Clamp01(remaining / fadeOutTime);
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = m_BaseScale * (Mathf.Min(grow, shrink) * pulse);
        }

        private void Follow()
        {
            Vector3 p = m_Owner.transform.position;
            p.z = 0f;
            transform.position = p;
        }

        private void BuildDefaultVisual(float radius)
        {
            // Nền mờ
            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localScale = Vector3.one * (radius * 2f);
            SpriteRenderer fill = fillGo.AddComponent<SpriteRenderer>();
            fill.sprite = WaterB_Utils.GetDiscSprite();
            fill.color = fillColor;
            fill.sortingOrder = sortingOrder;

            // Viền
            GameObject ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(transform, false);
            LineRenderer ring = ringGo.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            const int count = 64;
            ring.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
            ring.startWidth = ringWidth;
            ring.endWidth = ringWidth;
            ring.material = new Material(Shader.Find("Sprites/Default"));
            ring.startColor = ringColor;
            ring.endColor = ringColor;
            ring.sortingOrder = sortingOrder + 1;
        }
    }
}
