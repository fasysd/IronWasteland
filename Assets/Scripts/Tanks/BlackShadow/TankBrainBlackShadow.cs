using IronWasteland;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using IronWasteland.Tanks.BlackShadow;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TankBrainBlackShadow : TankBrain
{
    [Header("Normal Skill - Design")]
    [Tooltip("Prefab hoặc mẫu của NormalSkill.")]
    [SerializeField] private NormalSkill normalSkill;

    [Header("Normal Skill - Pool")]
    [Tooltip("Số lượng đạn khởi tạo sẵn trong Pool.")]
    [SerializeField, Min(0)] private int initialPoolSize = 4;

    [Header("Normal Skill - Combat")]
    [Tooltip("Sát thương gây ra khi trúng mục tiêu.")]
    [SerializeField, Min(0f)] private float normalSkillDmg = 1f;

    [Tooltip("Thời gian hồi chiêu của Normal Skill (giây).")]
    [SerializeField, Min(0.01f)] private float normalSkillCooldown = 0.5f;

    [Header("Specialized Skill - Design")]
    [Tooltip("Prefab hoặc mẫu của SpecializedSkill.")]
    [SerializeField] private SpecializedSkill specializedSkill;

    [Header("Specialized Skill - Pool")]
    [Tooltip("Số lượng ảo ảnh khởi tạo sẵn trong Pool.")]
    [SerializeField, Min(0)] private int specializedPoolSize = 2;

    [Header("Specialized Skill - Combat")]
    [Tooltip("Sát thương diện rộng gây ra khi kích nổ ảo ảnh.")]
    [SerializeField, Min(0f)] private float specializedSkillDmg = 50f;

    [Tooltip("Thời gian hồi chiêu của Specialized Skill (giây).")]
    [SerializeField, Min(0.01f)] private float specializedSkillCooldown = 5f;

    [Tooltip("Khoảng trễ tối thiểu giữa lần đặt ảo ảnh và lần bấm đổi chỗ (giây).")]
    [SerializeField, Min(0.05f)] private float recastDelay = 0.2f;

    private const int normalSkillIndex = 0;
    private const int specializedSkillIndex = 1;

    // Pool NormalSkill
    private readonly Stack<NormalSkill> skillPool = new Stack<NormalSkill>();
    private readonly HashSet<NormalSkill> allSkills = new HashSet<NormalSkill>();

    // Pool SpecializedSkill
    private readonly Stack<SpecializedSkill> specializedSkillPool = new Stack<SpecializedSkill>();
    private readonly HashSet<SpecializedSkill> allSpecializedSkills = new HashSet<SpecializedSkill>();
    private SpecializedSkill activeSpecializedSkill;

    private void Start()
    {
        PrewarmNormalSkillPool();
        PrewarmSpecializedSkillPool();
    }

    protected override float GetBaseCooldown(int index)
    {
        if (index == normalSkillIndex)
            return normalSkillCooldown;

        if (index == specializedSkillIndex)
        {
            // Nếu ảo ảnh đang ở State 0 (đặt ảo ảnh) -> lần bấm này là kích nổ / đổi chỗ -> tính full cooldown
            if (activeSpecializedSkill != null && activeSpecializedSkill.SkillState == 0)
                return specializedSkillCooldown;

            // Nếu chưa có ảo ảnh -> lần bấm này là đặt ảo ảnh -> cho phép bấm lần 2 sau recastDelay
            return recastDelay;
        }

        return base.GetBaseCooldown(index);
    }

    protected override void OnUseSkill(int index)
    {
        if (index == normalSkillIndex)
        {
            FireNormalSkill();
            return;
        }

        if (index == specializedSkillIndex)
        {
            UseSpecializedSkill();
            return;
        }

        base.OnUseSkill(index);
    }

    #region Normal Skill Logic
    private void PrewarmNormalSkillPool()
    {
        if (normalSkill == null)
        {
            Debug.LogWarning($"[{nameof(TankBrainBlackShadow)}] Chưa gán prefab/mẫu NormalSkill.", this);
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            NormalSkill skillInstance = CreateNormalSkillInstance();
            if (skillInstance != null)
                skillPool.Push(skillInstance);
        }
    }

    private NormalSkill CreateNormalSkillInstance()
    {
        if (normalSkill == null)
            return null;

        NormalSkill skillInstance = Instantiate(normalSkill, transform.position, Quaternion.identity);
        if (skillInstance == null)
            return null;

        skillInstance.transform.SetParent(null);
        skillInstance.gameObject.SetActive(true);
        skillInstance.Clear();
        skillInstance.gameObject.SetActive(false);

        allSkills.Add(skillInstance);
        return skillInstance;
    }

    private NormalSkill GetNormalSkillFromPool()
    {
        while (skillPool.Count > 0)
        {
            NormalSkill skillInstance = skillPool.Pop();
            if (skillInstance != null)
                return skillInstance;
        }

        return CreateNormalSkillInstance();
    }

    private void ReturnNormalSkillToPool(NormalSkill skillInstance)
    {
        if (skillInstance == null)
            return;

        skillInstance.Clear();
        skillInstance.gameObject.SetActive(false);

        if (!skillPool.Contains(skillInstance))
            skillPool.Push(skillInstance);
    }

    private void FireNormalSkill()
    {
        if (normalSkill == null)
        {
            Debug.LogWarning($"[{nameof(TankBrainBlackShadow)}] Chưa gán NormalSkill.", this);
            return;
        }

        if (View == null || View.Muzzle == null)
        {
            Debug.LogWarning($"[{nameof(TankBrainBlackShadow)}] Không tìm thấy View hoặc Muzzle (nòng súng).", this);
            return;
        }

        if (!HasLookTarget)
            return;

        Transform muzzle = View.Muzzle;

        Vector2 direction = LookTarget - (Vector2)muzzle.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        direction.Normalize();

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

        NormalSkill skillInstance = GetNormalSkillFromPool();
        if (skillInstance == null)
            return;

        skillInstance.transform.SetParent(null);
        skillInstance.transform.SetPositionAndRotation(muzzle.position, rotation);

        skillInstance.Initialize(direction, ReturnNormalSkillToPool);
        skillInstance.TargetFound += OnNormalSkillTargetFound;

        skillInstance.gameObject.SetActive(true);
    }

    private void OnNormalSkillTargetFound(NormalSkill skillInstance, EnemyBrain enemy)
    {
        if (enemy == null)
            return;

        enemy.TakeDamage(normalSkillDmg);
        TextDamageManager.Instance?.Show(normalSkillDmg, skillInstance.transform.position);
    }
    #endregion

    #region Specialized Skill Logic
    private void PrewarmSpecializedSkillPool()
    {
        if (specializedSkill == null)
        {
            Debug.LogWarning($"[{nameof(TankBrainBlackShadow)}] Chưa gán prefab/mẫu SpecializedSkill.", this);
            return;
        }

        for (int i = 0; i < specializedPoolSize; i++)
        {
            SpecializedSkill skillInstance = CreateSpecializedSkillInstance();
            if (skillInstance != null)
                specializedSkillPool.Push(skillInstance);
        }
    }

    private SpecializedSkill CreateSpecializedSkillInstance()
    {
        if (specializedSkill == null)
            return null;

        SpecializedSkill skillInstance = Instantiate(specializedSkill, transform.position, Quaternion.identity);
        if (skillInstance == null)
            return null;

        skillInstance.transform.SetParent(null);
        skillInstance.gameObject.SetActive(true);
        skillInstance.Clear();
        skillInstance.gameObject.SetActive(false);

        allSpecializedSkills.Add(skillInstance);
        return skillInstance;
    }

    private SpecializedSkill GetSpecializedSkillFromPool()
    {
        while (specializedSkillPool.Count > 0)
        {
            SpecializedSkill skillInstance = specializedSkillPool.Pop();
            if (skillInstance != null)
                return skillInstance;
        }

        return CreateSpecializedSkillInstance();
    }

    private void ReturnSpecializedSkillToPool(SpecializedSkill skillInstance)
    {
        if (skillInstance == null)
            return;

        if (activeSpecializedSkill == skillInstance)
        {
            activeSpecializedSkill = null;
        }

        skillInstance.Clear();
        skillInstance.gameObject.SetActive(false);

        if (!specializedSkillPool.Contains(skillInstance))
            specializedSkillPool.Push(skillInstance);
    }

    private void UseSpecializedSkill()
    {
        if (specializedSkill == null)
        {
            Debug.LogWarning($"[{nameof(TankBrainBlackShadow)}] Chưa gán SpecializedSkill.", this);
            return;
        }

        // GIAI ĐOẠN 2: Ảo ảnh đang ở State 0 (chờ đổi chỗ) -> Đổi vị trí và kích nổ
        if (activeSpecializedSkill != null && activeSpecializedSkill.SkillState == 0)
        {
            Vector2 targetPosition = activeSpecializedSkill.CopyPosition;

            // Đổi vị trí xe tăng tới vị trí ảo ảnh
            transform.position = targetPosition;

            // Kích nổ ảo ảnh
            SpecializedSkill explodingSkill = activeSpecializedSkill;
            activeSpecializedSkill = null;
            explodingSkill.Explode();
            return;
        }

        // GIAI ĐOẠN 1: Chưa có ảo ảnh -> Đặt ảo ảnh tại vị trí chuột (LookTarget)
        if (!HasLookTarget)
            return;

        SpecializedSkill skillInstance = GetSpecializedSkillFromPool();
        if (skillInstance == null)
            return;

        Vector2 spawnPosition = LookTarget;
        skillInstance.transform.SetParent(null);
        skillInstance.transform.position = spawnPosition;

        skillInstance.Initialize(spawnPosition, ReturnSpecializedSkillToPool);
        skillInstance.TargetFound += OnSpecializedSkillTargetFound;

        skillInstance.gameObject.SetActive(true);
        activeSpecializedSkill = skillInstance;
    }

    private void OnSpecializedSkillTargetFound(SpecializedSkill skillInstance, EnemyBrain enemy)
    {
        if (enemy == null)
            return;

        enemy.TakeDamage(specializedSkillDmg);
        TextDamageManager.Instance?.Show(specializedSkillDmg, enemy.transform.position);
    }
    #endregion

    private void OnDestroy()
    {
        foreach (NormalSkill skillInstance in allSkills)
        {
            if (skillInstance != null)
                Destroy(skillInstance.gameObject);
        }
        allSkills.Clear();
        skillPool.Clear();

        foreach (SpecializedSkill skillInstance in allSpecializedSkills)
        {
            if (skillInstance != null)
                Destroy(skillInstance.gameObject);
        }
        allSpecializedSkills.Clear();
        specializedSkillPool.Clear();
    }
}
