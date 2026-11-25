using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 1 (STT 2 GDD): Lõi Huyết Tinh Sinh Học.
    /// - Chỉ số nền: +15% Tấn công, +15% Giới hạn máu.
    /// - Nội tại: Chuyển 12% sát thương gây ra thành máu hồi phục cho bản thân và 1 xe đồng minh thấp máu nhất trong phạm vi 5m.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_01_BioBloodCore : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_01_BioBloodCore_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseAttackMultiplier = 0.15f;
        [SerializeField, Range(0f, 1f)] private float baseMaxHealthMultiplier = 0.15f;

        [Header("Nội tại Hút máu & Hồi phục đồng minh")]
        [SerializeField, Range(0f, 1f)] private float lifestealRatio = 0.12f;
        [SerializeField, Min(1f)] private float allySearchRadius = 5.0f;


        protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { Attack = baseAttackMultiplier, MaxHealth = baseMaxHealthMultiplier });
            }
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            base.OnUnequipped(owner);
        }

        public override void OnOwnerDealtDamage(DamageInfo info)
        {
            if (Owner == null || !Owner.IsAlive) return;
            if (!info.IsTankSkill) return;
            if (info.FinalDamage <= 0f) return;

            float healAmount = info.FinalDamage * lifestealRatio;
            if (healAmount <= 0f) return;

            // Hồi máu cho bản thân
            Owner.Heal(healAmount);

            // Tìm 1 xe đồng minh thấp máu nhất trong phạm vi 5m
            TankBrain lowestAlly = FindLowestHealthAlly(Owner.transform.position, allySearchRadius);
            if (lowestAlly != null && lowestAlly != Owner)
            {
                lowestAlly.Heal(healAmount);
            }
        }

        private TankBrain FindLowestHealthAlly(Vector2 center, float radius)
        {
            TankBrain[] allTanks = Object.FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            TankBrain best = null;
            float lowestHealthRatio = float.MaxValue;

            for (int i = 0; i < allTanks.Length; i++)
            {
                TankBrain tank = allTanks[i];
                if (tank == null || !tank.IsAlive) continue;
                if (tank == Owner) continue;

                float dist = Vector2.Distance(center, tank.transform.position);
                if (dist <= radius)
                {
                    float ratio = tank.CurrentHealth / Mathf.Max(1f, tank.FinalStats.MaxHealth);
                    if (ratio < lowestHealthRatio)
                    {
                        lowestHealthRatio = ratio;
                        best = tank;
                    }
                }
            }
            return best;
        }
    }
}
