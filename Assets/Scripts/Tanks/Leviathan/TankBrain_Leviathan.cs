using IronWasteland;
using IronWasteland.Enemies;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.Leviathan
{
    /// <summary>
    /// TankBrain cụ thể cho xe tăng Leviathan - kế thừa từ <see cref="TankBrain"/>.
    /// Kỹ năng thường: Water Pulse (Sóng Xung Kích Nước).
    /// Kỹ năng chuyên biệt: Water Bubble (Bong Bóng Nước) - nhốt kẻ địch lơ lửng rồi phát nổ gây sát thương và đẩy lùi xung quanh.
    /// </summary>
    [DisallowMultipleComponent]
    public class TankBrain_Leviathan : TankBrain
    {
        [Header("Normal Skill - Design")]
        [Tooltip("Prefab hoặc mẫu của NormalSkill_Leviathan.")]
        [SerializeField] private NormalSkill_Leviathan normalSkill;

        [Header("Normal Skill - Pool")]
        [Tooltip("Số lượng đạn khởi tạo sẵn trong Pool.")]
        [SerializeField, Min(0)] private int initialPoolSize = 6;

        [Header("Normal Skill - Combat")]
        [Tooltip("Sát thương gây ra khi sóng nước trúng mục tiêu.")]
        [SerializeField, Min(0f)] private float normalSkillDmg = 45f;

        [Tooltip("Thời gian hồi chiêu của Normal Skill (giây).")]
        [SerializeField, Min(0.01f)] private float normalSkillCooldown = 0.5f;

        [Tooltip("Lượng năng lượng tích lũy cho xe tăng mỗi khi đánh trúng một kẻ địch.")]
        [SerializeField, Min(0f)] private float energyGainPerHit = 8f;

        [Header("Specialized Skill - Design")]
        [Tooltip("Prefab hoặc mẫu của SpecializedSkill_Leviathan (Bong bóng nước).")]
        [SerializeField] private SpecializedSkill_Leviathan specializedSkill;

        [Header("Specialized Skill - Pool")]
        [Tooltip("Số lượng bong bóng nước khởi tạo sẵn trong Pool.")]
        [SerializeField, Min(0)] private int specializedPoolSize = 2;

        [Header("Specialized Skill - Combat")]
        [Tooltip("Sát thương diện rộng khi bong bóng nước phát nổ.")]
        [SerializeField, Min(0f)] private float specializedSkillDmg = 70f;

        [Tooltip("Thời gian hồi chiêu của Specialized Skill (giây).")]
        [SerializeField, Min(0.01f)] private float specializedSkillCooldown = 8f;

        [Tooltip("Lượng năng lượng tiêu hao khi bắn bong bóng nước.")]
        [SerializeField, Min(0f)] private float specializedSkillEnergyCost = 25f;

        [Header("Optional Skill 1 - Design")]
        [Tooltip("Kỹ năng tùy chọn 1 - Biến thể 1: Water Armor (Giáp Nước).")]
        [SerializeField] private OptionalSkill1_Leviathan optionalSkill1;

        [Tooltip("Kỹ năng tùy chọn 1 - Biến thể 2: Guardian Current (Dòng Chảy Hộ Vệ).")]
        [SerializeField] private OptionalSkill1_2_Leviathan optionalSkill1_2;

        [Tooltip("Kỹ năng tùy chọn 1 - Biến thể 3: Regenerative Shell (Kén Nước Trị Liệu).")]
        [SerializeField] private OptionalSkill1_3_Leviathan optionalSkill1_3;

        [Header("Optional Skill 1 (Water Armor) - Combat & Scaling")]
        [Tooltip("Sát thương diện rộng cơ bản khi màng nước phát nổ.")]
        [SerializeField, Min(0f)] private float optionalSkill1ExplosionDmg = 55f;

        [Tooltip("Phần trăm phòng thủ tăng thêm khi màng nước bao phủ (0.4 = giảm 40% sát thương nhận vào).")]
        [SerializeField, Range(0f, 1f)] private float optionalSkill1BonusDefense = 0.4f;

        [Tooltip("Phần trăm sát thương gây ra bị giảm trong khi giáp duy trì (0.2 = giảm 20% sát thương gây ra).")]
        [SerializeField, Range(0f, 1f)] private float optionalSkill1DamagePenalty = 0.2f;

        [Tooltip("Thời gian duy trì của giáp nước trước khi tự phát nổ (giây).")]
        [SerializeField, Min(0.5f)] private float optionalSkill1Duration = 5f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 1 (giây).")]
        [SerializeField, Min(0.01f)] private float optionalSkill1Cooldown = 8f;

        [Header("Optional Skill 1_2 (Guardian Current) - Combat & Scaling")]
        [Tooltip("Lượng máu hồi phục mỗi giây cho xe tăng đồng minh.")]
        [SerializeField, Min(0f)] private float optionalSkill1_2HealPerSecond = 15f;

        [Tooltip("Tỷ lệ sát thương của đồng minh chuyển sang cho Leviathan gánh thay (0.35 = gánh 35% sát thương).")]
        [SerializeField, Range(0f, 1f)] private float optionalSkill1_2DamageShareRatio = 0.35f;

        [Tooltip("Thời gian duy trì tối đa của dòng chảy liên kết (giây).")]
        [SerializeField, Min(0.5f)] private float optionalSkill1_2Duration = 8f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 1_2 (giây).")]
        [SerializeField, Min(0.01f)] private float optionalSkill1_2Cooldown = 8f;

        [Header("Optional Skill 1_3 (Regenerative Shell) - Combat & Scaling")]
        [Tooltip("Lượng máu hồi phục cơ bản mỗi giây trong thời gian kén duy trì.")]
        [SerializeField, Min(0f)] private float optionalSkill1_3BaseHealPerSecond = 20f;

        [Tooltip("Lượng máu hồi phục thưởng thêm cực lớn khi kén hoàn tất mà không nhận bất kỳ sát thương nào.")]
        [SerializeField, Min(0f)] private float optionalSkill1_3BonusCompletionHeal = 150f;

        [Tooltip("Thời gian duy trì của kén nước trị liệu (giây).")]
        [SerializeField, Min(0.5f)] private float optionalSkill1_3Duration = 5f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 1_3 (giây).")]
        [SerializeField, Min(0.01f)] private float optionalSkill1_3Cooldown = 10f;

        [Header("Optional Skill 2 - Design")]
        [Tooltip("Kỹ năng tùy chọn 2 - Biến thể 1: Tidal Return (Thủy Triều Nghịch Chuyển).")]
        [SerializeField] private OptionalSkill2_Leviathan optionalSkill2;

        [Tooltip("Kỹ năng tùy chọn 2 - Biến thể 2: Leviathan Form (Dạng Thủy Quái).")]
        [SerializeField] private OptionalSkill2_1_Leviathan optionalSkill2_1;

        [Tooltip("Kỹ năng tùy chọn 2 - Biến thể 3: Tide Commander (Chỉ Huy Thủy Triều).")]
        [SerializeField] private OptionalSkill2_2_Leviathan optionalSkill2_2;

        [Header("Optional Skill 2 (Tidal Return) - Combat & Scaling")]
        [Tooltip("Sát thương diện rộng cơ bản khi kéo kẻ địch về tâm xoáy.")]
        [SerializeField, Min(0f)] private float optionalSkill2Dmg = 65f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 2 (giây).")]
        [SerializeField, Min(0.01f)] private float optionalSkill2Cooldown = 10f;

        [Header("Optional Skill 2_1 (Leviathan Form) - Combat & Scaling")]
        [Tooltip("Thời gian duy trì dạng thủy quái (giây).")]
        [SerializeField, Min(1.0f)] private float optionalSkill2_1Duration = 8f;

        [Tooltip("Thời gian miễn nhiễm sát thương khi chuyển dạng (giây).")]
        [SerializeField, Min(0.1f)] private float optionalSkill2_1InvulnerabilityDuration = 1.2f;

        [Tooltip("Lượng máu tối đa cộng thêm khi ở dạng thủy quái.")]
        [SerializeField, Min(0f)] private float optionalSkill2_1BonusMaxHealth = 300f;

        [Tooltip("Lượng máu hồi phục mỗi giây trong suốt thời gian biến dạng.")]
        [SerializeField, Min(0f)] private float optionalSkill2_1HealPerSecond = 20f;

        [Tooltip("Phần trăm sát thương của đòn đánh thường bị giảm khi ở dạng thủy quái (0.25 = giảm 25% sát thương đòn đánh thường).")]
        [SerializeField, Range(0f, 1f)] private float optionalSkill2_1DamagePenalty = 0.25f;

        [Tooltip("Phần trăm thời gian hồi chiêu của đòn đánh thường bị tăng thêm khi ở dạng thủy quái (0.2 = tăng 20% hồi chiêu đòn đánh thường).")]
        [SerializeField, Range(0f, 1f)] private float optionalSkill2_1CooldownPenalty = 0.2f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 2_1 (giây).")]
        [SerializeField, Min(0.01f)] private float optionalSkill2_1Cooldown = 15f;

        [Header("Optional Skill 2_2 (Tide Commander) - Combat & Scaling")]
        [Tooltip("Thời gian duy trì của vùng thủy triều (giây).")]
        [SerializeField, Min(1.0f)] private float optionalSkill2_2Duration = 8f;

        [Tooltip("Tốc độ di chuyển cộng thêm cho đồng minh khi đứng trong vùng (đơn vị/giây).")]
        [SerializeField, Min(0f)] private float optionalSkill2_2BonusMoveSpeed = 2.0f;

        [Tooltip("Lượng sát thương tối đa lớp giáp nước có thể hấp thụ cho mỗi đồng minh.")]
        [SerializeField, Min(0f)] private float optionalSkill2_2ShieldCapacity = 120f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 2_2 (giây).")]
        [SerializeField, Min(0.01f)] private float optionalSkill2_2Cooldown = 12f;

        private const int normalSkillIndex = 0;
        private const int specializedSkillIndex = 1;
        private const int optionalSkill1Index = 2;
        private const int optionalSkill2Index = 3;

        // Quản lý Object Pool cho NormalSkill
        private readonly Stack<NormalSkill_Leviathan> skillPool = new Stack<NormalSkill_Leviathan>();
        private readonly HashSet<NormalSkill_Leviathan> allSkills = new HashSet<NormalSkill_Leviathan>();

        // Quản lý Object Pool cho SpecializedSkill
        private readonly Stack<SpecializedSkill_Leviathan> specializedSkillPool = new Stack<SpecializedSkill_Leviathan>();
        private readonly HashSet<SpecializedSkill_Leviathan> allSpecializedSkills = new HashSet<SpecializedSkill_Leviathan>();

        private void Start()
        {
            PrewarmNormalSkillPool();
            PrewarmSpecializedSkillPool();
        }

        protected override float GetBaseCooldown(int index)
        {
            if (index == normalSkillIndex)
            {
                float cd = normalSkillCooldown;
                if (optionalSkill2_1 != null && optionalSkill2_1.IsActive)
                {
                    cd *= (1f + optionalSkill2_1.CooldownPenalty);
                }
                return cd;
            }

            if (index == specializedSkillIndex)
            {
                return specializedSkillCooldown;
            }

            if (index == optionalSkill1Index)
            {
                if (Context.OptionSkill1 == 1)
                {
                    return optionalSkill1_2Cooldown;
                }
                if (Context.OptionSkill1 == 2)
                {
                    return optionalSkill1_3Cooldown;
                }
                return optionalSkill1Cooldown;
            }

            if (index == optionalSkill2Index)
            {
                if (Context.OptionSkill2 == 1)
                {
                    return optionalSkill2_1Cooldown;
                }
                if (Context.OptionSkill2 == 2)
                {
                    return optionalSkill2_2Cooldown;
                }
                return optionalSkill2Cooldown;
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

            if (index == optionalSkill1Index)
            {
                UseOptionalSkill1();
                return;
            }

            if (index == optionalSkill2Index)
            {
                UseOptionalSkill2();
                return;
            }

            base.OnUseSkill(index);
        }

        #region Optional Skill 1 Logic
        private void UseOptionalSkill1()
        {
            if (Context.OptionSkill1 == 1)
            {
                if (optionalSkill1_2 != null)
                {
                    bool isLinked = optionalSkill1_2.Initialize(
                        this,
                        LookTarget,
                        optionalSkill1_2HealPerSecond,
                        optionalSkill1_2DamageShareRatio,
                        optionalSkill1_2Duration);

                    if (!isLinked)
                    {
                        // Không tìm thấy xe đồng minh hợp lệ dưới con trỏ chuột -> hoàn lại hồi chiêu
                        ReduceSkillCooldown(optionalSkill1Index, GetCooldown(optionalSkill1Index));
                    }
                }
                else
                {
                    Debug.LogWarning($"[{nameof(TankBrain_Leviathan)}] Chưa gán OptionalSkill1_2_Leviathan.", this);
                }
                return;
            }

            if (Context.OptionSkill1 == 2)
            {
                if (optionalSkill1_3 != null)
                {
                    optionalSkill1_3.gameObject.SetActive(true);
                    optionalSkill1_3.Initialize(
                        this,
                        optionalSkill1_3BaseHealPerSecond,
                        optionalSkill1_3BonusCompletionHeal,
                        optionalSkill1_3Duration);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(TankBrain_Leviathan)}] Chưa gán OptionalSkill1_3_Leviathan.", this);
                }
                return;
            }

            // Biến thể mặc định (OptionSkill1 == 0): Water Armor
            if (optionalSkill1 == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_Leviathan)}] Chưa gán OptionalSkill1_Leviathan.", this);
                return;
            }

            optionalSkill1.gameObject.SetActive(true);
            float finalExplosionDmg = optionalSkill1ExplosionDmg * Stats.DamageMultiplier;
            optionalSkill1.Initialize(
                this,
                finalExplosionDmg,
                optionalSkill1BonusDefense,
                optionalSkill1DamagePenalty,
                optionalSkill1Duration);
        }
        #endregion

        #region Optional Skill 2 Logic
        private void UseOptionalSkill2()
        {
            if (Context.OptionSkill2 == 1)
            {
                if (optionalSkill2_1 != null)
                {
                    optionalSkill2_1.gameObject.SetActive(true);
                    optionalSkill2_1.Initialize(
                        this,
                        optionalSkill2_1Duration,
                        optionalSkill2_1InvulnerabilityDuration,
                        optionalSkill2_1BonusMaxHealth,
                        optionalSkill2_1HealPerSecond,
                        optionalSkill2_1DamagePenalty,
                        optionalSkill2_1CooldownPenalty);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(TankBrain_Leviathan)}] Chưa gán OptionalSkill2_1_Leviathan.", this);
                }
                return;
            }

            if (Context.OptionSkill2 == 2)
            {
                if (optionalSkill2_2 != null)
                {
                    optionalSkill2_2.gameObject.SetActive(true);
                    optionalSkill2_2.Initialize(
                        this,
                        optionalSkill2_2Duration,
                        optionalSkill2_2BonusMoveSpeed,
                        optionalSkill2_2ShieldCapacity);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(TankBrain_Leviathan)}] Chưa gán OptionalSkill2_2_Leviathan.", this);
                }
                return;
            }

            if (optionalSkill2 == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_Leviathan)}] Chưa gán OptionalSkill2_Leviathan.", this);
                return;
            }

            Vector2 targetPos = HasLookTarget ? LookTarget : (Vector2)transform.position;
            float finalDmg = optionalSkill2Dmg * Stats.DamageMultiplier;
            optionalSkill2.Initialize(this, targetPos, finalDmg);
        }
        #endregion

        #region Normal Skill Logic & Object Pool
        private void PrewarmNormalSkillPool()
        {
            if (normalSkill == null)
            {
                return;
            }

            for (int i = 0; i < initialPoolSize; i++)
            {
                NormalSkill_Leviathan skillInstance = CreateNormalSkillInstance();
                if (skillInstance != null)
                {
                    skillPool.Push(skillInstance);
                }
            }
        }

        private NormalSkill_Leviathan CreateNormalSkillInstance()
        {
            if (normalSkill == null)
                return null;

            NormalSkill_Leviathan skillInstance = Instantiate(normalSkill, transform.position, Quaternion.identity);
            if (skillInstance == null)
                return null;

            skillInstance.transform.SetParent(null);
            skillInstance.gameObject.SetActive(true);
            skillInstance.Clear();
            skillInstance.gameObject.SetActive(false);

            allSkills.Add(skillInstance);
            return skillInstance;
        }

        private NormalSkill_Leviathan GetNormalSkillFromPool()
        {
            while (skillPool.Count > 0)
            {
                NormalSkill_Leviathan skillInstance = skillPool.Pop();
                if (skillInstance != null)
                    return skillInstance;
            }

            return CreateNormalSkillInstance();
        }

        private void ReturnNormalSkillToPool(NormalSkill_Leviathan skillInstance)
        {
            if (skillInstance == null)
                return;

            skillInstance.Clear();
            skillInstance.gameObject.SetActive(false);

            if (!skillPool.Contains(skillInstance))
            {
                skillPool.Push(skillInstance);
            }
        }

        private void FireNormalSkill()
        {
            if (normalSkill == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_Leviathan)}] Chưa gán NormalSkill_Leviathan.", this);
                return;
            }

            if (View == null || View.Muzzle == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_Leviathan)}] Không tìm thấy View hoặc Muzzle (nòng súng).", this);
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

            NormalSkill_Leviathan skillInstance = GetNormalSkillFromPool();
            if (skillInstance == null)
                return;

            skillInstance.transform.SetParent(null);
            skillInstance.transform.SetPositionAndRotation(muzzle.position, rotation);

            skillInstance.gameObject.SetActive(true);
            skillInstance.Initialize(direction, ReturnNormalSkillToPool);
            skillInstance.TargetFound += OnNormalSkillTargetFound;
        }

        private void OnNormalSkillTargetFound(NormalSkill_Leviathan skillInstance, EnemyBrain enemy)
        {
            if (enemy == null)
                return;

            // Tính hệ số sát thương (kèm hình phạt giảm sát thương nếu đang ở dạng Leviathan Form)
            float multiplier = Stats.DamageMultiplier;
            if (optionalSkill2_1 != null && optionalSkill2_1.IsActive)
            {
                multiplier *= Mathf.Max(0f, 1f - optionalSkill2_1.DamagePenalty);
            }

            // Gây sát thương lên kẻ địch
            float finalDmg = normalSkillDmg * multiplier;
            enemy.TakeDamage(finalDmg);
            TextDamageManager.Instance?.Show(finalDmg, enemy.transform.position);

            // Tích lũy năng lượng cho xe tăng khi đánh trúng
            if (energyGainPerHit > 0f)
            {
                GainEnergy(energyGainPerHit);
            }
        }
        #endregion

        #region Specialized Skill Logic & Object Pool
        private void PrewarmSpecializedSkillPool()
        {
            if (specializedSkill == null)
                return;

            for (int i = 0; i < specializedPoolSize; i++)
            {
                SpecializedSkill_Leviathan skillInstance = CreateSpecializedSkillInstance();
                if (skillInstance != null)
                {
                    specializedSkillPool.Push(skillInstance);
                }
            }
        }

        private SpecializedSkill_Leviathan CreateSpecializedSkillInstance()
        {
            if (specializedSkill == null)
                return null;

            SpecializedSkill_Leviathan skillInstance = Instantiate(specializedSkill, transform.position, Quaternion.identity);
            if (skillInstance == null)
                return null;

            skillInstance.transform.SetParent(null);
            skillInstance.gameObject.SetActive(true);
            skillInstance.Clear();
            skillInstance.gameObject.SetActive(false);

            allSpecializedSkills.Add(skillInstance);
            return skillInstance;
        }

        private SpecializedSkill_Leviathan GetSpecializedSkillFromPool()
        {
            while (specializedSkillPool.Count > 0)
            {
                SpecializedSkill_Leviathan skillInstance = specializedSkillPool.Pop();
                if (skillInstance != null)
                    return skillInstance;
            }

            return CreateSpecializedSkillInstance();
        }

        private void ReturnSpecializedSkillToPool(SpecializedSkill_Leviathan skillInstance)
        {
            if (skillInstance == null)
                return;

            skillInstance.Clear();
            skillInstance.gameObject.SetActive(false);

            if (!specializedSkillPool.Contains(skillInstance))
            {
                specializedSkillPool.Push(skillInstance);
            }
        }

        private void UseSpecializedSkill()
        {
            if (specializedSkill == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_Leviathan)}] Chưa gán SpecializedSkill_Leviathan.", this);
                return;
            }

            if (View == null || View.Muzzle == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_Leviathan)}] Không tìm thấy View hoặc Muzzle (nòng súng).", this);
                return;
            }

            // Kiểm tra và tiêu hao năng lượng
            if (CurrentEnergy < specializedSkillEnergyCost)
            {
                // Không đủ năng lượng -> hoàn lại hồi chiêu
                ReduceSkillCooldown(specializedSkillIndex, GetCooldown(specializedSkillIndex));
                return;
            }

            if (!HasLookTarget)
                return;

            SpendEnergy(specializedSkillEnergyCost);

            Transform muzzle = View.Muzzle;

            Vector2 direction = LookTarget - (Vector2)muzzle.position;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            direction.Normalize();

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            SpecializedSkill_Leviathan skillInstance = GetSpecializedSkillFromPool();
            if (skillInstance == null)
                return;

            skillInstance.transform.SetParent(null);
            skillInstance.transform.SetPositionAndRotation(muzzle.position, rotation);

            float finalDmg = specializedSkillDmg * Stats.DamageMultiplier;
            skillInstance.gameObject.SetActive(true);
            skillInstance.Initialize(this, direction, finalDmg, ReturnSpecializedSkillToPool);
        }
        #endregion

        private void OnDestroy()
        {
            foreach (NormalSkill_Leviathan skillInstance in allSkills)
            {
                if (skillInstance != null)
                {
                    Destroy(skillInstance.gameObject);
                }
            }

            allSkills.Clear();
            skillPool.Clear();

            foreach (SpecializedSkill_Leviathan skillInstance in allSpecializedSkills)
            {
                if (skillInstance != null)
                {
                    Destroy(skillInstance.gameObject);
                }
            }

            allSpecializedSkills.Clear();
            specializedSkillPool.Clear();

            if (optionalSkill1 != null)
            {
                optionalSkill1.Clear();
            }

            if (optionalSkill1_2 != null)
            {
                optionalSkill1_2.Clear();
            }

            if (optionalSkill1_3 != null)
            {
                optionalSkill1_3.Clear();
            }

            if (optionalSkill2 != null)
            {
                optionalSkill2.Clear();
            }

            if (optionalSkill2_1 != null)
            {
                optionalSkill2_1.Clear();
            }

            if (optionalSkill2_2 != null)
            {
                optionalSkill2_2.Clear();
            }
        }

        public override void TakeDamage(float amount)
        {
            base.TakeDamage(amount);

            if (amount > 0f && optionalSkill1_3 != null && optionalSkill1_3.IsActive)
            {
                optionalSkill1_3.OnOwnerDamaged(amount);
            }
        }
    }
}
