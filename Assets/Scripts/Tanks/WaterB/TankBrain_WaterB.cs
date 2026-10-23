using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronWasteland.Tanks.WaterB
{
    [DisallowMultipleComponent]
    public class TankBrain_WaterB : TankBrain
    {
        [Header("Skill 0 (Healing Water) - Settings")]
        [SerializeField, Min(0f)] private float baseHealAmount = 30f;
        [SerializeField, Min(0.01f)] private float skill0Cooldown = 0.5f;
        [SerializeField, Min(0f)] private float energyGainPerHeal = 15f;
        [SerializeField] private WaterB_HealingProjectile healingWaterPrefab;

        [Header("Skill 1 (Bubble Shield) - Settings")]
        [SerializeField, Min(0f)] private float shieldCapacity = 100f;
        [SerializeField, Min(0.1f)] private float shieldDuration = 5f;
        [SerializeField, Min(0f)] private float shieldHealOnBreak = 50f;
        [SerializeField, Min(0f)] private float energyCostForShield = 40f;
        [SerializeField] private WaterB_BubbleShield bubbleShieldPrefab;

        [Header("Skill Sub 1 & Sub 2 References")]
        [SerializeField] private WaterB_Skill1Sub skill1SubDesign;
        [SerializeField] private WaterB_Skill2Sub skill2SubDesign;
        [SerializeField, Min(0.01f)] private float skill1SubCooldown = 12f;
        [SerializeField, Min(0.01f)] private float skill2SubCooldown = 10f;

        [Header("Skill Sub 1 Prefabs (Variants)")]
        [SerializeField] private WaterB_BubbleProtectorDome bubbleProtectorPrefab;

        [Header("Skill 2 Prefabs (Option 2)")]
        [SerializeField] private WaterB_MobileAltar mobileAltarPrefab;
        [SerializeField] private WaterB_WaterTurret waterTurretPrefab;
        [SerializeField, Min(0f)] private float energyGainPerTurretHit = 5f;
        [SerializeField, Min(1f)] private float maxPlacementRange = 8f;

        private const int Skill0Index = 0;
        private const int Skill1Index = 1;
        private const int Skill1SubIndex = 2;
        private const int Skill2SubIndex = 3;

        private float m_WaterEnergy = 0f;
        private const float MaxWaterEnergy = 100f;

        private bool m_IsAquaAssaultActive = false;
        private Coroutine m_AquaAssaultCoroutine;

        private bool m_IsLifeDrainActive = false;
        private Coroutine m_LifeDrainCoroutine;

        // Trạng thái Mobile Altar (Skill 2A): Khi active sẽ chặn đánh thường & skill khác
        private bool m_IsMobileAltarActive = false;
        private Coroutine m_MobileAltarCoroutine;

        private WaterB_SharedHealingLink m_ActiveLink;

        public float WaterEnergy => m_WaterEnergy;
        public bool IsAquaAssaultActive => m_IsAquaAssaultActive;
        public bool IsLifeDrainActive => m_IsLifeDrainActive;
        public bool IsMobileAltarActive => m_IsMobileAltarActive;
        public float LifeDrainPercent => skill1SubDesign != null ? skill1SubDesign.DrainPercent : 0.3f;

        protected override float GetBaseCooldown(int index)
        {
            float cdMultiplier = m_IsAquaAssaultActive && skill2SubDesign != null ? skill2SubDesign.CooldownMultiplier : 1f;

            if (index == Skill0Index) return skill0Cooldown * cdMultiplier;
            if (index == Skill1Index) return 3.0f;
            if (index == Skill1SubIndex) return skill1SubCooldown;
            if (index == Skill2SubIndex) return skill2SubCooldown;

            return base.GetBaseCooldown(index);
        }

        protected override void OnUseSkill(int index)
        {
            // Nếu đang trong thời gian triển khai Mobile Altar (Skill 2A) -> Chặn toàn bộ hành động tấn công/dùng skill khác
            if (m_IsMobileAltarActive && index != Skill2SubIndex)
            {
                Debug.Log("[WaterB] Đang trong trạng thái Đài Tế Thủy Triều (Mobile Altar) - Không thể tấn công hoặc dùng kỹ năng!");
                return;
            }

            if (index == Skill0Index) { CastHealingWater(); return; }
            if (index == Skill1Index) { CastBubbleShield(); return; }
            if (index == Skill1SubIndex) { ActivateSkill1Sub(); return; }
            if (index == Skill2SubIndex) { ActivateSkill2Sub(); return; }

            base.OnUseSkill(index);
        }

        private void CastHealingWater()
        {
            float finalPower = m_IsAquaAssaultActive ? baseHealAmount * 1.3f : baseHealAmount;

            if (healingWaterPrefab != null)
            {
                Vector2 mouseScreenPos = Vector2.zero;
                if (Pointer.current != null)
                {
                    mouseScreenPos = Pointer.current.position.ReadValue();
                }

                Vector3 mouseWorldPos = Vector3.zero;
                if (Camera.main != null)
                {
                    Vector3 screenPoint = new Vector3(mouseScreenPos.x, mouseScreenPos.y, Mathf.Abs(Camera.main.transform.position.z));
                    mouseWorldPos = Camera.main.ScreenToWorldPoint(screenPoint);
                }
                mouseWorldPos.z = 0f;

                Vector3 spawnPos = transform.position + (mouseWorldPos - transform.position).normalized * 1.0f;
                spawnPos.z = 0f;

                Vector3 shootDirection = (mouseWorldPos - transform.position);
                if (shootDirection.sqrMagnitude < 0.001f)
                {
                    shootDirection = transform.up;
                }
                shootDirection.z = 0f;

                WaterB_HealingProjectile projectile = Instantiate(healingWaterPrefab, spawnPos, Quaternion.identity);

                projectile.Initialize(this, shootDirection.normalized, finalPower, m_IsAquaAssaultActive, (val) =>
                {
                    m_WaterEnergy = Mathf.Clamp(m_WaterEnergy + energyGainPerHeal, 0f, MaxWaterEnergy);
                });
            }
            else
            {
                m_WaterEnergy = Mathf.Clamp(m_WaterEnergy + energyGainPerHeal, 0f, MaxWaterEnergy);
                TextDamageManager.Instance?.Show(finalPower, transform.position);
                TriggerHealAndShare(finalPower);
            }
        }

        private void CastBubbleShield()
        {
            if (m_WaterEnergy < energyCostForShield) return;
            if (bubbleShieldPrefab == null) return;

            Vector2 mouseScreenPos = Vector2.zero;
            if (Pointer.current != null)
            {
                mouseScreenPos = Pointer.current.position.ReadValue();
            }

            Vector3 mouseWorldPos = Vector3.zero;
            if (Camera.main != null)
            {
                Vector3 screenPoint = new Vector3(mouseScreenPos.x, mouseScreenPos.y, Mathf.Abs(Camera.main.transform.position.z));
                mouseWorldPos = Camera.main.ScreenToWorldPoint(screenPoint);
            }
            mouseWorldPos.z = 0f;

            Collider2D[] hits = Physics2D.OverlapCircleAll(mouseWorldPos, 1.5f);
            Transform targetTransform = transform;

            float minDistance = float.MaxValue;
            foreach (var hit in hits)
            {
                TankBrain tank = hit.GetComponentInParent<TankBrain>();
                if (tank != null)
                {
                    float dist = Vector2.Distance(transform.position, hit.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        targetTransform = tank.transform;
                    }
                }
            }

            m_WaterEnergy -= energyCostForShield;

            WaterB_BubbleShield shield = Instantiate(bubbleShieldPrefab, targetTransform.position, Quaternion.identity);
            shield.Initialize(targetTransform, shieldCapacity, shieldDuration, shieldHealOnBreak, 0.2f);
        }

        private void ActivateSkill1Sub()
        {
            if (skill1SubDesign == null) return;

            switch (skill1SubDesign.ActiveVariant)
            {
                case Skill1WaterVariant.SharedHealing:
                    CastSharedHealing();
                    break;

                case Skill1WaterVariant.LifeDrain:
                    m_IsLifeDrainActive = true;
                    if (m_LifeDrainCoroutine != null) StopCoroutine(m_LifeDrainCoroutine);
                    m_LifeDrainCoroutine = StartCoroutine(LifeDrainRoutine(skill1SubDesign.DrainDuration));
                    break;

                case Skill1WaterVariant.BubbleProtector:
                    if (bubbleProtectorPrefab != null)
                    {
                        // Thả vòm bong bóng bảo vệ bằng chuột
                        Vector3 spawnPos = GetValidPlacementPosition();

                        WaterB_BubbleProtectorDome dome = Instantiate(bubbleProtectorPrefab, spawnPos, Quaternion.identity);
                        dome.Initialize(
                            skill1SubDesign.DomeDuration,
                            skill1SubDesign.DomeHealOnEnd,
                            skill1SubDesign.DomeDefenseBonus
                        );

                        Debug.Log($"[WaterB] Đã triển khai Bubble Protector tại vị trí: {spawnPos}");
                    }
                    else
                    {
                        Debug.LogWarning("[WaterB] Chưa gán BubbleProtector Prefab trong TankBrain_WaterB!");
                    }
                    break;
            }
        }

        private void CastSharedHealing()
        {
            if (skill1SubDesign.SharedHealingLinkPrefab == null) return;

            Vector2 mouseScreenPos = Vector2.zero;
            if (Pointer.current != null)
            {
                mouseScreenPos = Pointer.current.position.ReadValue();
            }

            Vector3 mouseWorldPos = Vector3.zero;
            if (Camera.main != null)
            {
                Vector3 screenPoint = new Vector3(mouseScreenPos.x, mouseScreenPos.y, Mathf.Abs(Camera.main.transform.position.z));
                mouseWorldPos = Camera.main.ScreenToWorldPoint(screenPoint);
            }
            mouseWorldPos.z = 0f;

            Collider2D[] hits = Physics2D.OverlapCircleAll(mouseWorldPos, 1.5f);
            TankBrain bestAllyTank = null;
            float minDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                TankBrain tank = hit.GetComponentInParent<TankBrain>();
                if (tank != null && tank != this)
                {
                    float dist = Vector2.Distance(transform.position, hit.transform.position);
                    if (dist <= skill1SubDesign.MaxSelectRange && dist < minDistance)
                    {
                        minDistance = dist;
                        bestAllyTank = tank;
                    }
                }
            }

            if (bestAllyTank != null)
            {
                if (m_ActiveLink != null)
                {
                    Destroy(m_ActiveLink.gameObject);
                }

                m_ActiveLink = Instantiate(skill1SubDesign.SharedHealingLinkPrefab, transform.position, Quaternion.identity);
                m_ActiveLink.Initialize(this, bestAllyTank, skill1SubDesign.SharedPercent, skill1SubDesign.SharedHealingDuration);
            }
        }

        public void TriggerHealAndShare(float amount)
        {
            Heal(amount);

            if (m_ActiveLink != null)
            {
                m_ActiveLink.TriggerSharedHeal(this, amount);
            }
        }

        private IEnumerator LifeDrainRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            m_IsLifeDrainActive = false;
            m_LifeDrainCoroutine = null;
        }

        private void ActivateSkill2Sub()
        {
            if (skill2SubDesign == null) return;

            switch (skill2SubDesign.ActiveVariant)
            {
                case Skill2WaterVariant.MobileAltar:
                    if (mobileAltarPrefab != null)
                    {
                        // Kích hoạt trạng thái cấm tấn công trong thời gian triển khai Mobile Altar
                        float altarDuration = skill2SubDesign.AltarDuration > 0f ? skill2SubDesign.AltarDuration : 8f;
                        m_IsMobileAltarActive = true;

                        if (m_MobileAltarCoroutine != null) StopCoroutine(m_MobileAltarCoroutine);
                        m_MobileAltarCoroutine = StartCoroutine(MobileAltarStateRoutine(altarDuration));

                        // Sinh Mobile Altar ngay dưới chân Water B
                        WaterB_MobileAltar altar = Instantiate(mobileAltarPrefab, transform.position, Quaternion.identity);
                        altar.Initialize(transform, skill2SubDesign.AltarDuration, skill2SubDesign.AltarInterval, skill2SubDesign.AltarHealPerTick);

                        Debug.Log($"[WaterB] Triển khai Mobile Altar dưới chân! Khóa tấn công trong {altarDuration} giây.");
                    }
                    break;

                case Skill2WaterVariant.WaterTurrets:
                    if (waterTurretPrefab != null)
                    {
                        Vector3 spawnPosition = GetValidPlacementPosition();
                        WaterB_WaterTurret turret = Instantiate(waterTurretPrefab, spawnPosition, Quaternion.identity);

                        turret.Initialize(() =>
                        {
                            m_WaterEnergy = Mathf.Clamp(m_WaterEnergy + energyGainPerTurretHit, 0f, MaxWaterEnergy);
                        });
                    }
                    break;

                case Skill2WaterVariant.AquaAssault:
                    m_IsAquaAssaultActive = true;
                    if (m_AquaAssaultCoroutine != null) StopCoroutine(m_AquaAssaultCoroutine);
                    m_AquaAssaultCoroutine = StartCoroutine(AquaAssaultRoutine(skill2SubDesign.AssaultDuration));
                    break;
            }
        }

        private IEnumerator MobileAltarStateRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            m_IsMobileAltarActive = false;
            m_MobileAltarCoroutine = null;
            Debug.Log("[WaterB] Mobile Altar kết thúc. Water B có thể tấn công bình thường trở lại.");
        }

        private Vector3 GetValidPlacementPosition()
        {
            Vector2 mouseScreenPos = Vector2.zero;

            if (Pointer.current != null)
            {
                mouseScreenPos = Pointer.current.position.ReadValue();
            }

            Vector3 mouseWorldPos = Vector3.zero;
            if (Camera.main != null)
            {
                Vector3 screenPoint = new Vector3(mouseScreenPos.x, mouseScreenPos.y, Mathf.Abs(Camera.main.transform.position.z));
                mouseWorldPos = Camera.main.ScreenToWorldPoint(screenPoint);
            }

            mouseWorldPos.z = 0f;

            float distance = Vector2.Distance(transform.position, mouseWorldPos);
            if (distance > maxPlacementRange)
            {
                Vector3 direction = (mouseWorldPos - transform.position).normalized;
                return transform.position + direction * maxPlacementRange;
            }

            return mouseWorldPos;
        }

        private IEnumerator AquaAssaultRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            m_IsAquaAssaultActive = false;
            m_AquaAssaultCoroutine = null;
        }
    }
}