using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IronWasteland.Enemies;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Water B (Support / DPS mở rộng) - viết lại theo GDD, dùng các cơ chế sẵn có của TankBrain:
    /// - Năng lượng: TankBrain.CurrentEnergy / GainEnergy / SpendEnergy (UseSkill tự trừ theo GetSkillEnergyCost)
    /// - Hồi chiêu: GetBaseCooldown + CooldownReduction trong Stats
    /// - Khiên: TankBrain.AddShield (ShieldEffect)
    /// - Buff chỉ số: TankBrain.AddBonusStats / RemoveBonusStats
    /// - Hồi máu: TankBrain.Heal(amount, false) để sự kiện OnHealed (Shared Healing) luôn kích hoạt
    /// </summary>
    [DisallowMultipleComponent]
    public class TankBrain_WaterB : TankBrain
    {
        [Header("Skill 0 - Healing Water")]
        [SerializeField] private WaterB_HealingProjectile healingWaterPrefab;
        [SerializeField, Min(0.01f)] private float healingCooldown = 0.6f;
        [SerializeField, Min(0f)] private float healingEnergyGain = 10f;          // Hồi 10 năng lượng mỗi lần đánh trúng kẻ địch
        [SerializeField, Min(0f)] private float healingAtkRatio = 0.8f;           // Sát thương: 80% Tấn công
        [SerializeField, Min(0f)] private float healingMaxHpRatio = 0.01f;        //           + 1% Giới hạn máu

        [Header("Skill 1 - Bubble Shield")]
        [SerializeField] private WaterB_BubbleShield bubbleShieldPrefab;
        [SerializeField, Min(0f)] private float shieldEnergyCost = 45f;
        [SerializeField, Min(0.1f)] private float shieldCooldown = 9f;
        [SerializeField, Min(0.1f)] private float shieldDuration = 4f;
        [SerializeField, Min(0f)] private float shieldAtkRatio = 1.2f;            // Khiên: 120% Tấn công
        [SerializeField, Min(0f)] private float shieldMaxHpRatio = 0.10f;         //        + 10% Giới hạn máu
        [SerializeField, Min(0f)] private float shieldBreakHealAtkRatio = 1.0f;   // Hồi khi vỡ: 100% Tấn công
        [SerializeField, Min(0f)] private float shieldBreakHealMaxHpRatio = 0.05f;//            + 5% Giới hạn máu
        [SerializeField, Min(1f)] private float shieldMaxCastRange = 12f;

        [Header("Skill Sub 1 & Sub 2 (thông số từng biến thể)")]
        [SerializeField] private WaterB_Skill1Sub skill1SubDesign;
        [SerializeField] private WaterB_Skill2Sub skill2SubDesign;

        [Header("Skill Sub 1 Prefabs")]
        [SerializeField] private WaterB_BubbleProtectorDome bubbleProtectorPrefab;

        [Header("Life Drain Visual")]
        [Tooltip("Prefab luồng sáng chảy về đồng minh khi hút máu (gắn WaterB_LifeDrainStreamEffect). Để trống = tự tạo hiệu ứng mặc định.")]
        [SerializeField] private GameObject lifeDrainStreamPrefab;
        [SerializeField, Min(1)] private int lifeDrainStreamCount = 2;
        [Tooltip("Prefab vòng tròn hiển thị vùng hồi máu đồng minh khi bật Life Drain (gắn WaterB_LifeDrainRangeVisual). Để trống = tự tạo vòng xanh nhạt mặc định.")]
        [SerializeField] private GameObject lifeDrainRangeVisualPrefab;

        [Header("Skill Sub 2 Prefabs")]
        [SerializeField] private WaterB_MobileAltar mobileAltarPrefab;
        [SerializeField] private WaterB_WaterTurret waterTurretPrefab;
        [SerializeField, Min(1f)] private float maxPlacementRange = 8f;

        [Header("Aqua Assault Visual (2C)")]
        [Tooltip("Prefab HÌNH ẢNH gắn vào giữa Water B khi bật Aqua Assault (gắn WaterB_AquaAssaultVisual). Để trống = tự tạo hiệu ứng mặc định.")]
        [SerializeField] private GameObject aquaAssaultVisualPrefab;

        private const int Skill0Index = 0;
        private const int Skill1Index = 1;
        private const int Skill1SubIndex = 2;
        private const int Skill2SubIndex = 3;

        private const string LifeDrainBonusKey = "WaterB_LifeDrain";
        private const string AquaAssaultBonusKey = "WaterB_AquaAssault";

        private bool m_IsAquaAssaultActive;
        private Coroutine m_AquaAssaultCoroutine;

        private bool m_IsLifeDrainActive;
        private Coroutine m_LifeDrainCoroutine;

        private bool m_IsMobileAltarActive;
        private WaterB_AquaAssaultVisual m_AquaAssaultVisual;
        private WaterB_LifeDrainRangeVisual m_LifeDrainRangeVisual;

        private WaterB_SharedHealingLink m_ActiveLink;
        private readonly List<TankBrain> m_ScanBuffer = new List<TankBrain>();

        /// <summary>Mã định danh riêng của Water B này (dùng làm Id khiên/buff, thay cho GetInstanceID đã lỗi thời).</summary>
        public string InstanceKey { get; } = System.Guid.NewGuid().ToString("N");

        public bool IsAquaAssaultActive => m_IsAquaAssaultActive;
        public bool IsLifeDrainActive => m_IsLifeDrainActive;
        public bool IsMobileAltarActive => m_IsMobileAltarActive;

        // Điểm duy nhất đọc chỉ số cuối của Water B (nếu TankStats đặt tên khác thì chỉ sửa ở đây)
        private float Atk => Stats.Attack;
        private float MaxHp => Stats.MaxHealth;
        private float Scaled(float atkRatio, float maxHpRatio) => Atk * atkRatio + MaxHp * maxHpRatio;

        // ------------------------------------------------------------------
        // Biến thể chọn qua TankBrainContext (0 = mặc định, 1, 2)
        // ------------------------------------------------------------------
        private Skill1WaterVariant GetSkill1Variant()
        {
            switch (Context.OptionSkill1)
            {
                case 1: return Skill1WaterVariant.LifeDrain;
                case 2: return Skill1WaterVariant.BubbleProtector;
                default: return Skill1WaterVariant.SharedHealing;
            }
        }

        private Skill2WaterVariant GetSkill2Variant()
        {
            switch (Context.OptionSkill2)
            {
                case 1: return Skill2WaterVariant.WaterTurrets;
                case 2: return Skill2WaterVariant.AquaAssault;
                default: return Skill2WaterVariant.MobileAltar;
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
                case Skill1Index: return shieldEnergyCost;
                case Skill1SubIndex:
                    return skill1SubDesign != null ? skill1SubDesign.GetEnergyCost(GetSkill1Variant()) : base.GetSkillEnergyCost(index);
                case Skill2SubIndex:
                    return skill2SubDesign != null ? skill2SubDesign.GetEnergyCost(GetSkill2Variant()) : base.GetSkillEnergyCost(index);
                default: return base.GetSkillEnergyCost(index);
            }
        }

        // GDD: năng lượng của kỹ năng thường chỉ tích lũy khi đạn trúng kẻ địch (xử lý ở CastHealingWater)
        protected override float GetSkillEnergyGain(int index) => 0f;

        protected override float GetBaseCooldown(int index)
        {
            switch (index)
            {
                case Skill0Index: return healingCooldown;
                case Skill1Index: return shieldCooldown;
                case Skill1SubIndex:
                    return skill1SubDesign != null ? skill1SubDesign.GetCooldown(GetSkill1Variant()) : base.GetBaseCooldown(index);
                case Skill2SubIndex:
                    return skill2SubDesign != null ? skill2SubDesign.GetCooldown(GetSkill2Variant()) : base.GetBaseCooldown(index);
                default: return base.GetBaseCooldown(index);
            }
        }

        protected override void OnUseSkill(int index)
        {
            bool success;
            switch (index)
            {
                case Skill0Index: success = CastHealingWater(); break;
                case Skill1Index: success = CastBubbleShield(); break;
                case Skill1SubIndex: success = ActivateSkill1Sub(); break;
                case Skill2SubIndex: success = ActivateSkill2Sub(); break;
                default: base.OnUseSkill(index); return;
            }

            // Tung chiêu thất bại (thiếu mục tiêu/prefab...) thì hoàn lại năng lượng đã trừ
            if (!success && index != Skill0Index)
            {
                GainEnergy(GetSkillEnergyCost(index));
            }
        }

        // ------------------------------------------------------------------
        // Dùng chung
        // ------------------------------------------------------------------

        /// <summary>Hồi máu cho 1 xe (kể cả bản thân). Heal(amount,false) -> OnHealed -> Shared Healing tự chia sẻ.</summary>
        public void HealAlly(TankBrain target, float amount)
        {
            if (target == null || amount <= 0f) return;

            target.Heal(amount, false);
            TextDamageManager.Instance?.Show(amount, target.transform.position);
        }

        private Vector3 GetValidPlacementPosition()
        {
            Vector3 mouse = WaterB_Utils.GetMouseWorldPosition();
            float distance = Vector2.Distance(transform.position, mouse);
            if (distance > maxPlacementRange)
            {
                Vector3 direction = (mouse - transform.position).normalized;
                return transform.position + direction * maxPlacementRange;
            }
            return mouse;
        }

        // ------------------------------------------------------------------
        // Kỹ năng thường - Healing Water
        // ------------------------------------------------------------------
        private bool CastHealingWater()
        {
            // Mobile Altar: Water B không thể tấn công
            if (m_IsMobileAltarActive) return false;

            if (healingWaterPrefab == null)
            {
                Debug.LogWarning("[WaterB] Chưa gán healingWaterPrefab nên không thể bắn Healing Water.");
                return false;
            }

            float power = Scaled(healingAtkRatio, healingMaxHpRatio);

            Vector3 mouse = WaterB_Utils.GetMouseWorldPosition();
            Vector3 dir = mouse - transform.position;
            dir.z = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = transform.up;
            dir.Normalize();

            Vector3 spawnPos = transform.position + dir * 0.2f;
            spawnPos.z = 0f;

            float speedMul = 1f;
            float sizeMul = 1f;
            if (m_IsAquaAssaultActive && skill2SubDesign != null)
            {
                speedMul = skill2SubDesign.AssaultProjectileSpeedMultiplier;
                sizeMul = skill2SubDesign.AssaultProjectileSizeMultiplier;
            }

            WaterB_HealingProjectile projectile = Instantiate(healingWaterPrefab, spawnPos, Quaternion.identity);
            projectile.Initialize(this, dir, power, _ => GainEnergy(healingEnergyGain), speedMul, sizeMul);
            return true;
        }

        /// <summary>Đạn Healing Water trúng kẻ địch: tính sát thương cuối (Tỉ lệ sát thương) và áp dụng Life Drain nếu đang bật.</summary>
        public void HandleProjectileDamage(EnemyBrain enemy, float baseDamage)
        {
            if (enemy == null || baseDamage <= 0f) return;

            float damage = baseDamage * (1f + Stats.DamageRate); // Tỉ lệ sát thương (vd. Aqua Assault +15%)
            DealDamage(enemy, damage);
        }

        /// <summary>
        /// Cổng chung cho MỌI sát thương Water B gây ra (bắn thường, trụ nước...): gây sát thương rồi áp dụng Life Drain.
        /// Sát thương truyền vào là sát thương cuối (đã tính hệ số nếu cần).
        /// </summary>
        public void DealDamage(EnemyBrain enemy, float finalDamage)
        {
            if (enemy == null || finalDamage <= 0f) return;

            // Lấy vị trí trước khi TakeDamage vì kẻ địch có thể chết ngay
            Vector3 enemyPos = enemy.transform.position;

            enemy.TakeDamage(finalDamage);
            TextDamageManager.Instance?.Show(finalDamage, enemyPos);

            ApplyLifeDrain(finalDamage, enemyPos);
        }

        // ------------------------------------------------------------------
        // Kỹ năng chuyên biệt - Bubble Shield
        // ------------------------------------------------------------------
        private bool CastBubbleShield()
        {
            if (bubbleShieldPrefab == null) return false;

            TankBrain target = this;
            Vector3 mouse = WaterB_Utils.GetMouseWorldPosition();
            TankBrain picked = WaterB_Utils.FindTankNearPoint(mouse, 1.5f, null);
            if (picked != null && Vector2.Distance(transform.position, picked.transform.position) <= shieldMaxCastRange)
            {
                target = picked;
            }

            float shieldAmount = Scaled(shieldAtkRatio, shieldMaxHpRatio);
            float healOnBreak = Scaled(shieldBreakHealAtkRatio, shieldBreakHealMaxHpRatio);

            WaterB_BubbleShield shield = Instantiate(bubbleShieldPrefab, target.transform.position, Quaternion.identity);
            shield.Initialize(this, target, shieldAmount, shieldDuration, healOnBreak);
            return true;
        }

        // ------------------------------------------------------------------
        // Kỹ năng tùy chọn 1
        // ------------------------------------------------------------------
        private bool ActivateSkill1Sub()
        {
            if (skill1SubDesign == null) return false;

            switch (GetSkill1Variant())
            {
                case Skill1WaterVariant.SharedHealing: return CastSharedHealing();
                case Skill1WaterVariant.LifeDrain: return CastLifeDrain();
                case Skill1WaterVariant.BubbleProtector: return CastBubbleProtector();
            }
            return false;
        }

        private bool CastSharedHealing()
        {
            Vector3 mouse = WaterB_Utils.GetMouseWorldPosition();
            TankBrain ally = WaterB_Utils.FindTankNearPoint(mouse, 1.5f, this);
            if (ally == null) return false;
            if (Vector2.Distance(transform.position, ally.transform.position) > skill1SubDesign.MaxSelectRange) return false;

            if (m_ActiveLink != null) Destroy(m_ActiveLink.gameObject);

            // Prefab không bắt buộc: để trống thì tự tạo sợi dây nước mặc định
            WaterB_SharedHealingLink linkPrefab = skill1SubDesign.SharedHealingLinkPrefab;
            m_ActiveLink = linkPrefab != null
                ? Instantiate(linkPrefab, transform.position, Quaternion.identity)
                : new GameObject("WaterB_SharedHealingLink").AddComponent<WaterB_SharedHealingLink>();
            m_ActiveLink.Initialize(this, ally, skill1SubDesign.SharedPercent, skill1SubDesign.SharedHealingDuration);
            return true;
        }

        private bool CastLifeDrain()
        {
            // +25% Tấn công trong thời gian kích hoạt (percent bonus)
            TankStats percent = TankStats.Zeroed();
            percent.Attack = skill1SubDesign.DrainAttackBonusPercent;
            AddBonusStats(LifeDrainBonusKey, TankStats.Zeroed(), percent);

            m_IsLifeDrainActive = true;
            if (m_LifeDrainCoroutine != null) StopCoroutine(m_LifeDrainCoroutine);
            m_LifeDrainCoroutine = StartCoroutine(LifeDrainRoutine(skill1SubDesign.DrainDuration));

            SpawnLifeDrainRangeVisual(skill1SubDesign.DrainSearchRadius, skill1SubDesign.DrainDuration);
            return true;
        }

        /// <summary>Vòng tròn xanh nhạt thể hiện vùng hồi máu đồng minh của Life Drain (chỉ hình ảnh).</summary>
        private void SpawnLifeDrainRangeVisual(float radius, float duration)
        {
            if (m_LifeDrainRangeVisual != null) Destroy(m_LifeDrainRangeVisual.gameObject); // tung lại thì thay cái cũ

            GameObject go = lifeDrainRangeVisualPrefab != null
                ? Instantiate(lifeDrainRangeVisualPrefab, transform.position, Quaternion.identity)
                : new GameObject("WaterB_LifeDrainRangeVisual");

            WaterB_LifeDrainRangeVisual visual = go.GetComponent<WaterB_LifeDrainRangeVisual>();
            if (visual == null) visual = go.AddComponent<WaterB_LifeDrainRangeVisual>();

            visual.Initialize(this, radius, duration);
            m_LifeDrainRangeVisual = visual;
        }

        private IEnumerator LifeDrainRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            m_IsLifeDrainActive = false;
            m_LifeDrainCoroutine = null;
            RemoveBonusStats(LifeDrainBonusKey);
        }

        /// <summary>Life Drain: 35% sát thương gây ra -> hồi cho đồng minh (kể cả bản thân) có máu thấp nhất xung quanh.</summary>
        private void ApplyLifeDrain(float damageDealt, Vector3 fromPosition)
        {
            if (!m_IsLifeDrainActive || skill1SubDesign == null) return;

            float heal = damageDealt * skill1SubDesign.DrainPercent;
            if (heal <= 0f) return;

            WaterB_Utils.GetTanksInRadius(transform.position, skill1SubDesign.DrainSearchRadius, m_ScanBuffer);

            TankBrain lowest = this;
            float lowestRatio = float.MaxValue;
            for (int i = 0; i < m_ScanBuffer.Count; i++)
            {
                TankBrain t = m_ScanBuffer[i];
                float maxHp = Mathf.Max(1f, t.Stats.MaxHealth);
                float ratio = t.CurrentHealth / maxHp;
                if (ratio < lowestRatio)
                {
                    lowestRatio = ratio;
                    lowest = t;
                }
            }

            HealAlly(lowest, heal);
            SpawnLifeDrainStreams(fromPosition, lowest.transform);
        }

        /// <summary>Life Drain: các luồng sáng chảy từ kẻ địch về người được hồi máu (chỉ hình ảnh).</summary>
        private void SpawnLifeDrainStreams(Vector3 from, Transform target)
        {
            if (target == null) return;

            for (int i = 0; i < lifeDrainStreamCount; i++)
            {
                GameObject go = lifeDrainStreamPrefab != null
                    ? Instantiate(lifeDrainStreamPrefab, from, Quaternion.identity)
                    : new GameObject("WaterB_LifeDrainStream");

                WaterB_LifeDrainStreamEffect stream = go.GetComponent<WaterB_LifeDrainStreamEffect>();
                if (stream == null) stream = go.AddComponent<WaterB_LifeDrainStreamEffect>();
                stream.Initialize(from, target);
            }
        }

        private bool CastBubbleProtector()
        {
            if (bubbleProtectorPrefab == null) return false;

            Vector3 spawnPos = GetValidPlacementPosition();
            float healOnEnd = Scaled(skill1SubDesign.DomeHealAtkRatio, skill1SubDesign.DomeHealMaxHpRatio);

            WaterB_BubbleProtectorDome dome = Instantiate(bubbleProtectorPrefab, spawnPos, Quaternion.identity);
            dome.Initialize(this,
                skill1SubDesign.DomeDuration,
                skill1SubDesign.DomeDefenseBonus,
                skill1SubDesign.DomeAttackBonusPercent,
                healOnEnd);
            return true;
        }

        // ------------------------------------------------------------------
        // Kỹ năng tùy chọn 2
        // ------------------------------------------------------------------
        private bool ActivateSkill2Sub()
        {
            if (skill2SubDesign == null) return false;

            switch (GetSkill2Variant())
            {
                case Skill2WaterVariant.MobileAltar: return CastMobileAltar();
                case Skill2WaterVariant.WaterTurrets: return CastWaterTurrets();
                case Skill2WaterVariant.AquaAssault: return CastAquaAssault();
            }
            return false;
        }

        private bool CastMobileAltar()
        {
            if (mobileAltarPrefab == null) return false;

            m_IsMobileAltarActive = true;

            WaterB_MobileAltar altar = Instantiate(mobileAltarPrefab, transform.position, Quaternion.identity);
            altar.Initialize(this,
                skill2SubDesign.AltarDuration,
                skill2SubDesign.AltarInterval,
                Scaled(skill2SubDesign.AltarHealAtkRatio, skill2SubDesign.AltarHealMaxHpRatio),
                Scaled(skill2SubDesign.AltarShieldAtkRatio, skill2SubDesign.AltarShieldMaxHpRatio),
                skill2SubDesign.AltarShieldDuration,
                () => m_IsMobileAltarActive = false);
            return true;
        }

        private bool CastWaterTurrets()
        {
            if (waterTurretPrefab == null) return false;

            Vector3 center = GetValidPlacementPosition();
            int count = Mathf.Max(1, skill2SubDesign.TurretCount);
            float damage = Atk * skill2SubDesign.TurretDamageAtkRatio;
            float energyPerHit = skill2SubDesign.EnergyRegenPerHit;

            for (int i = 0; i < count; i++)
            {
                Vector3 offset = Vector3.zero;
                if (count > 1)
                {
                    // Xếp các tháp đều quanh điểm đặt
                    float angle = 360f / count * i;
                    offset = Quaternion.Euler(0f, 0f, angle) * Vector3.right * skill2SubDesign.TurretSpacing;
                }

                WaterB_WaterTurret turret = Instantiate(waterTurretPrefab, center + offset, Quaternion.identity);
                turret.Initialize(damage, skill2SubDesign.TurretDuration, skill2SubDesign.TurretAttackInterval,
                    () => GainEnergy(energyPerHit), DealDamage);
            }
            return true;
        }

        private bool CastAquaAssault()
        {
            // +25% Tấn công (percent); +20% giảm hồi chiêu, +15% tỉ lệ sát thương, +20% xuyên giáp (cộng phẳng)
            TankStats flat = TankStats.Zeroed();
            flat.CooldownReduction = skill2SubDesign.AssaultCooldownReduction;
            flat.DamageRate = skill2SubDesign.AssaultDamageBonus;
            flat.ArmorPenetration = skill2SubDesign.AssaultArmorPenetration;

            TankStats percent = TankStats.Zeroed();
            percent.Attack = skill2SubDesign.AssaultAttackBonusPercent;

            AddBonusStats(AquaAssaultBonusKey, flat, percent);

            m_IsAquaAssaultActive = true;
            if (m_AquaAssaultCoroutine != null) StopCoroutine(m_AquaAssaultCoroutine);
            m_AquaAssaultCoroutine = StartCoroutine(AquaAssaultRoutine(skill2SubDesign.AssaultDuration));

            SpawnAquaAssaultVisual(skill2SubDesign.AssaultDuration);
            return true;
        }

        /// <summary>Hình ảnh gắn vào giữa Water B suốt thời gian Aqua Assault (chỉ hình ảnh, không có chức năng).</summary>
        private void SpawnAquaAssaultVisual(float duration)
        {
            if (m_AquaAssaultVisual != null) Destroy(m_AquaAssaultVisual.gameObject); // tung lại thì thay cái cũ

            GameObject go = aquaAssaultVisualPrefab != null
                ? Instantiate(aquaAssaultVisualPrefab, transform.position, Quaternion.identity)
                : new GameObject("WaterB_AquaAssaultVisual");

            WaterB_AquaAssaultVisual visual = go.GetComponent<WaterB_AquaAssaultVisual>();
            if (visual == null) visual = go.AddComponent<WaterB_AquaAssaultVisual>();

            visual.Initialize(this, duration);
            m_AquaAssaultVisual = visual;
        }

        private IEnumerator AquaAssaultRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            m_IsAquaAssaultActive = false;
            m_AquaAssaultCoroutine = null;
            RemoveBonusStats(AquaAssaultBonusKey);
        }

        private void OnDestroy()
        {
            if (m_ActiveLink != null) Destroy(m_ActiveLink.gameObject);
        }
    }
}