using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.The2Face
{
    /// <summary>
    /// Vùng xoáy năng lượng do TuNhiet của The2Face tạo ra.
    /// Gây sát thương theo thời gian lên toàn bộ Tank đứng trong vùng.
    /// </summary>
    public class The2FaceVortexZone : MonoBehaviour
    {
        [Tooltip("SpriteRenderer của vùng xoáy.")]
        [SerializeField] private SpriteRenderer zoneRenderer;

        [Tooltip("Layer của Tank để quét sát thương.")]
        [SerializeField] private LayerMask tankLayer;

        [Tooltip("Chu kỳ gây sát thương (giây).")]
        [SerializeField, Min(0.1f)] private float tickInterval = 0.5f;

        [Tooltip("Tốc độ xoay hình ảnh (độ/giây).")]
        [SerializeField] private float rotateSpeed = 90f;

        private EnemyBrain m_Owner;
        private float m_Radius;
        private float m_RemainingTime;
        private float m_DamagePerSecond;
        private float m_TickTimer;
        private Vector3 m_BaseScale;

        /// <summary>Khởi tạo vùng xoáy.</summary>
        public void Initialize(EnemyBrain owner, float radius, float duration, float damagePerSecondRatio)
        {
            m_Owner = owner;
            m_Radius = Mathf.Max(0.5f, radius);
            m_RemainingTime = Mathf.Max(0.5f, duration);
            m_DamagePerSecond = owner != null && owner.Stats != null
                ? owner.Stats.Attack * Mathf.Max(0f, damagePerSecondRatio)
                : 0f;
            m_TickTimer = 0f;

            m_BaseScale = transform.localScale;
            if (zoneRenderer != null && zoneRenderer.sprite != null)
            {
                float diameter = m_Radius * 2f;
                float width = Mathf.Max(0.001f, zoneRenderer.sprite.bounds.size.x);
                float scale = diameter / width;
                m_BaseScale = new Vector3(scale, scale, 1f);
                zoneRenderer.transform.localScale = m_BaseScale;
            }
        }

        private void Update()
        {
            m_RemainingTime -= Time.deltaTime;
            m_TickTimer -= Time.deltaTime;

            if (zoneRenderer != null)
            {
                zoneRenderer.transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);
            }

            if (m_TickTimer <= 0f)
            {
                m_TickTimer = tickInterval;
                DealDamage();
            }

            if (m_RemainingTime <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void DealDamage()
        {
            if (m_DamagePerSecond <= 0f) return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, m_Radius, tankLayer);
            float damage = m_DamagePerSecond * tickInterval;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;

                ColliderOwnerRef ownerRef = hit.GetComponent<ColliderOwnerRef>() ?? hit.GetComponentInParent<ColliderOwnerRef>();
                TankBrain tank = ownerRef?.GetOwner<TankBrain>();
                if (tank == null || !tank.IsAlive) continue;

                DamageInfo info = new DamageInfo(
                    m_Owner,
                    tank,
                    damage,
                    0f,
                    hit.bounds.center,
                    false,
                    false,
                    false,
                    DamageSourceType.EnemyAttack);
                tank.TakeDamage(info);
                IronWasteland.TextDamageManager.Instance?.Show(info.FinalDamage, hit.bounds.center);
            }
        }
    }
}
