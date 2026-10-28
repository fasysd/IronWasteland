using IronWasteland.Enemies;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace IronWasteland.Tanks.GreenFox
{
    [DisallowMultipleComponent]
    public class TankBrain_GreenFox : TankBrain
    {
        [Header("Skill 0 (Basic Attack) - Design")]
        [SerializeField] private GreenFox_Skill0 skill0Design;
        [SerializeField, Min(0)] private int skill0InitialPoolSize = 6;
        [SerializeField, Min(0f)] private float skill0Damage = 15f;
        [SerializeField, Min(0.01f)] private float skill0Cooldown = 0.5f;

        private const int Skill0Index = 0;
        private readonly Stack<GreenFox_Skill0> m_Skill0Pool = new Stack<GreenFox_Skill0>();
        private readonly HashSet<GreenFox_Skill0> m_AllSkill0s = new HashSet<GreenFox_Skill0>();

        [Header("Skill 1 - Design (Fox Mark)")]
        [SerializeField] private GreenFox_Skill1 skill1Design;
        [SerializeField, Min(0)] private int initialPoolSize = 4;
        [SerializeField, Min(0f)] private float skill1Damage = 30f;
        [SerializeField, Min(0.01f)] private float skill1Cooldown = 6f;
        [SerializeField, Min(0f)] private float skill1EnergyCost = 30f;

        private const int Skill1Index = 1;

        private readonly Stack<GreenFox_Skill1> m_SkillPool = new Stack<GreenFox_Skill1>();
        private readonly HashSet<GreenFox_Skill1> m_AllSkills = new HashSet<GreenFox_Skill1>();

        [Header("Skill 1 Sub (1A Mark Hunter / 1B Burn / 1C Trap)")]
        [SerializeField] private GreenFox_Skill1Sub skill1SubDesign;
        [SerializeField] private Sign signPrefab;
        [SerializeField] private GreenFox_EnergyAbsorbEffect energyAbsorbPrefab;
        [SerializeField] private GreenFox_BurnEffect burnPrefab;
        [SerializeField] private GreenFox_BurnEffect skill1BEffectPrefab;
        [SerializeField] private GreenFox_MagicTrap trapPrefab;
        private float skill1SubDynamicCooldown = 10f;

        private const int Skill1SubIndex = 2;

        [Header("Skill 2 Sub (2A / 2B / 2C)")]
        [SerializeField] private GreenFox_Skill2Sub skill2SubDesign;
        [SerializeField] private GreenFox_Skill2 skill2ExplosionDesign;
        [SerializeField] private GreenFox_FoxTerritory territoryPrefab;
        [SerializeField] private GreenFox_NineTailedAssault nineTailedPrefab;
        [SerializeField, Min(0f)] private float skill2BaseDamage = 40f;
        [SerializeField, Min(0.01f)] private float skill2Cooldown = 6f;

        private const int Skill2Index = 3;

        private bool m_Is1ABuffActive = false;
        private Coroutine m_1ABuffCoroutine;

        private bool m_Is1BBuffActive = false;
        private Coroutine m_1BBuffCoroutine;

        private bool m_Is2ABuffActive = false;
        private Coroutine m_2ABuffCoroutine;

        protected override float GetBaseCooldown(int index)
        {
            if (index == Skill0Index) return skill0Cooldown;
            if (index == Skill1Index) return skill1Cooldown;
            if (index == Skill2Index)
            {
                if (skill2SubDesign != null)
                {
                    switch (skill2SubDesign.ActiveVariant)
                    {
                        case Skill2Variant.Variant_2A: return skill2SubDesign.Buff2ACooldown; // 12s
                        case Skill2Variant.Variant_2B: return skill2SubDesign.Buff2BCooldown; // 15s
                        case Skill2Variant.Variant_2C: return skill2SubDesign.Buff2CCooldown; // 16s
                    }
                }
                return skill2Cooldown;
            }
            if (index == Skill1SubIndex)
            {
                if (skill1SubDesign == null) return skill1SubDynamicCooldown;

                switch (skill1SubDesign.ActiveVariant)
                {
                    case Skill1Variant.Variant_1A: return skill1SubDesign.Buff1ACooldown;
                    case Skill1Variant.Variant_1B: return skill1SubDesign.Buff1BCooldown;
                    case Skill1Variant.Variant_1C: return skill1SubDesign.Buff1CCooldown;
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
            if (index == Skill0Index) { FireSkill0(); return; }
            if (index == Skill1Index) { FireSkill1(); return; }
            if (index == Skill2Index) { ExecuteSkill2(); return; }
            if (index == Skill1SubIndex) { ActivateSkill1Sub(); return; }

            base.OnUseSkill(index);
        }

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
            skill0.Initialize(this, spawnPos, shootDirection, skill0Damage, ReturnSkill0ToPool);
        }

        public void HandleSkill0Hit(EnemyBrain enemy, float damage)
        {
            if (enemy == null) return;

            bool isTargetBurning = false;
            GreenFox_BurnEffect[] activeBurns = FindObjectsByType<GreenFox_BurnEffect>(FindObjectsInactive.Exclude);
            foreach (var burn in activeBurns)
            {
                if (burn != null && burn.OwnerEnemy == enemy && burn.IsBurning)
                {
                    isTargetBurning = true;
                    break;
                }
            }

            GainEnergy(10f);

            if (isTargetBurning && m_Is1BBuffActive && skill1SubDesign != null)
            {
                GainEnergy(skill1SubDesign.EnergyOnHitBurnTarget);
            }

            TryApplyBurn(enemy, damage);
        }

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

        private void FireSkill1()
        {
            if (skill1Design == null) return;

            if (CurrentEnergy < skill1EnergyCost)
            {
                Debug.Log("[TankBrain_GreenFox] Không đủ năng lượng để sử dụng Fox Mark!");
                return;
            }

            SpendEnergy(skill1EnergyCost);

            Vector2 targetPos;
            if (HasLookTarget) targetPos = LookTarget;
            else if (View != null && View.Muzzle != null) targetPos = (Vector2)View.Muzzle.position + (Vector2)View.Muzzle.right * 2f;
            else targetPos = (Vector2)transform.position + (Vector2)transform.right * 3f;

            GreenFox_Skill1 skill = GetSkillFromPool();
            if (skill == null) return;

            skill.transform.SetParent(null);
            skill.Initialize(targetPos, (enemy) => ApplySignAndDamage(skill, enemy), ReturnSkillToPool);
        }

        private void ApplySignAndDamage(GreenFox_Skill1 skill, EnemyBrain enemy)
        {
            if (enemy == null) return;

            float baseAtk = Stats.Attack;
            float baseDamage = baseAtk * 1.0f;
            if (m_Is2ABuffActive && skill2SubDesign != null)
            {
                baseDamage *= skill2SubDesign.AttackMultiplier;
            }

            enemy.TakeDamage(baseDamage);
            TextDamageManager.Instance?.Show(baseDamage, skill.transform.position);

            int stacksToAdd = m_Is1ABuffActive && skill1SubDesign != null ? skill1SubDesign.Buff1AStackAmount : 1;

            Sign enemySign = null;
            Sign[] activeSigns = FindObjectsByType<Sign>(FindObjectsInactive.Exclude);

            foreach (Sign sign in activeSigns)
            {
                if (sign != null && sign.OwnerEnemy == enemy)
                {
                    enemySign = sign;
                    break;
                }
            }

            if (enemySign == null)
            {
                if (signPrefab != null)
                {
                    enemySign = Instantiate(signPrefab, enemy.transform.position, Quaternion.identity);
                    enemySign.SetTarget(enemy);
                    enemySign.UpdateVisualState(m_Is1ABuffActive);

                    if (stacksToAdd > 1)
                    {
                        enemySign.AddStack(stacksToAdd - 1);
                    }
                }
            }
            else
            {
                enemySign.AddStack(stacksToAdd);

                if (enemySign.CurrentStacks >= enemySign.MaxStacks)
                {
                    TriggerMarkExplosion(enemySign, enemy);
                    return;
                }
            }

            TryApplyBurn(enemy, baseDamage);
        }

        private void TriggerMarkExplosion(Sign sign, EnemyBrain enemy)
        {
            if (sign == null || enemy == null) return;

            Vector3 explosionPos = sign.transform.position;
            float baseAtk = Stats.Attack;

            float bonusDamage = (m_Is1ABuffActive && skill1SubDesign != null) ? (baseAtk * (skill1SubDesign.Buff1AExplosionBonusDamage / 100f)) : 0f;
            float explosionDamage = (baseAtk * 2.0f * sign.CurrentStacks) + bonusDamage;

            enemy.TakeDamage(explosionDamage);
            TextDamageManager.Instance?.Show(explosionDamage, explosionPos);

            if (skill2ExplosionDesign != null)
            {
                GreenFox_Skill2 explosion = Instantiate(skill2ExplosionDesign, explosionPos, Quaternion.identity);
                explosion.gameObject.SetActive(true);
                explosion.Initialize();
            }

            TryApplyBurn(enemy, explosionDamage);
            Destroy(sign.gameObject);
        }

        private void ExecuteSkill2()
        {
            if (skill2SubDesign == null)
            {
                Execute2AMarkExplosion();
                return;
            }

            switch (skill2SubDesign.ActiveVariant)
            {
                case Skill2Variant.Variant_2A: Execute2AMarkExplosion(); break;
                case Skill2Variant.Variant_2B: Execute2BFoxTerritory(); break;
                case Skill2Variant.Variant_2C: Execute2CNineTailedAssault(); break;
            }
        }

        private void Execute2AMarkExplosion()
        {
            float energyCost = skill2SubDesign != null ? skill2SubDesign.Buff2AEnergyCost : 55f;
            if (CurrentEnergy < energyCost)
            {
                Debug.Log("[TankBrain_GreenFox] Không đủ năng lượng để kích hoạt Mark Explosion (2A)!");
                return;
            }
            SpendEnergy(energyCost);

            Sign[] allSigns = FindObjectsByType<Sign>(FindObjectsInactive.Exclude);
            if (allSigns == null || allSigns.Length == 0) return;

            float baseAtk = Stats.Attack;
            float percentPerStack = skill2SubDesign != null ? skill2SubDesign.DamagePerStackPercent : 20f;
            float damageMultiplier = m_Is1ABuffActive ? 1.5f : 1.0f;

            foreach (Sign sign in allSigns)
            {
                if (sign == null) continue;
                EnemyBrain enemy = sign.OwnerEnemy;
                Vector3 explosionPos = sign.transform.position;

                int stacks = sign.CurrentStacks;
                float finalDamage = baseAtk * stacks * (percentPerStack / 100f) * damageMultiplier;

                if (enemy != null) enemy.TakeDamage(finalDamage);
                TextDamageManager.Instance?.Show(finalDamage, explosionPos);

                if (skill2ExplosionDesign != null)
                {
                    GreenFox_Skill2 explosion = Instantiate(skill2ExplosionDesign, explosionPos, Quaternion.identity);
                    explosion.gameObject.SetActive(true);
                    explosion.Initialize();
                }

                if (enemy != null) TryApplyBurn(enemy, finalDamage);
                Destroy(sign.gameObject);
            }

            m_Is2ABuffActive = true;
            if (m_2ABuffCoroutine != null) StopCoroutine(m_2ABuffCoroutine);

            float buffDuration = skill2SubDesign != null ? skill2SubDesign.AttackBuffDuration : 6f;
            float buffPercent = skill2SubDesign != null ? skill2SubDesign.AttackBuffPercent : 0.3f;

            TankStats bonusBuff = TankStats.Zeroed();
            bonusBuff.Attack = baseAtk * buffPercent;
            AddBonusStats("MarkExplosion_AttackBuff", bonusBuff);

            m_2ABuffCoroutine = StartCoroutine(Buff2ARoutine(buffDuration));
        }

        private IEnumerator Buff2ARoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            m_Is2ABuffActive = false;
            RemoveBonusStats("MarkExplosion_AttackBuff");
            m_2ABuffCoroutine = null;
        }

        private void Execute2BFoxTerritory()
        {
            if (territoryPrefab == null || skill2SubDesign == null) return;

            float energyCost = skill2SubDesign.Buff2BEnergyCost;
            if (CurrentEnergy < energyCost)
            {
                Debug.Log("[TankBrain_GreenFox] Không đủ năng lượng để triển khai Fox Territory (2B)!");
                return;
            }

            SpendEnergy(energyCost);

            Vector2 spawnPos = HasLookTarget ? LookTarget : (Vector2)transform.position + (Vector2)transform.right * 3f;
            GreenFox_FoxTerritory territory = Instantiate(territoryPrefab, spawnPos, Quaternion.identity);

            territory.Initialize(
                this,
                skill2SubDesign.TerritoryDuration,
                skill2SubDesign.SlowAmount,
                skill2SubDesign.SpeedBoostPercent,
                skill2SubDesign.EnergyPerHit,
                skill2SubDesign.MaxEnergyGain
            );
        }

        private void Execute2CNineTailedAssault()
        {
            if (nineTailedPrefab == null || skill2SubDesign == null) return;

            float energyCost = skill2SubDesign.Buff2CEnergyCost; // 75 năng lượng
            if (CurrentEnergy < energyCost)
            {
                Debug.Log("[TankBrain_GreenFox] Không đủ năng lượng để kích hoạt Nine-Tailed Assault (2C)!");
                return;
            }

            SpendEnergy(energyCost);

            GreenFox_NineTailedAssault assault = Instantiate(nineTailedPrefab);

            // Tính toán sát thương mỗi đuôi = 15% ATK
            float calculatedTailDamage = Stats.Attack * (skill2SubDesign.TailDamagePercent / 100f);

            assault.Initialize(
                this,
                transform,
                skill2SubDesign.AssaultDuration,   // 6s
                skill2SubDesign.AttackInterval,    // 1s
                calculatedTailDamage               // 15% ATK
            );
        }

        public void TryApplyBurn(EnemyBrain enemy, float damageSource)
        {
            if (enemy == null || !m_Is1BBuffActive || skill1SubDesign == null) return;

            if (skill1SubDesign.ActiveVariant == Skill1Variant.Variant_1B)
            {
                GreenFox_BurnEffect existingBurn = null;
                GreenFox_BurnEffect[] activeBurns = FindObjectsByType<GreenFox_BurnEffect>(FindObjectsInactive.Exclude);

                foreach (GreenFox_BurnEffect burn in activeBurns)
                {
                    if (burn != null && burn.OwnerEnemy == enemy)
                    {
                        existingBurn = burn;
                        break;
                    }
                }

                if (existingBurn == null && burnPrefab != null)
                {
                    GreenFox_BurnEffect burnInstance = Instantiate(burnPrefab, enemy.transform.position, Quaternion.identity);
                    burnInstance.ApplyBurn(
                        enemy,
                        Stats.Attack,
                        skill1SubDesign.BurnDuration,
                        skill1SubDesign.BurnInterval,
                        skill1SubDesign.BurnDamagePercent
                    );
                }
            }
        }

        private void ActivateSkill1Sub()
        {
            if (skill1SubDesign == null) return;

            switch (skill1SubDesign.ActiveVariant)
            {
                case Skill1Variant.Variant_1A:
                    if (CurrentEnergy < skill1SubDesign.Buff1AEnergyCost) return;
                    SpendEnergy(skill1SubDesign.Buff1AEnergyCost);

                    m_Is1ABuffActive = true;
                    UpdateAllSignsState(true);

                    if (m_1ABuffCoroutine != null) StopCoroutine(m_1ABuffCoroutine);
                    m_1ABuffCoroutine = StartCoroutine(Buff1ARoutine(skill1SubDesign.Buff1ADuration));
                    break;

                case Skill1Variant.Variant_1B:
                    if (CurrentEnergy < skill1SubDesign.Buff1BEnergyCost)
                    {
                        Debug.Log("[TankBrain_GreenFox] Không đủ năng lượng để kích hoạt Thiêu đốt ma thuật (1B)!");
                        return;
                    }
                    SpendEnergy(skill1SubDesign.Buff1BEnergyCost);

                    m_Is1BBuffActive = true;
                    if (m_1BBuffCoroutine != null) StopCoroutine(m_1BBuffCoroutine);
                    m_1BBuffCoroutine = StartCoroutine(Buff1BRoutine(skill1SubDesign.BurnDuration));

                    if (skill1BEffectPrefab != null)
                    {
                        GreenFox_BurnEffect tankAura = Instantiate(skill1BEffectPrefab, transform.position, Quaternion.identity);
                        tankAura.Initialize(transform, skill1SubDesign.BurnDuration);
                    }
                    break;

                case Skill1Variant.Variant_1C:
                    if (CurrentEnergy < skill1SubDesign.Buff1CEnergyCost)
                    {
                        Debug.Log("[TankBrain_GreenFox] Không đủ năng lượng để đặt Fox Trap (1C)!");
                        return;
                    }

                    SpendEnergy(skill1SubDesign.Buff1CEnergyCost);

                    if (trapPrefab != null)
                    {
                        Vector2 trapPosition = HasLookTarget ? LookTarget : (Vector2)transform.position + (Vector2)transform.right * 2f;
                        GreenFox_MagicTrap trap = Instantiate(trapPrefab, trapPosition, Quaternion.identity);

                        trap.Initialize(
                            Stats.Attack,
                            skill1SubDesign.TrapDamagePercent,
                            skill1SubDesign.EliteTrapDamagePercent,
                            skill1SubDesign.RootDuration,
                            skill1SubDesign.TrapDuration
                        );
                    }
                    break;
            }
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
            Sign[] activeSigns = FindObjectsByType<Sign>(FindObjectsInactive.Exclude);
            foreach (Sign sign in activeSigns)
            {
                if (sign != null) sign.UpdateVisualState(is1AActive);
            }
        }

        public void NotifyEnemyKilled(Vector3 enemyPosition, EnemyBrain enemy)
        {
            if (m_Is1ABuffActive && energyAbsorbPrefab != null)
            {
                GreenFox_EnergyAbsorbEffect effect = Instantiate(energyAbsorbPrefab);
                effect.Initialize(transform);
            }

            if (skill1SubDesign != null && m_Is1ABuffActive)
            {
                ReduceSkillCooldown(Skill1Index, skill1SubDesign.Buff1ACooldownReductionOnKill);
            }
        }

        private void ReduceSkillCooldown(int index, float amount)
        {
            System.Reflection.FieldInfo field = typeof(TankBrain).GetField("skillCooldowns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                float[] cooldowns = field.GetValue(this) as float[];
                if (cooldowns != null && index >= 0 && index < cooldowns.Length)
                {
                    cooldowns[index] = Mathf.Max(0f, cooldowns[index] - amount);
                }
            }
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