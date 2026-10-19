using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Kiến Lửa Khổng Lồ - Kẻ địch thường có kỹ năng tấn công hình tam giác theo thời gian hồi chiêu.
    /// </summary>
    [DisallowMultipleComponent]
    public class NormalEnemy_KIenLuaKhongLo : NormalEnemy_BaseChaser
    {
        [Header("Skill Settings")]
        [SerializeField] private Skill_KIenLuaKhongLo skill;
        [SerializeField, Min(0.1f)] private float skillCooldown = 3f;
        [SerializeField, ReadOnly] private float currentCooldownTimer;
        [SerializeField, ReadOnly] private bool isCastingSkill;

        public Skill_KIenLuaKhongLo Skill => skill;
        public float SkillCooldown => skillCooldown;
        public float CurrentCooldownTimer => currentCooldownTimer;
        public bool IsCastingSkill => isCastingSkill;

        protected override void Awake()
        {
            base.Awake();

            if (skill == null)
            {
                skill = GetComponentInChildren<Skill_KIenLuaKhongLo>(true);
            }

            if (skill != null)
            {
                skill.OnFound += HandleSkillTargetFound;
            }
        }

        protected virtual void OnDestroy()
        {
            if (skill != null)
            {
                skill.OnFound -= HandleSkillTargetFound;
            }
        }

        protected override void OnBrainTick(float deltaTime)
        {
            if (currentCooldownTimer > 0f)
            {
                currentCooldownTimer = Mathf.Max(0f, currentCooldownTimer - deltaTime);
            }


            // Nếu đang trong quá trình cast chiêu: dừng di chuyển và chờ hoàn tất
            if (isCastingSkill)
            {
                movementAgent?.Stop();
                return;
            }

            base.OnBrainTick(deltaTime);

            repathTimer -= deltaTime;
            if (movementAgent != null && Stats != null)
            {
                movementAgent.SetSpeed(Stats.MoveSpeed);
            }

            // Quét tìm mục tiêu
            currentTarget = ScanAndValidateTarget();

            Vector2 currentPos = transform.position;
            float distToHome = Vector2.Distance(currentPos, anchorPointD);

            if (currentTarget != null)
            {
                // Khi đang trong thời gian hồi chiêu:
                // Đứng yên hoàn toàn (kể cả mục tiêu ra khỏi phạm vi B cũng không tìm đường)
                if (currentCooldownTimer > 0f)
                {
                    movementAgent?.Stop();
                    awayFromHomeTimer = 0f;
                    return;
                }

                // Khi hết thời gian hồi chiêu:
                // Nếu đang đứng yên trong B -> Xoay về mục tiêu và thi triển Skill
                if (currentState == NormalEnemyState.StandStillInB)
                {
                    CastSkill();
                    return;
                }

                // Nếu chưa đứng yên (mục tiêu ở ngoài B), tìm đường tiếp cận mục tiêu vào A
                HandleTargetTracking(currentPos, currentTarget.position);

                // Nếu sau khi bám đuổi đã vào tầm và chuyển sang đứng yên -> thi triển skill ngay
                if (currentState == NormalEnemyState.StandStillInB && currentCooldownTimer <= 0f)
                {
                    CastSkill();
                }
            }
            else
            {
                HandleReturnHomeLogic(currentPos, distToHome, deltaTime);
            }
        }

        public void CastSkill()
        {
            if (skill == null || isCastingSkill) return;

            isCastingSkill = true;
            movementAgent?.Stop();

            if (currentTarget != null)
            {
                RotateTowards(currentTarget.position);
            }

            skill.Cast(OnSkillFinished);
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

        protected override void OnDeath()
        {
            if (skill != null)
            {
                skill.CancelCast();
            }

            base.OnDeath();
        }

        private void OnSkillFinished()
        {
            isCastingSkill = false;
            currentCooldownTimer = skillCooldown;
        }

        private void HandleSkillTargetFound(Skill_KIenLuaKhongLo sender, TankBrain target)
        {
            if (target == null || !target.IsAlive) return;

            float damage = Stats != null ? Stats.Attack : 10f;
            target.TakeDamage(damage);
            TextDamageManager.Instance?.Show(damage, target.transform.position);
        }
    }
}
