using UnityEngine;

namespace IronWasteland.Enemies.D1onk.VFX
{
    /// <summary>
    /// Tự động hủy VFX tạm thời sau khi ParticleSystem ngừng hoạt động hoặc sau thời gian sống lifetime,
    /// đồng thời hỗ trợ làm mờ dần (fade out) và nở rộng (scale up) cho SpriteRenderer.
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkSimpleAutoCleanup : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float lifetime = 1.5f;
        [SerializeField] private bool fadeOut = true;
        [SerializeField] private bool scaleUp = false;

        private SpriteRenderer m_Sprite;
        private Color m_InitialColor;
        private Vector3 m_InitialScale;
        private float m_Timer = 0f;

        public float Lifetime
        {
            get => lifetime;
            set => lifetime = Mathf.Max(0.05f, value);
        }

        private void Awake()
        {
            m_Sprite = GetComponent<SpriteRenderer>();
            if (m_Sprite != null)
            {
                m_InitialColor = m_Sprite.color;
            }
            m_InitialScale = transform.localScale;
        }

        private void Start()
        {
            var ps = GetComponent<ParticleSystem>();
            if (ps != null && !ps.main.loop)
            {
                lifetime = Mathf.Max(lifetime, ps.main.duration + ps.main.startLifetime.constantMax);
            }

            if (Application.isPlaying)
            {
                Destroy(gameObject, lifetime);
            }
        }

        private void Update()
        {
            ManualTick(Time.deltaTime);
        }

        public void ManualTick(float dt)
        {
            m_Timer += dt;
            float progress = Mathf.Clamp01(m_Timer / lifetime);

            if (m_Sprite != null && fadeOut)
            {
                Color c = m_InitialColor;
                c.a = Mathf.Lerp(m_InitialColor.a, 0f, progress * progress);
                m_Sprite.color = c;
            }

            if (scaleUp)
            {
                transform.localScale = Vector3.Lerp(m_InitialScale, m_InitialScale * 1.5f, progress);
            }
        }
    }
}
