using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.The2Face
{
    /// <summary>
    /// Hiệu ứng VFX Câm lặng (CamLang) bám theo Tank mục tiêu trong thời gian bị khóa kỹ năng thường.
    /// </summary>
    public class The2FaceSilenceVfx : MonoBehaviour
    {
        [Tooltip("Tốc độ xoay nhẹ của ấn ký câm lặng (độ/giây).")]
        [SerializeField] private float rotateSpeed = 90f;

        [Tooltip("Tốc độ nhấp nháy kích thước.")]
        [SerializeField] private float pulseSpeed = 4f;

        [Tooltip("Độ lệch vị trí so với tâm Tank.")]
        [SerializeField] private Vector3 offset = Vector3.zero;

        private Transform m_Target;
        private TankBrain m_TargetTank;
        private float m_Remaining;
        private Vector3 m_BaseScale;
        private float m_Elapsed;

        private void Awake()
        {
            m_BaseScale = transform.localScale;
        }

        public void Initialize(Transform target, float duration)
        {
            m_Target = target;
            m_TargetTank = target != null ? target.GetComponent<TankBrain>() : null;
            m_Remaining = Mathf.Max(0.1f, duration);
            m_Elapsed = 0f;

            if (m_Target != null)
            {
                transform.position = m_Target.position + offset;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            m_Elapsed += dt;
            m_Remaining -= dt;

            if (m_Remaining <= 0f || m_Target == null || (m_TargetTank != null && !m_TargetTank.IsAlive))
            {
                Destroy(gameObject);
                return;
            }

            transform.position = m_Target.position + offset;
            transform.Rotate(0f, 0f, rotateSpeed * dt);

            float pulse = 1f + Mathf.Sin(m_Elapsed * pulseSpeed) * 0.08f;
            transform.localScale = m_BaseScale * pulse;
        }
    }
}
