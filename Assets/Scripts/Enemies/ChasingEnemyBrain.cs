using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Lop dem chung cho quai "lao vao tan cong": tu tim tank nguoi choi,
    /// di chuyen toi muc tieu bang Rigidbody2D va quay mat theo huong chay.
    ///
    /// Khong trien khai OnDeath - lop con quyet dinh chet thi lam gi.
    /// </summary>
    public abstract class ChasingEnemyBrain : NormalEnemyBrain
    {
        [Header("Di chuyen")]
        [Tooltip("Khoang cach dung lai (khong lao them vao muc tieu).")]
        [SerializeField, Min(0f)] private float stopDistance = 0.35f;

        [Tooltip("Do nhay khi doi huong (cang lon cang quay nhanh).")]
        [SerializeField, Min(0.01f)] private float turnSharpness = 14f;

        [Tooltip("Latinh huong di chuyen khi khong co muc tieu (bat/tat quay mat).")]
        [SerializeField] private bool faceMoveDirection = true;

        private Rigidbody2D m_Body;
        private SpriteRenderer m_Sprite;
        private Vector2 m_DesiredVelocity;
        private TankBrain m_Target;

        /// <summary>Tank nguoi choi hien tai (co the null neu chua spawn).</summary>
        protected TankBrain Target
        {
            get
            {
                if (m_Target != null && m_Target.IsAlive) return m_Target;

                m_Target = FindPlayerTank();
                return m_Target;
            }
        }

        /// <summary>Khoang cach tu quai toi muc tieu (Mathf.Infinity neu chua co muc tieu).</summary>
        protected float DistanceToTarget
        {
            get
            {
                TankBrain target = Target;
                if (target == null) return Mathf.Infinity;
                return Vector2.Distance(transform.position, target.transform.position);
            }
        }

        protected Rigidbody2D Body
        {
            get
            {
                if (m_Body == null)
                {
                    m_Body = GetComponent<Rigidbody2D>();
                    if (m_Body != null) m_Body.gravityScale = 0f;
                }

                return m_Body;
            }
        }

        /// <summary>Huong (da chuan hoa) tu quai toi tank; Vector2.right neu chua co muc tieu.</summary>
        protected Vector2 DirectionToTarget
        {
            get
            {
                TankBrain target = Target;
                if (target == null) return Vector2.right;

                Vector2 delta = (Vector2)target.transform.position - (Vector2)transform.position;
                return delta.sqrMagnitude < 0.0001f ? Vector2.right : delta.normalized;
            }
        }

        /// <summary>
        /// Vector van toc mong muon (chua lam min). Lop con override de doi cach di chuyen
        /// (giu khoang cach, di vong quanh...) ma khong phai viet lai phan ap van toc.
        /// </summary>
        protected virtual Vector2 ComputeDesiredVelocity(TankBrain target, float distance)
        {
            if (distance <= stopDistance || distance <= 0.0001f) return Vector2.zero;

            Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;
            return toTarget / distance * Mathf.Max(0f, Stats.MoveSpeed);
        }

        protected override void OnBrainTick(float deltaTime)
        {
            TankBrain target = Target;

            if (target == null)
            {
                m_DesiredVelocity = Vector2.zero;
                return;
            }

            Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;
            float distance = toTarget.magnitude;

            Vector2 desired = ComputeDesiredVelocity(target, distance);

            // Lam min huong chay de quai khong giat khi muc tieu doi huong dot ngot.
            float t = 1f - Mathf.Exp(-turnSharpness * deltaTime);
            m_DesiredVelocity = Vector2.Lerp(m_DesiredVelocity, desired, t);

            if (faceMoveDirection && Mathf.Abs(m_DesiredVelocity.x) > 0.01f)
            {
                if (m_Sprite == null && Model != null) m_Sprite = Model.SpriteRenderer;
                if (m_Sprite != null) m_Sprite.flipX = m_DesiredVelocity.x < 0f;
            }
        }

        private void FixedUpdate()
        {
            Rigidbody2D body = Body;
            if (body == null) return;

            // Prefab quai dung Kinematic (khong bi trong luc / khong day tank):
            // voi Kinematic phai dung MovePosition, linearVelocity se bi bo qua.
            if (body.bodyType == RigidbodyType2D.Kinematic)
            {
                body.MovePosition(body.position + m_DesiredVelocity * Time.fixedDeltaTime);
            }
            else
            {
                body.linearVelocity = m_DesiredVelocity;
            }
        }

        /// <summary>
        /// Gay sat thuong cho moi tank co collider nam trong ban kinh (tinh theo hinh hoc collider,
        /// khong phai khoang cach tam-tam). Tra ve so tank trung don.
        /// </summary>
        protected static int DamageTanksInRadius(Vector2 center, float radius, float damage)
        {
            if (damage <= 0f || radius <= 0f) return 0;

            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
            HashSet<TankBrain> damaged = new HashSet<TankBrain>();

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;

                TankBrain tank = hit.GetComponentInParent<TankBrain>();
                if (tank == null || !tank.IsAlive) continue;
                if (!damaged.Add(tank)) continue;

                tank.TakeDamage(damage);
                TextDamageManager.Instance?.Show(damage, tank.transform.position);
            }

            return damaged.Count;
        }

        private static TankBrain FindPlayerTank()
        {
            GameController controller = FindAnyObjectByType<GameController>();
            if (controller != null && controller.PlayerTank != null) return controller.PlayerTank;

            return FindAnyObjectByType<TankBrain>();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (stopDistance < 0f) stopDistance = 0f;
            if (turnSharpness < 0.01f) turnSharpness = 0.01f;
        }
    }
}
