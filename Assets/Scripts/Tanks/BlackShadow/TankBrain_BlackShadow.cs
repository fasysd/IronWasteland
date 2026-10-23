using IronWasteland;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.BlackShadow
{
    [DisallowMultipleComponent]
    public class TankBrain_BlackShadow : TankBrain
    {
        [Header("Normal Skill - Design")]
        [Tooltip("Prefab hoặc mẫu của NormalSkill.")]
        [SerializeField] private NormalSkill normalSkill;

        [Header("Normal Skill - Pool")]
        [Tooltip("Số lượng đạn khởi tạo sẵn trong Pool.")]
        [SerializeField, Min(0)] private int initialPoolSize = 4;

        [Header("Normal Skill - Combat")]
        [Tooltip("Sát thương gây ra khi trúng mục tiêu.")]
        [SerializeField, Min(0f)] private float normalSkillDmg = 50f;

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
        [SerializeField, Min(0.01f)] private float specializedSkillCooldown = 10f;

        [Tooltip("Khoảng trễ tối thiểu giữa lần đặt ảo ảnh và lần bấm đổi chỗ (giây).")]
        [SerializeField, Min(0.05f)] private float recastDelay = 0.2f;

        [Header("Optional Skill 1 - Design")]
        [Tooltip("Kỹ năng tùy chọn 1 - Biến thể 1: Cường hóa đao khí.")]
        [SerializeField] private OptionalSkill1 optionalSkill1;

        [Tooltip("Kỹ năng tùy chọn 1 - Biến thể 2: Shadow Link (Liên kết bóng tối).")]
        [SerializeField] private OptionalSkill1_2 optionalSkill1_2;

        [Tooltip("Kỹ năng tùy chọn 1 - Biến thể 3: Shadow Guard (Bóng vệ tinh bảo vệ).")]
        [SerializeField] private OptionalSkill1_3 optionalSkill1_3;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 1 (giây).")]
        [SerializeField, Min(0f)] private float optionalSkill1Cooldown = 5f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 1_2 (giây).")]
        [SerializeField, Min(0f)] private float optionalSkill1_2Cooldown = 6f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 1_3 (giây).")]
        [SerializeField, Min(0f)] private float optionalSkill1_3Cooldown = 10f;
        [SerializeField, Min(0f)] private float cooldownReductionKillingInEnhanced = 10f;

        [Header("Optional Skill 2 - Design")]
        [Tooltip("Kỹ năng tùy chọn 2 - Biến thể 1: Shadow Ambush (Mai phục bóng tối).")]
        [SerializeField] private OptionalSkill2 optionalSkill2;

        [Tooltip("Kỹ năng tùy chọn 2 - Biến thể 2: Shadow Possession (Nhập thể bóng tối).")]
        [SerializeField] private OptionalSkill2_1 optionalSkill2_1;

        [Tooltip("Kỹ năng tùy chọn 2 - Biến thể 3: Shadow Exchange (Hoán đổi bóng tối).")]
        [SerializeField] private OptionalSkill2_2 optionalSkill2_2;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 2 (giây).")]
        [SerializeField, Min(0f)] private float optionalSkill2Cooldown = 8f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 2_1 (giây).")]
        [SerializeField, Min(0f)] private float optionalSkill2_1Cooldown = 12f;

        [Tooltip("Thời gian hồi chiêu của Optional Skill 2_2 (giây).")]
        [SerializeField, Min(0f)] private float optionalSkill2_2Cooldown = 10f;

        [Header("Optional Skill 1 - Orb VFX")]
        [Tooltip("Prefab của tàn hỏa đen (OptionalSkill1Orb) hút về thân xe.")]
        [SerializeField] private OptionalSkill1Orb optionalSkill1OrbPrefab;

        [Tooltip("Số lượng tàn hỏa đen khởi tạo sẵn trong Pool.")]
        [SerializeField, Min(0)] private int orbPoolSize = 6;

        private const int normalSkillIndex = 0;
        private const int specializedSkillIndex = 1;
        private const int optionalSkill1Index = 2;
        private const int optionalSkill2Index = 3;

        private bool isEnhanced = false;

        // Pool NormalSkill
        private readonly Stack<NormalSkill> skillPool = new Stack<NormalSkill>();
        private readonly HashSet<NormalSkill> allSkills = new HashSet<NormalSkill>();

        // Pool SpecializedSkill
        private readonly Stack<SpecializedSkill> specializedSkillPool = new Stack<SpecializedSkill>();
        private readonly HashSet<SpecializedSkill> allSpecializedSkills = new HashSet<SpecializedSkill>();
        private SpecializedSkill activeSpecializedSkill;

        // Pool OptionalSkill1Orb
        private readonly Stack<OptionalSkill1Orb> orbPool = new Stack<OptionalSkill1Orb>();
        private readonly HashSet<OptionalSkill1Orb> allOrbs = new HashSet<OptionalSkill1Orb>();

        private void Start()
        {
            PrewarmNormalSkillPool();
            PrewarmSpecializedSkillPool();
            PrewarmOrbPool();
        }

        /// <summary>
        /// Đăng ký nhận event từ các skill con. Dùng OnEnable thay vì Start để việc
        /// đăng ký bám theo vòng đời bật/tắt của component; luôn gỡ trước rồi mới
        /// gắn lại nhằm tránh đăng ký trùng khi component được bật lại nhiều lần.
        /// </summary>
        private void OnEnable()
        {
            if (optionalSkill1 != null)
            {
                optionalSkill1.OnIsEnhancedChange -= OptionalSkill1_OnTimeEnhancedRunOut;
                optionalSkill1.OnIsEnhancedChange += OptionalSkill1_OnTimeEnhancedRunOut;
            }

            if (optionalSkill2_2 != null)
            {
                optionalSkill2_2.OnCloneExpired -= HandleOptionalSkill2CloneExpired;
                optionalSkill2_2.OnCloneExpired += HandleOptionalSkill2CloneExpired;
            }
        }

        /// <summary>
        /// Gỡ đăng ký khi component bị tắt. Người nghe tự quản lý đăng ký của mình,
        /// skill không bao giờ tự xoá subscriber.
        /// </summary>
        private void OnDisable()
        {
            if (optionalSkill1 != null)
                optionalSkill1.OnIsEnhancedChange -= OptionalSkill1_OnTimeEnhancedRunOut;

            if (optionalSkill2_2 != null)
                optionalSkill2_2.OnCloneExpired -= HandleOptionalSkill2CloneExpired;
        }

        private void HandleOptionalSkill2CloneExpired()
        {
            // Khi bản sao hết hạn hoặc bị đánh vỡ mà người chơi chưa kịp hoán đổi -> Kích hoạt full hồi chiêu (đã trừ CDR)
            float fullCooldown = GetCooldownWithReduction(optionalSkill2_2Cooldown);
            SetSkillCooldown(optionalSkill2Index, fullCooldown);
        }

        private void OptionalSkill1_OnTimeEnhancedRunOut(bool obj)
        {
            isEnhanced = obj;
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
            if (index == optionalSkill1Index)
            {
                if (Context.OptionSkill1 == 1) return optionalSkill1_2Cooldown;
                if (Context.OptionSkill1 == 2) return optionalSkill1_3Cooldown;
                return optionalSkill1Cooldown;
            }
            if (index == optionalSkill2Index)
            {
                if (Context.OptionSkill2 == 1) return optionalSkill2_1Cooldown;
                if (Context.OptionSkill2 == 2)
                {
                    // Nếu đang có bản sao ở State 1 (chờ hoán đổi) -> lần bấm này là hoán đổi vị trí -> tính full cooldown
                    if (optionalSkill2_2 != null && optionalSkill2_2.HasActiveClone)
                        return optionalSkill2_2Cooldown;

                    // Nếu chưa có bản sao -> lần bấm này là tạo bản sao -> cho phép bấm lần 2 sau recastDelay
                    return recastDelay;
                }
                return optionalSkill2Cooldown;
            }

            return base.GetBaseCooldown(index);
        }

        protected override void OnUseSkill(int index)
        {
            if (index == normalSkillIndex)
            {
                FireNormalSkill(isEnhanced);
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
        private void UseOptionalSkill1()
        {
            if (Context.OptionSkill1 == 1)
            {
                if (optionalSkill1_2 != null)
                {
                    bool isLinked = optionalSkill1_2.Initialize(this, LookTarget);
                    if (!isLinked)
                    {
                        // Không tìm thấy xe đồng minh hợp lệ dưới con trỏ chuột -> hoàn lại hồi chiêu
                        ReduceSkillCooldown(optionalSkill1Index, GetCooldown(optionalSkill1Index));
                    }
                }
            }
            else if (Context.OptionSkill1 == 2)
            {
                if (optionalSkill1_3 != null)
                {
                    bool isShielded = optionalSkill1_3.Initialize(this, LookTarget);
                    if (!isShielded)
                    {
                        // Không tìm thấy xe đồng minh hợp lệ dưới con trỏ chuột -> hoàn lại hồi chiêu
                        ReduceSkillCooldown(optionalSkill1Index, GetCooldown(optionalSkill1Index));
                    }
                }
            }
            else
            {
                optionalSkill1?.Initialize();
            }
        }

        private void UseOptionalSkill2()
        {
            if (Context.OptionSkill2 == 1)
            {
                if (optionalSkill2_1 != null)
                {
                    bool success = optionalSkill2_1.Initialize(this, LookTarget);
                    if (!success)
                    {
                        // Không tìm thấy xe đồng minh hợp lệ dưới con trỏ chuột -> hoàn lại hồi chiêu
                        ReduceSkillCooldown(optionalSkill2Index, GetCooldown(optionalSkill2Index));
                    }
                }
            }
            else if (Context.OptionSkill2 == 2)
            {
                if (optionalSkill2_2 != null)
                {
                    bool success = optionalSkill2_2.Execute(this, LookTarget);
                    if (!success)
                    {
                        // Không tìm thấy xe đồng minh hợp lệ dưới con trỏ chuột -> hoàn lại hồi chiêu
                        ReduceSkillCooldown(optionalSkill2Index, GetCooldown(optionalSkill2Index));
                    }
                }
            }
            else
            {
                if (optionalSkill2 != null)
                {
                    bool success = optionalSkill2.Initialize(this, LookTarget);
                    if (!success)
                    {
                        // Không tìm thấy kẻ địch hợp lệ dưới con trỏ chuột -> hoàn lại hồi chiêu
                        ReduceSkillCooldown(optionalSkill2Index, GetCooldown(optionalSkill2Index));
                    }
                }
            }
        }

        #region Normal Skill Logic
        private void PrewarmNormalSkillPool()
        {
            if (normalSkill == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_BlackShadow)}] Chưa gán prefab/mẫu NormalSkill.", this);
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

        private void FireNormalSkill(bool isEnhanced = false)
        {
            if (normalSkill == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_BlackShadow)}] Chưa gán NormalSkill.", this);
                return;
            }

            if (View == null || View.Muzzle == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_BlackShadow)}] Không tìm thấy View hoặc Muzzle (nòng súng).", this);
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

            skillInstance.Initialize(direction, ReturnNormalSkillToPool,isEnhanced);
            skillInstance.TargetFound += OnNormalSkillTargetFound;

            skillInstance.gameObject.SetActive(true);
        }

        private void OnNormalSkillTargetFound(NormalSkill skillInstance, EnemyBrain enemy)
        {
            if (enemy == null)
                return;

            float damage = normalSkillDmg;

            // Nếu có buff chí mạng từ Shadow Ambush cho đòn đánh kế tiếp
            if (optionalSkill2 != null && optionalSkill2.HasBackstabBuff)
            {
                // Kiểm tra vị trí của xe tank có đang ở sau lưng kẻ địch hay không
                bool isBackstab = optionalSkill2.CheckAndConsumeBackstab(transform.position, enemy);
                if (isBackstab)
                {
                    // Xe tank đang ở sau lưng -> tính kèm bonus DamageMultiplier chí mạng
                    damage = normalSkillDmg * Stats.DamageMultiplier;
                }
                else
                {
                    // Đòn kế tiếp không ở sau lưng -> tiêu hao buff và tính sát thương thường
                    damage = normalSkillDmg;
                }
            }
            else
            {
                damage = normalSkillDmg * Stats.DamageMultiplier;
            }

            enemy.TakeDamage(damage);

            if (!enemy.IsAlive && isEnhanced)
            {
                ReduceSkillCooldown(specializedSkillIndex, cooldownReductionKillingInEnhanced);
                SpawnOptionalSkill1Orb(enemy.transform.position);
            }
            TextDamageManager.Instance?.Show(damage, skillInstance.transform.position);
        }
        #endregion

        #region Specialized Skill Logic
        private void PrewarmSpecializedSkillPool()
        {
            if (specializedSkill == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_BlackShadow)}] Chưa gán prefab/mẫu SpecializedSkill.", this);
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
                Debug.LogWarning($"[{nameof(TankBrain_BlackShadow)}] Chưa gán SpecializedSkill.", this);
                return;
            }

            // GIAI ĐOẠN 2: Ảo ảnh đang ở State 0 (chờ đổi chỗ) -> Đổi vị trí và kích nổ
            if (activeSpecializedSkill != null && activeSpecializedSkill.SkillState == 0)
            {
                Vector2 oldPosition = transform.position;
                Vector2 targetPosition = activeSpecializedSkill.CopyPosition;

                // Đổi vị trí xe tăng tới vị trí ảo ảnh
                transform.position = targetPosition;

                // Kích nổ ảo ảnh
                SpecializedSkill explodingSkill = activeSpecializedSkill;
                activeSpecializedSkill = null;
                explodingSkill.Explode();

                SpecializedSkill tankSmoke = GetSpecializedSkillFromPool();
                if(tankSmoke != null)
                {
                    tankSmoke.transform.SetParent(null);
                    tankSmoke.transform.position =oldPosition;
                    tankSmoke.Initialize(oldPosition, ReturnSpecializedSkillToPool);
                    tankSmoke.gameObject.SetActive(true);
                    tankSmoke.Explode();
                }
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

            if (!enemy.IsAlive && isEnhanced)
            {
                SpawnOptionalSkill1Orb(enemy.transform.position);
            }
        }
        #endregion

        #region Optional Skill 1 Orb Logic
        /// <summary>
        /// Khởi tạo sẵn một lượng đốm tàn hỏa đen trong Object Pool.
        /// </summary>
        private void PrewarmOrbPool()
        {
            if (optionalSkill1OrbPrefab == null)
                return;

            for (int i = 0; i < orbPoolSize; i++)
            {
                OptionalSkill1Orb orbInstance = CreateOrbInstance();
                if (orbInstance != null)
                    orbPool.Push(orbInstance);
            }
        }

        /// <summary>
        /// Tạo instance mới cho OptionalSkill1Orb và đưa vào danh sách quản lý.
        /// </summary>
        private OptionalSkill1Orb CreateOrbInstance()
        {
            if (optionalSkill1OrbPrefab == null)
                return null;

            OptionalSkill1Orb orbInstance = Instantiate(optionalSkill1OrbPrefab, transform.position, Quaternion.identity);
            if (orbInstance == null)
                return null;

            orbInstance.transform.SetParent(null);
            orbInstance.gameObject.SetActive(true);
            orbInstance.Clear();
            orbInstance.gameObject.SetActive(false);

            allOrbs.Add(orbInstance);
            return orbInstance;
        }

        /// <summary>
        /// Lấy một đốm tàn hỏa đen khả dụng từ Pool (hoặc tạo mới nếu rỗng).
        /// </summary>
        private OptionalSkill1Orb GetOrbFromPool()
        {
            while (orbPool.Count > 0)
            {
                OptionalSkill1Orb orb = orbPool.Pop();
                if (orb != null)
                    return orb;
            }

            return CreateOrbInstance();
        }

        /// <summary>
        /// Thu hồi đốm tàn hỏa đen về Pool sau khi đã chạm vào thân xe.
        /// </summary>
        private void ReturnOrbToPool(OptionalSkill1Orb orb)
        {
            if (orb == null)
                return;

            orb.Clear();
            orb.gameObject.SetActive(false);

            if (!orbPool.Contains(orb))
                orbPool.Push(orb);
        }

        /// <summary>
        /// Sinh tàn hỏa đen tại vị trí kẻ địch chết và bắt đầu bay hút về thân xe tank.
        /// </summary>
        private void SpawnOptionalSkill1Orb(Vector2 spawnPosition)
        {
            if (optionalSkill1OrbPrefab == null)
                return;

            OptionalSkill1Orb orb = GetOrbFromPool();
            if (orb == null)
                return;

            orb.transform.SetParent(null);
            orb.Initialize(spawnPosition, transform, ReturnOrbToPool);
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

            foreach (OptionalSkill1Orb orb in allOrbs)
            {
                if (orb != null)
                    Destroy(orb.gameObject);
            }
            allOrbs.Clear();
            orbPool.Clear();
            if (optionalSkill1 != null)
                optionalSkill1.OnIsEnhancedChange -= OptionalSkill1_OnTimeEnhancedRunOut;
            if (optionalSkill2_2 != null)
                optionalSkill2_2.OnCloneExpired -= HandleOptionalSkill2CloneExpired;
        }
    }
}
