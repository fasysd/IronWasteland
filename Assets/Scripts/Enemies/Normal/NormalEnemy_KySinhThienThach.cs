using System.Collections;
using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Ký Sinh Thiên Thạch - Kẻ địch tự nổ khi tiếp cận mục tiêu trong tầm A hoặc khi bị tiêu diệt.
    /// Gây sát thương diện rộng và làm chậm tốc độ di chuyển của Tank.
    /// </summary>
    [DisallowMultipleComponent]
    public class NormalEnemy_KySinhThienThach : NormalEnemy_BaseChaser
    {
        [Header("Skill Settings")]
        [SerializeField] private Skill_KySinhThienThach skill;

        [Tooltip("Tỷ lệ giảm tốc độ di chuyển của Tank (0.5 = giảm 50%).")]
        [SerializeField, Range(0.1f, 0.9f)] private float slowPercent = 0.5f;

        [Tooltip("Thời gian hiệu lực của hiệu ứng làm chậm (giây).")]
        [SerializeField, Min(0.5f)] private float slowDuration = 3f;

        [Header("Runtime State")]
        [SerializeField, ReadOnly] private bool isCastingSkill = false;
        [SerializeField, ReadOnly] private bool isInvulnerable = false;
        [SerializeField, ReadOnly] private bool hasDetonated = false;

        private readonly List<TankBrain> m_AffectedTanks = new List<TankBrain>();
        private string m_SlowKey;

        public Skill_KySinhThienThach Skill => skill;
        public float SlowPercent => slowPercent;
        public float SlowDuration => slowDuration;
        public bool IsCastingSkill => isCastingSkill;
        public bool IsInvulnerable => isInvulnerable;
        public bool HasDetonated => hasDetonated;

        protected override void Awake()
        {
            base.Awake();

            m_SlowKey = "KySinhThienThach_Slow_" + System.Guid.NewGuid().ToString("N");

            if (skill == null)
            {
                skill = GetComponentInChildren<Skill_KySinhThienThach>(true);
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

            // Dọn dẹp an toàn nếu bị Destroy đột ngột trước khi hết thời gian làm chậm
            ClearSlowDebuffs();
        }

        protected override void OnBrainTick(float deltaTime)
        {
            // Khi đang thi triển hoặc đã nổ: dừng toàn bộ di chuyển và không tìm đường nữa
            if (isCastingSkill || hasDetonated)
            {
                movementAgent?.Stop();
                return;
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

                // Khi mục tiêu ở trong phạm vi A: dừng lại ngay lập tức và thi triển kỹ năng
                if (distToTarget <= radiusA)
                {
                    movementAgent?.Stop();
                    StartDetonation();
                    return;
                }

                // Nếu chưa vào tầm A: chạy logic bám đuổi thông thường
                HandleTargetTracking(currentPos, currentTarget.position);

                // Nếu sau khi di chuyển đã vào trong tầm A: kích hoạt thi triển ngay
                if (distToTarget <= radiusA)
                {
                    movementAgent?.Stop();
                    StartDetonation();
                }
            }
            else
            {
                HandleReturnHomeLogic(currentPos, distToHome, deltaTime);
            }
        }

        public override void TakeDamage(float amount)
        {
            // Khi bắt đầu thi triển kỹ năng cho đến lúc chết thật sự: không nhận sát thương
            if (isInvulnerable || isCastingSkill || hasDetonated || !IsAlive)
            {
                return;
            }

            base.TakeDamage(amount);
        }

        protected override void OnDeath()
        {
            // Nếu máu <= 0 nhưng chưa thi triển kỹ năng: đứng yên tại chỗ và thi triển kỹ năng
            if (!hasDetonated && !isCastingSkill)
            {
                StartDetonation();
                return;
            }

            // Nếu đang trong quá trình thi triển hoặc đang đếm ngược gỡ debuff thì không hủy ngay
            if (isCastingSkill || hasDetonated)
            {
                return;
            }

            base.OnDeath();
        }

        public void StartDetonation()
        {
            if (isCastingSkill || hasDetonated)
            {
                return;
            }

            isCastingSkill = true;
            isInvulnerable = true;

            // 1. Dừng di chuyển và tắt EnemyMovementAgent để hủy trọng số trên PathfindingGrid2D
            if (movementAgent != null)
            {
                movementAgent.Stop();
                movementAgent.enabled = false;
            }

            // 2. Tắt Collider Body của Model để không va chạm vật lý và không nhận đạn
            if (Model != null && Model.BodyCollider != null)
            {
                Model.BodyCollider.enabled = false;
            }

            // 3. Thi triển kỹ năng
            if (skill != null)
            {
                skill.Cast(OnSkillFinished);
            }
            else
            {
                // Fallback nếu chưa gắn Skill component
                HandleSkillTargetFound(null, new List<TankBrain>());
            }
        }

        private void OnSkillFinished()
        {
            isCastingSkill = false;
        }

        private void HandleSkillTargetFound(Skill_KySinhThienThach sender, IReadOnlyList<TankBrain> targets)
        {
            hasDetonated = true;

            float damage = Stats != null ? Stats.Attack : 10f;

            m_AffectedTanks.Clear();
            if (targets != null)
            {
                foreach (TankBrain tank in targets)
                {
                    if (tank == null || !tank.IsAlive) continue;

                    // 1. Gây sát thương 100% tấn công
                    tank.TakeDamage(damage);
                    TextDamageManager.Instance?.Show(damage, tank.transform.position);

                    // 2. Làm chậm tốc độ di chuyển của Tank
                    float slowAmount = tank.BaseStats.MoveSpeed * slowPercent;
                    tank.AddBonusStats(m_SlowKey, new TankStats { MoveSpeed = -slowAmount });
                    m_AffectedTanks.Add(tank);
                }
            }

            // 3. Ẩn EnemyModel ngay lập tức để không còn hiển thị trên GameScene
            if (Model != null)
            {
                Model.gameObject.SetActive(false);
            }

            // 4. Quản lý thời gian làm chậm trên chính EnemyBrain, sau đó mới hủy GameObject
            float waitTime = m_AffectedTanks.Count > 0 ? slowDuration : 0.2f;
            StartCoroutine(SlowDurationAndCleanupRoutine(waitTime));
        }

        private IEnumerator SlowDurationAndCleanupRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);

            ClearSlowDebuffs();
            Destroy(gameObject);
        }

        private void ClearSlowDebuffs()
        {
            if (m_AffectedTanks.Count == 0) return;

            foreach (TankBrain tank in m_AffectedTanks)
            {
                if (tank != null)
                {
                    tank.RemoveBonusStats(m_SlowKey);
                }
            }
            m_AffectedTanks.Clear();
        }
    }
}
