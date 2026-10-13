using IronWasteland.Enemies;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.RedNova
{
    /// <summary>
    /// TankBrain cụ thể cho xe tăng "Red Nova" (DPS / DPS):
    /// - Slot 0 (J): Nova Shot — Bắn luồng đạn năng lượng + hồi Energy (hoặc bắn tia Laser nếu đang bật Laser Cannon).
    /// - Slot 1 (K): Triple Nova — Bắn đồng thời 3 viên đạn năng lượng phân bổ sát thương (bị khóa khi bật Laser Cannon).
    /// - Slot 2 (L): Kỹ năng tùy chọn 1 (0: Overdrive, 1: Laser Cannon, 2: Execution Core).
    /// - Slot 3 (I): Kỹ năng tùy chọn 2 (0: Nova Barrage, 1: Nova Pierce, 2: Nova Rush).
    /// </summary>
    [DisallowMultipleComponent]
    public class TankBrain_RedNova : TankBrain
    {
        private const int SkillNovaShotIndex = 0;
        private const int SkillTripleNovaIndex = 1;
        private const int SkillOption1Index = 2;
        private const int SkillOption2Index = 3;

        private const string BonusKeyOverdrive = "RedNova_Overdrive";
        private const string BonusKeyNovaPierce = "RedNova_NovaPierce";

        #region Inspector - Skill 0: Nova Shot
        [Header("Skill 0 (J) - Nova Shot")]
        [Tooltip("GameObject mẫu inactive của đạn Nova Shot.")]
        [SerializeField] private Skill_NovaShot novaShotDesign;
        [SerializeField, Min(0)] private int novaShotPoolSize = 8;
        [SerializeField, Min(0f)] private float novaShotDamage = 32f;
        [SerializeField, Min(0f)] private float novaShotEnergyGain = 6f;
        [SerializeField, Min(0.01f)] private float novaShotCooldown = 0.8f;
        #endregion

        #region Inspector - Skill 1: Triple Nova
        [Header("Skill 1 (K) - Triple Nova")]
        [Tooltip("GameObject mẫu inactive của đạn Triple Nova (nếu để trống sẽ dùng chung novaShotDesign).")]
        [SerializeField] private Skill_NovaShot tripleNovaDesign;
        [SerializeField, Min(1)] private int tripleNovaBulletCount = 3;
        [SerializeField, Range(0f, 45f)] private float tripleNovaSpreadAngle = 10f;
        [SerializeField, Min(0f)] private float tripleNovaTotalDamage = 66f;
        [SerializeField, Min(0f)] private float tripleNovaEnergyCost = 15f;
        [SerializeField, Min(0.01f)] private float tripleNovaCooldown = 4f;
        #endregion

        #region Inspector - Skill 2: Option Skill 1
        [Header("Skill 2 (L) - Option Skill 1 (0: Overdrive, 1: LaserCannon, 2: ExecutionCore)")]
        [Tooltip("Nhánh kỹ năng tùy chọn 1 mặc định trên Inspector (0, 1 hoặc 2).")]
        [SerializeField, Range(0, 2)] private int optionSkill1Branch = 1;
        [SerializeField, Min(0f)] private float optionSkill1EnergyCost = 20f;
        [SerializeField, Min(0.01f)] private float optionSkill1Cooldown = 8f;

        [Header("Option 1.0 - Overdrive")]
        [SerializeField] private GameObject overdriveVisual;
        [SerializeField, Min(0f)] private float overdriveMoveSpeedBonus = 2f;
        [SerializeField, Range(0.1f, 1f)] private float overdriveNormalShotCooldownMultiplier = 0.5f;
        [SerializeField, Min(0.1f)] private float overdriveDuration = 4f;

        [Header("Option 1.1 - Laser Cannon")]
        [Tooltip("GameObject mẫu inactive của tia Laser Cannon.")]
        [SerializeField] private Skill_LaserCannon laserCannonDesign;
        [SerializeField, Min(0)] private int laserCannonPoolSize = 2;
        [SerializeField, Min(0f)] private float laserCannonDamage = 55f;
        [SerializeField, Min(0.1f)] private float laserShotCooldown = 1.6f;
        [SerializeField, Min(0.1f)] private float laserCannonStateDuration = 6f;

        [Header("Option 1.2 - Execution Core")]
        [SerializeField] private GameObject executionCoreVisual;
        [SerializeField, Range(0.1f, 0.9f)] private float executionLowHpThreshold = 0.4f;
        [SerializeField, Min(1f)] private float executionDamageMultiplier = 1.5f;
        [SerializeField, Min(0.1f)] private float executionCoreDuration = 5f;
        #endregion

        #region Inspector - Skill 3: Option Skill 2
        [Header("Skill 3 (I) - Option Skill 2 (0: NovaBarrage, 1: NovaPierce, 2: NovaRush)")]
        [Tooltip("Nhánh kỹ năng tùy chọn 2 mặc định trên Inspector (0, 1 hoặc 2).")]
        [SerializeField, Range(0, 2)] private int optionSkill2Branch = 0;
        [SerializeField, Min(0f)] private float optionSkill2EnergyCost = 20f;
        [SerializeField, Min(0.01f)] private float optionSkill2Cooldown = 9f;

        [Header("Option 2.0 - Nova Barrage")]
        [Tooltip("GameObject mẫu inactive của vùng oanh kích Nova Barrage.")]
        [SerializeField] private Skill_NovaBarrage novaBarrageDesign;
        [SerializeField, Min(0)] private int novaBarragePoolSize = 2;
        [SerializeField, Min(0f)] private float novaBarrageDamagePerTick = 14f;

        [Header("Option 2.1 - Nova Pierce")]
        [SerializeField] private GameObject novaPierceVisual;
        [SerializeField, Range(0f, 1f)] private float novaPierceArmorPenBonus = 0.35f;
        [SerializeField, Min(0.1f)] private float novaPierceDuration = 5f;

        [Header("Option 2.2 - Nova Rush")]
        [SerializeField] private GameObject novaRushVisual;
        [SerializeField, Min(0.5f)] private float novaRushMaxDistance = 4f;
        [SerializeField, Min(0f)] private float novaRushBonusDamage = 25f;
        #endregion

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
        private bool m_HasNovaRushEmpoweredShot;

        private Coroutine m_OverdriveCoroutine;
        private Coroutine m_LaserStateCoroutine;
        private Coroutine m_ExecutionCoroutine;
        private Coroutine m_NovaPierceCoroutine;

        protected override float GetBaseCooldown(int index)
        {
            switch (index)
            {
                case SkillNovaShotIndex:
                    if (m_IsLaserCannonActive)
                        return laserShotCooldown;
                    if (m_IsOverdriveActive)
                        return novaShotCooldown * overdriveNormalShotCooldownMultiplier;
                    return novaShotCooldown;

                case SkillTripleNovaIndex:
                    return tripleNovaCooldown;

                case SkillOption1Index:
                    return optionSkill1Cooldown;

                case SkillOption2Index:
                    return optionSkill2Cooldown;

                default:
                    return base.GetBaseCooldown(index);
            }
        }

        private void Start()
        {
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

            float damage = CalculateFinalAttackDamage(novaShotDamage, enemy);
            enemy.TakeDamage(damage);
            GainEnergy(novaShotEnergyGain);

            TextDamageManager.Instance?.Show(damage, skill.transform.position);
        }

        private void FireTripleNova()
        {
            // Trong trạng thái Laser Cannon, không thể sử dụng Triple Nova (theo GDD).
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

            SpendEnergy(tripleNovaEnergyCost);

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

            int count = Mathf.Max(1, tripleNovaBulletCount);
            float perBulletDamage = tripleNovaTotalDamage / count;
            float finalDamage = CalculateFinalAttackDamage(perBulletDamage, enemy);

            enemy.TakeDamage(finalDamage);
            TextDamageManager.Instance?.Show(finalDamage, skill.transform.position);
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
            SpendEnergy(optionSkill1EnergyCost);

            int branch = Context.OptionSkill1 != 0 ? Context.OptionSkill1 : optionSkill1Branch;

            switch (branch)
            {
                case 0:
                    ActivateOverdrive();
                    break;
                case 1:
                    ActivateLaserCannonMode();
                    break;
                case 2:
                default:
                    ActivateExecutionCore();
                    break;
            }
        }

        private void ActivateOverdrive()
        {
            if (m_OverdriveCoroutine != null)
                StopCoroutine(m_OverdriveCoroutine);

            m_OverdriveCoroutine = StartCoroutine(OverdriveRoutine());
        }

        private IEnumerator OverdriveRoutine()
        {
            m_IsOverdriveActive = true;

            TankStats bonus = TankStats.Zeroed();
            bonus.MoveSpeed = overdriveMoveSpeedBonus;
            AddBonusStats(BonusKeyOverdrive, bonus);

            if (overdriveVisual != null)
                overdriveVisual.SetActive(true);

            yield return new WaitForSeconds(overdriveDuration);

            if (overdriveVisual != null)
                overdriveVisual.SetActive(false);

            RemoveBonusStats(BonusKeyOverdrive);
            m_IsOverdriveActive = false;
            m_OverdriveCoroutine = null;
        }

        private void ActivateLaserCannonMode()
        {
            if (m_LaserStateCoroutine != null)
                StopCoroutine(m_LaserStateCoroutine);

            m_LaserStateCoroutine = StartCoroutine(LaserCannonStateRoutine());

            // Bắn ngay một phát tia Laser khi kích hoạt để người chơi thấy hiệu ứng tức thì.
            FireLaserBeam();
        }

        private IEnumerator LaserCannonStateRoutine()
        {
            m_IsLaserCannonActive = true;

            yield return new WaitForSeconds(laserCannonStateDuration);

            m_IsLaserCannonActive = false;
            m_LaserStateCoroutine = null;
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

            Transform muzzle = View.Muzzle;
            Vector2 direction = LookTarget - (Vector2)muzzle.position;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            Skill_LaserCannon beam = GetLaserFromPool();
            if (beam == null)
                return;

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

            float damage = CalculateFinalAttackDamage(laserCannonDamage, enemy);
            enemy.TakeDamage(damage);
            GainEnergy(novaShotEnergyGain);

            TextDamageManager.Instance?.Show(damage, enemy.transform.position);
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
            if (executionCoreVisual != null)
                executionCoreVisual.SetActive(true);

            yield return new WaitForSeconds(executionCoreDuration);

            if (executionCoreVisual != null)
                executionCoreVisual.SetActive(false);

            m_IsExecutionCoreActive = false;
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
            SpendEnergy(optionSkill2EnergyCost);

            int branch = Context.OptionSkill2 != 0 ? Context.OptionSkill2 : optionSkill2Branch;

            switch (branch)
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
            skill.TargetFound += OnBarrageTargetFound;
            skill.StartSkill();
        }

        private void OnBarrageTargetFound(Skill_NovaBarrage skill, EnemyBrain enemy)
        {
            if (enemy == null)
                return;

            float damage = CalculateFinalAttackDamage(novaBarrageDamagePerTick, enemy);
            enemy.TakeDamage(damage);

            Collider2D enemyCollider = enemy.Model != null ? enemy.Model.BodyCollider : null;
            if (enemyCollider != null &&
                Collider2DUtility.TryGetRandomOverlapPoint(skill.ColliderRef.Collider, enemyCollider, out Vector2 center))
            {
                TextDamageManager.Instance?.Show(damage, center);
            }
            else
            {
                TextDamageManager.Instance?.Show(damage, skill.transform.position);
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
            TankStats bonus = TankStats.Zeroed();
            bonus.ArmorPenetration = novaPierceArmorPenBonus;
            AddBonusStats(BonusKeyNovaPierce, bonus);

            if (novaPierceVisual != null)
                novaPierceVisual.SetActive(true);

            yield return new WaitForSeconds(novaPierceDuration);

            if (novaPierceVisual != null)
                novaPierceVisual.SetActive(false);

            RemoveBonusStats(BonusKeyNovaPierce);
            m_NovaPierceCoroutine = null;
        }

        private void PerformNovaRush()
        {
            if (!HasLookTarget)
                return;

            Vector2 currentPos = transform.position;
            Vector2 delta = LookTarget - currentPos;
            Vector2 targetPos = delta.magnitude > novaRushMaxDistance
                ? currentPos + delta.normalized * novaRushMaxDistance
                : LookTarget;

            transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);
            m_HasNovaRushEmpoweredShot = true;

            if (novaRushVisual != null)
                StartCoroutine(FlashRushVisualRoutine());
        }

        private IEnumerator FlashRushVisualRoutine()
        {
            novaRushVisual.SetActive(true);
            yield return new WaitForSeconds(0.35f);
            if (novaRushVisual != null)
                novaRushVisual.SetActive(false);
        }
        #endregion

        #region Damage Calculation Helper
        private float CalculateFinalAttackDamage(float baseSkillDamage, EnemyBrain target)
        {
            float damage = baseSkillDamage;

            // Đòn tấn công ngay sau Nova Rush gây thêm sát thương.
            if (m_HasNovaRushEmpoweredShot)
            {
                damage += novaRushBonusDamage;
                m_HasNovaRushEmpoweredShot = false;
            }

            // Execution Core: tăng sát thương lên mục tiêu đang có lượng HP thấp.
            if (m_IsExecutionCoreActive && target != null && target.MaxHealth > 0f)
            {
                float hpRatio = target.CurrentHealth / target.MaxHealth;
                if (hpRatio <= executionLowHpThreshold)
                {
                    damage *= executionDamageMultiplier;
                }
            }

            return damage;
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
