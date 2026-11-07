using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Quai Vat Thach Anh (chuong 1): tan cong tu xa, moi dot phong 3-5 gai nhon
    /// duoi theo muc tieu. Gai gay 100% Tan cong; neu muc tieu trung tu 2 gai tro len
    /// trong cung 1 dot thi cac gai sau tang them 25% he so sat thuong.
    /// Theo tai lieu: HP 220 / ATK 50 / SPD 85 / CD 1.5s.
    /// </summary>
    [DisallowMultipleComponent]
    public class QuartzBruteBrain : ChasingEnemyBrain
    {
        [Header("Giu khoang cach")]
        [SerializeField, Min(0f)] private float keepMinDistance = 6.5f;
        [SerializeField, Min(0f)] private float keepMaxDistance = 9.5f;
        [SerializeField, Range(0f, 1f)] private float strafeSpeedRatio = 0.4f;

        [Header("Ban gai")]
        [SerializeField, Min(0.1f)] private float attackRange = 12f;
        [SerializeField, Min(0.05f)] private float cooldown = 1.5f;
        [SerializeField, Range(1, 8)] private int spikeCountMin = 3;
        [SerializeField, Range(1, 8)] private int spikeCountMax = 5;
        [SerializeField, Min(0f)] private float damageRatio = 1f;
        [SerializeField, Min(0f)] private float stackBonus = 0.25f;
        [SerializeField, Min(0.5f)] private float spikeSpeed = 7f;
        [SerializeField, Min(0.2f)] private float spikeLifetime = 4f;
        [SerializeField, Range(0f, 60f)] private float spreadAngle = 14f;
        [SerializeField] private bool logVolley;

        private float m_Cooldown;
        private float m_StrafeSign = 1f;
        private float m_StrafeTimer;

        protected override Vector2 ComputeDesiredVelocity(TankBrain target, float distance)
        {
            float speed = Mathf.Max(0f, Stats.MoveSpeed);
            Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;

            if (distance <= 0.0001f) return Vector2.zero;

            Vector2 forward = toTarget / distance;

            if (distance > keepMaxDistance) return forward * speed;
            if (distance < keepMinDistance) return -forward * speed;

            m_StrafeTimer += Time.deltaTime;
            if (m_StrafeTimer >= 2.5f)
            {
                m_StrafeTimer = 0f;
                m_StrafeSign = -m_StrafeSign;
            }

            Vector2 tangent = new Vector2(-forward.y, forward.x) * m_StrafeSign;
            return tangent * (speed * strafeSpeedRatio);
        }

        protected override void OnBrainTick(float deltaTime)
        {
            base.OnBrainTick(deltaTime);

            TankBrain target = Target;
            if (target == null) return;

            m_Cooldown -= deltaTime;
            if (m_Cooldown > 0f) return;

            if (DistanceToTarget > attackRange) return;

            m_Cooldown = cooldown;
            FireVolley(target);
        }

        private void FireVolley(TankBrain target)
        {
            int count = Random.Range(spikeCountMin, spikeCountMax + 1);
            QuartzVolley volley = new QuartzVolley
            {
                BaseDamage = Mathf.Max(0f, Stats.Attack * damageRatio),
                StackBonus = stackBonus,
            };

            Vector2 origin = transform.position;
            Vector2 aim = ((Vector2)target.transform.position - origin).normalized;

            for (int i = 0; i < count; i++)
            {
                float angle = spreadAngle * (count == 1 ? 0f : (i / (float)(count - 1)) * 2f - 1f);
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * aim;
                Vector2 spawnPosition = origin + direction * 0.8f;

                QuartzSpike.Spawn(spawnPosition, target, volley, spikeSpeed, spikeLifetime);
            }

            if (logVolley)
            {
                Debug.Log("[QuartzBrute] Ban " + count + " gai, moi gai " + volley.BaseDamage.ToString("0") + " sat thuong (+" + (stackBonus * 100f).ToString("0") + "% moi gai trung sau).");
            }
        }

        protected override void OnDeath()
        {
            Destroy(gameObject);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (keepMinDistance > keepMaxDistance) keepMinDistance = keepMaxDistance;
            if (spikeCountMin < 1) spikeCountMin = 1;
            if (spikeCountMax < spikeCountMin) spikeCountMax = spikeCountMin;
            if (cooldown < 0.05f) cooldown = 0.05f;
        }
    }
}
