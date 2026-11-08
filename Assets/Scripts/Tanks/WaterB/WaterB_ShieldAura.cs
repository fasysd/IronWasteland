using UnityEngine;
using IronWasteland.Core.Effects;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Khiên nhỏ màu xanh bao quanh xe tăng đang có khiên (hiện dùng cho khiên của Mobile Altar).
    /// Chỉ là HÌNH ẢNH: bám theo xe, tự theo dõi khiên có Id chỉ định, và tự biến mất khi khiên hết hạn hoặc bị phá.
    ///
    /// - Để trống prefab ở WaterB_MobileAltar: script tự tạo vòng viền + nền mờ xanh nhạt.
    /// - Nếu prefab có SpriteRenderer: script tự co/giãn sprite phủ đúng đường kính hào quang.
    /// - Bán kính tự lấy theo Collider2D của xe (nhân hệ số); không có collider thì dùng bán kính dự phòng.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterB_ShieldAura : MonoBehaviour
    {
        [Header("Size")]
        [SerializeField] private bool autoFitToTank = true;
        [SerializeField, Min(0.1f)] private float fallbackRadius = 0.9f;
        [SerializeField, Min(1f)] private float tankRadiusMultiplier = 3f;

        [Header("Motion")]
        [SerializeField, Min(0.01f)] private float fadeInTime = 0.2f;
        [SerializeField, Min(0.01f)] private float fadeOutTime = 0.35f;
        [SerializeField, Min(0f)] private float pulseAmount = 0.04f;
        [SerializeField, Min(0f)] private float pulseSpeed = 4f;

        [Header("Default Visual (chỉ dùng khi prefab không có Renderer nào)")]
        [SerializeField, Min(0.01f)] private float ringWidth = 0.07f;
        [SerializeField] private Color ringColor = new Color(0.35f, 0.75f, 1f, 0.9f);
        [SerializeField] private Color fillColor = new Color(0.35f, 0.75f, 1f, 0.28f);
        [SerializeField] private int sortingOrder = 6; // trên xe tăng một chút

        private TankBrain m_Tank;
        private string m_ShieldId;
        private Vector3 m_BaseScale = Vector3.one;
        private float m_Appear;      // 0..1 mức hiện
        private bool m_Fading;       // khiên đã mất -> đang mờ dần

        private SpriteRenderer[] m_Sprites;
        private LineRenderer[] m_Lines;
        private Color[] m_SpriteBaseColors;
        private Color[] m_LineBaseColors;

        public void Initialize(TankBrain tank, string shieldId)
        {
            if (tank == null || string.IsNullOrEmpty(shieldId))
            {
                Destroy(gameObject);
                return;
            }

            m_Tank = tank;
            m_ShieldId = shieldId;

            float radius = ResolveRadius(tank);

            if (GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                BuildDefaultVisual(radius);
            }
            else
            {
                SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>(true);
                if (sr != null) WaterB_Utils.FitVisualToRadius(transform, sr, radius);
            }
            m_BaseScale = transform.localScale;

            CacheRenderers();
            Follow();
            m_Appear = 0f;
            Apply();
        }

        /// <summary>Khiên được đặt lại (Mobile Altar tạo lại khiên mỗi nhịp): hủy trạng thái mờ dần.</summary>
        public void Refresh()
        {
            m_Fading = false;
        }

        private void Update()
        {
            if (m_Tank == null || !m_Tank.IsAlive)
            {
                Destroy(gameObject);
                return;
            }

            ShieldEffect shield = m_Tank.GetEffect(m_ShieldId) as ShieldEffect;
            bool hasShield = shield != null && !shield.IsExpired && shield.RemainingShield > 0f;
            if (!hasShield) m_Fading = true;

            if (m_Fading)
            {
                m_Appear -= Time.deltaTime / fadeOutTime;
                if (m_Appear <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            else
            {
                m_Appear = Mathf.Min(1f, m_Appear + Time.deltaTime / fadeInTime);
            }

            Follow();
            Apply();
        }

        private void Follow()
        {
            Vector3 p = m_Tank.transform.position;
            p.z = 0f;
            transform.position = p;
        }

        private void Apply()
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = m_BaseScale * pulse;

            for (int i = 0; i < m_Sprites.Length; i++)
            {
                if (m_Sprites[i] == null) continue;
                Color c = m_SpriteBaseColors[i];
                c.a *= m_Appear;
                m_Sprites[i].color = c;
            }
            for (int i = 0; i < m_Lines.Length; i++)
            {
                if (m_Lines[i] == null) continue;
                Color c = m_LineBaseColors[i];
                c.a *= m_Appear;
                m_Lines[i].startColor = c;
                m_Lines[i].endColor = c;
            }
        }

        private void CacheRenderers()
        {
            m_Sprites = GetComponentsInChildren<SpriteRenderer>(true);
            m_Lines = GetComponentsInChildren<LineRenderer>(true);

            m_SpriteBaseColors = new Color[m_Sprites.Length];
            for (int i = 0; i < m_Sprites.Length; i++) m_SpriteBaseColors[i] = m_Sprites[i].color;

            m_LineBaseColors = new Color[m_Lines.Length];
            for (int i = 0; i < m_Lines.Length; i++) m_LineBaseColors[i] = m_Lines[i].startColor;
        }

        private float ResolveRadius(TankBrain tank)
        {
            if (!autoFitToTank) return fallbackRadius;

            Collider2D col = tank.GetComponentInChildren<Collider2D>();
            if (col == null) return fallbackRadius;

            Vector3 e = col.bounds.extents;
            float r = Mathf.Max(e.x, e.y) * tankRadiusMultiplier;
            return r > 0.1f ? r : fallbackRadius;
        }

        private void BuildDefaultVisual(float radius)
        {
            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localScale = Vector3.one * (radius * 2f);
            SpriteRenderer fill = fillGo.AddComponent<SpriteRenderer>();
            fill.sprite = WaterB_Utils.GetDiscSprite();
            fill.color = fillColor;
            fill.sortingOrder = sortingOrder;

            GameObject ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(transform, false);
            LineRenderer ring = ringGo.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            const int count = 48;
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