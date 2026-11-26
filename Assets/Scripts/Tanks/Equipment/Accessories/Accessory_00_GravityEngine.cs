using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 0 (STT 1 GDD): Lõi Động Cơ Trọng Lực Địa Mạch.
    /// - Chỉ số nền: +35% Phòng thủ.
    /// - Nội tại: Đứng yên sau 1.5s giúp +35% Phòng thủ và hồi 1.5% Giới hạn máu mỗi giây. Di chuyển sẽ mất buff.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_00_GravityEngine : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_00_GravityEngine_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseDefenseMultiplier = 0.35f;

        [Header("Nội tại Đứng yên")]
        [SerializeField, Min(0.1f)] private float stationaryRequiredTime = 1.5f;
        [SerializeField, Range(0f, 1f)] private float stationaryBonusDefenseMultiplier = 0.35f;
        [SerializeField, Range(0f, 0.2f)] private float stationaryHealthRegenPerSecond = 0.015f;

        private const string StationaryBonusKey = "Acc00_StationaryDefense";
        private Vector2 m_LastPosition;
        private float m_StationaryTimer;
        private bool m_IsStationaryActive;


        protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { Defense = baseDefenseMultiplier });
            }
            if (owner != null)
            {
                m_LastPosition = owner.transform.position;
            }
            m_StationaryTimer = 0f;
            m_IsStationaryActive = false;
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            RemoveStationaryBuff();
            base.OnUnequipped(owner);
        }

        public override void OnBrainTick(float deltaTime)
        {
            if (Owner == null || !Owner.IsAlive) return;

            Vector2 currentPos = Owner.transform.position;
            float movementDelta = Vector2.Distance(currentPos, m_LastPosition);

            if (movementDelta > 0.01f)
            {
                // Có di chuyển -> cập nhật điểm neo, reset bộ đếm và gỡ buff nếu đang có
                m_LastPosition = currentPos;
                m_StationaryTimer = 0f;
                if (m_IsStationaryActive)
                {
                    RemoveStationaryBuff();
                }
            }
            else
            {
                // Đang đứng yên
                m_StationaryTimer += deltaTime;
                if (m_StationaryTimer >= stationaryRequiredTime)
                {
                    if (!m_IsStationaryActive)
                    {
                        ApplyStationaryBuff();
                    }

                    // Hồi 1.5% Max HP mỗi giây
                    float healAmount = Owner.FinalStats.MaxHealth * stationaryHealthRegenPerSecond * deltaTime;
                    Owner.Heal(healAmount);
                }
            }
        }

        private void ApplyStationaryBuff()
        {
            m_IsStationaryActive = true;
            Owner.AddBonusStats(StationaryBonusKey, null, new TankStats { Defense = stationaryBonusDefenseMultiplier });
        }

        private void RemoveStationaryBuff()
        {
            if (m_IsStationaryActive && Owner != null)
            {
                Owner.RemoveBonusStats(StationaryBonusKey);
            }
            m_IsStationaryActive = false;
        }
    }
}
