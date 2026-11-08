using IronWasteland.Enemies;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    /// <summary>
    /// Green Fox (DPS) - viết lại theo GDD, dùng cơ chế sẵn có của TankBrain:
    /// - Năng lượng/hồi chiêu: GetSkillEnergyCost + GetBaseCooldown (UseSkill tự trừ năng lượng, KHÔNG trừ tay nữa)
    /// - Buff chỉ số: AddBonusStats / RemoveBonusStats (percent cho %, flat cho cộng phẳng)
    /// - Sát thương: Tấn công * hệ số kỹ năng * (1 + Tỉ lệ sát thương)
    /// - Dấu ấn: MỌI sát thương Green Fox gây ra qua DamageEnemy đều cộng dồn ấn (trừ nổ ấn và sát thương thưởng cùng đòn)
    /// </summary>
    [DisallowMultipleComponent]
    public class TankBrain_GreenFox : TankBrain
    {
        private const int Skill0Index = 0;
        private const int Skill1Index = 1;
        private const int Skill1SubIndex = 2;
        private const int Skill2Index = 3;

        private const string MarkExplosionBuffKey = "GreenFox_MarkExplosion_AttackBuff";

        [Header("Skill 0 - Fox Fang")]
        [SerializeField] private GreenFox_Skill0 skill0Design;
        [SerializeField, Min(0)] private int skill0InitialPoolSize = 6;
        [SerializeField, Min(0.01f)] private float skill0Cooldown = 0.5f;
        [SerializeField, Min(0f)] private float skill0EnergyGain = 10f;
        [SerializeField, Min(0f)] private float skill0DamagePercent = 100f;      // 100% ATK

        [Header("Skill 1 - Fox Mark")]
        [SerializeField] private GreenFox_Skill1 skill1Design;
        [SerializeField, Min(0)] private int initialPoolSize = 4;
        [SerializeField, Min(0.01f)] private float skill1Cooldown = 6f;
        [SerializeField, Min(0f)] private float skill1EnergyCost = 30f;
        [SerializeField, Min(0.1f)] private float markLifetime = 8f;             // Thời gian tồn tại ấn
        [SerializeField, Min(1)] private int markStacksPerAttack = 1;            // Cộng dồn mỗi đòn đánh
        [SerializeField, Min(0f)] private float markExplosionDamagePercent = 200f; // Nổ ấn: 200% ATK

        [Header("Skill 1 Sub (1A Mark Hunter / 1B Burning Fox / 1C Fox Trap)")]
        [SerializeField] private GreenFox_Skill1Sub skill1SubDesign;
        [SerializeField] private Sign signPrefab;
        [Tooltip("1A: prefab luồng xanh chảy về Green Fox khi hạ gục (script GreenFox_KillStreamEffect). Để trống = tự tạo hiệu ứng mặc định.")]
        [SerializeField] private GameObject killStreamPrefab;
        [SerializeField, Min(1)] private int killStreamCount = 3;
        [SerializeField] private GreenFox_BurnEffect burnPrefab;
        [SerializeField] private GreenFox_BurnEffect skill1BEffectPrefab;
        [SerializeField] private GreenFox_MagicTrap trapPrefab;

        [Header("Skill 2 Sub (2A Mark Explosion / 2B Fox Territory / 2C Nine-Tailed)")]
        [SerializeField] private GreenFox_Skill2Sub skill2SubDesign;
        [SerializeField] private GreenFox_Skill2 skill2ExplosionDesign;
        [SerializeField] private GreenFox_FoxTerritory territoryPrefab;
        [SerializeField] private GreenFox_NineTailedAssault nineTailedPrefab;
        [Tooltip("2C: prefab HÌNH ẢNH 9 đuôi (chỉ để hiển thị, gắn vào xe khi kích hoạt). Để trống = không hiện đuôi.")]
        [SerializeField] private GameObject nineTailsVisualPrefab;

        // Pool Fox Fang
        private readonly Stack<GreenFox_Skill0> m_Skill0Pool = new Stack<GreenFox_Skill0>();
        private readonly HashSet<GreenFox_Skill0> m_AllSkill0s = new HashSet<GreenFox_Skill0>();

        // Pool Fox Mark
        private readonly Stack<GreenFox_Skill1> m_SkillPool = new Stack<GreenFox_Skill1>();
        private readonly HashSet<GreenFox_Skill1> m_AllSkills = new HashSet<GreenFox_Skill1>();

        // Dấu ấn & thiêu đốt đang tồn tại (thay cho FindObjectsByType mỗi lần)
        private readonly Dictionary<EnemyBrain, Sign> m_Signs = new Dictionary<EnemyBrain, Sign>();
        private readonly Dictionary<EnemyBrain, GreenFox_BurnEffect> m_Burns = new Dictionary<EnemyBrain, GreenFox_BurnEffect>();

        private bool m_Is1ABuffActive;
        private Coroutine m_1ABuffCoroutine;

        private bool m_Is1BBuffActive;
        private Coroutine m_1BBuffCoroutine;

        private Coroutine m_2ABuffCoroutine;
        private GreenFox_FoxTerritory m_ActiveTerritory;

        private static FieldInfo s_CooldownField;

        public bool IsMarkHunterActive => m_Is1ABuffActive;
        public bool IsBurningFoxActive => m_Is1BBuffActive;

        // ------------------------------------------------------------------
        // Biến thể chọn qua TankBrainContext (0 = mặc định, 1, 2)
        // ------------------------------------------------------------------
        private Skill1Variant GetSkill1Variant()
        {
            switch (Context.OptionSkill1)
            {
                case 1: return Skill1Variant.Variant_1B;
                case 2: return Skill1Variant.Variant_1C;
                default: return Skill1Variant.Variant_1A;
            }
        }

        private Skill2Variant GetSkill2Variant()
        {
            switch (Context.OptionSkill2)
            {
                case 1: return Skill2Variant.Variant_2B;
                case 2: return Skill2Variant.Variant_2C;
                default: return Skill2Variant.Variant_2A;
            }
        }

        // ------------------------------------------------------------------
        // Năng lượng & hồi chiêu (TankBrain.UseSkill tự áp dụng)
        // ------------------------------------------------------------------
        protected override float GetSkillEnergyCost(int index)
        {
            switch (index)
            {
                case Skill0Index: return 0f;
                case Skill1Index: return skill1EnergyCost;
                case Skill1SubIndex:
                    if (skill1SubDesign == null) break;
                    switch (GetSkill1Variant())
                    {
                        case Skill1Variant.Variant_1A: return skill1SubDesign.Buff1AEnergyCost;
                        case Skill1Variant.Variant_1B: return skill1SubDesign.Buff1BEnergyCost;
                        default: return skill1SubDesign.Buff1CEnergyCost;
                    }
                case Skill2Index:
                    if (skill2SubDesign == null) break;
                    switch (GetSkill2Variant())
                    {
                        case Skill2Variant.Variant_2A: return skill2SubDesign.Buff2AEnergyCost;
                        case Skill2Variant.Variant_2B: return skill2SubDesign.Buff2BEnergyCost;
                        default: return skill2SubDesign.Buff2CEnergyCost;
                    }
            }
            return base.GetSkillEnergyCost(index);
        }

        // GDD: Fox Fang hồi năng lượng khi ĐÁNH TRÚNG (xử lý ở HandleSkill0Hit), không hồi lúc bắn
        protected override float GetSkillEnergyGain(int index) => 0f;

        protected override float GetBaseCooldown(int index)
        {
            switch (index)
            {
                case Skill0Index: return skill0Cooldown;
                case Skill1Index: return skill1Cooldown;
                case Skill1SubIndex:
                    if (skill1SubDesign == null) break;
                    switch (GetSkill1Variant())
                    {
                        case Skill1Variant.Variant_1A: return skill1SubDesign.Buff1ACooldown;
                        case Skill1Variant.Variant_1B: return skill1SubDesign.Buff1BCooldown;
                        default: return skill1SubDesign.Buff1CCooldown;
                    }
                case Skill2Index:
                    if (skill2SubDesign == null) break;
                    switch (GetSkill2Variant())
                    {
                        case Skill2Variant.Variant_2A: return skill2SubDesign.Buff2ACooldown;
                        case Skill2Variant.Variant_2B: return skill2SubDesign.Buff2BCooldown;
                        default: return skill2SubDesign.Buff2CCooldown;
                    }
            }
            return base.GetBaseCooldown(index);
        }

        private void Start()
        {
            PrewarmSkill0Pool();
            PrewarmSkillPool();
        }

        protected override void OnUseSkill(int index)
        {
            bool success;
            switch (index)
            {
                case Skill0Index: FireSkill0(); return;
                case Skill1Index: success = CastFoxMark(); break;
                case Skill1SubIndex: success = ActivateSkill1Sub(); break;
                case Skill2Index: success = ExecuteSkill2(); break;
                default: base.OnUseSkill(index); return;
            }

            // Tung chiêu thất bại (trượt, thiếu prefab...) thì hoàn lại năng lượng và hồi chiêu
            if (!success)
            {
                GainEnergy(GetSkillEnergyCost(index));
                SetSkillCooldownRemaining(index, 0f);
            }
        }

        // ------------------------------------------------------------------
        // Sát thương & hiệu ứng dùng chung
        // ------------------------------------------------------------------

        /// <summary>Sát thương cuối = Tấn công * hệ số * (1 + Tỉ lệ sát thương).</summary>
        public float ScaledDamage(float atkRatio) => Stats.Attack * atkRatio * (1f + Stats.DamageRate);

        /// <summary>
        /// Gây sát thương lên kẻ địch: hiện số, báo Fox Territory (hồi năng lượng nếu hợp lệ), báo hạ gục (1A)
        /// và CỘNG DỒN ẤN (nếu kẻ địch đang có ấn).
        /// addMarkStacks = false cho sát thương của chính nổ ấn và sát thương thưởng của cùng 1 đòn (tránh cộng dồn lặp).
        /// </summary>
        public void DamageEnemy(EnemyBrain enemy, float damage, Vector3 textPosition, bool addMarkStacks = true)
        {
            if (enemy == null || damage <= 0f) return;

            bool wasAlive = enemy.IsAlive;
            Vector3 deathPosition = enemy.transform.position;

            DealDamage(enemy, damage); // Attacker = Green Fox, EnemyBrain tự báo Fox Territory
            TextDamageManager.Instance?.Show(damage, textPosition);


            // Đòn này hạ gục kẻ địch => Mark Hunter (1A)
            if (wasAlive && !enemy.IsAlive) NotifyEnemyKilled(deathPosition, enemy);

            // Mọi sát thương từ Green Fox đều cộng dồn ấn
            if (addMarkStacks && enemy != null && enemy.IsAlive) AddMarkStacks(enemy);
        }


        /// <summary>Trói chân (đặt tốc độ = 0). Tinh anh miễn nhiễm. Trả về true nếu trói được.</summary>
        public bool RootEnemy(EnemyBrain enemy, float duration)
        {
            if (enemy == null || enemy is EliteEnemyBrain) return false;

            GreenFox_EnemyMoveModifier.Apply(enemy, "GreenFox_Root", 0f, duration);
            return true;
        }

        private void SetSkillCooldownRemaining(int index, float value)
        {
            float[] cooldowns = GetCooldownArray();
            if (cooldowns != null && index >= 0 && index < cooldowns.Length)
            {
                cooldowns[index] = Mathf.Max(0f, value);
            }
        }

        private void ReduceSkillCooldown(int index, float amount)
        {
            float[] cooldowns = GetCooldownArray();
            if (cooldowns != null && index >= 0 && index < cooldowns.Length)
            {
                cooldowns[index] = Mathf.Max(0f, cooldowns[index] - amount);
            }
        }

        private float[] GetCooldownArray()
        {
            if (s_CooldownField == null)
            {
                s_CooldownField = typeof(TankBrain).GetField("skillCooldowns", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            return s_CooldownField != null ? s_CooldownField.GetValue(this) as float[] : null;
        }

        // ------------------------------------------------------------------
        // Kỹ năng thường - Fox Fang
        // ------------------------------------------------------------------
        private void PrewarmSkill0Pool()
        {
            if (skill0Design == null) return;
            for (int i = 0; i < skill0InitialPoolSize; i++)
            {
                GreenFox_Skill0 skill = CreateSkill0Instance();
                if (skill != null) m_Skill0Pool.Push(skill);
            }
        }

        private GreenFox_Skill0 CreateSkill0Instance()
        {
            if (skill0Design == null) return null;
            GreenFox_Skill0 skill = Instantiate(skill0Design, transform.position, Quaternion.identity);
            skill.transform.SetParent(null);
            skill.gameObject.SetActive(false);
            skill.Clear();
            m_AllSkill0s.Add(skill);
            return skill;
        }

        private GreenFox_Skill0 GetSkill0FromPool()
        {
            while (m_Skill0Pool.Count > 0)
            {
                GreenFox_Skill0 skill = m_Skill0Pool.Pop();
                if (skill != null) return skill;
            }
            return CreateSkill0Instance();
        }

        private void ReturnSkill0ToPool(GreenFox_Skill0 skill)
        {
            if (skill == null) return;
            skill.Clear();
            skill.gameObject.SetActive(false);
            if (!m_Skill0Pool.Contains(skill)) m_Skill0Pool.Push(skill);
        }

        private void FireSkill0()
        {
            if (skill0Design == null) return;

            Vector2 spawnPos = (View != null && View.Muzzle != null) ? (Vector2)View.Muzzle.position : (Vector2)transform.position;
            Vector2 shootDirection = HasLookTarget ? (LookTarget - spawnPos).normalized : (Vector2)transform.right;

            GreenFox_Skill0 skill0 = GetSkill0FromPool();
            if (skill0 == null) return;

            float damage = ScaledDamage(skill0DamagePercent / 100f);
            skill0.Initialize(this, spawnPos, shootDirection, damage, ReturnSkill0ToPool);
        }

        /// <summary>
        /// Fox Fang trúng kẻ địch: hồi năng lượng, đính kèm thiêu đốt (1B).
        /// (Cộng dồn ấn đã được xử lý trong DamageEnemy cho mọi nguồn sát thương.)
        /// </summary>
        public void HandleSkill0Hit(EnemyBrain enemy)
        {
            if (enemy == null) return;

            // Kiểm tra TRƯỚC khi đòn này đính kèm thiêu đốt mới
            bool wasBurning = IsBurning(enemy);

            GainEnergy(skill0EnergyGain);

            if (wasBurning && m_Is1BBuffActive && skill1SubDesign != null)
            {
                GainEnergy(skill1SubDesign.EnergyOnHitBurnTarget);
            }

            TryApplyBurn(enemy);
        }

        // ------------------------------------------------------------------
        // Kỹ năng chuyên biệt - Fox Mark
        // ------------------------------------------------------------------
        private void PrewarmSkillPool()
        {
            if (skill1Design == null) return;
            for (int i = 0; i < initialPoolSize; i++)
            {
                GreenFox_Skill1 skill = CreateSkillInstance();
                if (skill != null) m_SkillPool.Push(skill);
            }
        }

        private GreenFox_Skill1 CreateSkillInstance()
        {
            if (skill1Design == null) return null;
            GreenFox_Skill1 skill = Instantiate(skill1Design, transform.position, Quaternion.identity);
            skill.transform.SetParent(null);
            skill.gameObject.SetActive(false);
            skill.Clear();
            m_AllSkills.Add(skill);
            return skill;
        }

        private GreenFox_Skill1 GetSkillFromPool()
        {
            while (m_SkillPool.Count > 0)
            {
                GreenFox_Skill1 skill = m_SkillPool.Pop();
                if (skill != null) return skill;
            }
            return CreateSkillInstance();
        }

        private void ReturnSkillToPool(GreenFox_Skill1 skill)
        {
            if (skill == null) return;
            skill.Clear();
            skill.gameObject.SetActive(false);
            if (!m_SkillPool.Contains(skill)) m_SkillPool.Push(skill);
        }

        private bool CastFoxMark()
        {
            if (skill1Design == null || signPrefab == null) return false;

            Vector2 targetPos;
            if (HasLookTarget) targetPos = LookTarget;
            else if (View != null && View.Muzzle != null) targetPos = (Vector2)View.Muzzle.position + (Vector2)View.Muzzle.right * 2f;
            else targetPos = (Vector2)transform.position + (Vector2)transform.right * 3f;

            GreenFox_Skill1 skill = GetSkillFromPool();
            if (skill == null) return false;

            bool found = false;
            skill.transform.SetParent(null);
            skill.Initialize(targetPos, enemy =>
            {
                found = true;
                ApplyMark(enemy);
            }, ReturnSkillToPool);

            return found; // trượt thì hoàn lại năng lượng và hồi chiêu
        }

        private void ApplyMark(EnemyBrain enemy)
        {
            if (enemy == null || signPrefab == null) return;

            // Đã có ấn: chỉ làm mới thời gian tồn tại
            if (m_Signs.TryGetValue(enemy, out Sign existing) && existing != null)
            {
                existing.Refresh(markLifetime);
                return;
            }

            Sign sign = Instantiate(signPrefab, enemy.transform.position, Quaternion.identity);
            sign.SetTarget(enemy, markLifetime, OnSignRemoved);
            sign.UpdateVisualState(m_Is1ABuffActive);
            m_Signs[enemy] = sign;
        }

        private void OnSignRemoved(Sign sign)
        {
            EnemyBrain key = null;
            bool found = false;
            foreach (var kv in m_Signs)
            {
                if (kv.Value == sign)
                {
                    key = kv.Key;
                    found = true;
                    break;
                }
            }
            if (found) m_Signs.Remove(key);
        }

        /// <summary>Khi Green Fox gây sát thương lên mục tiêu có dấu ấn: cộng dồn; đủ tối đa thì nổ.</summary>
        private void AddMarkStacks(EnemyBrain enemy)
        {
            if (!m_Signs.TryGetValue(enemy, out Sign sign) || sign == null) return;

            int stacks = (m_Is1ABuffActive && skill1SubDesign != null) ? skill1SubDesign.Buff1AStackAmount : markStacksPerAttack;
            sign.AddStack(stacks);

            if (sign.IsFull) TriggerMarkExplosion(sign, enemy);
        }

        private void TriggerMarkExplosion(Sign sign, EnemyBrain enemy)
        {
            if (sign == null || enemy == null) return;

            // Mark Hunter: nổ 300% ATK (thay cho 200%)
            float percent = (m_Is1ABuffActive && skill1SubDesign != null)
                ? skill1SubDesign.Buff1AExplosionDamagePercent
                : markExplosionDamagePercent;

            Vector3 explosionPos = sign.transform.position;

            // Sát thương nổ ấn KHÔNG cộng dồn ấn (addMarkStacks = false)
            DamageEnemy(enemy, ScaledDamage(percent / 100f), explosionPos, false);
            SpawnExplosionEffect(explosionPos);

            TryApplyBurn(enemy);
            Destroy(sign.gameObject);
        }

        private void SpawnExplosionEffect(Vector3 position)
        {
            if (skill2ExplosionDesign == null) return;

            GreenFox_Skill2 explosion = Instantiate(skill2ExplosionDesign, position, Quaternion.identity);
            explosion.gameObject.SetActive(true);
            explosion.Initialize();
        }

        // ------------------------------------------------------------------
        // Kỹ năng tùy chọn 1
        // ------------------------------------------------------------------
        private bool ActivateSkill1Sub()
        {
            if (skill1SubDesign == null) return false;

            switch (GetSkill1Variant())
            {
                case Skill1Variant.Variant_1A: return Cast1AMarkHunter();
                case Skill1Variant.Variant_1B: return Cast1BBurningFox();
                case Skill1Variant.Variant_1C: return Cast1CFoxTrap();
            }
            return false;
        }

        private bool Cast1AMarkHunter()
        {
            m_Is1ABuffActive = true;
            UpdateAllSignsState(true);

            if (m_1ABuffCoroutine != null) StopCoroutine(m_1ABuffCoroutine);
            m_1ABuffCoroutine = StartCoroutine(Buff1ARoutine(skill1SubDesign.Buff1ADuration));
            return true;
        }

        private bool Cast1BBurningFox()
        {
            m_Is1BBuffActive = true;
            if (m_1BBuffCoroutine != null) StopCoroutine(m_1BBuffCoroutine);
            m_1BBuffCoroutine = StartCoroutine(Buff1BRoutine(skill1SubDesign.BurnDuration));

            if (skill1BEffectPrefab != null)
            {
                GreenFox_BurnEffect aura = Instantiate(skill1BEffectPrefab, transform.position, Quaternion.identity);
                aura.Initialize(transform, skill1SubDesign.BurnDuration);
            }
            return true;
        }

        private bool Cast1CFoxTrap()
        {
            if (trapPrefab == null) return false;

            Vector2 trapPosition = HasLookTarget ? LookTarget : (Vector2)transform.position + (Vector2)transform.right * 2f;
            GreenFox_MagicTrap trap = Instantiate(trapPrefab, trapPosition, Quaternion.identity);

            trap.Initialize(
                this,
                ScaledDamage(skill1SubDesign.TrapDamagePercent / 100f),
                ScaledDamage(skill1SubDesign.EliteTrapDamagePercent / 100f),
                skill1SubDesign.RootDuration,
                skill1SubDesign.TrapDuration);
            return true;
        }

        private IEnumerator Buff1ARoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            m_Is1ABuffActive = false;
            m_1ABuffCoroutine = null;
            UpdateAllSignsState(false);
        }

        private IEnumerator Buff1BRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            m_Is1BBuffActive = false;
            m_1BBuffCoroutine = null;
        }

        private void UpdateAllSignsState(bool is1AActive)
        {
            foreach (var kv in m_Signs)
            {
                if (kv.Value != null) kv.Value.UpdateVisualState(is1AActive);
            }
        }

        // ------------------------------------------------------------------
        // Thiêu đốt (Burning Fox 1B)
        // ------------------------------------------------------------------
        private bool IsBurning(EnemyBrain enemy)
        {
            return enemy != null
                && m_Burns.TryGetValue(enemy, out GreenFox_BurnEffect burn)
                && burn != null
                && burn.IsBurning;
        }

        /// <summary>Đính kèm thiêu đốt (25% ATK mỗi 0.5s trong 6s) nếu đang bật Burning Fox. Đã cháy thì làm mới.</summary>
        public void TryApplyBurn(EnemyBrain enemy)
        {
            if (enemy == null || !m_Is1BBuffActive || skill1SubDesign == null || burnPrefab == null) return;

            float tickDamage = ScaledDamage(skill1SubDesign.BurnDamagePercent);

            if (m_Burns.TryGetValue(enemy, out GreenFox_BurnEffect existing) && existing != null)
            {
                existing.Refresh(tickDamage, skill1SubDesign.BurnDuration);
                return;
            }

            GreenFox_BurnEffect burn = Instantiate(burnPrefab, enemy.transform.position, Quaternion.identity);
            m_Burns[enemy] = burn;
            burn.ApplyBurn(this, enemy, tickDamage, skill1SubDesign.BurnDuration, skill1SubDesign.BurnInterval, OnBurnEnded);
        }

        private void OnBurnEnded(GreenFox_BurnEffect burn)
        {
            EnemyBrain key = null;
            bool found = false;
            foreach (var kv in m_Burns)
            {
                if (kv.Value == burn)
                {
                    key = kv.Key;
                    found = true;
                    break;
                }
            }
            if (found) m_Burns.Remove(key);
        }

        // ------------------------------------------------------------------
        // Hạ gục kẻ địch (Mark Hunter) - được hệ thống kẻ địch gọi khi 1 kẻ địch chết
        // ------------------------------------------------------------------
        public void NotifyEnemyKilled(Vector3 enemyPosition, EnemyBrain enemy)
        {
            if (!m_Is1ABuffActive || skill1SubDesign == null) return;

            SpawnKillStreams(enemyPosition);
            ReduceSkillCooldown(Skill1Index, skill1SubDesign.Buff1ACooldownReductionOnKill);
        }

        /// <summary>Mark Hunter (1A): các luồng xanh chảy từ chỗ kẻ địch chết về Green Fox.</summary>
        private void SpawnKillStreams(Vector3 from)
        {
            for (int i = 0; i < killStreamCount; i++)
            {
                GameObject go = killStreamPrefab != null
                    ? Instantiate(killStreamPrefab, from, Quaternion.identity)
                    : new GameObject("GreenFox_KillStream");

                GreenFox_KillStreamEffect stream = go.GetComponent<GreenFox_KillStreamEffect>();
                if (stream == null) stream = go.AddComponent<GreenFox_KillStreamEffect>();
                stream.Initialize(from, transform);
            }
        }

        // ------------------------------------------------------------------
        // Kỹ năng tùy chọn 2
        // ------------------------------------------------------------------
        private bool ExecuteSkill2()
        {
            if (skill2SubDesign == null) return false;

            switch (GetSkill2Variant())
            {
                case Skill2Variant.Variant_2A: return Execute2AMarkExplosion();
                case Skill2Variant.Variant_2B: return Execute2BFoxTerritory();
                case Skill2Variant.Variant_2C: return Execute2CNineTailedAssault();
            }
            return false;
        }

        private bool Execute2AMarkExplosion()
        {
            // Lấy các ấn trong vùng (bản sao vì nổ ấn sẽ làm thay đổi danh sách)
            var signsInRange = new List<Sign>();
            float range = skill2SubDesign.MarkExplosionRange;

            foreach (var kv in m_Signs)
            {
                if (kv.Key == null || kv.Value == null) continue;
                if (Vector2.Distance(transform.position, kv.Key.transform.position) <= range) signsInRange.Add(kv.Value);
            }

            if (signsInRange.Count == 0) return false; // không có ấn thì không tung chiêu

            float damagePerStack = ScaledDamage(skill2SubDesign.DamagePerStackPercent / 100f);

            foreach (Sign sign in signsInRange)
            {
                if (sign == null) continue;

                EnemyBrain enemy = sign.OwnerEnemy;
                Vector3 explosionPos = sign.transform.position;
                float damage = damagePerStack * sign.CurrentStacks; // 20% ATK mỗi cộng dồn

                if (enemy != null)
                {
                    // Sát thương nổ ấn KHÔNG cộng dồn ấn (ấn đang bị tiêu hao)
                    DamageEnemy(enemy, damage, explosionPos, false);
                    TryApplyBurn(enemy);
                }

                SpawnExplosionEffect(explosionPos);
                Destroy(sign.gameObject);
            }

            // +30% Tấn công trong 6s (percent bonus)
            TankStats percent = TankStats.Zeroed();
            percent.Attack = skill2SubDesign.AttackBuffPercent;
            AddBonusStats(MarkExplosionBuffKey, TankStats.Zeroed(), percent);

            if (m_2ABuffCoroutine != null) StopCoroutine(m_2ABuffCoroutine);
            m_2ABuffCoroutine = StartCoroutine(Buff2ARoutine(skill2SubDesign.AttackBuffDuration));
            return true;
        }

        private IEnumerator Buff2ARoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            RemoveBonusStats(MarkExplosionBuffKey);
            m_2ABuffCoroutine = null;
        }

        private bool Execute2BFoxTerritory()
        {
            if (territoryPrefab == null) return false;

            Vector2 spawnPos = HasLookTarget ? LookTarget : (Vector2)transform.position + (Vector2)transform.right * 3f;
            GreenFox_FoxTerritory territory = Instantiate(territoryPrefab, spawnPos, Quaternion.identity);

            territory.Initialize(
                this,
                skill2SubDesign.TerritoryDuration,
                skill2SubDesign.SlowAmount,
                skill2SubDesign.SpeedBoostPercent,
                skill2SubDesign.EnergyPerHit,
                skill2SubDesign.MaxEnergyGain);

            m_ActiveTerritory = territory;
            return true;
        }

        private bool Execute2CNineTailedAssault()
        {
            if (nineTailedPrefab == null) return false;

            GreenFox_NineTailedAssault assault = Instantiate(nineTailedPrefab);
            SpawnNineTailsVisual(skill2SubDesign.AssaultDuration);

            assault.Initialize(
                this,
                transform,
                skill2SubDesign.AssaultDuration,
                skill2SubDesign.AttackInterval,
                skill2SubDesign.TailCount,
                ScaledDamage(skill2SubDesign.TailDamagePercent / 100f));
            return true;
        }

        /// <summary>Hình ảnh 9 đuôi (không có chức năng): gắn vào xe suốt thời gian 2C rồi tự biến mất.</summary>
        private void SpawnNineTailsVisual(float duration)
        {
            if (nineTailsVisualPrefab == null) return;

            GameObject go = Instantiate(nineTailsVisualPrefab, transform.position, Quaternion.identity);
            GreenFox_NineTailsVisual visual = go.GetComponent<GreenFox_NineTailsVisual>();
            if (visual == null) visual = go.AddComponent<GreenFox_NineTailsVisual>();

            visual.Initialize(this, duration);
        }

        private void OnDestroy()
        {
            foreach (GreenFox_Skill0 skill0 in m_AllSkill0s)
            {
                if (skill0 != null) Destroy(skill0.gameObject);
            }
            m_AllSkill0s.Clear();
            m_Skill0Pool.Clear();

            foreach (GreenFox_Skill1 skill in m_AllSkills)
            {
                if (skill != null) Destroy(skill.gameObject);
            }
            m_AllSkills.Clear();
            m_SkillPool.Clear();
        }
    }
}