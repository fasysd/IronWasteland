using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.The2Face
{
    /// <summary>
    /// Chế độ của vùng xoáy TuNhiet.
    /// </summary>
    public enum The2FaceVortexMode
    {
        Sad,    // Buồn bã: 20% ATK/s, gây debuff Buồn bã (-20% SPD, -10% ATK/DEF) cho Tank trong vùng
        Anger   // Tức giận: 40% ATK/s, Tank khi bị tấn công đầu tiên bị trừ 20% Max Mana (mỗi Tank chỉ trừ 1 lần cho 1 lần cast)
    }

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
        private The2FaceVortexMode m_Mode;
        private float m_Radius;
        private float m_RemainingTime;
        private float m_DamagePerSecond;
        private float m_TickTimer;
        private Vector3 m_BaseScale;

        // Cấu hình hiệu ứng Buồn bã
        private float m_SadSlowPercent;
        private float m_SadAttackReduction;
        private float m_SadDefenseReduction;
        private float m_SadDuration;

        // Cấu hình hiệu ứng Tức giận
        private float m_AngerEnergyDrainRatio;
        private readonly HashSet<TankBrain> m_EnergyDrainedTanks = new HashSet<TankBrain>();

        /// <summary>Chế độ hiện tại của vùng xoáy (Buồn bã hoặc Tức giận).</summary>
        public The2FaceVortexMode Mode => m_Mode;

        /// <summary>Khởi tạo vùng xoáy với chế độ Buồn bã hoặc Tức giận.</summary>
        public void Initialize(
            EnemyBrain owner,
            The2FaceVortexMode mode,
            float radius,
            float duration,
            float damagePerSecondRatio,
            float sadSlowPercent = 0.2f,
            float sadAttackReduction = 0.1f,
            float sadDefenseReduction = 0.1f,
            float sadDuration = 5f,
            float angerEnergyDrainRatio = 0.2f)
        {
            m_Owner = owner;
            m_Mode = mode;
            m_Radius = Mathf.Max(0.5f, radius);
            m_RemainingTime = Mathf.Max(0.5f, duration);
            m_DamagePerSecond = owner != null && owner.Stats != null
                ? owner.Stats.Attack * Mathf.Max(0f, damagePerSecondRatio)
                : 0f;
            m_TickTimer = 0f;

            m_SadSlowPercent = sadSlowPercent;
            m_SadAttackReduction = sadAttackReduction;
            m_SadDefenseReduction = sadDefenseReduction;
            m_SadDuration = sadDuration;

            m_AngerEnergyDrainRatio = angerEnergyDrainRatio;
            m_EnergyDrainedTanks.Clear();

            m_BaseScale = transform.localScale;
            if (zoneRenderer != null && zoneRenderer.sprite != null)
            {
                float diameter = m_Radius * 2f;
                float width = Mathf.Max(0.001f, zoneRenderer.sprite.bounds.size.x);
                float scale = diameter / width;
                m_BaseScale = new Vector3(scale, scale, 1f);
                zoneRenderer.transform.localScale = m_BaseScale;

                // Đổi màu trực quan để phân biệt rõ ràng 2 trạng thái
                if (m_Mode == The2FaceVortexMode.Sad)
                {
                    zoneRenderer.color = new Color(0.25f, 0.55f, 1f, 0.55f); // Xanh dương buồn bã
                }
                else
                {
                    zoneRenderer.color = new Color(1f, 0.28f, 0.12f, 0.6f); // Đỏ cam tức giận
                }
            }
        }

        private void Update()
        {
            m_RemainingTime -= Time.deltaTime;
            m_TickTimer -= Time.deltaTime;

            if (zoneRenderer != null)
            {
                float speed = m_Mode == The2FaceVortexMode.Anger ? rotateSpeed * 1.5f : rotateSpeed;
                zoneRenderer.transform.Rotate(0f, 0f, speed * Time.deltaTime);
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

                //Xử lý hiệu ứng theo trạng thái kỹ năng
                if (m_Mode == The2FaceVortexMode.Sad)
                {
                    // Buồn bã: áp dụng hiệu ứng Buồn bã (-20% SPD, -10% ATK/DEF) lên mọi Tank trong phạm vi
                    tank.ApplyEffect(new The2FaceSadEffect(
                        m_SadDuration,
                        m_SadSlowPercent,
                        m_SadAttackReduction,
                        m_SadDefenseReduction));
                }
                else if (m_Mode == The2FaceVortexMode.Anger)
                {
                    // Tức giận: trừ 20% năng lượng tối đa
                    if (m_EnergyDrainedTanks.Add(tank))
                    {
                        tank.ApplyEffect(new The2FaceAngerEffect(
                            m_SadDuration,
                            m_AngerEnergyDrainRatio,
                            m_Owner));
                    }
                }
            }
        }
    }
}
