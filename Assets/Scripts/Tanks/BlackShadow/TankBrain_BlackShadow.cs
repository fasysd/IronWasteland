
using IronWasteland.Enemies;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.BlackShadow
{
    [DisallowMultipleComponent]
    public class TankBrain_BlackShadow : TankBrain
    {
        [Header("Skill Test 1 - Design")]
        [Tooltip("GameObject mẫu inactive, nằm sẵn trong TankBrain.")]
        [SerializeField] private Skill_Test1 skillTest1Design;

        [Header("Skill Test 1 - Pool")]
        [SerializeField, Min(0)] private int initialPoolSize = 4;

        [Header("Skill Test 1 - Combat")]
        [SerializeField, Min(0f)] private float skillTest1Damage = 36f;

        private const int SkillTest1Index = 0;
        private const float SkillTest1Cooldown = 1f;

        private readonly Stack<Skill_Test1> m_SkillPool =
            new Stack<Skill_Test1>();

        private readonly HashSet<Skill_Test1> m_AllSkills =
            new HashSet<Skill_Test1>();

        protected override float GetBaseCooldown(int index)
        {
            if (index == SkillTest1Index)
                return SkillTest1Cooldown;

            return base.GetBaseCooldown(index);
        }

        private void Start()
        {
            PrewarmSkillPool();
        }

        protected override void OnUseSkill(int index)
        {
            if (index != SkillTest1Index)
            {
                base.OnUseSkill(index);
                return;
            }

            FireSkillTest1();
        }

        private void PrewarmSkillPool()
        {
            if (skillTest1Design == null)
            {
                Debug.LogWarning(
                    $"{nameof(TankBrain_BlackShadow)}: " +
                    "Chưa gán Skill_Test1 Design.",
                    this);
                return;
            }

            for (int i = 0; i < initialPoolSize; i++)
            {
                Skill_Test1 skill = CreateSkillInstance();

                if (skill != null)
                    m_SkillPool.Push(skill);
            }
        }

        private Skill_Test1 CreateSkillInstance()
        {
            if (skillTest1Design == null)
                return null;

            Skill_Test1 skill = Instantiate(
                skillTest1Design,
                transform.position,
                Quaternion.identity);

            if (skill == null)
                return null;

            // Tách khỏi TankBrain để đạn di chuyển độc lập.
            skill.transform.SetParent(null);

            // Đánh thức component và cache các component cần thiết.
            skill.gameObject.SetActive(true);

            // Đưa về trạng thái sạch trước khi đưa vào pool.
            skill.Clear();
            skill.gameObject.SetActive(false);

            m_AllSkills.Add(skill);
            return skill;
        }

        private Skill_Test1 GetSkillFromPool()
        {
            while (m_SkillPool.Count > 0)
            {
                Skill_Test1 skill = m_SkillPool.Pop();

                if (skill != null)
                    return skill;

                // Bỏ qua tham chiếu đã bị Destroy.
            }

            // Pool hết đạn rảnh: tạo thêm một instance.
            return CreateSkillInstance();
        }

        private void ReturnSkillToPool(Skill_Test1 skill)
        {
            if (skill == null)
                return;

            // Clear lần nữa để bảo đảm trạng thái sạch.
            skill.Clear();
            skill.gameObject.SetActive(false);

            if (!m_SkillPool.Contains(skill))
                m_SkillPool.Push(skill);
        }

        private void FireSkillTest1()
        {
            if (skillTest1Design == null)
            {
                Debug.LogWarning("Chưa gán Skill_Test1 Design.", this);
                return;
            }

            if (View == null || View.Muzzle == null)
            {
                Debug.LogWarning("Không tìm thấy Muzzle của Tank.", this);
                return;
            }

            if (!HasLookTarget)
                return;

            Transform muzzle = View.Muzzle;

            // Hướng bắn = vị trí Look - vị trí Muzzle.
            Vector2 direction =
                LookTarget - (Vector2)muzzle.position;

            if (direction.sqrMagnitude <= 0.0001f)
                return;

            direction.Normalize();

            float angle = Mathf.Atan2(
                direction.y,
                direction.x) * Mathf.Rad2Deg;

            Quaternion rotation =
                Quaternion.Euler(0f, 0f, angle);

            Skill_Test1 skill = GetSkillFromPool();

            if (skill == null)
                return;

            skill.transform.SetParent(null);
            skill.transform.SetPositionAndRotation(
                muzzle.position,
                rotation);

            skill.Initialize(direction, ReturnSkillToPool);
            skill.TargetFound += OnSkillTest1TargetFound;

            skill.gameObject.SetActive(true);
        }

        private void OnSkillTest1TargetFound(Skill_Test1 skill, EnemyBrain enemy)
        {
            if (enemy == null)
                return;

            enemy.TakeDamage(skillTest1Damage);
            TextDamageManager.Instance?.Show(skillTest1Damage, skill.transform.position);
        }

        private void OnDestroy()
        {
            foreach (Skill_Test1 skill in m_AllSkills)
            {
                if (skill != null)
                    Destroy(skill.gameObject);
            }

            m_AllSkills.Clear();
            m_SkillPool.Clear();
        }
    }
}