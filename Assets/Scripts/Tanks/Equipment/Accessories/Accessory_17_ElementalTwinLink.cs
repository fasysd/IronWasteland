using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 17 (STT 18 GDD): Lõi Liên Kết Song Tinh Nguyên Tố.
    /// - Chỉ số nền: +15% Giới hạn máu, +25% Phòng thủ.
    /// - Nội tại: Nối cáp với 1 xe đồng minh thấp máu nhất ở gần (10m), chuyển 20% sát thương nhận vào của đồng minh
    ///   sang cho bản thân (trừ trực tiếp vào máu). Khi không có đồng minh xung quanh để nối cáp, sẽ nhận 50% Phòng thủ.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_17_ElementalTwinLink : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_17_ElementalTwinLink_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseMaxHealthMultiplier = 0.15f;
        [SerializeField, Range(0f, 1f)] private float baseDefenseMultiplier = 0.25f;

        [Header("Nội tại Liên Kết Song Tinh")]
        [SerializeField, Min(1f)] private float searchRadius = 10.0f;
        [SerializeField, Range(0f, 1f)] private float sharedDamageRatio = 0.20f;
        [SerializeField, Range(0f, 1f)] private float soloDefenseBonus = 0.50f;

        private const string SoloDefenseBuffKey = "Acc17_SoloDefense";
        private TankBrain m_LinkedAlly;
        private bool m_IsSoloBuffActive;
        private float m_SearchTimer;


        protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { MaxHealth = baseMaxHealthMultiplier, Defense = baseDefenseMultiplier });
            }
            m_SearchTimer = 0f;
            UpdateLink();
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            UnbindCurrentAlly();
            RemoveSoloBuff();
            base.OnUnequipped(owner);
        }

        public override void OnBrainTick(float deltaTime)
        {
            if (Owner == null || !Owner.IsAlive)
            {
                UnbindCurrentAlly();
                RemoveSoloBuff();
                return;
            }

            m_SearchTimer += deltaTime;
            if (m_SearchTimer >= 1.0f)
            {
                m_SearchTimer = 0f;
                UpdateLink();
            }
        }

        private void UpdateLink()
        {
            TankBrain bestAlly = FindLowestHealthAlly(Owner.transform.position, searchRadius);
            if (bestAlly != m_LinkedAlly)
            {
                UnbindCurrentAlly();
                if (bestAlly != null)
                {
                    m_LinkedAlly = bestAlly;
                    m_LinkedAlly.OnTakeDamageEvent += HandleAllyTookDamage;
                    RemoveSoloBuff();
                }
                else
                {
                    ApplySoloBuff();
                }
            }
            else if (bestAlly == null && !m_IsSoloBuffActive)
            {
                ApplySoloBuff();
            }
        }

        private void HandleAllyTookDamage(DamageInfo allyDamageInfo)
        {
            if (Owner == null || !Owner.IsAlive || allyDamageInfo.FinalDamage <= 0f) return;

            // Chuyển 20% sát thương nhận vào của đồng minh sang cho bản thân (trừ trực tiếp vào máu)
            float damageToSelf = allyDamageInfo.FinalDamage * sharedDamageRatio;
            DamageInfo selfDmg = new DamageInfo(
                allyDamageInfo.Attacker,
                Owner,
                damageToSelf,
                isDirectToHealth: true,
                isRedirected: true,
                sourceType: allyDamageInfo.SourceType);

            Owner.TakeDamage(selfDmg);
            TextDamageManager.Instance?.Show(damageToSelf, Owner.transform.position);
        }

        private void ApplySoloBuff()
        {
            if (!m_IsSoloBuffActive && Owner != null)
            {
                m_IsSoloBuffActive = true;
                Owner.AddBonusStats(SoloDefenseBuffKey, null, new TankStats { Defense = soloDefenseBonus });
            }
        }

        private void RemoveSoloBuff()
        {
            if (m_IsSoloBuffActive && Owner != null)
            {
                Owner.RemoveBonusStats(SoloDefenseBuffKey);
            }
            m_IsSoloBuffActive = false;
        }

        private void UnbindCurrentAlly()
        {
            if (m_LinkedAlly != null)
            {
                m_LinkedAlly.OnTakeDamageEvent -= HandleAllyTookDamage;
                m_LinkedAlly = null;
            }
        }

        private TankBrain FindLowestHealthAlly(Vector2 center, float radius)
        {
            TankBrain[] allTanks = Object.FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            TankBrain best = null;
            float lowestRatio = float.MaxValue;

            for (int i = 0; i < allTanks.Length; i++)
            {
                TankBrain tank = allTanks[i];
                if (tank == null || !tank.IsAlive || tank == Owner) continue;

                float dist = Vector2.Distance(center, tank.transform.position);
                if (dist <= radius)
                {
                    float ratio = tank.CurrentHealth / Mathf.Max(1f, tank.FinalStats.MaxHealth);
                    if (ratio < lowestRatio)
                    {
                        lowestRatio = ratio;
                        best = tank;
                    }
                }
            }
            return best;
        }
    }
}
