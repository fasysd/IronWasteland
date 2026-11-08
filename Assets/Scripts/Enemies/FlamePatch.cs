using System.Collections.Generic;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Vet lua (hoac vung sat thuong dung yen) do quai de lai tren san.
    /// Theo tai lieu: ban kinh 1.5m, ton tai 5s, gay 100% Tan cong moi 1s
    /// va lam cham 20% toc do trong 2s.
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

        /// <summary>Tao mot vet lua tai vi tri chi dinh va tu huy sau khi het thoi gian ton tai.</summary>
        public static FlamePatch Spawn(
            Vector2 position,
            float radius,
            float lifetime,
            float damagePerTick,
            float slowPercent,
            float slowDuration)
        {
            GameObject go = new GameObject("FlamePatch");
            go.transform.position = position;

            FlamePatch patch = go.AddComponent<FlamePatch>();
            patch.m_Radius = Mathf.Max(0.05f, radius);
            patch.m_Lifetime = Mathf.Max(0.05f, lifetime);
            patch.m_DamagePerTick = Mathf.Max(0f, damagePerTick);
            patch.m_SlowPercent = Mathf.Clamp01(slowPercent);
            patch.m_SlowDuration = Mathf.Max(0f, slowDuration);
            patch.BuildVisual();
            return patch;
        }

        private void BuildVisual()
        {
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteFactory.SoftCircle;
            renderer.color = m_BaseColor;
            // Sprite 64px @ 64 PPU = 1 don vi -> scale = duong kinh mong muon.
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

            if (m_Age >= m_Lifetime) Destroy(gameObject);
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
                    tank.TakeDamage(m_DamagePerTick);
                    TextDamageManager.Instance?.Show(m_DamagePerTick, tank.transform.position);
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
