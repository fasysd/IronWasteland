using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.The2Face
{
    /// <summary>
    /// Điểm Sáng (LightPointTotem) của Boss The2Face.
    /// - Có máu riêng, bị quái đầm lầy phá hủy.
    /// - Di chuyển chậm bám theo Tank gần nhất.
    /// - Bật khi có Tank đứng trong bán kính quy định, tắt khi Tank rời đi.
    /// </summary>
    public class The2FaceLightPoint : MonoBehaviour
    {
        private static readonly List<The2FaceLightPoint> s_Active = new List<The2FaceLightPoint>();

        [Tooltip("SpriteRenderer thân Điểm Sáng.")]
        [SerializeField] private SpriteRenderer bodyRenderer;

        [Tooltip("Màu khi Điểm Sáng đang bật.")]
        [SerializeField] private Color litColor = new Color(1f, 0.83f, 0.25f, 1f);

        [Tooltip("Màu khi Điểm Sáng đang tắt.")]
        [SerializeField] private Color unlitColor = new Color(0.34f, 0.38f, 0.5f, 0.9f);

        [Tooltip("Tốc độ nhấp nháy khi đang bật.")]
        [SerializeField, Min(0f)] private float pulseSpeed = 2.2f;

        private The2FaceBrain m_Boss;
        private float m_MaxHealth;
        private float m_CurrentHealth;
        private float m_MoveSpeed;
        private float m_LitRadius;
        private float m_Elapsed;
        private Vector3 m_BaseScale;
        private bool m_Initialized;

        /// <summary>Danh sách toàn bộ Điểm Sáng đang tồn tại trong scene.</summary>
        public static IReadOnlyList<The2FaceLightPoint> ActivePoints => s_Active;

        /// <summary>Điểm Sáng còn sống hay không.</summary>
        public bool IsAlive => m_Initialized && m_CurrentHealth > 0f;

        /// <summary>Điểm Sáng đang được Tank kích hoạt hay không.</summary>
        public bool IsLit { get; private set; }

        /// <summary>Máu hiện tại.</summary>
        public float CurrentHealth => m_CurrentHealth;

        /// <summary>Máu tối đa.</summary>
        public float MaxHealth => m_MaxHealth;

        private void Awake()
        {
            m_BaseScale = transform.localScale;
            if (bodyRenderer != null) bodyRenderer.color = unlitColor;
        }

        private void OnEnable()
        {
            if (!s_Active.Contains(this)) s_Active.Add(this);
        }

        private void OnDisable()
        {
            s_Active.Remove(this);
        }

        private void OnDestroy()
        {
            s_Active.Remove(this);
        }

        /// <summary>Khởi tạo Điểm Sáng do Boss sinh ra.</summary>
        public void Initialize(The2FaceBrain boss, float maxHealth, float moveSpeed, float litRadius)
        {
            m_Boss = boss;
            m_MaxHealth = Mathf.Max(1f, maxHealth);
            m_CurrentHealth = m_MaxHealth;
            m_MoveSpeed = Mathf.Max(0f, moveSpeed);
            m_LitRadius = Mathf.Max(0.1f, litRadius);
            m_Elapsed = 0f;
            m_Initialized = true;
            IsLit = false;

            if (bodyRenderer != null) bodyRenderer.color = unlitColor;
        }

        private void Update()
        {
            if (!m_Initialized || m_CurrentHealth <= 0f) return;

            m_Elapsed += Time.deltaTime;

            TankBrain nearest = FindNearestTank();
            IsLit = nearest != null &&
                    Vector2.Distance(transform.position, nearest.transform.position) <= m_LitRadius;

            UpdateMovement(nearest);
            UpdateVisual();
        }

        private TankBrain FindNearestTank()
        {
            TankBrain[] tanks = FindObjectsByType<TankBrain>();
            TankBrain result = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain tank = tanks[i];
                if (tank == null || !tank.IsAlive) continue;

                float sqr = ((Vector2)tank.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    result = tank;
                }
            }

            return result;
        }

        private void UpdateMovement(TankBrain nearest)
        {
            if (nearest == null || m_MoveSpeed <= 0f) return;

            Vector2 toTank = (Vector2)nearest.transform.position - (Vector2)transform.position;
            float distance = toTank.magnitude;
            if (distance <= m_LitRadius * 0.85f || distance <= 0.001f) return;

            Vector2 next = Vector2.MoveTowards(
                transform.position,
                nearest.transform.position,
                m_MoveSpeed * Time.deltaTime);
            transform.position = next;
        }

        private void UpdateVisual()
        {
            if (bodyRenderer != null)
            {
                Color targetColor = IsLit ? litColor : unlitColor;
                bodyRenderer.color = Color.Lerp(bodyRenderer.color, targetColor, Time.deltaTime * 8f);
            }

            float pulse = IsLit ? 1f + Mathf.Sin(m_Elapsed * pulseSpeed) * 0.06f : 1f;
            transform.localScale = m_BaseScale * pulse;
        }

        /// <summary>Nhận sát thương từ quái hoặc kỹ năng của Boss.</summary>
        public void ApplyDamage(float amount)
        {
            if (!m_Initialized || m_CurrentHealth <= 0f || amount <= 0f) return;

            m_CurrentHealth = Mathf.Max(0f, m_CurrentHealth - amount);

            if (bodyRenderer != null && m_CurrentHealth > 0f)
            {
                bodyRenderer.color = Color.white;
            }

            IronWasteland.CombatFeedback.Show(amount, transform.position, IronWasteland.TextValueType.EnemyDamage);

            if (m_CurrentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            IsLit = false;
            if (m_Boss != null)
            {
                m_Boss.OnLightPointDestroyed(this);
            }

            Destroy(gameObject);
        }
    }
}
