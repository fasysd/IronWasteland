
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
        [SerializeField, Min(0.01f)] private float skillTest1Cooldown = 5f;

        private const int SkillTest1Index = 0;

        private readonly Stack<Skill_Test1> m_SkillPool =
            new Stack<Skill_Test1>();

        private readonly HashSet<Skill_Test1> m_AllSkills =
            new HashSet<Skill_Test1>();

        [Header("Skill Test 2 - Design")]
        [SerializeField] private Skill_Test2 skillTest2Design;

        [Header("Skill Test 2 - Pool")]
        [SerializeField, Min(0)] private int skillTest2InitialPoolSize = 2;

        [Header("Skill Test 2 - Combat")]
        [SerializeField, Min(0f)] private float skillTest2Damage = 10f;
        [SerializeField, Min(0.01f)] private float skillTest2Cooldown = 5f;

        private const int SkillTest2Index = 1;

        private readonly Stack<Skill_Test2> m_SkillTest2Pool =
            new Stack<Skill_Test2>();

        private readonly HashSet<Skill_Test2> m_AllSkillTest2 =
            new HashSet<Skill_Test2>();

        protected override float GetBaseCooldown(int index)
        {
            if (index == SkillTest1Index)
                return skillTest1Cooldown;

            if (index == SkillTest2Index)
                return skillTest2Cooldown;

            return base.GetBaseCooldown(index);
        }

        private void Start()
        {
            PrewarmSkillPool();
            PrewarmSkillTest2Pool();
        }

        protected override void OnUseSkill(int index)
        {
            if (index == SkillTest1Index)
            {
                FireSkillTest1();
                return;
            }

            if (index == SkillTest2Index)
            {
                FireSkillTest2();
                return;
            }

            base.OnUseSkill(index);
        }

        #region Skill 1
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
        #endregion

        #region Skill 2
        private void PrewarmSkillTest2Pool()
        {
            if (skillTest2Design == null)
            {
                Debug.LogWarning("Chưa gán Skill_Test2 Design.", this);
                return;
            }

            for (int i = 0; i < skillTest2InitialPoolSize; i++)
            {
                Skill_Test2 skill = CreateSkillTest2Instance();

                if (skill != null)
                    m_SkillTest2Pool.Push(skill);
            }
        }

        private Skill_Test2 CreateSkillTest2Instance()
        {
            if (skillTest2Design == null)
                return null;

            Skill_Test2 skill = Instantiate(
                skillTest2Design,
                transform.position,
                Quaternion.identity);

            skill.transform.SetParent(null);
            skill.gameObject.SetActive(true);
            skill.Clear();
            skill.gameObject.SetActive(false);

            m_AllSkillTest2.Add(skill);
            return skill;
        }

        private Skill_Test2 GetSkillTest2FromPool()
        {
            while (m_SkillTest2Pool.Count > 0)
            {
                Skill_Test2 skill = m_SkillTest2Pool.Pop();

                if (skill != null)
                    return skill;
            }

            return CreateSkillTest2Instance();
        }

        private void ReturnSkillTest2ToPool(Skill_Test2 skill)
        {
            if (skill == null)
                return;

            skill.Clear();
            skill.gameObject.SetActive(false);

            if (!m_SkillTest2Pool.Contains(skill))
                m_SkillTest2Pool.Push(skill);
        }

        private void FireSkillTest2()
        {
            if (skillTest2Design == null)
                return;

            if (!HasLookTarget)
                return;

            Skill_Test2 skill = GetSkillTest2FromPool();

            if (skill == null)
                return;

            // Vị trí Look là tâm vùng sát thương.
            Vector2 spawnPosition = LookTarget;

            skill.transform.SetParent(null);
            skill.transform.position = spawnPosition;
            skill.gameObject.SetActive(true);

            skill.Initialize(spawnPosition, ReturnSkillTest2ToPool);
            skill.TargetFound += OnSkillTest2TargetFound;

            // Khởi động vòng lặp sau khi đăng ký event.
            skill.StartSkill();
        }

        private void OnSkillTest2TargetFound(
            Skill_Test2 skill,
            EnemyBrain enemy)
        {
            if (enemy == null)
                return;

            enemy.TakeDamage(skillTest2Damage);

            Collider2D enemyCollider = enemy.Model.BodyCollider;

            if (Collider2DUtility.TryGetRandomOverlapPoint(skill.ColliderRef.Collider, enemyCollider, out Vector2 center))
                TextDamageManager.Instance?.Show(skillTest2Damage, center);
            else
                TextDamageManager.Instance?.Show(skillTest2Damage, skill.transform.position);

        }

        #endregion

        private void OnDestroy()
        {
            foreach (Skill_Test1 skill in m_AllSkills)
            {
                if (skill != null)
                    Destroy(skill.gameObject);
            }

            m_AllSkills.Clear();
            m_SkillPool.Clear();

            foreach (Skill_Test2 skill in m_AllSkillTest2)
            {
                if (skill != null)
                    Destroy(skill.gameObject);
            }

            m_AllSkillTest2.Clear();
            m_SkillTest2Pool.Clear();
        }
    }
}