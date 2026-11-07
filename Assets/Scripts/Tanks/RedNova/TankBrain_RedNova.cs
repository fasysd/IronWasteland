using IronWasteland.Core;
using IronWasteland.Enemies;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.RedNova
{
    /// <summary>
    /// TankBrain cụ thể cho xe tăng "Red Nova" (DPS):
    /// - Slot 0: Nova Shot — 110% ATK, hồi 10 năng lượng, hồi chiêu 0.5s.
    ///   (Khi bật Laser Cannon: thay bằng tia laser 150% ATK, tốn 5 năng lượng, hồi chiêu 1s.)
    /// - Slot 1: Triple Nova — 3 đạn × 90% ATK, 40 năng lượng, hồi chiêu 5s.
    /// - Slot 2: Kỹ năng tùy chọn 1 (0: Overdrive, 1: Laser Cannon, 2: Execution Core).
    /// - Slot 3: Kỹ năng tùy chọn 2 (0: Nova Barrage, 1: Nova Pierce, 2: Nova Rush).
    /// Toàn bộ thông số lấy theo GDD mục 4.3.
    /// </summary>
    [DisallowMultipleComponent]
    public class TankBrain_RedNova : TankBrain, ISkillBranchConfigurable
    {
        private const int SkillNovaShotIndex = 0;
        private const int SkillTripleNovaIndex = 1;
        private const int SkillOption1Index = 2;
        private const int SkillOption2Index = 3;

        private const string BonusKeyOverdrive = "RedNova_Overdrive";
        private const string BonusKeyNovaPierce = "RedNova_NovaPierce";
        private const string BonusKeyExecution = "RedNova_ExecutionCore";

        #region Inspector - Skill 0: Nova Shot
        [Header("Skill 0 - Nova Shot  [GDD: 110% ATK, +10 EN, CD 0.5s]")]
        [Tooltip("GameObject mẫu inactive của đạn Nova Shot.")]
        [SerializeField] private Skill_NovaShot novaShotDesign;
        [SerializeField, Min(0)] private int novaShotPoolSize = 8;
        [SerializeField, Min(0f)] private float novaShotDamageMultiplier = 1.10f;
        [SerializeField, Min(0f)] private float novaShotEnergyGain = 10f;
        [SerializeField, Min(0.01f)] private float novaShotCooldown = 0.5f;
        #endregion

        #region Inspector - Skill 1: Triple Nova
        [Header("Skill 1 - Triple Nova  [GDD: 3 đạn × 90% ATK, 40 EN, CD 5s]")]
        [Tooltip("GameObject mẫu inactive của đạn Triple Nova (nếu để trống sẽ dùng chung novaShotDesign).")]
        [SerializeField] private Skill_NovaShot tripleNovaDesign;
        [SerializeField, Min(1)] private int tripleNovaBulletCount = 3;
        [SerializeField, Range(0f, 45f)] private float tripleNovaSpreadAngle = 10f;
        [SerializeField, Min(0f)] private float tripleNovaDamageMultiplier = 0.90f;
        [SerializeField, Min(0f)] private float tripleNovaEnergyCost = 40f;
        [SerializeField, Min(0.01f)] private float tripleNovaCooldown = 5f;
        #endregion

        #region Inspector - Skill 2: Option Skill 1
        [Header("Skill 2 - Option Skill 1 (0: Overdrive, 1: Laser Cannon, 2: Execution Core)")]
        [Tooltip("Nhánh kỹ năng tùy chọn 1 mặc định trên Inspector (0, 1 hoặc 2).")]
        [SerializeField, Range(0, 2)] private int optionSkill1Branch = 0;

        [Header("Option 1.0 - Overdrive  [20 EN, CD 1s, +35% Tốc độ 1s, +20% Tỉ lệ sát thương]")]
        [SerializeField] private GameObject overdriveVisual;
        [SerializeField, Min(0f)] private float overdriveEnergyCost = 20f;
        [SerializeField, Min(0.01f)] private float overdriveCooldown = 1f;
        [SerializeField, Range(0f, 2f)] private float overdriveMoveSpeedPercent = 0.35f;
        [SerializeField, Range(0f, 1f)] private float overdriveDamageRateBonus = 0.20f;
        [SerializeField, Min(0.05f)] private float overdriveDuration = 1f;

        [Header("Option 1.1 - Laser Cannon  [5 EN/lần bắn, 150% ATK, CD 1s]")]
        [Tooltip("GameObject mẫu inactive của tia Laser Cannon.")]
        [SerializeField] private Skill_LaserCannon laserCannonDesign;
        [SerializeField, Min(0)] private int laserCannonPoolSize = 2;
        [SerializeField, Min(0f)] private float laserCannonDamageMultiplier = 1.50f;
        [SerializeField, Min(0f)] private float laserShotEnergyCost = 5f;
        [SerializeField, Min(0.05f)] private float laserShotCooldown = 1f;
        [SerializeField, Min(0.01f)] private float laserCannonCooldown = 1f;

        [Header("Option 1.2 - Execution Core  [45 EN, CD 10s, +55% Tỉ lệ sát thương, +30% Xuyên giáp]")]
        [SerializeField] private GameObject executionCoreVisual;
        [SerializeField, Min(0f)] private float executionCoreEnergyCost = 45f;
        [SerializeField, Min(0.01f)] private float executionCoreCooldown = 10f;
        [SerializeField, Range(0f, 2f)] private float executionDamageRateBonus = 0.55f;
        [SerializeField, Range(0f, 1f)] private float executionArmorPenBonus = 0.30f;
        #endregion

        #region Inspector - Skill 3: Option Skill 2
        [Header("Skill 3 - Option Skill 2 (0: Nova Barrage, 1: Nova Pierce, 2: Nova Rush)")]
        [Tooltip("Nhánh kỹ năng tùy chọn 2 mặc định trên Inspector (0, 1 hoặc 2).")]
        [SerializeField, Range(0, 2)] private int optionSkill2Branch = 0;

        [Header("Option 2.0 - Nova Barrage  [65 EN, CD 12s, 3s, tick 0.5s, 75% ATK/tick]")]
        [Tooltip("GameObject mẫu inactive của vùng oanh kích Nova Barrage.")]
        [SerializeField] private Skill_NovaBarrage novaBarrageDesign;
        [SerializeField, Min(0)] private int novaBarragePoolSize = 2;
        [SerializeField, Min(0f)] private float novaBarrageDamageMultiplier = 0.75f;
        [SerializeField, Min(0f)] private float novaBarrageEnergyCost = 65f;
        [SerializeField, Min(0.01f)] private float novaBarrageCooldown = 12f;
        [SerializeField, Min(0.1f)] private float novaBarrageDuration = 3f;
        [SerializeField, Min(0.05f)] private float novaBarrageInterval = 0.5f;

        [Header("Option 2.1 - Nova Pierce  [55 EN, CD 12s, 5s, +50% ATK, +25% Tỉ lệ sát thương, +75% Xuyên giáp]")]
        [SerializeField] private GameObject novaPierceVisual;
        [SerializeField, Range(0f, 3f)] private float novaPierceAttackPercent = 0.50f;
        [SerializeField, Range(0f, 2f)] private float novaPierceDamageRateBonus = 0.25f;
        [SerializeField, Range(0f, 1f)] private float novaPierceArmorPenBonus = 0.75f;
        [SerializeField, Min(0.1f)] private float novaPierceDuration = 5f;
        [SerializeField, Min(0f)] private float novaPierceEnergyCost = 55f;
        [SerializeField, Min(0.01f)] private float novaPierceCooldown = 12f;

        [Header("Option 2.2 - Nova Rush  [35 EN, CD 7s, đòn kế tiếp +200% ATK]")]
        [SerializeField] private GameObject novaRushVisual;
        [SerializeField, Min(0.5f)] private float novaRushMaxDistance = 4f;
        [SerializeField, Range(0f, 5f)] private float novaRushBonusAttackPercent = 2f;
        [SerializeField, Min(0f)] private float novaRushEnergyCost = 35f;
        [SerializeField, Min(0.01f)] private float novaRushCooldown = 7f;
        [SerializeField, Range(3, 10)] private int novaRushShadowCount = 6;
        [SerializeField, Min(0.1f)] private float novaRushShadowFadeDuration = 0.35f;
        #endregion

        [Header("VFX (Assets/Art/Tank/VFX/RedNova)")]
        [SerializeField] private RedNovaVfx vfx;

        // --- Pools ---
        private readonly Stack<Skill_NovaShot> m_NovaShotPool = new Stack<Skill_NovaShot>();
        private readonly HashSet<Skill_NovaShot> m_AllNovaShots = new HashSet<Skill_NovaShot>();

        private readonly Stack<Skill_LaserCannon> m_LaserPool = new Stack<Skill_LaserCannon>();
        private readonly HashSet<Skill_LaserCannon> m_AllLasers = new HashSet<Skill_LaserCannon>();

        private readonly Stack<Skill_NovaBarrage> m_BarragePool = new Stack<Skill_NovaBarrage>();
        private readonly HashSet<Skill_NovaBarrage> m_AllBarrages = new HashSet<Skill_NovaBarrage>();

        // --- Runtime States ---
        private bool m_IsOverdriveActive;
        private bool m_IsLaserCannonActive;
        private bool m_IsExecutionCoreActive;

        /// <summary>Đòn kế tiếp sau Nova Rush được cộng thêm sát thương.</summary>
        private bool m_HasNovaRushEmpoweredShot;

        /// <summary>Execution Core hạ gục mục tiêu → lần thi triển kế tiếp miễn năng lượng.</summary>
        private bool m_ExecutionCoreFreeCast;

        private Coroutine m_OverdriveCoroutine;
        private Coroutine m_ExecutionCoroutine;
        private Coroutine m_NovaPierceCoroutine;

        #region Cooldown
        protected override float GetBaseCooldown(int index)
        {
            switch (index)
            {
                case SkillNovaShotIndex:
                    return m_IsLaserCannonActive ? laserShotCooldown : novaShotCooldown;

                case SkillTripleNovaIndex:
                    return tripleNovaCooldown;

                case SkillOption1Index:
                    switch (ResolveOption1Branch())
                    {
                        case 0: return overdriveCooldown;
                        case 1: return laserCannonCooldown;
                        default: return executionCoreCooldown;
                    }

                case SkillOption2Index:
                    switch (ResolveOption2Branch())
                    {
                        case 0: return novaBarrageCooldown;
                        case 1: return novaPierceCooldown;
                        default: return novaRushCooldown;
                    }

                default:
                    return base.GetBaseCooldown(index);
            }
        }

        private int ResolveOption1Branch()
        {
            int fromContext = Context.OptionSkill1;
            return fromContext != 0 ? fromContext : optionSkill1Branch;
        }

        private int ResolveOption2Branch()
        {
            int fromContext = Context.OptionSkill2;
            return fromContext != 0 ? fromContext : optionSkill2Branch;
        }

        /// <summary>
        /// Chi phí năng lượng của từng kỹ năng theo GDD. Lớp cha mặc định 30 EN cho mọi slot
        /// khác 0, nên nếu không ghi đè thì mỗi chiêu sẽ bị trừ thêm 30 EN.
        /// Laser Cannon: bật/tắt miễn phí, mỗi phát trừ riêng trong FireLaserBeam().
        /// </summary>
        protected override float GetSkillEnergyCost(int index)
        {
            switch (index)
            {
                case 1:
                    return tripleNovaEnergyCost;

                case 2:
                    switch (ResolveOption1Branch())
                    {
                        case 1: return 0f;
                        case 2: return executionCoreEnergyCost;
                        default: return overdriveEnergyCost;
                    }

                case 3:
                    switch (ResolveOption2Branch())
                    {
                        case 1: return novaPierceEnergyCost;
                        case 2: return novaRushEnergyCost;
                        default: return novaBarrageEnergyCost;
                    }

                default:
                    return 0f;
            }
        }
        #endregion

        private void Start()
        {
            if (vfx == null)
                vfx = GetComponent<RedNovaVfx>();

            PrewarmNovaShotPool();
            PrewarmLaserPool();
            PrewarmBarragePool();
        }

        protected override void OnUseSkill(int index)
        {
            switch (index)
            {
                case SkillNovaShotIndex:
                    if (m_IsLaserCannonActive && laserCannonDesign != null)
                        FireLaserBeam();
                    else
                        FireNovaShot();
                    return;

                case SkillTripleNovaIndex:
                    FireTripleNova();
                    return;

                case SkillOption1Index:
                    UseOptionSkill1();
                    return;

                case SkillOption2Index:
                    UseOptionSkill2();
                    return;

                default:
                    base.OnUseSkill(index);
                    return;
            }
        }

        #region Damage Helper
        /// <summary>
        /// Sát thương gốc theo GDD 4.1: Tấn công × Scale kỹ năng × (1 + Tỉ lệ sát thương).
        /// </summary>
        private float CalculateRawDamage(float attackCoefficient, float flatAttackPercentBonus = 0f)
        {
            float attack = Stats.Attack * (1f + flatAttackPercentBonus);
            return attack * attackCoefficient * (1f + Stats.DamageRate);
        }

        /// <summary>
        /// Gây sát thương qua đúng đường ống CombatUtility (giáp → xuyên giáp → khiên),
        /// rồi xử lý các buff "mất khi gây sát thương" và hiệu ứng hạ gục.
        /// </summary>
        private void ApplySkillDamage(EnemyBrain enemy, float rawDamage, Vector2 hitPosition)
        {
            if (enemy == null || !enemy.IsAlive)
                return;

            DamageInfo info = new DamageInfo(this, enemy, rawDamage, Stats.ArmorPenetration, hitPosition);
            enemy.TakeDamage(info);

            TextDamageManager.Instance?.Show(info.FinalDamage, hitPosition);

            // Overdrive / Execution Core: hiệu ứng mất ngay khi gây sát thương.
            if (m_HasNovaRushEmpoweredShot)
                m_HasNovaRushEmpoweredShot = false;

            if (m_IsOverdriveActive)
                RemoveOverdriveDamageBuff();

            if (m_IsExecutionCoreActive)
            {
                if (!enemy.IsAlive)
                    OnExecutionCoreKill();
                else
                    RemoveExecutionCoreBuff();
            }
        }

        private void OnExecutionCoreKill()
        {
            RemoveExecutionCoreBuff();
            m_ExecutionCoreFreeCast = true;
            ResetSkillCooldown(SkillOption1Index);

            if (vfx != null)
                vfx.PlayExecutionShockwave(transform.position);
        }

        private void RemoveOverdriveDamageBuff()
        {
            AddBonusStats(BonusKeyOverdrive, TankStats.Zeroed());
        }

        private void RemoveExecutionCoreBuff()
        {
            if (!m_IsExecutionCoreActive)
                return;

            m_IsExecutionCoreActive = false;
            RemoveBonusStats(BonusKeyExecution);
            if (executionCoreVisual != null)
                executionCoreVisual.SetActive(false);

            if (vfx != null)
                vfx.SetExecutionCoreActive(false);
        }
        #endregion

        #region Skill 0 & 1 - Nova Shot & Triple Nova
        private void PrewarmNovaShotPool()
        {
            Skill_NovaShot design = novaShotDesign != null ? novaShotDesign : tripleNovaDesign;
            if (design == null)
                return;

            for (int i = 0; i < novaShotPoolSize; i++)
            {
                Skill_NovaShot instance = CreateNovaShotInstance(design);
                if (instance != null)
                    m_NovaShotPool.Push(instance);
            }
        }

        private Skill_NovaShot CreateNovaShotInstance(Skill_NovaShot design)
        {
            if (design == null)
                design = novaShotDesign != null ? novaShotDesign : tripleNovaDesign;

            if (design == null)
                return null;

            Skill_NovaShot skill = Instantiate(design, transform.position, Quaternion.identity);
            if (skill == null)
                return null;

            skill.transform.SetParent(null);
            skill.gameObject.SetActive(true);
            skill.Clear();
            skill.gameObject.SetActive(false);

            m_AllNovaShots.Add(skill);
            return skill;
        }

        private Skill_NovaShot GetNovaShotFromPool()
        {
            while (m_NovaShotPool.Count > 0)
            {
                Skill_NovaShot skill = m_NovaShotPool.Pop();
                if (skill != null)
                    return skill;
            }

            return CreateNovaShotInstance(novaShotDesign);
        }

        private void ReturnNovaShotToPool(Skill_NovaShot skill)
        {
            if (skill == null)
                return;

            skill.Clear();
            skill.gameObject.SetActive(false);

            if (!m_NovaShotPool.Contains(skill))
                m_NovaShotPool.Push(skill);
        }

        private void FireNovaShot()
        {
            if (novaShotDesign == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_RedNova)}] Chưa gán NovaShot Design.", this);
                return;
            }

            if (View == null || View.Muzzle == null || !HasLookTarget)
                return;

            Transform muzzle = View.Muzzle;
            Vector2 direction = LookTarget - (Vector2)muzzle.position;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            direction.Normalize();
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            Skill_NovaShot skill = GetNovaShotFromPool();
            if (skill == null)
                return;

            skill.transform.SetParent(null);
            skill.transform.SetPositionAndRotation(muzzle.position, rotation);

            skill.Initialize(direction, ReturnNovaShotToPool);
            skill.TargetFound += OnNovaShotTargetFound;

            skill.gameObject.SetActive(true);
        }

        private void OnNovaShotTargetFound(Skill_NovaShot skill, EnemyBrain enemy)
        {
            if (enemy == null)
                return;

            // Nova Rush: đòn kế tiếp được cộng thêm ATK (GDD: +200%).
            float rushBonus = m_HasNovaRushEmpoweredShot ? novaRushBonusAttackPercent : 0f;

            ApplySkillDamage(enemy, CalculateRawDamage(novaShotDamageMultiplier, rushBonus), skill.transform.position);
            GainEnergy(novaShotEnergyGain);
        }

        private void FireTripleNova()
        {
            // Theo GDD: đang ở trạng thái Laser Cannon thì không thể dùng Triple Nova.
            if (m_IsLaserCannonActive)
            {
                Debug.Log($"[{nameof(TankBrain_RedNova)}] Đang ở trạng thái Laser Cannon, không thể dùng Triple Nova.");
                return;
            }

            Skill_NovaShot design = tripleNovaDesign != null ? tripleNovaDesign : novaShotDesign;
            if (design == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_RedNova)}] Chưa gán TripleNova / NovaShot Design.", this);
                return;
            }

            if (View == null || View.Muzzle == null || !HasLookTarget)
                return;


            Transform muzzle = View.Muzzle;
            Vector2 baseDirection = LookTarget - (Vector2)muzzle.position;
            if (baseDirection.sqrMagnitude <= 0.0001f)
                return;

            baseDirection.Normalize();
            float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;

            int count = Mathf.Max(1, tripleNovaBulletCount);

            for (int i = 0; i < count; i++)
            {
                float offsetAngle = 0f;
                if (count > 1)
                {
                    float t = (float)i / (count - 1) - 0.5f; // -0.5 .. +0.5
                    offsetAngle = t * 2f * tripleNovaSpreadAngle;
                }

                float angle = baseAngle + offsetAngle;
                float rad = angle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

                Skill_NovaShot bullet = GetNovaShotFromPool();
                if (bullet == null)
                    continue;

                bullet.transform.SetParent(null);
                bullet.transform.SetPositionAndRotation(muzzle.position, rotation);

                bullet.Initialize(dir, ReturnNovaShotToPool);
                bullet.TargetFound += OnTripleNovaTargetFound;

                bullet.gameObject.SetActive(true);
            }
        }

        private void OnTripleNovaTargetFound(Skill_NovaShot skill, EnemyBrain enemy)
        {
            if (enemy == null)
                return;

            float rushBonus = m_HasNovaRushEmpoweredShot ? novaRushBonusAttackPercent : 0f;
            ApplySkillDamage(enemy, CalculateRawDamage(tripleNovaDamageMultiplier, rushBonus), skill.transform.position);
        }
        #endregion

        #region Skill 2 - Option Skill 1 (Overdrive / Laser Cannon / Execution Core)
        private void PrewarmLaserPool()
        {
            if (laserCannonDesign == null)
                return;

            for (int i = 0; i < laserCannonPoolSize; i++)
            {
                Skill_LaserCannon instance = CreateLaserInstance();
                if (instance != null)
                    m_LaserPool.Push(instance);
            }
        }

        private Skill_LaserCannon CreateLaserInstance()
        {
            if (laserCannonDesign == null)
                return null;

            Skill_LaserCannon skill = Instantiate(laserCannonDesign, transform.position, Quaternion.identity);
            if (skill == null)
                return null;

            skill.transform.SetParent(null);
            skill.gameObject.SetActive(true);
            skill.Clear();
            skill.gameObject.SetActive(false);

            m_AllLasers.Add(skill);
            return skill;
        }

        private Skill_LaserCannon GetLaserFromPool()
        {
            while (m_LaserPool.Count > 0)
            {
                Skill_LaserCannon skill = m_LaserPool.Pop();
                if (skill != null)
                    return skill;
            }

            return CreateLaserInstance();
        }

        private void ReturnLaserToPool(Skill_LaserCannon skill)
        {
            if (skill == null)
                return;

            skill.Clear();
            skill.gameObject.SetActive(false);

            if (!m_LaserPool.Contains(skill))
                m_LaserPool.Push(skill);
        }

        private void UseOptionSkill1()
        {
            switch (ResolveOption1Branch())
            {
                case 0:
                            ActivateOverdrive();
                    break;

                case 1:
                    // Laser Cannon là trạng thái bật/tắt, không tốn năng lượng khi bật;
                    // mỗi phát bắn thường trong trạng thái tốn 5 năng lượng.
                    ActivateLaserCannonMode();
                    break;

                case 2:
                default:
                    if (m_ExecutionCoreFreeCast)
                        m_ExecutionCoreFreeCast = false;
                    else
            
                    ActivateExecutionCore();
                    break;
            }
        }

        private void ActivateOverdrive()
        {
            if (m_OverdriveCoroutine != null)
                StopCoroutine(m_OverdriveCoroutine);

            // Đặt lại thời gian hồi đòn đánh thường (GDD).
            ResetSkillCooldown(SkillNovaShotIndex);

            m_OverdriveCoroutine = StartCoroutine(OverdriveRoutine());
        }

        private IEnumerator OverdriveRoutine()
        {
            m_IsOverdriveActive = true;

            TankStats percent = TankStats.Zeroed();
            percent.MoveSpeed = overdriveMoveSpeedPercent;
            TankStats flat = TankStats.Zeroed();
            flat.DamageRate = overdriveDamageRateBonus;
            AddBonusStats(BonusKeyOverdrive, flat, percent);

            if (overdriveVisual != null)
                overdriveVisual.SetActive(true);

            if (vfx != null)
                vfx.SetOverdriveActive(true);

            yield return new WaitForSeconds(overdriveDuration);

            if (overdriveVisual != null)
                overdriveVisual.SetActive(false);

            if (vfx != null)
                vfx.SetOverdriveActive(false);

            RemoveBonusStats(BonusKeyOverdrive);
            m_IsOverdriveActive = false;
            m_OverdriveCoroutine = null;
        }

        private void ActivateLaserCannonMode()
        {
            if (m_IsLaserCannonActive)
            {
                // Dùng lại kỹ năng để tắt trạng thái (GDD).
                m_IsLaserCannonActive = false;
                return;
            }

            m_IsLaserCannonActive = true;
            FireLaserBeam();
        }

        private void FireLaserBeam()
        {
            if (laserCannonDesign == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_RedNova)}] Chưa gán LaserCannon Design.", this);
                return;
            }

            if (View == null || View.Muzzle == null || !HasLookTarget)
                return;

            SpendEnergy(laserShotEnergyCost);

            Transform muzzle = View.Muzzle;
            Vector2 direction = LookTarget - (Vector2)muzzle.position;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            Skill_LaserCannon beam = GetLaserFromPool();
            if (beam == null)
                return;

            if (vfx != null)
                vfx.ShowLaserBeam(muzzle.position, LookTarget);

            beam.transform.SetParent(null);
            beam.gameObject.SetActive(true);
            beam.Initialize(muzzle.position, direction, ReturnLaserToPool);
            beam.TargetFound += OnLaserTargetFound;
            beam.StartSkill();
        }

        private void OnLaserTargetFound(Skill_LaserCannon beam, EnemyBrain enemy)
        {
            if (enemy == null)
                return;

            ApplySkillDamage(enemy, CalculateRawDamage(laserCannonDamageMultiplier), enemy.transform.position);
            GainEnergy(novaShotEnergyGain);
        }

        private void ActivateExecutionCore()
        {
            if (m_ExecutionCoroutine != null)
                StopCoroutine(m_ExecutionCoroutine);

            m_ExecutionCoroutine = StartCoroutine(ExecutionCoreRoutine());
        }

        private IEnumerator ExecutionCoreRoutine()
        {
            m_IsExecutionCoreActive = true;

            TankStats flat = TankStats.Zeroed();
            flat.DamageRate = executionDamageRateBonus;
            flat.ArmorPenetration = executionArmorPenBonus;
            AddBonusStats(BonusKeyExecution, flat);

            if (executionCoreVisual != null)
                executionCoreVisual.SetActive(true);

            if (vfx != null)
                vfx.SetExecutionCoreActive(true);

            // Tồn tại tới khi gây sát thương hoặc hết hồi chiêu kế tiếp (GDD không ghi thời hạn,
            // dùng đúng bằng hồi chiêu 10s làm chặn trên an toàn).
            yield return new WaitForSeconds(executionCoreCooldown);

            RemoveExecutionCoreBuff();
            m_ExecutionCoroutine = null;
        }
        #endregion

        #region Skill 3 - Option Skill 2 (Nova Barrage / Nova Pierce / Nova Rush)
        private void PrewarmBarragePool()
        {
            if (novaBarrageDesign == null)
                return;

            for (int i = 0; i < novaBarragePoolSize; i++)
            {
                Skill_NovaBarrage instance = CreateBarrageInstance();
                if (instance != null)
                    m_BarragePool.Push(instance);
            }
        }

        private Skill_NovaBarrage CreateBarrageInstance()
        {
            if (novaBarrageDesign == null)
                return null;

            Skill_NovaBarrage skill = Instantiate(novaBarrageDesign, transform.position, Quaternion.identity);
            if (skill == null)
                return null;

            skill.transform.SetParent(null);
            skill.gameObject.SetActive(true);
            skill.Clear();
            skill.gameObject.SetActive(false);

            m_AllBarrages.Add(skill);
            return skill;
        }

        private Skill_NovaBarrage GetBarrageFromPool()
        {
            while (m_BarragePool.Count > 0)
            {
                Skill_NovaBarrage skill = m_BarragePool.Pop();
                if (skill != null)
                    return skill;
            }

            return CreateBarrageInstance();
        }

        private void ReturnBarrageToPool(Skill_NovaBarrage skill)
        {
            if (skill == null)
                return;

            skill.Clear();
            skill.gameObject.SetActive(false);

            if (!m_BarragePool.Contains(skill))
                m_BarragePool.Push(skill);
        }

        private void UseOptionSkill2()
        {
            switch (ResolveOption2Branch())
            {
                case 0:
                default:
                            FireNovaBarrage();
                    break;

                case 1:
                            ActivateNovaPierce();
                    break;

                case 2:
                            PerformNovaRush();
                    break;
            }
        }

        private void FireNovaBarrage()
        {
            if (novaBarrageDesign == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_RedNova)}] Chưa gán NovaBarrage Design.", this);
                return;
            }

            if (!HasLookTarget)
                return;

            Skill_NovaBarrage skill = GetBarrageFromPool();
            if (skill == null)
                return;

            Vector2 spawnPos = LookTarget;
            skill.transform.SetParent(null);
            skill.transform.position = spawnPos;
            skill.gameObject.SetActive(true);

            skill.Initialize(spawnPos, ReturnBarrageToPool);
            skill.Duration = novaBarrageDuration;
            skill.Interval = novaBarrageInterval;
            skill.TargetFound += OnBarrageTargetFound;
            skill.StartSkill();
        }

        private void OnBarrageTargetFound(Skill_NovaBarrage skill, EnemyBrain enemy)
        {
            if (enemy == null)
                return;

            float damage = CalculateRawDamage(novaBarrageDamageMultiplier);

            Collider2D enemyCollider = enemy.Model != null ? enemy.Model.BodyCollider : null;
            if (enemyCollider != null &&
                Collider2DUtility.TryGetRandomOverlapPoint(skill.ColliderRef.Collider, enemyCollider, out Vector2 center))
            {
                ApplySkillDamage(enemy, damage, center);
            }
            else
            {
                ApplySkillDamage(enemy, damage, skill.transform.position);
            }
        }

        private void ActivateNovaPierce()
        {
            if (m_NovaPierceCoroutine != null)
                StopCoroutine(m_NovaPierceCoroutine);

            m_NovaPierceCoroutine = StartCoroutine(NovaPierceRoutine());
        }

        private IEnumerator NovaPierceRoutine()
        {
            TankStats percent = TankStats.Zeroed();
            percent.Attack = novaPierceAttackPercent;

            TankStats flat = TankStats.Zeroed();
            flat.DamageRate = novaPierceDamageRateBonus;
            flat.ArmorPenetration = novaPierceArmorPenBonus;

            AddBonusStats(BonusKeyNovaPierce, flat, percent);

            if (novaPierceVisual != null)
                novaPierceVisual.SetActive(true);

            if (vfx != null)
                vfx.SetPierceActive(true);

            yield return new WaitForSeconds(novaPierceDuration);

            if (novaPierceVisual != null)
                novaPierceVisual.SetActive(false);

            if (vfx != null)
                vfx.SetPierceActive(false);

            RemoveBonusStats(BonusKeyNovaPierce);
            m_NovaPierceCoroutine = null;
        }

        private void PerformNovaRush()
        {
            if (!HasLookTarget)
                return;

            Vector2 currentPos = transform.position;
            Vector2 delta = LookTarget - currentPos;
            if (delta.sqrMagnitude <= 0.0001f)
                return;

            Vector2 targetPos = delta.magnitude > novaRushMaxDistance
                ? currentPos + delta.normalized * novaRushMaxDistance
                : LookTarget;

            SpawnNovaRushShadows(currentPos, targetPos);

            if (vfx != null)
                vfx.PlayRushTrail(currentPos, targetPos);

            transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);

            // Đòn tấn công thành công ngay sau cú lướt gây thêm sát thương (GDD: +200% ATK).
            m_HasNovaRushEmpoweredShot = true;
        }

        private void SpawnNovaRushShadows(Vector2 startPos, Vector2 endPos)
        {
            if (novaRushVisual == null)
                return;

            Vector2 dir = endPos - startPos;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Quaternion rot = Quaternion.Euler(0f, 0f, angle);

            int count = Mathf.Clamp(novaRushShadowCount, 2, 10);
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                Vector2 spawnPos = Vector2.Lerp(startPos, endPos, t);

                GameObject shadow = Instantiate(novaRushVisual, spawnPos, rot);
                shadow.transform.SetParent(null);
                shadow.SetActive(true);

                StartCoroutine(FadeAndDestroyShadowRoutine(shadow, novaRushShadowFadeDuration));
            }
        }

        private IEnumerator FadeAndDestroyShadowRoutine(GameObject shadow, float duration)
        {
            if (shadow == null)
                yield break;

            SpriteRenderer[] renderers = shadow.GetComponentsInChildren<SpriteRenderer>(true);
            Color[] initialColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    initialColors[i] = renderers[i].color;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float norm = Mathf.Clamp01(elapsed / duration);

                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        Color c = initialColors[i];
                        c.a = Mathf.Lerp(initialColors[i].a, 0f, norm);
                        renderers[i].color = c;
                    }
                }

                yield return null;
            }

            if (shadow != null)
                Destroy(shadow);
        }
        #endregion

        #region Test API (Skill Arena)
        /// <summary>Nhánh kỹ năng tùy chọn 1 đang dùng (đổi lúc chạy để test).</summary>
        public int Option1Branch
        {
            get => optionSkill1Branch;
            set
            {
                optionSkill1Branch = Mathf.Clamp(value, 0, 2);
                ResetSkillCooldown(SkillOption1Index);
            }
        }

        /// <summary>Nhánh kỹ năng tùy chọn 2 đang dùng (đổi lúc chạy để test).</summary>
        public int Option2Branch
        {
            get => optionSkill2Branch;
            set
            {
                optionSkill2Branch = Mathf.Clamp(value, 0, 2);
                ResetSkillCooldown(SkillOption2Index);
            }
        }

        public string GetSkillName(int slotIndex)
        {
            switch (slotIndex)
            {
                case SkillNovaShotIndex:
                    return m_IsLaserCannonActive ? "Nova Shot (LASER ON)" : "Nova Shot";

                case SkillTripleNovaIndex:
                    return "Triple Nova";

                case SkillOption1Index:
                    switch (optionSkill1Branch)
                    {
                        case 0: return "Overdrive";
                        case 1: return "Laser Cannon";
                        default: return "Execution Core";
                    }

                case SkillOption2Index:
                    switch (optionSkill2Branch)
                    {
                        case 0: return "Nova Barrage";
                        case 1: return "Nova Pierce";
                        default: return "Nova Rush";
                    }

                default:
                    return $"Skill {slotIndex}";
            }
        }
        #endregion

        private void OnDestroy()
        {
            foreach (Skill_NovaShot skill in m_AllNovaShots)
            {
                if (skill != null) Destroy(skill.gameObject);
            }
            m_AllNovaShots.Clear();
            m_NovaShotPool.Clear();

            foreach (Skill_LaserCannon skill in m_AllLasers)
            {
                if (skill != null) Destroy(skill.gameObject);
            }
            m_AllLasers.Clear();
            m_LaserPool.Clear();

            foreach (Skill_NovaBarrage skill in m_AllBarrages)
            {
                if (skill != null) Destroy(skill.gameObject);
            }
            m_AllBarrages.Clear();
            m_BarragePool.Clear();
        }
    }
}
