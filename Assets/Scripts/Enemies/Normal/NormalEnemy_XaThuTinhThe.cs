using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Xạ Thủ Tinh Thể - Kẻ địch tầm xa bắn ra viên đạn xuyên thấu bay thẳng về phía mục tiêu.
    /// Gây 100% tấn công cho mỗi xe tăng mà viên đạn xuyên qua.
    /// </summary>
    [DisallowMultipleComponent]
    public class NormalEnemy_XaThuTinhThe : NormalEnemy_BaseChaser
    {
        [Header("Combat Settings")]
        [Tooltip("Prefab đạn xuyên thấu.")]
        [SerializeField] private Projectile_XaThuTinhThe projectilePrefab;

        [Tooltip("Điểm xuất phát của viên đạn (nếu để trống sẽ lấy từ vị trí của Model).")]
        [SerializeField] private Transform firePoint;

        [Tooltip("Thời gian hồi giữa mỗi phát bắn (giây).")]
        [SerializeField, Min(0.5f)] private float attackCooldown = 2.5f;

        [SerializeField, ReadOnly] private float attackCooldownTimer;

        public Projectile_XaThuTinhThe ProjectilePrefab => projectilePrefab;
        public float AttackCooldown => attackCooldown;
        public float AttackCooldownTimer => attackCooldownTimer;

        protected override void Awake()
        {
            base.Awake();

            if (firePoint == null)
            {
                firePoint = Model != null ? Model.transform : transform;
            }
        }

        protected override void OnBrainTick(float deltaTime)
        {
            if (attackCooldownTimer > 0f)
            {
                attackCooldownTimer = Mathf.Max(0f, attackCooldownTimer - deltaTime);
            }

            repathTimer -= deltaTime;
            if (movementAgent != null && Stats != null)
            {
                movementAgent.SetSpeed(Stats.MoveSpeed);
            }

            currentTarget = ScanAndValidateTarget();

            Vector2 currentPos = transform.position;
            float distToHome = Vector2.Distance(currentPos, anchorPointD);

            if (currentTarget != null)
            {
                float distToTarget = Vector2.Distance(currentPos, currentTarget.position);

                // Khi mục tiêu nằm trong tầm bắn A hoặc đang ở trạng thái dừng bắn trong B
                if (distToTarget <= radiusA || currentState == NormalEnemyState.StandStillInB)
                {
                    movementAgent?.Stop();

                    if (attackCooldownTimer <= 0f)
                    {
                        RotateTowards(currentTarget.position);
                        Shoot();
                    }
                    return;
                }

                // Nếu mục tiêu ngoài tầm, tiếp tục tìm đường tiếp cận
                HandleTargetTracking(currentPos, currentTarget.position);

                // Nếu vừa vào tầm A thì bắn ngay nếu sẵn sàng
                if (distToTarget <= radiusA && attackCooldownTimer <= 0f)
                {
                    movementAgent?.Stop();
                    RotateTowards(currentTarget.position);
                    Shoot();
                }
            }
            else
            {
                HandleReturnHomeLogic(currentPos, distToHome, deltaTime);
            }
        }

        public void Shoot()
        {
            if (projectilePrefab == null || currentTarget == null) return;

            Vector2 spawnPos = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
            Vector2 dir = ((Vector2)currentTarget.position - spawnPos);
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = transform.right;
            }
            else
            {
                dir.Normalize();
            }

            Projectile_XaThuTinhThe projectile = Instantiate(
                projectilePrefab,
                spawnPos,
                Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg)
            );
            projectile.gameObject.SetActive(true);

            if (projectile != null)
            {
                projectile.Initialize(dir, Stats != null ? Stats.Attack : 10f, OnProjectileHit);
            }

            attackCooldownTimer = attackCooldown;
        }

        public void RotateTowards(Vector2 targetPosition)
        {
            Vector2 dir = targetPosition - (Vector2)transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                float offset = movementAgent != null ? movementAgent.RotationOffset : 90f;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + offset;
                Transform targetTransform = Model != null ? Model.transform : transform;
                targetTransform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void OnProjectileHit(EnemyProjectileBase sender, TankBrain tank)
        {
            if (tank == null || !tank.IsAlive) return;

            float damage = Stats != null ? Stats.Attack : 10f;
            tank.TakeDamage(damage);
            TextDamageManager.Instance?.Show(damage, tank.transform.position);
        }
    }
}
