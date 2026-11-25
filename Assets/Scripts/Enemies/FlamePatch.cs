using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Net;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Vết lửa (hoặc vùng sát thương đứng yên) do quái để lại trên sàn.
    /// Theo tài liệu: bán kính 1.5m, tồn tại 5s, gây 100% Tấn công mỗi 1s
    /// và làm chậm 20% tốc độ trong 2s.
    /// </summary>
    [DisallowMultipleComponent]
    public class FlamePatch : MonoBehaviour
    {
        private float m_Radius = 1.5f;
        private float m_Lifetime = 5f;
        private float m_DamagePerTick = 35f;
        private float m_TickInterval = 1f;
        private float m_SlowPercent = 0.2f;
        private float m_SlowDuration = 2f;

        private float m_Age;
        private float m_TickTimer;
        private SpriteRenderer m_Renderer;
        private Color m_BaseColor = new Color(1f, 0.45f, 0.1f, 0.45f);

        /// <summary>Tạo một vết lửa tại vị trí chỉ định và tự hủy sau khi hết thời gian tồn tại.</summary>
        public static FlamePatch Spawn(
            Vector2 position,
            float radius,
            float lifetime,
            float damagePerTick,
            float slowPercent,
            float slowDuration,
            float tickInterval = 1f)
        {
            GameObject go = new GameObject("FlamePatch");
            go.transform.position = position;

            FlamePatch patch = go.AddComponent<FlamePatch>();
            patch.m_Radius = Mathf.Max(0.05f, radius);
            patch.m_Lifetime = Mathf.Max(0.05f, lifetime);
            patch.m_DamagePerTick = Mathf.Max(0f, damagePerTick);
            patch.m_TickInterval = Mathf.Max(0.05f, tickInterval);
            patch.m_SlowPercent = Mathf.Clamp01(slowPercent);
            patch.m_SlowDuration = Mathf.Max(0f, slowDuration);
            patch.BuildVisual();

            // Client không có template cho vệt lửa (object tạo bằng code) nên host gửi visual thủ tục riêng.
            NetVisual.SpawnPrimitive(position, Vector2.zero, patch.m_Radius, patch.m_Lifetime,
                fade: true, sortingOrder: -1, color: patch.m_BaseColor);

            return patch;
        }

        private void BuildVisual()
        {
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteFactory.SoftCircle;
            renderer.color = m_BaseColor;
            // Sprite 64px @ 64 PPU = 1 đơn vị -> scale = đường kính mong muốn.
            renderer.transform.localScale = Vector3.one * (m_Radius * 2f);
            renderer.sortingOrder = -1;
            m_Renderer = renderer;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            m_Age += dt;
            m_TickTimer += dt;

            if (m_Renderer != null)
            {
                float lifeRatio = Mathf.Clamp01(1f - m_Age / m_Lifetime);
                Color color = m_BaseColor;
                color.a = m_BaseColor.a * lifeRatio;
                m_Renderer.color = color;
            }

            if (m_TickTimer >= m_TickInterval)
            {
                m_TickTimer -= m_TickInterval;
                ApplyTick();
            }

            if (m_Age >= m_Lifetime) IronWasteland.Net.NetEntity.Despawn(gameObject);
        }

        private void ApplyTick()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, m_Radius);
            HashSet<TankBrain> damaged = new HashSet<TankBrain>();

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;

                TankBrain tank = hit.GetComponentInParent<TankBrain>();
                if (tank == null || !tank.IsAlive) continue;
                if (!damaged.Add(tank)) continue;

                if (m_DamagePerTick > 0f)
                {
                    DamageInfo info = new DamageInfo(this, tank, m_DamagePerTick, 0f, tank.transform.position, sourceType: DamageSourceType.EnemyAttack);
                    tank.TakeDamage(info);
                    CombatFeedback.Show(info.FinalDamage, tank.transform.position);
                }

                if (m_SlowPercent > 0f && m_SlowDuration > 0f)
                {
                    TankSlowDebuff.Apply(tank, m_SlowPercent, m_SlowDuration);
                }
            }
        }

        private void Start()
        {
            if (m_Renderer == null) BuildVisual();
        }

        private void OnDestroy()
        {
            if (m_Renderer != null && m_Renderer.sprite == SpriteFactory.SoftCircle) m_Renderer.sprite = null;
        }
    }
}
