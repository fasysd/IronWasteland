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



        private TankBrain m_BuffedAlly;

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
            ClearCurrentAllyBuff();
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            base.OnUnequipped(owner);
        }

        public override void OnBrainTick(float deltaTime)
        {
            if (Owner == null || !Owner.IsAlive)
            {
                ClearCurrentAllyBuff();
                return;
            }

            // Luôn áp dụng +10% CDR cho đồng minh gần nhất trong phạm vi 7m khi đủ điều kiện
            TankBrain nearest = FindNearestAlly(Owner.transform.position, shareRadius);
            if (nearest != m_BuffedAlly)
            {
                ClearCurrentAllyBuff();
                m_BuffedAlly = nearest;
                if (m_BuffedAlly != null)
                {
                    m_BuffedAlly.AddBonusStats(AllyBuffKey, null, new TankStats { CooldownReduction = allyBonusCooldownReduction });
                }
            }
        }

        private void ClearCurrentAllyBuff()
        {
            if (m_BuffedAlly != null)
            {
                m_BuffedAlly.RemoveBonusStats(AllyBuffKey);
                m_BuffedAlly = null;
            }
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
