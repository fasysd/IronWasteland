using IronWasteland.Enemies;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        [Header("Skill 1 - Design")]
        [SerializeField] private GreenFox_Skill1 skill1Design;

        [Header("Skill 1 - Pool")]
        [SerializeField, Min(0)] private int initialPoolSize = 4;

        [Header("Skill 1 - Combat")]
        [SerializeField, Min(0f)] private float skill1Damage = 30f;
        [SerializeField, Min(0.01f)] private float skill1Cooldown = 1.5f;

        private const int Skill1Index = 1;

        private readonly Stack<GreenFox_Skill1> m_SkillPool = new Stack<GreenFox_Skill1>();
        private readonly HashSet<GreenFox_Skill1> m_AllSkills = new HashSet<GreenFox_Skill1>();

        [Header("Skill 1 Sub (1A / 1B / 1C)")]
        [SerializeField] private GreenFox_Skill1Sub skill1SubDesign;
        [SerializeField] private Sign signPrefab;
        [SerializeField]private GreenFox_BurnEffect burnPrefab;
        [SerializeField] private GreenFox_BurnEffect skill1BEffectPrefab;
        [SerializeField] private GreenFox_MagicTrap trapPrefab;
        [SerializeField, Min(0.01f)] private float skill1SubCooldown = 10f;

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
            if (index == Skill2Index) return skill2Cooldown;
            if (index == Skill1SubIndex) return skill1SubCooldown;

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

        // ==========================================
        // SKILL 0 (ĐÒN ĐÁNH THƯỜNG) POOLING & LOGIC
        // ==========================================
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

        // ==========================================
        // SKILL 1 POOLING & LOGIC
        // ==========================================
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

            Vector2 targetPos;
            if (HasLookTarget)
            {
                targetPos = LookTarget;
            }
            else if (View != null && View.Muzzle != null)
            {
                targetPos = (Vector2)View.Muzzle.position + (Vector2)View.Muzzle.right * 2f;
            }
            else
            {
                targetPos = (Vector2)transform.position + (Vector2)transform.right * 3f;
            }

            GreenFox_Skill1 skill = GetSkillFromPool();
            if (skill == null) return;

            skill.transform.SetParent(null);
            skill.Initialize(targetPos, (enemy) => ApplySignAndDamage(skill, enemy), ReturnSkillToPool);
        }

        private void ApplySignAndDamage(GreenFox_Skill1 skill, EnemyBrain enemy)
        {
            if (enemy == null) return;

            // 1. Gây sát thương
            float baseDamage = skill1Damage;
            if (m_Is2ABuffActive && skill2SubDesign != null)
            {
                baseDamage *= skill2SubDesign.AttackMultiplier;
            }

            enemy.TakeDamage(baseDamage);
            TextDamageManager.Instance?.Show(baseDamage, skill.transform.position);

            // 2. Xử lý đặt Dấu Ấn (Sign)
            int stacksToAdd = m_Is1ABuffActive ? 2 : 1;

            // Tìm Sign đang đi theo chính enemy này
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
                    // Sinh ra 1 Sign duy nhất cho enemy này
                    enemySign = Instantiate(signPrefab, enemy.transform.position, Quaternion.identity);

                    // Gán enemy làm Owner
                    enemySign.SetTarget(enemy);

                    if (stacksToAdd > 1)
                    {
                        enemySign.AddStack(stacksToAdd - 1);
                    }
                }
            }
            else
            {
                // Nếu đã có sẵn Sign -> Cộng dồn trực tiếp vào Stack của Sign đó!
                enemySign.AddStack(stacksToAdd);
            }

            TryApplyBurn(enemy, baseDamage);
        }

        // ==========================================
        // SKILL 2 LOGIC (2A, 2B, 2C)
        // ==========================================
        private void ExecuteSkill2()
        {
            if (skill2SubDesign == null)
            {
                Execute2AMarkExplosion();
                return;
            }

            switch (skill2SubDesign.ActiveVariant)
            {
                case Skill2Variant.Variant_2A:
                    Execute2AMarkExplosion();
                    break;
                case Skill2Variant.Variant_2B:
                    Execute2BFoxTerritory();
                    break;
                case Skill2Variant.Variant_2C:
                    Execute2CNineTailedAssault();
                    break;
            }
        }

        private void Execute2AMarkExplosion()
        {
            Sign[] allSigns = FindObjectsByType<Sign>(FindObjectsInactive.Exclude);
            if (allSigns == null || allSigns.Length == 0) return;

            float damageMultiplier = m_Is1ABuffActive ? 1.5f : 1.0f;

            foreach (Sign sign in allSigns)
            {
                if (sign == null) continue;

                // Lấy EnemyBrain từ kẻ địch đang bị gán dấu ấn
                EnemyBrain enemy = sign.OwnerEnemy;
                // LẤY NGAY VỊ TRÍ CỦA SIGN ĐỂ LÀM TÂM VỤ NỔ
                Vector3 explosionPos = sign.transform.position;

                int stacks = sign.CurrentStacks;
                float finalDamage = skill2BaseDamage * stacks * damageMultiplier;

                if (enemy != null)
                {
                    enemy.TakeDamage(finalDamage);
                }

                TextDamageManager.Instance?.Show(finalDamage, explosionPos);

                // Sinh ra hiệu ứng nổ ngay vị trí của Sign
                if (skill2ExplosionDesign != null)
                {
                    GreenFox_Skill2 explosion = Instantiate(skill2ExplosionDesign, explosionPos, Quaternion.identity);
                    explosion.gameObject.SetActive(true);
                    explosion.Initialize();
                }

                if (enemy != null)
                {
                    TryApplyBurn(enemy, finalDamage);
                }

                Destroy(sign.gameObject);
            }

            m_Is2ABuffActive = true;
            if (m_2ABuffCoroutine != null) StopCoroutine(m_2ABuffCoroutine);
            float buffTime = (skill2SubDesign != null) ? skill2SubDesign.AttackBuffDuration : 5f;
            m_2ABuffCoroutine = StartCoroutine(Buff2ARoutine(buffTime));
        }

        private IEnumerator Buff2ARoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            m_Is2ABuffActive = false;
            m_2ABuffCoroutine = null;
        }

        private void Execute2BFoxTerritory()
        {
            if (territoryPrefab == null || !HasLookTarget) return;

            GreenFox_FoxTerritory territory = Instantiate(territoryPrefab, LookTarget, Quaternion.identity);
            territory.Initialize(
                this,
                skill2SubDesign.TerritoryDuration,
                skill2SubDesign.SlowAmount,
                5f // Truyền số 5 vào lãnh địa
            );
        }

        private void Execute2CNineTailedAssault()
        {
            if (nineTailedPrefab == null || skill2SubDesign == null) return;

            GreenFox_NineTailedAssault assault = Instantiate(nineTailedPrefab);
            assault.Initialize(
                this,
                transform,
                skill2SubDesign.AssaultDuration,
                skill2SubDesign.AttackInterval,
                skill2SubDesign.TailDamage
            );
        }

        // ==========================================
        // HÀM DÙNG CHUNG KÍCH HOẠT HIỆU ỨNG CHÁY (1B)
        // ==========================================
        public void TryApplyBurn(EnemyBrain enemy, float damageSource)
        {
            if (enemy == null || !m_Is1BBuffActive || skill1SubDesign == null) return;

            if (skill1SubDesign.ActiveVariant == Skill1Variant.Variant_1B)
            {
                // Kiểm tra xem kẻ địch đã có hiệu ứng BurnEffect độc lập nào chưa
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

                // Nếu chưa có thì Instantiate Prefab cháy mới
                if (existingBurn == null && burnPrefab != null)
                {
                    GreenFox_BurnEffect burnInstance = Instantiate(burnPrefab, enemy.transform.position, Quaternion.identity);
                    burnInstance.ApplyBurn(
                        enemy,
                        damageSource,
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
                    m_Is1ABuffActive = true;
                    if (m_1ABuffCoroutine != null) StopCoroutine(m_1ABuffCoroutine);
                    m_1ABuffCoroutine = StartCoroutine(Buff1ARoutine(skill1SubDesign.Buff1ADuration));
                    break;

                case Skill1Variant.Variant_1B:
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
                    if (trapPrefab != null && HasLookTarget)
                    {
                        GreenFox_MagicTrap trap = Instantiate(trapPrefab, LookTarget, Quaternion.identity);
                        trap.Initialize(skill1Damage, skill1SubDesign.TrapDamagePercent, skill1SubDesign.RootDuration);
                    }
                    break;
            }
        }

        private IEnumerator Buff1ARoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            m_Is1ABuffActive = false;
            m_1ABuffCoroutine = null;
        }

        private IEnumerator Buff1BRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            m_Is1BBuffActive = false;
            m_1BBuffCoroutine = null;
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