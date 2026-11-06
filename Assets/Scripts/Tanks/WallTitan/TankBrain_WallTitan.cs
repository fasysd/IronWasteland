using IronWasteland.Enemies;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.WallTitan
{
    /// <summary>
    /// TankBrain cụ thể cho xe tăng "Wall Titan" (Tanker / Support):
    /// - Slot 0 (J): Earth Break — Phóng khối đá gây sát thương giảm dần theo khoảng cách + hồi năng lượng.
    /// - Slot 1 (K): Earth Wall — Dựng tường đất chặn di chuyển và đạn tại vị trí LookTarget.
    /// - Slot 2 (L): Kỹ năng tùy chọn 1 (0: Stone Fortress, 1: Guardian Wall, 2: Earth Prison).
    /// - Slot 3 (I): Kỹ năng tùy chọn 2 (0: Immovable Titan, 1: Earth Guardian, 2: Earthquake).
    /// </summary>
    [DisallowMultipleComponent]
    public class TankBrain_WallTitan : TankBrain
    {
        private const int SkillEarthBreakIndex = 0;
        private const int SkillEarthWallIndex = 1;
        private const int SkillOption1Index = 2;
        private const int SkillOption2Index = 3;

        private const string BonusKeyStoneFortress = "WallTitan_StoneFortress";
        private const string BonusKeyGuardianWall = "WallTitan_GuardianWall";
        private const string BonusKeyEarthGuardian = "WallTitan_EarthGuardian";

        #region Inspector - Skill 0: Earth Break
        [Header("Skill 0 (J) - Earth Break")]
        [Tooltip("GameObject mẫu inactive của khối đá Earth Break.")]
        [SerializeField] private Skill_EarthBreak earthBreakDesign;
        [SerializeField, Min(0)] private int earthBreakPoolSize = 4;
        [SerializeField, Min(0f)] private float earthBreakMaxDamage = 45f;
        [Tooltip("Tỉ lệ sát thương tối thiểu khi mục tiêu ở xa (0.4 = 40% sát thương gốc).")]
        [SerializeField, Range(0.1f, 1f)] private float earthBreakMinDamageRatio = 0.4f;
        [Tooltip("Khoảng cách tối đa để tính giảm sát thương.")]
        [SerializeField, Min(1f)] private float earthBreakMaxFalloffDistance = 12f;
        [SerializeField, Min(0f)] private float earthBreakEnergyGain = 8f;
        [SerializeField, Min(0.01f)] private float earthBreakCooldown = 1.2f;
        #endregion

        #region Inspector - Skill 1: Earth Wall
        [Header("Skill 1 (K) - Earth Wall")]
        [Tooltip("GameObject mẫu inactive của bức tường đất Earth Wall.")]
        [SerializeField] private Skill_EarthWall earthWallDesign;
        [SerializeField, Min(0)] private int earthWallPoolSize = 2;
        [SerializeField, Min(0f)] private float earthWallEnergyCost = 15f;
        [SerializeField, Min(0.01f)] private float earthWallCooldown = 8f;
        #endregion

        #region Inspector - Skill 2: Option Skill 1
        [Header("Skill 2 (L) - Option Skill 1 (0: StoneFortress, 1: GuardianWall, 2: EarthPrison)")]
        [Tooltip("Nhánh kỹ năng tùy chọn 1 mặc định trên Inspector (0, 1 hoặc 2).")]
        [SerializeField, Range(0, 2)] private int optionSkill1Branch = 2;
        [SerializeField, Min(0f)] private float optionSkill1EnergyCost = 20f;
        [SerializeField, Min(0.01f)] private float optionSkill1Cooldown = 10f;

        [Header("Option 1.0 - Stone Fortress")]
        [SerializeField] private GameObject stoneFortressVisual;
        [SerializeField, Min(0f)] private float stoneFortressHealAmount = 35f;
        [SerializeField, Range(0f, 0.9f)] private float stoneFortressDefenseBonus = 0.25f;
        [SerializeField, Min(0f)] private float stoneFortressMoveSpeedPenalty = 1.2f;
        [SerializeField, Min(0.1f)] private float stoneFortressDuration = 4f;

        [Header("Option 1.1 - Guardian Wall")]
        [SerializeField] private GameObject guardianWallVisual;
        [SerializeField, Range(0f, 0.9f)] private float guardianWallDefenseBonus = 0.2f;
        [SerializeField, Min(0.1f)] private float guardianWallDuration = 4f;

        [Header("Option 1.2 - Earth Prison")]
        [Tooltip("GameObject mẫu inactive của ngục đất Earth Prison.")]
        [SerializeField] private Skill_EarthPrison earthPrisonDesign;
        [SerializeField, Min(0)] private int earthPrisonPoolSize = 2;
        [SerializeField, Min(0f)] private float earthPrisonDamage = 50f;
        #endregion

        #region Inspector - Skill 3: Option Skill 2
        [Header("Skill 3 (I) - Option Skill 2 (0: ImmovableTitan, 1: EarthGuardian, 2: Earthquake)")]
        [Tooltip("Nhánh kỹ năng tùy chọn 2 mặc định trên Inspector (0, 1 hoặc 2).")]
        [SerializeField, Range(0, 2)] private int optionSkill2Branch = 2;
        [SerializeField, Min(0f)] private float optionSkill2EnergyCost = 20f;
        [SerializeField, Min(0.01f)] private float optionSkill2Cooldown = 12f;

        [Header("Option 2.0 - Immovable Titan")]
        [SerializeField] private GameObject immovableTitanVisual;
        [SerializeField, Min(0.1f)] private float immovableTitanDuration = 4f;

        [Header("Option 2.1 - Earth Guardian")]
        [SerializeField] private GameObject earthGuardianVisual;
        [SerializeField, Range(0f, 0.9f)] private float earthGuardianDefenseBonus = 0.15f;
        [SerializeField, Min(0.1f)] private float earthGuardianDuration = 5f;

        [Header("Option 2.2 - Earthquake")]
        [Tooltip("GameObject mẫu inactive của vùng chấn động Earthquake.")]
        [SerializeField] private Skill_Earthquake earthquakeDesign;
        [SerializeField, Min(0)] private int earthquakePoolSize = 2;
        [SerializeField, Min(0f)] private float earthquakeDamagePerTick = 12f;
        #endregion

        // --- Pools ---
        private readonly Stack<Skill_EarthBreak> m_EarthBreakPool = new Stack<Skill_EarthBreak>();
        private readonly HashSet<Skill_EarthBreak> m_AllEarthBreaks = new HashSet<Skill_EarthBreak>();

        private readonly Stack<Skill_EarthWall> m_EarthWallPool = new Stack<Skill_EarthWall>();
        private readonly HashSet<Skill_EarthWall> m_AllEarthWalls = new HashSet<Skill_EarthWall>();

        private readonly Stack<Skill_EarthPrison> m_EarthPrisonPool = new Stack<Skill_EarthPrison>();
        private readonly HashSet<Skill_EarthPrison> m_AllEarthPrisons = new HashSet<Skill_EarthPrison>();

        private readonly Stack<Skill_Earthquake> m_EarthquakePool = new Stack<Skill_Earthquake>();
        private readonly HashSet<Skill_Earthquake> m_AllEarthquakes = new HashSet<Skill_Earthquake>();

        private Coroutine m_StoneFortressCoroutine;
        private Coroutine m_GuardianWallCoroutine;
        private Coroutine m_ImmovableCoroutine;
        private Coroutine m_EarthGuardianCoroutine;

        /// <summary>True khi Wall Titan đang trong trạng thái Immovable Titan (miễn đẩy/kéo).</summary>
        public bool IsImmovable { get; private set; }

        protected override float GetBaseCooldown(int index)
        {
            switch (index)
            {
                case SkillEarthBreakIndex:
                    return earthBreakCooldown;
                case SkillEarthWallIndex:
                    return earthWallCooldown;
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
            PrewarmEarthBreakPool();
            PrewarmEarthWallPool();
            PrewarmEarthPrisonPool();
            PrewarmEarthquakePool();
        }

        protected override void OnUseSkill(int index)
        {
            switch (index)
            {
                case SkillEarthBreakIndex:
                    FireEarthBreak();
                    return;

                case SkillEarthWallIndex:
                    FireEarthWall();
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

        #region Skill 0 - Earth Break
        private void PrewarmEarthBreakPool()
        {
            if (earthBreakDesign == null)
                return;

            for (int i = 0; i < earthBreakPoolSize; i++)
            {
                Skill_EarthBreak instance = CreateEarthBreakInstance();
                if (instance != null)
                    m_EarthBreakPool.Push(instance);
            }
        }

        private Skill_EarthBreak CreateEarthBreakInstance()
        {
            if (earthBreakDesign == null)
                return null;

            Skill_EarthBreak skill = Instantiate(earthBreakDesign, transform.position, Quaternion.identity);
            if (skill == null)
                return null;

            skill.transform.SetParent(null);
            skill.gameObject.SetActive(true);
            skill.Clear();
            skill.gameObject.SetActive(false);

            m_AllEarthBreaks.Add(skill);
            return skill;
        }

        private Skill_EarthBreak GetEarthBreakFromPool()
        {
            while (m_EarthBreakPool.Count > 0)
            {
                Skill_EarthBreak skill = m_EarthBreakPool.Pop();
                if (skill != null)
                    return skill;
            }

            return CreateEarthBreakInstance();
        }

        private void ReturnEarthBreakToPool(Skill_EarthBreak skill)
        {
            if (skill == null)
                return;

            skill.Clear();
            skill.gameObject.SetActive(false);

            if (!m_EarthBreakPool.Contains(skill))
                m_EarthBreakPool.Push(skill);
        }

        private void FireEarthBreak()
        {
            if (earthBreakDesign == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_WallTitan)}] Chưa gán EarthBreak Design.", this);
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

            Skill_EarthBreak skill = GetEarthBreakFromPool();
            if (skill == null)
                return;

            skill.transform.SetParent(null);
            skill.transform.SetPositionAndRotation(muzzle.position, rotation);

            skill.Initialize(muzzle.position, direction, ReturnEarthBreakToPool);
            skill.TargetFound += OnEarthBreakTargetFound;

            skill.gameObject.SetActive(true);
        }

        private void OnEarthBreakTargetFound(Skill_EarthBreak skill, EnemyBrain enemy, float travelDistance)
        {
            if (enemy == null)
                return;

            // Sát thương giảm dần theo khoảng cách (ở gần sát thương cao nhất).
            float t = Mathf.Clamp01(travelDistance / Mathf.Max(0.1f, earthBreakMaxFalloffDistance));
            float damageRatio = Mathf.Lerp(1f, earthBreakMinDamageRatio, t);
            float finalDamage = earthBreakMaxDamage * damageRatio;

            enemy.TakeDamage(finalDamage);
            GainEnergy(earthBreakEnergyGain);

            TextDamageManager.Instance?.Show(finalDamage, skill.transform.position);
        }
        #endregion

        #region Skill 1 - Earth Wall
        private void PrewarmEarthWallPool()
        {
            if (earthWallDesign == null)
                return;

            for (int i = 0; i < earthWallPoolSize; i++)
            {
                Skill_EarthWall instance = CreateEarthWallInstance();
                if (instance != null)
                    m_EarthWallPool.Push(instance);
            }
        }

        private Skill_EarthWall CreateEarthWallInstance()
        {
            if (earthWallDesign == null)
                return null;

            Skill_EarthWall skill = Instantiate(earthWallDesign, transform.position, Quaternion.identity);
            if (skill == null)
                return null;

            skill.transform.SetParent(null);
            skill.gameObject.SetActive(true);
            skill.Clear();
            skill.gameObject.SetActive(false);

            m_AllEarthWalls.Add(skill);
            return skill;
        }

        private Skill_EarthWall GetEarthWallFromPool()
        {
            while (m_EarthWallPool.Count > 0)
            {
                Skill_EarthWall skill = m_EarthWallPool.Pop();
                if (skill != null)
                    return skill;
            }

            return CreateEarthWallInstance();
        }

        private void ReturnEarthWallToPool(Skill_EarthWall skill)
        {
            if (skill == null)
                return;

            skill.Clear();
            skill.gameObject.SetActive(false);

            if (!m_EarthWallPool.Contains(skill))
                m_EarthWallPool.Push(skill);
        }

        private void FireEarthWall()
        {
            if (earthWallDesign == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_WallTitan)}] Chưa gán EarthWall Design.", this);
                return;
            }

            if (!HasLookTarget)
                return;

            SpendEnergy(earthWallEnergyCost);

            Skill_EarthWall skill = GetEarthWallFromPool();
            if (skill == null)
                return;

            Vector2 spawnPos = LookTarget;
            Vector2 dir = spawnPos - (Vector2)transform.position;
            float angle = dir.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f
                : 0f;

            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            skill.transform.SetParent(null);
            skill.gameObject.SetActive(true);
            skill.Initialize(spawnPos, rotation, ReturnEarthWallToPool);
            skill.StartSkill();
        }
        #endregion

        #region Skill 2 - Option Skill 1 (Stone Fortress / Guardian Wall / Earth Prison)
        private void PrewarmEarthPrisonPool()
        {
            if (earthPrisonDesign == null)
                return;

            for (int i = 0; i < earthPrisonPoolSize; i++)
            {
                Skill_EarthPrison instance = CreateEarthPrisonInstance();
                if (instance != null)
                    m_EarthPrisonPool.Push(instance);
            }
        }

        private Skill_EarthPrison CreateEarthPrisonInstance()
        {
            if (earthPrisonDesign == null)
                return null;

            Skill_EarthPrison skill = Instantiate(earthPrisonDesign, transform.position, Quaternion.identity);
            if (skill == null)
                return null;

            skill.transform.SetParent(null);
            skill.gameObject.SetActive(true);
            skill.Clear();
            skill.gameObject.SetActive(false);

            m_AllEarthPrisons.Add(skill);
            return skill;
        }

        private Skill_EarthPrison GetEarthPrisonFromPool()
        {
            while (m_EarthPrisonPool.Count > 0)
            {
                Skill_EarthPrison skill = m_EarthPrisonPool.Pop();
                if (skill != null)
                    return skill;
            }

            return CreateEarthPrisonInstance();
        }

        private void ReturnEarthPrisonToPool(Skill_EarthPrison skill)
        {
            if (skill == null)
                return;

            skill.Clear();
            skill.gameObject.SetActive(false);

            if (!m_EarthPrisonPool.Contains(skill))
                m_EarthPrisonPool.Push(skill);
        }

        private void UseOptionSkill1()
        {
            SpendEnergy(optionSkill1EnergyCost);

            int branch = Context.OptionSkill1 != 0 ? Context.OptionSkill1 : optionSkill1Branch;

            switch (branch)
            {
                case 0:
                    ActivateStoneFortress();
                    break;
                case 1:
                    ActivateGuardianWall();
                    break;
                case 2:
                default:
                    FireEarthPrison();
                    break;
            }
        }

        private void ActivateStoneFortress()
        {
            Heal(stoneFortressHealAmount);

            if (m_StoneFortressCoroutine != null)
                StopCoroutine(m_StoneFortressCoroutine);

            m_StoneFortressCoroutine = StartCoroutine(StoneFortressRoutine());
        }

        private IEnumerator StoneFortressRoutine()
        {
            TankStats bonus = TankStats.Zeroed();
            bonus.Defense = stoneFortressDefenseBonus;
            bonus.MoveSpeed = -stoneFortressMoveSpeedPenalty;
            AddBonusStats(BonusKeyStoneFortress, bonus);

            if (stoneFortressVisual != null)
                stoneFortressVisual.SetActive(true);

            yield return new WaitForSeconds(stoneFortressDuration);

            if (stoneFortressVisual != null)
                stoneFortressVisual.SetActive(false);

            RemoveBonusStats(BonusKeyStoneFortress);
            m_StoneFortressCoroutine = null;
        }

        private void ActivateGuardianWall()
        {
            if (m_GuardianWallCoroutine != null)
                StopCoroutine(m_GuardianWallCoroutine);

            m_GuardianWallCoroutine = StartCoroutine(GuardianWallRoutine());
        }

        private IEnumerator GuardianWallRoutine()
        {
            TankStats bonus = TankStats.Zeroed();
            bonus.Defense = guardianWallDefenseBonus;
            AddBonusStats(BonusKeyGuardianWall, bonus);

            if (guardianWallVisual != null)
                guardianWallVisual.SetActive(true);

            yield return new WaitForSeconds(guardianWallDuration);

            if (guardianWallVisual != null)
                guardianWallVisual.SetActive(false);

            RemoveBonusStats(BonusKeyGuardianWall);
            m_GuardianWallCoroutine = null;
        }

        private void FireEarthPrison()
        {
            if (earthPrisonDesign == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_WallTitan)}] Chưa gán EarthPrison Design.", this);
                return;
            }

            if (!HasLookTarget)
                return;

            Skill_EarthPrison skill = GetEarthPrisonFromPool();
            if (skill == null)
                return;

            Vector2 spawnPos = LookTarget;
            skill.transform.SetParent(null);
            skill.transform.position = spawnPos;
            skill.gameObject.SetActive(true);

            skill.Initialize(spawnPos, ReturnEarthPrisonToPool);
            skill.TargetFound += OnEarthPrisonTargetFound;
            skill.StartSkill();
        }

        private void OnEarthPrisonTargetFound(Skill_EarthPrison skill, EnemyBrain enemy)
        {
            if (enemy == null)
                return;

            enemy.TakeDamage(earthPrisonDamage);

            Collider2D enemyCollider = enemy.Model != null ? enemy.Model.BodyCollider : null;
            if (enemyCollider != null &&
                Collider2DUtility.TryGetRandomOverlapPoint(skill.ColliderRef.Collider, enemyCollider, out Vector2 hitPoint))
            {
                TextDamageManager.Instance?.Show(earthPrisonDamage, hitPoint);
            }
            else
            {
                TextDamageManager.Instance?.Show(earthPrisonDamage, enemy.transform.position);
            }
        }
        #endregion

        #region Skill 3 - Option Skill 2 (Immovable Titan / Earth Guardian / Earthquake)
        private void PrewarmEarthquakePool()
        {
            if (earthquakeDesign == null)
                return;

            for (int i = 0; i < earthquakePoolSize; i++)
            {
                Skill_Earthquake instance = CreateEarthquakeInstance();
                if (instance != null)
                    m_EarthquakePool.Push(instance);
            }
        }

        private Skill_Earthquake CreateEarthquakeInstance()
        {
            if (earthquakeDesign == null)
                return null;

            Skill_Earthquake skill = Instantiate(earthquakeDesign, transform.position, Quaternion.identity);
            if (skill == null)
                return null;

            skill.transform.SetParent(null);
            skill.gameObject.SetActive(true);
            skill.Clear();
            skill.gameObject.SetActive(false);

            m_AllEarthquakes.Add(skill);
            return skill;
        }

        private Skill_Earthquake GetEarthquakeFromPool()
        {
            while (m_EarthquakePool.Count > 0)
            {
                Skill_Earthquake skill = m_EarthquakePool.Pop();
                if (skill != null)
                    return skill;
            }

            return CreateEarthquakeInstance();
        }

        private void ReturnEarthquakeToPool(Skill_Earthquake skill)
        {
            if (skill == null)
                return;

            skill.Clear();
            skill.gameObject.SetActive(false);

            if (!m_EarthquakePool.Contains(skill))
                m_EarthquakePool.Push(skill);
        }

        private void UseOptionSkill2()
        {
            SpendEnergy(optionSkill2EnergyCost);

            int branch = Context.OptionSkill2 != 0 ? Context.OptionSkill2 : optionSkill2Branch;

            switch (branch)
            {
                case 0:
                    ActivateImmovableTitan();
                    break;
                case 1:
                    ActivateEarthGuardian();
                    break;
                case 2:
                default:
                    FireEarthquake();
                    break;
            }
        }

        private void ActivateImmovableTitan()
        {
            if (m_ImmovableCoroutine != null)
                StopCoroutine(m_ImmovableCoroutine);

            m_ImmovableCoroutine = StartCoroutine(ImmovableTitanRoutine());
        }

        private IEnumerator ImmovableTitanRoutine()
        {
            IsImmovable = true;
            if (immovableTitanVisual != null)
                immovableTitanVisual.SetActive(true);

            yield return new WaitForSeconds(immovableTitanDuration);

            if (immovableTitanVisual != null)
                immovableTitanVisual.SetActive(false);

            IsImmovable = false;
            m_ImmovableCoroutine = null;
        }

        private void ActivateEarthGuardian()
        {
            if (m_EarthGuardianCoroutine != null)
                StopCoroutine(m_EarthGuardianCoroutine);

            m_EarthGuardianCoroutine = StartCoroutine(EarthGuardianRoutine());
        }

        private IEnumerator EarthGuardianRoutine()
        {
            TankStats bonus = TankStats.Zeroed();
            bonus.Defense = earthGuardianDefenseBonus;
            AddBonusStats(BonusKeyEarthGuardian, bonus);

            if (earthGuardianVisual != null)
                earthGuardianVisual.SetActive(true);

            yield return new WaitForSeconds(earthGuardianDuration);

            if (earthGuardianVisual != null)
                earthGuardianVisual.SetActive(false);

            RemoveBonusStats(BonusKeyEarthGuardian);
            m_EarthGuardianCoroutine = null;
        }

        private void FireEarthquake()
        {
            if (earthquakeDesign == null)
            {
                Debug.LogWarning($"[{nameof(TankBrain_WallTitan)}] Chưa gán Earthquake Design.", this);
                return;
            }

            if (!HasLookTarget)
                return;

            Skill_Earthquake skill = GetEarthquakeFromPool();
            if (skill == null)
                return;

            Vector2 spawnPos = LookTarget;
            skill.transform.SetParent(null);
            skill.transform.position = spawnPos;
            skill.gameObject.SetActive(true);

            skill.Initialize(spawnPos, ReturnEarthquakeToPool);
            skill.TargetFound += OnEarthquakeTargetFound;
            skill.StartSkill();
        }

        private void OnEarthquakeTargetFound(Skill_Earthquake skill, EnemyBrain enemy)
        {
            if (enemy == null)
                return;

            enemy.TakeDamage(earthquakeDamagePerTick);

            Collider2D enemyCollider = enemy.Model != null ? enemy.Model.BodyCollider : null;
            if (enemyCollider != null &&
                Collider2DUtility.TryGetRandomOverlapPoint(skill.ColliderRef.Collider, enemyCollider, out Vector2 center))
            {
                TextDamageManager.Instance?.Show(earthquakeDamagePerTick, center);
            }
            else
            {
                TextDamageManager.Instance?.Show(earthquakeDamagePerTick, skill.transform.position);
            }
        }
        #endregion

        private void OnDestroy()
        {
            foreach (Skill_EarthBreak skill in m_AllEarthBreaks)
            {
                if (skill != null) Destroy(skill.gameObject);
            }
            m_AllEarthBreaks.Clear();
            m_EarthBreakPool.Clear();

            foreach (Skill_EarthWall skill in m_AllEarthWalls)
            {
                if (skill != null) Destroy(skill.gameObject);
            }
            m_AllEarthWalls.Clear();
            m_EarthWallPool.Clear();

            foreach (Skill_EarthPrison skill in m_AllEarthPrisons)
            {
                if (skill != null) Destroy(skill.gameObject);
            }
            m_AllEarthPrisons.Clear();
            m_EarthPrisonPool.Clear();

            foreach (Skill_Earthquake skill in m_AllEarthquakes)
            {
                if (skill != null) Destroy(skill.gameObject);
            }
            m_AllEarthquakes.Clear();
            m_EarthquakePool.Clear();
        }
    }
}
