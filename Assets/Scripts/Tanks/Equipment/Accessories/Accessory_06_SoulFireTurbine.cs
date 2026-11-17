using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks.Equipment
{
    /// <summary>
    /// Phụ kiện 6 (STT 7 GDD): Động Cơ Tua-Bin Linh Hỏa.
    /// - Chỉ số nền: +25% Tốc độ di chuyển, +15% Giới hạn máu.
    /// - Nội tại: Cứ di chuyển tích lũy 3m sẽ để lại vùng lửa xanh bán kính 1.2m tồn tại 3.5s.
    ///   Đồng minh đi qua +20% Tốc độ di chuyển, kẻ địch đi vào bị giảm 20% tốc độ.
    /// </summary>
    [DisallowMultipleComponent]
    public class Accessory_06_SoulFireTurbine : EquipmentBrain
    {
        private const string BaseStatsKey = "Accessory_06_SoulFireTurbine_BaseStats";

        [Header("Chỉ số nền")]
        [SerializeField, Range(0f, 1f)] private float baseMoveSpeedMultiplier = 0.25f;
        [SerializeField, Range(0f, 1f)] private float baseMaxHealthMultiplier = 0.15f;

        [Header("Nội tại Vệt Lửa Xanh")]
        [SerializeField, Min(0.5f)] private float distancePerZone = 3.0f;
        [SerializeField, Min(0.5f)] private float zoneRadius = 1.2f;
        [SerializeField, Min(0.5f)] private float zoneDuration = 3.5f;
        [SerializeField] private Color zoneColor = new Color(0.1f, 0.6f, 1f, 0.4f); // Xanh dương lam sáng

        private Vector2 m_LastPosition;
        private float m_TraveledDistance;


        protected override void OnEquipped(TankBrain owner)
        {
            base.OnEquipped(owner);
            if (owner != null)
            {
                owner.AddBonusStats(BaseStatsKey, null, new TankStats { MoveSpeed = baseMoveSpeedMultiplier, MaxHealth = baseMaxHealthMultiplier });
            }
            if (owner != null)
            {
                m_LastPosition = owner.transform.position;
            }
            m_TraveledDistance = 0f;
        }

        protected override void OnUnequipped(TankBrain owner)
        {
            if (owner != null)
            {
                owner.RemoveBonusStats(BaseStatsKey);
            }
            base.OnUnequipped(owner);
        }

        public override void OnBrainTick(float deltaTime)
        {
            if (Owner == null || !Owner.IsAlive) return;

            Vector2 currentPos = Owner.transform.position;
            float step = Vector2.Distance(currentPos, m_LastPosition);
            m_LastPosition = currentPos;

            m_TraveledDistance += step;
            if (m_TraveledDistance >= distancePerZone)
            {
                m_TraveledDistance -= distancePerZone;
                SpawnSoulFirePatch(currentPos);
            }
        }

        private void SpawnSoulFirePatch(Vector2 position)
        {
            AccessoryAreaZone.Spawn(position, AccessoryAreaZone.ZoneEffectType.SoulFire, zoneRadius, zoneDuration, zoneColor);
        }
    }
}
