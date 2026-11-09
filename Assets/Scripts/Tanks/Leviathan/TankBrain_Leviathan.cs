using IronWasteland;
using IronWasteland.Core;
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
        [Tooltip("Hệ số sát thương của Water Pulse theo Tấn công (0.6 = 60% Tấn công theo GDD).")]
        [SerializeField, Min(0f)] private float normalSkillScale = 0.6f;

        [Tooltip("Tỉ lệ sát thương cộng thêm theo Giới hạn máu của chính Leviathan (0.01 = 1% máu tối đa theo GDD).")]
        [SerializeField, Min(0f)] private float normalSkillMaxHealthRatio = 0.01f;

        [Tooltip("Thời gian hồi chiêu của Normal Skill (giây).")]
        [SerializeField, Min(0.01f)] private float normalSkillCooldown = 0.6f;

        [Tooltip("Năng lượng hồi lại cho xe mỗi lần bắn Water Pulse.")]
        [SerializeField, Min(0f)] private float normalSkillEnergyGain = 12f;

        [Tooltip("Năng lượng cộng thêm cho mỗi kẻ địch trúng đòn (để 0 nếu chỉ muốn hồi theo mỗi lần bắn).")]
        [SerializeField, Min(0f)] private float energyGainPerHit = 0f;

        [Header("Specialized Skill - Design")]
        [Tooltip("Prefab hoặc mẫu của SpecializedSkill_Leviathan (Bong bóng nước).")]
        [SerializeField] private SpecializedSkill_Leviathan specializedSkill;

        [Header("Specialized Skill - Pool")]
        [Tooltip("Số lượng bong bóng nước khởi tạo sẵn trong Pool.")]
        [SerializeField, Min(0)] private int specializedPoolSize = 2;

        [Header("Specialized Skill - Combat")]
        [Tooltip("Thời gian hồi chiêu của Specialized Skill (giây).")]
        [SerializeField, Min(0.01f)] private float specializedSkillCooldown = 9f;

        [Tooltip("Lượng năng lượng tiêu hao khi bắn bong bóng nước.")]
        [SerializeField, Min(0f)] private float specializedSkillEnergyCost = 50f;

        [Header("Optional Skill 1 - Design")]
        [Tooltip("Kỹ năng tùy chọn 1 - Biến thể 1: Water Armor (Giáp Nước).")]
        [SerializeField] private OptionalSkill1_Leviathan optionalSkill1;

        [Tooltip("Kỹ năng tùy chọn 1 - Biến thể 2: Guardian Current (Dòng Chảy Hộ Vệ).")]
        [SerializeField] private OptionalSkill1_2_Leviathan optionalSkill1_2;

        [Tooltip("Kỹ năng tùy chọn 1 - Biến thể 3: Regenerative Shell (Kén Nước Trị Liệu).")]
        [SerializeField] private OptionalSkill1_3_Leviathan optionalSkill1_3;

        [Header("Optional Skill 1 (Water Armor) - Combat & Scaling")]
        [Tooltip("Hệ số sát thương diện rộng khi màng nước phát nổ (0.6 = 60% Tấn công theo GDD).")]
        [SerializeField, Min(0f)] private float optionalSkill1ExplosionAttackScale = 0.6f;

        [Tooltip("Tỉ lệ sát thương diện rộng theo Giới hạn máu của chính Leviathan (0.01 = 1% máu tối đa theo GDD).")]
        [SerializeField, Min(0f)] private float optionalSkill1ExplosionMaxHealthRatio = 0.01f;

        [Tooltip("Giá trị Phòng thủ tăng thêm khi màng nước bao phủ (0.5 = +50% Phòng thủ).")]
        [SerializeField, Range(0f, 1f)] private float optionalSkill1BonusDefense = 0.5f;

        [Tooltip("Tỉ lệ tăng nhẹ sát thương cho Water Pulse khi giáp nước duy trì (0.15 = +15%).")]
        [SerializeField, Range(0f, 1f)] private float optionalSkill1WaterPulseBonus = 0.15f;

        [Tooltip("Thời gian duy trì của giáp nước trước khi tự phát nổ (giây).")]
        [SerializeField, Min(0.5f)] private float optionalSkill1Duration = 6f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 1 (giây).")]
        [SerializeField, Min(0.01f)] private float optionalSkill1Cooldown = 12f;

        [Tooltip("Năng lượng tiêu hao của Optional Skill 1 (Water Armor).")]
        [SerializeField, Min(0f)] private float optionalSkill1EnergyCost = 55f;

        [Header("Optional Skill 1_2 (Guardian Current) - Combat & Scaling")]
        [Tooltip("Giãn cách giữa hai nhịp hồi máu cho đồng minh (giây).")]
        [SerializeField, Min(0.05f)] private float optionalSkill1_2HealInterval = 1f;

        [Tooltip("Lượng máu hồi mỗi nhịp theo Giới hạn máu của chính Leviathan (0.035 = 3.5% máu tối đa).")]
        [SerializeField, Range(0f, 1f)] private float optionalSkill1_2HealMaxHealthRatio = 0.035f;

        [Tooltip("Tỷ lệ sát thương của đồng minh chuyển sang cho Leviathan gánh thay (0.3 = gánh 30% sát thương).")]
        [SerializeField, Range(0f, 1f)] private float optionalSkill1_2DamageShareRatio = 0.3f;

        [Tooltip("Thời gian duy trì tối đa của dòng chảy liên kết (giây).")]
        [SerializeField, Min(0.5f)] private float optionalSkill1_2Duration = 6f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 1_2 (giây).")]
        [SerializeField, Min(0.01f)] private float optionalSkill1_2Cooldown = 13f;

        [Tooltip("Năng lượng tiêu hao của Optional Skill 1_2 (Guardian Current).")]
        [SerializeField, Min(0f)] private float optionalSkill1_2EnergyCost = 60f;

        [Header("Optional Skill 1_3 (Regenerative Shell) - Combat & Scaling")]
        [Tooltip("Giãn cách giữa hai nhịp hồi máu trong thời gian kén duy trì (giây).")]
        [SerializeField, Min(0.05f)] private float optionalSkill1_3HealInterval = 0.5f;

        [Tooltip("Lượng máu hồi mỗi nhịp theo Giới hạn máu của chính Leviathan (0.04 = 4% máu tối đa).")]
        [SerializeField, Range(0f, 1f)] private float optionalSkill1_3HealMaxHealthRatio = 0.04f;

        [Tooltip("Lượng máu hồi phục thưởng thêm khi kén hoàn tất mà không nhận sát thương, theo Giới hạn máu của chính Leviathan (0.2 = 20% máu tối đa).")]
        [SerializeField, Range(0f, 1f)] private float optionalSkill1_3BonusCompletionHealMaxHealthRatio = 0.2f;

        [Tooltip("Thời gian duy trì của kén nước trị liệu (giây).")]
        [SerializeField, Min(0.5f)] private float optionalSkill1_3Duration = 5f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 1_3 (giây).")]
        [SerializeField, Min(0.01f)] private float optionalSkill1_3Cooldown = 15f;

        [Tooltip("Năng lượng tiêu hao của Optional Skill 1_3 (Regenerative Shell).")]
        [SerializeField, Min(0f)] private float optionalSkill1_3EnergyCost = 50f;

        [Header("Optional Skill 2 - Design")]
        [Tooltip("Kỹ năng tùy chọn 2 - Biến thể 1: Tidal Return (Thủy Triều Nghịch Chuyển).")]
        [SerializeField] private OptionalSkill2_Leviathan optionalSkill2;

        [Tooltip("Kỹ năng tùy chọn 2 - Biến thể 2: Leviathan Form (Dạng Thủy Quái).")]
        [SerializeField] private OptionalSkill2_2_Leviathan optionalSkill2_2;

        [Tooltip("Kỹ năng tùy chọn 2 - Biến thể 3: Tide Commander (Chỉ Huy Thủy Triều).")]
        [SerializeField] private OptionalSkill2_3_Leviathan optionalSkill2_3;

        [Header("Optional Skill 2 (Tidal Return) - Combat & Scaling")]
        [Tooltip("Hệ số sát thương theo Tấn công khi kéo kẻ địch về tâm xoáy (1.5 = 150% Tấn công).")]
        [SerializeField, Min(0f)] private float optionalSkill2AttackScale = 1.5f;

        [Tooltip("Tỉ lệ sát thương theo Giới hạn máu của chính Leviathan (0.03 = 3% máu tối đa của Leviathan).")]
        [SerializeField, Min(0f)] private float optionalSkill2MaxHealthRatio = 0.03f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 2 (giây).")]
        [SerializeField, Min(0.01f)] private float optionalSkill2Cooldown = 14f;

        [Tooltip("Năng lượng tiêu hao của Optional Skill 2 - Tidal Return.")]
        [SerializeField, Min(0f)] private float optionalSkill2EnergyCost = 60f;

        [Header("Optional Skill 2_2 (Leviathan Form) - Combat & Scaling")]
        [Tooltip("Thời gian duy trì dạng thủy quái (giây).")]
        [SerializeField, Min(1.0f)] private float optionalSkill2_2Duration = 10f;

        [Tooltip("Giá trị Phòng thủ cộng thêm khi ở dạng thủy quái (điểm Phòng thủ, không phải phần trăm).")]
        [SerializeField, Min(0f)] private float optionalSkill2_2BonusDefense = 50f;

        [Tooltip("Tỉ lệ Giới hạn máu cộng thêm khi ở dạng thủy quái (0.5 = +50% Giới hạn máu).")]
        [SerializeField, Min(0f)] private float optionalSkill2_2BonusMaxHealthRatio = 0.5f;

        [Tooltip("Tỉ lệ Tấn công bị giảm khi ở dạng thủy quái (0.5 = -50% Tấn công).")]
        [SerializeField, Range(0f, 1f)] private float optionalSkill2_2AttackPenalty = 0.5f;

        [Tooltip("Giãn cách hồi máu khi ở dạng thủy quái (giây).")]
        [SerializeField, Min(0.05f)] private float optionalSkill2_2HealInterval = 1f;

        [Tooltip("Tỉ lệ Giới hạn máu hồi mỗi nhịp khi ở dạng thủy quái (0.01 = 1% Giới hạn máu).")]
        [SerializeField, Min(0f)] private float optionalSkill2_2HealMaxHealthRatio = 0.01f;

        [Tooltip("Thời gian hồi chiêu của Water Pulse khi đang ở dạng thủy quái (giây).")]
        [SerializeField, Min(0.01f)] private float optionalSkill2_2NormalSkillCooldown = 1.5f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 2_2 (giây).")]
        [SerializeField, Min(0.01f)] private float optionalSkill2_2Cooldown = 18f;

        [Tooltip("Năng lượng tiêu hao của Optional Skill 2_2 - Leviathan Form.")]
        [SerializeField, Min(0f)] private float optionalSkill2_2EnergyCost = 80f;

        [Header("Optional Skill 2_3 (Tide Commander) - Combat & Scaling")]
        [Tooltip("Thời gian duy trì của vùng thủy triều (giây).")]
        [SerializeField, Min(1.0f)] private float optionalSkill2_3Duration = 7f;

        [Tooltip("Tỉ lệ Tốc độ cộng thêm cho đồng minh đứng trong vùng (0.35 = +35% Tốc độ).")]
        [SerializeField, Min(0f)] private float optionalSkill2_3MoveSpeedRate = 0.35f;

        [Tooltip("Thời gian duy trì của lớp giáp nước (giây); mỗi nhịp hồi khiên sẽ đặt lại thời gian này.")]
        [SerializeField, Min(0.1f)] private float optionalSkill2_3ShieldLifetime = 5f;

        [Tooltip("Tỉ lệ Giới hạn máu của Leviathan cho lượng khiên ban đầu (0.1 = 10%).")]
        [SerializeField, Min(0f)] private float optionalSkill2_3InitialShieldMaxHealthRatio = 0.1f;

        [Tooltip("Tỉ lệ Giới hạn máu của Leviathan hồi thêm mỗi nhịp khiên (0.05 = 5%).")]
        [SerializeField, Min(0f)] private float optionalSkill2_3ShieldRefreshMaxHealthRatio = 0.05f;

        [Tooltip("Tỉ lệ Giới hạn máu của Leviathan cho mức khiên tối đa (0.15 = 15%).")]
        [SerializeField, Min(0f)] private float optionalSkill2_3MaxShieldMaxHealthRatio = 0.15f;

        [Tooltip("Giãn cách hồi khiên cho đồng minh đứng trong vùng (giây).")]
        [SerializeField, Min(0.05f)] private float optionalSkill2_3ShieldRefreshInterval = 1f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 2_3 (giây).")]
        [SerializeField, Min(0.01f)] private float optionalSkill2_3Cooldown = 16f;

        [Tooltip("Năng lượng tiêu hao của Optional Skill 2_3 - Tide Commander.")]
        [SerializeField, Min(0f)] private float optionalSkill2_3EnergyCost = 70f;

        [Header("Water Bubble (Specialized Skill) - Sát Thương")]
        [Tooltip("Hệ số sát thương khi bong bóng trúng Tinh anh (1.0 = 100% Tấn công).")]
        [SerializeField, Min(0f)] private float bubbleEliteHitAttackScale = 1.0f;

        [Tooltip("Tỉ lệ sát thương theo Giới hạn máu của chính Leviathan khi trúng Tinh anh (0.03 = 3% máu tối đa của Leviathan).")]
        [SerializeField, Min(0f)] private float bubbleEliteHitMaxHealthRatio = 0.03f;

        [Tooltip("Hệ số sát thương diện rộng khi bong bóng nổ (1.5 = 150% Tấn công).")]
        [SerializeField, Min(0f)] private float bubbleExplosionAttackScale = 1.5f;

        [Tooltip("Tỉ lệ sát thương diện rộng theo Giới hạn máu của chính Leviathan (0.02 = 2% máu tối đa của Leviathan).")]
        [SerializeField, Min(0f)] private float bubbleExplosionMaxHealthRatio = 0.02f;

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
                // Khi ở dạng Leviathan Form, Water Pulse bị kéo dài thời gian hồi theo thiết kế
                // (1.5s thay vì 0.6s) — đây là cái giá của việc tăng mạnh phòng thủ và giới hạn máu.
                if (optionalSkill2_2 != null && optionalSkill2_2.IsActive)
                {
                    return optionalSkill2_2NormalSkillCooldown;
                }
                return normalSkillCooldown;
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
                    return optionalSkill2_2Cooldown;
                }
                if (Context.OptionSkill2 == 2)
                {
                    return optionalSkill2_3Cooldown;
                }
                return optionalSkill2Cooldown;
            }

            return base.GetBaseCooldown(index);
        }

        /// <summary>
        /// Năng lượng tiêu hao theo từng biến thể kỹ năng. TankBrain.UseSkill gọi hàm này để
        /// kiểm tra và trừ năng lượng trước khi thi triển, nên các hàm Use* bên dưới KHÔNG trừ thêm.
        /// </summary>
        protected override float GetSkillEnergyCost(int index)
        {
            if (index == specializedSkillIndex)
            {
                return specializedSkillEnergyCost;
            }

            if (index == optionalSkill1Index)
            {
                if (Context.OptionSkill1 == 1)
                {
                    return optionalSkill1_2EnergyCost;
                }
                if (Context.OptionSkill1 == 2)
                {
                    return optionalSkill1_3EnergyCost;
                }
                return optionalSkill1EnergyCost;
            }

            if (index == optionalSkill2Index)
            {
                if (Context.OptionSkill2 == 1)
                {
                    return optionalSkill2_2EnergyCost;
                }
                if (Context.OptionSkill2 == 2)
                {
                    return optionalSkill2_3EnergyCost;
                }
                return optionalSkill2EnergyCost;
            }

            return base.GetSkillEnergyCost(index);
        }

        /// <summary>
        /// Năng lượng hồi lại sau khi dùng kỹ năng. Water Pulse hồi riêng theo thiết kế
        /// (12 năng lượng mỗi lần bắn), các kỹ năng còn lại dùng giá trị mặc định của TankBrain.
        /// </summary>
        protected override float GetSkillEnergyGain(int index)
        {
            if (index == normalSkillIndex)
            {
                return normalSkillEnergyGain;
            }

            return base.GetSkillEnergyGain(index);
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
                        optionalSkill1_2HealInterval,
                        optionalSkill1_2HealMaxHealthRatio,
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
                        optionalSkill1_3HealInterval,
                        optionalSkill1_3HealMaxHealthRatio,
                        optionalSkill1_3BonusCompletionHealMaxHealthRatio,
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
            float finalExplosionDmg = CalculateSkillDamage(optionalSkill1ExplosionAttackScale, optionalSkill1ExplosionMaxHealthRatio);
            optionalSkill1.Initialize(
                this,
                finalExplosionDmg,
                optionalSkill1BonusDefense,
                optionalSkill1WaterPulseBonus,
                optionalSkill1Duration);
        }
        #endregion

        #region Optional Skill 2 Logic
        private void UseOptionalSkill2()
        {
            if (Context.OptionSkill2 == 1)
            {
                if (optionalSkill2_2 != null)
                {
                    optionalSkill2_2.gameObject.SetActive(true);
                    optionalSkill2_2.Initialize(
                        this,
                        optionalSkill2_2Duration,
                        optionalSkill2_2BonusMaxHealthRatio,
                        optionalSkill2_2BonusDefense,
                        optionalSkill2_2HealInterval,
                        optionalSkill2_2HealMaxHealthRatio,
                        optionalSkill2_2AttackPenalty);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(TankBrain_Leviathan)}] Chưa gán OptionalSkill2_2_Leviathan.", this);
                }
                return;
            }

            if (Context.OptionSkill2 == 2)
            {
                if (optionalSkill2_3 != null)
                {
                    optionalSkill2_3.gameObject.SetActive(true);
                    optionalSkill2_3.Initialize(
                        this,
                        optionalSkill2_3Duration,
                        optionalSkill2_3MoveSpeedRate,
                        optionalSkill2_3ShieldLifetime,
                        optionalSkill2_3InitialShieldMaxHealthRatio,
                        optionalSkill2_3ShieldRefreshMaxHealthRatio,
                        optionalSkill2_3MaxShieldMaxHealthRatio,
                        optionalSkill2_3ShieldRefreshInterval);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(TankBrain_Leviathan)}] Chưa gán OptionalSkill2_3_Leviathan.", this);
                }
                return;
            }

            if (optionalSkill2 == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_Leviathan)}] Chưa gán OptionalSkill2_Leviathan.", this);
                return;
            }

            Vector2 targetPos = HasLookTarget ? LookTarget : (Vector2)transform.position;

            // Sát thương = 150% Tấn công + 3% Giới hạn máu của chính Leviathan, đi qua đúng một
            // công thức dùng chung nên tự động chịu hình phạt -50% Tấn công khi đang ở dạng Form.
            float finalDmg = CalculateSkillDamage(optionalSkill2AttackScale, optionalSkill2MaxHealthRatio);
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

        /// <summary>
        /// Tính sát thương thô của kỹ năng theo công thức chuẩn của Leviathan:
        /// Tấn công x Hệ số kỹ năng x (1 + Tỉ lệ sát thương) + Giới hạn máu của Leviathan x Tỉ lệ,
        /// sau đó áp hình phạt sát thương nếu đang ở dạng Leviathan Form.
        /// Dùng chung cho Water Pulse (đòn thường) và Water Bubble (đòn chuyên biệt) để mọi đòn
        /// của Leviathan đều đi qua đúng một công thức, tránh việc skill tự tính rồi quên hình phạt.
        /// Lưu ý: hình phạt -50% Tấn công của Leviathan Form KHÔNG áp ở đây mà nằm trong
        /// <see cref="Stats"/> (percent bonus Attack âm), nên mọi đòn đánh — kể cả đòn thường
        /// không đi qua hàm này — đều bị giảm đúng một lần, không bị trừ hai lần.
        /// </summary>
        /// <param name="attackScale">Hệ số theo Tấn công (0.5 = 50% Tấn công).</param>
        /// <param name="maxHealthRatio">Tỉ lệ theo Giới hạn máu của chính Leviathan (0.005 = 0.5%).</param>
        public float CalculateSkillDamage(float attackScale, float maxHealthRatio)
        {
            float damage = CombatUtility.CalculateTankRawDamage(Stats.Attack, attackScale, Stats.DamageRate)
                           + Mathf.Max(0f, Stats.MaxHealth) * Mathf.Max(0f, maxHealthRatio);

            return Mathf.Max(0f, damage);
        }

        private void OnNormalSkillTargetFound(NormalSkill_Leviathan skillInstance, EnemyBrain enemy)
        {
            if (enemy == null)
                return;

            // Sát thương Water Pulse = 60% Tấn công + 1% Giới hạn máu
            float scale = normalSkillScale;
            float hpRatio = normalSkillMaxHealthRatio;

            // Trong thời gian giáp duy trì, sát thương Water Pulse gây ra của Leviathan tăng nhẹ
            if (optionalSkill1 != null && optionalSkill1.IsActive)
            {
                scale *= (1f + optionalSkill1.WaterPulseDamageBonus);
                hpRatio *= (1f + optionalSkill1.WaterPulseDamageBonus);
            }

            float finalDmg = CalculateSkillDamage(scale, hpRatio);

            // Gây sát thương lên kẻ địch
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

            // Đăng ký một lần cho mỗi instance trong pool: Brain tính và áp sát thương,
            // skill chỉ phát sự kiện theo từng mục tiêu.
            skillInstance.OnEliteHit += OnBubbleEliteHit;
            skillInstance.OnExplosionHit += OnBubbleExplosionHit;

            allSpecializedSkills.Add(skillInstance);
            return skillInstance;
        }

        /// <summary>
        /// Bong bóng trúng Tinh anh: Brain là nơi duy nhất tính và áp sát thương,
        /// dùng chung <see cref="CalculateSkillDamage(float, float)"/> với đòn đánh thường.
        /// </summary>
        private void OnBubbleEliteHit(SpecializedSkill_Leviathan skillInstance, EnemyBrain enemy)
        {
            ApplyBubbleDamage(enemy, bubbleEliteHitAttackScale, bubbleEliteHitMaxHealthRatio);
        }

        /// <summary>
        /// Bong bóng nổ: áp sát thương diện rộng cho từng kẻ địch trong bán kính.
        /// </summary>
        private void OnBubbleExplosionHit(SpecializedSkill_Leviathan skillInstance, EnemyBrain enemy)
        {
            ApplyBubbleDamage(enemy, bubbleExplosionAttackScale, bubbleExplosionMaxHealthRatio);
        }

        /// <summary>
        /// Tính sát thương Water Bubble rồi áp lên mục tiêu, hiện luôn số damage bay.
        /// </summary>
        private void ApplyBubbleDamage(EnemyBrain enemy, float attackScale, float maxHealthRatio)
        {
            if (enemy == null || !enemy.IsAlive)
                return;

            float damage = CalculateSkillDamage(attackScale, maxHealthRatio);
            enemy.TakeDamage(damage);
            TextDamageManager.Instance?.Show(damage, enemy.transform.position);
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

            // Năng lượng đã được TankBrain.UseSkill kiểm tra và trừ qua GetSkillEnergyCost,
            // nên ở đây không trừ thêm (tránh tính tiền hai lần).

            if (!HasLookTarget)
                return;

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

            skillInstance.gameObject.SetActive(true);
            skillInstance.Initialize(this, direction, ReturnSpecializedSkillToPool);
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

            if (optionalSkill2_2 != null)
            {
                optionalSkill2_2.Clear();
            }

            if (optionalSkill2_3 != null)
            {
                optionalSkill2_3.Clear();
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
