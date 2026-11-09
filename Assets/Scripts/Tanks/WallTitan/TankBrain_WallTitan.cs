using IronWasteland.Core;
using IronWasteland.Enemies;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.WallTitan
{
    /// <summary>
    /// TankBrain cụ thể cho xe tăng "Wall Titan" (Tanker / Support):
    /// - Slot 0: Earth Break — 50% ATK + 50% DEF (&lt;3m), 20% ATK + 10% DEF (&gt;7m), hồi 10 EN, CD 0.6s.
    /// - Slot 1: Earth Wall — 45 EN, CD 10s, tường tồn tại 4s.
    /// - Slot 2: Kỹ năng tùy chọn 1 (0: Stone Fortress, 1: Guardian Wall, 2: Earth Prison).
    /// - Slot 3: Kỹ năng tùy chọn 2 (0: Immovable Titan, 1: Earth Guardian, 2: Earthquake).
    /// Toàn bộ thông số lấy theo GDD mục 4.3.
    /// </summary>
    [DisallowMultipleComponent]
    public class TankBrain_WallTitan : TankBrain, ISkillBranchConfigurable
    {
        private const int SkillEarthBreakIndex = 0;
        private const int SkillEarthWallIndex = 1;
        private const int SkillOption1Index = 2;
        private const int SkillOption2Index = 3;

        private const string BonusKeyStoneFortress = "WallTitan_StoneFortress";

        #region Inspector - Skill 0: Earth Break
        [Header("Skill 0 - Earth Break  [GDD: 50% ATK + 50% DEF gần, 20% ATK + 10% DEF xa, +10 EN, CD 0.6s]")]
        [Tooltip("GameObject mẫu inactive của đạn Earth Break.")]
        [SerializeField] private Skill_EarthBreak earthBreakDesign;
        [SerializeField, Min(0)] private int earthBreakPoolSize = 8;
        [SerializeField, Min(0f)] private float earthBreakNearAttackRatio = 0.5f;
        [SerializeField, Min(0f)] private float earthBreakNearDefenseRatio = 0.5f;
        [SerializeField, Min(0f)] private float earthBreakFarAttackRatio = 0.2f;
        [SerializeField, Min(0f)] private float earthBreakFarDefenseRatio = 0.1f;
        [SerializeField, Min(0f)] private float earthBreakNearRange = 3f;
        [SerializeField, Min(0f)] private float earthBreakFarRange = 7f;
        [SerializeField, Min(0f)] private float earthBreakEnergyGain = 10f;
        [SerializeField, Min(0.01f)] private float earthBreakCooldown = 0.6f;
        #endregion

        #region Inspector - Skill 1: Earth Wall
        [Header("Skill 1 - Earth Wall  [GDD: 45 EN, CD 10s, tồn tại 4s]")]
        [Tooltip("GameObject mẫu inactive của bức tường đá.")]
        [SerializeField] private Skill_EarthWall earthWallDesign;
        [SerializeField, Min(0)] private int earthWallPoolSize = 2;
        [SerializeField, Min(0f)] private float earthWallEnergyCost = 45f;
        [SerializeField, Min(0.01f)] private float earthWallCooldown = 10f;
        [SerializeField, Min(0.1f)] private float earthWallDuration = 4f;
        #endregion

        #region Inspector - Skill 2: Option Skill 1
        [Header("Skill 2 - Option Skill 1 (0: Stone Fortress, 1: Guardian Wall, 2: Earth Prison)")]
        [SerializeField, Range(0, 2)] private int optionSkill1Branch = 0;

        [Header("Option 1.0 - Stone Fortress  [50 EN, CD 12s, khiên 30% HP 8s, +50% DEF, -30% Tốc độ]")]
        [SerializeField] private GameObject stoneFortressVisual;
        [SerializeField, Min(0f)] private float stoneFortressEnergyCost = 50f;
        [SerializeField, Min(0.01f)] private float stoneFortressCooldown = 12f;
        [SerializeField, Min(0.1f)] private float stoneFortressDuration = 8f;
        [SerializeField, Range(0f, 2f)] private float stoneFortressShieldHpRatio = 0.30f;
        [SerializeField, Range(0f, 2f)] private float stoneFortressDefensePercent = 0.50f;
        [SerializeField, Range(0f, 1f)] private float stoneFortressSpeedPenalty = 0.30f;

        [Header("Option 1.1 - Guardian Wall  [50 EN, CD 10s, khiên 30% HP 5s, chuyển 35% sát thương]")]
        [SerializeField] private GameObject guardianWallVisual;
        [SerializeField, Min(0f)] private float guardianWallEnergyCost = 50f;
        [SerializeField, Min(0.01f)] private float guardianWallCooldown = 10f;
        [SerializeField, Min(0.1f)] private float guardianWallDuration = 5f;
        [SerializeField, Range(0f, 2f)] private float guardianWallShieldHpRatio = 0.30f;
        [SerializeField, Range(0f, 1f)] private float guardianWallRedirectRatio = 0.35f;
        [Tooltip("Bán kính quanh con trỏ để chọn đồng minh; ngoài bán kính này thì tự buff mình.")]
        [SerializeField, Min(0.5f)] private float allyCastRange = 8f;

        [Header("Option 1.2 - Earth Prison  [60 EN, CD 14s, tồn tại 4s, 100% ATK + 150% DEF]")]
        [Tooltip("GameObject mẫu inactive của vòng giam đá.")]
        [SerializeField] private Skill_EarthPrison earthPrisonDesign;
        [SerializeField, Min(0)] private int earthPrisonPoolSize = 2;
        [SerializeField, Min(0f)] private float earthPrisonAirAttackRatio = 1.0f;
        [SerializeField, Min(0f)] private float earthPrisonDefenseRatio = 1.5f;
        [SerializeField, Min(0f)] private float earthPrisonEnergyCost = 60f;
        [SerializeField, Min(0.01f)] private float earthPrisonCooldown = 14f;
        [SerializeField, Min(0.1f)] private float earthPrisonDuration = 4f;
        #endregion

        #region Inspector - Skill 3: Option Skill 2
        [Header("Skill 3 - Option Skill 2 (0: Immovable Titan, 1: Earth Guardian, 2: Earthquake)")]
        [SerializeField, Range(0, 2)] private int optionSkill2Branch = 0;

        [Header("Option 2.0 - Immovable Titan  [hồi 40 EN, CD 10s, 5s, +50 DEF, miễn khống chế]")]
        [SerializeField] private GameObject immovableTitanVisual;
        [SerializeField, Min(0f)] private float immovableTitanEnergyGain = 40f;
        [SerializeField, Min(0.01f)] private float immovableTitanCooldown = 10f;
        [SerializeField, Min(0.1f)] private float immovableTitanDuration = 5f;
        [SerializeField, Min(0f)] private float immovableTitanDefenseFlat = 50f;

        [Header("Option 2.1 - Earth Guardian  [50 EN, CD 10s, 5s, +35 DEF, chuyển 35% sát thương]")]
        [SerializeField] private GameObject earthGuardianVisual;
        [SerializeField, Min(0f)] private float earthGuardianEnergyCost = 50f;
        [SerializeField, Min(0.01f)] private float earthGuardianCooldown = 10f;
        [SerializeField, Min(0.1f)] private float earthGuardianDuration = 5f;
        [SerializeField, Min(0f)] private float earthGuardianDefenseFlat = 35f;
        [SerializeField, Range(0f, 1f)] private float earthGuardianRedirectRatio = 0.35f;

        [Header("Option 2.2 - Earthquake  [65 EN, CD 14s, 6s, tick 0.5s, 60% ATK + 100% DEF, -35% Tốc độ]")]
        [Tooltip("GameObject mẫu inactive của vùng chấn động.")]
        [SerializeField] private Skill_Earthquake earthquakeDesign;
        [SerializeField, Min(0)] private int earthquakePoolSize = 2;
        [SerializeField, Min(0f)] private float earthquakeAttackRatio = 0.6f;
        [SerializeField, Min(0f)] private float earthquakeDefenseRatio = 1.0f;
        [SerializeField, Range(0f, 1f)] private float earthquakeSlowPercent = 0.35f;
        [SerializeField, Min(0.1f)] private float earthquakeSlowDuration = 0.6f;
        [SerializeField, Min(0f)] private float earthquakeEnergyCost = 65f;
        [SerializeField, Min(0.01f)] private float earthquakeCooldown = 14f;
        [SerializeField, Min(0.1f)] private float earthquakeDuration = 6f;
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

        // --- Runtime States ---
        private readonly List<SharedDamageLink> m_SharedLinks = new List<SharedDamageLink>();
        private Coroutine m_StoneFortressCoroutine;
        private Coroutine m_ImmovableTitanCoroutine;

        /// <summary>Đã đổi nhánh bằng Test API/Skill Arena hay chưa (override context).</summary>
        private bool m_Option1BranchOverridden;
        private bool m_Option2BranchOverridden;

        #region Cooldown
        protected override float GetBaseCooldown(int index)
        {
            switch (index)
            {
                case SkillEarthBreakIndex:
                    return earthBreakCooldown;

                case SkillEarthWallIndex:
                    return earthWallCooldown;

                case SkillOption1Index:
                    switch (ResolveOption1Branch())
                    {
                        case 0: return stoneFortressCooldown;
                        case 1: return guardianWallCooldown;
                        default: return earthPrisonCooldown;
                    }

                case SkillOption2Index:
                    switch (ResolveOption2Branch())
                    {
                        case 0: return immovableTitanCooldown;
                        case 1: return earthGuardianCooldown;
                        default: return earthquakeCooldown;
                    }

                default:
                    return base.GetBaseCooldown(index);
            }
        }

        /// <summary>
        /// Nhánh kỹ năng tùy chọn 1. Context lấy từ TankDefinition là nguồn chuẩn và
        /// 0..2 đều là ID hợp lệ (0 = Stone Fortress), không dùng 0 làm sentinel nữa.
        /// Khi test/arena đổi nhánh lúc chạy thì override được ưu tiên.
        /// </summary>
        private int ResolveOption1Branch()
        {
            if (m_Option1BranchOverridden)
                return Mathf.Clamp(optionSkill1Branch, 0, 2);

            return Mathf.Clamp(Context.OptionSkill1, 0, 2);
        }

        private int ResolveOption2Branch()
        {
            if (m_Option2BranchOverridden)
                return Mathf.Clamp(optionSkill2Branch, 0, 2);

            return Mathf.Clamp(Context.OptionSkill2, 0, 2);
        }

        /// <summary>
        /// Chi phí năng lượng theo GDD. Lớp cha mặc định 30 EN cho slot khác 0 nên phải ghi đè,
        /// nếu không mỗi chiêu bị trừ thêm 30 EN. Immovable Titan không tốn EN mà hồi 40 EN.
        /// </summary>
        protected override float GetSkillEnergyCost(int index)
        {
            switch (index)
            {
                case 1:
                    return earthWallEnergyCost;

                case 2:
                    switch (ResolveOption1Branch())
                    {
                        case 1: return guardianWallEnergyCost;
                        case 2: return earthPrisonEnergyCost;
                        default: return stoneFortressEnergyCost;
                    }

                case 3:
                    switch (ResolveOption2Branch())
                    {
                        case 1: return earthGuardianEnergyCost;
                        case 2: return earthquakeEnergyCost;
                        default: return 0f; // Immovable Titan: hồi 40 EN
                    }

                default:
                    return 0f;
            }
        }

        /// <summary>
        /// GDD: Earth Break chỉ hồi EN khi TRÚNG mục tiêu (+10 trong OnEarthBreakTargetFound),
        /// không hồi lúc bấm. Lớp cha cộng 10 EN ngay khi dùng slot 0 nên phải chặn tại đây,
        /// nếu không mỗi phát trúng sẽ hồi 20 EN và bắn trượt vẫn được 10 EN.
        /// </summary>
        protected override float GetSkillEnergyGain(int index) => 0f;
        #endregion

        [Header("VFX (Assets/Art/Tank/VFX/WallTitan)")]
        [SerializeField] private WallTitanVfx vfx;

        private void Start()
        {
            if (vfx == null)
                vfx = GetComponent<WallTitanVfx>();

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

        #region Damage Helper
        /// <summary>Tấn công × hệ số ATK + Phòng thủ × hệ số DEF, nhân (1 + Tỉ lệ sát thương).</summary>
        private float CalculateRawDamage(float attackRatio, float defenseRatio)
        {
            return (Stats.Attack * attackRatio + Stats.Defense * defenseRatio) * (1f + Stats.DamageRate);
        }

        private void ApplySkillDamage(EnemyBrain enemy, float rawDamage, Vector2 hitPosition)
        {
            if (enemy == null || !enemy.IsAlive)
                return;

            DamageInfo info = new DamageInfo(this, enemy, rawDamage, Stats.ArmorPenetration, hitPosition);
            enemy.TakeDamage(info);
            TextDamageManager.Instance?.Show(info.FinalDamage, hitPosition);
        }

        /// <summary>Tìm đồng minh gần nhất có ColliderOwnerRef sở hữu bởi TankBrain quanh điểm chỉ định; không có thì trả về null.</summary>
        private TankBrain FindTargetTankNear(Vector2 point, float maxDistance, bool includeSelf = false)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(point, maxDistance);

            TankBrain best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                    continue;

                ColliderOwnerRef ownerRef = hit.GetComponent<ColliderOwnerRef>();
                if (ownerRef == null)
                    ownerRef = hit.GetComponentInParent<ColliderOwnerRef>();

                if (ownerRef == null)
                    continue;

                TankBrain candidate = ownerRef.GetOwner<TankBrain>();
                if (candidate == null || !candidate.IsAlive)
                    continue;

                if (!includeSelf && candidate == this)
                    continue;

                float sqr = ((Vector2)candidate.transform.position - point).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                }
            }

            return best;
        }

        private TankBrain FindAllyNear(Vector2 point, float maxDistance) => FindTargetTankNear(point, maxDistance, false);
        #endregion

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

            Vector2 spawnPos = View.Muzzle.position;
            Vector2 direction = LookTarget - spawnPos;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            direction.Normalize();

            Skill_EarthBreak skill = GetEarthBreakFromPool();
            if (skill == null)
                return;

            skill.transform.SetParent(null);

            // Đặt đúng vị trí/hướng TRƯỚC khi bật object: nếu SetActive trước,
            // collider sẽ được kích hoạt tại vị trí cũ trong pool và có thể ăn
            // nhầm mục tiêu hoặc trượt mục tiêu thật.
            skill.Initialize(spawnPos, direction, ReturnEarthBreakToPool);
            skill.TargetFound += OnEarthBreakTargetFound;
            skill.gameObject.SetActive(true);
        }

        private void OnEarthBreakTargetFound(Skill_EarthBreak skill, EnemyBrain enemy, float travelDistance)
        {
            if (enemy == null)
                return;

            // GDD: dưới 3m dùng hệ số gần, trên 7m dùng hệ số xa, ở giữa nội suy tuyến tính.
            float t = Mathf.InverseLerp(earthBreakNearRange, earthBreakFarRange, travelDistance);
            float attackRatio = Mathf.Lerp(earthBreakNearAttackRatio, earthBreakFarAttackRatio, t);
            float defenseRatio = Mathf.Lerp(earthBreakNearDefenseRatio, earthBreakFarDefenseRatio, t);

            ApplySkillDamage(enemy, CalculateRawDamage(attackRatio, defenseRatio), skill.transform.position);

            if (vfx != null)
                vfx.PlayEarthShards(skill.transform.position);

            GainEnergy(earthBreakEnergyGain);
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


            Skill_EarthWall wall = GetEarthWallFromPool();
            if (wall == null)
                return;

            Vector2 delta = LookTarget - (Vector2)transform.position;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            wall.transform.SetParent(null);
            wall.gameObject.SetActive(true);
            wall.Initialize(LookTarget, rotation, ReturnEarthWallToPool);
            wall.Duration = earthWallDuration;
            wall.StartSkill();
        }
        #endregion

        #region Skill 2 - Option Skill 1 (Stone Fortress / Guardian Wall / Earth Prison)
        private void UseOptionSkill1()
        {
            switch (ResolveOption1Branch())
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
            if (m_StoneFortressCoroutine != null)
                StopCoroutine(m_StoneFortressCoroutine);

            m_StoneFortressCoroutine = StartCoroutine(StoneFortressRoutine());
        }

        private IEnumerator StoneFortressRoutine()
        {
            float shieldValue = Stats.MaxHealth * stoneFortressShieldHpRatio;
            AddShield(shieldValue, stoneFortressDuration, BonusKeyStoneFortress, this, "Khiên Đá");

            TankStats flat = TankStats.Zeroed();
            TankStats percent = TankStats.Zeroed();
            percent.Defense = stoneFortressDefensePercent;
            percent.MoveSpeed = -stoneFortressSpeedPenalty;
            AddBonusStats(BonusKeyStoneFortress, flat, percent);

            if (stoneFortressVisual != null)
                stoneFortressVisual.SetActive(true);

            if (vfx != null)
                vfx.SetFortressActive(true);

            yield return new WaitForSeconds(stoneFortressDuration);

            if (stoneFortressVisual != null)
                stoneFortressVisual.SetActive(false);

            if (vfx != null)
                vfx.SetFortressActive(false);

            RemoveBonusStats(BonusKeyStoneFortress);
            m_StoneFortressCoroutine = null;
        }

        private void ActivateGuardianWall()
        {
            // Tìm mục tiêu Tank mà chuột/LookTarget trỏ vào (bao gồm cả bản thân)
            TankBrain target = FindTargetTankNear(LookTarget, allyCastRange, true);
            if (target == null)
            {
                // Nếu không trỏ vào ai (kể cả bản thân) thì coi như không thi triển
                return;
            }

            if (target == this)
            {
                // Trỏ vào bản thân: tự thi triển lên bản thân
                float selfShieldValue = Stats.MaxHealth * guardianWallShieldHpRatio;
                AddShield(selfShieldValue, guardianWallDuration, "WallTitan_GuardianWall_Self", this, "Khiên Hộ Vệ");

                if (vfx != null)
                {
                    vfx.ShowGuardianOnTarget(transform, guardianWallDuration);
                }
            }
            else
            {
                // Trỏ vào đồng minh: tạo khiên cho bản thân và đồng minh, chia sẻ sát thương
                float selfShieldValue = Stats.MaxHealth * guardianWallShieldHpRatio;
                AddShield(selfShieldValue, guardianWallDuration, "WallTitan_GuardianWall_Self", this, "Khiên Hộ Vệ");

                float allyShieldValue = target.Stats.MaxHealth * guardianWallShieldHpRatio;
                target.AddShield(allyShieldValue, guardianWallDuration, "WallTitan_GuardianWall", this, "Khiên Hộ Vệ");
                RegisterSharedDamageLink(target, guardianWallRedirectRatio, guardianWallDuration);

                if (vfx != null)
                {
                    vfx.ShowGuardianOnTarget(transform, guardianWallDuration);
                    vfx.ShowGuardianOnTarget(target.transform, guardianWallDuration);
                }
            }
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

            Skill_EarthPrison prison = GetEarthPrisonFromPool();
            if (prison == null)
                return;

            prison.transform.SetParent(null);
            prison.Initialize(LookTarget, ReturnEarthPrisonToPool);
            prison.Duration = earthPrisonDuration;
            prison.TargetFound += OnEarthPrisonTargetFound;
            prison.gameObject.SetActive(true);
            prison.StartSkill();
        }

        private void OnEarthPrisonTargetFound(Skill_EarthPrison skill, EnemyBrain enemy)
        {
            if (enemy == null)
                return;

            float raw = CalculateRawDamage(earthPrisonAirAttackRatio, earthPrisonDefenseRatio);

            Collider2D enemyCollider = enemy.Model != null ? enemy.Model.BodyCollider : null;
            if (enemyCollider != null &&
                Collider2DUtility.TryGetRandomOverlapPoint(skill.ColliderRef.Collider, enemyCollider, out Vector2 center))
            {
                ApplySkillDamage(enemy, raw, center);
            }
            else
            {
                ApplySkillDamage(enemy, raw, skill.transform.position);
            }
        }

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
        #endregion

        #region Skill 3 - Option Skill 2 (Immovable Titan / Earth Guardian / Earthquake)
        private void UseOptionSkill2()
        {
            switch (ResolveOption2Branch())
            {
                case 0:
                    // GDD ghi "Năng lượng hồi" → kỹ năng này KHÔNG tốn năng lượng mà hồi thêm.
                    GainEnergy(immovableTitanEnergyGain);
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
            if (m_ImmovableTitanCoroutine != null)
                StopCoroutine(m_ImmovableTitanCoroutine);

            m_ImmovableTitanCoroutine = StartCoroutine(ImmovableTitanRoutine());
        }

        private IEnumerator ImmovableTitanRoutine()
        {
            TankStats flat = TankStats.Zeroed();
            flat.Defense = immovableTitanDefenseFlat;
            AddBonusStats(BonusKeyStoneFortress + "_Immovable", flat);

            IsCrowdControlImmune = true;
            ClearAllCrowdControl();

            if (immovableTitanVisual != null)
                immovableTitanVisual.SetActive(true);

            if (vfx != null)
                vfx.SetImmovableActive(true);

            yield return new WaitForSeconds(immovableTitanDuration);

            if (immovableTitanVisual != null)
                immovableTitanVisual.SetActive(false);

            if (vfx != null)
                vfx.SetImmovableActive(false);

            IsCrowdControlImmune = false;
            RemoveBonusStats(BonusKeyStoneFortress + "_Immovable");
            m_ImmovableTitanCoroutine = null;
        }

        private void ActivateEarthGuardian()
        {
            TankBrain ally = FindAllyNear(LookTarget, allyCastRange);
            if (ally == null)
            {
                // Không có ColliderOwnerRef nào có Owner là TankBrain thì thôi.
                return;
            }

            TankStats flat = TankStats.Zeroed();
            flat.Defense = earthGuardianDefenseFlat;
            ally.AddBonusStats("WallTitan_EarthGuardian", flat);
            ally.StartCoroutine(ClearAllyBonusAfterDelay(ally, "WallTitan_EarthGuardian", earthGuardianDuration));

            RegisterSharedDamageLink(ally, earthGuardianRedirectRatio, earthGuardianDuration);

            if (vfx != null)
            {
                vfx.ShowEarthGuardianOnTarget(ally.transform, earthGuardianDuration);
            }
        }

        private void RegisterSharedDamageLink(TankBrain ally, float ratio, float duration)
        {
            if (ally == null || ally == this || ratio <= 0f)
                return;

            SharedDamageLink link = new SharedDamageLink(this, ratio, duration);
            m_SharedLinks.Add(link);
            ally.AddDamageRedirector(link);

            StartCoroutine(UnregisterSharedDamageLinkAfter(link, ally, duration));
        }

        private IEnumerator UnregisterSharedDamageLinkAfter(SharedDamageLink link, TankBrain ally, float duration)
        {
            yield return new WaitForSeconds(duration);

            if (ally != null)
                ally.RemoveDamageRedirector(link);

            m_SharedLinks.Remove(link);
        }

        private IEnumerator ClearBonusAfterDelay(string key, float duration)
        {
            yield return new WaitForSeconds(duration);
            RemoveBonusStats(key);
        }

        private IEnumerator ClearAllyBonusAfterDelay(TankBrain ally, string key, float duration)
        {
            yield return new WaitForSeconds(duration);
            if (ally != null)
                ally.RemoveBonusStats(key);
        }

        private IEnumerator PlayVisualRoutine(GameObject visual, float duration)
        {
            if (visual == null)
                yield break;

            visual.SetActive(true);
            yield return new WaitForSeconds(duration);
            if (visual != null)
                visual.SetActive(false);
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

            Skill_Earthquake quake = GetEarthquakeFromPool();
            if (quake == null)
                return;

            quake.transform.SetParent(null);
            quake.Initialize(LookTarget, ReturnEarthquakeToPool);
            quake.Duration = earthquakeDuration;
            quake.TargetFound += OnEarthquakeTargetFound;
            quake.gameObject.SetActive(true);
            quake.StartSkill();

            if (vfx != null)
                vfx.ShowEarthquake(LookTarget);
        }

        private void OnEarthquakeTargetFound(Skill_Earthquake skill, EnemyBrain enemy)
        {
            if (enemy == null || !enemy.IsAlive)
                return;

            float raw = CalculateRawDamage(earthquakeAttackRatio, earthquakeDefenseRatio);
            ApplySkillDamage(enemy, raw, enemy.transform.position);

            // GDD: kẻ địch trong vùng bị giảm 35% tốc độ.
            enemy.ApplyCrowdControl(CrowdControlType.Slow, earthquakeSlowDuration, earthquakeSlowPercent);
        }

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
        #endregion

        #region Shared Damage Link
        /// <summary>
        /// Liên kết gánh hộ: đồng minh nhận đòn → chuyển <see cref="ratio"/> sát thương gốc sang Wall Titan.
        /// Sát thương chuyển sang được tính là trừ trực tiếp vào máu (IsDirectToHealth), đúng GDD.
        /// </summary>
        private class SharedDamageLink : IDamageRedirector
        {
            private readonly TankBrain m_Protector;
            private readonly float m_Ratio;
            private readonly float m_ExpireTime;

            public SharedDamageLink(TankBrain protector, float ratio, float duration)
            {
                m_Protector = protector;
                m_Ratio = Mathf.Clamp01(ratio);
                m_ExpireTime = Time.time + duration;
            }

            public bool TryRedirect(DamageInfo info, out TankBrain protector, out float sharedAmount)
            {
                protector = null;
                sharedAmount = 0f;

                if (m_Protector == null || !m_Protector.IsAlive)
                    return false;

                if (Time.time > m_ExpireTime)
                    return false;

                protector = m_Protector;
                sharedAmount = info.RawDamage * m_Ratio;
                return sharedAmount > 0f;
            }
        }
        #endregion

        #region Test API (Skill Arena)
        public int Option1Branch
        {
            get => ResolveOption1Branch();
            set
            {
                optionSkill1Branch = Mathf.Clamp(value, 0, 2);
                m_Option1BranchOverridden = true;
                ResetSkillCooldown(SkillOption1Index);
            }
        }

        public int Option2Branch
        {
            get => ResolveOption2Branch();
            set
            {
                optionSkill2Branch = Mathf.Clamp(value, 0, 2);
                m_Option2BranchOverridden = true;
                ResetSkillCooldown(SkillOption2Index);
            }
        }

        public string GetSkillName(int slotIndex)
        {
            switch (slotIndex)
            {
                case SkillEarthBreakIndex: return "Earth Break";
                case SkillEarthWallIndex: return "Earth Wall";

                case SkillOption1Index:
                    switch (ResolveOption1Branch())
                    {
                        case 0: return "Stone Fortress";
                        case 1: return "Guardian Wall";
                        default: return "Earth Prison";
                    }

                case SkillOption2Index:
                    switch (ResolveOption2Branch())
                    {
                        case 0: return "Immovable Titan";
                        case 1: return "Earth Guardian";
                        default: return "Earthquake";
                    }

                default:
                    return $"Skill {slotIndex}";
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
