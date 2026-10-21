using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Mặt Trăng Đen - Kẻ địch thường có di chuyển tìm đường tiếp cận mục tiêu trong phạm vi A.
    /// Khi mục tiêu ở trong phạm vi A và kỹ năng sẵn sàng, thi triển kỹ năng 2 giai đoạn:
    /// - Giai đoạn 1: Dịch chuyển đến vị trí mục tiêu.
    /// - Giai đoạn 2: Gây sát thương diện rộng xung quanh bản thân (100% tấn công).
    /// </summary>
    [DisallowMultipleComponent]
    public class NormalEnemy_MatTrangDen : NormalEnemy_BaseChaser
    {
        [Header("Skill Settings")]
        [Tooltip("Component Skill của Mặt Trăng Đen (nếu để trống sẽ tự tìm trong Children).")]
        [SerializeField] private Skill_MatTrangDen skill;

        [Tooltip("Thời gian hồi kỹ năng (giây).")]
        [SerializeField, Min(0.5f)] private float skillCooldown = 3.5f;

        [SerializeField, ReadOnly] private float currentCooldownTimer;
        [SerializeField, ReadOnly] private bool isCastingSkill;

        public Skill_MatTrangDen Skill => skill;
        public float SkillCooldown => skillCooldown;
        public float CurrentCooldownTimer => currentCooldownTimer;
        public bool IsCastingSkill => isCastingSkill;

        protected override void Awake()
        {
            base.Awake();

            if (skill == null)
            {
                skill = GetComponentInChildren<Skill_MatTrangDen>(true);
            }

            if (skill != null)
            {
                skill.OnDamageDealt += HandleSkillDamageDealt;
            }
        }

        protected virtual void OnDestroy()
        {
            if (skill != null)
            {
                skill.OnDamageDealt -= HandleSkillDamageDealt;
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

            repathTimer -= deltaTime;
            if (movementAgent != null && Stats != null)
            {
                movementAgent.SetSpeed(Stats.MoveSpeed);
            }

            // 1. Quét tìm và kiểm tra mục tiêu
            currentTarget = ScanAndValidateTarget();

            Vector2 currentPos = transform.position;
            float distToHome = Vector2.Distance(currentPos, anchorPointD);

            if (currentTarget != null)
            {
                float distToTarget = Vector2.Distance(currentPos, currentTarget.position);

                // Khi mục tiêu nằm trong tầm A:
                if (distToTarget <= radiusA)
                {
                    movementAgent?.Stop();

                    // Nếu đã hết hồi chiêu -> Kích hoạt kỹ năng ngay
                    if (currentCooldownTimer <= 0f)
                    {
                        CastSkill(currentTarget.position);
                        return;
                    }

                    // Nếu đang trong thời gian hồi chiêu: đứng yên quan sát
                    return;
                }

                // Khi mục tiêu nằm ngoài tầm A: tiếp tục tìm đường tiếp cận
                HandleTargetTracking(currentPos, currentTarget.position);

                // Nếu sau khi di chuyển đã vào tầm A và hết hồi chiêu -> thi triển ngay
                if (distToTarget <= radiusA && currentCooldownTimer <= 0f)
                {
                    movementAgent?.Stop();
                    CastSkill(currentTarget.position);
                }
            }
            else
            {
                HandleReturnHomeLogic(currentPos, distToHome, deltaTime);
            }
        }

        public void CastSkill(Vector2 targetPosition)
        {
            if (isCastingSkill || skill == null) return;

            isCastingSkill = true;
            movementAgent?.Stop();

            skill.CastSkill(movementAgent, transform, targetPosition, OnSkillFinished);
        }

        private void OnSkillFinished()
        {
            isCastingSkill = false;
            currentCooldownTimer = skillCooldown;
        }

        private void HandleSkillDamageDealt(Skill_MatTrangDen sender, List<TankBrain> targets)
        {
            if (targets == null || targets.Count == 0) return;

            float damage = Stats != null ? Stats.Attack : 10f;
            foreach (TankBrain tank in targets)
            {
                if (tank == null || !tank.IsAlive) continue;
                tank.TakeDamage(damage);
                TextDamageManager.Instance?.Show(damage, tank.transform.position);
            }
        }

        protected override void Die()
        {
            if (skill != null)
            {
                skill.CancelSkill();
            }
            isCastingSkill = false;

            base.Die();
        }
    }
}
