using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Mặt Trăng Đen - Kẻ địch thường lao vào tấn công tầm gần.
    /// Theo GDD:
    /// - HP: 260 (Co-op: 520)
    /// - ATK: 15
    /// - Giáp: 50
    /// - SPD: 95
    /// - CD: 2.0s
    /// - Cơ chế: Lao vào tấn công tầm gần, phát xung tỏa ra trường bức xạ năng lượng xung quanh 2.5m và gây sát thương diện rộng mỗi 0.5s bằng 100% Tấn công.
    /// </summary>
    [DisallowMultipleComponent]
    public class NormalEnemy_MatTrangDen : NormalEnemy_BaseChaser
    {
        [Header("Skill Settings")]
        [Tooltip("Component Skill của Mặt Trăng Đen (nếu để trống sẽ tự tìm trong Children).")]
        [SerializeField] private Skill_MatTrangDen skill;

        [Tooltip("Thời gian hồi kỹ năng giữa các lần phát xung bức xạ (giây).")]
        [SerializeField, Min(0.5f)] private float skillCooldown = 2.0f;

        [Tooltip("Phạm vi tiếp cận để bắt đầu phát xung bức xạ (m). Mặc định 2.5m theo GDD.")]
        [SerializeField, Min(0.5f)] private float radiationTriggerRange = 2.5f;

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

            repathTimer -= deltaTime;
            if (movementAgent != null && Stats != null)
            {
                movementAgent.SetSpeed(Stats.MoveSpeed / 100f);
            }

            // 1. Quét tìm và kiểm tra mục tiêu
            currentTarget = ScanAndValidateTarget();

            Vector2 currentPos = transform.position;
            float distToHome = Vector2.Distance(currentPos, anchorPointD);

            if (currentTarget != null)
            {
                float distToTarget = Vector2.Distance(currentPos, currentTarget.position);

                // Nếu mục tiêu nằm trong tầm kích hoạt bức xạ (radiationTriggerRange)
                if (distToTarget <= radiationTriggerRange)
                {
                    // Nếu đã hết hồi chiêu và chưa kích hoạt bức xạ -> Bật trường bức xạ
                    if (!isCastingSkill && currentCooldownTimer <= 0f)
                    {
                        StartRadiationAura();
                    }

                    // Tiếp tục áp sát hoặc bám theo mục tiêu trong tầm gần
                    HandleTargetTracking(currentPos, currentTarget.position);
                    return;
                }

                // Khi mục tiêu ở xa: tiếp tục tìm đường lao vào mục tiêu
                HandleTargetTracking(currentPos, currentTarget.position);

                if (distToTarget <= radiationTriggerRange && !isCastingSkill && currentCooldownTimer <= 0f)
                {
                    StartRadiationAura();
                }
            }
            else
            {
                HandleReturnHomeLogic(currentPos, distToHome, deltaTime);
            }
        }

        public void StartRadiationAura()
        {
            if (isCastingSkill || skill == null) return;

            isCastingSkill = true;
            skill.CastSkill(OnSkillFinished);
        }

        private void OnSkillFinished()
        {
            isCastingSkill = false;
            currentCooldownTimer = skillCooldown;
        }

        private void HandleSkillDamageDealt(Skill_MatTrangDen sender, List<TankBrain> targets)
        {
            if (targets == null || targets.Count == 0) return;

            // GDD: Sát thương mỗi nhịp 0.5s bằng 100% Tấn công
            float damage = Stats != null ? Stats.Attack : 15f;
            foreach (TankBrain tank in targets)
            {
                if (tank == null || !tank.IsAlive) continue;
                DamageInfo damageInfo = new DamageInfo(this, tank, damage, 0f, tank.transform.position);
                tank.TakeDamage(damageInfo);
                TextDamageManager.Instance?.Show(damageInfo.FinalDamage, tank.transform.position);
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
