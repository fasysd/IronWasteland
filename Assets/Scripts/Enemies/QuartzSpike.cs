using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Mot lan ban gai cua Quai Vat Thach Anh. Cac gai trong cung 1 loat chia se
    /// bo dem trung don: gai trung sau tang them 25% he so sat thuong.
    /// </summary>
    public class QuartzVolley
    {
        public float BaseDamage;
        public float StackBonus = 0.25f;
        public int HitCount;

        /// <summary>Lay he so sat thuong cho gai tiep theo roi tang bo dem.</summary>
        public float NextDamage()
        {
            float multiplier = 1f + StackBonus * HitCount;
            HitCount++;
            return Mathf.Max(0f, BaseDamage * multiplier);
        }
    }

    /// <summary>Gai nhon tu dong duoi theo muc tieu (ban ra tu Quai Vat Thach Anh).</summary>
    [DisallowMultipleComponent]
    public class QuartzSpike : MonoBehaviour
    {
        private const float HitGap = 0.2f;

        private TankBrain m_Target;
        private QuartzVolley m_Volley;
        private float m_Speed = 8f;
        private float m_Lifetime = 4f;
        private float m_Age;
        private SpriteRenderer m_Renderer;

        public static QuartzSpike Spawn(Vector2 position, TankBrain target, QuartzVolley volley, float speed, float lifetime)
        {
            GameObject go = new GameObject("QuartzSpike");
            go.transform.position = position;

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteFactory.Circle;
            renderer.color = new Color(0.85f, 0.75f, 1f, 1f);
            renderer.sortingOrder = 4;
            go.transform.localScale = Vector3.one * 0.4f;

            QuartzSpike spike = go.AddComponent<QuartzSpike>();
            spike.m_Target = target;
            spike.m_Volley = volley;
            spike.m_Speed = Mathf.Max(0.5f, speed);
            spike.m_Lifetime = Mathf.Max(0.2f, lifetime);
            spike.m_Renderer = renderer;
            return spike;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            m_Age += dt;

            if (m_Age >= m_Lifetime || m_Target == null || !m_Target.IsAlive)
            {
                Destroy(gameObject);
                return;
            }

            Vector2 position = transform.position;
            Vector2 toTarget = (Vector2)m_Target.transform.position - position;

            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Vector2 direction = toTarget.normalized;
                transform.position = position + direction * (m_Speed * dt);
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (HasHitTarget()) Hit();
        }

        private bool HasHitTarget()
        {
            Collider2D self = GetComponent<Collider2D>();
            Bounds spikeBounds = self != null
                ? self.bounds
                : new Bounds(transform.position, Vector3.one * 0.35f);

            Collider2D targetCollider = FindSolidCollider(m_Target);
            if (targetCollider == null) return false;

            return BoundsGap(spikeBounds, targetCollider.bounds) <= HitGap;
        }

        private void Hit()
        {
            float damage = m_Volley != null ? m_Volley.NextDamage() : 0f;

            if (damage > 0f && m_Target != null)
            {
                m_Target.TakeDamage(damage);
                TextDamageManager.Instance?.Show(damage, m_Target.transform.position);
            }

            Destroy(gameObject);
        }

        private static Collider2D FindSolidCollider(TankBrain tank)
        {
            Collider2D[] colliders = tank.GetComponentsInChildren<Collider2D>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D candidate = colliders[i];
                if (candidate == null || !candidate.enabled) continue;
                if (candidate.isTrigger) continue;
                if (candidate.bounds.size.sqrMagnitude <= 0.0001f) continue;
                return candidate;
            }

            return null;
        }

        private static float BoundsGap(Bounds a, Bounds b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
            float dy = Mathf.Max(0f, Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private void OnDestroy()
        {
            if (m_Renderer != null) m_Renderer.sprite = null;
        }
    }
}
