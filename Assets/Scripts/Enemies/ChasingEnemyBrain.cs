using System.Collections.Generic;
using IronWasteland.Core;
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

        [Tooltip("He so chia toc do di chuyen (Stats.MoveSpeed / moveSpeedDivisor). Mac dinh 100 de quy doi sang unit/s.")]
        [SerializeField, Min(1f)] private float moveSpeedDivisor = 100f;

        [Header("Rotation")]
        [Tooltip("Tu dong xoay theo huong di chuyen.")]
        [SerializeField] private bool rotateTowardsMovement = true;

        [Tooltip("Goc bu do lech ban dau cua Sprite (do).")]
        [SerializeField] private float rotationOffset = 0f;

        [Tooltip("Toc do xoay (do/giay). Gia tri <= 0 xoay tuc thoi.")]
        [SerializeField] private float rotationSpeed = 720f;

        [SerializeField] private Transform rotationPivot;

        [Tooltip("Latinh huong di chuyen khi khong co muc tieu (bat/tat quay mat flipX).")]
        [SerializeField] private bool faceMoveDirection = false;

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
            float divisor = moveSpeedDivisor > 0f ? moveSpeedDivisor : 100f;
            return toTarget / distance * Mathf.Max(0f, Stats.MoveSpeed / divisor);
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
            desired *= SlowSpeedFactor;

            // Lam min huong chay de quai khong giat khi muc tieu doi huong dot ngot.
            float t = 1f - Mathf.Exp(-turnSharpness * deltaTime);
            m_DesiredVelocity = Vector2.Lerp(m_DesiredVelocity, desired, t);

            if (rotateTowardsMovement && m_DesiredVelocity.sqrMagnitude > 0.001f)
            {
                RotateTowardsDirection(m_DesiredVelocity);
            }

            if (faceMoveDirection && Mathf.Abs(m_DesiredVelocity.x) > 0.01f)
            {
                if (m_Sprite == null && Model != null) m_Sprite = Model.SpriteRenderer;
                if (m_Sprite != null) m_Sprite.flipX = m_DesiredVelocity.x < 0f;
            }
        }

        #region Rotation

        public void RotateTowardsDirection(Vector2 direction, bool instant = false)
        {
            Transform pivot = rotationPivot != null ? rotationPivot : (Model != null ? Model.transform : transform);
            if (pivot == null) return;

            if (direction.sqrMagnitude < 0.0001f)
            {
                RotateToZero(instant);
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;
            ApplyRotationAngle(pivot, angle, instant);
        }

        public void RotateTowardsTarget(Vector2 targetPosition, bool instant = false)
        {
            Vector2 dir = targetPosition - (Vector2)transform.position;
            RotateTowardsDirection(dir, instant);
        }

        public void RotateToZero(bool instant = false)
        {
            Transform pivot = rotationPivot != null ? rotationPivot : (Model != null ? Model.transform : transform);
            if (pivot == null) return;

            ApplyRotationAngle(pivot, 0f, instant);
        }

        private void ApplyRotationAngle(Transform pivot, float targetAngle, bool instant)
        {
            if (instant || rotationSpeed <= 0f)
            {
                pivot.rotation = Quaternion.Euler(0f, 0f, targetAngle);
            }
            else
            {
                float currentAngle = pivot.eulerAngles.z;
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
                pivot.rotation = Quaternion.Euler(0f, 0f, newAngle);
            }
        }

        #endregion

        private void FixedUpdate()
        {
            // Proxy trên client: host đã mô phỏng AI và vị trí, không tự dịch chuyển ở đây.
            if (!IsServerSimulated) return;

            Rigidbody2D body = Body;
            if (body == null) return;

            // Bi khong che cung (Root/Stun): dung han, khong di chuyen.
            if (IsMovementLocked)
            {
                m_DesiredVelocity = Vector2.zero;
                body.linearVelocity = Vector2.zero;
                return;
            }

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
        protected int DamageTanksInRadius(Vector2 center, float radius, float damage)
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

                DamageInfo info = new DamageInfo(this, tank, damage, 0f, tank.transform.position, sourceType: DamageSourceType.EnemyAttack);
                tank.TakeDamage(info);
            }

            return damaged.Count;
        }

        /// <summary>
        /// Chọn tank còn sống gần quái nhất. Trước đây chỉ lấy player #1 (GameController.PlayerTank)
        /// nên các đồng đội còn lại không bao giờ bị đuổi trong chế độ nhiều người.
        /// </summary>
        private TankBrain FindPlayerTank()
        {
            TankBrain[] tanks = FindObjectsByType<TankBrain>(FindObjectsInactive.Exclude);
            Vector2 selfPosition = transform.position;
            TankBrain nearest = null;
            float nearestSqrDistance = float.MaxValue;

            for (int i = 0; i < tanks.Length; i++)
            {
                TankBrain candidate = tanks[i];
                if (candidate == null || !candidate.IsAlive) continue;

                Vector2 candidatePosition = candidate.transform.position;
                float sqrDistance = (candidatePosition - selfPosition).sqrMagnitude;
                if (sqrDistance >= nearestSqrDistance) continue;

                nearestSqrDistance = sqrDistance;
                nearest = candidate;
            }

            return nearest;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (stopDistance < 0f) stopDistance = 0f;
            if (turnSharpness < 0.01f) turnSharpness = 0.01f;
        }
    }
}
