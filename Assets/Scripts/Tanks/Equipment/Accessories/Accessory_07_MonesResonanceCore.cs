using System.Collections.Generic;
using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 7 (STT 8 GDD): Lõi Ma Trận Cộng Hưởng Mones.
    /// - Chỉ số nền: +80 Giới hạn năng lượng, +15% Tỉ lệ giảm hồi chiêu.
    /// - Nội tại: Dùng kỹ năng thường hồi năng lượng sẽ chia sẻ 50% lượng hồi cho đồng minh gần nhất trong 7m
    ///   và cho họ +10% Giảm hồi chiêu trong 4s.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_07_MonesResonanceCore : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_07_MonesResonanceCore_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Min(0f)] private float baseMaxEnergyFlat = 80f;
        [SerializeField, Range(0f, 1f)] private float baseCooldownReduction = 0.15f;

        [Header("Nội tại Cộng Hưởng Đồng Minh")]
        [SerializeField, Range(0f, 1f)] private float sharedEnergyRatio = 0.50f;
        [SerializeField, Min(1f)] private float shareRadius = 7.0f;
        [SerializeField, Range(0f, 0.5f)] private float allyBonusCooldownReduction = 0.10f;
        [SerializeField, Min(1f)] private float allyBuffDuration = 4.0f;

        private const string AllyBuffKey = "Acc07_MonesResonance_CDR";



        protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, new TankStats { MaxEnergy = baseMaxEnergyFlat }, new TankStats { CooldownReduction = baseCooldownReduction });
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

        public override void OnOwnerGainedEnergy(float amount, int skillIndex)
        {
            if (Owner == null || !Owner.IsAlive) return;
            // Chỉ kích hoạt khi hồi năng lượng từ kỹ năng thường (Slot 0)
            if (skillIndex != 0 || amount <= 0f) return;

            TankBrain nearestAlly = FindNearestAlly(Owner.transform.position, shareRadius);
            if (nearestAlly != null && nearestAlly != Owner)
            {
                float sharedAmount = amount * sharedEnergyRatio;
                nearestAlly.GainEnergy(sharedAmount);

                // Cấp buff giảm hồi chiêu cho đồng minh trong 4s
                nearestAlly.AddBonusStats(AllyBuffKey, null, new TankStats { CooldownReduction = allyBonusCooldownReduction });
                StartCoroutine(RemoveAllyBuffAfterDelay(nearestAlly, allyBuffDuration));
            }
        }

        private System.Collections.IEnumerator RemoveAllyBuffAfterDelay(TankBrain ally, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (ally != null)
            {
                ally.RemoveBonusStats(AllyBuffKey);
            }
        }

        private TankBrain FindNearestAlly(Vector2 center, float radius)
        {
            TankBrain[] allTanks = Object.FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            TankBrain nearest = null;
            float minDistance = float.MaxValue;

            for (int i = 0; i < allTanks.Length; i++)
            {
                TankBrain tank = allTanks[i];
                if (tank == null || !tank.IsAlive || tank == Owner) continue;

                float dist = Vector2.Distance(center, tank.transform.position);
                if (dist <= radius && dist < minDistance)
                {
                    minDistance = dist;
                    nearest = tank;
                }
            }
            return nearest;
        }
    }
}
