using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Quái Vật Thạch Anh - Kẻ địch tầm xa phóng ra ngẫu nhiên 3 đến 5 viên đạn xòe góc 30 độ và đuổi theo mục tiêu.
    /// Sát thương tăng tiến (100%, 125%, 150%, 175%, 200%) nếu cùng một mục tiêu trúng nhiều viên trong 1 loạt bắn.
    /// </summary>
    [DisallowMultipleComponent]
    public class NormalEnemy_QuaiVatTinhThach : NormalEnemy_BaseChaser
    {
        [Header("Combat Settings")]
        [Tooltip("Prefab đạn đuổi mục tiêu.")]
        [SerializeField] private Projectile_QuaiVatTinhThach projectilePrefab;

        [Tooltip("Điểm xuất phát của viên đạn (nếu để trống sẽ lấy từ vị trí của Model).")]
        [SerializeField] private Transform firePoint;

        [Tooltip("Góc lệch cách đều giữa các viên đạn (mặc định 30 độ).")]
        [SerializeField, Min(5f)] private float angleSpacing = 30f;

        [Tooltip("Thời gian hồi giữa mỗi loạt bắn (giây).")]
        [SerializeField, Min(0.5f)] private float attackCooldown = 4.0f;

        [SerializeField, ReadOnly] private float attackCooldownTimer;
        [SerializeField, ReadOnly] private int currentVolleyId;

        private readonly Dictionary<TankBrain, int> m_VolleyHitCounts = new Dictionary<TankBrain, int>();

        public Projectile_QuaiVatTinhThach ProjectilePrefab => projectilePrefab;
        public float AttackCooldown => attackCooldown;
        public float AttackCooldownTimer => attackCooldownTimer;
        public int CurrentVolleyId => currentVolleyId;

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
                        FireVolley();
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
                    FireVolley();
                }
            }
            else
            {
                HandleReturnHomeLogic(currentPos, distToHome, deltaTime);
            }
        }

        public void FireVolley()
        {
            if (projectilePrefab == null || currentTarget == null) return;

            currentVolleyId++;
            m_VolleyHitCounts.Clear();

            Vector2 spawnPos = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
            Vector2 forward = ((Vector2)currentTarget.position - spawnPos);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = transform.right;
            }
            else
            {
                forward.Normalize();
            }

            // Tạo ngẫu nhiên từ 3 đến 5 viên đạn
            int bulletCount = Random.Range(3, 6); // 3, 4 hoặc 5
            float startAngle = -(bulletCount - 1) * angleSpacing / 2f;

            for (int i = 0; i < bulletCount; i++)
            {
                float angleOffset = startAngle + i * angleSpacing;
                Vector2 shootDir = (Quaternion.Euler(0f, 0f, angleOffset) * forward).normalized;

                Projectile_QuaiVatTinhThach projectile = Instantiate(
                    projectilePrefab,
                    spawnPos,
                    Quaternion.Euler(0f, 0f, Mathf.Atan2(shootDir.y, shootDir.x) * Mathf.Rad2Deg)
                );
                projectile.gameObject.SetActive(true);

                if (projectile != null)
                {
                    projectile.InitializeHoming(
                        shootDir,
                        Stats != null ? Stats.Attack : 10f,
                        currentTarget,
                        currentVolleyId,
                        OnHomingProjectileHit
                    );
                }
            }

            attackCooldownTimer = attackCooldown;
        }

        public void RotateTowards(Vector2 targetPosition)
        {
            Vector2 dir = targetPosition - (Vector2)transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                float offset = movementAgent != null ? movementAgent.RotationOffset : -90f;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + offset;
                Transform targetTransform = Model != null ? Model.transform : transform;
                targetTransform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void OnHomingProjectileHit(EnemyProjectileBase sender, TankBrain tank)
        {
            if (tank == null || !tank.IsAlive) return;

            if (sender is Projectile_QuaiVatTinhThach homingProj)
            {
                // Kiểm tra xem đạn có thuộc loạt bắn hiện tại hay không
                if (homingProj.VolleyId != currentVolleyId) return;

                if (!m_VolleyHitCounts.TryGetValue(tank, out int hitCount))
                {
                    hitCount = 0;
                }
                hitCount++;
                m_VolleyHitCounts[tank] = hitCount;

                // Công thức sát thương tăng tiến:
                // Viên 1: 100% (multiplier = 1.0)
                // Viên 2: 125% (multiplier = 1.25)
                // Viên 3: 150% (multiplier = 1.50)
                // Viên 4: 175% (multiplier = 1.75)
                // Viên 5: 200% (multiplier = 2.00)
                float multiplier = 1.0f + (hitCount - 1) * 0.25f;
                float baseAtk = Stats != null ? Stats.Attack : 10f;
                float finalDamage = baseAtk * multiplier;

                tank.TakeDamage(finalDamage);
                TextDamageManager.Instance?.Show(finalDamage, tank.transform.position);
            }
        }
    }
}
